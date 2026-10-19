using System;
using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.Tightrope.Fire;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.Tightrope.Saws
{
    /// <summary>톱날 종류.</summary>
    public enum SawKind : byte
    {
        Horizontal,
        Vertical,
        CooperativeGate,
    }

    /// <summary>톱날 절단 결과(호스트가 정해 모든 화면에 알림).</summary>
    public readonly struct SawHit
    {
        public readonly NetworkPlayer Player;
        /// <summary>실제로 잘린 부위(020 신체 손상이 확정). 거절됐거나 자를 부위가 없으면 None.</summary>
        public readonly BodyPart Part;
        /// <summary>닿은 위치로 정한 연출용 부위. 실제 손실은 공용 고정 순서를 따른다.</summary>
        public readonly BodyPart Requested;
        public readonly SawKind Kind;
        /// <summary>가로 톱날 번호(0부터). 수직 톱날은 출현 순번(1부터).</summary>
        public readonly int Index;
        /// <summary>닿은 곳(월드).</summary>
        public readonly Vector3 Contact;
        /// <summary>닿은 높이(m, 발바닥 기준, 호스트가 계산).</summary>
        public readonly float ContactHeight;

        public SawHit(NetworkPlayer player, BodyPart part, BodyPart requested, SawKind kind, int index, Vector3 contact, float contactHeight)
        {
            Player = player;
            Part = part;
            Requested = requested;
            Kind = kind;
            Index = index;
            Contact = contact;
            ContactHeight = contactHeight;
        }
    }

    /// <summary>
    /// 외줄 톱날 장애물과 공개 진입점. 규칙: Harness/Project/Decisions/tightrope-course-rules.md #6~#9 (2026-10-03).
    /// - 장애물 순서(<see cref="SawSettings.steps"/>, 레벨 디자인): 호스트가 <see cref="ObstacleSequence"/>로 단계 조건을 보고 가로 톱날 작동·정지, 수직 톱날 출현을 실행한다.
    /// - 가로 톱날: 호스트가 작동·정지 기록을 "게임 시작 후 지난 시간"(<see cref="TightropeRun"/>의 남은 시간, 모든 화면에서 같음)과 함께 공유하고, 위치는 각 화면이 그 기록으로 계산한다.
    /// - 수직 톱날: 호스트가 줄·출현·내려감을 정해 NGO 이름 붙은 메시지로 공유하고, 위치는 각 화면이 시간으로 계산한다.
    ///   뒤의 발판(임시 형태)을 살아 있는 플레이어가 밟으면 톱날이 빠르게 내려간 뒤 사라진다. 눌림 판정(<see cref="IsPlatePressed"/>)과 겉모습은 분리돼 있다.
    /// - 판정: 호스트가 묘기 진행 중에 진행 중인 플레이어(<see cref="PlayerState.Normal"/>)만 본다(도착 완료·사망 무시). 020 신체 손상에 절단을 요청한다(규칙 #9):
    ///   가로·수직 톱날 모두 공용 순서 절단(<see cref="NetworkPlayer.ServerCut"/>)을 사용한다.
    ///   실제로 잘린 부위를 모든 화면에 <see cref="Hit"/>로 알리고 로그로 남긴다. 패널티·탈락·공유는 020이 한다.
    /// - 묘기 재시작(<see cref="TightropeRun.RunRestarted"/>)이면 처음 상태로.
    /// 수치는 <see cref="Settings"/>(<see cref="SawSettings"/>) 한 곳에 모은다. 씬에 코스(<see cref="TightropeCourse"/>·<see cref="TightropeRun"/>)와 함께 둔다.
    /// </summary>
    public sealed partial class TightropeSaws : MonoBehaviour
    {
        const string VerticalMessage = "CurtainCall.Tightrope.Saws.Vertical";
        const string HorizontalMessage = "CurtainCall.Tightrope.Saws.Horizontal";
        const string RequestMessage = "CurtainCall.Tightrope.Saws.Request";
        const string HitMessage = "CurtainCall.Tightrope.Saws.Hit";
        const string GeneratedName = "Generated";

        [Header("겉모습(비우면 기본 도형)")]
        [Tooltip("가로 톱날 모델. 보이는 지름 크기로 만들고, 회전축이 위(+Y)인 프리팹.")]
        [SerializeField] GameObject horizontalPrefab;

        [Tooltip("수직 톱날 모델. 보이는 지름 크기로 만들고, 회전축이 오른쪽(+X)인 프리팹.")]
        [SerializeField] GameObject verticalPrefab;

        [Tooltip("발판 모델. 발판 크기로 만든 프리팹.")]
        [SerializeField] GameObject platePrefab;

        [SerializeField] Color plateColor = new(1f, 0.8f, 0.2f);
        [SerializeField] Color platePressedColor = new(0.3f, 0.9f, 0.3f);

        static TightropeSaws current;

        TightropeCourse course;
        TightropeRun run;
        NetworkSessionManager session;
        CustomMessagingManager messaging;

        Transform[] horizontalViews = Array.Empty<Transform>();

        // 가로 톱날(모든 화면): 작동·정지 기록. 시각은 게임 경과 시간
        HorizontalSawTrack[] tracks = Array.Empty<HorizontalSawTrack>();
        readonly List<(StepAction action, int first, int last, float at)> horizontalCommands = new();
        Transform verticalView, plateView;
        Renderer[] plateRenderers = Array.Empty<Renderer>();
        Material[] plateMaterials = Array.Empty<Material>();

        // 수직 톱날(모든 화면, 시각은 이 화면 기준)
        bool verticalActive;
        int verticalLane;
        int verticalSerial;               // 출현 순번(1부터)
        float verticalSpawnedAt;
        float verticalLoweredAt = -1f;    // 발판이 눌린 시각. 안 눌렸으면 -1

        // 호스트 전용
        ObstacleSequence sequence;
        int verticalStep = -1;            // 지금 수직 톱날을 낸 단계 번호
        readonly Dictionary<int, string> waitReasons = new(); // 단계별 마지막으로 남긴 대기 이유(바뀔 때만 로그)
        float platePressedFor;
        readonly HashSet<(int saw, ulong player)> touching = new();
        readonly HashSet<(int saw, ulong player)> touchingNow = new();
        readonly Dictionary<(int saw, ulong player), float> lastHitAt = new();

        /// <summary>씬의 톱날. 없으면 null.</summary>
        public static TightropeSaws Current
        {
            get
            {
                if (current == null) current = FindAnyObjectByType<TightropeSaws>();
                return current;
            }
        }

        /// <summary>톱날 조정값(외줄 세팅 에셋의 톱날 칸, 쓸 때마다 읽는다).</summary>
        public SawSettings Settings => settings;

        static SawSettings settings => GameSettings.Tightrope.Saws;

        /// <summary>
        /// 톱날 판정이 났을 때(모든 화면). 호스트가 확정한 결과다. 020(신체 손상)이 이 신호로 부위를 잃게 한다.
        /// <see cref="SawHit.Part"/>가 None이면 잘릴 부위가 남아 있지 않은 경우다.
        /// </summary>
        public event Action<SawHit> Hit;

        /// <summary>수직 톱날 출현·눌림·제거처럼 상태가 바뀌었을 때(모든 화면).</summary>
        public event Action VerticalChanged;

        /// <summary>가로 톱날 수.</summary>
        public int HorizontalCount => settings.HorizontalCount;

        /// <summary>게임 시작 후 지난 시간(초). 가로 톱날 위치 기준. 진행 전이면 0, 끝나면 멈춘 값.</summary>
        public float GameElapsed => run == null ? 0f : Mathf.Max(0f, run.TimeLimit - run.TimeRemaining);

        /// <summary>수직 톱날이 나와 있는지.</summary>
        public bool IsVerticalActive => verticalActive;

        /// <summary>수직 톱날이 있는 줄. 없으면 <see cref="TightropeCourse.NoLane"/>.</summary>
        public int VerticalLane => verticalActive ? verticalLane : TightropeCourse.NoLane;

        /// <summary>수직 톱날 진행 거리(m). 없으면 NaN.</summary>
        public float VerticalDistance => verticalActive ? settings.GetVerticalDistance(Time.time - verticalSpawnedAt) : float.NaN;

        /// <summary>발판이 눌렸는지(톱날이 내려가는 중).</summary>
        public bool IsPlatePressed => verticalActive && verticalLoweredAt >= 0f;

        /// <summary>가로 톱날 중심 위치(월드).</summary>
        public Vector3 GetHorizontalPosition(int index)
        {
            float distance = settings.GetHorizontalDistance(index);
            return course.transform.position
                + course.Forward * distance
                + course.Right * tracks[index].GetSide(GameElapsed)
                + Vector3.up * settings.HorizontalCenterHeight;
        }

        /// <summary>초기화 전·없는 번호에는 false. 카메라 등 외부 관찰자가 안전하게 위치를 조회한다.</summary>
        public bool TryGetHorizontalPosition(int index, out Vector3 position)
        {
            position = default;
            if (course == null || index < 0 || index >= tracks.Length || tracks[index] == null) return false;
            position = GetHorizontalPosition(index);
            return true;
        }

        /// <summary>가로 톱날이 움직이고 있는지(작동 중이거나 정지 명령 뒤 끝으로 가는 중).</summary>
        public bool IsHorizontalMoving(int index) => tracks[index].IsMoving(GameElapsed);

        /// <summary>수직 톱날 중심 위치(월드). 없으면 false.</summary>
        public bool TryGetVerticalPosition(out Vector3 position)
        {
            position = default;
            if (!verticalActive || course == null) return false;
            float sinceLower = verticalLoweredAt < 0f ? -1f : Time.time - verticalLoweredAt;
            position = course.GetLanePosition(verticalLane, VerticalDistance)
                + Vector3.up * (settings.verticalCenterHeight - settings.GetLowerOffset(sinceLower));
            return true;
        }

        /// <summary>발판 중심 위치(월드, 외줄 윗면 높이). 수직 톱날이 없으면 false.</summary>
        public bool TryGetPlatePosition(out Vector3 position)
        {
            position = default;
            if (!verticalActive || course == null) return false;
            position = course.GetLanePosition(verticalLane, VerticalDistance + settings.plateOffset);
            return true;
        }

        // ── 수명 ────────────────────────────────────────────

        void OnEnable()
        {
            current = this;
            if (run != null) run.RunRestarted += HandleRunRestarted;
        }

        void Start()
        {
            course = TightropeCourse.Current;
            run = TightropeRun.Current;
            if (course == null || run == null)
            {
                Debug.LogError("[Saws] 씬에 TightropeCourse·TightropeRun이 없어 톱날을 끕니다.");
                enabled = false;
                return;
            }
            run.RunRestarted += HandleRunRestarted;
            BuildViews();
            ResetObstacles();
        }

        void OnDisable()
        {
            if (current == this) current = null;
            if (run != null) run.RunRestarted -= HandleRunRestarted;
            Unregister();
            ClearTeamPassages();
            ResetCooperativeGates();
            if (session != null)
            {
                session.ConnectionStateChanged -= HandleConnectionState;
                session = null;
            }
        }

        void Update()
        {
            if (session == null && NetworkSessionManager.Instance != null)
            {
                session = NetworkSessionManager.Instance;
                session.ConnectionStateChanged += HandleConnectionState;
                HandleConnectionState(session.ConnectionState);
            }

            if (session != null && session.IsHost && run.State == TightropeRunState.Running)
            {
                ServerTickSequence();
                ServerUpdateVertical();
                ServerUpdateCooperativeGate();
                ServerJudgeHits();
            }
            UpdateViews();
        }

        void HandleRunRestarted()
        {
            ResetObstacles();
            // 재시작 신호와 공유 메시지는 순서가 보장되지 않으므로, 클라이언트는 재시작 뒤 호스트에 현재 상태를 다시 묻는다
            if (session != null && session.ConnectionState == SessionConnectionState.Client) SendRequest();
        }

        /// <summary>처음 상태로: 가로 톱날은 출발 쪽 끝 대기, 수직 톱날 없음, 순서 처음부터.</summary>
        void ResetObstacles()
        {
            teamPassages.Clear();
            ResetCooperativeGates();
            platePressedFor = 0f;
            touching.Clear();
            lastHitAt.Clear();
            verticalStep = -1;
            waitReasons.Clear();
            sequence = new ObstacleSequence(settings.steps);
            horizontalCommands.Clear();
            tracks = new HorizontalSawTrack[settings.HorizontalCount];
            for (int i = 0; i < tracks.Length; i++) tracks[i] = settings.CreateTrack(i);
            if (verticalActive) SetVertical(false, verticalLane, verticalSerial, 0f, -1f);
        }

        // ── 호스트: 장애물 순서 ──────────────────────────────

        void ServerTickSequence()
        {
            float performanceStartedAt = run.IsPerformanceStarted ? GameElapsed - run.PerformanceElapsed : -1f;
            sequence.Tick(GameElapsed, performanceStartedAt, GetLeaderDistance(), ServerExecuteStep);

            // 진단: 묘기 시작을 기다리는 수직 톱날 단계(시작 조건 자체가 안 맞음)
            if (run.IsPerformanceStarted) return;
            for (int i = 0; i < sequence.Count; i++)
                if (sequence[i].action == StepAction.SpawnVertical && sequence[i].trigger == StepTrigger.PerformanceStart && sequence.GetFiredCount(i) == 0)
                    LogWait(i, "묘기 시작 전(아무도 줄에 오르지 않음)");
        }

        /// <summary>진단 로그: 단계가 나오지 못하는 이유가 바뀌었을 때만 남긴다(플레이어 위치 포함).</summary>
        void LogWait(int index, string reason)
        {
            if (waitReasons.TryGetValue(index, out string last) && last == reason) return;
            waitReasons[index] = reason;
            Debug.Log($"[Saws] 단계 '{sequence[index].name}' 대기: {reason} — {DescribePlayers()} (게임 {GameElapsed:0.0}초)");
        }

        /// <summary>진단용: 플레이어마다 상태·줄·거리. 예) "P1 줄0 12.3m, P2 플랫폼 -1.0m, P3 Fallen".</summary>
        string DescribePlayers()
        {
            var parts = new List<string>();
            foreach (var player in NetworkPlayer.All)
            {
                string name = $"P{player.Slot + 1}";
                if (player.State != PlayerState.Normal)
                {
                    parts.Add($"{name} {player.State}");
                    continue;
                }
                bool onRope = course.TryGetRopePoint(player.transform.position, out int lane, out float distance) && distance >= 0f;
                parts.Add(onRope ? $"{name} 줄{lane} {distance:0.0}m" : $"{name} 플랫폼 {course.GetDistance(player.transform.position):0.0}m");
            }
            return parts.Count == 0 ? "플레이어 없음" : string.Join(", ", parts);
        }

        /// <summary>진행 중인 플레이어 중 가장 앞선 거리(m). 없으면 NaN.</summary>
        float GetLeaderDistance()
        {
            float leader = float.NaN;
            foreach (var player in NetworkPlayer.All)
            {
                if (player.State != PlayerState.Normal) continue;
                float distance = course.GetDistance(player.transform.position);
                if (float.IsNaN(leader) || distance > leader) leader = distance;
            }
            return leader;
        }

        bool ServerExecuteStep(int index)
        {
            var step = sequence[index];
            if (step.action == StepAction.SpawnVertical)
            {
                // 협동 관문은 독립 진행 거리 순서를 사용한다. 기존 일반 톱 설정/경로 보호는 그대로 둔다.
                if (settings.cooperativeGates) return true;
                if (!ServerSpawnVertical(step, out string reason))
                {
                    LogWait(index, reason);
                    return false;
                }
                waitReasons.Remove(index);
                verticalStep = index;
                Debug.Log($"[Saws] 단계 '{step.name}' 실행: 수직 톱날 #{verticalSerial} {verticalLane}번 줄 — {DescribePlayers()} (게임 {GameElapsed:0.0}초)");
                return true;
            }

            int first = Mathf.Clamp(step.firstSaw, 1, tracks.Length), last = Mathf.Clamp(step.lastSaw, first, tracks.Length);
            if (tracks.Length == 0) return true;
            ApplyHorizontal(step.action, first, last, GameElapsed);
            BroadcastHorizontal();
            string verb = step.action == StepAction.StartHorizontal ? "작동" : "정지";
            Debug.Log($"[Saws] 단계 '{step.name}' 실행: 가로 톱날 #{first}~#{last} {verb} (게임 {GameElapsed:0.0}초)");
            return true;
        }

        /// <summary>모든 화면: 가로 톱날 범위(1부터)를 그 시각에 작동·정지하고 기록한다.</summary>
        void ApplyHorizontal(StepAction action, int first, int last, float at)
        {
            horizontalCommands.Add((action, first, last, at));
            for (int i = first - 1; i <= last - 1 && i < tracks.Length; i++)
            {
                if (i < 0) continue;
                if (action == StepAction.StartHorizontal) tracks[i].Start(at);
                else tracks[i].Stop(at);
            }
        }

        // ── 호스트: 수직 톱날 ────────────────────────────────

        void ServerUpdateVertical()
        {
            if (!verticalActive) return;
            // 인원수와 무관하게 마지막 줄과 화재 경고 중 반드시 건너야 하는 길을 남긴다.
            if (WouldBlockRequiredRoute(verticalLane, VerticalDistance))
            {
                ServerRemoveVertical();
                return;
            }

            float sinceSpawn = Time.time - verticalSpawnedAt;
            if (verticalLoweredAt >= 0f)
            {
                if (settings.HasLowered(Time.time - verticalLoweredAt)) ServerRemoveVertical();
                return;
            }
            if (settings.HasVerticalReachedEnd(sinceSpawn))
            {
                ServerRemoveVertical();
                return;
            }

            if (IsAnyoneOnPlate()) platePressedFor += Time.deltaTime;
            else platePressedFor = 0f;
            if (platePressedFor > 0f && platePressedFor >= settings.plateHoldTime)
            {
                ServerOpenTeamPassage();
                SetVertical(true, verticalLane, verticalSerial, sinceSpawn, 0f);
                Broadcast();
            }
        }

        /// <summary>
        /// 수직 톱날을 낸다. 이미 나와 있으면 false(제거될 때까지 기다림). 줄을 정한 단계는 그 줄(놓여 있고 막히지 않았을 때),
        /// 아니면 생성 지점보다 앞(시작 쪽, 0m 이상)에 진행 중인 플레이어가 있는 줄 중 랜덤 — 톱날이 항상 누군가를 향해 내려온다(2026-10-03 PM).
        /// 고를 줄이 없으면 false로 미루고 다음 프레임에 다시 본다.
        /// </summary>
        bool ServerSpawnVertical(ObstacleStep step, out string reason)
        {
            reason = null;
            if (verticalActive)
            {
                reason = $"수직 톱날 #{verticalSerial}이 아직 나와 있음(최대 1개)";
                return false;
            }
            if (step.lane >= 0)
            {
                if (!course.IsLaneUsable(step.lane, settings.verticalSpawnDistance))
                {
                    reason = $"지정한 {step.lane}번 줄을 쓸 수 없음";
                    return false;
                }
                if (WouldBlockRequiredRoute(step.lane, settings.verticalSpawnDistance))
                {
                    reason = "옆줄 회피 불가 또는 화재 경고 중 필수 이동 경로 보호";
                    return false;
                }
                SpawnVerticalOn(step.lane);
                return true;
            }

            var lanes = new List<int>();
            foreach (var player in NetworkPlayer.All)
            {
                if (player.State != PlayerState.Normal) continue;
                if (!course.TryGetRopePoint(player.transform.position, out int lane, out float distance)) continue;
                if (distance < 0f || distance >= settings.verticalSpawnDistance || lanes.Contains(lane)) continue;
                if (WouldBlockRequiredRoute(lane, settings.verticalSpawnDistance)) continue;
                lanes.Add(lane);
            }
            if (lanes.Count == 0)
            {
                reason = $"고를 줄 없음(생성 지점 {settings.verticalSpawnDistance:0.#}m 앞의 진행자 줄이 없거나 회피 경로 보호 중)";
                return false;
            }
            SpawnVerticalOn(lanes[UnityEngine.Random.Range(0, lanes.Count)]);
            return true;
        }

        bool WouldBlockRequiredRoute(int sawLane, float sawDistance)
        {
            var fire = TightropeFire.Current;
            bool warningActive = run != null && fire != null && fire.IsPerformanceActive && fire.Round == run.Round
                && fire.PendingSegment >= 0 && fire.WarningRemaining(fire.PendingSegment) >= 0f;
            var fireSettings = warningActive ? fire.Settings : null;
            int pending = warningActive ? fire.PendingSegment : -1;
            if (!SawRouteSafety.HasEscapeLane(course, sawLane, sawDistance, fireSettings, pending)) return true;
            foreach (var player in NetworkPlayer.All)
            {
                if (player == null || !player.isActiveAndEnabled || !player.IsSpawned || player.State != PlayerState.Normal) continue;
                float radius = player.TryGetComponent(out PlayerMover mover) ? mover.BodyRadius : 0f;
                if (SawRouteSafety.BlocksPlayerRoute(course, player.transform.position, sawLane, sawDistance,
                    settings.verticalColliderDiameter * 0.5f + radius, fireSettings, pending)) return true;
            }
            return false;
        }

        /// <summary>1명만 진행 중이고, 현재 줄에서 옆줄 이동으로 닿을 수 있는 회피 경로를 수직 톱날이 막는지.</summary>
        internal static bool BlocksSoloRoute(TightropeCourse targetCourse, int survivors, Vector3 survivorPosition,
            int sawLane, float sawDistance, float contactReach)
        {
            if (targetCourse == null || survivors != 1) return false;
            float distance = targetCourse.GetDistance(survivorPosition);
            // 중심이 뒤로 지나갔어도 날 끝이 몸과 겹치는 동안은 아직 통과한 장애물이 아니다.
            if (distance < 0f || distance >= targetCourse.FinishDistance || sawDistance + contactReach < distance) return false;
            int currentLane = targetCourse.Layout.GetLaneSlotAt(targetCourse.GetSide(survivorPosition));
            if (sawLane != currentLane || !targetCourse.IsLaneUsable(currentLane, distance)) return false;
            if (!targetCourse.IsLaneChangeAllowed(distance)) return true;

            // 긴 점프는 없으므로 불탄 중간 줄 너머는 다른 구역이다. 현재 줄에 연결된 범위만 센다.
            int first = currentLane, last = currentLane;
            while (first > 0 && targetCourse.IsLaneUsable(first - 1, distance)) first--;
            while (last + 1 < targetCourse.LaneCount && targetCourse.IsLaneUsable(last + 1, distance)) last++;
            return first == last;
        }

        void SpawnVerticalOn(int lane)
        {
            platePressedFor = 0f;
            SetVertical(true, lane, verticalSerial + 1, 0f, -1f);
            Broadcast();
        }

        void ServerRemoveVertical()
        {
            sequence.NotifyFinished(verticalStep, GameElapsed);
            verticalStep = -1;
            SetVertical(false, verticalLane, verticalSerial, 0f, -1f);
            Broadcast();
        }

        /// <summary>진행 중인 플레이어의 발이 발판 위에 있는지.</summary>
        bool IsAnyoneOnPlate()
        {
            if (!TryGetPlatePosition(out Vector3 plate)) return false;
            foreach (var player in NetworkPlayer.All)
            {
                if (player.State != PlayerState.Normal || !player.IsSpawned || !player.isActiveAndEnabled || player.IsAirborne
                    || !player.TryGetComponent(out PlayerMover mover) || mover.IsAirborne
                    || !course.TryGetSupportingRope(mover, out int supportedLane) || supportedLane != verticalLane) continue;
                Vector3 offset = player.transform.position - plate;
                if (Mathf.Abs(Vector3.Dot(offset, course.Forward)) > settings.plateLength * 0.5f) continue;
                if (Mathf.Abs(Vector3.Dot(offset, course.Right)) > settings.plateWidth * 0.5f) continue;
                if (offset.y < -settings.plateHeightTolerance || offset.y > settings.plateHeightTolerance) continue;
                return true;
            }
            return false;
        }

        // ── 호스트: 판정 ────────────────────────────────────

        void ServerJudgeHits()
        {
            touchingNow.Clear();
            var horizontalRadius = settings.horizontalColliderDiameter * 0.5f;
            var verticalRadius = settings.verticalColliderDiameter * 0.5f;
            bool hasVertical = TryGetVerticalPosition(out Vector3 verticalCenter);

            foreach (var player in NetworkPlayer.All)
            {
                if (player.State != PlayerState.Normal || !player.TryGetComponent(out PlayerMover mover)) continue;
                var body = new BodyCapsule(player.transform.position, mover.BodyHeight, mover.BodyRadius);

                for (int i = 0; i < settings.HorizontalCount; i++)
                {
                    if (IsHorizontalSafeForTeamPassage(i)) continue;
                    var disc = new SawDisc(GetHorizontalPosition(i), Vector3.up, horizontalRadius, settings.horizontalThickness * 0.5f);
                    if (SawMath.TryGetContact(disc, body, out Vector3 contact))
                        ServerTouch(player, body, SawKind.Horizontal, i, i, contact);
                }

                if (hasVertical)
                {
                    var disc = new SawDisc(verticalCenter, course.Right, verticalRadius, settings.verticalThickness * 0.5f);
                    if (SawMath.TryGetContact(disc, body, out Vector3 contact))
                        ServerTouch(player, body, SawKind.Vertical, -verticalSerial, verticalSerial, contact);
                }
                ServerJudgeCooperativeGate(player, body);
            }

            touching.Clear();
            touching.UnionWith(touchingNow);
        }

        /// <summary>닿기 시작한 순간에만, 쿨다운이 지났으면 판정한다(계속 닿아 있으면 한 번).</summary>
        void ServerTouch(NetworkPlayer player, in BodyCapsule body, SawKind kind, int sawKey, int index, Vector3 contact)
        {
            if (run == null || run.State != TightropeRunState.Running || course == null || !course.IsGameplayActive) return;
            var key = (sawKey, player.NetworkObjectId);
            touchingNow.Add(key);
            if (touching.Contains(key)) return;
            if (lastHitAt.TryGetValue(key, out float last) && Time.time - last < settings.hitCooldown) return;
            lastHitAt[key] = Time.time;

            // 접촉 위치는 연출에 남기고, 실제 손실 순서는 공용 부상 규칙에 맡긴다.
            var requested = SawMath.DecidePart(contact, body, course.Right, settings.waistHeight);
            var cut = player.ServerCut();
            var hit = new SawHit(player, cut, requested, kind, index, contact, contact.y - body.Feet.y);
            RaiseHit(hit);
            BroadcastHit(hit);
        }

        // ── 모든 화면 ───────────────────────────────────────

        void SetVertical(bool active, int lane, int serial, float sinceSpawn, float sinceLower)
        {
            bool wasActive = verticalActive, wasLowering = verticalLoweredAt >= 0f;
            verticalActive = active;
            verticalLane = lane;
            verticalSerial = serial;
            verticalSpawnedAt = Time.time - sinceSpawn;
            verticalLoweredAt = active && sinceLower >= 0f ? Time.time - sinceLower : -1f;

            if (active && !wasActive)
                Debug.Log($"[Saws] 수직 톱날 #{serial} 출현: {lane}번 줄, {VerticalDistance:0.0}m");
            else if (!active && wasActive)
                Debug.Log($"[Saws] 수직 톱날 #{serial} 제거");
            if (active && !wasLowering && verticalLoweredAt >= 0f)
                Debug.Log($"[Saws] 수직 톱날 #{serial} 발판 눌림 → {settings.verticalLowerDuration:0.00}초 동안 내려감 ({VerticalDistance:0.0}m)");
            VerticalChanged?.Invoke();
        }

        void RaiseHit(in SawHit hit)
        {
            string saw = hit.Kind == SawKind.Horizontal ? $"가로 톱날 #{hit.Index + 1}"
                : hit.Kind == SawKind.CooperativeGate ? $"목마 협동 관문 #{hit.Index + 1}" : $"수직 톱날 #{hit.Index}";
            string who = hit.Player != null ? $"P{hit.Player.Slot + 1}" : "(나간 플레이어)";
            string part = hit.Part == BodyPart.None ? "잘린 부위 없음(거절 또는 남은 부위 없음)" : hit.Part.ToString();
            string requested = hit.Kind == SawKind.Horizontal
                ? $"순서 절단, 닿은 쪽 {((hit.Requested & (BodyPart.LeftArm | BodyPart.LeftLeg)) != 0 ? "왼쪽" : "오른쪽")}"
                : $"닿은 부위 {hit.Requested}";
            Debug.Log($"[Saws] 절단: {who} → {part} ({saw}, {requested}, 닿은 높이 {hit.ContactHeight:0.00}m)");
            Hit?.Invoke(hit);
        }

        // ── 겉모습 ──────────────────────────────────────────

        void BuildViews()
        {
            var old = transform.Find(GeneratedName);
            if (old != null)
            {
                old.gameObject.SetActive(false);
                old.name = GeneratedName + " (retired)";
                if (Application.isPlaying) Destroy(old.gameObject);
                else DestroyImmediate(old.gameObject);
            }
            var root = new GameObject(GeneratedName).transform;
            root.SetParent(transform, false);
            var resources = root.gameObject.AddComponent<TightropeSawVisualResources>();

            horizontalViews = new Transform[settings.HorizontalCount];
            for (int i = 0; i < horizontalViews.Length; i++)
                horizontalViews[i] = CreateSaw(root, $"HorizontalSaw{i + 1:00}", horizontalPrefab, settings.horizontalVisualDiameter, settings.horizontalThickness, SawKind.Horizontal);

            verticalView = CreateSaw(root, "VerticalSaw", verticalPrefab, settings.verticalVisualDiameter, settings.verticalThickness, SawKind.Vertical);
            verticalView.gameObject.SetActive(false);

            plateView = platePrefab != null
                ? Instantiate(platePrefab, root).transform
                : CreatePrimitive(PrimitiveType.Cube, root, "Plate", new Vector3(settings.plateWidth, 0.04f, settings.plateLength), plateColor);
            plateView.name = "Plate";
            plateRenderers = Array.FindAll(plateView.GetComponentsInChildren<Renderer>(true),
                renderer => !TightropeOutline.IsOutlineRenderer(renderer));
            plateMaterials = new Material[plateRenderers.Length];
            for (int i = 0; i < plateRenderers.Length; i++)
            {
                if (plateRenderers[i].sharedMaterial == null) continue;
                plateMaterials[i] = resources.Clone(plateRenderers[i].sharedMaterial);
                plateRenderers[i].sharedMaterial = plateMaterials[i];
            }
            TightropeOutline.AttachToHierarchy(plateView.gameObject);
            plateView.gameObject.SetActive(false);
            BuildCooperativeGateViews(root);
        }

        /// <summary>기본 톱날: 밝은 날·양면 회전 표식. 모든 도형은 지정된 시각 지름·두께 안에 둔다.</summary>
        Transform CreateSaw(Transform parent, string name, GameObject prefab, float diameter, float thickness, SawKind kind)
        {
            if (prefab != null)
            {
                var instance = Instantiate(prefab, parent).transform;
                instance.name = name;
                TightropeOutline.AttachToHierarchy(instance.gameObject);
                foreach (var outline in instance.GetComponentsInChildren<TightropeOutline>(true)) outline.SetWidth(3f);
                return instance;
            }
            var saw = new GameObject(name).transform;
            saw.SetParent(parent, false);
            var bladeColor = kind == SawKind.Horizontal ? settings.horizontalColor : settings.verticalColor;
            CreatePrimitive(PrimitiveType.Cylinder, saw, "Blade", new Vector3(diameter * 0.9f, thickness * 0.46f, diameter * 0.9f), bladeColor, true);
            // 사각 톱니의 모서리까지 시각 반지름 안에 들어간다. 실제 접촉 판정은 기존 SawDisc를 그대로 쓴다.
            float toothDistance = diameter * (Mathf.Sqrt(0.25f - 0.06f * 0.06f) - 0.06f);
            for (int i = 0; i < 8; i++)
            {
                var tooth = CreatePrimitive(PrimitiveType.Cube, saw, $"Tooth{i}", new Vector3(diameter * 0.12f, thickness, diameter * 0.12f), new Color(0.93f, 0.97f, 1f), true);
                tooth.localRotation = Quaternion.Euler(0f, i * 45f + 45f, 0f);
                tooth.localPosition = tooth.localRotation * Vector3.forward * toothDistance;
            }
            CreatePrimitive(PrimitiveType.Cylinder, saw, "Hub", new Vector3(diameter * 0.24f, thickness * 0.5f, diameter * 0.24f), new Color(0.035f, 0.045f, 0.06f));
            // 대칭 톱니만 돌면 회전이 잘 안 보이므로 한쪽 방향의 표식을 앞뒷면에 넣는다.
            foreach (int side in new[] { -1, 1 })
            {
                var mark = CreatePrimitive(PrimitiveType.Cube, saw, side > 0 ? "RotationMarkTop" : "RotationMarkBottom",
                    new Vector3(diameter * 0.1f, thickness * 0.04f, diameter * 0.28f), new Color(1f, 0.8f, 0.15f), true);
                mark.localPosition = new Vector3(0f, side * thickness * 0.48f, diameter * 0.23f);
            }
            TightropeOutline.AttachToHierarchy(saw.gameObject).SetWidth(3f);
            return saw;
        }

        /// <summary>보이기만 하는 도형(충돌체 없음 — 판정은 계산으로 한다).</summary>
        static Transform CreatePrimitive(PrimitiveType type, Transform parent, string name, Vector3 scale, Color color, bool luminous = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var collider = go.GetComponent<Collider>();
            collider.enabled = false;
            if (Application.isPlaying) Destroy(collider);
            else DestroyImmediate(collider);
            go.transform.SetParent(parent, false);
            go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = parent.GetComponentInParent<TightropeSawVisualResources>()
                .Colored(renderer.sharedMaterial, color, luminous);
            return go.transform;
        }

        void UpdateViews()
        {
            UpdateCooperativeGateViews();
            float spin = Time.time * settings.spinSpeed;
            Quaternion courseRotation = Quaternion.LookRotation(course.Forward, Vector3.up);

            for (int i = 0; i < horizontalViews.Length; i++)
            {
                float horizontalSpin = tracks[i].GetElapsed(GameElapsed) * settings.spinSpeed;
                horizontalViews[i].SetPositionAndRotation(GetHorizontalPosition(i) - Vector3.up * TeamPassageLowerOffset(i),
                    courseRotation * Quaternion.Euler(0f, horizontalSpin, 0f));
            }

            bool hasVertical = TryGetVerticalPosition(out Vector3 verticalCenter);
            verticalView.gameObject.SetActive(hasVertical);
            UpdateTeamPassagePlate(hasVertical, courseRotation);
            if (!hasVertical) return;

            // 회전축(+Y)을 코스 오른쪽으로 눕히고, 시작점 쪽으로 굴러가듯 돌린다
            verticalView.SetPositionAndRotation(verticalCenter,
                Quaternion.AngleAxis(-spin, course.Right) * courseRotation * Quaternion.Euler(0f, 0f, -90f));
            TryGetPlatePosition(out Vector3 plate);
            plateView.SetPositionAndRotation(plate + Vector3.up * 0.02f, courseRotation);
            Color color = IsPlatePressed ? platePressedColor : plateColor;
            foreach (var material in plateMaterials) if (material != null) material.color = color;
        }

        // ── 공유 ───────────────────────────────────────────

        void HandleConnectionState(SessionConnectionState state)
        {
            switch (state)
            {
                case SessionConnectionState.Host:
                    Register((RequestMessage, HandleRequest));
                    break;
                case SessionConnectionState.Client:
                    Register((VerticalMessage, HandleVerticalMessage), (HorizontalMessage, HandleHorizontalMessage), (HitMessage, HandleHitMessage),
                        (TeamPassageMessage, HandleTeamPassageMessage), (CooperativeGateMessage, HandleCooperativeGateMessage));
                    SendRequest();
                    break;
                case SessionConnectionState.Offline:
                    Unregister();
                    ResetObstacles();
                    break;
            }
        }

        void Broadcast()
        {
            var manager = Messaging;
            if (manager == null) return;
            using var writer = WriteVertical();
            manager.SendNamedMessageToAll(VerticalMessage, writer, NetworkDelivery.ReliableSequenced);
        }

        void HandleRequest(ulong senderId, FastBufferReader reader)
        {
            var manager = Messaging;
            if (manager == null) return;
            using (var writer = WriteHorizontal())
                manager.SendNamedMessage(HorizontalMessage, senderId, writer, NetworkDelivery.ReliableFragmentedSequenced);
            using (var writer = WriteVertical())
                manager.SendNamedMessage(VerticalMessage, senderId, writer, NetworkDelivery.ReliableSequenced);
            using (var writer = WriteTeamPassages())
                manager.SendNamedMessage(TeamPassageMessage, senderId, writer, NetworkDelivery.ReliableFragmentedSequenced);
            using (var writer = WriteCooperativeGate())
                manager.SendNamedMessage(CooperativeGateMessage, senderId, writer, NetworkDelivery.ReliableSequenced);
        }

        void BroadcastHorizontal()
        {
            var manager = Messaging;
            if (manager == null) return;
            using var writer = WriteHorizontal();
            manager.SendNamedMessageToAll(HorizontalMessage, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }

        /// <summary>가로 톱날 작동·정지 기록 전체(재시작 후부터). 받는 쪽은 기록을 처음부터 다시 적용한다.</summary>
        FastBufferWriter WriteHorizontal()
        {
            var writer = new FastBufferWriter(8 + horizontalCommands.Count * 9, Allocator.Temp, 64 * 1024);
            writer.WriteValueSafe(horizontalCommands.Count);
            foreach (var (action, first, last, at) in horizontalCommands)
            {
                writer.WriteValueSafe((byte)action);
                writer.WriteValueSafe((short)first);
                writer.WriteValueSafe((short)last);
                writer.WriteValueSafe(at);
            }
            return writer;
        }

        void HandleHorizontalMessage(ulong senderId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out int count);
            horizontalCommands.Clear();
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out byte action);
                reader.ReadValueSafe(out short first);
                reader.ReadValueSafe(out short last);
                reader.ReadValueSafe(out float at);
                horizontalCommands.Add(((StepAction)action, first, last, at));
            }
            RebuildHorizontalTracks();
        }

        void SendRequest()
        {
            var manager = Messaging;
            if (manager == null) return;
            using var writer = new FastBufferWriter(1, Allocator.Temp);
            manager.SendNamedMessage(RequestMessage, NetworkManager.ServerClientId, writer);
        }

        /// <summary>나와 있는지, 줄, 순번, 생성 후 지난 시간, 눌린 뒤 지난 시간(-1 = 안 눌림).</summary>
        FastBufferWriter WriteVertical()
        {
            var writer = new FastBufferWriter(32, Allocator.Temp);
            writer.WriteValueSafe(verticalActive);
            writer.WriteValueSafe((byte)Mathf.Max(0, verticalLane));
            writer.WriteValueSafe(verticalSerial);
            writer.WriteValueSafe(verticalActive ? Time.time - verticalSpawnedAt : 0f);
            writer.WriteValueSafe(verticalActive && verticalLoweredAt >= 0f ? Time.time - verticalLoweredAt : -1f);
            return writer;
        }

        void HandleVerticalMessage(ulong senderId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out bool active);
            reader.ReadValueSafe(out byte lane);
            reader.ReadValueSafe(out int serial);
            reader.ReadValueSafe(out float sinceSpawn);
            reader.ReadValueSafe(out float sinceLower);
            SetVertical(active, lane, serial, sinceSpawn, sinceLower);
        }

        void BroadcastHit(in SawHit hit)
        {
            var manager = Messaging;
            if (manager == null || hit.Player == null) return;
            using var writer = new FastBufferWriter(64, Allocator.Temp); // 8 + 4 + 4 + 1 + 4 + 12 + 4 = 37바이트
            writer.WriteValueSafe(hit.Player.NetworkObjectId);
            writer.WriteValueSafe((int)hit.Part);
            writer.WriteValueSafe((int)hit.Requested);
            writer.WriteValueSafe((byte)hit.Kind);
            writer.WriteValueSafe(hit.Index);
            writer.WriteValueSafe(hit.Contact);
            writer.WriteValueSafe(hit.ContactHeight);
            manager.SendNamedMessageToAll(HitMessage, writer, NetworkDelivery.ReliableSequenced);
        }

        void HandleHitMessage(ulong senderId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out ulong playerId);
            reader.ReadValueSafe(out int part);
            reader.ReadValueSafe(out int requested);
            reader.ReadValueSafe(out byte kind);
            reader.ReadValueSafe(out int index);
            reader.ReadValueSafe(out Vector3 contact);
            reader.ReadValueSafe(out float height);

            NetworkPlayer player = null;
            foreach (var candidate in NetworkPlayer.All)
                if (candidate.NetworkObjectId == playerId) player = candidate;
            RaiseHit(new SawHit(player, (BodyPart)part, (BodyPart)requested, (SawKind)kind, index, contact, height));
        }

        CustomMessagingManager Messaging =>
            session != null && session.NetworkManager != null ? session.NetworkManager.CustomMessagingManager : null;

        void Register(params (string name, CustomMessagingManager.HandleNamedMessageDelegate handler)[] handlers)
        {
            Unregister();
            messaging = Messaging;
            if (messaging == null) return;
            foreach (var (name, handler) in handlers) messaging.RegisterNamedMessageHandler(name, handler);
        }

        void Unregister()
        {
            if (messaging == null) return;
            messaging.UnregisterNamedMessageHandler(VerticalMessage);
            messaging.UnregisterNamedMessageHandler(HorizontalMessage);
            messaging.UnregisterNamedMessageHandler(RequestMessage);
            messaging.UnregisterNamedMessageHandler(HitMessage);
            messaging.UnregisterNamedMessageHandler(TeamPassageMessage);
            messaging.UnregisterNamedMessageHandler(CooperativeGateMessage);
            messaging = null;
        }
    }
}
