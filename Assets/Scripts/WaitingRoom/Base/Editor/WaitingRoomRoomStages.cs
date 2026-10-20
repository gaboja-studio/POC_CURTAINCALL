using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.EditorTools
{
    /// <summary>침대 교체, 폐기물 보관소, 소품실을 각각 별도 트랜잭션으로 적용한다.</summary>
    public static class WaitingRoomRoomStages
    {
        const string Bundle = "Assets/Scenes/Tests/WaitingRoom/Base/Prefabs/WaitingRoomRoomStages.asset";
        static Transform g, source, kit, output;
        static readonly List<Transform> allowed = new();
        public static string LastReport { get; private set; }

        static void Begin(string generatedName)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity") throw new InvalidOperationException("WaitingRoomBase 편집 모드 전용입니다.");
            g = scene.GetRootGameObjects().Single(o => o.name == "WaitingRoomGreybox").transform;
            source = scene.GetRootGameObjects().Single(o => o.name == "대기실").transform;
            kit = scene.GetRootGameObjects().Single(o => o.name == "WaitingRoom").transform;
            if (g.Find("Mood/InteriorArt/" + generatedName)) throw new InvalidOperationException("이미 적용된 단계: " + generatedName);
            allowed.Clear();
        }
        static void Permit(params string[] paths) { foreach (string p in paths) { var t = g.Find(p); if (t) allowed.Add(t); } }
        static void Run(string name, Action build)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var protectedObjects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Where(t => !allowed.Any(a => t == a || t.IsChildOf(a))).ToArray();
            string before = Signature(protectedObjects);
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName(name);
            try
            {
                output = Group(g.Find("Mood/InteriorArt"), name);
                build();
                if (before != Signature(protectedObjects)) throw new InvalidOperationException("비대상 변경 감지 — 단계를 되돌립니다.");
                AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); Undo.CollapseUndoOperations(undo);
                LastReport = name + ": 비대상 " + protectedObjects.Length + "개 서명 동일(상점·천막·무대 통로·원본 포함)";
                Debug.Log("[RoomStages] " + LastReport);
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        [MenuItem("Tools/CurtainCall/대기실/숙소·WaitingRoom 침대로 교체")]
        public static void ReplaceDormitoryBeds()
        {
            Begin("Stage01_WaitingRoomBeds");
            Permit("Mood/InteriorArt/Stage01_Dormitory");
            var native = Kit("R02_Dormitory/Props/SM_Prop_Bed_Bunk_01");
            Run("Stage01_WaitingRoomBeds", () =>
            {
                var dorm = g.Find("Mood/InteriorArt/Stage01_Dormitory");
                float[] xs = { -8.6f, -6.5f, -2.4f, 1.2f };
                for (int i = 0; i < 4; i++)
                {
                    Mute(dorm.Find("Bed_" + (char)('A' + i)));
                    Clone("Bed_" + (char)('A' + i), native, new Vector3(xs[i], .025f, 14.25f), 270, 1, true);
                    MoveBottom(dorm.Find("FootTrunk_" + i), new Vector3(xs[i] + .1f, 0, 12.6f));
                    MoveBottom(dorm.Find("PersonalClothes_" + i), new Vector3(xs[i] + .15f, .47f, 12.65f));
                    var lamp = dorm.Find("BedsideLamp_" + i); var p = Local(lamp); p.y = 2.2f; Move(lamp, p);
                    var light = dorm.Find("BedsideGlow_" + i); p = Local(light); p.y = 2.35f; Move(light, p);
                }
            });
        }

        [MenuItem("Tools/CurtainCall/대기실/공간별 2단계·폐기물 보관소")]
        public static void ApplyJunkStorage()
        {
            Begin("Stage02_JunkStorage");
            Permit("Mood/ProtoFill/JunkStorage", "Mood/Rooms/JunkStorage", "Mood/Furnishings/JunkStorage", "CameraZones/Zone_JunkStorage",
                "S05_JunkStorage/PerformanceLogNote", "S05_JunkStorage/PerformanceLogDesk", "S05_JunkStorage/BrokenProps", "S05_JunkStorage/StorageShelf", "S05_JunkStorage/CircusRecords",
                "S05_JunkStorage/OldCostume_1", "S05_JunkStorage/OldCostume_2", "S05_JunkStorage/OldCostume_3");
            foreach (Transform t in g.Find("Mood/InteriorArt"))
                if (t.name.StartsWith("WR06_PreviousPerformer") || t.name == "WR03_DiscardedCircusProps" || t.name == "JunkStorage_PatchedCeiling" || t.name.StartsWith("Junk_") || t.name.EndsWith("_2") || t.name.EndsWith("_3"))
                {
                    // 번호가 같아도 다른 방의 장식은 제외한다.
                    var rs = Renderers(t); if (t.name.StartsWith("WR06_PreviousPerformer") || t.name == "WR03_DiscardedCircusProps" || t.name == "JunkStorage_PatchedCeiling" || t.name.StartsWith("Junk_") || (rs.Length > 0 && LocalBounds(t).center.x < -10)) allowed.Add(t);
                }
            foreach (Transform t in g.Find("Mood/VisibilityLighting")) if (t.name.StartsWith("JunkStorage_")) allowed.Add(t);
            Run("Stage02_JunkStorage", BuildJunkStorage);
        }
        static void BuildJunkStorage()
        {
            foreach (string p in new[] { "Mood/ProtoFill/JunkStorage", "Mood/Rooms/JunkStorage", "Mood/Furnishings/JunkStorage" }) Mute(g.Find(p));
            foreach (var t in allowed.Where(t => t.parent == g.Find("Mood/InteriorArt"))) Mute(t);
            foreach (string n in new[] { "PerformanceLogDesk", "BrokenProps", "StorageShelf", "CircusRecords", "OldCostume_1", "OldCostume_2", "OldCostume_3" }) HideStandIn(g.Find("S05_JunkStorage/" + n));
            Box("InnerWestWall", new Vector3(-18.35f, 2.65f, 0), new Vector3(.3f, 5.3f, 17.3f), "V4_Wall_PeeledPlaster", true);
            Box("NorthPatchedWall", new Vector3(-14.1f, 2.65f, 8.65f), new Vector3(8.5f, 5.3f, .3f), "V4_Wall_DamagedConcrete", true);
            Box("SouthPatchedWall", new Vector3(-14.1f, 2.65f, -8.65f), new Vector3(8.5f, 5.3f, .3f), "V4_Wall_PeeledPlaster", true);
            for (int i = 0; i < 3; i++) Box("LowRoof_" + i, new Vector3(-14.1f, 5.35f + .04f * (i % 2), -5.7f + i * 5.7f), new Vector3(8.4f, .14f, 5.85f), "V4_Ceiling_RustyMetal", false, new Vector3(0, 0, i % 2 == 0 ? 1 : -1));
            for (int i = 0; i < 7; i++) Box("WestRepairStud_" + i, new Vector3(-18.12f, 2.5f, -7.6f + i * 2.5f), new Vector3(.1f, 4.9f, .1f), "V4_RustyMetal", false, new Vector3(i % 2 == 0 ? 1 : -1, 0, 0));
            Box("LowSortingPartition", new Vector3(-16.3f, 1, -1.7f), new Vector3(3.7f, 2, .12f), "V4_Wood_Old", true, new Vector3(0, 1, .5f));
            var filled = g.Find("Mood/ProtoFill/JunkStorage/StorageShelf_Kit0");
            for (int i = 0; i < 3; i++)
            {
                float x = -16.75f + i * 2.65f;
                Clone("ArchiveShelf_" + i, filled, new Vector3(x, 0, 8.05f), -90, 1, true);
                PaperFiles(new Vector3(x, .86f, 8.02f), i);
                Clone("ArchiveBooks_" + i, Kit("R08_JunkStorage_Hidden/Props/SM_Prop_BurnPile_Books_01"), new Vector3(x - .5f, 1.55f, 8.02f), i * 12, .18f);
            }
            Clone("ArchiveDesk", Src("소품실/가구/SM_WW2_Table02_c1_LOD0"), new Vector3(-14, 0, 6.5f), 0, .8f, true);
            Clone("ArchiveChair", Src("소품실/가구/SM_WW2_WoodenChair_c1_LOD0"), new Vector3(-14, 0, 5.25f), 180, 1, true);
            Move(g.Find("S05_JunkStorage/PerformanceLogNote"), new Vector3(-14, .73f, 6.5f));
            Lamp("ArchiveDeskLamp", new Vector3(-14.6f, 1.4f, 6.7f), 2.5f, 4.5f);
            Rug("ArchiveRug", new Vector3(-14, .012f, 4), 2.1f, 2.5f);
            Clone("WestArchiveShelf", filled, new Vector3(-17.75f, 0, 5.65f), 0, 1, true);
            for (int i = 0; i < 2; i++)
            {
                Clone("StoredCostume_" + i, g.Find("Mood/InteriorArt/WR06_PreviousPerformer_" + i), new Vector3(-17.25f + i * 1.1f, 0, 2.3f + i * .35f), 75, 1, true);
                Clone("CostumeTrunk_" + i, Src("대기실_본관/잡동사니/트렁크"), new Vector3(-17.15f + i * 1.05f, 0, 3.35f), -9 + i * 13, .7f, true);
                Clone("SortedStorageShelf_" + i, filled, new Vector3(-17.75f, 0, -3.35f - i * 2.65f), 0, 1, true);
            }
            Clone("RetiredCircusEquipment", g.Find("Mood/InteriorArt/WR03_DiscardedCircusProps"), new Vector3(-16.7f, 0, -.25f), 12, 1, true);
            Clone("RetiredChair", Src("소품실/가구/SM_WW2_WoodenChair_c1_LOD0"), new Vector3(-15.4f, 0, -.3f), 32, .9f, true);
            Clone("RecordsDesk", Src("소품실/가구/SM_WW2_Table02_c1_LOD0"), new Vector3(-14.3f, 0, -6.9f), 180, .8f, true);
            Clone("RecordsChair", Src("소품실/가구/SM_WW2_WoodenChair_c1_LOD0"), new Vector3(-14.3f, 0, -5.6f), 0, 1, true);
            PaperFiles(new Vector3(-14.3f, .7f, -6.9f), 2);
            Lamp("RecordsDeskLamp", new Vector3(-15, 1.35f, -7.1f), 2.5f, 4.5f);
            for (int i = 0; i < 2; i++) Clone("StoredBarrel_" + i, Src("대기실_본관/잡동사니/SM_Barrel"), new Vector3(-11.25f, 0, -6.9f + i * 1.2f), i * 17, .8f, true);
            for (int i = 0; i < 3; i++)
            {
                float z = new[] { 6.9f, 3, -3.5f }[i];
                Clone("EastCrates_" + i, Src("대기실_본관/잡동사니/SM_MET_WoodenCrate01a_c1_LOD0"), new Vector3(-10.8f, 0, z), i * 7 - 5, .6f, true);
                Clone("EastCrateLuggage_" + i, Src("대기실_본관/잡동사니/트렁크"), new Vector3(-10.8f, .52f, z), i * 9, .5f);
            }
            Zone("Zone_JunkStorage", new Vector3(-14.1f, 3, 0), new Vector3(7.9f, 6, 16.7f));
            foreach (Transform t in g.Find("Mood/VisibilityLighting")) if (t.name.StartsWith("JunkStorage_"))
            { var at = Local(t); at.x = -14.1f; Move(t, at); var l = t.GetComponent<Light>(); Undo.RecordObject(l, "보관소 보조광"); l.intensity = 4.5f; }
        }

        public static void RefineJunkStorage()
        {
            Begin("Stage02_ArchiveDetails");
            Permit("Mood/InteriorArt/Stage02_JunkStorage", "Mood/Dressing/JunkStorage_Trunk", "Mood/Dressing/JunkStorage_Masks");
            Run("Stage02_ArchiveDetails", () =>
            {
                Mute(g.Find("Mood/Dressing/JunkStorage_Trunk"));
                Mute(g.Find("Mood/Dressing/JunkStorage_Masks"));
                for (int i = 0; i < 3; i++)
                {
                    var shelf = g.Find("Mood/InteriorArt/Stage02_JunkStorage/ArchiveShelf_" + i).GetChild(0);
                    foreach (Transform child in shelf) if (child.name != "선반") Mute(child);
                    float x = -16.75f + i * 2.65f;
                    for (int row = 0; row < 4; row++) for (int book = 0; book < 7; book++)
                    {
                        float y = .15f + row * .7f;
                        var p = new Vector3(x - .95f + book * .3f, y + .15f, 8.02f);
                        Box("ArchiveBinder_" + i + "_" + row + "_" + book, p, new Vector3(.17f, .3f + .03f * (book % 2), .26f), book % 3 == 0 ? "V4_Wood_Old" : "V4_Blanket_Olive", false, new Vector3(0, 0, book % 2 == 0 ? 2 : -3));
                        Box("BinderLabel_" + i + "_" + row + "_" + book, p + new Vector3(0, .025f, -.132f), new Vector3(.11f, .055f, .004f), "V6_Paper_Old", false);
                    }
                }
            });
        }

        [MenuItem("Tools/CurtainCall/대기실/공간별 3단계·소품실")]
        public static void ApplyPropRoom()
        {
            Begin("Stage03_PropRoom");
            Permit("Mood/ProtoFill/PropRoom", "Mood/Furnishings/PropRoom", "S04_PropRoom/HandoverShelf_4Tier");
            foreach (string n in new[] { "CostumeStorage", "PropShelf_A", "PropShelf_B", "OldDolls", "MascotSuit", "DressingTable", "DressingMirror", "DressingWardrobe" }) Permit("S04_PropRoom/" + n);
            foreach (Transform t in g.Find("Mood/InteriorArt")) if (t.name.StartsWith("WR07_") || t.name.StartsWith("DressingBulb_") || t.name == "WR02_CostumeRack" || t.name is "WR06_BearSuit" or "WR06_ClownSuit") allowed.Add(t);
            Run("Stage03_PropRoom", BuildPropRoom);
        }
        static void BuildPropRoom()
        {
            Mute(g.Find("Mood/ProtoFill/PropRoom"));
            foreach (var t in allowed.Where(t => t.parent == g.Find("Mood/InteriorArt"))) Mute(t);
            var furnishings = g.Find("Mood/Furnishings/PropRoom");
            foreach (Transform t in furnishings) if (t.name != "HandoverShelf_Visual") Mute(t);
            foreach (string n in new[] { "CostumeStorage", "PropShelf_A", "PropShelf_B", "OldDolls", "MascotSuit", "DressingTable", "DressingMirror", "DressingWardrobe" }) HideStandIn(g.Find("S04_PropRoom/" + n));
            var rack = g.Find("Mood/InteriorArt/WR02_CostumeRack");
            for (int i = 0; i < 2; i++)
            {
                var rail = Clone("WestActiveCostumes_" + i, rack, new Vector3(-7.2f, 0, -11.35f - i * 2.55f), 0, 1, true);
                foreach (var r in rail.GetComponentsInChildren<Renderer>(true)) if (r.name.StartsWith("CostumeCanvas_"))
                {
                    int color = int.Parse(r.name.Substring(r.name.LastIndexOf('_') + 1));
                    r.sharedMaterial = Mat(new[] { "V4_Blanket_DullRed", "V4_Blanket_Navy", "V4_Mattress_Stained" }[color % 3]);
                }
            }
            Clone("CostumeCupboard", furnishings.Find("DressingCupboard"), new Vector3(-5.8f, 0, -16.65f), 0, 1, true);
            Clone("CostumeRepairBench", g.Find("Mood/ProtoFill/PropRoom/Workbench"), new Vector3(-5.3f, 0, -12.85f), 0, .9f, true);
            Clone("RepairChair", Src("소품실/가구/SM_WW2_WoodenChair_c1_LOD0"), new Vector3(-4.3f, 0, -12.3f), 270, .85f, true);
            var shelf = g.Find("Mood/ProtoFill/PropRoom/PropShelf_B_Kit0");
            Clone("NorthShelf_West", shelf, new Vector3(-5.35f, 0, -9.6f), 0, 1, true);
            Clone("NorthShelf_East", shelf, new Vector3(1.15f, 0, -9.6f), 0, 1, true);
            var visual = furnishings.Find("HandoverShelf_Visual");
            var old = LocalBounds(visual); var delta = new Vector3(-1.65f - old.center.x, 0, -15.15f - old.center.z);
            MoveDelta(visual, delta); MoveDelta(g.Find("S04_PropRoom/HandoverShelf_4Tier"), delta);
            Clone("SouthGeneralPropShelf", shelf, new Vector3(1.75f, 0, -15.15f), 0, 1, true);
            for (int i = 0; i < 2; i++)
            {
                float x = 4.5f + i * 2;
                Clone("SouthDressingDesk_" + i, Kit("R04_DressingRoom/Props/SM_Prop_Dresser_02"), new Vector3(x, 0, -16.7f), 0, .85f, true);
                Clone("SouthDressingMirror_" + i, furnishings.Find("FullLengthMirror"), new Vector3(x, 1.02f, -17.27f), 0, .72f);
                Clone("SouthDressingChair_" + i, Kit("R04_DressingRoom/Props/SM_Prop_Chair_Metal_01"), new Vector3(x, 0, -15.1f), 180, 1, true);
                VanityBulbs("SouthBulbs_" + i, new Vector3(x, 1.75f, -17.12f), 0);
                Point("VanityWarm_" + i, new Vector3(x, 2.15f, -16.3f), new Color(1, .69f, .39f), 3.2f, 4);
                Rug("DressingRug_" + i, new Vector3(x, .012f, -13.5f), 1.75f, 1.5f);
                Clone("WigStand_" + i, Kit("R04_DressingRoom/Props/SM_Prop_Mannequin_Head_01"), new Vector3(x - .25f, .86f, -16.6f), 0, .65f);
                Clone("MakeupJar_" + i, Src("상점/가구/Jar"), new Vector3(x + .35f, .86f, -16.55f), 0, .5f);
            }
            Clone("EastDressingDesk", Kit("R04_DressingRoom/Props/SM_Prop_Dresser_02"), new Vector3(7.15f, 0, -11.2f), 270, .85f, true);
            Clone("EastDressingMirror", furnishings.Find("FullLengthMirror"), new Vector3(7.83f, 1.02f, -11.2f), 90, .72f);
            Clone("EastDressingChair", Kit("R04_DressingRoom/Props/SM_Prop_Chair_Metal_01"), new Vector3(5.7f, 0, -11.2f), 90, 1, true);
            VanityBulbs("EastBulbs", new Vector3(7.7f, 1.75f, -11.2f), 90);
            Point("EastVanityWarm", new Vector3(7, 2.15f, -11.2f), new Color(1, .69f, .39f), 2.7f, 3.5f);
            for (int i = 0; i < 3; i++) Clone("StoredMascotHead_" + i, Src("분장실/" + new[] { "탈_광대", "탈_토끼", "탈_곰" }[i]), new Vector3(-6.7f + i * 1.05f, 2.15f, -17.18f), 180, .65f);
            Clone("PreviousToy", Kit("R02_Dormitory/Props/SM_Prop_Toy_Bear_01"), new Vector3(2.1f, .85f, -15.1f), 0, 1.3f);
            Point("CoolToolsAccent", new Vector3(6.9f, 1.3f, -9.6f), new Color(.3f, .66f, .8f), .7f, 2.3f);
        }

        static void PaperFiles(Vector3 p, int seed)
        {
            for (int i = 0; i < 5; i++) Box("RecordFolder_" + seed + "_" + i, p + new Vector3(-.6f + i * .27f, .025f * (i % 3), 0), new Vector3(.23f, .05f + .015f * (i % 2), .35f), i % 2 == 0 ? "V6_Paper_Old" : "V4_Blanket_Olive", false, new Vector3(0, -9 + i * 5, 0));
        }
        static void VanityBulbs(string name, Vector3 at, float yaw)
        {
            var root = Group(output, name); root.localPosition = at; root.localRotation = Quaternion.Euler(0, yaw, 0);
            var mat = Variant("WarmVanityBulb", "V5_Bulb_Dead", new Color(1, .8f, .5f)); mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", new Color(1.8f, .9f, .3f));
            for (int i = 0; i < 11; i++)
            {
                Vector3 p = i < 8 ? new Vector3(i < 4 ? -.43f : .43f, -.5f + i % 4 * .32f, 0) : new Vector3(-.3f + (i - 8) * .3f, .55f, 0);
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); Undo.RegisterCreatedObjectUndo(go, "분장 전구"); go.name = "Bulb_" + i; go.transform.SetParent(root, false); go.transform.localPosition = p; go.transform.localScale = Vector3.one * .075f; go.GetComponent<Renderer>().sharedMaterial = mat; Object.DestroyImmediate(go.GetComponent<Collider>());
            }
        }
        static void Lamp(string name, Vector3 p, float intensity, float range)
        { Clone(name, Src("대기실_본관/조명/CagedLamp"), p, 0, .35f); Point(name + "Glow", p + Vector3.up * .15f, new Color(1, .65f, .34f), intensity, range); }
        static void Point(string name, Vector3 at, Color color, float intensity, float range)
        { var t = Group(output, name); t.localPosition = at; var l = Undo.AddComponent<Light>(t.gameObject); l.type = LightType.Point; l.color = color; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None; }
        static void Rug(string name, Vector3 at, float width, float depth)
        {
            var rug = Clone(name, AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_ThirdParty/Synty/PolygonApocalypse/Prefabs/Props/SM_Prop_Rug_04.prefab").transform, at);
            var b = Bounds(rug); rug.localScale *= Mathf.Min(width / b.size.x, depth / b.size.z); MoveBottom(rug, at);
            foreach (var mf in rug.GetComponentsInChildren<MeshFilter>(true))
            {
                float bottom = mf.GetComponent<Renderer>().bounds.min.y;
                var mesh = WaitingRoomRugMesh.Create(name + "Pattern"); EnsureBundle(); AssetDatabase.AddObjectToAsset(mesh, Bundle); mf.sharedMesh = mesh;
                mf.transform.position += Vector3.up * (bottom - mf.GetComponent<Renderer>().bounds.min.y);
                mf.GetComponent<Renderer>().sharedMaterial = Variant("FadedCircusRug", "V4_Harlequin", new Color(.52f, .43f, .35f));
            }
        }
        static Transform Clone(string name, Transform src, Vector3 at, float yaw = 0, float scale = 1, bool solid = false)
        {
            if (!src) throw new InvalidOperationException("원본 없음: " + name);
            var root = Group(output, name); root.position = Vector3.zero;
            var copy = Object.Instantiate(src.gameObject, root); Undo.RegisterCreatedObjectUndo(copy, "제작된 에셋 복제"); copy.name = src.name;
            copy.transform.SetPositionAndRotation(src.position, src.rotation); copy.transform.localScale = src.lossyScale; copy.SetActive(true);
            foreach (var c in copy.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var c in copy.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(c);
            foreach (var c in copy.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(c);
            foreach (var c in copy.GetComponentsInChildren<TextMesh>(true)) Object.DestroyImmediate(c.gameObject);
            var b = Bounds(root); copy.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);
            root.localRotation = Quaternion.Euler(0, yaw, 0); root.localScale = Vector3.one * scale; root.position = g.TransformPoint(at);
            if (solid) ColliderFor(root); return root;
        }
        static void ColliderFor(Transform t)
        {
            var b = Bounds(t); var lb = new Bounds(t.InverseTransformPoint(b.center), Vector3.zero);
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2) lb.Encapsulate(t.InverseTransformPoint(b.center + Vector3.Scale(b.extents, new Vector3(x, y, z))));
            var c = Undo.AddComponent<BoxCollider>(t.gameObject); c.center = lb.center; c.size = lb.size;
        }
        static Transform Box(string name, Vector3 at, Vector3 size, string material, bool solid, Vector3 rotation = default)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); Undo.RegisterCreatedObjectUndo(go, "공간 구조"); go.name = name; go.transform.SetParent(output, false); go.transform.localPosition = at; go.transform.localScale = size; go.transform.localRotation = Quaternion.Euler(rotation); go.GetComponent<Renderer>().sharedMaterial = Mat(material); if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>()); return go.transform;
        }
        static Transform Group(Transform p, string name) { var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "공간별 배치"); go.transform.SetParent(p, false); return go.transform; }
        static Transform Src(string path) => source.Find(path) ?? throw new InvalidOperationException("대기실 원본 없음: " + path);
        static Transform Kit(string path) => kit.Find(path) ?? throw new InvalidOperationException("WaitingRoom 원본 없음: " + path);
        static Renderer[] Renderers(Transform t) => t.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer) && !r.GetComponent<TextMesh>()).ToArray();
        static Bounds Bounds(Transform t) { var rs = Renderers(t); if (rs.Length == 0) throw new InvalidOperationException("외형 없음: " + t.name); var b = rs[0].bounds; foreach (var r in rs.Skip(1)) b.Encapsulate(r.bounds); return b; }
        static Bounds LocalBounds(Transform t) { var b = Bounds(t); return new Bounds(g.InverseTransformPoint(b.center), b.size); }
        static Vector3 Local(Transform t) => g.InverseTransformPoint(t.position);
        static void Move(Transform t, Vector3 p) { Undo.RecordObject(t, "공간 배치"); t.position = g.TransformPoint(p); }
        static void MoveDelta(Transform t, Vector3 p) { Undo.RecordObject(t, "공간 배치"); t.position += g.TransformVector(p); }
        static void MoveBottom(Transform t, Vector3 p) { var b = Bounds(t); MoveDelta(t, p - g.InverseTransformPoint(new Vector3(b.center.x, b.min.y, b.center.z))); }
        static void Mute(Transform t) { if (!t) return; Undo.RecordObject(t.gameObject, "이전 배치 보관"); t.gameObject.SetActive(false); }
        static void HideStandIn(Transform t)
        { foreach (var r in t.GetComponentsInChildren<Renderer>(true)) { if (r.GetComponent<TextMesh>()) continue; Undo.RecordObject(r, "대역 외형 교체"); r.enabled = false; } foreach (var c in t.GetComponentsInChildren<Collider>(true)) if (!c.isTrigger) { Undo.RecordObject(c, "대역 충돌 교체"); c.enabled = false; } }
        static void Zone(string name, Vector3 p, Vector3 size) { var t = g.Find("CameraZones/" + name); Move(t, p); var c = t.GetComponent<BoxCollider>(); Undo.RecordObject(c, "카메라 구역"); c.size = size; }
        static Material Mat(string name) => source.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).First(m => m && m.name == name);
        static void EnsureBundle() { if (!AssetDatabase.LoadMainAssetAtPath(Bundle)) AssetDatabase.CreateAsset(new Mesh { name = "RoomStageAssets" }, Bundle); }
        static Material Variant(string name, string original, Color color)
        { var found = AssetDatabase.LoadAllAssetsAtPath(Bundle).OfType<Material>().FirstOrDefault(m => m.name == name); if (found) return found; EnsureBundle(); var mat = new Material(Mat(original)) { name = name, color = color }; AssetDatabase.AddObjectToAsset(mat, Bundle); return mat; }
        static string Signature(IEnumerable<Transform> objects)
        {
            var text = new System.Text.StringBuilder(); foreach (var t in objects) { if (!t) { text.Append("DESTROYED"); continue; } text.Append(t.GetInstanceID()).Append(t.name).Append(t.parent ? t.parent.GetInstanceID() : 0).Append(t.localPosition.ToString("R")).Append(t.localRotation.ToString("R")).Append(t.localScale.ToString("R")).Append(t.gameObject.activeSelf).Append(t.gameObject.layer); foreach (var c in t.GetComponents<Component>()) if (c && !(c is Transform)) text.Append(EditorJsonUtility.ToJson(c)); } return Hash128.Compute(text.ToString()).ToString();
        }
    }
}
