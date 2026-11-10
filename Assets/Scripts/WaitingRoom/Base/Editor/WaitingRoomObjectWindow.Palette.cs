using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CurtainCall.WaitingRoom.Editor
{
    public sealed partial class WaitingRoomObjectWindow
    {
        const string CardDragKey = "WaitingRoomObjectCard";
        Transform existingPaletteSource;
        readonly List<GameObject> palettePrefabs = new();
        readonly Dictionary<int, Texture2D> thumbnails = new();
        readonly Queue<(GameObject source, Image image)> thumbnailQueue = new();
        bool catalogLoaded;
        int lastPaletteClickId;
        double lastPaletteClickTime;

        ScrollView CreatePalette()
        {
            var view = new ScrollView { style = { flexGrow = 1, minWidth = 120 } };
            view.schedule.Execute(UpdateThumbnail).Every(80);
            return view;
        }
        void LoadPaletteCatalog()
        {
            if (catalogLoaded) return;
            catalogLoaded = true; palettePrefabs.Clear();
            var folders = new[] { "Assets/Prefabs", "Assets/Resources", "Assets/Scenes" }.Where(AssetDatabase.IsValidFolder).ToArray();
            if (folders.Length > 0)
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", folders)) {
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                    if (InteractionPrefab(asset)) palettePrefabs.Add(asset);
                }
            // Also include connected prefab sources even if they live outside the usual folders.
            foreach (var t in objectRows) {
                var asset = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                if (InteractionPrefab(asset) && !palettePrefabs.Contains(asset)) palettePrefabs.Add(asset);
            }
        }
        void RefreshPalette()
        {
            if (objects == null) return;
            objects.Clear(); thumbnailQueue.Clear();
            if (!IsIntegrated) { objects.Add(new Label("Integrated 대기실을 열어 주세요.")); return; }
            LoadPaletteCatalog();
            AddCreationPalette(objects);
            objects.Add(new Label($"현재 층 상호작용 · {objectRows.Count}개"));
            objects.Add(new Label("카드 선택 → 맵에서 Alt+드래그, 또는 카드를 맵에 끌어 놓기 · 더블클릭 → 맵·씬 뷰 위치 보기") { style = { whiteSpace = WhiteSpace.Normal } });
            var existing = CardGrid(); objects.Add(existing);
            foreach (var t in objectRows) AddCard(existing, t.gameObject, false);
            objects.Add(new Label("새로 추가 가능한 프리팹"));
            var assets = CardGrid(); objects.Add(assets);
            var matches = palettePrefabs.Where(p => p != null && (string.IsNullOrEmpty(filter) || p.name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0)).OrderBy(p => p.name).ToArray();
            foreach (var asset in matches) AddCard(assets, asset, true);
            if (matches.Length == 0) objects.Add(new Label("추가 가능한 기능 프리팹 없음 · 위 카드는 기존 대상 재배치용입니다.") { style = { whiteSpace = WhiteSpace.Normal } });
        }
        static VisualElement CardGrid() => new() { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
        void AddCard(VisualElement grid, GameObject source, bool isPrefab)
        {
            var button = new Button(() => {
                bool doubleClick = lastPaletteClickId == source.GetInstanceID() && EditorApplication.timeSinceStartup - lastPaletteClickTime < .4;
                lastPaletteClickId = source.GetInstanceID(); lastPaletteClickTime = EditorApplication.timeSinceStartup;
                if (doubleClick && !isPrefab) { EndMapDrag(); ClearCreation(); FocusObject(source.transform); return; }
                ChooseCard(source, isPrefab);
            }) { tooltip = isPrefab ? AssetDatabase.GetAssetPath(source) : Path(source.transform), style = { width = 100, height = 132, paddingLeft = 2, paddingRight = 2 } };
            var picture = new Image { scaleMode = ScaleMode.ScaleToFit, style = { width = 94, height = 90 } };
            if (thumbnails.TryGetValue(source.GetInstanceID(), out var cached)) picture.image = cached;
            else thumbnailQueue.Enqueue((source, picture));
            button.Add(picture);
            button.Add(new Label((isPrefab ? source.name : InteractionLabel(source.transform)) + (!isPrefab && ConditionalContent(source.transform) ? " (개방·발견 후)" : "")) { style = { whiteSpace = WhiteSpace.Normal, fontSize = 11 } });
            if ((isPrefab && placing && prefab == source) || (!isPrefab && existingPaletteSource == source.transform)) {
                button.style.borderTopColor = button.style.borderBottomColor = button.style.borderLeftColor = button.style.borderRightColor = Color.cyan;
                button.style.borderTopWidth = button.style.borderBottomWidth = button.style.borderLeftWidth = button.style.borderRightWidth = 2;
            }
            MakeCardDraggable(button, () => ChooseCard(source, isPrefab), isPrefab ? source.name : InteractionLabel(source.transform));
            grid.Add(button);
        }
        void ChooseCard(GameObject source, bool isPrefab)
        {
            EndMapDrag(); ClearCreation();
            if (isPrefab) { existingPaletteSource = null; prefab = source; placing = true; }
            else { Selection.activeGameObject = source; existingPaletteSource = source.transform; prefab = null; placing = false; }
            status.text = isPrefab ? $"{source.name} · 맵에서 Alt+드래그 또는 카드를 맵에 끌어 놓아 새로 배치" : $"{source.name} · 맵에서 Alt+드래그 또는 카드를 맵에 끌어 놓아 재배치 (복제 안 함)";
            RefreshPalette(); RepaintMap();
        }
        // 카드를 맵으로 끌어다 놓아도 배치된다. 기본 Button은 Alt가 눌린 클릭을 무시하므로 Alt 클릭도 받게 한다.
        void MakeCardDraggable(Button button, Action select, string label)
        {
            button.clickable.activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse, modifiers = EventModifiers.Alt });
            Vector2? press = null;
            button.RegisterCallback<PointerDownEvent>(e => { if (e.button == 0) press = e.position; }, TrickleDown.TrickleDown);
            button.RegisterCallback<PointerUpEvent>(_ => press = null, TrickleDown.TrickleDown);
            button.RegisterCallback<PointerMoveEvent>(e => {
                if (press == null || ((Vector2)e.position - press.Value).sqrMagnitude < 36) return;
                press = null;
                if (button.HasPointerCapture(e.pointerId)) button.ReleasePointer(e.pointerId);
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = Array.Empty<UnityEngine.Object>();
                DragAndDrop.SetGenericData(CardDragKey, true);
                DragAndDrop.StartDrag(label);
                select();
            });
        }
        void UpdateThumbnail()
        {
            if (thumbnailQueue.Count == 0 || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var item = thumbnailQueue.Dequeue();
            if (item.source == null || item.image.panel == null) return;
            var texture = MakeThumbnail(item.source);
            thumbnails[item.source.GetInstanceID()] = texture;
            item.image.image = texture != null ? texture : AssetPreview.GetMiniThumbnail(item.source);
        }
        static Texture2D MakeThumbnail(GameObject source)
        {
            if (source.TryGetComponent<FoundItemSpot>(out var spot)) {
                var art = SpotArt(spot); if (art != null) source = art;
            }
            var presentation = source.GetComponents<MonoBehaviour>().OfType<IShopPresentation>().FirstOrDefault();
            var visual = presentation?.VisualRoot;
            var renderers = (visual != null ? visual.gameObject : source).GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return null;
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            var preview = new PreviewRenderUtility();
            var baked = new List<Mesh>();
            try {
                preview.BeginStaticPreview(new Rect(0, 0, 128, 112));
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = new Color(.18f, .19f, .21f);
                preview.camera.orthographic = true;
                float size = Mathf.Max(.15f, bounds.extents.magnitude);
                preview.camera.orthographicSize = size * 1.1f;
                preview.camera.nearClipPlane = .01f; preview.camera.farClipPlane = size * 10 + 10;
                preview.camera.transform.position = new Vector3(1, .75f, -1).normalized * (size * 3 + 1);
                preview.camera.transform.LookAt(Vector3.zero);
                preview.lights[0].intensity = 3f; preview.lights[0].transform.rotation = Quaternion.Euler(35, 25, 0);
                preview.lights[1].intensity = 1.8f;
                preview.ambientColor = new Color(.65f, .65f, .65f);
                foreach (var renderer in renderers) {
                    Mesh mesh = null;
                    if (renderer is SkinnedMeshRenderer skinned) { mesh = new Mesh(); skinned.BakeMesh(mesh); baked.Add(mesh); }
                    else { var filter = renderer.GetComponent<MeshFilter>(); if (filter != null) mesh = filter.sharedMesh; }
                    if (mesh == null) continue;
                    var materials = renderer.sharedMaterials;
                    var matrix = Matrix4x4.Translate(-bounds.center) * renderer.localToWorldMatrix;
                    for (int i = 0; i < mesh.subMeshCount && i < materials.Length; i++)
                        if (materials[i] != null) preview.DrawMesh(mesh, matrix, materials[i], i);
                }
                preview.Render(true);
                return preview.EndStaticPreview();
            }
            finally { foreach (var mesh in baked) DestroyImmediate(mesh); preview.Cleanup(); }
        }
        void CleanupPalette()
        {
            thumbnailQueue.Clear();
            foreach (var texture in thumbnails.Values) if (texture != null) DestroyImmediate(texture);
            thumbnails.Clear(); catalogLoaded = false;
        }
    }
}
