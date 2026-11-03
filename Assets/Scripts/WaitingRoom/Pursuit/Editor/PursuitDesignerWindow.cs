using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public sealed partial class PursuitDesignerWindow : EditorWindow
    {
        PursuitDirector director;
        PursuitRoom[] rooms = Array.Empty<PursuitRoom>();
        int roomIndex, pointIndex = -1, stageIndex, inspectorTab;
        PursuitPlacement selected;
        bool addPoint;
        float cutHeight = 2.4f, zoom = 1;
        Vector2 inspectorScroll, paletteScroll;
        Camera previewCamera;
        RenderTexture texture;
        readonly Dictionary<PursuitPlacement, GameObject> previews = new();
        double nextRefresh;
        GameObject dragPrefab;
        bool dragging;
        int dragUndo = -1;
        GUIStyle titleStyle, badgeStyle;
        PursuitRoom Room => rooms.Length > 0 ? rooms[Mathf.Clamp(roomIndex, 0, rooms.Length - 1)] : null;
        [MenuItem("Tools/Curtain Call/Pursuit/Mannequin Designer")]
        public static PursuitDesignerWindow Open()
        {
            var window = GetWindow<PursuitDesignerWindow>("추격자 배치");
            window.minSize = new Vector2(1060, 700); window.Show(); return window;
        }
        public static void NotifyRouteRemoved(PursuitPlacement item)
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<PursuitDesignerWindow>()) {
                if (window.selected == item) { window.pointIndex = -1; window.addPoint = false; }
                window.ShowNotification(new GUIContent($"{item.name}: 순찰 경로에서 {item.RouteDetachDistance:0.#}m를 초과하여 동선이 제거되었습니다."), 3);
                window.Repaint();
            }
        }
        void OnEnable()
        {
            SubscribePreviewLifetime();
            EditorApplication.update += Tick; Undo.undoRedoPerformed += Changed;
            EditorApplication.playModeStateChanged += PlayChanged; Refresh();
        }
        void OnDisable()
        {
            UnsubscribePreviewLifetime();
            EndPan();
            EditorApplication.update -= Tick; Undo.undoRedoPerformed -= Changed;
            EditorApplication.playModeStateChanged -= PlayChanged; Cleanup();
        }
        void PlayChanged(PlayModeStateChange _) { Cleanup(); selected = null; pointIndex = -1; Refresh(); }
        void Changed() { Cleanup(); Refresh(); Repaint(); }
        void Tick()
        {
            if (!panelVisible) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode != EditorApplication.isPlaying) return;
            if (EditorApplication.timeSinceStartup < nextRefresh) return;
            nextRefresh = EditorApplication.timeSinceStartup + .1;
            if (EditorApplication.isPlaying) TickLive();
            else Refresh();
            Repaint();
        }
        void Refresh()
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!EditorApplication.isPlaying && director != null && director.gameObject.scene != active) {
                Cleanup(); director = null;
            }
            if (director == null) {
                if (previews.Count > 0 || previewCamera != null || texture != null) Cleanup();
                director = FindObjectsByType<PursuitDirector>(FindObjectsSortMode.None)
                    .FirstOrDefault(d => EditorApplication.isPlaying || d.gameObject.scene == active);
            }
            rooms = director != null && director.Rooms != null ? director.Rooms.Where(r => r != null).ToArray() : Array.Empty<PursuitRoom>();
            roomIndex = Mathf.Clamp(roomIndex, 0, Mathf.Max(0, rooms.Length - 1));
        }
        void Cleanup()
        {
            ClearLiveCache();
            foreach (var item in previews.Values) if (item != null) DestroyImmediate(item);
            previews.Clear();
            if (previewCamera != null) DestroyImmediate(previewCamera.gameObject);
            if (texture != null) { texture.Release(); DestroyImmediate(texture); }
        }
        void ChooseRoom(int index)
        {
            if (EditorApplication.isPlaying) liveFollowPlayer = false;
            roomIndex = (index + rooms.Length) % rooms.Length;
            selected = null; pointIndex = -1; addPoint = false; FrameRoom();
            if (!EditorApplication.isPlaying) Cleanup();
        }
        void OnGUI()
        {
            titleStyle ??= new GUIStyle(EditorStyles.boldLabel) { fontSize = 16 };
            badgeStyle ??= new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            if (EditorApplication.isPlaying) { DrawLiveGUI(); return; }
            if (director == null || Room == null) {
                EditorGUILayout.HelpBox("추격 시스템이 있는 대기실 씬을 열어 주세요. 플레이 중에는 이 창에서 실시간 구조도를 볼 수 있습니다.", MessageType.Info);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                    if (GUILayout.Button("추격 테스트 대기실 열기", GUILayout.Height(36)) && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                        EditorSceneManager.OpenScene(PursuitAuthoring.ScenePath); Refresh();
                    }
                return;
            }
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
                if (GUILayout.Button("◀ 이전 방", EditorStyles.toolbarButton, GUILayout.Width(90))) ChooseRoom(roomIndex - 1);
                int next = EditorGUILayout.Popup(roomIndex, rooms.Select((r, i) => $"{i + 1:00}  {r.name} · {PlacedCount(r)}체").ToArray(), GUILayout.Width(290));
                if (next != roomIndex) ChooseRoom(next);
                if (GUILayout.Button("다음 방 ▶", EditorStyles.toolbarButton, GUILayout.Width(90))) ChooseRoom(roomIndex + 1);
                GUILayout.Label($"전체 배치 {TotalPlacedCount()}체" + (EditorApplication.isPlaying ? $" · 실행 {director.Actors.Count(a => a != null)}체" : ""), EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label(EditorApplication.isPlaying ? "● LIVE · 배치 편집 잠김" : "EDIT · Ctrl+Z 실행 취소", EditorStyles.miniLabel);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                    if (GUILayout.Button("저장", EditorStyles.toolbarButton, GUILayout.Width(60))) {
                        EditorSceneManager.SaveScene(director.gameObject.scene); AssetDatabase.SaveAssetIfDirty(GameSettings.WaitingRoom);
                    }
            }
            if (rooms.Any(r => !r.UsePlacements)) {
                EditorGUILayout.HelpBox("기존 방 배치를 개별 마네킹 배치로 전환하면 타입·독립 순찰 경로를 편집할 수 있습니다. 현재 위치와 타입은 유지됩니다.", MessageType.Info);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                    if (GUILayout.Button("기존 배치 가져오기 (Undo 가능)")) { PursuitAuthoring.Migrate(director); Changed(); }
            }
            DrawNavigationToolbar();
            using (new EditorGUILayout.HorizontalScope()) {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(215))) {
                    GUILayout.Label("마네킹 팔레트", titleStyle);
                    GUILayout.Label("카드를 탑뷰로 끌어서 놓으세요", EditorStyles.miniLabel);
                    DrawPalette();
                    DrawCurrentRoomLabel();
                    GUILayout.Label($"평면도 · {ActiveFloor:0}m 층 (X/Z)", EditorStyles.boldLabel);
                    DrawOverview(GUILayoutUtility.GetRect(210, 180, GUILayout.ExpandWidth(true)));
                    GUILayout.Label($"측면도 · {ActiveFloor:0}m 층", EditorStyles.boldLabel);
                    sideAxis = GUILayout.Toolbar(sideAxis, new[] { "앞뒤 / 높이", "좌우 / 높이" });
                    DrawSideOverview(GUILayoutUtility.GetRect(210, 150, GUILayout.ExpandWidth(true)));
                    GUILayout.Label("주황 테두리: 현재 방 · 클릭으로 이동", EditorStyles.miniLabel);
                    GUILayout.Label("빨강 점: 추격 / 청록 점: 이동", EditorStyles.miniLabel);
                }
                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true))) {
                    using (new EditorGUILayout.HorizontalScope()) {
                        GUILayout.Label(HasTargetRoom ? Room.name : "중심에 해당하는 방 없음", titleStyle); GUILayout.FlexibleSpace();
                        GUILayout.Label("천장 절단 높이", GUILayout.Width(85)); cutHeight = EditorGUILayout.Slider(cutHeight, .5f, 6, GUILayout.Width(150));
                    }
                    Rect viewport = GUILayoutUtility.GetRect(300, 300, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                    DrawTopView(viewport);
                    GUILayout.Label(addPoint ? "감시 지점: 바닥 클릭 · 중클릭 드래그로 화면 이동 · Esc 종료" : "빈 곳 좌클릭 / 중클릭 드래그: 화면 이동 · 마커 드래그: 편집 · 더블클릭: Scene 보기 · 휠: 확대 · F: 방 맞춤", EditorStyles.helpBox);
                }
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(305))) {
                    inspectorTab = GUILayout.Toolbar(inspectorTab, new[] { "선택 / 순찰", "단계 / 연출" });
                    inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll);
                    using (new EditorGUI.DisabledScope(EditorApplication.isPlaying)) {
                        if (inspectorTab == 0) DrawSelection(); else DrawSettings();
                    }
                    EditorGUILayout.EndScrollView();
                    if (EditorApplication.isPlaying) EditorGUILayout.HelpBox("플레이 중에는 위치와 상태만 관찰합니다. 설정·배치 변경은 Play를 종료한 뒤 해 주세요.", MessageType.Info);
                }
            }
        }
        void DrawPalette()
        {
            paletteScroll = EditorGUILayout.BeginScrollView(paletteScroll, GUILayout.ExpandHeight(true));
            var prefabs = director.Mannequins ?? Array.Empty<GameObject>();
            for (int i = 0; i < prefabs.Length; i += 2) {
                using (new EditorGUILayout.HorizontalScope()) for (int j = i; j < Mathf.Min(i + 2, prefabs.Length); j++) {
                    var prefab = prefabs[j]; if (prefab == null) continue;
                    Rect rect = GUILayoutUtility.GetRect(92, 94, GUILayout.Width(94));
                    GUI.Box(rect, GUIContent.none);
                    var icon = AssetPreview.GetAssetPreview(prefab) ?? AssetPreview.GetMiniThumbnail(prefab);
                    if (icon != null) GUI.DrawTexture(new Rect(rect.x + 10, rect.y + 2, 74, 68), icon, ScaleMode.ScaleToFit);
                    GUI.Label(new Rect(rect.x, rect.y + 70, rect.width, 20), prefab.name.Replace("Pursuit_", ""), badgeStyle);
                    var e = Event.current;
                    if (!EditorApplication.isPlaying && rect.Contains(e.mousePosition) && e.type == EventType.MouseDown && e.button == 0) { dragPrefab = prefab; e.Use(); }
                    if (!EditorApplication.isPlaying && e.type == EventType.MouseDrag && dragPrefab == prefab) {
                        DragAndDrop.PrepareStartDrag(); DragAndDrop.objectReferences = new UnityEngine.Object[] { prefab }; DragAndDrop.StartDrag(prefab.name); dragPrefab = null; e.Use();
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }
        void DrawSelection()
        {
            if (!HasTargetRoom) { EditorGUILayout.HelpBox("화면 중심이 현재 층의 방 밖에 있습니다. 화면을 방으로 이동하거나 구조도에서 방을 선택해 주세요.", MessageType.Info); return; }
            GUILayout.Label("방 설정", EditorStyles.boldLabel);
            var roomSO = new SerializedObject(Room); roomSO.Update();
            EditorGUILayout.PropertyField(roomSO.FindProperty("RoamAcrossRooms"), new GUIContent("다른 방까지 추격")); roomSO.ApplyModifiedProperties();
            int count = Room.UsePlacements ? Room.Placements.Count(p => p != null) : Room.Count;
            GUILayout.Label($"현재 방 {count}체 / 전체 {TotalPlacedCount()}체", EditorStyles.miniLabel);
            if (selected == null) { EditorGUILayout.HelpBox("탑뷰의 원형 마커를 클릭하면 마네킹을 선택합니다. 시야 부채꼴은 단계 설정의 시야각·범위를 보여줍니다.", MessageType.Info); return; }
            EditorGUILayout.Space(); GUILayout.Label(selected.name, EditorStyles.boldLabel);
            var prefabs = director.Mannequins;
            int index = Array.IndexOf(prefabs, selected.Prefab);
            int type = EditorGUILayout.Popup("마네킹 타입", index, prefabs.Select(p => p != null ? p.name.Replace("Pursuit_", "") : "없음").ToArray());
            if (type != index && type >= 0) { Undo.RecordObject(selected, "마네킹 타입 변경"); selected.Prefab = prefabs[type]; PursuitAuthoring.Dirty(selected); Cleanup(); }
            using (new EditorGUI.DisabledScope(true)) EditorGUILayout.Vector3Field("지면 위치", selected.transform.position);
            EditorGUI.BeginChangeCheck();
            float yaw = EditorGUILayout.Slider("방향 (°)", selected.transform.eulerAngles.y, 0, 360);
            if (EditorGUI.EndChangeCheck()) PursuitAuthoring.Move(selected, selected.transform.position, Quaternion.Euler(0, yaw, 0));
            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("↶ 15°")) Rotate(-15);
                if (GUILayout.Button("15° ↷")) Rotate(15);
                if (GUILayout.Button("마네킹 제거")) { pointIndex = -1; RemoveSelection(); GUIUtility.ExitGUI(); }
            }
            GUILayout.Space(12); GUILayout.Label("마네킹 설정", EditorStyles.boldLabel);
            var placementSO = new SerializedObject(selected);
            placementSO.Update();
            var activation = placementSO.FindProperty("ActivationStage");
            EditorGUI.BeginChangeCheck();
            int activationIndex = EditorGUILayout.Popup("활동 시작 단계", Mathf.Clamp(activation.intValue - 1, 0, 3), ActivationStageLabels);
            if (EditorGUI.EndChangeCheck()) activation.intValue = activationIndex + 1;
            EditorGUILayout.HelpBox("지정 단계 전에는 장식처럼 대기합니다. 해당 단계부터 각성 후 순찰·탐지·추격하며, 이후 단계에도 계속 활동합니다.", MessageType.None);
            var stages = GameSettings.WaitingRoom.Pursuit.Stages;
            if (stages == null || activationIndex >= stages.Length || stages[activationIndex] == null)
                EditorGUILayout.HelpBox("지정한 단계가 전역 설정에 없습니다. 단계/연출 설정을 확인하세요.", MessageType.Warning);
            else EditorGUILayout.LabelField($"시작 시점: 추격 시작 후 {stages[activationIndex].StartsAt:0.#}초", EditorStyles.wordWrappedLabel);
            EditorGUILayout.PropertyField(placementSO.FindProperty("Size"), new GUIContent("크기 (Small 소형 / Medium 중형)"));
            EditorGUILayout.HelpBox("중형은 외형 1.16배입니다. 이동 속도와 탐지·포획 규칙은 단계 설정을 공유합니다.", MessageType.None);
            GUILayout.Space(12); GUILayout.Label("감시 지점 · 순서대로 반복 순찰", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(placementSO.FindProperty("RouteDetachDistance"), new GUIContent("동선 자동 해제 거리 (m)", "가장 가까운 순찰 선분까지의 거리. 0은 자동 해제 안 함."));
            placementSO.ApplyModifiedProperties();
            addPoint = GUILayout.Toggle(addPoint, "＋ 바닥 클릭으로 감시 지점 추가", "Button");
            var points = selected.PatrolPoints;
            if (points == null || points.Length == 0) EditorGUILayout.HelpBox("감시 지점이 없으면 시작 위치에서 대기합니다. 1개면 해당 위치를 지키고, 2개 이상이면 순서대로 순환합니다.", MessageType.None);
            for (int i = 0; points != null && i < points.Length; i++) {
                using (new EditorGUILayout.HorizontalScope()) {
                    if (GUILayout.Toggle(pointIndex == i, $"{i + 1:00}  감시 지점", "Button")) pointIndex = i;
                    using (new EditorGUI.DisabledScope(i == 0)) if (GUILayout.Button("↑", GUILayout.Width(27))) { PursuitAuthoring.Reorder(selected, i, i - 1); pointIndex = i - 1; GUIUtility.ExitGUI(); }
                    using (new EditorGUI.DisabledScope(i == points.Length - 1)) if (GUILayout.Button("↓", GUILayout.Width(27))) { PursuitAuthoring.Reorder(selected, i, i + 1); pointIndex = i + 1; GUIUtility.ExitGUI(); }
                    if (GUILayout.Button("×", GUILayout.Width(25))) { PursuitAuthoring.RemovePoint(selected, i); pointIndex = -1; GUIUtility.ExitGUI(); }
                }
            }
        }
        static readonly string[] ActivationStageLabels = { "1단계부터", "2단계부터", "3단계부터", "4단계부터" };
        void Rotate(float degrees) { if (selected != null) PursuitAuthoring.Move(selected, selected.transform.position, Quaternion.Euler(0, selected.transform.eulerAngles.y + degrees, 0)); }
        void RemoveSelection()
        {
            if (selected == null) return;
            if (pointIndex >= 0 && pointIndex < selected.PatrolPoints.Length) { PursuitAuthoring.RemovePoint(selected, pointIndex); pointIndex = -1; }
            else { PursuitAuthoring.Remove(Room, selected); selected = null; Cleanup(); }
        }
    }
}
