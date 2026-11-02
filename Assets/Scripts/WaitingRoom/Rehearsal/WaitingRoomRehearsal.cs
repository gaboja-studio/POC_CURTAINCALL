using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.SpaceLink;
using CurtainCall.Tightrope;
using CurtainCall.UI.SceneIntegration;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 최초 리허설 진행(Task-097, 기획서 2~8장). 런이 <see cref="CampaignPhase.Rehearsal"/>이고 대기실 씬이면 연습실 링 위에
    /// 현재 소구간의 외줄 코스만 만들고(기존 코스 생성 모드·외부 관리·연습 모드) 호스트가 완료·추락 복구·포기 투표를 판정한다.
    /// 진행(소구간·회차·완료·포기)은 이름 붙은 메시지로 모두에게 공유하고, 각 화면은 자기 캐릭터를 출발대로 옮기고 퀘스트를 띄운다.
    /// 전 단계를 마치면 무대 입구 통로로 길안내하고, 전원이 모이면 <see cref="StageEntranceGather"/>가 첫 공연으로 넘긴다.
    /// 완료한 소구간은 앱을 켜 둔 동안 기억해 포기 뒤 재진입하면 이어서 한다(기획서 8장).
    /// </summary>
    public sealed partial class WaitingRoomRehearsal : MonoBehaviour
    {
        const string StateMessage = "CurtainCall.Rehearsal.State";
        const string RequestMessage = "CurtainCall.Rehearsal.Request";
        /// <summary>코스가 링 바닥에서 떠 있는 높이(m). 줄에서 떨어지면 바닥까지 내려와 추락으로 판정된다(추락 깊이 1m).</summary>
        const float CourseHeight = 2.5f;
        /// <summary>추락 뒤 출발대로 되돌리기까지(초).</summary>
        const float RecoverSeconds = 1.5f;
        /// <summary>과반수 포기 뒤 종료까지(초, 기획서 16.1).</summary>
        const float GiveUpCountdownSeconds = 30f;
        /// <summary>링 가운데에서 이 거리(m) 안에 살아 있는 사람이 들어오면 포기한 리허설에 다시 들어온 것으로 본다.</summary>
        const float RingRadius = 18f;
        const string RingDeckPath = "LegacyRing/PracticeRing/DeckCollider";
        /// <summary>연습실 링에 원래 있던 연습 소품(탑·사다리·트램펄린·대포·그물·옛 줄). 리허설 동안 숨긴다(사용자 요청 2026-11-02).</summary>
        static readonly string[] RingPropPaths = { "LegacyRing/PracticeLanes" };
        static readonly Vector3 FallbackRingCenter = new(-20f, .6f, 128.5f);

        enum Operation : byte { Sync, GiveUp }

        static WaitingRoomRehearsal instance;
        // 앱을 켜 둔 동안 기억하는 진행(포기 뒤 재진입·호스트 이어하기). 세대가 바뀌면 리허설이 없으므로 그대로 둔다.
        static int savedStage;

        /// <summary>리허설 3단계를 모두 마쳤다(무대 입구 집결 대기). 호스트 판정이 공유된 값.</summary>
        public static bool IsCompleted => instance != null && instance.active && instance.completed;
        /// <summary>리허설 진행 중(대기실 씬·리허설 시간대).</summary>
        public static bool IsActive => instance != null && instance.active;
        /// <summary>지금 소구간(0부터). 진행 전이면 -1.</summary>
        public static int CurrentStage => instance != null && instance.active ? instance.stage : -1;

        // ── 공유 상태(호스트가 정하고 모두 같게 본다) ──
        int stage = -1, round, version;
        bool completed, abandoned;
        int giveUpVotes, giveUpRequired;
        double giveUpDeadline = -1d;

        // ── 이 화면 ──
        bool active;
        CampaignRun run;
        NetworkSessionManager session;
        CustomMessagingManager messaging;
        SessionConnectionState registeredFor = SessionConnectionState.Offline;
        bool registered;
        Vector3 ringCenter;
        bool ringFound;
        Transform ringRoom;
        readonly List<GameObject> hiddenRingProps = new();
        GameObject courseRoot;
        TightropeCourse course;
        TightropeRun courseRun;
        int builtStage = -1, builtRound = -1;
        bool courseStarted;
        double nextSync;

        // ── 호스트 ──
        readonly Dictionary<ulong, double> fallenAt = new();
        readonly HashSet<ulong> votes = new();

        bool HasAuthority => run != null && run.HasAuthority;
        double Clock => session != null && session.NetworkManager != null && session.NetworkManager.IsListening
            ? session.NetworkManager.ServerTime.Time : Time.timeAsDouble;
        RehearsalStage Stage => stage >= 0 && stage < RehearsalStage.All.Length ? RehearsalStage.All[stage] : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; savedStage = 0; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance != null) return;
            var go = new GameObject(nameof(WaitingRoomRehearsal));
            DontDestroyOnLoad(go);
            instance = go.AddComponent<WaitingRoomRehearsal>();
        }

        void Awake()
        {
            AwakeLocal();
        }

        void OnDestroy()
        {
            Unregister();
            DestroyLocal();
            if (instance == this) instance = null;
        }

        void Update()
        {
            run = CampaignRun.Instance;
            session = NetworkSessionManager.Instance;
            Connect();
            TickRestriction();
            bool wanted = run != null && run.State.Phase == CampaignPhase.Rehearsal && !SceneCurtainTransition.IsActive
                && SpaceLinkDirector.Instance != null && SpaceLinkDirector.Instance.Current == SpaceId.WaitingRoom
                && !SpaceLinkDirector.Instance.IsTransitioning && FindRing();
            if (!wanted) { if (active) Deactivate(); return; }
            if (!active) Activate();
            EnsureLockdownProps();
            if (HasAuthority) HostTick();
            else if (stage < 0 && Clock >= nextSync) { Send(Operation.Sync); nextSync = Clock + 2d; }
            EnsureCourse();
            ApplyGaps();
            TickStageProps();
            LocalTick();
            TickMonitors();
        }

        void Activate()
        {
            active = true;
            if (HasAuthority)
            {
                // 호스트: 기억해 둔 소구간부터. 완료했으면 바로 집결 안내.
                stage = Mathf.Clamp(savedStage, 0, RehearsalStage.All.Length - 1);
                completed = savedStage >= RehearsalStage.All.Length;
                abandoned = false;
                practiceSealed = false;
                votes.Clear(); giveUpVotes = 0; giveUpDeadline = -1d;
                round++; version++;
                Publish();
            }
            else { stage = -1; nextSync = 0d; }
            fallenAt.Clear();
            HideRingProps(true);
            ActivateLocal();
        }

        void Deactivate()
        {
            active = false;
            foreach (var monitor in monitors) if (monitor != null) monitor.TurnOff();
            DestroyCourse();
            HideRingProps(false);
            DeactivateLocal();
            if (HasAuthority) votes.Clear();
        }

        // ── 연습실 링 찾기 ───────────────────────────────────

        bool FindRing()
        {
            if (ringFound) return true;
            var room = GameObject.Find("S09_PracticeRoom");
            if (room == null) return false;
            ringRoom = room.transform;
            var deck = room.transform.Find(RingDeckPath);
            var collider = deck != null ? deck.GetComponent<Collider>() : null;
            if (collider != null)
            {
                var b = collider.bounds;
                ringCenter = new Vector3(b.center.x, b.max.y, b.center.z);
            }
            else
            {
                Debug.LogWarning("[Rehearsal] 연습실 링 바닥을 찾지 못해 기본 위치를 씁니다.");
                ringCenter = FallbackRingCenter;
            }
            ringFound = true;
            return true;
        }

        /// <summary>링의 옛 연습 소품을 숨기거나(리허설 시작) 숨긴 것만 되돌린다(리허설 끝).</summary>
        void HideRingProps(bool hide)
        {
            if (!hide)
            {
                foreach (var go in hiddenRingProps) if (go != null) go.SetActive(true);
                hiddenRingProps.Clear();
                return;
            }
            if (ringRoom == null) return;
            foreach (string path in RingPropPaths)
            {
                var prop = ringRoom.Find(path);
                if (prop == null || !prop.gameObject.activeSelf) continue;
                prop.gameObject.SetActive(false);
                hiddenRingProps.Add(prop.gameObject);
            }
        }

        // ── 코스 만들기(모든 화면, 같은 값이라 같은 모양) ─────

        void EnsureCourse()
        {
            int wanted = active && !abandoned && !completed ? stage : -1;
            if (builtStage == wanted && builtRound == round) return;
            DestroyCourse();
            builtStage = wanted; builtRound = round;
            if (wanted >= 0) BuildCourse(RehearsalStage.All[wanted]);
            OnCourseChanged();
        }

        void BuildCourse(RehearsalStage s)
        {
            courseRoot = new GameObject($"RehearsalCourse_{s.Id}");
            courseRoot.SetActive(false);
            // 코스 전진은 월드 +Z. 출발대(-4m)~도착대(+3m)를 링 가운데에 맞춘다.
            courseRoot.transform.position = ringCenter + new Vector3(0f, CourseHeight, -(s.Finish - 1f) * .5f);
            course = courseRoot.AddComponent<TightropeCourse>();
            course.ImportShape(s.CourseJson);
            course.SetLocked(true, "리허설 코스는 소구간 값으로 고정");
            courseRoot.AddComponent<CourseControlSwitcher>();
            courseRun = courseRoot.AddComponent<TightropeRun>();
            courseRun.SetExternallyManaged(true);
            courseRun.SetPracticeMode(true);
            courseRoot.AddComponent<RopeLaneJumpRules>();
            courseRoot.SetActive(true);
            // 코스 값의 끊김 대신 구조도 자리에 그네를 놓는다(줄 끊김은 ApplyGaps가 뺀다).
            courseRoot.GetComponent<TightropeSwingPole>()?.UseSpots(s.Swings);
            // 트리거 그네는 공연 해제봉을 구조도 자리에 놓는다(조작·매달리기는 공연 그대로).
            courseRoot.GetComponent<TightropeRelayGate>()?.UseSpots(RelaySpots(s));
            BuildStageProps(s);
            courseStarted = false;
            Debug.Log($"[Rehearsal] 소구간 {s.Id} 코스 생성(회차 {round})");
        }

        void DestroyCourse()
        {
            DestroyStageProps();
            if (courseRoot != null) { courseRoot.SetActive(false); Destroy(courseRoot); }
            courseRoot = null; course = null; courseRun = null; courseStarted = false;
        }

        // ── 호스트 판정 ─────────────────────────────────────

        void HostTick()
        {
            if (abandoned) { HostCheckReentry(); return; }
            if (completed) { HostTickPracticeSeal(); return; }
            if (courseRun != null && !courseStarted && session != null && session.GameState == GameSessionState.Playing)
                courseStarted = courseRun.StartExternalPerformance();
            HostRecoverFallen();
            HostJudgeStage();
            HostJudgeGiveUp();
        }

        /// <summary>추락한 사람은 대가 없이 잠시 뒤 보통 상태로 되돌린다(소유자 화면이 출발대로 옮긴다).</summary>
        void HostRecoverFallen()
        {
            foreach (var player in NetworkPlayer.All)
            {
                if (player == null || !player.IsSpawned) continue;
                ulong id = player.OwnerClientId;
                if (player.State != PlayerState.Fallen) { fallenAt.Remove(id); continue; }
                if (!fallenAt.TryGetValue(id, out double at)) { fallenAt[id] = Clock; continue; }
                if (Clock - at < RecoverSeconds) continue;
                fallenAt.Remove(id);
                player.ServerRestoreAll();
            }
        }

        void HostJudgeStage()
        {
            var s = Stage;
            if (s == null || course == null) return;
            int alive = 0, finished = 0;
            foreach (var player in NetworkPlayer.All)
            {
                if (player == null || !player.IsSpawned || player.State == PlayerState.Retired) continue;
                alive++;
                if (IsFinished(player)) finished++;
            }
            if (alive == 0 || finished == 0) return;
            if (s.Rule == RehearsalClearRule.Everyone && finished < alive) return;
            stage++;
            if (stage >= RehearsalStage.All.Length) { stage = RehearsalStage.All.Length - 1; completed = true; }
            savedStage = completed ? RehearsalStage.All.Length : stage;
            round++; version++;
            Debug.Log(completed ? "[Rehearsal] 리허설 완료 → 무대 입구 통로 집결" : $"[Rehearsal] 소구간 {RehearsalStage.All[stage].Id} 시작");
            Publish();
        }

        /// <summary>도착대 위에 서 있는지(위치 기준, 모든 화면이 같은 코스를 가진다).</summary>
        bool IsFinished(NetworkPlayer player)
        {
            if (course == null || player.State == PlayerState.Fallen) return false;
            var p = player.transform.position;
            return course.HasReachedFinish(p) && Mathf.Abs(p.y - course.transform.position.y) < .8f
                && course.GetDistance(p) <= course.FinishDistance + course.Shape.FinishPlatformLength + .5f;
        }

        void HostJudgeGiveUp()
        {
            int alive = 0;
            var present = new HashSet<ulong>();
            foreach (var player in NetworkPlayer.All)
                if (player != null && player.IsSpawned && player.State != PlayerState.Retired) { alive++; present.Add(player.OwnerClientId); }
            if (run.IsOfflineRun && alive == 0) { alive = 1; present.Add(NetworkManager.ServerClientId); }
            votes.RemoveWhere(id => !present.Contains(id));
            bool changed = giveUpVotes != votes.Count || giveUpRequired != alive;
            giveUpVotes = votes.Count; giveUpRequired = alive;
            if (giveUpVotes == 0) { if (giveUpDeadline >= 0d) { giveUpDeadline = -1d; changed = true; } }
            else if (giveUpVotes >= alive) { HostAbandon(); return; }
            else if (giveUpVotes * 2 > alive)
            {
                if (giveUpDeadline < 0d) { giveUpDeadline = Clock + GiveUpCountdownSeconds; changed = true; }
                else if (Clock >= giveUpDeadline) { HostAbandon(); return; }
            }
            else if (giveUpDeadline >= 0d) { giveUpDeadline = -1d; changed = true; }
            if (changed) { version++; Publish(); }
        }

        void HostAbandon()
        {
            abandoned = true;
            votes.Clear(); giveUpVotes = 0; giveUpDeadline = -1d;
            round++; version++;
            Debug.Log("[Rehearsal] 포기 → 연습실 밖으로. 다시 들어오면 이어서 한다");
            Publish();
        }

        void HostCheckReentry()
        {
            foreach (var player in NetworkPlayer.All)
            {
                if (player == null || !player.IsSpawned || player.State != PlayerState.Normal) continue;
                var p = player.transform.position;
                if (new Vector2(p.x - ringCenter.x, p.z - ringCenter.z).sqrMagnitude > RingRadius * RingRadius) continue;
                abandoned = false;
                round++; version++;
                Debug.Log("[Rehearsal] 재진입 → 이어서 진행");
                Publish();
                return;
            }
        }

        // ── 공유 ───────────────────────────────────────────

        void Connect()
        {
            var state = session != null ? session.ConnectionState : SessionConnectionState.Offline;
            if (registered && registeredFor == state) return;
            Unregister();
            messaging = session != null && session.NetworkManager != null ? session.NetworkManager.CustomMessagingManager : null;
            if (messaging == null) return;
            registeredFor = state; registered = true;
            if (state == SessionConnectionState.Host) messaging.RegisterNamedMessageHandler(RequestMessage, HandleRequest);
            else if (state == SessionConnectionState.Client) messaging.RegisterNamedMessageHandler(StateMessage, HandleState);
        }

        void Unregister()
        {
            if (!registered) return;
            registered = false;
            if (messaging == null) return;
            messaging.UnregisterNamedMessageHandler(RequestMessage);
            messaging.UnregisterNamedMessageHandler(StateMessage);
            messaging = null;
        }

        void Publish(ulong? only = null)
        {
            if (messaging == null || session == null || !session.IsHost) return;
            using var writer = new FastBufferWriter(64, Allocator.Temp);
            writer.WriteValueSafe(version);
            writer.WriteValueSafe(stage);
            writer.WriteValueSafe(round);
            writer.WriteValueSafe(completed);
            writer.WriteValueSafe(abandoned);
            writer.WriteValueSafe(giveUpVotes);
            writer.WriteValueSafe(giveUpRequired);
            writer.WriteValueSafe(giveUpDeadline);
            writer.WriteValueSafe(practiceSealed);
            if (only.HasValue) messaging.SendNamedMessage(StateMessage, only.Value, writer, NetworkDelivery.ReliableSequenced);
            else messaging.SendNamedMessageToAll(StateMessage, writer, NetworkDelivery.ReliableSequenced);
        }

        void Send(Operation operation)
        {
            if (HasAuthority) { if (operation == Operation.GiveUp) HostToggleVote(NetworkManager.ServerClientId); return; }
            if (messaging == null) return;
            using var writer = new FastBufferWriter(2, Allocator.Temp);
            writer.WriteValueSafe((byte)operation);
            messaging.SendNamedMessage(RequestMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }

        void HostToggleVote(ulong clientId)
        {
            if (!active || abandoned || completed) return;
            if (!votes.Remove(clientId)) votes.Add(clientId);
        }

        void HandleRequest(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out byte op);
            if ((Operation)op == Operation.Sync) Publish(sender);
            else if ((Operation)op == Operation.GiveUp) HostToggleVote(sender);
        }

        void HandleState(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out int incoming);
            if (incoming < version) return;
            version = incoming;
            reader.ReadValueSafe(out stage);
            reader.ReadValueSafe(out round);
            reader.ReadValueSafe(out completed);
            reader.ReadValueSafe(out abandoned);
            reader.ReadValueSafe(out giveUpVotes);
            reader.ReadValueSafe(out giveUpRequired);
            reader.ReadValueSafe(out giveUpDeadline);
            reader.ReadValueSafe(out practiceSealed);
        }
    }
}
