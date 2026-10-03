using System;
using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
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

    /// <summary>톱날 판정 결과(호스트가 정해 모든 화면에 알림).</summary>
    public readonly struct SawHit
    {
        public readonly NetworkPlayer Player;
        public readonly BodyPart Part;
        public readonly SawKind Kind;
        /// <summary>가로 톱날 번호(0부터). 수직 톱날은 출현 순번(1부터).</summary>
        public readonly int Index;
        /// <summary>닿은 곳(월드).</summary>
        public readonly Vector3 Contact;

        public SawHit(NetworkPlayer player, BodyPart part, SawKind kind, int index, Vector3 contact)
        {
            Player = player;
            Part = part;
            Kind = kind;
            Index = index;
            Contact = contact;
        }
    }

    /// <summary>
    /// 외줄 톱날 장애물과 공개 진입점. 규칙: Harness/Project/Decisions/tightrope-course-rules.md #6~#9 (2026-10-03).
    /// - 장애물 순서(<see cref="SawSettings.steps"/>, 레벨 디자인): 호스트가 <see cref="ObstacleSequence"/>로 단계 조건을 보고 가로 톱날 작동·정지, 수직 톱날 출현을 실행한다.
    /// - 가로 톱날: 호스트가 작동·정지 기록을 "게임 시작 후 지난 시간"(<see cref="TightropeRun"/>의 남은 시간, 모든 화면에서 같음)과 함께 공유하고, 위치는 각 화면이 그 기록으로 계산한다.
    /// - 수직 톱날: 호스트가 줄·출현·내려감을 정해 NGO 이름 붙은 메시지로 공유하고, 위치는 각 화면이 시간으로 계산한다.
    ///   뒤의 발판(임시 형태)을 살아 있는 플레이어가 밟으면 톱날이 빠르게 내려간 뒤 사라진다. 눌림 판정(<see cref="IsPlatePressed"/>)과 겉모습은 분리돼 있다.
    /// - 판정: 호스트가 묘기 진행 중에 진행 중인 플레이어(<see cref="PlayerState.Normal"/>)만 본다(도착 완료·사망 무시). 닿은 곳으로 팔·다리 중 하나를 정하고
    ///   모든 화면에 <see cref="Hit"/>로 알린다. **지금은 판정 결과를 로그로만 남긴다** — 부위 손상·패널티·탈락은 020이 이 신호로 처리한다.
    /// - 묘기 재시작(<see cref="TightropeRun.RunRestarted"/>)이면 처음 상태로.
    /// 수치는 <see cref="Settings"/>(<see cref="SawSettings"/>) 한 곳에 모은다. 씬에 코스(<see cref="TightropeCourse"/>·<see cref="TightropeRun"/>)와 함께 둔다.
    /// </summary>
    public sealed class TightropeSaws : MonoBehaviour
    {
        const string VerticalMessage = "CurtainCall.Tightrope.Saws.Vertical";
        const string HorizontalMessage = "CurtainCall.Tightrope.Saws.Horizontal";
        const string RequestMessage = "CurtainCall.Tightrope.Saws.Request";
        const string HitMessage = "CurtainCall.Tightrope.Saws.Hit";
        const string GeneratedName = "Generated";

        [SerializeField] SawSettings settings = new();

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

        /// <summary>톱날 조정값.</summary>
        public SawSettings Settings => settings;

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
                SpawnVerticalOn(step.lane);
                return true;
            }

            var lanes = new List<int>();
            foreach (var player in NetworkPlayer.All)
            {
                if (player.State != PlayerState.Normal) continue;
                if (!course.TryGetRopePoint(player.transform.position, out int lane, out float distance)) continue;
                if (distance < 0f || distance >= settings.verticalSpawnDistance || lanes.Contains(lane)) continue;
                lanes.Add(lane);
            }
            if (lanes.Count == 0)
            {
                reason = $"고를 줄 없음(줄 위 0m~생성 지점 {settings.verticalSpawnDistance:0.#}m 사이에 진행 중인 플레이어가 없음)";
                return false;
            }
            SpawnVerticalOn(lanes[UnityEngine.Random.Range(0, lanes.Count)]);
            return true;
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

                for (int i = 0; i < settings.HorizontalCount; i++)
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

            var lost = player.TryGetComponent(out PlayerCondition condition) ? condition.LostParts : BodyPart.None;
            var part = SawMath.DecidePart(contact, body, course.Right, settings.waistHeight, lost);
            var hit = new SawHit(player, part, kind, index, contact);
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
            string saw = hit.Kind == SawKind.Horizontal ? $"가로 톱날 #{hit.Index + 1}" : $"수직 톱날 #{hit.Index}";
            string who = hit.Player != null ? $"P{hit.Player.Slot + 1}" : "(나간 플레이어)";
            string part = hit.Part == BodyPart.None ? "잘릴 부위 없음" : hit.Part.ToString();
            float height = hit.Player != null ? hit.Contact.y - hit.Player.transform.position.y : 0f;
            Debug.Log($"[Saws] 절단 판정: {who} → {part} ({saw}, 닿은 높이 {height:0.00}m) — 020 병합 전이라 판정만, 적용 없음");
            Hit?.Invoke(hit);
        }

        // ── 겉모습 ──────────────────────────────────────────

        void BuildViews()
        {
            var old = transform.Find(GeneratedName);
            if (old != null) Destroy(old.gameObject);
            var root = new GameObject(GeneratedName).transform;
            root.SetParent(transform, false);

            horizontalViews = new Transform[settings.HorizontalCount];
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
            verticalView.gameObject.SetActive(hasVertical);
            plateView.gameObject.SetActive(hasVertical);
            if (!hasVertical) return;

            // 회전축(+Y)을 코스 오른쪽으로 눕히고, 시작점 쪽으로 굴러가듯 돌린다
            verticalView.SetPositionAndRotation(verticalCenter,
                Quaternion.AngleAxis(-spin, course.Right) * courseRotation * Quaternion.Euler(0f, 0f, -90f));
            TryGetPlatePosition(out Vector3 plate);
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
            using var writer = new FastBufferWriter(32, Allocator.Temp);
            writer.WriteValueSafe(hit.Player.NetworkObjectId);
            writer.WriteValueSafe((int)hit.Part);
            writer.WriteValueSafe((byte)hit.Kind);
            writer.WriteValueSafe(hit.Index);
            writer.WriteValueSafe(hit.Contact);
            manager.SendNamedMessageToAll(HitMessage, writer, NetworkDelivery.ReliableSequenced);
        }

        void HandleHitMessage(ulong senderId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out ulong playerId);
            reader.ReadValueSafe(out int part);
            reader.ReadValueSafe(out byte kind);
            reader.ReadValueSafe(out int index);
            reader.ReadValueSafe(out Vector3 contact);

            NetworkPlayer player = null;
            foreach (var candidate in NetworkPlayer.All)
                if (candidate.NetworkObjectId == playerId) player = candidate;
            RaiseHit(new SawHit(player, (BodyPart)part, (SawKind)kind, index, contact));
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
