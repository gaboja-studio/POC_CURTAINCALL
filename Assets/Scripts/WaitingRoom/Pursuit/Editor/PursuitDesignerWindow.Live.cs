using System;
using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public sealed partial class PursuitDesignerWindow
    {
        [SerializeField] bool liveFollowPlayer = true;
        [SerializeField] bool liveActorLabels;
        int followedPlayerId;
        double nextLiveDiscovery;
        readonly Dictionary<PursuitRoom, float> liveFloors = new();
        readonly Dictionary<PursuitRoom, int> liveRoomCounts = new();
        readonly List<Transform> livePlayers = new();
        readonly List<PursuitRoom> liveRoomsBySize = new();
        string[] livePlayerOptions = { "내 플레이어 (자동)" };
        string[] liveRoomOptions = Array.Empty<string>();
        Transform liveLocalPlayer;
        PursuitActor liveSelectedActor;
        int livePlacedCount;
        bool panelVisible = true;
        static readonly string[] LiveModes = { "플레이어 추적", "자유 모드" };
        static readonly Color LivePlayerColor = new(.4f, 1, .35f);
        readonly Vector3[] liveLine = new Vector3[2];
        Vector2 liveListScroll;
        void OnBecameVisible() { panelVisible = true; nextRefresh = 0; }
        void OnBecameInvisible() { panelVisible = false; }

        void ClearLiveCache()
        {
            liveFloors.Clear(); liveRoomCounts.Clear(); livePlayers.Clear(); liveRoomsBySize.Clear();
            liveWalls.Clear(); liveGeometryScene = 0;
            liveLocalPlayer = null; liveSelectedActor = null; nextLiveDiscovery = 0;
            liveRoomOptions = Array.Empty<string>(); livePlayerOptions = new[] { "내 플레이어 (자동)" };
        }
        void TickLive()
        {
            if (previewCamera != null || texture != null || previews.Count > 0) Cleanup();
            double now = EditorApplication.timeSinceStartup;
            if (director == null || now >= nextLiveDiscovery) {
                Refresh(); nextLiveDiscovery = now + 1;
                liveFloors.Clear(); liveRoomsBySize.Clear();
                liveRoomOptions = new string[rooms.Length];
                for (int i = 0; i < rooms.Length; i++) {
                    liveFloors[rooms[i]] = Mathf.Round(PursuitAuthoring.FloorReference(rooms[i]));
                    liveRoomsBySize.Add(rooms[i]); liveRoomOptions[i] = rooms[i].name;
                }
                liveRoomsBySize.Sort((a, b) => {
                    var sa = PursuitAuthoring.Bounds(a).size; var sb = PursuitAuthoring.Bounds(b).size;
                    return (sb.x * sb.z).CompareTo(sa.x * sa.z);
                });
                livePlacedCount = TotalPlacedCount();
                RefreshLivePlayers();
                if (director != null && liveGeometryScene != director.gameObject.scene.handle) CacheLiveWalls();
            }
            if (director == null || rooms.Length == 0) return;
            UpdateLiveTracking();
            liveRoomCounts.Clear();
            foreach (var room in rooms) liveRoomCounts[room] = 0;
            foreach (var actor in director.Actors) {
                if (actor == null) continue;
                var position = actor.transform.position;
                float floor = NearestFloor(position.y);
                foreach (var room in rooms)
                    if (Mathf.Abs(floor - FloorHeight(room)) < .75f && room.Contains(position)) liveRoomCounts[room]++;
            }
        }
        void RefreshLivePlayers()
        {
            livePlayers.Clear();
            foreach (var player in NetworkPlayer.All)
                if (player != null && player.IsSpawned && player.Slot != NetworkPlayer.NoSlot) livePlayers.Add(player.transform);
            liveLocalPlayer = NetworkPlayer.Local != null ? NetworkPlayer.Local.transform : null;
            if (livePlayers.Count == 0) {
                foreach (var mover in FindObjectsByType<PlayerMover>(FindObjectsSortMode.None))
                    if (mover.isActiveAndEnabled) livePlayers.Add(mover.transform);
                if (livePlayers.Count > 0) liveLocalPlayer = livePlayers[0];
            }
            livePlayerOptions = new string[livePlayers.Count + 1];
            livePlayerOptions[0] = "내 플레이어 (자동)";
            for (int i = 0; i < livePlayers.Count; i++) {
                var player = livePlayers[i].GetComponent<NetworkPlayer>();
                livePlayerOptions[i + 1] = player != null && player.IsSpawned ? $"플레이어 {player.Slot + 1}" : $"플레이어 {i + 1}";
                if (livePlayers[i] == liveLocalPlayer) livePlayerOptions[i + 1] += " · 나";
            }
            if (followedPlayerId != 0 && LiveFollowTarget() == null) followedPlayerId = 0;
        }
        Transform LiveFollowTarget()
        {
            if (followedPlayerId == 0) return liveLocalPlayer;
            foreach (var player in livePlayers) if (player != null && player.GetInstanceID() == followedPlayerId) return player;
            return null;
        }
        void UpdateLiveTracking()
        {
            if (!liveFollowPlayer || rooms.Length == 0) return;
            var target = LiveFollowTarget();
            if (target == null) return;
            if (!viewInitialized) { viewSpan = 32; zoom = 1; viewInitialized = true; }
            viewCenter = target.position;
            viewFloor = NearestFloor(target.position.y);
            autoTargetRoom = true; TargetCenterRoom();
        }
        int LiveRoomCount(PursuitRoom room) => liveRoomCounts.TryGetValue(room, out int count) ? count : 0;

        void DrawLiveGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
                int mode = GUILayout.Toolbar(liveFollowPlayer ? 0 : 1, LiveModes, EditorStyles.toolbarButton, GUILayout.Width(220));
                if ((mode == 0) != liveFollowPlayer) { liveFollowPlayer = mode == 0; EndPan(); UpdateLiveTracking(); }
                int current = 0;
                for (int i = 0; i < livePlayers.Count; i++)
                    if (livePlayers[i] != null && livePlayers[i].GetInstanceID() == followedPlayerId) current = i + 1;
                int chosen = EditorGUILayout.Popup(current, livePlayerOptions, GUILayout.Width(180));
                if (chosen != current) { followedPlayerId = chosen == 0 || livePlayers[chosen - 1] == null ? 0 : livePlayers[chosen - 1].GetInstanceID(); UpdateLiveTracking(); }
                liveActorLabels = GUILayout.Toggle(liveActorLabels, "전체 행동 이름", EditorStyles.toolbarButton);
                GUILayout.FlexibleSpace(); GUILayout.Label("LIVE · 2D 구조도 · 10Hz", EditorStyles.miniLabel);
            }
            if (director == null || rooms.Length == 0 || liveRoomOptions.Length != rooms.Length) {
                EditorGUILayout.HelpBox("추격 대기실을 기다리고 있습니다. 공연/씬 이동 후 대기실로 돌아오면 관찰을 재개합니다.", MessageType.Info); return;
            }
            if (liveFollowPlayer && LiveFollowTarget() == null)
                EditorGUILayout.HelpBox("추적할 플레이어를 기다리는 중입니다. 플레이어가 생성되면 자동으로 따라갑니다.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
                if (GUILayout.Button("◀", EditorStyles.toolbarButton, GUILayout.Width(30))) ChooseRoom(roomIndex - 1);
                int next = EditorGUILayout.Popup(roomIndex, liveRoomOptions, GUILayout.Width(270));
                if (next != roomIndex) ChooseRoom(next);
                if (GUILayout.Button("▶", EditorStyles.toolbarButton, GUILayout.Width(30))) ChooseRoom(roomIndex + 1);
                GUILayout.Label($"전체 배치 {livePlacedCount}체 · 실행 {director.Actors.Count}체 · 플레이어 {livePlayers.Count}명", EditorStyles.miniLabel);
            }
            DrawNavigationToolbar();
            using (new EditorGUILayout.HorizontalScope()) {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(215))) {
                    DrawCurrentRoomLabel();
                    GUILayout.Label("평면도 · 현재 층", EditorStyles.boldLabel);
                    DrawOverview(GUILayoutUtility.GetRect(210, 220));
                    sideAxis = GUILayout.Toolbar(sideAxis, new[] { "앞뒤 / 높이", "좌우 / 높이" });
                    DrawSideOverview(GUILayoutUtility.GetRect(210, 150));
                    GUILayout.Label("초록: 플레이어\n빨강: 추격 · 청록: 이동\n주황 테두리: 현재 방", EditorStyles.helpBox);
                    GUILayout.FlexibleSpace();
                }
                using (new EditorGUILayout.VerticalScope()) {
                    var area = GUILayoutUtility.GetRect(300, 300, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                    DrawLiveMap(area);
                    GUILayout.Label("드래그: 자유 모드로 화면 이동 · 휠: 확대 · 마네킹 클릭: 상세 · 더블클릭: Scene 보기 · F: 방 맞춤", EditorStyles.helpBox);
                }
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(230))) DrawLiveActorDetails();
            }
        }
        void DrawLiveActorDetails()
        {
            GUILayout.Label("추격자 관찰", EditorStyles.boldLabel);
            if (liveSelectedActor != null) {
                GUILayout.Label(liveSelectedActor.name, EditorStyles.wordWrappedLabel);
                GUILayout.Label($"행동: {liveSelectedActor.Behaviour}");
                GUILayout.Label($"위치: {liveSelectedActor.transform.position:F1}");
                var target = liveSelectedActor.ChasedPlayer;
                var player = target != null ? target.GetComponent<NetworkPlayer>() : null;
                GUILayout.Label(player != null ? $"추격 대상: 플레이어 {player.Slot + 1}" : "추격 대상: 없음");
                GUILayout.Label(liveSelectedActor.IsRendered ? "게임 화면 렌더: 켜짐" : "게임 화면 렌더: 숨김");
            } else EditorGUILayout.HelpBox("마네킹 점 또는 목록을 선택하면 행동과 순찰 경로를 표시합니다.", MessageType.None);
            GUILayout.Space(8); GUILayout.Label("현재 층 마네킹", EditorStyles.boldLabel);
            liveListScroll = EditorGUILayout.BeginScrollView(liveListScroll);
            foreach (var actor in director.Actors) {
                if (actor == null || !OnActiveFloor(actor.transform.position)) continue;
                if (GUILayout.Toggle(liveSelectedActor == actor, $"{actor.name}\n{actor.Behaviour}", "Button")) liveSelectedActor = actor;
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
