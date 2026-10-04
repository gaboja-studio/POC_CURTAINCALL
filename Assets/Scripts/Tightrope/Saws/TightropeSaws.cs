using System;
using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Settings;
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
    }

    /// <summary>톱날 절단 결과(호스트가 정해 모든 화면에 알림).</summary>
    public readonly struct SawHit
    {
        public readonly NetworkPlayer Player;
        /// <summary>실제로 잘린 부위(020 신체 손상이 확정). 거절됐거나 자를 부위가 없으면 None.</summary>
        public readonly BodyPart Part;
        /// <summary>닿은 위치로 정한 부위(가로 톱날은 첫 다리 좌우를 고르는 데만 쓴다).</summary>
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
    /// - 장애물 배치(<see cref="Layout"/>, 레벨 디자인, 021): 배치 파일을 단계 목록으로 바꿔 호스트가 <see cref="ObstacleSequence"/>로 가로 톱날 작동·정지, 수직 톱날 출현을 실행한다.
    /// - 가로 톱날: 호스트가 작동·정지 기록을 "게임 시작 후 지난 시간"(<see cref="TightropeRun"/>의 남은 시간, 모든 화면에서 같음)과 함께 공유하고, 위치는 각 화면이 그 기록으로 계산한다.
    /// - 수직 톱날: 호스트가 줄·출현·내려감을 정해 NGO 이름 붙은 메시지로 공유하고, 위치는 각 화면이 시간으로 계산한다.
    ///   뒤의 발판(임시 형태)을 살아 있는 플레이어가 밟으면 톱날이 빠르게 내려간 뒤 사라진다. 눌림 판정(<see cref="IsPlatePressed"/>)과 겉모습은 분리돼 있다.
    /// - 판정: 호스트가 묘기 진행 중에 진행 중인 플레이어(<see cref="PlayerState.Normal"/>)만 본다(도착 완료·사망 무시). 020 신체 손상에 절단을 요청한다(규칙 #9):
    ///   가로 톱날은 순서 절단(다리 → 남은 다리 → 팔, 첫 다리는 톱날이 닿은 쪽 — 2026-10-05 PM), 수직 톱날은 닿은 위치로 정한 부위(<see cref="NetworkPlayer.ServerCutPart"/>).
    ///   실제로 잘린 부위를 모든 화면에 <see cref="Hit"/>로 알리고 로그로 남긴다. 패널티·탈락·공유는 020이 한다.
    /// - 묘기 재시작(<see cref="TightropeRun.RunRestarted"/>)이면 처음 상태로.
    /// 공통 규격은 <see cref="Settings"/>(<see cref="SawSettings"/>), 위치·시간은 <see cref="Layout"/>(<see cref="ObstacleLayout"/>)에 둔다. 씬에 코스(<see cref="TightropeCourse"/>·<see cref="TightropeRun"/>)와 함께 둔다.
    /// </summary>
    public sealed class TightropeSaws : MonoBehaviour
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

        [SerializeField] Color sawColor = new(0.75f, 0.2f, 0.2f);
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

        // 수직 톱날(모든 화면, 시각은 이 화면 기준)
        bool verticalActive;
        int verticalLane;
        int verticalSerial;               // 출현 순번(1부터)
        float verticalSpawnedAt;
        float verticalSpawnDistance;      // 생긴 거리(m). 그 줄 선두 앞이라 매번 다르다(7-2)
        float verticalLoweredAt = -1f;    // 발판이 눌린 시각. 안 눌렸으면 -1

        // 호스트 전용
        ObstacleSequence sequence;
        int verticalStep = -1;            // 지금 수직 톱날을 낸 단계 번호
        readonly Dictionary<int, string> waitReasons = new(); // 단계별 마지막으로 남긴 대기 이유(바뀔 때만 로그)
        readonly List<string> startedLog = new(), stoppedLog = new(); // 이번 프레임에 작동·정지한 가로 톱날(로그·공유 한 번)
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

        /// <summary>장애물 배치(외줄 세팅이 연결한 배치 파일, 쓸 때마다 읽는다). 개수·시간은 묘기 (재)시작 때, 위치는 바로 반영된다.</summary>
        public ObstacleLayout Layout => layout;

        static ObstacleLayout layout => GameSettings.Tightrope.Obstacles;

        /// <summary>
        /// 톱날 판정이 났을 때(모든 화면). 호스트가 확정한 결과다. 020(신체 손상)이 이 신호로 부위를 잃게 한다.
        /// <see cref="SawHit.Part"/>가 None이면 잘릴 부위가 남아 있지 않은 경우다.
        /// </summary>
        public event Action<SawHit> Hit;

        /// <summary>수직 톱날 출현·눌림·제거처럼 상태가 바뀌었을 때(모든 화면).</summary>
        public event Action VerticalChanged;

        /// <summary>가로 톱날 수(묘기 시작 때 배치 파일 기준).</summary>
        public int HorizontalCount => tracks?.Length ?? layout.HorizontalCount;

        /// <summary>게임 시작 후 지난 시간(초). 가로 톱날 위치 기준. 진행 전이면 0, 끝나면 멈춘 값.</summary>
        public float GameElapsed => run == null ? 0f : Mathf.Max(0f, run.TimeLimit - run.TimeRemaining);

        /// <summary>수직 톱날이 나와 있는지.</summary>
        public bool IsVerticalActive => verticalActive;

        /// <summary>수직 톱날이 있는 줄. 없으면 <see cref="TightropeCourse.NoLane"/>.</summary>
        public int VerticalLane => verticalActive ? verticalLane : TightropeCourse.NoLane;

        /// <summary>수직 톱날 진행 거리(m). 없으면 NaN.</summary>
        public float VerticalDistance => verticalActive ? settings.GetVerticalDistance(verticalSpawnDistance, Time.time - verticalSpawnedAt) : float.NaN;

        /// <summary>수직 톱날이 등장 낙하를 마치고 줄 위에 섰는지(발판은 이때부터). 없으면 false.</summary>
        public bool IsVerticalLanded => verticalActive && settings.HasVerticalLanded(Time.time - verticalSpawnedAt);

        /// <summary>발판이 눌렸는지(톱날이 내려가는 중).</summary>
        public bool IsPlatePressed => verticalActive && verticalLoweredAt >= 0f;

        /// <summary>가로 톱날 중심 위치(월드).</summary>
        public Vector3 GetHorizontalPosition(int index)
        {
            float distance = index < layout.HorizontalCount ? layout.GetHorizontalDistance(index) : 0f;
            return course.transform.position
                + course.Forward * distance
                + course.Right * tracks[index].GetSide(GameElapsed)
                + Vector3.up * settings.HorizontalCenterHeight;
        }

        /// <summary>가로 톱날이 움직이고 있는지(작동 중이거나 정지 명령 뒤 끝으로 가는 중).</summary>
        public bool IsHorizontalMoving(int index) => tracks[index].IsMoving(GameElapsed);

        /// <summary>수직 톱날 중심 위치(월드). 없으면 false.</summary>
        public bool TryGetVerticalPosition(out Vector3 position)
        {
            position = default;
            if (!verticalActive || course == null) return false;
            float sinceLower = verticalLoweredAt < 0f ? -1f : Time.time - verticalLoweredAt;
            float drop = settings.GetVerticalDropOffset(Time.time - verticalSpawnedAt);
            position = course.GetLanePosition(verticalLane, VerticalDistance)
                + Vector3.up * (settings.verticalCenterHeight + drop - settings.GetLowerOffset(sinceLower));
            return true;
        }

        /// <summary>발판 중심 위치(월드, 외줄 윗면 높이). 수직 톱날이 없거나 아직 내려오는 중이면 false.</summary>
        public bool TryGetPlatePosition(out Vector3 position)
        {
            position = default;
            if (!IsVerticalLanded || course == null) return false;
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
            platePressedFor = 0f;
            touching.Clear();
            lastHitAt.Clear();
            verticalStep = -1;
            waitReasons.Clear();
            sequence = new ObstacleSequence(layout.BuildSteps());
            horizontalCommands.Clear();
            tracks = new HorizontalSawTrack[layout.HorizontalCount];
            for (int i = 0; i < tracks.Length; i++) tracks[i] = layout.CreateTrack(i, settings);
            if (horizontalViews != null && horizontalViews.Length != tracks.Length) BuildViews();
            if (verticalActive) SetVertical(false, verticalLane, verticalSerial, verticalSpawnDistance, 0f, -1f);
        }

        // ── 호스트: 장애물 순서 ──────────────────────────────

        void ServerTickSequence()
        {
            float performanceStartedAt = run.IsPerformanceStarted ? GameElapsed - run.PerformanceElapsed : -1f;
            sequence.Tick(GameElapsed, performanceStartedAt, ServerExecuteStep);
            FlushHorizontalLog();

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

        bool ServerExecuteStep(int index)
        {
            var step = sequence[index];
            if (step.action == StepAction.SpawnVertical)
            {
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
            (step.action == StepAction.StartHorizontal ? startedLog : stoppedLog).Add(first == last ? $"#{first}" : $"#{first}~#{last}");
            return true;
        }

        /// <summary>한 프레임에 실행된 가로 작동·정지를 한 번만 공유하고 한 줄로 남긴다(톱날마다 단계라 같은 순간에 여러 개가 실행됨).</summary>
        void FlushHorizontalLog()
        {
            if (startedLog.Count == 0 && stoppedLog.Count == 0) return;
            BroadcastHorizontal();
            if (startedLog.Count > 0) Debug.Log($"[Saws] 가로 톱날 작동: {string.Join(", ", startedLog)} (게임 {GameElapsed:0.0}초)");
            if (stoppedLog.Count > 0) Debug.Log($"[Saws] 가로 톱날 정지: {string.Join(", ", stoppedLog)} (게임 {GameElapsed:0.0}초)");
            startedLog.Clear();
            stoppedLog.Clear();
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

            float sinceSpawn = Time.time - verticalSpawnedAt;
            if (verticalLoweredAt >= 0f)
            {
                if (settings.HasLowered(Time.time - verticalLoweredAt)) ServerRemoveVertical();
                return;
            }
            if (settings.HasVerticalReachedEnd(verticalSpawnDistance, sinceSpawn))
            {
                ServerRemoveVertical();
                return;
            }

            if (IsAnyoneOnPlate()) platePressedFor += Time.deltaTime;
            else platePressedFor = 0f;
            if (platePressedFor > 0f && platePressedFor >= settings.plateHoldTime)
            {
                SetVertical(true, verticalLane, verticalSerial, verticalSpawnDistance, sinceSpawn, 0f);
                Broadcast();
            }
        }

        /// <summary>
        /// 수직 톱날을 낸다. 이미 나와 있으면 false(제거될 때까지 기다림). 줄을 정한 단계는 그 줄(놓여 있고 막히지 않았을 때),
        /// 아니면 진행 중인 플레이어가 있는 줄 중 랜덤 — 톱날이 항상 누군가를 향해 내려온다(2026-10-03 PM).
        /// 생기는 곳은 그 줄 선두 + 선두 앞 최소 거리이고 생성 한계(93m)를 넘지 않는다. 넘는 줄은 후보에서 빠지고, 후보가 없으면 false로 미룬다(7-1·7-2).
        /// </summary>
        bool ServerSpawnVertical(ObstacleStep step, out string reason)
        {
            reason = null;
            if (verticalActive)
            {
                reason = $"수직 톱날 #{verticalSerial}이 아직 나와 있음(최대 1개)";
                return false;
            }

            var leaders = GetLaneLeaders();
            if (step.lane >= 0)
            {
                leaders.TryGetValue(step.lane, out float leader);
                float distance = layout.GetVerticalSpawnDistance(leaders.ContainsKey(step.lane) ? leader : float.NaN, settings);
                if (float.IsNaN(distance))
                {
                    reason = $"지정한 {step.lane}번 줄 선두({leader:0.0}m) 앞 {layout.vertical.minLeadDistance:0.#}m가 생성 한계 {settings.verticalSpawnDistance:0.#}m를 넘음";
                    return false;
                }
                if (!course.IsLaneUsable(step.lane, distance))
                {
                    reason = $"지정한 {step.lane}번 줄을 쓸 수 없음";
                    return false;
                }
                SpawnVerticalOn(step.lane, distance);
                return true;
            }

            var lanes = new List<(int lane, float distance)>();
            foreach (var (lane, leader) in leaders)
            {
                float distance = layout.GetVerticalSpawnDistance(leader, settings);
                if (!float.IsNaN(distance) && course.IsLaneUsable(lane, distance)) lanes.Add((lane, distance));
            }
            if (lanes.Count == 0)
            {
                reason = leaders.Count == 0
                    ? "고를 줄 없음(줄 위에 진행 중인 플레이어가 없음)"
                    : $"고를 줄 없음(줄 선두 앞 {layout.vertical.minLeadDistance:0.#}m가 모두 생성 한계 {settings.verticalSpawnDistance:0.#}m를 넘음)";
                return false;
            }
            var pick = lanes[UnityEngine.Random.Range(0, lanes.Count)];
            SpawnVerticalOn(pick.lane, pick.distance);
            return true;
        }

        /// <summary>줄마다 가장 앞선 진행 중 플레이어의 거리(m). 줄 위(0m 이상)에 있는 플레이어만.</summary>
        Dictionary<int, float> GetLaneLeaders()
        {
            var leaders = new Dictionary<int, float>();
            foreach (var player in NetworkPlayer.All)
            {
                if (player.State != PlayerState.Normal) continue;
                if (!course.TryGetRopePoint(player.transform.position, out int lane, out float distance) || distance < 0f) continue;
                if (!leaders.TryGetValue(lane, out float best) || distance > best) leaders[lane] = distance;
            }
            return leaders;
        }

        void SpawnVerticalOn(int lane, float distance)
        {
            platePressedFor = 0f;
            SetVertical(true, lane, verticalSerial + 1, distance, 0f, -1f);
            Broadcast();
        }

        void ServerRemoveVertical()
        {
            sequence.NotifyFinished(verticalStep, GameElapsed);
            verticalStep = -1;
            SetVertical(false, verticalLane, verticalSerial, verticalSpawnDistance, 0f, -1f);
            Broadcast();
        }

        /// <summary>진행 중인 플레이어의 발이 발판 위에 있는지.</summary>
        bool IsAnyoneOnPlate()
        {
            if (!TryGetPlatePosition(out Vector3 plate)) return false;
            foreach (var player in NetworkPlayer.All)
            {
                if (player.State != PlayerState.Normal) continue;
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

                for (int i = 0; i < HorizontalCount; i++)
                {
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
            }

            touching.Clear();
            touching.UnionWith(touchingNow);
        }

        /// <summary>닿기 시작한 순간에만, 쿨다운이 지났으면 판정한다(계속 닿아 있으면 한 번).</summary>
        void ServerTouch(NetworkPlayer player, in BodyCapsule body, SawKind kind, int sawKey, int index, Vector3 contact)
        {
            var key = (sawKey, player.NetworkObjectId);
            touchingNow.Add(key);
            if (touching.Contains(key)) return;
            if (lastHitAt.TryGetValue(key, out float last) && Time.time - last < settings.hitCooldown) return;
            lastHitAt[key] = Time.time;

            // 닿은 위치로 부위를 정하고 020에 절단을 요청한다(이미 잃은 부위 처리·거절·탈락은 020이 한다)
            var requested = SawMath.DecidePart(contact, body, course.Right, settings.waistHeight);
            bool leftSide = (requested & (BodyPart.LeftArm | BodyPart.LeftLeg)) != 0;
            var cut = kind == SawKind.Horizontal ? player.ServerCutLegThenArm(leftSide) : player.ServerCutPart(requested);
            var hit = new SawHit(player, cut, requested, kind, index, contact, contact.y - body.Feet.y);
            RaiseHit(hit);
            BroadcastHit(hit);
        }

        // ── 모든 화면 ───────────────────────────────────────

        void SetVertical(bool active, int lane, int serial, float spawnDistance, float sinceSpawn, float sinceLower)
        {
            bool wasActive = verticalActive, wasLowering = verticalLoweredAt >= 0f;
            verticalActive = active;
            verticalLane = lane;
            verticalSerial = serial;
            verticalSpawnDistance = spawnDistance;
            verticalSpawnedAt = Time.time - sinceSpawn;
            verticalLoweredAt = active && sinceLower >= 0f ? Time.time - sinceLower : -1f;

            if (active && !wasActive)
                Debug.Log($"[Saws] 수직 톱날 #{serial} 출현: {lane}번 줄, {spawnDistance:0.0}m (화면 위에서 {settings.verticalDropDuration:0.0}초 동안 내려옴)");
            else if (!active && wasActive)
                Debug.Log($"[Saws] 수직 톱날 #{serial} 제거");
            if (active && !wasLowering && verticalLoweredAt >= 0f)
                Debug.Log($"[Saws] 수직 톱날 #{serial} 발판 눌림 → {settings.verticalLowerDuration:0.00}초 동안 내려감 ({VerticalDistance:0.0}m)");
            VerticalChanged?.Invoke();
        }

        void RaiseHit(in SawHit hit)
        {
            string saw = hit.Kind == SawKind.Horizontal ? $"가로 톱날 #{hit.Index + 1}" : $"수직 톱날 #{hit.Index}";
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
            if (old != null) Destroy(old.gameObject);
            var root = new GameObject(GeneratedName).transform;
            root.SetParent(transform, false);

            horizontalViews = new Transform[layout.HorizontalCount];
            for (int i = 0; i < horizontalViews.Length; i++)
                horizontalViews[i] = CreateSaw(root, $"HorizontalSaw{i + 1:00}", horizontalPrefab, settings.horizontalVisualDiameter, settings.horizontalThickness);

            verticalView = CreateSaw(root, "VerticalSaw", verticalPrefab, settings.verticalVisualDiameter, settings.verticalThickness);
            verticalView.gameObject.SetActive(false);

            plateView = platePrefab != null
                ? Instantiate(platePrefab, root).transform
                : CreatePrimitive(PrimitiveType.Cube, root, "Plate", new Vector3(settings.plateWidth, 0.04f, settings.plateLength), plateColor);
            plateView.name = "Plate";
            plateRenderers = plateView.GetComponentsInChildren<Renderer>();
            plateView.gameObject.SetActive(false);
        }

        /// <summary>기본 톱날: 회전축이 위(+Y)인 납작한 원판 + 톱니 8개(돌아가는 게 보이게).</summary>
        Transform CreateSaw(Transform parent, string name, GameObject prefab, float diameter, float thickness)
        {
            if (prefab != null)
            {
                var instance = Instantiate(prefab, parent).transform;
                instance.name = name;
                return instance;
            }
            var saw = new GameObject(name).transform;
            saw.SetParent(parent, false);
            CreatePrimitive(PrimitiveType.Cylinder, saw, "Blade", new Vector3(diameter * 0.9f, thickness * 0.5f, diameter * 0.9f), sawColor);
            for (int i = 0; i < 8; i++)
            {
                var tooth = CreatePrimitive(PrimitiveType.Cube, saw, $"Tooth{i}", new Vector3(diameter * 0.12f, thickness, diameter * 0.12f), sawColor * 0.7f);
                tooth.localRotation = Quaternion.Euler(0f, i * 45f + 45f, 0f);
                tooth.localPosition = tooth.localRotation * Vector3.forward * (diameter * 0.44f);
            }
            return saw;
        }

        /// <summary>보이기만 하는 도형(충돌체 없음 — 판정은 계산으로 한다).</summary>
        static Transform CreatePrimitive(PrimitiveType type, Transform parent, string name, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().material.color = color;
            return go.transform;
        }

        void UpdateViews()
        {
            float spin = Time.time * settings.spinSpeed;
            Quaternion courseRotation = Quaternion.LookRotation(course.Forward, Vector3.up);

            for (int i = 0; i < horizontalViews.Length; i++)
            {
                horizontalViews[i].SetPositionAndRotation(GetHorizontalPosition(i), courseRotation * Quaternion.Euler(0f, spin, 0f));
            }

            bool hasVertical = TryGetVerticalPosition(out Vector3 verticalCenter);
            bool hasPlate = TryGetPlatePosition(out Vector3 plate);
            verticalView.gameObject.SetActive(hasVertical);
            plateView.gameObject.SetActive(hasPlate);
            if (!hasVertical) return;

            // 회전축(+Y)을 코스 오른쪽으로 눕히고, 시작점 쪽으로 굴러가듯 돌린다
            verticalView.SetPositionAndRotation(verticalCenter,
                Quaternion.AngleAxis(-spin, course.Right) * courseRotation * Quaternion.Euler(0f, 0f, -90f));
            if (!hasPlate) return;
            plateView.SetPositionAndRotation(plate + Vector3.up * 0.02f, courseRotation);
            Color color = IsPlatePressed ? platePressedColor : plateColor;
            foreach (var renderer in plateRenderers) renderer.material.color = color;
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
                    Register((VerticalMessage, HandleVerticalMessage), (HorizontalMessage, HandleHorizontalMessage), (HitMessage, HandleHitMessage));
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
            foreach (var track in tracks) track.Clear();
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out byte action);
                reader.ReadValueSafe(out short first);
                reader.ReadValueSafe(out short last);
                reader.ReadValueSafe(out float at);
                ApplyHorizontal((StepAction)action, first, last, at);
            }
        }

        void SendRequest()
        {
            var manager = Messaging;
            if (manager == null) return;
            using var writer = new FastBufferWriter(1, Allocator.Temp);
            manager.SendNamedMessage(RequestMessage, NetworkManager.ServerClientId, writer);
        }

        /// <summary>나와 있는지, 줄, 순번, 생긴 거리, 생성 후 지난 시간, 눌린 뒤 지난 시간(-1 = 안 눌림).</summary>
        FastBufferWriter WriteVertical()
        {
            var writer = new FastBufferWriter(32, Allocator.Temp);
            writer.WriteValueSafe(verticalActive);
            writer.WriteValueSafe((byte)Mathf.Max(0, verticalLane));
            writer.WriteValueSafe(verticalSerial);
            writer.WriteValueSafe(verticalSpawnDistance);
            writer.WriteValueSafe(verticalActive ? Time.time - verticalSpawnedAt : 0f);
            writer.WriteValueSafe(verticalActive && verticalLoweredAt >= 0f ? Time.time - verticalLoweredAt : -1f);
            return writer;
        }

        void HandleVerticalMessage(ulong senderId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out bool active);
            reader.ReadValueSafe(out byte lane);
            reader.ReadValueSafe(out int serial);
            reader.ReadValueSafe(out float spawnDistance);
            reader.ReadValueSafe(out float sinceSpawn);
            reader.ReadValueSafe(out float sinceLower);
            SetVertical(active, lane, serial, spawnDistance, sinceSpawn, sinceLower);
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
            messaging = null;
        }
    }
}
