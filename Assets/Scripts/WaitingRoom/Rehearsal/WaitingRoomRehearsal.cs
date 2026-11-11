using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Settings;
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
    /// 현재 소구간의 외줄 코스만 만들고(기존 코스 생성 모드·외부 관리·연습 모드) 호스트가 완료·추락 복구를 판정한다.
    /// 진행(소구간·회차·완료)은 이름 붙은 메시지로 모두에게 공유하고, 각 화면은 자기 캐릭터를 출발대로 옮기고 퀘스트를 띄운다.
    /// 전 단계를 마치면 무대 입구 통로로 길안내하고, 전원이 모이면 <see cref="StageEntranceGather"/>가 첫 공연으로 넘긴다.
    /// </summary>
    public sealed partial class WaitingRoomRehearsal : MonoBehaviour
    {
        const string StateMessage = "CurtainCall.Rehearsal.State";
        const string RequestMessage = "CurtainCall.Rehearsal.Request";
        /// <summary>코스가 링 바닥에서 떠 있는 높이(m). 줄에서 떨어지면 바닥까지 내려와 추락으로 판정된다(추락 깊이 1m).</summary>
        const float CourseHeight = 2.5f;
        const string RingDeckPath = "LegacyRing/PracticeRing/DeckCollider";
        const string RingBoundaryPath = "RoundArena/Frame8Boundary";
        /// <summary>연습실 링에 원래 있던 연습 소품(탑·사다리·트램펄린·대포·그물·옛 줄)과 사다리 뒤 막이. 리허설 동안 숨긴다(사용자 요청 2026-11-02·04).</summary>
        static readonly string[] RingPropPaths = { "LegacyRing/PracticeLanes", "LegacyRing/CollisionRepairs" };
        static readonly Vector3 FallbackRingCenter = new(-20f, .6f, 128.5f);

        enum Operation : byte { Sync, Vote, Placed, SkipVote, PracticePropose, PracticeAnswer }

        static WaitingRoomRehearsal instance;
        // 앱을 켜 둔 동안 기억하는 진행(호스트 이어하기). 세대가 바뀌면 리허설이 없으므로 그대로 둔다.
        static int savedStage;

        /// <summary>리허설 3단계를 모두 마쳤다(무대 입구 집결 대기). 호스트 판정이 공유된 값.</summary>
        public static bool IsCompleted => instance != null && instance.active && instance.completed;
        /// <summary>리허설 진행 중(대기실 씬·리허설 시간대).</summary>
        public static bool IsActive => instance != null && instance.active;
        public static bool UsesInitialTutorialRules => instance != null && instance.active && !instance.practice
            && instance.run != null && instance.run.State.Phase == CampaignPhase.Rehearsal;
        bool RecoversAfterDeath => UsesInitialTutorialRules || practice && GameSettings.WaitingRoom.Rehearsal.RecoverAfterDeath;
        /// <summary>지금 소구간(0부터). 진행 전이면 -1.</summary>
        public static int CurrentStage => instance != null && instance.active ? instance.stage : -1;
        /// <summary>이수 후 선택 재연습 중(CC-219). 최초 리허설과 달리 캠페인 단계·이수 기록을 바꾸지 않는다.</summary>
        public static bool IsPracticing => instance != null && instance.active && instance.practice;
        /// <summary>재연습 투표 또는 다음 프레임의 코스 생성을 기다리는 시작 요청.</summary>
        public static bool IsPracticePending => instance != null && (instance.practiceVote || instance.practice);
        /// <summary>최초 리허설을 이수했다(앱을 켜 둔 동안 기억).</summary>
        public static bool HasMastered => savedStage >= RehearsalStage.All.Length;

        /// <summary>호스트: 이수 뒤 연습실에서 고른 소구간부터 다시 연습한다. 완료 단계는 건너뛸 수 있다.</summary>
        public static bool StartPractice(int stageIndex)
        {
            var r = instance;
            if (r == null || !r.CanOfferPractice || stageIndex < 0 || stageIndex >= RehearsalStage.All.Length) return false;
            r.practice = true; r.practiceStage = stageIndex;
            Debug.Log($"[Rehearsal] 선택 재연습 시작: 소구간 {RehearsalStage.All[stageIndex].Id}");
            return true;
        }

        /// <summary>호스트: 재연습을 언제든 끝낸다. 이수 기록은 그대로다.</summary>
        public static bool EndPractice()
        {
            var r = instance;
            if (r == null || !r.practice || !r.HasAuthority) return false;
            r.ReleaseLessonDevices();
            r.practice = false; r.version++; r.Publish();
            Debug.Log("[Rehearsal] 선택 재연습 종료");
            return true;
        }

        /// <summary>Dev 선택으로 다시 시작할 때만 최초 교육 이수 기억을 초기화한다.</summary>
        public static void ResetForDevLaunch()
        {
            if (!CurtainCall.Dev.DevMode.Enabled) return;
            savedStage = 0;
            if (instance == null) return;
            if (instance.active) instance.Deactivate();
            instance.builtStage = -1;
            instance.builtRound = -1;
        }

        // ── 공유 상태(호스트가 정하고 모두 같게 본다) ──
        int stage = -1, round, version;
        bool completed;
        // 선택 재연습(CC-219). 호스트가 켜고 끄며 모두 같게 본다. 이수 기록(savedStage)은 건드리지 않는다.
        bool practice;
        int practiceStage;

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
        GameObject hiddenRingBoundary;
        GameObject courseRoot;
        TightropeCourse course;
        TightropeRun courseRun;
        int builtStage = -1, builtRound = -1;
        bool courseStarted;
        double nextSync;

        // ── 호스트 ──
        readonly Dictionary<ulong, double> fallenAt = new();
        readonly HashSet<ulong> arrived = new(), votes = new();
        readonly HashSet<ulong> placed = new();
        int participantCount;
        int voteRequestRound;

        bool CanVote => active && !completed && arrived.Count > 0 && local != null
            && local.IsSpawned && local.State != PlayerState.Retired;

        void RecordAction(string state, ulong? actor = null, string reason = null, bool? vote = null) =>
            CurtainCall.Telemetry.Pipeline.SpatialLogAdapter.RehearsalAction("TIGHTROPE", state, round,
                active ? stage : -1, active, IsCompleted, IsPracticing, actor, true, reason, vote);

        void SetVote(ulong id, bool value)
        {
            if (!active || completed || arrived.Count == 0) return;
            bool participant = false;
            foreach (var player in NetworkPlayer.All)
                if (player != null && player.IsSpawned && player.OwnerClientId == id && player.State != PlayerState.Retired)
                    participant = true;
            if (!participant) return;
            bool changed = value ? votes.Add(id) : votes.Remove(id);
            if (changed) { RecordAction("VOTE_CHANGED", id, "NEXT_STAGE", value); version++; Publish(); }
        }

        void RequestVote(bool value)
        {
            if (!CanVote) return;
            if (HasAuthority) { SetVote(local.OwnerClientId, value); return; }
            if (messaging == null) return;
            using var writer = new FastBufferWriter(16, Allocator.Temp);
            writer.WriteValueSafe((byte)Operation.Vote);
            writer.WriteValueSafe(round);
            writer.WriteValueSafe(value);
            messaging.SendNamedMessage(RequestMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }

        bool HasAuthority => run != null && run.HasAuthority;
        static bool PracticePhase(CampaignPhase phase) => phase == CampaignPhase.RestTime || phase == CampaignPhase.Night;
        /// <summary>호스트가 연습실 안에 있고 이수했으며 리허설·재연습 중이 아니면 재연습을 고를 수 있다.</summary>
        bool CanOfferPractice => !active && !practice && !practiceVote && !WaitingRoomCannonRehearsal.IsActive && HasAuthority && HasMastered && PracticePhase(run.State.Phase)
            && !SceneCurtainTransition.IsActive && SpaceLinkDirector.Instance != null && SpaceLinkDirector.Instance.Current == SpaceId.WaitingRoom
            && !SpaceLinkDirector.Instance.IsTransitioning && ringFound
            && local != null && local.IsSpawned && local.State == PlayerState.Normal && InPracticeRoom(local.transform.position);
        double Clock => session != null && session.NetworkManager != null && session.NetworkManager.IsListening
            ? session.NetworkManager.ServerTime.Time : Time.timeAsDouble;
        RehearsalStage Stage => stage >= 0 && stage < RehearsalStage.All.Length ? RehearsalStage.All[stage] : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; savedStage = 0; PiggybackSystem.Suppressed = false; }

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
            AwakeVotes();
        }

        void OnDestroy()
        {
            Unregister();
            DestroyLocal();
            PiggybackSystem.Suppressed = false;
            if (instance == this) instance = null;
        }

        void Update()
        {
            run = CampaignRun.Instance;
            session = NetworkSessionManager.Instance;
            Connect();
            TickRestriction();
            bool inWaitingRoom = !SceneCurtainTransition.IsActive && SpaceLinkDirector.Instance != null
                && SpaceLinkDirector.Instance.Current == SpaceId.WaitingRoom && !SpaceLinkDirector.Instance.IsTransitioning && FindRing();
            // 재연습은 대기실 쉬는 시간·밤에만. 공연으로 넘어가면 호스트가 끝낸다.
            if (practice && HasAuthority && (run == null || !PracticePhase(run.State.Phase))) EndPractice();
            bool wanted = run != null && (run.State.Phase == CampaignPhase.Rehearsal || practice) && inWaitingRoom;
            HostTickVotes();
            TickVoteUi();
            if (!wanted)
            {
                if (active) Deactivate();
                // 참가자는 호스트의 재연습 시작을 알기 위해 가끔 상태를 묻는다.
                if (!HasAuthority && messaging != null && inWaitingRoom && Clock >= nextSync) { Send(Operation.Sync); nextSync = Clock + 3d; }
                TickPracticeOffer();
                return;
            }
            if (!active) Activate();
            // 모든 외줄 소구간에서 실제 공연의 목마 연결 규칙을 보존한다.
            PiggybackSystem.Suppressed = false;
            EnsureLockdownProps();
            UpdateTriggerGeometry();
            if (HasAuthority) HostTick();
            else if (stage < 0 && Clock >= nextSync) { Send(Operation.Sync); nextSync = Clock + 2d; }
            EnsureCourse();
            LocalTick();
            TickMonitors();
        }

        void Activate()
        {
            active = true;
            foreach (var monitor in monitors) if (monitor != null) monitor.ResetPlayback();
            fallenAt.Clear(); arrived.Clear(); votes.Clear(); placed.Clear(); participantCount = 0;
            if (HasAuthority)
            {
                // 호스트: 기억해 둔 소구간부터. 완료했으면 바로 집결 안내. 재연습은 고른 소구간부터.
                stage = practice ? practiceStage : Mathf.Clamp(savedStage, 0, RehearsalStage.All.Length - 1);
                completed = !practice && savedStage >= RehearsalStage.All.Length;
                practiceSealed = false; monitorStarted = -1d;
                round++; version++;
                Publish();
            }
            else { stage = -1; nextSync = 0d; }
            HideRingProps(true);
            ActivateLocal();
        }

        void Deactivate()
        {
            active = false;
            PiggybackSystem.Suppressed = false;
            foreach (var monitor in monitors) if (monitor != null) monitor.TurnOff();
            DestroyCourse();
            SetRingBoundaryHidden(false);
            HideRingProps(false);
            DeactivateLocal();
        }

        // ── 연습실 링 찾기 ───────────────────────────────────

        bool FindRing()
        {
            // 대기실 씬을 다시 불러오면 앞 씬의 연습실은 파괴된다. 그대로 쓰면 소품을 숨기지 못하므로 다시 찾는다(2026-11-04).
            if (ringFound && ringRoom != null) return true;
            ringFound = false;
            ringRoom = WaitingRoomScenes.FindPracticeRoom();
            if (ringRoom == null) return false;
            // 이수 뒤 연습실 외줄 상호작용으로 리허설 다시 하기 투표를 연다.
            var practiceRope = ringRoom.Find("LegacyRing/PracticeLanes/Rope");
            if (practiceRope != null && practiceRope.GetComponent<RehearsalPracticeStation>() == null) practiceRope.gameObject.AddComponent<RehearsalPracticeStation>();
            var deck = ringRoom.Find(RingDeckPath);
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

        void SetRingBoundaryHidden(bool hide)
        {
            if (!hide)
            {
                if (hiddenRingBoundary != null) hiddenRingBoundary.SetActive(true);
                hiddenRingBoundary = null;
                return;
            }
            if (hiddenRingBoundary != null || ringRoom == null) return;
            var boundary = ringRoom.Find(RingBoundaryPath);
            if (boundary == null || !boundary.gameObject.activeSelf) return;
            hiddenRingBoundary = boundary.gameObject;
            hiddenRingBoundary.SetActive(false);
        }

        void EnsureCourse()
        {
            int wanted = active && !completed ? stage : -1;
            // 교육 코스에는 대체 외곽 벽이 없다. 진행·집결 모두 기존 연습실 경계를 유지한다.
            SetRingBoundaryHidden(false);
            if (builtStage == wanted && builtRound == round) return;
            DestroyCourse();
            builtStage = wanted; builtRound = round;
            if (wanted >= 0)
            {
                BuildCourse(RehearsalStage.All[wanted]);
            }
            OnCourseChanged();
        }

        void BuildCourse(RehearsalStage s)
        {
            // 공연과 같은 아트·조작·진행 컴포넌트가 저장된 준비 프리팹을 사용한다.
            var prefab = Resources.Load<GameObject>("Prefabs/Objects/ETCs/TightropeArtKit/Prepared/TightropeCoursePrepared");
            if (prefab == null) { Debug.LogError("[Rehearsal] 공연 코스 프리팹이 없어 리허설을 만들 수 없습니다."); return; }
            var staging = new GameObject("Rehearsal staging");
            staging.SetActive(false);
            courseRoot = Instantiate(prefab, staging.transform, false);
            courseRoot.SetActive(false);
            courseRoot.transform.SetParent(null, false);
            Destroy(staging);
            courseRoot.name = $"RehearsalCourse_{s.Id}";
            courseRoot.transform.position = ringCenter + new Vector3(0f, CourseHeight, -(s.Finish - 1f) * .5f);
            course = courseRoot.GetComponent<TightropeCourse>();
            string courseJson = s.CourseJson;
            var courseShape = TightropeSettings.CourseSettings.FromJson(courseJson);
            var boundary = ringRoom != null ? ringRoom.Find(RingBoundaryPath + "/WallColliders") : null;
            course.FinishPlatformScale = RehearsalPlatformLayout.FitFinishScale(courseShape, courseRoot.transform, ringCenter, boundary, 2f);
            course.ImportShape(courseJson);
            course.SetLocked(true, "리허설 단계 배치");
            // 리허설 단계는 이미 호스트가 공유한다. 공연 대기실용 모양 동기화가 덮어쓰지 않게 한다.
            var shapeSync = courseRoot.GetComponent<CourseShapeSync>();
            if (shapeSync != null) shapeSync.enabled = false;
            courseRun = courseRoot.GetComponent<TightropeRun>();
            courseRun.SetExternallyManaged(true);
            courseRun.SetPracticeMode(true);
            course.CompactPracticeLayout = true;
            if (s.UsesPoles)
            {
                var trapeze = courseRoot.GetComponent<TightropeTrapeze>() ?? courseRoot.AddComponent<TightropeTrapeze>();
                trapeze.ConfigurePracticeLesson(s.Kind == RehearsalStageKind.SinglePole ? 1 : 2,
                    s.Kind != RehearsalStageKind.Trapeze);
            }
            if (courseRoot.GetComponent<RopeLaneJumpRules>() == null) courseRoot.AddComponent<RopeLaneJumpRules>();
            // 공연 씬에 별도로 붙은 공통 기능도 모든 소구간에 원본 그대로 연결한다.
            // 목마도 공연처럼 모든 외줄 소구간에서 같은 연결 규칙을 쓴다.
            if (PiggybackSystem.Current == null || !PiggybackSystem.Current.isActiveAndEnabled)
            {
                var stack = courseRoot.AddComponent<PiggybackSystem>();
                stack.SuppressLegacyFeedback = true;
                courseRoot.AddComponent<PiggybackFollow>();
            }
            if (TightropeCatchSystem.Current == null) courseRoot.AddComponent<TightropeCatchSystem>();
            if (courseRoot.GetComponent<TightropeBalancePoseView>() == null) courseRoot.AddComponent<TightropeBalancePoseView>();
            if (courseRoot.GetComponent<TightropeHandholdRangeView>() == null) courseRoot.AddComponent<TightropeHandholdRangeView>();
            BuildPerformanceUi();
            courseRoot.SetActive(true);
            if (s.Kind == RehearsalStageKind.LaneChange) course.SetIntermediatePlatformLanes(new[] { 0 });
            if (s.Kind == RehearsalStageKind.Relay)
            {
                var relay = courseRoot.GetComponent<TightropeRelayGate>();
                relay.UseSpots(new[] { new RelaySpot(0, 11f, 1, 12f, 20f) });
            }
            courseStarted = false;
            Debug.Log($"[Rehearsal] 소구간 {s.Id} 코스 생성(회차 {round})");
        }

        void DestroyCourse()
        {
            if (courseRoot != null) { courseRoot.SetActive(false); Destroy(courseRoot); }
            courseRoot = null; course = null; courseRun = null; courseStarted = false;
        }

        // ── 호스트 판정 ─────────────────────────────────────

        /// <summary>공통 튜토리얼 설정에 따라 실제 코스 아래 바닥 착지를 재시도로 처리한다.</summary>
        public static bool ShouldRestartAfterFloorLanding(NetworkPlayer player, Vector3 feet)
        {
            var r = instance;
            if (!UsesInitialTutorialRules || !GameSettings.WaitingRoom.Tutorial.ResetOnFloorFall
                || r.completed || r.course == null || player == null || !r.placed.Contains(player.OwnerClientId)
                || !player.TryGetComponent(out PlayerMover mover)) return false;
            return feet.y < r.course.transform.position.y - r.course.RopeHeightTolerance
                && !r.course.TryGetSupportingRopeAt(mover, feet, out _)
                && WalkableSupport.Contains(mover, feet);
        }

        void HostTick()
        {
            HostTickPracticeSeal();
            if (completed) { HostRecoverFallen(); return; }
            if (courseRun != null && !courseStarted && session != null && session.GameState == GameSessionState.Playing)
                courseStarted = courseRun.StartExternalPerformance();
            HostRecoverFallen();
            HostJudgeStage();
        }

        /// <summary>추락한 사람은 대가 없이 잠시 뒤 보통 상태로 되돌린다(소유자 화면이 출발대로 옮긴다).</summary>
        void HostRecoverFallen()
        {
            if (!RecoversAfterDeath) { fallenAt.Clear(); return; }
            bool changed = false;
            foreach (var player in NetworkPlayer.All)
            {
                if (player == null || !player.IsSpawned) continue;
                ulong id = player.OwnerClientId;
                if (player.State != PlayerState.Fallen) { fallenAt.Remove(id); continue; }
                // A previous arrival must not imprison a retry at the finish barrier.
                changed |= arrived.Remove(id);
                changed |= votes.Remove(id);
                changed |= placed.Remove(id);
                lessonSamples.Remove(id);
                if (!fallenAt.TryGetValue(id, out double at)) { fallenAt[id] = Clock; RecordAction("FALLEN", id); continue; }
                float delay = practice ? GameSettings.WaitingRoom.Rehearsal.RecoverSeconds : GameSettings.WaitingRoom.Tutorial.RecoverSeconds;
                if (Clock - at < delay) continue;
                fallenAt.Remove(id);
                HumanCannonSystem.Current?.ServerReleaseForInspection(id);
                TightropeHangSystem.Current?.ServerReleaseForInspection(id);
                PiggybackSystem.Current?.ServerReleaseForInspection(id);
                player.ServerRestoreAll();
            }
            if (changed) { version++; Publish(); }
        }

        void HostJudgeStage()
        {
            var s = Stage;
            if (s == null || course == null || builtRound != round) return;
            var participants = new HashSet<ulong>();
            bool changed = false;
            foreach (var player in NetworkPlayer.All)
            {
                if (player == null || !player.IsSpawned || player.State == PlayerState.Retired) continue;
                participants.Add(player.OwnerClientId);
                // 소유자 배치와 호스트 위치 수신을 확인하기 전에는 이전 소구간 좌표를 판정하지 않는다.
                if (placed.Contains(player.OwnerClientId))
                {
                    changed |= ObserveLesson(player);
                    if ((!s.RequiresSkillForArrival || LearnedSkill(player)) && (s.Kind == RehearsalStageKind.Trapeze || IsFinished(player))
                        && arrived.Add(player.OwnerClientId))
                    {
                        changed = true;
                        RecordAction("ARRIVED", player.OwnerClientId, s.Kind == RehearsalStageKind.Trapeze ? "LESSON_CRITERION" : "FINISH_PLATFORM");
                    }
                }
            }
            changed |= arrived.RemoveWhere(id => !participants.Contains(id)) > 0;
            changed |= placed.RemoveWhere(id => !participants.Contains(id)) > 0;
            changed |= votes.RemoveWhere(id => !participants.Contains(id)) > 0;
            if (arrived.Count == 0 && votes.Count > 0) { votes.Clear(); changed = true; }
            if (participantCount != participants.Count) { participantCount = participants.Count; changed = true; }
            if (changed) { version++; Publish(); }
            // 재연습은 도착 후에도 왕복할 수 있다. 다음 구간은 기존 '다음' 과반 투표로만 진행한다.
            if (practice && votes.Count <= participantCount / 2) return;
            if (!s.CanAdvance(participantCount, arrived.Count, votes.Count)) return;
            RecordAction("STAGE_CLEARED", reason: arrived.Count == participantCount ? "ALL_ARRIVED" : s.Rule == RehearsalClearRule.Anyone ? "ANY_ARRIVED" : "MAJORITY_NEXT");
            AdvanceStage();
        }

        void ReleaseLessonDevices()
        {
            // Finishing a lesson can teleport living players too. Clear the shared
            // device state before relocation, not only the owner's local pilot.
            foreach (var player in NetworkPlayer.All)
            {
                if (player == null || !player.IsSpawned) continue;
                HumanCannonSystem.Current?.ServerReleaseForInspection(player.OwnerClientId);
                TightropeHangSystem.Current?.ServerReleaseForInspection(player.OwnerClientId);
                PiggybackSystem.Current?.ServerReleaseForInspection(player.OwnerClientId);
            }
        }

        void AdvanceStage()
        {
            ReleaseLessonDevices();
            arrived.Clear(); votes.Clear(); placed.Clear(); participantCount = 0;
            lessonProgress.Clear(); lessonSamples.Clear(); lessonRound = -1;
            stage++;
            if (practice && stage >= RehearsalStage.All.Length)
            {
                // 재연습은 마지막 소구간을 마치면 그냥 끝난다. 이수 기록·집결 흐름은 건드리지 않는다.
                stage = RehearsalStage.All.Length - 1;
                EndPractice();
                return;
            }
            if (stage >= RehearsalStage.All.Length) { stage = RehearsalStage.All.Length - 1; completed = true; }
            if (!practice) savedStage = completed ? RehearsalStage.All.Length : stage;
            if (completed)
                foreach (var player in NetworkPlayer.All)
                    if (player != null && player.IsSpawned && player.State == PlayerState.Fallen) player.ServerRestoreAll();
            round++; version++;
            Debug.Log(completed ? "[Rehearsal] 리허설 완료 → 무대 입구 통로 집결" : $"[Rehearsal] 소구간 {RehearsalStage.All[stage].Id} 시작");
            Publish();
        }

        public bool CanClearCurrentStage => active && !completed && HasAuthority && course != null
            && builtRound == round && !SceneCurtainTransition.IsActive;

        /// <summary>호스트가 현재 소구간만 즉시 완료한다. 정상 도착과 같은 진행·공유 경로를 사용한다.</summary>
        public bool TryClearCurrentStage()
        {
            if (!CanClearCurrentStage) return false;
            foreach (var player in NetworkPlayer.All)
                if (player != null && player.IsSpawned && player.State == PlayerState.Fallen) player.ServerRestoreAll();
            RecordAction("STAGE_CLEARED", reason: "DEV_CLEAR");
            AdvanceStage();
            return true;
        }

        /// <summary>도착대 위에 서 있는지(위치 기준, 모든 화면이 같은 코스를 가진다).</summary>
        bool IsFinished(NetworkPlayer player)
        {
            if (course == null || player.State != PlayerState.Normal || player.IsAirborne
                || !course.TryGetFinishPlatformBounds(out var floor)) return false;
            var p = player.transform.position;
            float side = course.GetSide(p), distance = course.GetDistance(p);
            return side >= floor.min.x && side <= floor.max.x && distance >= floor.min.z && distance <= floor.max.z
                && Mathf.Abs(p.y - course.transform.position.y - floor.max.y) < .8f;
        }

        // ── 공유 ───────────────────────────────────────────

        void Connect()
        {
            var state = session != null ? session.ConnectionState : SessionConnectionState.Offline;
            var next = state is SessionConnectionState.Host or SessionConnectionState.Client
                ? session.NetworkManager?.CustomMessagingManager : null;
            if (registered && registeredFor == state && messaging == next) return;
            bool previousClient = registered && registeredFor == SessionConnectionState.Client;
            Unregister();
            if (previousClient || state == SessionConnectionState.Client) ResetClientSession();
            messaging = next;
            registeredFor = state;
            if (messaging == null) return;
            registered = true;
            if (state == SessionConnectionState.Host) messaging.RegisterNamedMessageHandler(RequestMessage, HandleRequest);
            else if (state == SessionConnectionState.Client) messaging.RegisterNamedMessageHandler(StateMessage, HandleState);
        }

        void ResetClientSession()
        {
            if (active) Deactivate();
            version = round = 0; stage = builtStage = builtRound = -1;
            completed = practice = ending = practiceVote = practiceSealed = false;
            endingShown = practiceVoteShown = false;
            participantCount = voterCount = 0;
            monitorStarted = -1d; nextSync = 0d;
            arrived.Clear(); votes.Clear(); placed.Clear(); fallenAt.Clear();
            skipVotes.Clear(); practiceYes.Clear(); lessonProgress.Clear(); lessonSamples.Clear(); lessonRound = -1;
            CancelInvoke(nameof(BeginEndingFade));
            votePanel?.Hide(); fader?.Cancel();
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
            using var writer = new FastBufferWriter(1024, Allocator.Temp);
            writer.WriteValueSafe(version);
            writer.WriteValueSafe(stage);
            writer.WriteValueSafe(round);
            writer.WriteValueSafe(completed);
            writer.WriteValueSafe(practice);
            writer.WriteValueSafe(practiceSealed);
            writer.WriteValueSafe(monitorStarted);
            writer.WriteValueSafe(participantCount);
            writer.WriteValueSafe(placed.Count);
            foreach (ulong id in placed) writer.WriteValueSafe(id);
            writer.WriteValueSafe(arrived.Count);
            foreach (ulong id in arrived) writer.WriteValueSafe(id);
            writer.WriteValueSafe(votes.Count);
            foreach (ulong id in votes) writer.WriteValueSafe(id);
            WriteLesson(writer);
            WriteVotes(writer);
            if (only.HasValue) messaging.SendNamedMessage(StateMessage, only.Value, writer, NetworkDelivery.ReliableSequenced);
            else messaging.SendNamedMessageToAll(StateMessage, writer, NetworkDelivery.ReliableSequenced);
        }

        void Send(Operation operation)
        {
            if (HasAuthority) return;
            if (messaging == null) return;
            using var writer = new FastBufferWriter(2, Allocator.Temp);
            writer.WriteValueSafe((byte)operation);
            messaging.SendNamedMessage(RequestMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }

        void HandleRequest(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out byte op);
            if ((Operation)op == Operation.Sync) Publish(sender);
            else if ((Operation)op == Operation.Vote)
            {
                reader.ReadValueSafe(out voteRequestRound);
                reader.ReadValueSafe(out bool value);
                if (voteRequestRound == round) SetVote(sender, value);
            }
            else if ((Operation)op == Operation.Placed)
            {
                reader.ReadValueSafe(out int placementRound);
                reader.ReadValueSafe(out Vector3 position);
                AcceptPlacement(sender, placementRound, position);
            }
            else if ((Operation)op is Operation.PracticePropose or Operation.PracticeAnswer)
                HandleVoteRequest(sender, (Operation)op, reader);
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
            reader.ReadValueSafe(out practice);
            reader.ReadValueSafe(out practiceSealed);
            reader.ReadValueSafe(out monitorStarted);
            reader.ReadValueSafe(out participantCount);
            placed.Clear();
            reader.ReadValueSafe(out int placements);
            for (int i = 0; i < placements; i++) { reader.ReadValueSafe(out ulong id); placed.Add(id); }
            arrived.Clear(); votes.Clear();
            reader.ReadValueSafe(out int arrivals);
            for (int i = 0; i < arrivals; i++) { reader.ReadValueSafe(out ulong id); arrived.Add(id); }
            reader.ReadValueSafe(out int count);
            for (int i = 0; i < count; i++) { reader.ReadValueSafe(out ulong id); votes.Add(id); }
            ReadLesson(reader);
            ReadVotes(reader);
        }
    }
}
