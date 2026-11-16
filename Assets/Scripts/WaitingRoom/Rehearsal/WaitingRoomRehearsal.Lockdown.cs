using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.UI.SceneIntegration;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 첫 공연 전 대기실 동선 제한(Task-097, 사용자 순서 2026-11-02).
    /// 연습실 입구는 공통 튜토리얼 설정에 따라 실내 사람이 있으면 열리고, 모두 나가면 닫힌다. 복귀자가 들어오면 다시 열린다.
    /// 리허설 성공 시 무대 통로 철창 열림 → 무대 입구 통로·로비·공연 스테이지 입구 외에는 모두 철창, 상호작용(상점·소품실·숙소 등)도 잠김 →
    /// 무대 입구 통로에 가까이 가면 로비·통로 위 모니터에 단장 얼굴 → 외줄 공연 뒤 대기실 튜토리얼 때(리허설 단계가 끝나면) 모두 해제.
    /// </summary>
    public sealed partial class WaitingRoomRehearsal
    {
        /// <summary>무대 입구 통로 셔터(<see cref="NightDoorLock"/>) 이름.</summary>
        const string StageShutterName = "NightSchedule";
        const string LayoutRootName = "WaitingRoom_LayoutA";
        const string PracticeAreaName = "Area_S09_PracticeRoom";
        double monitorStarted = -1d;
        BoxCollider corridorApproach, corridorPassage;

        /// <summary>최종본 배치(2026-11-12): 무대 입구 통로가 로비 북쪽, 작업실·소품실이 남쪽인 씬. 편집 도구가 남긴 표식으로 구분한다.</summary>
        static bool FinalLayout { get { var layout = GameObject.Find(LayoutRootName); return layout != null && layout.transform.Find("FinalLayout_Swap") != null; } }
        static (string name, Vector3 from, Vector3 to)[] LockdownOpenings => FinalLayout ? FinalLockdownOpenings : StructureALockdownOpenings;
        /// <summary>최종본: 남쪽 작업실·소품실 전실 입구, 북동쪽 홀, 동쪽 숙소.</summary>
        static readonly (string name, Vector3 from, Vector3 to)[] FinalLockdownOpenings =
        {
            ("LockdownGate_PropRoom", new(-5.2f, 0f, 63.3f), new(-2.1f, 0f, 63.3f)),
            ("LockdownGate_ShopHall", new(-3.8f, 0f, 91.5f), new(6.7f, 0f, 84.1f)),
            ("LockdownGate_Dormitory", new(8.3f, 0f, 75.2f), new(8.3f, 0f, 70.4f)),
        };
        /// <summary>구조 A(2026-11-02 실측): 북쪽 소품실·작업실 갈림길, 북동쪽 상점·승강기·지하 홀, 동쪽 숙소. 나머지 입구는 기존 <see cref="NightDoorLock"/>이 막는다.</summary>
        static readonly (string name, Vector3 from, Vector3 to)[] StructureALockdownOpenings =
        {
            ("LockdownGate_PropRoom", new(-10.4f, 0f, 91.0f), new(-5.7f, 0f, 91.7f)),
            ("LockdownGate_ShopHall", new(-3.8f, 0f, 91.5f), new(6.7f, 0f, 84.1f)),
            ("LockdownGate_Dormitory", new(8.3f, 0f, 75.2f), new(8.3f, 0f, 70.4f)),
        };

        // 호스트가 공유하는 연습실 봉쇄 상태. 공통 점유 정책이 켜져 있으면 다시 열릴 수 있다.
        bool practiceSealed;
        readonly List<RehearsalLockdownGate> lockdownGates = new();
        readonly List<RingmasterMonitor> monitors = new();
        Transform lockdownRoot;
        Bounds practiceArea;
        bool practiceAreaFound;
        Transform practiceExit;
        BoxCollider practicePassage;
        Collider practicePassageRoof;

        /// <summary>첫 공연을 마치고 대기실 튜토리얼에 들어가기 전까지 유지하는 출입·기능 제한.</summary>
        public static bool IsPreTutorialRestricted
        {
            get
            {
                var run = CampaignRun.Instance;
                if (run == null) return false;
                var state = run.State;
                return IsEducationGeneration(state) && (state.Phase == CampaignPhase.Rehearsal
                    || (state.Phase == CampaignPhase.Performance && state.Day == 1 && state.PerformanceIndex == 0));
            }
        }

        /// <summary>최초 리허설을 거친 세대 번호. 개발용 시작 지점은 세대 번호가 2부터라, 1세대만 보면 제한이 통째로 꺼진다(2026-11-15 사용자 제보).</summary>
        static int educationGeneration;
        /// <summary>첫 교육 제한을 쓰는 세대인가: 1세대, 또는 이 세대에서 최초 리허설 시간대를 거쳤다.</summary>
        static bool IsEducationGeneration(CampaignSnapshot state)
        {
            if (state.Phase == CampaignPhase.Rehearsal) educationGeneration = state.Generation;
            return state.Generation == 1 || state.Generation == educationGeneration;
        }

        /// <summary>
        /// 첫 외줄 공연을 마치고 대기실로 돌아와 대기실 튜토리얼을 모두 마치기 전(2026-11-15 사용자).
        /// <see cref="IsPreTutorialRestricted"/>에 더해, 돌아온 뒤 전원이 안내를 확인할 때까지(가이드 쉬는 시간의 대기 상태)를 포함한다.
        /// 이 동안 숙소·작업실/소품실·연습실은 철창으로 막고 그 안의 연출은 나오지 않는다. 모두에게 같은 런 상태로만 정한다.
        /// </summary>
        public static bool IsFirstTutorialPending
        {
            get
            {
                if (IsPreTutorialRestricted) return true;
                var run = CampaignRun.Instance;
                if (run == null) return false;
                var state = run.State;
                return IsEducationGeneration(state) && state.Day == 1 && state.Phase == CampaignPhase.RestTime
                    && state.Rest == RestKind.Guide && state.Timer == RestTimerState.WaitingForExit;
            }
        }

        /// <summary>대기실 튜토리얼이 끝날 때까지 닫아 두는 기존 철창(<see cref="NightDoorLock"/>가 부른다): 연습실 통로, 작업실 갈림길.</summary>
        public static bool IsHeldThroughTutorial(string gate) => gate == NightDoorLock.RehearsalPassageName || gate == WorkshopGateName;
        const string WorkshopGateName = "WorkshopGate";
        /// <summary>대기실 튜토리얼이 끝날 때까지 닫아 두는 입구(숙소, 작업실·소품실). 북동쪽 홀은 예전처럼 첫 공연까지만 닫는다.</summary>
        static bool HeldThroughTutorial(string opening) => opening != "LockdownGate_ShopHall";

        /// <summary>리허설 단계에서 기존 철창이 닫혀야 하는가(<see cref="NightDoorLock"/>가 부른다).</summary>
        public static bool IsGateLocked(string gate)
        {
            var r = instance;
            bool completed = r != null && r.completed;
            if (gate == NightDoorLock.RehearsalPassageName)
            {
                if (UsesInitialTutorialRules && GameSettings.WaitingRoom.Tutorial.OpenPracticeDoorWhileOccupied)
                    return r.practiceSealed;
                return !(completed && !r.practiceSealed);
            }
            // 무대 입구 통로: 리허설을 마치면 연다.
            if (gate == StageShutterName) return !completed;
            return true;
        }

        /// <summary>매 프레임: 첫 공연 전이면 동선·상호작용 제한(대기실 튜토리얼 때 해제).</summary>
        void TickRestriction()
        {
            bool restricted = IsPreTutorialRestricted;
            RehearsalLockdownGate.Restricted = restricted;
            RehearsalLockdownGate.TutorialPending = IsFirstTutorialPending;
            ShopReach.Locked = restricted;
        }

        void EnsureLockdownProps()
        {
            if (lockdownRoot != null) return;
            var layout = GameObject.Find(LayoutRootName);
            if (layout == null) return;
            lockdownRoot = new GameObject("RehearsalLockdown (runtime)").transform;
            lockdownRoot.SetParent(layout.transform, true);
            lockdownGates.Clear(); monitors.Clear();
            Material iron = null, bezel = null;
            foreach (var gate in FindObjectsByType<NightDoorLock>(FindObjectsSortMode.None))
            {
                var renderer = gate.GetComponentInChildren<Renderer>();
                if (renderer != null && iron == null) iron = renderer.sharedMaterial;
            }
            var screenBezel = GameObject.Find("MonitorBezel");
            if (screenBezel != null && screenBezel.TryGetComponent(out Renderer bezelRenderer)) bezel = bezelRenderer.sharedMaterial;
            foreach (var (name, from, to) in LockdownOpenings)
                lockdownGates.Add(RehearsalLockdownGate.Create(lockdownRoot, name, from, to, iron, HeldThroughTutorial(name), LobbyCenter));
            FindSealedAreas(layout.transform);
            // 실제 벽·천장에 붙인다(허공에 띄우지 않는다). 붙일 면을 못 찾으면 만들지 않는다.
            // 모니터 연출을 맡는 물체가 씬에 있으면(최종본) 그쪽이 모니터를 만들고 켠다. 리허설 단계가 끝난 뒤에도 얼굴이 나와야 하기 때문이다(2026-11-14 사용자).
            if (LobbyMonitorDirector.Instance == null)
            {
                var lobby = PlaceLobbyMonitor(bezel);
                if (lobby != null) monitors.Add(lobby);
                var corridor = PlaceCorridorMonitor(bezel);
                if (corridor != null) monitors.Add(corridor);
            }
            var area = GameObject.Find(PracticeAreaName);
            var collider = area != null ? area.GetComponent<Collider>() : null;
            practiceAreaFound = collider != null;
            if (practiceAreaFound) practiceArea = collider.bounds;
            practiceExit = layout.transform.Find("NightMaintenance/PracticeGate");
            var passage = GameObject.Find("Passage_F8_Practice");
            practicePassage = passage != null && passage.scene == layout.scene
                ? passage.transform.Find("FloorCollider")?.GetComponent<BoxCollider>() : null;
            practicePassageRoof = practicePassage != null
                ? passage.transform.Find("PassageRoof")?.GetComponent<Collider>() : null;
        }

        // ── 닫힌 구역에 남은 사람 내보내기(2026-11-15 사용자) ──
        static readonly string[] DormZones = { "Area_S02_Dormitory", "F8_Dorm", "Area_Dormitory" },
            PropZones = { "F8_PropRoom", "WorkshopBranch", "Area_S04_PropRoom", "Area_S10_Workshop" },
            PracticeZones = { "F8_Practice", PracticeAreaName },
            ShopHallZones = { "ServiceGallery", "Area_S03_Shop", "Area_S08_ElevatorHall" };
        /// <summary>철창 하나가 막는 구역: 구역 상자들 + 철창 자리(그 너머만 구역 안으로 본다. 통로 상자가 로비와 겹치기 때문이다).</summary>
        readonly List<(bool practice, bool held, Vector3 gate, Vector3 outward, List<BoxCollider> zones)> sealedAreas = new();

        void FindSealedAreas(Transform layout)
        {
            sealedAreas.Clear();
            var timer = FindFirstObjectByType<ExplorationTimerZones>();
            if (timer == null) return;
            var all = timer.Zones;
            BoxCollider lobby = System.Array.Find(all, z => z.name == "Lobby");
            if (lobby == null) return;
            Vector3 lobbyCenter = lobby.bounds.center;
            void Add(bool practice, bool held, Vector3 gate, Vector3 normal, string[] names)
            {
                var zones = new List<BoxCollider>();
                foreach (var zone in all) if (System.Array.IndexOf(names, zone.name) >= 0) zones.Add(zone);
                if (zones.Count == 0) return;
                normal.y = 0f; normal.Normalize();
                // 로비 반대쪽이 구역 안이다.
                if (Vector3.Dot(lobbyCenter - gate, normal) > 0f) normal = -normal;
                sealedAreas.Add((practice, held, gate, normal, zones));
            }
            foreach (var (name, from, to) in LockdownOpenings)
            {
                // 북동쪽 홀(서비스 통로·상점·승강기)은 첫 공연까지만 닫히지만, 목마로 넘어가 갇히는 일이 있어 닫힌 동안은 같이 내보낸다(2026-11-15 사용자).
                Add(false, HeldThroughTutorial(name), (from + to) * .5f, Vector3.Cross(to - from, Vector3.up),
                    name == "LockdownGate_Dormitory" ? DormZones : name == "LockdownGate_ShopHall" ? ShopHallZones : PropZones);
            }
            var practiceGate = layout.Find("NightMaintenance/" + NightDoorLock.RehearsalPassageName);
            if (practiceGate != null) Add(true, true, practiceGate.position, practiceGate.forward, PracticeZones);
        }

        /// <summary>
        /// 철창이 닫혀 있는 동안 그 안(숙소, 작업실·소품실, 연습실)에 남아 있거나 다시 들어간 내 캐릭터를 로비 출발 자리로 옮긴다.
        /// 연습실은 리허설 시간대에는 건드리지 않는다(리허설 진행과 실내 점유 규칙이 맡는다). 이동은 소유자 화면에서 하고 위치 동기화가 알린다.
        /// </summary>
        void TickSealedAreas()
        {
            if (lockdownRoot == null || sealedAreas.Count == 0 || SceneCurtainTransition.IsActive) return;
            var player = NetworkPlayer.Local;
            if (player == null || !player.IsSpawned || player.State != PlayerState.Normal || player.Slot == NetworkPlayer.NoSlot
                || !player.TryGetComponent(out PlayerMover mover)) return;
            bool rehearsal = run != null && run.State.Phase == CampaignPhase.Rehearsal;
            Vector3 feet = player.transform.position, point = feet + Vector3.up * .5f;
            bool restricted = IsPreTutorialRestricted;
            foreach (var (practice, held, gate, outward, zones) in sealedAreas)
            {
                if (practice && rehearsal || !held && !restricted) continue;
                if (Vector3.Dot(feet - gate, outward) < .4f) continue;
                foreach (var zone in zones)
                {
                    if (zone == null || !zone.enabled || !zone.gameObject.activeInHierarchy) continue;
                    var local = zone.transform.InverseTransformPoint(point) - zone.center; var half = zone.size * .5f;
                    if (Mathf.Abs(local.x) > half.x || Mathf.Abs(local.y) > half.y || Mathf.Abs(local.z) > half.z) continue;
                    PlayerSpawnPoints.GetPose(player.Slot, mover.LaneSpacing, out Vector3 position, out Quaternion rotation);
                    mover.SetStartPose(position, rotation, true);
                    Debug.Log("[Rehearsal] 닫힌 구역(" + zone.name + ")에 있던 내 캐릭터를 로비로 옮김");
                    return;
                }
            }
        }

        static readonly Vector2 LobbyMonitorSize = new(3.2f, 1.8f), CorridorMonitorSize = new(2.6f, 1.46f);
        /// <summary>로비 둥근 방 가운데(구조 A 실측)와 동쪽 벽걸이 높이.</summary>
        static readonly Vector3 LobbyCenter = new(-5.6f, 4.2f, 77.3f);
        /// <summary>무대 입구 통로 안, 로비 쪽 입구에서 조금 들어간 자리(구조 A 실측: 통로 입구 약 z 63, 셔터 z 51).</summary>
        static Vector3 CorridorHangPoint => FinalLayout ? new(-9.7f, 1.5f, 95.8f) : new(-6f, 1.5f, 59f);
        /// <summary>매단 모니터 아래로 사람이 지나가도 닿지 않는 높이(m).</summary>
        const float HeadClearance = 2.6f;

        /// <summary>로비 가운데에서 동쪽 벽을 재서, 모니터 뒷면이 벽면에 닿게 건다.</summary>
        RingmasterMonitor PlaceLobbyMonitor(Material bezel)
        {
            if (!Physics.Raycast(LobbyCenter, Vector3.right, out var hit, 30f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            { Debug.LogWarning("[Rehearsal] 로비 동쪽 벽을 찾지 못해 로비 모니터를 걸지 않습니다."); return null; }
            Vector3 normal = Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized;
            return RingmasterMonitor.Create(lockdownRoot, "RingmasterMonitor_Lobby", hit.point + normal * RingmasterMonitor.BackDepth,
                normal, LobbyMonitorSize, bezel, 0f);
        }

        /// <summary>무대 입구 통로 안 천장 높이를 재서, 막대가 천장까지 닿게 매단다. 로비 쪽을 바라본다.</summary>
        RingmasterMonitor PlaceCorridorMonitor(Material bezel)
        {
            if (!Physics.Raycast(CorridorHangPoint, Vector3.up, out var ceiling, 20f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            { Debug.LogWarning("[Rehearsal] 무대 입구 통로 천장을 찾지 못해 통로 모니터를 달지 않습니다."); return null; }
            float half = CorridorMonitorSize.y * .5f + .09f;
            float center = Mathf.Max(CorridorHangPoint.y - 1.5f + HeadClearance + half, ceiling.point.y - .6f - half);
            float hang = ceiling.point.y - (center + half);
            if (hang < .05f) { Debug.LogWarning("[Rehearsal] 무대 입구 통로 천장이 낮아 통로 모니터를 달지 않습니다."); return null; }
            return RingmasterMonitor.Create(lockdownRoot, "RingmasterMonitor_StageCorridor",
                new Vector3(CorridorHangPoint.x, center, CorridorHangPoint.z), FinalLayout ? Vector3.back : Vector3.forward, CorridorMonitorSize, bezel, hang);
        }

        bool StillInsidePracticeExit(NetworkPlayer player)
        {
            var position = player.transform.position;
            float clearance = player.TryGetComponent(out PlayerMover mover) && mover.Body != null ? mover.BodyRadius : .4f;
            if (practiceExit != null)
            {
                var inward = practiceExit.forward;
                if (Vector3.Dot(ringCenter - practiceExit.position, inward) < 0f) inward = -inward;
                // Count the full body until it clears the shutter, including the connecting passage.
                if (Vector3.Dot(position - practiceExit.position, inward) < -clearance) return false;
                if (practicePassage != null)
                {
                    var point = practicePassage.transform.InverseTransformPoint(position) - practicePassage.center;
                    var half = practicePassage.size * .5f;
                    var scale = practicePassage.transform.lossyScale;
                    if (Mathf.Abs(point.x) <= half.x + clearance / Mathf.Abs(scale.x)
                        && Mathf.Abs(point.z) <= half.z + clearance / Mathf.Abs(scale.z))
                    {
                        // The room's coarse bounds also overlap the roof. Use the passage's actual vertical space here.
                        return position.y >= practicePassage.bounds.min.y
                            && (practicePassageRoof == null || position.y < practicePassageRoof.bounds.min.y);
                    }
                }
            }
            return InPracticeRoom(position);
        }

        bool InPracticeRoom(Vector3 position) => practiceAreaFound ? practiceArea.Contains(position)
            : new Vector2(position.x - ringCenter.x, position.z - ringCenter.z).sqrMagnitude < 30f * 30f;

        /// <summary>호스트: 공통 튜토리얼 정책은 실내 점유를 계속 반영한다. 정책이 꺼지면 기존 완료 후 봉쇄를 유지한다.</summary>
        void HostTickPracticeSeal()
        {
            if (UsesInitialTutorialRules && GameSettings.WaitingRoom.Tutorial.OpenPracticeDoorWhileOccupied)
            {
                bool occupied = false, hasPlayers = false;
                foreach (var player in NetworkPlayer.All)
                {
                    if (player == null || !player.IsSpawned || player.State == PlayerState.Retired) continue;
                    hasPlayers = true;
                    if (StillInsidePracticeExit(player)) { occupied = true; break; }
                }
                if (!hasPlayers) return;
                bool shouldSeal = !occupied;
                if (practiceSealed == shouldSeal) return;
                practiceSealed = shouldSeal; version++;
                Debug.Log(occupied ? "[Rehearsal] 연습실 안에 단원 있음 → 연습실 철창 열림" : "[Rehearsal] 연습실 안에 단원 없음 → 연습실 철창 닫힘");
                Publish();
                return;
            }
            if (!completed || practiceSealed) return;
            int alive = 0;
            foreach (var player in NetworkPlayer.All)
            {
                if (player == null || !player.IsSpawned || player.State == PlayerState.Retired) continue;
                alive++;
                if (StillInsidePracticeExit(player)) return;
            }
            if (alive == 0) return;
            practiceSealed = true; version++;
            Debug.Log("[Rehearsal] 모두 연습실을 나옴 → 연습실 철창 다시 잠금");
            Publish();
        }

        /// <summary>리허설을 마친 뒤 누구든 무대 입구 통로에 다가가면 모니터를 켠다(모든 화면이 같은 위치를 본다).</summary>
        void TickMonitors()
        {
            if (!completed || monitors.Count == 0) return;
            if (HasAuthority && monitorStarted < 0d)
            {
                if (corridorApproach == null) corridorApproach = GameObject.Find("Zone_StageApproach")?.GetComponent<BoxCollider>();
                if (corridorPassage == null) corridorPassage = GameObject.Find("Zone_StagePassage")?.GetComponent<BoxCollider>();
                foreach (var player in NetworkPlayer.All)
                {
                    if (player == null || !player.IsSpawned || player.State != PlayerState.Normal) continue;
                    bool Inside(BoxCollider box) => box != null && box.enabled && box.gameObject.activeInHierarchy
                        && player.TryGetComponent<PlayerMover>(out var mover)
                        && new Bounds(box.center, box.size).Contains(box.transform.InverseTransformPoint(player.transform.position + Vector3.up * mover.BodyRadius));
                    if (!Inside(corridorApproach) && !Inside(corridorPassage)) continue;
                    monitorStarted = Clock; version++; Publish(); break;
                }
            }
            if (monitorStarted < 0d) return;
            foreach (var monitor in monitors)
                if (monitor != null) monitor.PlayAt((float)(Clock - monitorStarted));
        }
    }
}
