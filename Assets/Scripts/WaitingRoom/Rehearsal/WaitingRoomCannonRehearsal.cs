using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.Campaign;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.SpaceLink;
using CurtainCall.UI.SceneIntegration;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>Wiki 3.5: one native cannon/trampoline lesson, two recovery checkpoints, unanimous arrival.</summary>
    public sealed partial class WaitingRoomCannonRehearsal : MonoBehaviour
    {
        [Serializable] sealed class Shared
        {
            public int version, round;
            public bool active, mastered, ending, successful, skipped;
            public double endAt;
            public List<ulong> participants = new(), placed = new(), middle = new(), bounced = new(), arrived = new(), votes = new(), skipVotes = new();
        }
        static WaitingRoomCannonRehearsal instance;
        Shared shared = new();
        readonly Dictionary<ulong, double> fallenAt = new();
        readonly Dictionary<ulong, BodyPart> conditions = new();
        readonly List<GameObject> hidden = new();
        RehearsalCannonCourse course;
        Transform room;
        Vector3 origin, entrance;
        int builtRound = -1, placedRound = -1;
        ulong lastCarrier = CannonSeatState.Nobody;
        NetworkPlayer local;
        PlayerState previousState;
        float nextPlacement, nextPublish;

        public static bool IsActive => instance != null && instance.shared.active;
        public static bool HasMastered => instance != null && instance.shared.mastered;
        public static bool IsEnding => instance != null && instance.shared.ending && instance.shared.active;
        public static bool CanPractice => instance != null && instance.CanStart;
        public static int Checkpoint(ulong actor) => instance != null && instance.shared.middle.Contains(actor) ? 1 : 0;
        public static bool HasArrived(ulong actor) => instance != null && instance.shared.arrived.Contains(actor);
        public RehearsalCannonCourse Course => course;
        bool Host => CampaignRun.Instance != null && CampaignRun.Instance.HasAuthority;
        double Clock => NetworkSessionManager.Instance?.NetworkManager is { IsListening: true } net ? net.ServerTime.Time : Time.timeAsDouble;
        static bool PracticePhase => CampaignRun.Instance != null && CampaignRun.Instance.State.Phase is CampaignPhase.RestTime or CampaignPhase.Night;
        bool CanStart => !shared.active && !WaitingRoomRehearsal.IsActive && PracticePhase
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
        void Awake() { AwakeNetwork(); AwakeUi(); HumanCannonTrampoline.ServerBounced += Bounced; }
        void OnDestroy()
        {
            HumanCannonTrampoline.ServerBounced -= Bounced;
            DestroyNetwork(); Cleanup(); ReleaseEndingLock();
            if (instance == this) instance = null;
        }
        void Update()
        {
            TickNetwork();
            if (Host && shared.active && !PracticePhase) { shared.active = shared.ending = false; Publish(); }
            if (shared.active && !shared.ending)
            {
                if (FindRoom()) EnsureCourse();
                if (Host && course != null) HostTick();
                LocalTick();
            }
            else if (!shared.ending && course != null) Cleanup();
            TickUi();
            TickEnding();
        }

        /// <summary>Host entry from schedule confirmation; campaign phase and schedule remain unchanged.</summary>
        public static bool StartPractice()
        {
            var r = instance;
            if (r == null || !r.Host || !r.CanStart || !r.FindRoom()
                || Resources.Load<GameObject>("Prefabs/Objects/ETCs/HumanCannonArtKit/Prepared/RehearsalCourseFromPerformance") == null) return false;
            r.shared = new Shared { active = true, mastered = r.shared.mastered, round = r.shared.round + 1, version = r.shared.version + 1 };
            r.fallenAt.Clear(); r.conditions.Clear(); r.lastCarrier = CannonSeatState.Nobody;
            if (CampaignRun.Instance.State.Phase == CampaignPhase.RestTime) CampaignRun.Instance.SetRestTimerPaused(true);
            r.Publish();
            return true;
        }

        bool FindRoom()
        {
            if (room != null) return true;
            var found = GameObject.Find("S09_PracticeRoom");
            if (found == null) return false;
            room = found.transform;
            var deck = room.Find("LegacyRing/PracticeRing/DeckCollider")?.GetComponent<Collider>();
            var entry = room.Find("Corridor/FloorCollider")?.GetComponent<Collider>();
            if (deck == null || entry == null) { room = null; return false; }
            origin = new Vector3(deck.bounds.center.x, deck.bounds.max.y, deck.bounds.center.z);
            entrance = new Vector3(entry.bounds.center.x, entry.bounds.max.y, entry.bounds.center.z);
            return true;
        }

        void EnsureCourse()
        {
            if (course != null && builtRound == shared.round) return;
            Cleanup();
            foreach (string path in new[] { "LegacyRing/PracticeLanes", "LegacyRing/CollisionRepairs" })
            {
                var prop = room.Find(path);
                if (prop == null || !prop.gameObject.activeSelf) continue;
                prop.gameObject.SetActive(false); hidden.Add(prop.gameObject);
            }
            foreach (var discovery in FindObjectsByType<PerformancePropDiscovery>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (discovery.TrickId == CampaignSnapshot.HumanCannonId)
                    foreach (var prop in discovery.PracticeProps)
                        if (prop != null && prop.activeSelf) { prop.SetActive(false); hidden.Add(prop); }
            var prefab = Resources.Load<GameObject>("Prefabs/Objects/ETCs/HumanCannonArtKit/Prepared/RehearsalCourseFromPerformance");
            if (prefab == null) { Debug.LogError("[CannonRehearsal] Native source copy is missing."); return; }
            var root = Instantiate(prefab); course = root.GetComponent<RehearsalCannonCourse>();
            root.SetActive(false); course.Prepare(origin); root.SetActive(true); Physics.SyncTransforms();
            builtRound = shared.round; placedRound = -1; nextPlacement = 0;
            quest.Show(T("title", "인간대포 · 트램펄린 리허설"), T("first", "시작대의 대포에 F로 탑승하고, 준비가 끝나면 다시 F로 발사하세요. WASD로 중간 도착대에 착지하세요."));
            ShowGuide("INTRO", T("title", "인간대포 · 트램펄린 리허설"), T("intro", "대포로 중간 도착대에 가세요. 한 명은 트램펄린을 운반하고, 나머지는 재발사 후 반동으로 최종 도착대에 갑니다. 전원 도착 시 이수합니다. 추락 부상·비용은 없습니다."), 14f);
            if (CampaignRun.Instance.State.Phase == CampaignPhase.Night) CampaignRun.Instance.SetReady(false);
        }

        void RecordAction(string state, ulong? actor = null, string reason = null, bool? vote = null, string guideId = null, bool host = true) =>
            CurtainCall.Telemetry.Pipeline.SpatialLogAdapter.RehearsalAction("CANNON", state, shared.round,
                actor.HasValue ? Checkpoint(actor.Value) : -1, shared.active, shared.mastered, true, actor,
                host, reason, vote, guideId);

        void ShowGuide(string id, string title, string body, float seconds)
        {
            if (WaitingRoomTutorialPanel.Show(title, body, seconds) && NetworkPlayer.Local != null)
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
            foreach (var p in players)
            {
                ulong id = p.OwnerClientId;
                if (!conditions.ContainsKey(id)) conditions[id] = p.LostParts;
                if (p.State == PlayerState.Fallen)
                {
                    changed |= shared.arrived.Remove(id);
                    if (!fallenAt.TryGetValue(id, out double at))
                    {
                        fallenAt[id] = Clock;
                        RecordAction("FALLEN", id);
                        if (id == lastCarrier) { course.Trampoline.ResetPracticePose(); course.Route.ResetPracticeTransport(); }
                    }
                    else if (Clock - at >= 1.5d) { fallenAt.Remove(id); p.ServerRestoreCondition(conditions[id]); }
                    continue;
                }
                fallenAt.Remove(id);
                if (!shared.placed.Contains(id) || p.State != PlayerState.Normal || p.IsAirborne || course.Cannon.IsFlying(id)) continue;
                if (!shared.middle.Contains(id) && RehearsalCannonCourse.Contains(course.MiddleFloor, p.transform.position)) { shared.middle.Add(id); changed = true; RecordAction("CHECKPOINT", id, "MIDDLE_PLATFORM"); }
                if (shared.middle.Contains(id) && shared.bounced.Contains(id) && !shared.arrived.Contains(id)
                    && RehearsalCannonCourse.Contains(course.FinishFloor, p.transform.position)) { shared.arrived.Add(id); changed = true; RecordAction("ARRIVED", id, "FINISH_PLATFORM"); }
            }
            lastCarrier = course.Trampoline.Carrier;
            if (ids.Length > 0 && shared.arrived.Count == ids.Length) { BeginEnding(true); return; }
            if (ids.Length > 0 && shared.skipVotes.Count > ids.Length / 2) { BeginEnding(true, true); return; }
            if (ids.Length > 0 && shared.votes.Count > ids.Length / 2) { BeginEnding(false); return; }
            if (changed || Time.unscaledTime >= nextPublish) { Publish(); nextPublish = Time.unscaledTime + 1f; }
        }

        void Bounced(ulong id)
        {
            if (!Host || !shared.active || shared.ending || !shared.middle.Contains(id) || shared.bounced.Contains(id)) return;
            shared.bounced.Add(id); RecordAction("BOUNCED", id); Publish();
        }

        void LocalTick()
        {
            if (shared.ending || course == null || NetworkPlayer.Local == null || !NetworkPlayer.Local.IsSpawned) return;
            var p = NetworkPlayer.Local;
            bool recovery = local == p && previousState == PlayerState.Fallen && p.State == PlayerState.Normal;
            local = p; previousState = p.State;
            if (p.State != PlayerState.Normal || !p.TryGetComponent(out PlayerMover mover)) return;
            if (placedRound != shared.round || recovery)
            {
                var pose = course.CheckpointPose(Checkpoint(p.OwnerClientId), Mathf.Max(0, p.Slot));
                mover.SetStartPose(pose.position, pose.rotation, true);
                if (p.TryGetComponent(out PlayerBalance balance)) balance.SetBalanceActive(false);
                CurtainCall.Cameras.CameraViewDirector.Current?.SetLookRotation(pose.rotation);
                placedRound = shared.round;
                if (recovery)
                {
                    RecordAction("RECOVERED", p.OwnerClientId, Checkpoint(p.OwnerClientId) > 0 ? "MIDDLE_PLATFORM" : "START_PLATFORM", host: false);
                    ShowGuide("FALL_RECOVERY", T("fall-title", "추락"), T("fall", "부상과 비용 없이 마지막 구간의 출발대에서 다시 시작합니다."), 4f);
                }
            }
            if (!shared.placed.Contains(p.OwnerClientId) && Time.unscaledTime >= nextPlacement)
            { SendPlacement(p.transform.position); nextPlacement = Time.unscaledTime + .2f; }
            UpdateQuest();
        }

        void Cleanup()
        {
            if (course != null) { course.gameObject.SetActive(false); Destroy(course.gameObject); }
            course = null; builtRound = placedRound = -1; walls = null;
            foreach (var prop in hidden) if (prop != null) prop.SetActive(true);
            hidden.Clear(); quest?.Hide();
            if (!shared.ending) ReleaseEndingLock();
        }
        static string T(string key, string fallback) => WaitingRoomText.Get("cannon-rehearsal." + key, fallback);
    }
}
