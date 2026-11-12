using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace CurtainCall.WaitingRoom.Editor
{
    public sealed partial class WaitingRoomObjectWindow
    {
        string creationItemId;
        bool creationBox;
        bool HasCreation => creationBox || !string.IsNullOrEmpty(creationItemId);
        string CreationLabel => creationBox ? "낡은 상자" : ItemCatalog.Get(creationItemId)?.displayName ?? creationItemId;
        static string SpotItem(FoundItemSpot spot) => new SerializedObject(spot).FindProperty("itemId").stringValue;
        static string SpotCondition(FoundItemSpot spot) => new SerializedObject(spot).FindProperty("availableAfter").stringValue;
        static GameObject SpotArt(FoundItemSpot spot) => Resources.Load<GameObject>("ItemArt/Prefabs/" + SpotItem(spot));
        void ClearCreation() { creationItemId = null; creationBox = false; }
        static void SetField(Component c, string field, object value)
        {
            var so = new SerializedObject(c); var p = so.FindProperty(field);
            if (value is string s) p.stringValue = s;
            else if (value is int n) p.intValue = n;
            else if (value is bool b) p.boolValue = b;
            else if (value is UnityEngine.Object o) p.objectReferenceValue = o;
            so.ApplyModifiedProperties();
        }
        internal static int AvailableOffset(Scene scene)
        {
            var used = new HashSet<int>();
            foreach (var root in scene.GetRootGameObjects()) {
                foreach (var spot in root.GetComponentsInChildren<FoundItemSpot>(true)) used.Add(new SerializedObject(spot).FindProperty("uidOffset").intValue);
                foreach (var drops in root.GetComponentsInChildren<ExplorationDrops>(true))
                    foreach (var entry in drops.entries ?? Array.Empty<ExplorationDrops.Entry>()) if (entry != null) used.Add(entry.uidOffset);
                foreach (var target in root.GetComponentsInChildren<MonoBehaviour>(true).OfType<IExplorationTarget>())
                    if (!string.IsNullOrEmpty(target.RewardItemId)) used.Add(target.RewardUidOffset);
            }
            return Enumerable.Range(1, 99).FirstOrDefault(i => !used.Contains(i));
        }
        internal GameObject CreateCatalogObject(Scene scene)
        {
            int offset = creationBox ? 0 : AvailableOffset(scene);
            if (!creationBox && offset == 0) { status.text = "발견품 번호 1~99가 모두 사용 중이라 추가할 수 없습니다."; return null; }
            var go = new GameObject(CreationLabel);
            SceneManager.MoveGameObjectToScene(go, scene);
            Undo.RegisterCreatedObjectUndo(go, "상호작용 기물 추가");
            string id = "placement-" + Guid.NewGuid().ToString("N");
            if (!creationBox) {
                var spot = Undo.AddComponent<FoundItemSpot>(go);
                SetField(spot, "spotId", id); SetField(spot, "uidOffset", offset);
                SetField(spot, "itemId", creationItemId); SetField(spot, "itemName", CreationLabel);
                SetField(spot, "debugOnly", false);
            } else {
                var sample = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ExplorationGate>(true))
                    .FirstOrDefault(g => IsInteractionObject(g.transform) && new SerializedObject(g).FindProperty("kind").enumValueIndex == (int)ExplorationGateKind.Breakable);
                if (sample != null) CopyMeshVisual(sample.transform, go.transform, false);
                else {
                    var visual = GameObject.CreatePrimitive(PrimitiveType.Cube); Undo.RegisterCreatedObjectUndo(visual, "상자 외형");
                    visual.transform.SetParent(go.transform, false); visual.transform.localPosition = Vector3.up * .375f;
                    visual.transform.localScale = new Vector3(.85f, .75f, .75f);
                    DestroyImmediate(visual.GetComponent<Collider>());
                }
                var collider = Undo.AddComponent<BoxCollider>(go); collider.center = Vector3.up * .375f; collider.size = new Vector3(.85f, .75f, .75f);
                var gate = Undo.AddComponent<ExplorationGate>(go);
                SetField(gate, "targetId", id); SetField(gate, "label", "낡은 상자");
                SetField(gate, "kind", (int)ExplorationGateKind.Breakable); SetField(gate, "toolId", ItemUses.StakePuller);
                SetField(gate, "requiredHits", 1); SetField(gate, "blocker", collider);
                var so = new SerializedObject(gate); var hidden = so.FindProperty("hideOnOpen"); hidden.arraySize = go.transform.childCount;
                for (int i = 0; i < hidden.arraySize; i++) hidden.GetArrayElementAtIndex(i).objectReferenceValue = go.transform.GetChild(i).gameObject;
                so.ApplyModifiedProperties();
                var loot = Undo.AddComponent<ExplorationDrops>(go);
                int hairpin = RequireOffset(scene);
                loot.entries = new[] { new ExplorationDrops.Entry { itemId = ItemUses.MagicianHairpin, uidOffset = hairpin, chancePercent = 15 } };
                loot.ConfigureOldBoxLoot(hairpin, RequireOffset(scene));
            }
            return go;
        }
        static void CopyMeshVisual(Transform source, Transform parentRoot, bool temporary)
        {
            foreach (var renderer in source.GetComponentsInChildren<MeshRenderer>(true)) {
                var filter = renderer.GetComponent<MeshFilter>(); if (filter == null || filter.sharedMesh == null) continue;
                var part = new GameObject(renderer.name);
                if (temporary) part.hideFlags = HideFlags.HideAndDontSave;
                part.transform.SetParent(parentRoot, false);
                var local = source.worldToLocalMatrix * renderer.localToWorldMatrix;
                part.transform.localPosition = local.GetColumn(3); part.transform.localRotation = local.rotation; part.transform.localScale = local.lossyScale;
                part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                part.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
                if (!temporary) Undo.RegisterCreatedObjectUndo(part, "상자 외형 추가");
            }
        }
        internal void PrepareNewPrefab(GameObject instance)
        {
            var components = instance.GetComponentsInChildren<MonoBehaviour>(true).Where(c => c != null).ToArray();
            var ids = new Dictionary<string, string>();
            foreach (var component in components.OfType<IExplorationTarget>())
                if (!string.IsNullOrEmpty(component.TargetId)) {
                    if (ids.ContainsKey(component.TargetId)) throw new InvalidOperationException("프리팹 내부의 상호작용 ID가 중복되어 있습니다.");
                    string role = component.TargetId.ToLowerInvariant().Contains("box") ? "box-" : component.TargetId.ToLowerInvariant().Contains("cabinet") ? "cabinet-" : "";
                    ids[component.TargetId] = "placement-" + role + Guid.NewGuid().ToString("N");
                }
            foreach (var component in components) {
                var so = new SerializedObject(component); var p = so.GetIterator();
                while (p.Next(true)) if (p.propertyType == SerializedPropertyType.String && ids.TryGetValue(p.stringValue, out var replacement)) p.stringValue = replacement;
                so.ApplyModifiedProperties();
                if (component is FoundItemSpot) {
                    SetField(component, "spotId", "placement-" + Guid.NewGuid().ToString("N"));
                    SetField(component, "uidOffset", RequireOffset(instance.scene));
                }
                if (component is ExplorationDrops drops) {
                    foreach (var entry in drops.entries ?? Array.Empty<ExplorationDrops.Entry>()) if (entry != null) entry.uidOffset = RequireOffset(instance.scene);
                    EditorUtility.SetDirty(drops);
                } else if (component is IExplorationTarget target && !string.IsNullOrEmpty(target.RewardItemId)) SetField(component, "rewardUidOffset", RequireOffset(instance.scene));
                EnsureOwnedDropAnchors(component.transform);
            }
        }
        static int RequireOffset(Scene scene)
        {
            int offset = AvailableOffset(scene);
            if (offset == 0) throw new InvalidOperationException("발견품 번호 1~99가 모두 사용 중입니다.");
            return offset;
        }
        void AddCreationPalette(VisualElement host)
        {
            host.Add(new Label("새 기물 추가"));
            var grid = CardGrid(); host.Add(grid);
            foreach (var item in ItemCatalog.Current.Items.Where(e => !ExplorationKeys.IsKey(e.id))) {
                var entry = item; var art = Resources.Load<GameObject>("ItemArt/Prefabs/" + entry.id);
                if (!string.IsNullOrEmpty(filter) && entry.displayName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 && entry.id.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (art != null) AddCreationCard(grid, entry.displayName, art, () => { creationItemId = entry.id; creationBox = false; });
            }
            var box = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ExplorationGate>(true))
                .FirstOrDefault(g => CurrentLayoutTarget(g.transform) && new SerializedObject(g).FindProperty("kind").enumValueIndex == (int)ExplorationGateKind.Breakable);
            if (string.IsNullOrEmpty(filter) || "낡은 상자 파괴 가능한 상자".IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                AddCreationCard(grid, "낡은 상자", box != null ? box.gameObject : null, () => { creationItemId = null; creationBox = true; });
        }
        void AddCreationCard(VisualElement grid, string label, GameObject art, Action choose)
        {
            void Select()
            {
                EndMapDrag(); existingPaletteSource = null; prefab = null; placing = true; choose();
                status.text = label + " · 맵에서 Alt+드래그 또는 카드를 맵에 끌어 놓아 새로 추가"; RepaintMap();
            }
            var button = new Button(Select) { style = { width = 100, height = 132 } };
            MakeCardDraggable(button, Select, label);
            var picture = new Image { scaleMode = ScaleMode.ScaleToFit, style = { width = 94, height = 90 } };
            if (art != null) {
                if (thumbnails.TryGetValue(art.GetInstanceID(), out var cached)) picture.image = cached;
                else thumbnailQueue.Enqueue((art, picture));
            }
            button.Add(picture); button.Add(new Label(label) { style = { whiteSpace = WhiteSpace.Normal, fontSize = 11 } }); grid.Add(button);
        }
        void DropControls()
        {
            var lottery = selected.GetComponent<ExplorationDrops>();
            if (lottery != null && lottery.exclusiveWeighted) {
                details.Add(new HelpBox($"낡은 상자 · 머리핀 15% / 나머지는 공연 토큰({CurtainCall.Settings.GameSettings.WaitingRoom.ExplorationTokenFunds}원), 빈 상자 없음. 토큰은 F로 주우면 공동 자금에 들어갑니다.", HelpBoxMessageType.Info));
                return;
            }
            if (selected.TryGetComponent<FoundItemSpot>(out var found)) {
                var catalogItems = ItemCatalog.Current.Items.Where(e => !ExplorationKeys.IsKey(e.id)).ToArray();
                var itemMenu = new DropdownField("발견 기물", catalogItems.Select(e => e.displayName).ToList(), Mathf.Max(0, Array.FindIndex(catalogItems, e => e.id == SpotItem(found))));
                itemMenu.RegisterValueChangedCallback(e => { SetField(found, "itemId", catalogItems[itemMenu.index].id); SetField(found, "itemName", catalogItems[itemMenu.index].displayName); CleanupSpotPreviews(); CleanupPalette(); Refresh(); }); details.Add(itemMenu);
                var owners = selected.gameObject.scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).OfType<IExplorationTarget>().ToArray();
                var condition = new UnityEditor.UIElements.ObjectField("생성 조건 기물") { objectType = typeof(GameObject), allowSceneObjects = true,
                    value = (owners.FirstOrDefault(o => o.TargetId == SpotCondition(found)) as Component)?.gameObject };
                condition.RegisterValueChangedCallback(e => {
                    var go = e.newValue as GameObject; var owner = go != null ? go.GetComponents<MonoBehaviour>().OfType<IExplorationTarget>().FirstOrDefault() : null;
                    if (go != null && (owner == null || go.scene != selected.gameObject.scene)) { status.text = "같은 씬의 상호작용 기물을 선택하세요."; return; }
                    Undo.RecordObject(found, "발견 조건 연결"); found.UnlockAfter(owner?.TargetId ?? ""); EditorUtility.SetDirty(found); RefreshDetails();
                }); details.Add(condition);
                return;
            }
            var owner = selected.GetComponents<MonoBehaviour>().OfType<IExplorationTarget>().FirstOrDefault();
            if (owner == null || owner is UvMark || owner is ClueInspectable) return;
            details.Add(new Button(() => {
                int offset = AvailableOffset(selected.gameObject.scene);
                if (offset == 0) { status.text = "빈 발견품 번호가 없습니다."; return; }
                Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
                var drops = selected.GetComponent<ExplorationDrops>();
                if (drops == null) {
                    drops = Undo.AddComponent<ExplorationDrops>(selected.gameObject);
                    if (!string.IsNullOrEmpty(owner.RewardItemId)) {
                        var original = new GameObject("OriginalDropPoint"); original.transform.SetParent(selected, false); original.transform.position = owner.RewardPoint; Undo.RegisterCreatedObjectUndo(original, "기존 드롭 위치 유지");
                        drops.TryConfigure(new[] { new ExplorationDrops.Entry { itemId = owner.RewardItemId, uidOffset = owner.RewardUidOffset, point = original.transform } }, out _);
                    }
                }
                var point = new GameObject("DropPoint"); point.transform.SetParent(selected, false);
                point.transform.position = owner.RewardPoint; Undo.RegisterCreatedObjectUndo(point, "드롭 위치 추가");
                Undo.RecordObject(drops, "드롭 아이템 추가");
                drops.TryConfigure((drops.entries ?? Array.Empty<ExplorationDrops.Entry>()).Concat(new[] { new ExplorationDrops.Entry { itemId = ItemUses.MagicianHairpin, uidOffset = offset, point = point.transform } }).ToArray(), out _);
                EditorUtility.SetDirty(drops); Undo.CollapseUndoOperations(group); RefreshDetails();
            }) { text = "드롭 아이템 추가" });
            var current = selected.GetComponent<ExplorationDrops>();
            if (current == null) return;
            var entries = current.entries ?? Array.Empty<ExplorationDrops.Entry>();
            var catalog = ItemCatalog.Current.Items.ToArray();
            for (int i = 0; i < entries.Length; i++) {
                var entry = entries[i]; if (entry == null) continue;
                var menu = new DropdownField("드롭 기물", catalog.Select(e => e.displayName).ToList(), Mathf.Max(0, Array.FindIndex(catalog, e => e.id == entry.itemId)));
                menu.RegisterValueChangedCallback(e => { Undo.RecordObject(current, "드롭 기물 변경"); entry.itemId = catalog[menu.index].id; EditorUtility.SetDirty(current); }); details.Add(menu);
                if (entry.point != null) {
                    var dropOwner = selected;
                    var position = new Vector3Field("드롭 위치 (기물 기준)") { value = dropOwner.InverseTransformPoint(entry.point.position) };
                    position.RegisterValueChangedCallback(e => {
                        if (!IsIntegrated || dropOwner == null || dropOwner.gameObject.scene != SceneManager.GetActiveScene() || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;
                        EnsureOwnedDropAnchors(dropOwner); var point = entry.point;
                        Undo.RecordObject(point, "드롭 위치 변경"); point.position = dropOwner.TransformPoint(e.newValue); PrefabUtility.RecordPrefabInstancePropertyModifications(point);
                    }); details.Add(position);
                }
            }
        }
    }
}
