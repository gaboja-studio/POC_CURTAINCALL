using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 첫 공연 전 대기실 동선 제한(Task-097, 사용자 순서 2026-11-02).
    /// 연습실에서 시작 → 리허설 동안 연습실 입구 철창 닫힘 → 리허설 성공 시 연습실·무대 통로 철창 열림 →
    /// 모두 연습실을 나오면 연습실 다시 잠김 → 무대 입구 통로·로비·공연 스테이지 입구 외에는 모두 철창, 상호작용(상점·소품실·숙소 등)도 잠김 →
    /// 무대 입구 통로에 가까이 가면 로비·통로 위 모니터에 단장 얼굴 → 외줄 공연 뒤 대기실 튜토리얼 때(리허설 단계가 끝나면) 모두 해제.
    /// </summary>
    public sealed partial class WaitingRoomRehearsal
    {
        /// <summary>무대 입구 통로 셔터(<see cref="NightDoorLock"/>) 이름.</summary>
        const string StageShutterName = "NightSchedule";
        const string LayoutRootName = "WaitingRoom_LayoutA";
        const string PracticeAreaName = "Area_S09_PracticeRoom";
        /// <summary>로비 → 무대 입구 통로 입구. 이 거리(m) 안에 누가 들어오면 모니터가 켜진다.</summary>
        static readonly Vector3 CorridorMouth = new(-6f, 0f, 63.5f);
        const float MonitorTriggerRadius = 9f;

        /// <summary>
        /// 로비에서 철창 없이 열려 있던 입구(구조 A 실측, 2026-11-02): 북쪽 소품실·작업실 갈림길, 북동쪽 상점·승강기·지하 홀, 동쪽 숙소.
        /// 작업실·물류·지하·연습실 입구와 무대 통로는 기존 <see cref="NightDoorLock"/>이 막는다.
        /// </summary>
        static readonly (string name, Vector3 from, Vector3 to)[] LockdownOpenings =
        {
            ("LockdownGate_PropRoom", new(-10.4f, 0f, 91.0f), new(-5.7f, 0f, 91.7f)),
            ("LockdownGate_ShopHall", new(-3.8f, 0f, 91.5f), new(6.7f, 0f, 84.1f)),
            ("LockdownGate_Dormitory", new(8.3f, 0f, 75.2f), new(8.3f, 0f, 70.4f)),
        };

        // 공유: 리허설을 마친 뒤 모두 연습실을 나와 연습실을 다시 잠갔다.
        bool practiceSealed;
        readonly List<RehearsalLockdownGate> lockdownGates = new();
        readonly List<RingmasterMonitor> monitors = new();
        Transform lockdownRoot;
        Bounds practiceArea;
        bool practiceAreaFound;

        /// <summary>리허설 단계에서 기존 철창이 닫혀야 하는가(<see cref="NightDoorLock"/>가 부른다).</summary>
        public static bool IsGateLocked(string gate)
        {
            var r = instance;
            bool completed = r != null && r.active && r.completed;
            bool abandoned = r != null && r.active && r.abandoned;
            // 연습실: 리허설 중 닫힘. 포기하면 다시 들어올 수 있게 열고, 성공하면 열었다가 모두 나오면 다시 잠근다.
            if (gate == NightDoorLock.RehearsalPassageName) return !(abandoned || (completed && !r.practiceSealed));
            // 무대 입구 통로: 리허설을 마치면 연다.
            if (gate == StageShutterName) return !completed;
            return true;
        }

        /// <summary>매 프레임: 첫 공연 전이면 동선·상호작용 제한(대기실 튜토리얼 때 해제).</summary>
        void TickRestriction()
        {
            bool restricted = run != null && run.State.Phase == CampaignPhase.Rehearsal;
            RehearsalLockdownGate.Restricted = restricted;
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
                lockdownGates.Add(RehearsalLockdownGate.Create(lockdownRoot, name, from, to, iron));
            // 로비 동쪽 벽(가운데를 바라봄)과 무대 입구 통로 위(로비를 바라봄).
            monitors.Add(RingmasterMonitor.Create(lockdownRoot, "RingmasterMonitor_Lobby", new Vector3(7.9f, 4.2f, 77.3f), Vector3.left, new Vector2(3.2f, 1.8f), bezel, false));
            monitors.Add(RingmasterMonitor.Create(lockdownRoot, "RingmasterMonitor_StageCorridor", new Vector3(-6f, 4.4f, 63.2f), Vector3.forward, new Vector2(2.6f, 1.46f), bezel, true));
            var area = GameObject.Find(PracticeAreaName);
            var collider = area != null ? area.GetComponent<Collider>() : null;
            practiceAreaFound = collider != null;
            if (practiceAreaFound) practiceArea = collider.bounds;
        }

        bool InPracticeRoom(Vector3 position) => practiceAreaFound
            ? practiceArea.Contains(position)
            : new Vector2(position.x - ringCenter.x, position.z - ringCenter.z).sqrMagnitude < 30f * 30f;

        /// <summary>호스트: 리허설을 마친 뒤 살아 있는 모두가 연습실 밖으로 나오면 연습실을 다시 잠근다.</summary>
        void HostTickPracticeSeal()
        {
            if (!completed || practiceSealed) return;
            int alive = 0;
            foreach (var player in NetworkPlayer.All)
            {
                if (player == null || !player.IsSpawned || player.State == PlayerState.Retired) continue;
                alive++;
                if (InPracticeRoom(player.transform.position)) return;
            }
            if (alive == 0) return;
            practiceSealed = true; version++;
            Debug.Log("[Rehearsal] 모두 연습실을 나옴 → 연습실 철창 다시 잠금");
            Publish();
        }

        /// <summary>리허설을 마친 뒤 누구든 무대 입구 통로에 다가가면 모니터를 켠다(모든 화면이 같은 위치를 본다).</summary>
        void TickMonitors()
        {
            if (!completed || monitors.Count == 0 || monitors[0] == null || monitors[0].IsOn) return;
            foreach (var player in NetworkPlayer.All)
            {
                if (player == null || !player.IsSpawned) continue;
                var p = player.transform.position;
                if (new Vector2(p.x - CorridorMouth.x, p.z - CorridorMouth.z).sqrMagnitude > MonitorTriggerRadius * MonitorTriggerRadius) continue;
                foreach (var monitor in monitors) if (monitor != null) monitor.TurnOn();
                return;
            }
        }
    }
}
