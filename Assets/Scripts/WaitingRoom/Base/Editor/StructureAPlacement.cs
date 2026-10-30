using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.WaitingRoom;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.Editor
{
    /// <summary>2026-10-30 사용자 배치도/목록. 실제 LayoutA 좌표로 배치하며 방 셸과 기존 에셋은 재생성하지 않는다.</summary>
    public static class StructureAPlacement
    {
        const string ScenePath = "Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity";
        const string Marker = "StructureAExploration";
        static Transform layout, registry;
        static Material metal, wood, paper, gold;
        static readonly Dictionary<string, Component> refs = new();
        static readonly List<string> manifest = new();
        static readonly Dictionary<string, Transform> groups = new();
        const string Report = "Tasks/Active/Task-20261029-079-waiting-room-m2/stage-07-placement-manifest.md";

        [MenuItem("CurtainCall/Waiting Room/Install Structure A Exploration")]
        public static void MenuInstall() => Debug.Log(Install());
        public static string Install()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != ScenePath || scene.isDirty)
                throw new InvalidOperationException("WaitingRoomBase must be open, stopped and saved before installing.");
            layout = scene.GetRootGameObjects().Single(g => g.name == "WaitingRoom_LayoutA").transform;
            if (layout.Find(Marker) != null) return "Already installed; existing placement was preserved.";
            foreach (var name in new[] { "S02_Dormitory/Restroom", "S10_Workshop", "S05_JunkStorage", "P05_PipeZone", "S12_CargoBay", "S11_StageGear", "LG_Lounge", "ST_Stairwell", "B1_Office", "B1_MachineRoom", "B1_EscapeEntrance" })
                if (layout.Find(name) == null) throw new InvalidOperationException("Missing room: " + name);
            var oldSpots = layout.GetComponentsInChildren<FoundItemSpot>(true);
            var crowbar = oldSpots.Single(s => s.MakeItem(1).ItemId == ItemUses.StakePuller);
            var hairpin = oldSpots.Single(s => s.MakeItem(1).ItemId == ItemUses.MagicianHairpin);
            var wall = layout.GetComponentsInChildren<BreakableWall>(true).Single();
            var oldUv = layout.GetComponentsInChildren<UvMark>(true).Single();
            var oldNote = layout.GetComponentsInChildren<ClueInspectable>(true).Single();
            var oldTrunk = layout.GetComponentsInChildren<ToolTarget>(true).Single(t => t.ToolId == ItemUses.MagicianHairpin);
            var stair = layout.Find("ST_Stairwell/StairwellDoorway");
            var sealedDoor = layout.Find("B1_EscapeEntrance/SealedDoor");
            if (stair == null || sealedDoor == null) throw new InvalidOperationException("Measured door assemblies missing");
            metal = Material("LA_SteelPlate"); wood = Material("LA_WallRust"); paper = Material("LA_WallConcrete"); gold = Material("V4_RustyMetal");
            refs.Clear(); manifest.Clear(); groups.Clear();
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Place Structure A exploration");
            try
            {
                registry = New(layout, Marker, layout.position); registry.gameObject.SetActive(false);
                // Reuse original discoveries and UIDs. UV stays in the lobby as explicitly requested.
                MoveSpot(crowbar, "S10_Workshop", new Vector3(-13.7f, .04f, 110.1f));
                MoveSpot(hairpin, "S10_Workshop", new Vector3(-14.5f, .04f, 110.1f));
                Spot("S10_Workshop", "workshop-hairpin-2", ItemUses.MagicianHairpin, 31, new Vector3(-13.7f, .04f, 111f));
                Spot("S02_Dormitory", "bed-hairpin", ItemUses.MagicianHairpin, 32, new Vector3(18.8f, .04f, 80.58f));
                Clue("S10_Workshop", "graffiti-3", "벽의 낙서 3", new Vector3(-18.9f, 1.25f, 109.6f), true);
                BoxGate("S02_Dormitory", "dormitoryBox1", new Vector3(13, 0, 78));
                BoxGate("S02_Dormitory", "dormitoryBox2", new Vector3(13, 0, 76));
                Tool("S02_Dormitory", "emptyCabinet", "캐비넷 1", new Vector3(18f, 0, 72.8f), true);
                Tool("S02_Dormitory", "mapCabinet", "캐비넷 2", new Vector3(18f, 0, 70.8f), true);
                var map = Clue("S02_Dormitory", "mapClue", "대기실 낡은 지도", new Vector3(17.3f, .12f, 70.8f));
                Gate("S02_Dormitory", "steelBox", "잠긴 강철 상자", new Vector3(16.2f, 0, 78.1f), ExplorationGateKind.Open, new Vector3(.9f, .65f, .65f));
                Key("S02_Dormitory", "managementKey", "관리실 열쇠", new Vector3(16.2f, .08f, 77.2f));
                Clue("S02_Dormitory/Restroom", "graffiti1", "벽의 낙서 1", new Vector3(12.1f, 1.25f, 66.8f), true);
                Clue("S02_Dormitory/Restroom", "graffiti2", "벽의 낙서 2", new Vector3(17.8f, 1.25f, 66.8f), true);
                Undo.RecordObject(oldTrunk, "Bind existing trunk"); Set(oldTrunk, "rewardItemId", "");
                Shape(oldUv.transform, "InteractableSurface", Vector3.zero, new Vector3(.6f, .45f, .02f), metal);
                Set(oldUv, "label", "숨겨진 낙서 1 — UV"); refs["restroomUv"] = oldUv;
                refs["restroomUvClue"] = Clue("S02_Dormitory/Restroom", "uv-1-detail", "숨겨진 낙서 1", new Vector3(14.3f, 1.3f, 62.59f), true);
                Key("S02_Dormitory/Restroom", "key1", "열쇠 1", new Vector3(17.55f, .07f, 64.37f));
                refs["oldNotebook"] = oldNote; Set(oldNote, "title", "낡은 노트");
                Clue("S05_JunkStorage", "performanceRecord", "공연 기록", new Vector3(26.7f, 1.03f, 44.15f));
                Uv("S05_JunkStorage", "passwordPrefixUv", new Vector3(31.3f, 1.25f, 44.8f));
                var prefix = Clue("S05_JunkStorage", "passwordPrefixClue", "암호 앞 두 자리", new Vector3(31.3f, 1.25f, 44.75f), true);
                Set(prefix, "description", "12");
                refs["lockedBox1"] = oldTrunk;
                Key("S05_JunkStorage", "stairsKey", "계단 열쇠", oldTrunk.transform.position + new Vector3(0, .08f, 1));
                var monitor = Gate("S05_JunkStorage", "monitor", "기계실 모니터", new Vector3(26.8f, 1.05f, 32.8f), ExplorationGateKind.Monitor, new Vector3(.8f, .55f, .08f), false);
                var cam = New(Group("B1_MachineRoom"), "MachineRoomMonitorCamera", new Vector3(0, -6.1f, 92)).gameObject.AddComponent<Camera>();
                cam.transform.LookAt(new Vector3(4, -8.7f, 83)); cam.fieldOfView = 65; cam.nearClipPlane = .1f; cam.farClipPlane = 100; cam.enabled = false;
                Set(monitor, "monitorCamera", cam);
                Door("ST_Stairwell", "stairsDoor", "계단 잠긴 문", stair, new Vector3(5.25f, .2f, 101.8f), new Vector3(3.3f, 3.1f, .18f));
                BoxGate("P05_PipeZone", "pipeBox1", new Vector3(23.5f, 0, 57.6f));
                Clue("P05_PipeZone", "graffiti4", "벽의 낙서 4", new Vector3(25.6f, 1.25f, 53), true);
                BoxGate("S12_CargoBay", "cargoBox1", new Vector3(-43, 0, 63));
                BoxGate("S12_CargoBay", "cargoBox2", new Vector3(-43, 0, 65));
                BoxGate("S12_CargoBay", "cargoBox3", new Vector3(-43, 0, 67));
                var locked2 = Tool("S12_CargoBay", "lockedBox2", "잠긴 상자 2", new Vector3(-40.5f, 0, 66));
                var key2 = Key("S12_CargoBay", "key2", "열쇠 2", new Vector3(-40.5f, .08f, 65));
                key2.ConfigureRoute(StructureAExitRoute.Key2, new[] { StructureAContainerRoute.LockedBox2 }, StructureAContainerRoute.LockedBox2);
                var hiddenUv = Uv("S12_CargoBay", "cargo-uv", new Vector3(-40.5f, 1.25f, 70)); hiddenUv.ConfigureRoute("structure-a:uv-3");
                var hidden = Clue("S12_CargoBay", "hiddenGraffiti3", "숨겨진 낙서 3", new Vector3(-40.5f, 1.25f, 69.96f), true);
                Set(hidden, "description", "무대 장치 보관실의 캐비넷 3");
                hiddenUv.RevealWhenFound(hidden.gameObject);
                Tool("S11_StageGear", "cabinet3", "캐비넷 3", new Vector3(-40, 0, 43), true);
                Key("S11_StageGear", "escapeKey1", "탈출로 열쇠 1", new Vector3(-39, .08f, 43));
                Clue("LG_Lounge", "newspaper", "낡은 신문", new Vector3(3.4f, .58f, 80.8f));
                BoxGate("LG_Lounge", "loungeBox1", new Vector3(1.7f, 0, 83.2f));
                BoxGate("LG_Lounge", "loungeBox2", new Vector3(3.7f, 0, 83.2f));
                // Unmarked panel at the measured lower-plant exit. No additional puzzle condition is invented.
                var machine = Gate("B1_MachineRoom", "machineDoor", "숨겨진 문", new Vector3(-4.05f, -9.8f, 91.15f), ExplorationGateKind.Open, new Vector3(.18f, 3.6f, 2.1f));
                machine.transform.rotation = Quaternion.identity;
                var officeDoor = Gate("B1_Office", "management-door", "관리실 문", new Vector3(-7.2f, -4.8f, 101.8f), ExplorationGateKind.Open, new Vector3(2.1f, 3.6f, .18f));
                officeDoor.transform.rotation = Quaternion.identity;
                officeDoor.ConfigureRoute("structure-a:management-door", new[] { StructureAKeyRoute.ManagementKey });
                var note = Clue("B1_Office", "confiscatedNotebook", "압수한 노트", new Vector3(-6.4f, -3.95f, 106.5f)); Set(note, "description", "34");
                Tool("B1_Office", "managementDrawer", "관리실 서랍", new Vector3(-8.9f, -5, 107.6f), false, ToolTargetKind.Pry);
                Key("B1_Office", "escapeKey2", "탈출로 열쇠 2", new Vector3(-8.9f, -4.9f, 106.7f));
                Gate("B1_EscapeEntrance", "passwordPad", "비밀번호 패드", new Vector3(-12.5f, -9.5f, 91.8f), ExplorationGateKind.Keypad, new Vector3(.12f, .45f, .36f), false);
                var exit = Door("B1_EscapeEntrance", "escapeDoor", "탈출로 문", sealedDoor, new Vector3(-13.5f, -9.8f, 93.35f), new Vector3(.25f, 3.6f, 2.6f), ExplorationGateKind.Cooperative);
                exit.ConfigureRoute(StructureAKeyRoute.EscapeDoor, null, StructureAExitRoute.PasswordPad);
                var keys = Undo.AddComponent<StructureAKeyRoute>(registry.gameObject);
                Bind(keys, "key1", "steelBox", "managementKey", "lockedBox1", "stairsKey", "stairsDoor", "cabinet3", "managementDrawer", "escapeKey1", "escapeKey2", "escapeDoor");
                if (!keys.TryApply(out var error)) throw new InvalidOperationException(error);
                var boxes = Undo.AddComponent<StructureAContainerRoute>(registry.gameObject);
                Bind(boxes, "dormitoryBox1", "dormitoryBox2", "pipeBox1", "cargoBox1", "cargoBox2", "cargoBox3", "loungeBox1", "loungeBox2", "emptyCabinet", "mapCabinet", "lockedBox2", "mapClue");
                if (!boxes.TryApply(out error)) throw new InvalidOperationException(error);
                // Key2 is a progression token, not an extra catalog prop or random reward.
                var drops = locked2.GetComponent<ExplorationDrops>(); drops.ConfigureEmpty(); EditorUtility.SetDirty(drops);
                var clues = Undo.AddComponent<StructureAClueRoute>(registry.gameObject);
                Bind(clues, "graffiti1", "graffiti2", "graffiti4", "oldNotebook", "performanceRecord", "newspaper", "confiscatedNotebook", "restroomUv", "passwordPrefixUv", "restroomUvClue", "passwordPrefixClue", "monitor", "hiddenGraffiti3");
                Set(clues, "hiddenGraffiti3Condition", "structure-a:uv-3");
                if (!clues.TryApply(out error)) throw new InvalidOperationException(error);
                var exits = Undo.AddComponent<StructureAExitRoute>(registry.gameObject); refs["restroomBreach"] = wall;
                Bind(exits, "restroomBreach", "key2", "machineDoor", "passwordPad", "escapeDoor");
                Set(exits, "escapePassword", "1234"); Set(exits, "machineDoorHasNoRevealPrerequisite", true);
                if (!exits.TryApply(out error)) throw new InvalidOperationException(error);
                foreach (var component in refs.Values.Distinct()) EditorUtility.SetDirty(component);
                registry.gameObject.SetActive(true);
                Physics.SyncTransforms(); Undo.CollapseUndoOperations(undo); EditorSceneManager.MarkSceneDirty(scene);
                System.IO.File.WriteAllLines(Report, new[] { "# 구조 A 실제 맵 배치 기록", "", "2026-10-30 사용자 배치도·최신 목록 기준. 방 셸/기존 에셋 재생성 없음. 좌표는 현재 맵 월드 좌표.", "", "| 대상 | 좌표(x,y,z) |", "|---|---|" }.Concat(manifest));
                return $"Installed: {refs.Count} bound references; original UV/tool/catalog/breach rules retained. Save only WaitingRoomBase after validation.";
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }
        static Transform Group(string room)
        {
            if (groups.TryGetValue(room, out var found)) return found;
            var parent = layout.Find(room); var group = New(parent, "Exploration_A", parent.position); groups[room] = group; return group;
        }
        static Transform New(Transform parent, string name, Vector3 point)
        { var g = new GameObject(name); Undo.RegisterCreatedObjectUndo(g, "Place exploration object"); g.transform.SetParent(parent, false); g.transform.position = point; return g.transform; }
        static Transform Shape(Transform parent, string name, Vector3 offset, Vector3 size, Material material)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); Undo.RegisterCreatedObjectUndo(g, "Exploration marker"); g.name = name;
            g.transform.SetParent(parent, false); g.transform.localPosition = offset; g.transform.localScale = size;
            Object.DestroyImmediate(g.GetComponent<Collider>()); g.GetComponent<Renderer>().sharedMaterial = material; return g.transform;
        }
        static ExplorationGate Gate(string room, string field, string label, Vector3 at, ExplorationGateKind kind, Vector3 size, bool solid = true)
        {
            var root = New(Group(room), field, at); var visual = Shape(root, "Visual", Vector3.up * size.y * .5f, size, kind == ExplorationGateKind.Breakable ? wood : metal);
            var gate = Undo.AddComponent<ExplorationGate>(root.gameObject); Set(gate, "targetId", "structure-a:" + field); Set(gate, "kind", (int)kind); Set(gate, "label", label);
            Set(gate, "reach", 2f); Set(gate, "hiddenVisuals", new Object[] { visual.gameObject });
            if (solid) { var block = Undo.AddComponent<BoxCollider>(root.gameObject); block.center = Vector3.up * size.y * .5f; block.size = size; Set(gate, "blocker", block); }
            if (kind != ExplorationGateKind.Monitor && kind != ExplorationGateKind.Keypad) Set(gate, "hideOnOpen", new Object[] { visual.gameObject });
            refs[field] = gate; Record(label, at); return gate;
        }
        static ExplorationGate Key(string room, string field, string label, Vector3 at)
        {
            var key = Gate(room, field, label, at, ExplorationGateKind.Open, new Vector3(.32f, .08f, .1f), false);
            key.transform.GetComponentInChildren<Renderer>().sharedMaterial = gold; return key;
        }
        static void BoxGate(string room, string field, Vector3 at)
        {
            var gate = Gate(room, field, "파괴 가능한 상자", at, ExplorationGateKind.Breakable, new Vector3(.85f, .75f, .75f));
            Set(gate, "toolId", ItemUses.StakePuller); Set(gate, "requiredHits", 1);
            var drops = Undo.AddComponent<ExplorationDrops>(gate.gameObject); drops.ConfigurePending();
            Shape(gate.transform.Find("Visual"), "Band", Vector3.zero, new Vector3(1.04f, .12f, 1.04f), metal);
        }
        static ToolTarget Tool(string room, string field, string label, Vector3 at, bool tall = false, ToolTargetKind kind = ToolTargetKind.HairpinLock)
        {
            var root = New(Group(room), field, at); var size = tall ? new Vector3(.85f, 1.9f, .6f) : new Vector3(.85f, .65f, .6f);
            Shape(root, "Body", Vector3.up * size.y * .5f, size, metal);
            // Front (-z) hinge in the cabinet's own axes; a world offset twisted the door sideways in rotated rooms.
            var hinge = New(root, "Hinge", at); hinge.localPosition = new Vector3(-size.x * .5f, 0, -size.z * .5f - .03f);
            Shape(hinge, "Door", new Vector3(size.x * .5f, size.y * .5f, 0), new Vector3(size.x, size.y, .06f), wood);
            var target = Undo.AddComponent<ToolTarget>(root.gameObject); Set(target, "label", label); Set(target, "kind", (int)kind); Set(target, "hinge", hinge); Set(target, "openEuler", new Vector3(0, -105, 0));
            var block = Undo.AddComponent<BoxCollider>(root.gameObject); block.center = Vector3.up * size.y * .5f; block.size = size; Set(target, "blocker", block);
            refs[field] = target; Record(label, at); return target;
        }
        static ClueInspectable Clue(string room, string field, string title, Vector3 at, bool wall = false)
        {
            var root = New(Group(room), field, at); Shape(root, "Document", Vector3.zero, wall ? new Vector3(.48f, .32f, .035f) : new Vector3(.36f, .035f, .26f), paper);
            var clue = Undo.AddComponent<ClueInspectable>(root.gameObject); Set(clue, "title", title); clue.ConfigureIdentity("structure-a:" + field); refs[field] = clue; Record(title, at); return clue;
        }
        static UvMark Uv(string room, string field, Vector3 at)
        {
            var root = New(Group(room), field, at); Shape(root, "Stain", Vector3.zero, new Vector3(.5f, .35f, .025f), wood);
            var uv = Undo.AddComponent<UvMark>(root.gameObject); Set(uv, "label", "숨겨진 낙서 — UV"); refs[field] = uv; return uv;
        }
        static ExplorationGate Door(string room, string field, string label, Transform existing, Vector3 at, Vector3 size, ExplorationGateKind kind = ExplorationGateKind.Open)
        {
            var gate = Gate(room, field, label, at, kind, size);
            gate.transform.rotation = Quaternion.identity; // Sizes are measured in world doorway axes, not the rotated room axes.
            // Existing door leaves must also release their collision; frames stay in place.
            var leaves = existing.GetComponentsInChildren<Renderer>(true).Where(r => r.name.Contains("Door") && !r.name.Contains("Frame") && !r.name.Contains("Label") && !r.name.Contains("Latch")).Select(r => r.gameObject).Distinct().ToArray();
            var visuals = existing.GetComponentsInChildren<Renderer>(true).Where(r => !r.name.Contains("Frame")).Select(r => r.gameObject).Distinct().Cast<Object>().Concat(new Object[] { gate.transform.Find("Visual").gameObject }).ToArray();
            Set(gate, "hideOnOpen", visuals); Set(gate, "hiddenVisuals", visuals);
            return gate;
        }
        static void MoveSpot(FoundItemSpot spot, string room, Vector3 at)
        { Undo.RecordObject(spot.transform, "Move discovery to authored room"); spot.transform.position = at; Undo.SetTransformParent(spot.transform, Group(room), "Move discovery"); Record(spot.SpotId, at); }
        static void Spot(string room, string id, string item, int uid, Vector3 at)
        { var root = New(Group(room), id, at); var spot = Undo.AddComponent<FoundItemSpot>(root.gameObject); Set(spot, "spotId", id); Set(spot, "itemId", item); Set(spot, "uidOffset", uid); Record(id, at); }
        static void Bind(Component route, params string[] fields) { foreach (var field in fields) Set(route, field, refs[field]); }
        static void Set(Object target, string field, object value)
        {
            Undo.RecordObject(target, "Bind exploration route"); var so = new SerializedObject(target); var p = so.FindProperty(field) ?? throw new InvalidOperationException("Missing field " + field);
            if (value is Object[] values) { p.arraySize = values.Length; for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; }
            else if (value is string s) p.stringValue = s;
            else if (value is bool b) p.boolValue = b;
            else if (value is int i) p.intValue = i;
            else if (value is float f) p.floatValue = f;
            else if (value is Vector3 v) p.vector3Value = v;
            else p.objectReferenceValue = (Object)value;
            so.ApplyModifiedProperties();
        }
        static Material Material(string name)
        {
            var paths = AssetDatabase.FindAssets(name + " t:Material").Select(AssetDatabase.GUIDToAssetPath);
            var path = paths.FirstOrDefault(p => System.IO.Path.GetFileNameWithoutExtension(p) == name);
            return path == null ? throw new InvalidOperationException("Material missing " + name) : AssetDatabase.LoadAssetAtPath<Material>(path);
        }
        static void Record(string label, Vector3 at) => manifest.Add($"| {label} | {at.x:F2}, {at.y:F2}, {at.z:F2} |");
    }
}
