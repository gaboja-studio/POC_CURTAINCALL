using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.Rendering.Universal;

namespace CurtainCall.WaitingRoom.Editor
{
    public sealed partial class WaitingRoomObjectWindow
    {
        Camera mapCamera;
        RenderTexture mapTexture;
        IMGUIContainer map;
        Vector3 mapCenter;
        float mapSize = 40, cutHeight = 2.4f;
        bool mapInitialized, mapDragging, mapPanning;
        Vector3 dragStart, dragPosition;
        int mapUndo = -1;
        bool mapPrefabDragging;
        Vector2 mapPress;
        float panDistance;
        int panButton;
        bool showConditionalContents;
        float[] floorHeights = { 0 };
        DropdownField floorSelector;
        const float DefaultCutHeight = 2.4f;
        CurtainCall.WaitingRoom.Pursuit.PursuitRoom[] MapRooms() => IsIntegrated ? SceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<CurtainCall.WaitingRoom.Pursuit.PursuitRoom>(true)).ToArray()
            : System.Array.Empty<CurtainCall.WaitingRoom.Pursuit.PursuitRoom>();
        void ShowManagementRoom()
        {
            var room = MapRooms().FirstOrDefault(r => r.name == "B1_Office");
            if (room == null || room.Area == null) return;
            planeHeight = Mathf.Round(CurtainCall.WaitingRoom.Pursuit.Editor.PursuitAuthoring.FloorReference(room)); allFloors = false;
            selected = null; linked.Clear(); existingPaletteSource = null; placing = false;
            RefreshFloors(); RefreshList(); RefreshDetails();
            var b = room.Area.bounds; mapCenter = b.center;
            float aspect = map != null && map.contentRect.height > 1 ? map.contentRect.width / map.contentRect.height : 1;
            mapSize = Mathf.Max(3, Mathf.Max(b.size.z, b.size.x / Mathf.Max(.1f, aspect)) * .65f);
            mapInitialized = true; RepaintMap();
        }
        void RefreshFloors()
        {
            if (!IsIntegrated || floorSelector == null) return;
            var scene = SceneManager.GetActiveScene();
            floorHeights = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CurtainCall.WaitingRoom.Pursuit.PursuitRoom>(true))
                .Select(r => Mathf.Round(CurtainCall.WaitingRoom.Pursuit.Editor.PursuitAuthoring.FloorReference(r)))
                .Append(0f).Distinct().OrderBy(y => y).ToArray();
            floorSelector.choices = floorHeights.Select(y => $"{y:0.##}m 층").Append(AllFloorsChoice).ToList();
            int index = allFloors ? floorHeights.Length : System.Array.IndexOf(floorHeights, planeHeight);
            if (index < 0) { index = 0; planeHeight = floorHeights[0]; }
            floorSelector.SetValueWithoutNotify(floorSelector.choices[index]);
        }
        internal static float FloorForHeight(float height, float[] floors) => floors.Where(y => y <= height + .5f).DefaultIfEmpty(floors[0]).Max();
        bool OnSelectedFloor(Transform t) => t != null && (allFloors || Mathf.Abs(FloorForHeight(t.position.y, floorHeights) - planeHeight) < .1f);
        float SliceTop => SliceTopFor(planeHeight);
        void FocusObject(Transform target)
        {
            if (!IsIntegrated || !IsInteractionObject(target)) return;
            EndMapDrag(); placing = false; ClearCreation(); existingPaletteSource = null;
            planeHeight = FloorForHeight(target.position.y, floorHeights); allFloors = false; RefreshFloors();
            Selection.activeGameObject = target.gameObject; selected = target;
            RefreshList(); RefreshDetails(); FrameMap(true);
            status.text = $"{InteractionLabel(target)} · 위치 보기";
        }

        // 가운데 맵: 패널 전체 높이를 쓴다. 설정은 CreateMapControls로 오른쪽 패널에 둔다.
        VisualElement CreateMap()
        {
            map = new IMGUIContainer(DrawMap) { focusable = true, style = { flexGrow = 1, minHeight = 200, minWidth = 200 },
                tooltip = "클릭 선택 · 드래그 맵 이동 · Alt+드래그 오브젝트 이동·배치 · 휠 확대 · F 선택 위치 보기 · Esc 배치 취소 · Ctrl+Z 실행 취소" };
            map.schedule.Execute(RepaintMap).Every(100);
            return map;
        }
        VisualElement CreateMapControls()
        {
            var panel = new VisualElement { style = { flexShrink = 0 } };
            floorSelector = new DropdownField("표시 층");
            RefreshFloors();
            floorSelector.RegisterValueChangedCallback(e => {
                int index = floorSelector.choices.IndexOf(e.newValue);
                if (index < 0) return;
                EndMapDrag(); placing = false; ClearCreation(); existingPaletteSource = null;
                allFloors = index >= floorHeights.Length;
                if (!allFloors) planeHeight = floorHeights[index];
                if (!allFloors || selected == null) { selected = null; linked.Clear(); }
                RefreshList(); RefreshDetails(); FrameMap(false);
            });
            panel.Add(floorSelector);
            var settings = new Foldout { text = "보기 설정", value = true, viewDataKey = "waiting-room-object-map-settings" };
            panel.Add(settings);
            var contents = new Toggle("개방·발견 후 아이템 위치 보기") { value = showConditionalContents };
            contents.RegisterValueChangedCallback(e => { showConditionalContents = e.newValue; RepaintMap(); }); settings.Add(contents);
            settings.Add(CreateVisibilityControls());
            settings.Add(CreateAllFloorsControls());
            settings.Add(new Button(ShowManagementRoom) { text = "관리실 보기 · 지하 -5m" });
            var cut = new UnityEngine.UIElements.FloatField("천장 절단 높이 (m)") { value = cutHeight };
            cut.RegisterValueChangedCallback(e => { cutHeight = Mathf.Max(.2f, e.newValue); RepaintMap(); });
            settings.Add(cut);
            settings.Add(new Button(() => { cutHeight = DefaultCutHeight; cut.SetValueWithoutNotify(cutHeight); RepaintMap(); }) { text = "천장 절단 높이 기본값 (2.4m)" });
            return panel;
        }
        void RepaintMap() { map?.MarkDirtyRepaint(); Repaint(); }
        void MapSceneChanged(Scene before, Scene after)
        {
            CleanupMap(); mapInitialized = false; placing = false; ClearCreation(); RefreshFloors(); SyncVisibilityToggles(); SelectionChanged();
        }
        void MapPlayModeChanged(PlayModeStateChange state) { placing = false; CleanupMap(); SelectionChanged(); }
        void MapSceneClosing(Scene scene, bool removing) { CleanupMap(); mapInitialized = false; }
        void OnLostFocus() => EndMapDrag();
        void EndMapDrag()
        {
            if (mapUndo >= 0) Undo.CollapseUndoOperations(mapUndo);
            mapUndo = -1; mapDragging = mapPanning = mapPrefabDragging = false;
        }
        void CleanupMap()
        {
            EndMapDrag();
            CleanupSpotPreviews();
            if (mapCamera != null) { mapCamera.targetTexture = null; DestroyImmediate(mapCamera.gameObject); }
            if (mapTexture != null) { mapTexture.Release(); DestroyImmediate(mapTexture); }
        }
        Renderer[] MapRenderers() => Resources.FindObjectsOfTypeAll<Renderer>().Where(r => r != null
            && r.gameObject.scene == SceneManager.GetActiveScene() && r.gameObject.activeInHierarchy
            && r.enabled && (r.gameObject.hideFlags & HideFlags.HideAndDontSave) == 0).ToArray();
        void FrameMap(bool selection)
        {
            if (allFloors) {
                if (!selection || selected == null) { FrameAllFloors(); return; }
                ShowFloor(FloorOf(selected));
            }
            if (!selection) {
                var rooms = MapRooms().Where(r => r.Area != null && Mathf.Abs(Mathf.Round(CurtainCall.WaitingRoom.Pursuit.Editor.PursuitAuthoring.FloorReference(r)) - planeHeight) < .1f).ToArray();
                if (rooms.Length > 0) {
                    var b = rooms[0].Area.bounds;
                    foreach (var room in rooms) b.Encapsulate(room.Area.bounds);
                    mapCenter = b.center;
                    float aspect = map != null && map.contentRect.height > 1 ? map.contentRect.width / map.contentRect.height : 1;
                    mapSize = Mathf.Max(2, Mathf.Max(b.size.z, b.size.x / Mathf.Max(.1f, aspect)) * .6f);
                    mapInitialized = true; RepaintMap(); return;
                }
            }
            var renderers = selection && selected != null ? PlacementVisual(selected).GetComponentsInChildren<Renderer>(true) : MapRenderers()
                .Where(r => r.bounds.max.y >= planeHeight - .5f && r.bounds.min.y <= SliceTop).ToArray();
            if (renderers.Length > 0) {
                var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                mapCenter = bounds.center;
                float aspect = map != null && map.contentRect.height > 1 ? map.contentRect.width / map.contentRect.height : 1;
                mapSize = Mathf.Max(2, Mathf.Max(bounds.size.z, bounds.size.x / Mathf.Max(.1f, aspect)) * .6f);
            } else if (selection && selected != null) { mapCenter = PlacementPoint(selected); mapSize = 8; }
            mapInitialized = true; RepaintMap();
        }
        internal static Vector3 MapWorld(Vector2 pixel, Rect rect, Vector3 center, float size, float height)
        {
            float units = 2 * size / Mathf.Max(1, rect.height);
            return new Vector3(center.x + (pixel.x - rect.center.x) * units, height,
                center.z - (pixel.y - rect.center.y) * units);
        }
        Vector2 MapPixel(Vector3 world, Rect rect)
        {
            float units = rect.height / (2 * mapSize);
            return rect.center + new Vector2(world.x - mapCenter.x, mapCenter.z - world.z) * units;
        }
        void EnsureMapTarget(Rect rect)
        {
            if (mapCamera == null) {
                var go = new GameObject("__WaitingRoomObjectMap") { hideFlags = HideFlags.HideAndDontSave };
                mapCamera = go.AddComponent<Camera>(); mapCamera.enabled = false;
                mapCamera.orthographic = true; mapCamera.cameraType = CameraType.Preview;
                mapCamera.clearFlags = CameraClearFlags.SolidColor; mapCamera.backgroundColor = new Color(.06f, .07f, .085f);
                mapCamera.allowHDR = false;
                var data = mapCamera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false; data.renderShadows = false;
            }
            int w = Mathf.Clamp(Mathf.RoundToInt(rect.width * EditorGUIUtility.pixelsPerPoint), 32, 2048);
            int h = Mathf.Clamp(Mathf.RoundToInt(rect.height * EditorGUIUtility.pixelsPerPoint), 32, 2048);
            if (mapTexture == null || mapTexture.width != w || mapTexture.height != h) {
                if (mapTexture != null) { mapCamera.targetTexture = null; mapTexture.Release(); DestroyImmediate(mapTexture); }
                mapTexture = new RenderTexture(w, h, 24) { hideFlags = HideFlags.HideAndDontSave, name = "Waiting room object map" };
            }
            mapCamera.overrideSceneCullingMask = UnityEditor.SceneManagement.EditorSceneManager.GetSceneCullingMask(SceneManager.GetActiveScene());
            mapCamera.targetTexture = mapTexture;
        }
        System.Collections.Generic.Dictionary<Renderer, bool> MaskMapRenderers()
        {
            var masked = new System.Collections.Generic.Dictionary<Renderer, bool>();
            if (!showConditionalContents)
                foreach (var gate in SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ExplorationGate>(true)))
                    if (IsInteractionObject(gate.transform) && ConditionalContent(gate.transform) && gate.transform != selected)
                        foreach (var r in gate.GetComponentsInChildren<Renderer>(true))
                            if (!masked.ContainsKey(r)) { masked[r] = r.forceRenderingOff; r.forceRenderingOff = true; }
            MaskHiddenRenderers(masked);
            return masked;
        }
        static void RestoreMapRenderers(System.Collections.Generic.Dictionary<Renderer, bool> masked)
        {
            foreach (var entry in masked) if (entry.Key != null) entry.Key.forceRenderingOff = entry.Value;
        }
        void RenderMap(Rect rect)
        {
            PrepareSpotPreviews();
            EnsureMapTarget(rect);
            mapCamera.rect = new Rect(0, 0, 1, 1);
            mapCamera.transform.SetPositionAndRotation(new Vector3(mapCenter.x, planeHeight + 1000, mapCenter.z), Quaternion.Euler(90, 0, 0));
            mapCamera.nearClipPlane = Mathf.Max(.01f, 1000 - (SliceTop - planeHeight));
            mapCamera.farClipPlane = 1000.5f;
            mapCamera.orthographicSize = mapSize; mapCamera.aspect = rect.width / rect.height;
            var masked = MaskMapRenderers();
            try { mapCamera.Render(); }
            finally { RestoreMapRenderers(masked); }
        }
        void DrawMap()
        {
            var rect = new Rect(0, 0, map.contentRect.width, map.contentRect.height);
            if (rect.width < 1 || rect.height < 1) return;
            if (!IsIntegrated) { placing = false; CleanupMap(); GUI.Label(rect, "지정된 Integrated 대기실을 열어 주세요."); return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode) {
                placing = false; CleanupMap(); GUI.Label(rect, "플레이 중에는 배치를 편집할 수 없습니다."); return;
            }
            if (!mapInitialized) FrameMap(false);
            if (allFloors) { DrawAllFloors(rect); return; }
            if (Event.current.type == EventType.Repaint) RenderMap(rect);
            if (mapTexture != null) GUI.DrawTexture(rect, mapTexture, ScaleMode.StretchToFill, false);
            GUI.Label(new Rect(8, 8, 500, 22), $"N ↑   {planeHeight:0.##}m 층 · {(placing ? "Alt+드래그 새 오브젝트 배치" : "드래그 맵 이동 / Alt+드래그 오브젝트 이동")}", EditorStyles.whiteBoldLabel);
            Handles.BeginGUI();
            foreach (var target in objectRows.Where(t => t != null && t != selected && !hideInteractions)) {
                if (!showConditionalContents && ConditionalContent(target)) continue;
                var p = MapPixel(PlacementPoint(target), rect);
                if (!rect.Contains(p)) continue;
                Handles.color = target.gameObject.activeSelf ? Color.cyan : Color.gray;
                Handles.DrawSolidDisc(p, Vector3.forward, 3);
            }
            if (mapPrefabDragging) {
                Handles.color = Color.cyan;
                Handles.DrawWireDisc(mapPress, Vector3.forward, 12);
                GUI.Label(new Rect(mapPress.x + 15, mapPress.y, 220, 24), existingPaletteSource != null ? existingPaletteSource.name : HasCreation ? CreationLabel : prefab != null ? prefab.name : "", EditorStyles.whiteLabel);
            }
            if (selected != null) {
                Handles.color = Color.yellow;
                var p = MapPixel(PlacementPoint(selected), rect);
                Handles.DrawWireDisc(p, Vector3.forward, 10);
                GUI.Label(new Rect(p.x + 12, p.y, 320, 22), InteractionLabel(selected) + (ConditionalContent(selected) ? " · 개방 후 발견 위치" : ""), EditorStyles.whiteLabel);
                foreach (var anchor in linked.Where(t => t != null)) {
                    var q = MapPixel(anchor.position, rect);
                    Handles.color = Color.cyan; Handles.DrawDottedLine(p, q, 4); Handles.DrawWireDisc(q, Vector3.forward, 6);
                }
            }
            Handles.EndGUI();
            MapInput(rect);
        }
        Transform PickMap(Vector2 mouse, Rect rect)
        {
            if (hideInteractions) return null;
            var marker = objectRows.Where(t => t != null && OnSelectedFloor(t) && (showConditionalContents || !ConditionalContent(t) || t == selected))
                .OrderBy(t => (MapPixel(PlacementPoint(t), rect) - mouse).sqrMagnitude).FirstOrDefault();
            if (marker != null && Vector2.Distance(MapPixel(PlacementPoint(marker), rect), mouse) <= 9) return marker;
            var world = MapWorld(mouse, rect, mapCenter, mapSize, SliceTop);
            var ray = new Ray(world, Vector3.down);
            Transform found = null; float nearest = float.MaxValue;
            foreach (var r in MapRenderers()) {
                var b = r.bounds;
                float lo = Mathf.Max(b.min.y, planeHeight - .5f), hi = Mathf.Min(b.max.y, SliceTop);
                if (hi < lo) continue;
                b.SetMinMax(new Vector3(b.min.x, lo, b.min.z), new Vector3(b.max.x, hi, b.max.z));
                var interaction = InteractionRoot(r.transform);
                if (interaction != null && (showConditionalContents || !ConditionalContent(interaction) || interaction == selected) && OnSelectedFloor(interaction) && b.IntersectRay(ray, out float distance) && distance < nearest) { nearest = distance; found = interaction; }
            }
            foreach (var preview in spotPreviews) {
                if (preview.Key == null || preview.Value == null || !OnSelectedFloor(preview.Key.transform)) continue;
                foreach (var r in preview.Value.GetComponentsInChildren<Renderer>())
                    if (r.bounds.IntersectRay(ray, out float distance) && distance < nearest) { nearest = distance; found = preview.Key.transform; }
            }
            if (found == null) return null;
            return found;
        }
        void PlaceMap(Vector2 mouse, Rect rect)
        {
            if (!IsIntegrated || (!HasCreation && !InteractionPrefab(prefab)) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            if (parent != null && parent.gameObject.scene != scene) { status.text = "배치 부모는 현재 씬에서 선택하세요."; return; }
            var point = MapWorld(mouse, rect, mapCenter, mapSize, planeHeight);
            var ray = new Ray(new Vector3(point.x, SliceTop, point.z), Vector3.down);
            var hits = Physics.RaycastAll(ray, SliceTop - planeHeight + .5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                .Where(h => h.collider.gameObject.scene == scene).OrderBy(h => h.distance).ToArray();
            if (hits.Length > 0) point = hits[0].point;
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            var instance = HasCreation ? CreateCatalogObject(scene) : (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            if (instance == null) return;
            if (!HasCreation) {
                Undo.RegisterCreatedObjectUndo(instance, "대기실 기존 프리팹 배치");
                try { PrepareNewPrefab(instance); }
                catch (System.Exception error) { Undo.RevertAllDownToGroup(group); status.text = "추가 취소: " + error.Message; return; }
            }
            if (parent != null) Undo.SetTransformParent(instance.transform, parent, "배치 부모");
            Undo.RecordObject(instance.transform, "기물 배치 위치");
            instance.transform.position = point;
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(group);
            placing = false; ClearCreation(); Selection.activeGameObject = instance;
            status.text = "배치 완료 · 기존 발견품·상호작용 시스템에 연결됨";
        }
        void MapInput(Rect rect)
        {
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) {
                placing = false; ClearCreation(); existingPaletteSource = null; EndMapDrag(); RefreshPalette(); e.Use(); return;
            }
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.F) { FrameMap(true); e.Use(); return; }
            if (e.type == EventType.MouseUp) {
                if (mapPrefabDragging && rect.Contains(e.mousePosition)) {
                    if (existingPaletteSource != null && IsInteractionObject(existingPaletteSource) && OnSelectedFloor(existingPaletteSource)) {
                        selected = existingPaletteSource;
                        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
                        ApplyPlacement(MapWorld(e.mousePosition, rect, mapCenter, mapSize, PlacementPoint(selected).y), selected.rotation);
                        Undo.CollapseUndoOperations(group);
                        existingPaletteSource = null; status.text = "기존 상호작용 오브젝트 재배치 완료";
                    } else PlaceMap(e.mousePosition, rect);
                }
                else if (mapPanning && panButton == 0 && panDistance < 3 && rect.Contains(e.mousePosition)) {
                    var hit = PickMap(e.mousePosition, rect);
                    if (hit != null) Selection.activeGameObject = hit.gameObject;
                }
                EndMapDrag(); RefreshDetails(); RefreshPalette(); e.Use(); return;
            }
            if (!rect.Contains(e.mousePosition) && !mapDragging && !mapPanning && !mapPrefabDragging) return;
            if (e.type == EventType.ScrollWheel) {
                var before = MapWorld(e.mousePosition, rect, mapCenter, mapSize, planeHeight);
                mapSize = Mathf.Clamp(mapSize * Mathf.Exp(e.delta.y * .08f), .5f, 2000);
                mapCenter += before - MapWorld(e.mousePosition, rect, mapCenter, mapSize, planeHeight);
                e.Use(); RepaintMap();
            } else if (e.type == EventType.MouseDown) {
                map.Focus();
                if (e.button == 0 && !e.alt && e.clickCount >= 2) {
                    var target = PickMap(e.mousePosition, rect);
                    if (target != null) FocusObject(target);
                    e.Use(); return;
                }
                if (e.button == 2 || (!e.alt && e.button == 0)) {
                    mapPanning = true; panDistance = 0; panButton = e.button;
                }
                else if (e.alt && e.button == 0) {
                    if (existingPaletteSource != null || (placing && (HasCreation || InteractionPrefab(prefab)))) { mapPrefabDragging = true; mapPress = e.mousePosition; e.Use(); return; }
                    var hit = PickMap(e.mousePosition, rect);
                    // Preserve an explicitly selected functional parent when clicking its visual child.
                    if (selected != null && hit != null && hit.IsChildOf(selected)) hit = selected;
                    if (hit != null) {
                        if (selected != hit) Selection.activeGameObject = hit.gameObject;
                        selected = hit; dragPosition = selected.position;
                        dragStart = MapWorld(e.mousePosition, rect, mapCenter, mapSize, selected.position.y);
                        mapDragging = true;
                        Undo.IncrementCurrentGroup(); mapUndo = Undo.GetCurrentGroup();
                        Undo.SetCurrentGroupName("대기실 맵 오브젝트 이동");
                    }
                }
                e.Use();
            } else if (e.type == EventType.MouseDrag) {
                if (mapPanning) {
                    panDistance += e.delta.magnitude;
                    mapCenter += new Vector3(-e.delta.x, 0, e.delta.y) * (2 * mapSize / rect.height);
                }
                else if (mapPrefabDragging) mapPress = e.mousePosition;
                else if (mapDragging && selected != null) Apply(dragPosition + MapWorld(e.mousePosition, rect, mapCenter, mapSize, dragPosition.y) - dragStart, selected.rotation);
                else return;
                e.Use(); RepaintMap();
            }
        }
    }
}
