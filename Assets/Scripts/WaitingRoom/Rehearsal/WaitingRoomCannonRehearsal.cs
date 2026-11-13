using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.Campaign;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.SpaceLink;
using CurtainCall.UI.SceneIntegration;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>Native cannon/trampoline rehearsal with common injuries and unanimous arrival.</summary>
    public sealed partial class WaitingRoomCannonRehearsal : MonoBehaviour
    {
        [Serializable] sealed class Shared
        {
            public int version, round;
            public bool active, mastered, ending, successful, skipped;
            public bool restoringCarrier;
            public ulong previousCarrier;
            public double endAt;
            public List<ulong> participants = new(), placed = new(), middle = new(), bounced = new(), arrived = new(), votes = new(), skipVotes = new();
        }
        static WaitingRoomCannonRehearsal instance;
        Shared shared = new();
        readonly HashSet<ulong> fallen = new();
        readonly Dictionary<ulong, double> fallenAt = new();
        readonly Dictionary<ulong, BodyPart> priorConditions = new();
        readonly HashSet<ulong> recoveringPlacement = new();
        readonly List<GameObject> hidden = new();
        readonly List<WaitingRoomFlickerLight> steadyLights = new();
        RehearsalCannonCourse course;
        Transform room;
        Collider roomFloor;
        Vector3 origin, entrance;
        int builtRound = -1, placedRound = -1;
        ulong lastCarrier = CannonSeatState.Nobody;
        NetworkPlayer local;
        PlayerState previousState;
        bool localRecoveryPending;
        bool placingAfterRecovery;
        Pose? lastSafePose;
        float nextPlacement, nextPublish;

        public static bool IsActive => instance != null && instance.shared.active;
        public static bool HasMastered => instance != null && instance.shared.mastered;
        public static bool IsEnding => instance != null && instance.shared.ending && instance.shared.active;
        public static bool CanPractice => instance != null && instance.CanStart;
        public static int Checkpoint(ulong actor) => instance != null && instance.shared.middle.Contains(actor) ? 1 : 0;
        public static bool HasArrived(ulong actor) => instance != null && instance.shared.arrived.Contains(actor);

        internal static bool ShouldRestartAfterFloorLanding(NetworkPlayer player, Vector3 feet)
        {
            var r = instance;
            return r != null && r.shared.active && !r.shared.ending && r.course != null && r.roomFloor != null
                && GameSettings.WaitingRoom.Tutorial.CannonPracticeFloorFallIsFatal
                && player != null && r.shared.placed.Contains(player.OwnerClientId)
                && !r.course.IsRecoveryDeck(feet) && player.TryGetComponent(out PlayerMover mover)
                && WalkableSupport.TryGetSupport(mover, feet, out var hit) && hit.collider == r.roomFloor;
        }
        public RehearsalCannonCourse Course => course;
        bool Host => CampaignRun.Instance != null && CampaignRun.Instance.HasAuthority;
        double Clock => NetworkSessionManager.Instance?.NetworkManager is { IsListening: true } net ? net.ServerTime.Time : Time.timeAsDouble;
        static bool PracticePhase => CampaignRun.Instance != null && CampaignRun.Instance.State.Phase is CampaignPhase.RestTime or CampaignPhase.Night;
        bool CanStart => !shared.active && !CampaignRun.RehearsalEntryBlocked && !WaitingRoomRehearsal.IsActive && !WaitingRoomRehearsal.IsPracticePending && PracticePhase
            && CampaignRun.Instance.State.CannonUnlocked && !SceneCurtainTransition.IsActive
            && SpaceLinkDirector.Instance != null && SpaceLinkDirector.Instance.Current == SpaceId.WaitingRoom && !SpaceLinkDirector.Instance.IsTransitioning;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance != null) return;
            var go = new GameObject(nameof(WaitingRoomCannonRehearsal)); DontDestroyOnLoad(go);
            instance = go.AddComponent<WaitingRoomCannonRehearsal>();
        }
        void Awake() { AwakeNetwork(); AwakeUi(); HumanCannonTrampoline.ServerBounced += Bounced; CampaignRun.TrickDiscovered += OnTrickDiscovered; NetworkPlayer.Revived += OnPlayerRevived; }
        void OnDestroy()
        {
            HumanCannonTrampoline.ServerBounced -= Bounced;
            CampaignRun.TrickDiscovered -= OnTrickDiscovered;
            NetworkPlayer.Revived -= OnPlayerRevived;
            DestroyNetwork(); Cleanup(); ReleaseEndingLock();
            if (instance == this) instance = null;
        }
        void Update()
        {
            TickNetwork();
            if (!UpdatePracticeContext()) return;
            OfferUnlockedRehearsal();
            if (shared.active && !shared.ending)
            {
                if (NetworkPlayer.Local is { IsSpawned: true } owner) CaptureReturnPose(owner);
                if (FindRoom()) EnsureCourse();
                if (Host && course != null) HostTick();
                LocalTick();
            }
            else if (!shared.ending && course != null) Cleanup();
            TickUi();
            TickEnding();
        }

        bool UpdatePracticeContext()
        {
            bool available = PracticePhase && !WaitingRoomRehearsal.IsActive && !SceneCurtainTransition.IsActive
                && SpaceLinkDirector.Instance != null && SpaceLinkDirector.Instance.Current == SpaceId.WaitingRoom
                && !SpaceLinkDirector.Instance.IsTransitioning;
            if (available) return true;
            // A completed round can still be waiting for a remote owner's return.
            // Scene and campaign transitions must cancel that work as well as active practice.
            if (Host && (shared.active || shared.ending))
            {
                shared.active = shared.ending = false;
                PropCarry.Instance?.RestoreRehearsalSelection();
                Publish();
            }
            prompt = false;
            promptBlock?.SetOpen(false);
            placementBlock?.SetOpen(false);
            Cleanup();
            ReleaseEndingLock();
            votePanel?.Hide();
            fader?.Cancel();
            return false;
        }

        /// <summary>Host entry from schedule confirmation; campaign phase and schedule remain unchanged.</summary>
        public static bool StartPractice()
        {
            var r = instance;
            if (r == null || !r.Host || !r.CanStart || !r.FindRoom()
                || Resources.Load<GameObject>("Prefabs/Objects/ETCs/HumanCannonArtKit/Prepared/RehearsalCourseFromPerformance") == null) return false;
            r.prompt = r.pendingUnlockPrompt = false;
            r.promptBlock.SetOpen(false); r.votePanel.Hide();
            r.shared = new Shared { active = true, mastered = r.shared.mastered, round = r.shared.round + 1, version = r.shared.version + 1 };
            r.fallen.Clear(); r.fallenAt.Clear(); r.priorConditions.Clear(); r.recoveringPlacement.Clear();
            r.localRecoveryPending = r.placingAfterRecovery = false; r.lastSafePose = null; r.lastCarrier = CannonSeatState.Nobody;
            if (NetworkPlayer.Local is { IsSpawned: true } owner) r.CaptureReturnPose(owner);
            WaitingRoomStageDevices.ReserveCarrierForRehearsal();
            PropCarry.Instance?.SaveRehearsalSelection();
            foreach (var player in NetworkPlayer.All)
            {
                if (player == null || !player.IsSpawned) continue;
                if (player.State == PlayerState.Normal) r.priorConditions[player.OwnerClientId] = player.LostParts;
                ReleasePlayerDevices(player.OwnerClientId);
            }
            r.Publish();
            return true;
        }

        bool FindRoom()
        {
            if (room != null) return true;
            room = WaitingRoomScenes.FindPracticeRoom();
            if (room == null) return false;
            roomFloor = room.Find("LegacyRing/PracticeRing/DeckCollider")?.GetComponent<Collider>();
            var entry = room.Find("Corridor/FloorCollider")?.GetComponent<Collider>();
            if (roomFloor == null || entry == null) { room = null; return false; }
            origin = new Vector3(roomFloor.bounds.center.x, roomFloor.bounds.max.y, roomFloor.bounds.center.z);
            entrance = new Vector3(entry.bounds.center.x, entry.bounds.max.y, entry.bounds.center.z);
            return true;
        }

        static readonly string[] CannonPracticeHiddenPaths =
        {
            "LegacyRing/PracticeLanes", "LegacyRing/CollisionRepairs",
            "LegacyRing/DomeSuspension"
        };

        void HidePracticeProps()
        {
            foreach (var light in room.GetComponentsInChildren<WaitingRoomFlickerLight>())
            {
                if (!light.isActiveAndEnabled) continue;
                light.enabled = false;
                steadyLights.Add(light);
            }
            foreach (string path in CannonPracticeHiddenPaths)
            {
                var prop = room.Find(path);
                if (prop != null) HidePracticeProp(prop.gameObject);
            }
            var canopyRig = room.Find("LegacyRing/CanopyAndRig");
            if (canopyRig != null)
                foreach (Transform prop in canopyRig)
                    if (prop.GetComponentInChildren<Light>(true) == null) HidePracticeProp(prop.gameObject);
            foreach (var discovery in FindObjectsByType<PerformancePropDiscovery>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (discovery.TrickId == CampaignSnapshot.HumanCannonId)
                    foreach (var prop in discovery.PracticeProps)
                        HidePracticeProp(prop);
        }

        void HidePracticeProp(GameObject prop)
        {
            if (prop == null || !prop.activeSelf) return;
            prop.SetActive(false);
            hidden.Add(prop);
        }

        void EnsureCourse()
        {
            if (course != null && builtRound == shared.round) return;
            BedRestView.EndForRehearsal();
            Cleanup();
            HidePracticeProps();
            var prefab = Resources.Load<GameObject>("Prefabs/Objects/ETCs/HumanCannonArtKit/Prepared/RehearsalCourseFromPerformance");
            if (prefab == null) { Debug.LogError("[CannonRehearsal] Native source copy is missing."); return; }
            WaitingRoomStageDevices.ReleaseForRehearsal(); // 대기실 상시 대포 시스템·트램펄린을 먼저 비운다(씬당 1개, CC-252)
            var root = Instantiate(prefab); course = root.GetComponent<RehearsalCannonCourse>();
            root.SetActive(false); course.Prepare(origin);
            course.Cannon.SetPracticePlacementGate(id => shared.active && !shared.ending && shared.placed.Contains(id));
            course.Trampoline.PracticeGrabAllowed = () => !shared.restoringCarrier;
            root.SetActive(true); Physics.SyncTransforms();
            builtRound = shared.round; placedRound = -1; nextPlacement = 0;
            lastSafePose = null; placingAfterRecovery = false;
            quest.Show(T("first-title", "공연 준비"), T("first", "시작대의 대포에 F로 탑승하고, 준비가 끝나면 다시 F로 발사하세요. WASD로 중간 도착대에 착지하세요."));
            ShowGuide("INTRO", T("title", "인간대포 · 트램펄린 리허설"),
                GameSettings.WaitingRoom.Rehearsal.RecoverAfterDeath
                    ? T("intro", "대포와 트램펄린으로 최종 도착대에 가세요. 전원 도착 시 이수합니다. 사망하면 마지막으로 안전하게 서 있던 위치에서 다시 시작합니다.")
                    : T("intro-no-recovery", "대포와 트램펄린으로 최종 도착대에 가세요. 전원 도착 시 이수합니다. 공통 부상·사망 규칙이 적용되며, 사망한 사람도 넘기기·포기에 투표할 수 있습니다."), 10f);
        }

        void RecordAction(string state, ulong? actor = null, string reason = null, bool? vote = null, string guideId = null, bool host = true) =>
            CurtainCall.Telemetry.Pipeline.SpatialLogAdapter.RehearsalAction("CANNON", state, shared.round,
                actor.HasValue ? Checkpoint(actor.Value) : -1, shared.active, shared.mastered, true, actor,
                host, reason, vote, guideId);

        void ShowGuide(string id, string title, string body, float seconds)
        {
            if (RehearsalGuide.Show(title, body, seconds) && NetworkPlayer.Local != null)
                RecordAction("GUIDE_SHOWN", NetworkPlayer.Local.OwnerClientId, guideId: id, host: false);
        }

        void HostTick()
        {
            bool changed = false;
            var players = NetworkPlayer.All.Where(p => p != null && p.IsSpawned && p.State != PlayerState.Retired).ToArray();
            var ids = players.Select(p => p.OwnerClientId).ToArray();
            if (!shared.participants.OrderBy(x => x).SequenceEqual(ids.OrderBy(x => x))) { shared.participants = ids.ToList(); changed = true; }
            foreach (var list in new[] { shared.placed, shared.middle, shared.bounced, shared.arrived, shared.votes, shared.skipVotes })
                changed |= list.RemoveAll(id => !shared.participants.Contains(id)) > 0;
            fallen.RemoveWhere(id => !shared.participants.Contains(id));
            recoveringPlacement.RemoveWhere(id => !shared.participants.Contains(id));
            foreach (ulong id in priorConditions.Keys.ToArray())
                if (!shared.participants.Contains(id)) priorConditions.Remove(id);
            if (shared.restoringCarrier && !shared.participants.Contains(shared.previousCarrier))
            { shared.restoringCarrier = false; changed = true; }
            foreach (ulong id in fallenAt.Keys.ToArray())
                if (!shared.participants.Contains(id)) fallenAt.Remove(id);
            foreach (var p in players)
            {
                ulong id = p.OwnerClientId;
                if (p.State == PlayerState.Fallen)
                {
                    changed |= shared.arrived.Remove(id);
                    if (fallen.Add(id))
                    {
                        fallenAt[id] = Clock;
                        changed |= shared.placed.Remove(id);
                        changed |= shared.votes.Remove(id);
                        changed |= shared.skipVotes.Remove(id);
                        RecordAction("FALLEN", id);
                        if (id == lastCarrier || course.Trampoline.Carrier == id)
                        {
                            if (GameSettings.WaitingRoom.Rehearsal.RecoverAfterDeath && !shared.restoringCarrier)
                            { shared.previousCarrier = id; shared.restoringCarrier = true; }
                            course.Trampoline.ResetPracticePose(); course.Route.ResetPracticeTransport();
                        }
                    }
                    if (GameSettings.WaitingRoom.Rehearsal.RecoverAfterDeath
                        && fallenAt.TryGetValue(id, out double at) && Clock - at >= GameSettings.WaitingRoom.Rehearsal.RecoverSeconds)
                    {
                        recoveringPlacement.Add(id);
                        RecoverPlayer(p);
                        fallen.Remove(id); fallenAt.Remove(id);
                        changed = true;
                    }
                    continue;
                }
                fallen.Remove(id);
                fallenAt.Remove(id);
                if (!shared.placed.Contains(id) || p.State != PlayerState.Normal) continue;
                priorConditions[id] = p.LostParts;
                if (p.IsAirborne || course.Cannon.IsFlying(id)) continue;
                if (!shared.middle.Contains(id) && RehearsalCannonCourse.Contains(course.MiddleFloor, p.transform.position)) { shared.middle.Add(id); changed = true; RecordAction("CHECKPOINT", id, "MIDDLE_PLATFORM"); }
                // 완료 조건은 최종 도착대다. 운반자나 복제 순서로 누락된 반동 기록 때문에 전원을 막지 않는다.
                if (!shared.arrived.Contains(id) && RehearsalCannonCourse.Contains(course.FinishFloor, p.transform.position))
                {
                    if (!shared.middle.Contains(id)) shared.middle.Add(id);
                    shared.arrived.Add(id); changed = true; RecordAction("ARRIVED", id, "FINISH_PLATFORM");
                }
            }
            lastCarrier = course.Trampoline.Carrier;
            if (ids.Length > 0 && shared.arrived.Count == ids.Length) { BeginEnding(true); return; }
            if (ids.Length > 0 && shared.skipVotes.Count > ids.Length / 2) { BeginEnding(true, true); return; }
            if (ids.Length > 0 && shared.votes.Count > ids.Length / 2) { BeginEnding(false); return; }
            if (changed || Time.unscaledTime >= nextPublish) { Publish(); nextPublish = Time.unscaledTime + 1f; }
        }

        void RecoverEndingPlayers()
        {
            if (!Host || !shared.active || !GameSettings.WaitingRoom.Rehearsal.RecoverAfterDeath) return;
            foreach (var player in NetworkPlayer.All)
                if (player != null && player.IsSpawned && player.State == PlayerState.Fallen
                    && shared.participants.Contains(player.OwnerClientId)) RecoverPlayer(player);
        }

        void RecoverPlayer(NetworkPlayer player)
        {
            ReleasePlayerDevices(player.OwnerClientId);
            player.ServerRestoreCondition(priorConditions.GetValueOrDefault(player.OwnerClientId));
        }

        static void ReleasePlayerDevices(ulong id)
        {
            HumanCannonSystem.Current?.ServerReleaseForInspection(id);
            CurtainCall.Tightrope.TightropeHangSystem.Current?.ServerReleaseForInspection(id);
            CurtainCall.Tightrope.PiggybackSystem.Current?.ServerReleaseForRelocation(id);
        }

        void Bounced(ulong id)
        {
            if (!Host || !shared.active || shared.ending || !shared.placed.Contains(id) || shared.bounced.Contains(id)) return;
            shared.bounced.Add(id); RecordAction("BOUNCED", id); Publish();
        }

        void LocalTick()
        {
            if (ExpertService.IsLocalInTreatment) return;
            if (shared.ending || course == null || NetworkPlayer.Local == null || !NetworkPlayer.Local.IsSpawned) return;
            var p = NetworkPlayer.Local;
            if (p.State == PlayerState.Normal && (localRecoveryPending || local == p && previousState == PlayerState.Fallen))
                ResetLocalRecovery(p);
            if (local == p && previousState == PlayerState.Normal && p.State == PlayerState.Fallen)
                WaitingRoomGuidePanel.ShowAs(T("fall-title", "사망"), DeathGuide, 10f, RoomNotice.Warning);
            local = p; previousState = p.State;
            if (p.State != PlayerState.Normal) { quest.Show(T("fall-title", "사망"), DeathGuide); return; }
            if (Pursuit.PursuitDirector.IsCaptured(p)) return;
            if (!p.TryGetComponent(out PlayerMover mover) || p.Slot == NetworkPlayer.NoSlot
                || (placedRound != shared.round && HumanCannonCarry.Held(p.OwnerClientId) != null)) return;
            PlaceLocalAtCheckpoint(p, mover);
            if (shared.placed.Contains(p.OwnerClientId) && !mover.IsAirborne && mover.Simulated
                && course.IsRecoveryDeck(p.transform.position) && WalkableSupport.Contains(mover, p.transform.position))
                lastSafePose = new Pose(p.transform.position, p.transform.rotation);
            if (shared.placed.Contains(p.OwnerClientId)) placingAfterRecovery = false;
            if (!shared.placed.Contains(p.OwnerClientId) && Time.unscaledTime >= nextPlacement)
            { SendPlacement(p.transform.position); nextPlacement = Time.unscaledTime + .2f; }
            UpdateQuest();
            TickActionGuides();
        }

        void OnPlayerRevived(NetworkPlayer player)
        {
            if (shared.active && !shared.ending && player != null && player == NetworkPlayer.Local)
                localRecoveryPending = true;
        }

        void ResetLocalRecovery(NetworkPlayer player)
        {
            localRecoveryPending = false;
            placingAfterRecovery = true;
            placedRound = -1;
            nextPlacement = 0f;
            shared.placed.Remove(player.OwnerClientId);
            shared.arrived.Remove(player.OwnerClientId);
            shared.votes.Remove(player.OwnerClientId);
            shared.skipVotes.Remove(player.OwnerClientId);
            if (walls != null) walls.SetActive(false);
        }

        void PlaceLocalAtCheckpoint(NetworkPlayer p, PlayerMover mover)
        {
            if (ExpertService.IsTreating(p.OwnerClientId) || Pursuit.PursuitDirector.IsCaptured(p)) return;
            CaptureReturnPose(p);
            var checkpointFloor = Checkpoint(p.OwnerClientId) > 0 ? course.MiddleFloor : course.StartFloor;
            bool validPosition = placingAfterRecovery ? course.IsRecoveryDeck(p.transform.position)
                : RehearsalCannonCourse.Contains(checkpointFloor, p.transform.position);
            bool displacedBeforePlacement = !shared.placed.Contains(p.OwnerClientId)
                && !validPosition;
            if (placedRound != shared.round || displacedBeforePlacement)
            {
                var pose = placingAfterRecovery && lastSafePose is Pose safe && course.IsRecoveryDeck(safe.position)
                    ? safe : course.CheckpointPose(Checkpoint(p.OwnerClientId), Mathf.Max(0, p.Slot));
                if (p.TryGetComponent(out HumanoidPlayerPresentation presentation)) presentation.CancelItemAction();
                CurtainCall.Tightrope.PiggybackSystem.Current?.GetComponent<CurtainCall.Tightrope.PiggybackFollow>()?.ReleaseForDevice(p);
                if (p.TryGetComponent(out HumanCannonPilot pilot)) pilot.ClearMotionForInspection();
                mover.SetStartPose(pose.position, pose.rotation, true);
                if (p.TryGetComponent(out PlayerBalance balance)) balance.SetBalanceActive(false);
                CurtainCall.Cameras.CameraViewDirector.Current?.SetLookRotation(pose.rotation);
                placedRound = shared.round;
            }
        }

        void Cleanup()
        {
            if (course != null) { course.gameObject.SetActive(false); Destroy(course.gameObject); }
            course = null; builtRound = placedRound = -1; walls = null;
            foreach (var prop in hidden) if (prop != null) prop.SetActive(true);
            foreach (var light in steadyLights) if (light != null) light.enabled = true;
            steadyLights.Clear();
            hidden.Clear(); quest?.Hide();
            if (!shared.ending) ReleaseEndingLock();
        }
        static string T(string key, string fallback) => WaitingRoomText.Get("cannon-rehearsal." + key, fallback);
        static string DeathGuide => GameSettings.WaitingRoom.Rehearsal.RecoverAfterDeath
            ? T("fall-recover", "잠시 뒤 마지막으로 안전하게 서 있던 위치에서 다시 시작합니다.")
            : T("fall", "사망했습니다. 넘기기·포기에 투표할 수 있습니다.");
    }
}
