using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace CurtainCall.WaitingRoom.Editor
{
    // 장식물/상호작용 오브젝트 분류와 에디터 전용 일괄 숨김. 씬 파일·활성 상태는 바꾸지 않는다.
    public sealed partial class WaitingRoomObjectWindow
    {
        internal enum SceneObjectKind { Interaction, Decoration, Facility, Structure }

        // 장식물 = 없어도 되는 환경·분위기 물체만. 이름 키워드가 있는 것만 장식물로 보고, 모르는 물체는 항상 표시(필수 시설·기타)한다.
        // 판정은 자기 자신부터 부모 쪽으로 올라가며 처음 걸린 이름 조각으로 정한다. 대소문자를 구분한다(Forklift ≠ Lift).
        // 같은 이름 안에서는 조명·배관·녹 → 필수 시설 → 구조물 → 일반 장식 순으로 본다.
        // 이 이름 아래는 통째로 필수 시설(조명·부속 포함): 연습 기구, 무대 입구 통로, 승강기, 화장실 설비, 숙소 가구, 상점·관리 설비.
        static readonly string[] LockedFacilityKeys = {
            "PracticeLanes", "HumanCannon", "Trampoline", "CollisionRepairs", "StageProcessionalRoute", "FreightLift", "Fixtures",
            "Wardrobe", "Locker", "Bed_", "Phone", "DeliverySlot", "HandoverShelf", "Schedule", "Shutter", "Cctv", "MonitorWall", "Console", "Gate"
        };
        static readonly string[] PriorityDecorationKeys = { "Lamp", "Bulb", "Glow", "Pipe", "Rust", "Stain", "Hazard", "Pennant", "Catenary", "Dressing", "Clutter", "Rubble", "Debris" };
        static readonly string[] FacilityKeys = {
            "Practice", "RoundArena", "LegacyRing", "RaisedRingAccess", "Bed", "Tent", "GuyRope", "Lift", "Elevator",
            "StageCorridor", "StageEntrance", "StageProcessional", "StageCurtain", "CurtainRoom", "VelvetCurtain",
            "Shop", "Phone", "DeliverySlot", "HandoverShelf", "Schedule", "Shutter", "Gate", "Cctv", "MonitorWall", "Console",
            "Restroom", "Fixtures", "Locker", "Wardrobe", "Expert", "GatherZone", "Spawn", "Sign", "sign", "Label"
        };
        static readonly string[] StructurePrefixes = {
            "Floor", "Wall", "W_", "Ceiling", "Roof", "Stair", "Step", "Landing", "Door", "Pillar", "Column", "Beam", "Girder", "Shaft"
        };
        static readonly string[] StructureKeys = {
            "Shell", "WallStructure", "Wall", "wall", "Floor", "floor", "Ceiling", "Roof", "Stair", "Tread", "Stringer", "Rail", "Deck", "Walkway",
            "Catwalk", "Mezzanine", "Landing", "Passage", "Dome", "Rib", "CrownRing", "RingBeam", "LedgerBand", "Pilaster", "Column", "Beam",
            "Upright", "Brace", "Doorway", "door", "Hinge", "Transom", "FloorModule", "Gallery", "Footing", "Joist", "Partition", "Corridor",
            "jamb", "lintel", "Enclosure"
        };
        static readonly string[] DecorationKeys = {
            "Mood", "Chunk", "Dust", "Crack", "Wear", "Wet", "Puddle", "Halo", "Runoff", "Bleed", "Flag", "ClothHeader", "AnchorEye",
            "SuspensionTie", "Spark", "Fog", "Paper", "Portrait", "Journal", "Poster", "Barrel", "Crate", "Pallet", "Spool", "Cablespool",
            "Trunk", "Gascylender", "Cylinder", "Lumber", "HangingTool", "Costume", "Mascot", "Rope", "Wagon", "Ladder", "Scaffold", "Carpet",
            "Rug", "Container", "Forklift", "Stock", "Flange", "Hanger", "Clamp", "Collar", "Valve", "Gauge", "Meter", "Riser", "JunctionBox",
            "Breaker", "Cabinet", "Shelf", "Shelving", "Equipment", "Rigging", "Scenery", "Backdrop", "Trapeze", "Hoop", "Tightrope", "Winch",
            "Wheel", "CurtainRack", "Machinery", "Plant", "Drain", "Boiler", "Abandoned", "Derelict", "Salvage", "Retired", "Display", "Bench",
            "Desk", "Storage", "Clothes", "Bear", "GiftBox", "Juggling", "CircusBall", "Drum", "Lighting", "LivedIn", "Living", "Overhead",
            "InteriorArt", "Furnishings", "Ceilingworks", "Weapon", "Rack", "Records", "Prop", "Dolls"
        };
        static readonly HashSet<string> StructureGroups = new(StringComparer.Ordinal) { "Walls", "Floors", "Ceilings", "Stairs", "Doors" };

        bool hideDecorations, hideInteractions;
        bool visibilityDirty = true;
        readonly List<GameObject> interactionRoots = new();
        readonly List<GameObject> decorationObjects = new();
        readonly HashSet<Renderer> interactionRenderers = new();
        readonly HashSet<Renderer> decorationRenderers = new();
        Toggle decorationToggle, interactionToggle;
        Label visibilityCount;

        internal static SceneObjectKind Classify(Transform t)
        {
            if (InteractionRoot(t) != null) return SceneObjectKind.Interaction;
            return ClassifyNonInteraction(t);
        }

        static SceneObjectKind ClassifyNonInteraction(Transform t)
        {
            if (IsStructureComponent(t)) return SceneObjectKind.Structure;
            for (var p = t; p != null; p = p.parent)
                if (LockedFacilityKeys.Any(k => p.name.IndexOf(k, StringComparison.Ordinal) >= 0)) return SceneObjectKind.Facility;
            for (var p = t; p != null; p = p.parent) {
                var kind = NameKind(p.name);
                if (kind.HasValue) return kind.Value;
            }
            return SceneObjectKind.Facility;
        }

        internal static SceneObjectKind? NameKind(string name)
        {
            static bool Has(string n, string[] keys) => keys.Any(k => n.IndexOf(k, StringComparison.Ordinal) >= 0);
            if (Has(name, PriorityDecorationKeys)) return SceneObjectKind.Decoration;
            if (Has(name, FacilityKeys)) return SceneObjectKind.Facility;
            if (StructureGroups.Contains(name) || CurtainCall.Cameras.Editor.WaitingRoomCameraCollisionAuthoring.StructureNames.Contains(name)
                || StructurePrefixes.Any(k => name.StartsWith(k, StringComparison.OrdinalIgnoreCase)) || Has(name, StructureKeys)) return SceneObjectKind.Structure;
            if (Has(name, DecorationKeys)) return SceneObjectKind.Decoration;
            return null;
        }

        // 카메라 충돌 구조물, 문·실제 숨은 길 벽(상호작용 기물이 아닌 ExplorationGate/BreakableWall).
        static bool IsStructureComponent(Transform t)
        {
            if (t == null) return false;
            if (t.GetComponent<CurtainCall.Cameras.CameraCollisionSurface>() != null) return true;
            for (var p = t; p != null; p = p.parent)
                if ((p.TryGetComponent<BreakableWall>(out _) || p.TryGetComponent<ExplorationGate>(out _)) && !IsInteractionObject(p)) return true;
            return false;
        }

        VisualElement CreateVisibilityControls()
        {
            var box = new VisualElement { style = { flexShrink = 0, marginTop = 4, marginBottom = 4 } };
            box.Add(new Label("에디터 표시 (씬 파일·게임에는 영향 없음)") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            decorationToggle = new Toggle("장식물 끄기") { value = hideDecorations };
            decorationToggle.RegisterValueChangedCallback(e => { hideDecorations = e.newValue; ApplyVisibility(); });
            interactionToggle = new Toggle("상호작용 오브젝트 끄기") { value = hideInteractions };
            interactionToggle.RegisterValueChangedCallback(e => { hideInteractions = e.newValue; ApplyVisibility(); });
            row.Add(decorationToggle); row.Add(interactionToggle);
            row.Add(new Button(() => SelectKind(SceneObjectKind.Decoration)) { text = "장식물 선택" });
            row.Add(new Button(() => SelectKind(SceneObjectKind.Interaction)) { text = "상호작용 선택" });
            box.Add(row);
            visibilityCount = new Label();
            box.Add(visibilityCount);
            SyncVisibilityToggles();
            return box;
        }

        void MarkVisibilityDirty() => visibilityDirty = true;

        void RebuildVisibilitySets()
        {
            if (!visibilityDirty) return;
            visibilityDirty = false;
            interactionRoots.Clear(); decorationObjects.Clear(); interactionRenderers.Clear(); decorationRenderers.Clear();
            var scene = SceneManager.GetActiveScene();
            if (!IsIntegrated || !scene.IsValid() || !scene.isLoaded) { UpdateVisibilityCount(); return; }
            var roots = new HashSet<Transform>();
            var isInteraction = new Dictionary<Transform, bool>();
            bool Interactive(Transform t) { if (!isInteraction.TryGetValue(t, out var v)) isInteraction[t] = v = IsInteractionObject(t); return v; }
            Transform Outermost(Transform t) { Transform found = null; for (; t != null; t = t.parent) if (Interactive(t)) found = t; return found; }
            int structures = 0, facilities = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var r in root.GetComponentsInChildren<Renderer>(true)) {
                    if (r == null || (r.gameObject.hideFlags & (HideFlags.HideInHierarchy | HideFlags.DontSave)) != 0) continue;
                    var interaction = Outermost(r.transform);
                    if (interaction != null) { interactionRenderers.Add(r); roots.Add(interaction); }
                    else switch (ClassifyNonInteraction(r.transform)) {
                        case SceneObjectKind.Decoration: decorationRenderers.Add(r); decorationObjects.Add(r.gameObject); break;
                        case SceneObjectKind.Structure: structures++; break;
                        default: facilities++; break;
                    }
                }
            interactionRoots.AddRange(roots.Select(t => t.gameObject));
            UpdateVisibilityCount(structures, facilities);
        }

        void UpdateVisibilityCount(int structures = 0, int facilities = 0)
        {
            if (visibilityCount == null) return;
            visibilityCount.text = IsIntegrated
                ? $"장식물 렌더러 {decorationRenderers.Count}개 · 상호작용 오브젝트 {interactionRoots.Count}개 (렌더러 {interactionRenderers.Count}) · 필수 시설·기타 {facilities}개, 구조물 {structures}개는 항상 표시"
                : "Integrated 대기실에서만 사용할 수 있습니다.";
        }

        // 씬 뷰는 SceneVisibilityManager(에디터 전용), 창 내부 맵은 렌더 시 임시 마스킹으로 숨긴다.
        void ApplyVisibility()
        {
            visibilityDirty = true; RebuildVisibilitySets();
            if (!IsIntegrated || EditorApplication.isPlayingOrWillChangePlaymode) { RepaintMap(); return; }
            var visibility = SceneVisibilityManager.instance;
            var interactions = interactionRoots.Where(g => g != null).ToArray();
            var decorations = decorationObjects.Where(g => g != null).ToArray();
            if (hideInteractions) visibility.Hide(interactions, true); else visibility.Show(interactions, true);
            if (hideDecorations) visibility.Hide(decorations, false); else visibility.Show(decorations, false);
            if (hideInteractions && selected != null) { selected = null; linked.Clear(); RefreshDetails(); }
            SceneView.RepaintAll(); RepaintMap();
        }

        void SyncVisibilityToggles()
        {
            visibilityDirty = true; RebuildVisibilitySets();
            var visibility = SceneVisibilityManager.instance;
            hideDecorations = decorationObjects.Count > 0 && decorationObjects.All(g => g != null && visibility.IsHidden(g));
            hideInteractions = interactionRoots.Count > 0 && interactionRoots.All(g => g != null && visibility.IsHidden(g));
            decorationToggle?.SetValueWithoutNotify(hideDecorations);
            interactionToggle?.SetValueWithoutNotify(hideInteractions);
        }

        void SelectKind(SceneObjectKind kind)
        {
            RebuildVisibilitySets();
            var targets = kind == SceneObjectKind.Interaction ? interactionRoots : decorationObjects;
            Selection.objects = targets.Where(g => g != null).Cast<UnityEngine.Object>().ToArray();
        }

        void MaskHiddenRenderers(Dictionary<Renderer, bool> masked)
        {
            if (!hideInteractions && !hideDecorations) return;
            RebuildVisibilitySets();
            if (hideInteractions) foreach (var r in interactionRenderers) Mask(r);
            if (hideDecorations) foreach (var r in decorationRenderers) Mask(r);
            void Mask(Renderer r) { if (r != null && !masked.ContainsKey(r)) { masked[r] = r.forceRenderingOff; r.forceRenderingOff = true; } }
        }
    }
}
