using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace CurtainCall.WaitingRoom.Editor
{
    // Edits the scene's real objects. No placement copy or alternate interaction database.
    public sealed partial class WaitingRoomObjectWindow : EditorWindow
    {
        Transform selected;
        GameObject prefab;
        Transform parent;
        string filter = "";
        bool placing;
        float planeHeight;
        ScrollView objects;
        ScrollView details;
        readonly List<Transform> objectRows = new();
        Label status;
        readonly List<Transform> linked = new();
        TwoPaneSplitView leftSplit, rightSplit;
        bool wideMap;
        // 좌우 패널을 접어 맵만 크게 보거나 다시 편다.
        void ToggleWideMap()
        {
            if (leftSplit == null || rightSplit == null) return;
            wideMap = !wideMap;
            if (wideMap) { leftSplit.CollapseChild(0); rightSplit.CollapseChild(1); }
            else { leftSplit.UnCollapse(); rightSplit.UnCollapse(); }
            RepaintMap();
        }
        const string IntegratedScenePath = "Assets/Scenes/InGame/WaitingRoom/WaitingRoom.unity";
        static bool IsIntegrated => SceneManager.GetActiveScene().path == IntegratedScenePath;
        internal static bool IsFurnitureGate(ExplorationGateKind kind, bool key, string id)
        {
            id = (id ?? "").ToLowerInvariant();
            if (id.Contains("door") || kind == ExplorationGateKind.Cooperative) return false;
            return key || kind is ExplorationGateKind.Breakable or ExplorationGateKind.Monitor or ExplorationGateKind.Keypad
                || id.Contains("box") || id.Contains("cabinet");
        }
        internal static bool IsInteractionObject(Transform t)
        {
            if (t == null) return false;
            foreach (var c in t.GetComponents<MonoBehaviour>()) {
                if (c == null) continue;
                if (c is ExplorationGate gate) {
                    var kind = new SerializedObject(gate).FindProperty("kind");
                    if (kind != null && IsFurnitureGate((ExplorationGateKind)kind.enumValueIndex, gate.IsKeyPickup, gate.TargetId)) return true;
                }
                else if (c is BreakableWall wall) { if (wall.TargetId == "structure-a:machine-pipe-box") return true; }
                else if (c.GetType().Name is "ShopPhone" or "NightBed" or "ScheduleBoardView" or "HandoverShelfView" or "DebugMoneyProp" or "DebugEscapeKeyProp" or "ClueInspectable" or "PerformancePropPickup" or "ToolTarget" or "UvMark" or "FoundItemSpot") return true;
            }
            return false;
        }
        static string InteractionLabel(Transform t)
        {
            var label = t.TryGetComponent<FoundItemSpot>(out var spot) ? ItemCatalog.Get(SpotItem(spot))?.displayName ?? t.name
                : t.GetComponents<MonoBehaviour>().OfType<IExplorationTarget>().FirstOrDefault()?.Label ?? t.name;
            return label == "파괴 가능한 상자" ? "낡은 상자" : label;
        }
        static bool CurrentLayoutTarget(Transform t)
        {
            for (var p = t.parent; p != null; p = p.parent) if (!p.gameObject.activeSelf) return false;
            return true;
        }
        internal static Transform InteractionRoot(Transform t)
        {
            for (; t != null; t = t.parent) if (IsInteractionObject(t)) return t;
            return null;
        }
        internal static bool IsPositionAnchor(Transform t) => t != null && t.childCount == 0
            && t.GetComponents<Component>().All(c => c is Transform);
        static bool InteractionPrefab(GameObject value) => value != null && PrefabUtility.IsPartOfPrefabAsset(value) && IsInteractionObject(value.transform)
            && !value.GetComponentsInChildren<ExplorationGate>(true).Any(g => g.IsKeyPickup);

        [MenuItem("Tools/CurtainCall/대기실/오브젝트 배치")]
        public static void Open()
        {
            var window = GetWindow<WaitingRoomObjectWindow>("대기실 오브젝트 배치");
            if (!window.docked && (window.position.width < 1200 || window.position.height < 800)) window.position = LargeWindowRect();
            window.Show();
        }
        // 메인 에디터 창의 90% 크기, 가운데.
        static Rect LargeWindowRect()
        {
            var main = EditorGUIUtility.GetMainWindowPosition();
            float w = Mathf.Max(1200, main.width * .9f), h = Mathf.Max(800, main.height * .9f);
            return new Rect(main.x + (main.width - w) / 2, main.y + (main.height - h) / 2, w, h);
        }
        // 도킹 위치와 무관하게 큰 독립 창으로 다시 연다. 기존 창은 닫는다(씬 내용은 그대로).
        void OpenLargeFloating()
        {
            var window = CreateInstance<WaitingRoomObjectWindow>();
            window.titleContent = new GUIContent("대기실 오브젝트 배치");
            window.Show();
            window.position = LargeWindowRect();
            Close();
        }
        // 배치 부모·배치 모드·상태 안내. 오른쪽 패널에 둔다.
        VisualElement CreatePlacementControls()
        {
            var panel = new Foldout { text = "배치", value = true, viewDataKey = "waiting-room-object-placement" };
            panel.Add(new Label("상호작용 오브젝트 전용 · 벽·바닥·문·맵 구조물은 편집 불가") { style = { whiteSpace = WhiteSpace.Normal } });
            var owner = new ObjectField("배치 부모") { objectType = typeof(Transform), allowSceneObjects = true };
            owner.RegisterValueChangedCallback(e => parent = e.newValue as Transform); panel.Add(owner);
            panel.Add(new Button(() => {
                placing = IsIntegrated && (HasCreation || InteractionPrefab(prefab)) && !EditorApplication.isPlayingOrWillChangePlaymode;
                status.text = placing ? "맵에서 Alt+드래그 후 놓아 배치 · Esc 취소" : "지원하는 상호작용 컴포넌트가 루트에 있는 프리팹을 선택하세요. 구조물은 배치할 수 없습니다.";
                RepaintMap();
            }) { text = "Alt+드래그 배치 모드" });
            status = new Label { style = { whiteSpace = WhiteSpace.Normal } }; panel.Add(status);
            panel.Add(new Label("클릭 선택 · 드래그 맵 이동 · Alt+드래그 이동·배치 · 휠 확대 · F 선택 위치 · Esc 취소 · Ctrl+Z 실행 취소") { style = { whiteSpace = WhiteSpace.Normal, color = Color.gray } });
            return panel;
        }

        void OnEnable()
        {
            Selection.selectionChanged += SelectionChanged;
            EditorApplication.hierarchyChanged += RefreshList;
            Undo.undoRedoPerformed += Refresh;
            EditorSceneManager.activeSceneChangedInEditMode += MapSceneChanged;
            AssemblyReloadEvents.beforeAssemblyReload += CleanupMap;
            EditorApplication.playModeStateChanged += MapPlayModeChanged;
            EditorSceneManager.sceneClosing += MapSceneClosing;
        }
        void OnDisable()
        {
            Selection.selectionChanged -= SelectionChanged;
            EditorApplication.hierarchyChanged -= RefreshList;
            Undo.undoRedoPerformed -= Refresh;
            EditorSceneManager.activeSceneChangedInEditMode -= MapSceneChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= CleanupMap;
            EditorApplication.playModeStateChanged -= MapPlayModeChanged;
            EditorSceneManager.sceneClosing -= MapSceneClosing;
            CleanupPalette();
            CleanupMap();
        }
        public void CreateGUI()
        {
            rootVisualElement.Clear();
            minSize = new Vector2(640, 480);
            var toolbar = new Toolbar(); toolbar.style.flexShrink = 0;
            toolbar.Add(new ToolbarButton(() => {
                if (IsIntegrated) return;
                if (!EditorApplication.isPlayingOrWillChangePlaymode && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    EditorSceneManager.OpenScene(IntegratedScenePath);
            }) { text = "Integrated 대기실 열기" });
            toolbar.Add(new ToolbarButton(() => FrameMap(false)) { text = "맵 전체 보기" });
            toolbar.Add(new ToolbarButton(() => FrameMap(true)) { text = "선택 위치 보기" });
            toolbar.Add(new ToolbarButton(ToggleWideMap) { text = "맵 크게 보기" });
            toolbar.Add(new ToolbarButton(OpenLargeFloating) { text = "큰 창으로 분리", tooltip = "도킹된 작은 창 대신 에디터 크기의 90% 독립 창으로 엽니다." });
            toolbar.Add(new ToolbarButton(() => {
                if (IsIntegrated && !EditorApplication.isPlayingOrWillChangePlaymode) EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            }) { text = "Integrated 씬 저장" });
            rootVisualElement.Add(toolbar);
            var search = new ToolbarSearchField { tooltip = "오브젝트 이름·표시 이름·ID 검색" };
            search.RegisterValueChangedCallback(e => { filter = e.newValue; RefreshList(); });
            toolbar.Add(search);
            // 목록 | 맵 | 오른쪽(표시 층·보기 설정·배치 / 상세). 경계선을 끌어 크기를 바꾼다. 맵은 남는 공간 전체를 쓴다.
            objects = CreatePalette();
            details = new ScrollView { style = { flexGrow = 1 } };
            var controls = new ScrollView { style = { flexGrow = 1 } };
            controls.Add(CreateMapControls());
            controls.Add(CreatePlacementControls());
            var side = new TwoPaneSplitView(0, 330, TwoPaneSplitViewOrientation.Vertical) { viewDataKey = "waiting-room-object-window-side", style = { minWidth = 220 } };
            side.Add(controls); side.Add(details);
            var right = new TwoPaneSplitView(1, 340, TwoPaneSplitViewOrientation.Horizontal) { viewDataKey = "waiting-room-object-window-right", style = { flexGrow = 1 } };
            right.Add(CreateMap()); right.Add(side);
            var body = new TwoPaneSplitView(0, 230, TwoPaneSplitViewOrientation.Horizontal) { viewDataKey = "waiting-room-object-window-left", style = { flexGrow = 1, flexShrink = 1, minHeight = 0 } };
            body.Add(objects); body.Add(right); rootVisualElement.Add(body);
            leftSplit = body; rightSplit = right;
            SelectionChanged();
        }
        void SelectionChanged()
        {
            var t = Selection.activeTransform;
            selected = IsIntegrated && t != null && t.gameObject.scene == SceneManager.GetActiveScene() ? InteractionRoot(t) : null;
            // 다른 층을 보고 있을 때 Hierarchy에서 고른 물체가 무시되어 옮길 수 없었다 → 그 물체의 층으로 바꿔 준다.
            if (selected != null && !OnSelectedFloor(selected) && floorHeights != null && floorHeights.Length > 0)
            { planeHeight = FloorForHeight(selected.position.y, floorHeights); allFloors = false; RefreshFloors(); }
            if (selected != null && !OnSelectedFloor(selected)) selected = null;
            if (existingPaletteSource != selected) existingPaletteSource = null;
            linked.Clear(); Refresh();
        }
        void Refresh() { RefreshList(); RefreshDetails(); RepaintMap(); }
        void RefreshList()
        {
            if (objects == null) return;
            MarkVisibilityDirty();
            objectRows.Clear();
            RefreshFloors();
            if (!IsIntegrated) { RefreshPalette(); return; }
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) {
                    if ((t.hideFlags & HideFlags.HideInHierarchy) != 0) continue;
                    if (!IsInteractionObject(t)) continue;
                    if (!CurrentLayoutTarget(t)) continue;
                    if (!OnSelectedFloor(t)) continue;
                    if (!string.IsNullOrEmpty(filter) && t.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0
                        && InteractionLabel(t).IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0
                        && !t.GetComponents<MonoBehaviour>().OfType<IExplorationTarget>().Any(c => c.TargetId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)) continue;
                    objectRows.Add(t);
                }
            RefreshPalette();
        }
        static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
        void RefreshDetails()
        {
            if (details == null) return;
            details.Clear();
            if (!IsInteractionObject(selected)) { selected = null; details.Add(new Label("상호작용 오브젝트만 선택·배치할 수 있습니다. 맵 구조물은 보호됩니다.")); return; }
            details.Add(new Label(Path(selected)));
            var position = new Vector3Field("배치 기준 위치") { value = PlacementPoint(selected) };
            position.RegisterValueChangedCallback(e => ApplyPlacement(e.newValue, selected.rotation)); details.Add(position);
            var rotation = new Vector3Field("월드 회전") { value = selected.eulerAngles };
            rotation.RegisterValueChangedCallback(e => ApplyPlacement(PlacementPoint(selected), Quaternion.Euler(e.newValue))); details.Add(rotation);
            if (ConditionalContent(selected)) details.Add(new HelpBox(ContentCondition(selected) + " · 이 위치는 해제 후 발견 위치입니다.", HelpBoxMessageType.Info));
            var inspected = selected.GetComponentsInChildren<MonoBehaviour>(true).Where(c => c != null).ToArray();
            details.Add(new Label($"기존 기능 컴포넌트 {inspected.Length}개 · 자식은 자동 동반 이동"));
            var refs = new HashSet<Transform>();
            foreach (var c in inspected) {
                var serialized = new SerializedObject(c);
                var prop = serialized.GetIterator();
                while (prop.Next(true)) {
                    if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var value = prop.objectReferenceValue;
                    var target = value is GameObject go ? go.transform : value is Component component ? component.transform : null;
                    if (target == null || target == selected || target.IsChildOf(selected) || selected.IsChildOf(target)
                        || target.gameObject.scene != selected.gameObject.scene || !IsPositionAnchor(target) || !refs.Add(target)) continue;
                    var anchor = target;
                    var toggle = new Toggle($"{c.GetType().Name}.{prop.propertyPath} → {Path(anchor)}") { value = linked.Contains(anchor) };
                    toggle.RegisterValueChangedCallback(e => { if (e.newValue) linked.Add(anchor); else linked.Remove(anchor); });
                    details.Add(toggle);
                }
            }
            details.Add(new HelpBox("상호작용·연결된 발견물·드롭 위치가 함께 이동합니다. 추가 위치 앵커만 아래에서 선택하세요. 기존 해제 조건과 획득 규칙은 유지됩니다.", HelpBoxMessageType.Info));
            DropControls();
            DeleteControl();
            var readOnly = new VisualElement();
            readOnly.Add(new InspectorElement(selected.gameObject));
            foreach (var component in selected.GetComponents<Component>().Where(c => c != null && c is not Transform))
                readOnly.Add(new InspectorElement(component));
            readOnly.SetEnabled(false); details.Add(readOnly);
            details.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode);
        }
        // 이미 배치된 기물·상자를 지운다. 방 프리팹 안에 든 것은 프리팹을 깨지 않도록 끄기만 한다(되돌리기 가능).
        void DeleteControl()
        {
            var target = selected; if (target == null) return;
            bool inPrefab = PrefabUtility.IsPartOfPrefabInstance(target);
            var button = new Button(() =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || target == null) return;
                if (!EditorUtility.DisplayDialog("오브젝트 배치", $"'{target.name}'을(를) {(inPrefab ? "끌까요? (방 프리팹 안의 물체라 지우지 않고 끕니다)" : "지울까요?")}", inPrefab ? "끄기" : "지우기", "취소")) return;
                var scene = target.gameObject.scene;
                DeletePlaced(target, OwnedCompanions(target).ToArray());
                selected = null; Selection.activeGameObject = null; EditorSceneManager.MarkSceneDirty(scene); Refresh();
            }) { text = inPrefab ? "이 오브젝트 끄기" : "이 오브젝트 지우기" };
            button.style.marginTop = 6; details.Add(button);
        }
        internal static void DeletePlaced(Transform target, Transform[] companions)
        {
            Undo.SetCurrentGroupName("배치 오브젝트 삭제");
            foreach (var t in new[] { target }.Concat(companions ?? new Transform[0]).Where(t => t != null).Distinct().ToArray())
            {
                if (t == null) continue;
                if (PrefabUtility.IsPartOfPrefabInstance(t)) { Undo.RecordObject(t.gameObject, "끄기"); t.gameObject.SetActive(false); PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject); }
                else Undo.DestroyObjectImmediate(t.gameObject);
            }
        }
        internal static Transform[] MoveRoots(Transform root, IEnumerable<Transform> references)
        {
            var all = new[] { root }.Concat(references).Where(t => t != null && t.gameObject.scene == root.gameObject.scene).Distinct().ToArray();
            return all.Where(t => !all.Any(other => other != t && t.IsChildOf(other))).ToArray();
        }
        void Apply(Vector3 position, Quaternion rotation)
        {
            if (!IsIntegrated || !IsInteractionObject(selected) || !OnSelectedFloor(selected) || selected.gameObject.scene != SceneManager.GetActiveScene() || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EnsureOwnedDropAnchors(selected);
            var companions = linked.Where(IsPositionAnchor).Concat(OwnedCompanions(selected))
                .Where(t => t != null && t.gameObject.scene == selected.gameObject.scene && !selected.IsChildOf(t));
            MovePlacementGroup(selected, companions, position, rotation);
            EditorSceneManager.MarkSceneDirty(selected.gameObject.scene);
            RepaintMap();
        }
    }
}
