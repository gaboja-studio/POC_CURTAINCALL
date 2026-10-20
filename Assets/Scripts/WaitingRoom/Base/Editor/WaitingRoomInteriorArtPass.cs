using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.EditorTools
{
    /// <summary>통로와 두 원본 프로토타입을 보존하는 일회성 실내 아트 배치. 변경 전체는 Undo 한 번으로 취소 가능.</summary>
    public static class WaitingRoomInteriorArtPass
    {
        const string MeshPath = "Assets/Scenes/Tests/WaitingRoom/Base/Prefabs/WaitingRoomInteriorMeshes.asset";
        static Transform g, source, kit, art;
        static readonly List<string> protectedPaths = new();
        public static string LastReport { get; private set; }

        [MenuItem("Tools/CurtainCall/대기실/실내 밀도·낡은 구조 적용")]
        public static void ApplyCurrent()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity")
                throw new InvalidOperationException("WaitingRoomBase 편집 씬에서만 실행합니다.");
            g = scene.GetRootGameObjects().Single(x => x.name == "WaitingRoomGreybox").transform;
            source = scene.GetRootGameObjects().Single(x => x.name == "대기실").transform;
            kit = scene.GetRootGameObjects().Single(x => x.name == "WaitingRoom").transform;
            if (g.Find("Mood/InteriorArt")) throw new InvalidOperationException("이미 적용된 실내입니다. 중복 압축을 막기 위해 먼저 Undo로 되돌려 주세요.");
            protectedPaths.Clear();
            protectedPaths.AddRange(new[] { "S06_StageCorridor_Temp", "S07_StageEntrance_Temp", "Mood/Tunnel", "Mood/Ceilings/Ceiling_Corridor", "Mood/Furnishings/Corridor", "CameraZones/Zone_Corridor", "Walls/W_06_East", "Walls/W_06_North_08_South", "Walls/W_06_South", "Walls/W_x+10/Seg1", "Walls/W_x+10/Seg2", "S01_Lobby/WaitingRoomDoor_Frame" });
            foreach (var t in g.GetComponentsInChildren<Transform>(true))
            {
                string p = Path(t);
                if ((p.StartsWith("Mood/Curtains/") && !p.Contains("ExpertRoom")) ||
                    p.StartsWith("Mood/VisibilityLighting/Corridor_") || p == "Mood/VisibilityLighting/StageEntrance" ||
                    p == "Mood/Lights/StageEntrance_RedSpill" || p == "Mood/LightingPass/Spot_StageCurtain")
                    protectedPaths.Add(p);
            }
            string before = ProtectedSignature(), proto = Signature(new[] { source, kit });
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("대기실 실내 압축·서커스 백스테이지 고도화");
            try
            {
                art = Group(g.Find("Mood"), "InteriorArt");
                CompactRooms();
                ReplaceAssets();
                ShabbyStructure();
                ClutterAndMood();
                if (before != ProtectedSignature() || proto != Signature(new[] { source, kit }))
                    throw new InvalidOperationException("보호 대상 변경 감지: 전체 실내 변경을 되돌립니다.");
                LastReport = $"통로 보호 {protectedPaths.Count}개 루트 서명 동일; 프로토타입 2개 동일; 교체 WR01~WR07; 실내 생성 {art.GetComponentsInChildren<Transform>(true).Length}개";
                var proof = Group(art, "Review_Protected_" + before);
                proof.gameObject.SetActive(false);
                EditorSceneManager.MarkSceneDirty(scene);
                AssetDatabase.SaveAssets();
                Undo.CollapseUndoOperations(undo);
                Debug.Log("[InteriorArt] " + LastReport);
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        static Vector3 Map(Vector3 p, bool props)
        {
            if (props) { p.x *= .8f; p.z = -9 + (p.z + 9) * (8.5f / 11.5f); }
            else { p.z = 9 + (p.z - 9) * .725f; if (p.x > 3) p.x = 3 + (p.x - 3) * (5.7f / 7); }
            return p;
        }
        static void MapGroup(string path, bool props)
        {
            var t = Find(path); if (!t) return;
            foreach (Transform child in t)
            {
                var rs = Renderers(child);
                Vector3 at = rs.Length > 0 ? g.InverseTransformPoint(Bounds(child).center) : g.InverseTransformPoint(child.position);
                MoveDelta(child, Map(at, props) - at);
            }
        }
        static void CompactRooms()
        {
            foreach (string p in new[] { "Mood/Furnishings/Dormitory", "Mood/Furnishings/Restroom", "Mood/ProtoFill/Dormitory", "Mood/Rooms/Dormitory", "S02_Dormitory/Restroom", "S02_Dormitory/Beds" }) MapGroup(p, false);
            foreach (string p in new[] { "Mood/Furnishings/PropRoom", "Mood/ProtoFill/PropRoom", "S04_PropRoom" }) MapGroup(p, true);
            SetBox("S02_Dormitory/Floor", new Vector3(0, -.1f, 13.35f), new Vector3(20, .2f, 8.7f));
            SetBox("S04_PropRoom/Floor", new Vector3(0, -.1f, -13.25f), new Vector3(16, .2f, 8.5f));
            SetBox("Walls/W_02_North/Seg0", new Vector3(0, 5, 17.7f), new Vector3(20, 10, .3f));
            SetBox("Walls/W_04_South/Seg0", new Vector3(0, 5, -17.5f), new Vector3(16, 10, .3f));
            // 공용 외벽은 보존하고 방 안쪽에 덧댄 서비스 벽으로 실사용 폭을 줄인다.
            Box(art, "Restroom_InnerServiceWall", new Vector3(8.85f, 2.2f, 13.35f), new Vector3(.3f, 4.4f, 8.7f), "V4_Wall_PeeledPlaster", true);
            Box(art, "Props_InnerWall_W", new Vector3(-8.15f, 2.5f, -13.25f), new Vector3(.3f, 5, 8.5f), "V4_Wall_DamagedConcrete", true);
            Box(art, "Props_InnerWall_E", new Vector3(8.15f, 2.5f, -13.25f), new Vector3(.3f, 5, 8.5f), "V4_Wall_PeeledPlaster", true);
            Zone("Zone_Dormitory", new Vector3(-3.5f, 3, 13.35f), new Vector3(12.7f, 6, 8.4f));
            Zone("Zone_Restroom", new Vector3(5.85f, 3, 13.35f), new Vector3(5.4f, 6, 8.4f));
            Zone("Zone_PropRoom", new Vector3(0, 3, -13.25f), new Vector3(15.7f, 6, 8.2f));
            foreach (var l in g.Find("Mood/Lights").Cast<Transform>().ToArray())
            {
                if (l.name.StartsWith("Dormitory") || l.name.StartsWith("Restroom")) MoveDelta(l, Map(Local(l), false) - Local(l));
                if (l.name.StartsWith("PropRoom") || l.name.StartsWith("DressingCorner")) MoveDelta(l, Map(Local(l), true) - Local(l));
            }
            foreach (var l in g.Find("Mood/VisibilityLighting").Cast<Transform>().ToArray())
            {
                if (l.name.StartsWith("Dormitory") || l.name.StartsWith("Restroom")) MoveDelta(l, Map(Local(l), false) - Local(l));
                if (l.name.StartsWith("PropRoom")) MoveDelta(l, Map(Local(l), true) - Local(l));
            }
            foreach (string p in new[] { "Mood/LightingPass/CoolFill_Dormitory", "Mood/LightingPass/CoolFill_PropRoom" })
            {
                var t = Find(p); if (!t) continue;
                var at = Map(Local(t), p.Contains("PropRoom")); at.y = 3.7f; MoveTo(t, at);
            }
            MoveTo(Find("Mood/LightingPass/Spot_HandoverShelf"), new Vector3(-.8f, 3.6f, -15.3f));
            MoveTo(Find("Mood/Dressing/Restroom_Wet"), new Vector3(6, .01f, 13.4f));
            MoveTo(Find("Mood/Dressing/Restroom_Dustbin"), new Vector3(8.3f, .31f, 10.4f));
            foreach (string room in new[] { "Dormitory", "PropRoom", "JunkStorage", "ExpertRoom" }) Mute("Mood/Ceilings/Ceiling_" + room);
            // 기능 선반은 수평 이동만 하고 네 판자의 높이는 유지한다.
            AtBottom("Mood/Furnishings/PropRoom/HandoverShelf_Visual", new Vector3(-.8f, 0, -17.04f));
            AtBottom("Mood/Furnishings/PropRoom/DressingCupboard", new Vector3(6.85f, 0, -16.55f));
            AtBottom("Mood/Furnishings/PropRoom/FullLengthMirror", new Vector3(4.85f, .05f, -17.15f));
            AtBottom("Mood/Furnishings/PropRoom/RepairShelf", new Vector3(-4.5f, 0, -16.7f));
            AtBottom("Mood/Furnishings/PropRoom/WorkMannequin", new Vector3(-4.7f, 0, -13.3f));
            AtBottom("Mood/Furnishings/PropRoom/WorkCrate", new Vector3(-5.6f, 0, -15.6f));
            AtBottom("Mood/ProtoFill/PropRoom/PropShelf_A_Kit0", new Vector3(-7.5f, 0, -10.9f));
            AtBottom("Mood/ProtoFill/PropRoom/PropShelf_A_Kit1", new Vector3(-7.5f, 0, -13.45f));
            AtBottom("Mood/ProtoFill/PropRoom/PropShelf_B_Kit0", new Vector3(-5.5f, 0, -9.65f));
            Mute("Mood/ProtoFill/PropRoom/PropShelf_B_Kit1");
            foreach (var t in Find("Mood/Furnishings/Restroom").Cast<Transform>())
                if (t.name.StartsWith("Sink_")) { var b = Bounds(t); var p = g.InverseTransformPoint(new Vector3(b.center.x, b.min.y, b.center.z)); p.z = 9.75f; AtBottom(Path(t), p); }
        }

        static void ReplaceAssets()
        {
            var review = Group(art, "AssetReview_Replaced_WR01_to_WR07"); review.gameObject.SetActive(false);
            foreach (string n in new[] { "WR01_대형천막_소형비대칭천막으로교체", "WR02_늘어난의상걸이_정상비율랙과천으로교체", "WR03_풀이섞인폐기물_서커스보관품으로교체", "WR04_캠핑가구_낡은목제가구로교체", "WR05_과도한길이벤치_짧은수선벤치로교체", "WR06_노출마네킹_의상과탈로보완", "WR07_대형화장대_개별분장대로교체" }) Group(review, n);
            // 의도된 천막의 찌그러짐은 새로운 외형에만 적용하며 기존 공유 메시를 변형하지 않는다.
            var tent = Find("S01_Lobby/CircusTent");
            var copy = Object.Instantiate(tent.gameObject, art); Undo.RegisterCreatedObjectUndo(copy, "작은 천막");
            copy.name = "WR01_SlumpedTent"; copy.transform.localPosition = new Vector3(-.7f, .04f, 2);
            copy.transform.localScale = new Vector3(.6f, .48f, .63f);
            copy.transform.localRotation = Quaternion.Euler(-4, 8, -7);
            foreach (var c in copy.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            var roof = copy.transform.Find("Roof");
            if (roof && roof.TryGetComponent<MeshFilter>(out var mf))
            {
                var mesh = Object.Instantiate(mf.sharedMesh); mesh.name = "WR01_SaggedTentRoof";
                var vs = mesh.vertices;
                for (int i = 0; i < vs.Length; i++) { var v = vs[i]; v.x += v.y * .12f; v.y -= .16f * Mathf.Sin(v.z * 3 + v.x * 2); vs[i] = v; }
                mesh.vertices = vs; mesh.RecalculateNormals(); mesh.RecalculateBounds(); mf.sharedMesh = Persist(mesh);
            }
            Mute("S01_Lobby/CircusTent");
            var pile = Find("Mood/Dressing/Lobby_TentPile");
            if (pile) foreach (Transform t in pile) { var p = Local(t); p.x = -.7f + p.x * .65f; p.z = 2 + (p.z - 1.8f) * .65f; MoveTo(t, p); }
            foreach (string p in new[] { "Mood/ProtoFill/PropRoom/CostumeRack_0", "Mood/ProtoFill/PropRoom/CostumeRack_1", "Mood/Furnishings/PropRoom/CostumeRack_West", "Mood/ProtoFill/Lobby/의상걸이" }) Mute(p);
            Rack("WR02_CostumeRack", new Vector3(-7.1f, 0, -16.05f), 90);
            Rack("WR02_LobbyCostumeRail", new Vector3(-8.95f, 0, 2.8f), 90);
            Mute("Mood/Furnishings/JunkStorage/DiscardedPropsPile");
            var junk = Group(art, "WR03_DiscardedCircusProps");
            Clone(junk, "Crate", Src("대기실_본관/잡동사니/SM_MET_WoodenCrate01a_c1_LOD0"), new Vector3(-18.1f, 0, -2.6f), 17, .9f, true);
            Clone(junk, "Drum", Src("대기실_본관/잡동사니/북"), new Vector3(-17.3f, 0, -2.5f), -14, .8f, true);
            Clone(junk, "FoldedCanvas", Src("분장실/SM_MET_TangledCloth02_c1_LOD1"), new Vector3(-18.2f, .78f, -2.6f), 25, .65f);
            foreach (string n in new[] { "SharedRestTable", "RestChair_North", "RestChair_East" }) Mute("Mood/Furnishings/Dormitory/" + n);
            Clone(art, "WR04_WornDiningTable", Src("소품실/가구/SM_WW2_Table02_c1_LOD0"), new Vector3(-6.2f, 0, 11.1f), 5, .75f, true);
            Clone(art, "WR04_Chair_N", Src("소품실/가구/SM_WW2_WoodenChair_c1_LOD0"), new Vector3(-6.2f, 0, 12.15f), 184, 1, true);
            Clone(art, "WR04_Chair_E", Src("소품실/가구/SM_WW2_WoodenChair_c1_LOD0"), new Vector3(-4.95f, 0, 11.1f), 82, 1, true);
            AtBottom("Mood/Furnishings/Dormitory/TablePersonalBox", new Vector3(-6.25f, .65f, 11.15f));
            Mute("Mood/Rooms/Dormitory/Bench");
            var bench = Group(art, "WR05_RepairedShortBench");
            Box(bench, "Seat", new Vector3(-4.6f, .48f, 14.3f), new Vector3(2.2f, .14f, .48f), "V4_Wood_Old", true, new Vector3(0, 3, 1));
            for (int i = 0; i < 2; i++) Box(bench, "Leg_" + i, new Vector3(-5.4f + i * 1.6f, .22f, 14.3f), new Vector3(.15f, .45f, .4f), "V4_Wood_Old", true, new Vector3(0, 0, i == 0 ? -4 : 3));
            foreach (string n in new[] { "MascotBody", "CostumeDisplay" }) Mute("Mood/Furnishings/PropRoom/" + n);
            Costume("WR06_BearSuit", new Vector3(3.15f, 0, -16.45f), "bear");
            Costume("WR06_ClownSuit", new Vector3(4.1f, 0, -16.35f), "clown");
            for (int i = 0; i < 3; i++)
            {
                var t = Find("Mood/Furnishings/JunkStorage/PreviousPerformer_" + i);
                if (t) { var p = g.InverseTransformPoint(Bounds(t).center); p.y = 0; Mute(Path(t)); Costume("WR06_PreviousPerformer_" + i, p, i == 1 ? "clown" : "bear"); }
            }
            Mute("Mood/ProtoFill/PropRoom/Vanity");
            for (int i = 0; i < 2; i++)
            {
                float z = -11.25f - i * 3;
                Clone(art, "WR07_DressingDesk_" + i, Kit("R04_DressingRoom/Props/SM_Prop_Dresser_02"), new Vector3(7.1f, 0, z), 270, .9f, true);
                Clone(art, "WR07_Mirror_" + i, new[] { Src("분장실/FullMirrorFrame"), Src("분장실/FullMirror") }, new Vector3(7.82f, 1.05f, z), 0, .7f);
                Clone(art, "WR07_DressingChair_" + i, Src("소품실/가구/SM_WW2_WoodenChair_c1_LOD0"), new Vector3(5.8f, 0, z), 90, 1, true);
                Point("DressingBulb_" + i, new Vector3(7, 2.3f, z), new Color(1, .67f, .35f), 2, 3.5f);
            }
        }
        static void Costume(string name, Vector3 p, string type)
        {
            var root = Group(art, name);
            Clone(root, "Body", Kit("R04_DressingRoom/Props/SM_Prop_Mannequin_Female_02"), p, 15, .85f, true);
            SuitDrape(root, p);
            var mask = Src(type == "clown" ? "분장실/탈_광대" : "분장실/탈_곰");
            Clone(root, "Head", mask, p + new Vector3(0, 1.5f, 0), 0, .8f);
        }
        static void Rack(string name, Vector3 at, float yaw)
        {
            var root = Group(art, name);
            Box(root, "Rail", at + new Vector3(0, 1.9f, 0), new Vector3(2.1f, .07f, .07f), "V4_RustyMetal", false);
            for (int i = 0; i < 2; i++) Box(root, "Upright_" + i, at + new Vector3(i == 0 ? -1 : 1, .95f, 0), new Vector3(.07f, 1.9f, .35f), "V4_RustyMetal", true);
            for (int i = 0; i < 3; i++) Cloth(root, "CostumeCanvas_" + i, at + new Vector3(-.65f + i * .65f, 1.15f, 0), .6f, 1.35f, .12f, i == 1 ? "V4_Harlequin" : "V5_Rag_Canvas_Cloth", Quaternion.Euler(90, 0, 0));
            // 그룹 피벗을 랙 위치로 옮긴 뒤 방향만 바꾼다.
            foreach (Transform t in root) t.position -= g.TransformVector(at);
            root.localPosition = at; root.localRotation = Quaternion.Euler(0, yaw, 0);
        }

        static void ShabbyStructure()
        {
            MoveTo(Find("Mood/Ceilingworks/Chains_Lobby"), new Vector3(0, 4.6f, 6.2f));
            MoveTo(Find("Mood/Ceilingworks/Chains_JunkStorage"), new Vector3(-16, 4.2f, 0));
            MoveTo(Find("Mood/Ceilingworks/Dormitory_Skylight"), new Vector3(-3, 4.18f, 13.4f));
            MoveTo(Find("Mood/Ceilingworks/JunkStorage_Skylight"), new Vector3(-16, 4.75f, -2.5f));
            Canopy("Dormitory", new Vector3(-3.5f, 4.65f, 13.35f), 12.8f, 8.5f, .45f);
            Canopy("Restroom", new Vector3(5.9f, 4.35f, 13.35f), 5.5f, 8.5f, .2f);
            Canopy("PropRoom", new Vector3(0, 4.9f, -13.25f), 15.8f, 8.3f, .6f);
            Canopy("JunkStorage", new Vector3(-16, 5.5f, 0), 11.8f, 17.8f, .8f);
            Canopy("ExpertRoom", new Vector3(14, 4.9f, 8.5f), 7.8f, 8.8f, .35f);
            Cloth(art, "Lobby_SaggingCanvas_W", new Vector3(-5.5f, 6.2f, 0), 8.5f, 16.5f, 1.05f, "V5_Rag_Stripes_Cloth", Quaternion.Euler(0, 0, 2));
            Cloth(art, "Lobby_SaggingCanvas_E", new Vector3(5.4f, 6.5f, 0), 8.3f, 16.5f, .85f, "V5_Rag_Canvas_Cloth", Quaternion.Euler(0, 0, -3));
            for (int i = 0; i < 4; i++)
            {
                float z = -6.8f + i * 4.6f;
                Box(art, "Lobby_RustCrossBeam_" + i, new Vector3(0, 6.05f + .08f * i, z), new Vector3(19.6f, .18f, .22f), "V4_RustyMetal", false, new Vector3(0, 0, i % 2 == 0 ? 1.5f : -2));
            }
            // 원래 문과 충돌체를 보존하면서 비뚤어진 덧댐 프레임만 추가한다.
            DoorRepair("DormEntry", new Vector3(-4, 0, 8.75f), 3, 0);
            DoorRepair("PropEntry", new Vector3(-2, 0, -8.75f), 3, 0);
            DoorRepair("JunkEntry", new Vector3(-9.75f, 0, 0), 3, 90);
            DoorRepair("RestroomEntry", new Vector3(2.78f, 0, 11), 1.5f, 90);
            // 각 방의 벽에 덧댄 조각을 불규칙하게 배치한다. 출입구와 통로 경계에는 배치하지 않는다.
            for (int i = 0; i < 6; i++)
            {
                Box(art, "Dorm_PeeledPatch_" + i, new Vector3(-8.5f + i * 2.7f, 1.3f + (i % 3) * .6f, 17.48f), new Vector3(1.1f + i % 2 * .5f, .8f, .05f), i % 2 == 0 ? "V4_Wood_Old" : "V4_Wall_PeeledPlaster", false, new Vector3(0, 0, -9 + i * 3));
                Box(art, "Props_WallRepair_" + i, new Vector3(-6.4f + i * 2.5f, 2.2f + i % 2 * .5f, -17.29f), new Vector3(1.6f, .65f, .06f), "V4_Ceiling_RustyMetal", false, new Vector3(0, 0, 7 - i * 3));
            }
            Box(art, "Restroom_LeaningPatch", new Vector3(8.65f, 2.2f, 14.5f), new Vector3(.06f, 2.6f, 1.8f), "V4_Partition_RustGreen", false, new Vector3(2, 0, -3));
            foreach (var p in new[] { new Vector3(-8.7f, 3.9f, 13.3f), new Vector3(-3.1f, 3.95f, 13.3f), new Vector3(-5.7f, 4.1f, -13.1f), new Vector3(2.5f, 4.1f, -13.1f), new Vector3(-19.5f, 4.4f, 0) })
            {
                var pipe = Clone(art, "RustPipe", Src("대기실_본관/배관/SM_BigPipe"), p, 0, .8f);
                var b = Bounds(pipe);
                if (b.size.y > b.size.x && b.size.y > b.size.z) pipe.rotation = Quaternion.Euler(90, 0, 0) * pipe.rotation;
                else if (b.size.x > b.size.z) pipe.rotation = Quaternion.Euler(0, 90, 0) * pipe.rotation;
                b = Bounds(pipe); pipe.localScale *= Mathf.Min(1, 7f / b.size.z);
                b = Bounds(pipe); pipe.position += g.TransformPoint(p) - b.center;
                foreach (var r in Renderers(pipe)) r.sharedMaterial = Mat("V4_RustyMetal");
            }
        }
        static void Canopy(string name, Vector3 p, float w, float d, float sag)
        {
            var root = Group(art, name + "_PatchedCeiling");
            Cloth(root, "Canvas", p, w, d, sag, name == "Restroom" ? "V4_Ceiling_RustyMetal" : "V5_Rag_Canvas_Cloth", Quaternion.identity);
            for (int i = 0; i < 3; i++)
            {
                float z = p.z - d * .42f + i * d * .42f;
                Box(root, "SaggingBeam_" + i, new Vector3(p.x, p.y - .08f + i * .04f, z), new Vector3(w, .14f, .18f), "V4_RustyMetal", false, new Vector3(0, 0, i % 2 == 0 ? 1 : -1.5f));
            }
        }
        static void DoorRepair(string name, Vector3 p, float width, float yaw)
        {
            var root = Group(art, name + "_CrookedRepairFrame");
            root.localPosition = p; root.localRotation = Quaternion.Euler(0, yaw, 0);
            for (int i = 0; i < 2; i++)
            {
                var t = Box(root, "PatchedJamb_" + i, Vector3.zero, new Vector3(.12f, 3.25f, .16f), "V4_Wood_Old", false);
                t.localPosition = new Vector3((i == 0 ? -1 : 1) * (width / 2 + .14f), 1.6f, 0);
                t.localRotation = Quaternion.Euler(0, 0, i == 0 ? 2 : -1.4f);
            }
            var beam = Box(root, "SlantedLintel", Vector3.zero, new Vector3(width + .5f, .23f, .18f), "V4_RustyMetal", false);
            beam.localPosition = new Vector3(0, 3.2f, 0); beam.localRotation = Quaternion.Euler(0, 0, -2);
        }

        static void ClutterAndMood()
        {
            Mute("Mood/Furnishings/Dormitory/BedsideTrunk_1");
            Mute("Mood/Furnishings/Dormitory/BedsideTrunk_2");
            for (int i = 0; i < 4; i++)
            {
                float x = new[] { -8.4f, -6.4f, -3.2f, -1.2f }[i];
                AtBottom("Mood/Furnishings/Dormitory/Bed_" + (i + 1) + "_Bunk", new Vector3(x, 0, 16.25f));
                Clone(art, "Dorm_PersonalTrunk_" + i, Src("대기실_본관/잡동사니/트렁크"), new Vector3(x, 0, 14.9f), -6 + i * 5, .65f, true);
                Cloth(art, "Dorm_HangingBlanket_" + i, new Vector3(x + .35f, 1.35f, 15.75f), .65f, .85f, .07f, i % 2 == 0 ? "V4_Blanket_DullRed" : "V4_Blanket_Olive", Quaternion.Euler(90, 0, 5));
            }
            var stash = new[] { new Vector3(-8.8f, 0, -7.6f), new Vector3(7.8f, 0, 7.5f), new Vector3(-20.5f, 0, -7.5f), new Vector3(-12.3f, 0, 7.7f), new Vector3(11.1f, 0, 11.6f) };
            for (int i = 0; i < stash.Length; i++)
            {
                Clone(art, "CircusStorageCrate_" + i, Src("대기실_본관/잡동사니/SM_MET_WoodenCrate01a_c1_LOD0"), stash[i], 9 + i * 13, .8f, true);
                Clone(art, "StorageTrunk_" + i, Src("대기실_본관/잡동사니/트렁크"), stash[i] + new Vector3(.65f, 0, .55f), -14 + i * 9, .75f, true);
                Clone(art, "CanvasOnCrate_" + i, Src("분장실/SM_MET_TangledCloth02_c1_LOD1"), stash[i] + new Vector3(0, .7f, 0), i * 25, .55f);
            }
            Point("Lobby_WarmPractical", new Vector3(-6, 2.9f, -5.5f), new Color(1, .65f, .32f), 3.5f, 6);
            Point("Dorm_WarmPractical", new Vector3(-7.5f, 3.25f, 12.6f), new Color(1, .68f, .4f), 3, 5);
            Point("Prop_CoolAmbient", new Vector3(-3, 3.55f, -12), new Color(.5f, .69f, 1), 3, 7);
            Point("Junk_ArchiveWarm", new Vector3(-18.2f, 3.3f, 6.3f), new Color(1, .64f, .34f), 3, 5);
            Dust("Dust_Dormitory", new Vector3(-5, 3, 13), new Vector3(9, 2, 6));
            Dust("Dust_Props", new Vector3(-2, 3, -13), new Vector3(10, 2, 5));
            Dust("Dust_Lobby", new Vector3(-4, 4.4f, 1), new Vector3(8, 2, 10));
        }

        static void Dust(string name, Vector3 p, Vector3 size)
        {
            var original = g.Find("Mood/Fog").GetComponentsInChildren<ParticleSystem>(true).FirstOrDefault();
            if (!original) return;
            var copy = Object.Instantiate(original.gameObject, art); Undo.RegisterCreatedObjectUndo(copy, "국소 먼지");
            copy.name = name; copy.transform.localPosition = p; copy.transform.localScale = Vector3.one;
            var ps = copy.GetComponent<ParticleSystem>(); var main = ps.main;
            main.startSize = new ParticleSystem.MinMaxCurve(.015f, .035f); main.startSpeed = .025f;
            main.startLifetime = 10; main.maxParticles = 100;
            main.startColor = new Color(.66f, .68f, .65f, .13f);
            var emission = ps.emission; emission.rateOverTime = 5;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = size;
            copy.SetActive(true);
        }
        static void Point(string name, Vector3 p, Color color, float intensity, float range)
        {
            var t = Group(art, name); t.localPosition = p;
            var l = Undo.AddComponent<Light>(t.gameObject); l.type = LightType.Point; l.color = color; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
        }
        static void Cloth(Transform parent, string name, Vector3 p, float w, float d, float sag, string material, Quaternion rotation)
        {
            const int nx = 16, nz = 12; var vs = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
            for (int z = 0; z <= nz; z++) for (int x = 0; x <= nx; x++)
            {
                float u = (float)x / nx, v = (float)z / nz;
                float dip = Mathf.Sin(u * Mathf.PI) * Mathf.Sin(v * Mathf.PI) * sag;
                float ripple = Mathf.Sin(u * 37 + v * 5) * .045f * Mathf.Sin(v * Mathf.PI);
                vs.Add(new Vector3((u - .5f) * w, -dip + ripple, (v - .5f) * d)); uv.Add(new Vector2(u * w * .5f, v * d * .5f));
                if (x < nx && z < nz) { int a = z * (nx + 1) + x, b = a + nx + 1; tr.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 }); }
            }
            var mesh = new Mesh { name = name + "_" + w + "_" + d }; mesh.SetVertices(vs); mesh.SetUVs(0, uv); mesh.SetTriangles(tr, 0); mesh.RecalculateNormals();
            DoubleSided(mesh); mesh.RecalculateBounds();
            var t = Group(parent, name); t.position = g.TransformPoint(p); t.rotation = g.rotation * rotation;
            Undo.AddComponent<MeshFilter>(t.gameObject).sharedMesh = Persist(mesh);
            Undo.AddComponent<MeshRenderer>(t.gameObject).sharedMaterial = Mat(material);
        }
        static Mesh Persist(Mesh mesh)
        {
            if (!AssetDatabase.LoadMainAssetAtPath(MeshPath)) AssetDatabase.CreateAsset(new Mesh { name = "InteriorArtMeshes" }, MeshPath);
            AssetDatabase.AddObjectToAsset(mesh, MeshPath); return mesh;
        }
        static void DoubleSided(Mesh mesh)
        {
            var vs = mesh.vertices; var ns = mesh.normals; var uv = mesh.uv; var ts = mesh.triangles; int n = vs.Length;
            mesh.vertices = vs.Concat(vs).ToArray(); mesh.normals = ns.Concat(ns.Select(v => -v)).ToArray(); mesh.uv = uv.Concat(uv).ToArray();
            var back = new List<int>(); for (int i = 0; i < ts.Length; i += 3) back.AddRange(new[] { ts[i + 2] + n, ts[i + 1] + n, ts[i] + n });
            mesh.triangles = ts.Concat(back).ToArray();
        }
        static void SuitDrape(Transform root, Vector3 p)
        {
            var vs = new List<Vector3>(); var uv = new List<Vector2>(); var ts = new List<int>();
            const int n = 24;
            for (int row = 0; row < 7; row++) for (int i = 0; i <= n; i++)
            {
                float a = (float)i / n * Mathf.PI * 2, v = row / 6f;
                float radius = Mathf.Lerp(.24f, .42f, v) + Mathf.Sin(a * 8) * .035f;
                vs.Add(new Vector3(Mathf.Cos(a) * radius, 1.4f - v * 1.1f + (row == 6 ? Mathf.Sin(a * 7) * .08f : 0), Mathf.Sin(a) * radius * .67f)); uv.Add(new Vector2(i / 12f, v));
                if (row < 6 && i < n) { int b = row * (n + 1) + i; ts.AddRange(new[] { b, b + 1, b + n + 1, b + 1, b + n + 2, b + n + 1 }); }
            }
            var mesh = new Mesh { name = "PleatedCostumeDrape" }; mesh.SetVertices(vs); mesh.SetUVs(0, uv); mesh.SetTriangles(ts, 0); mesh.RecalculateNormals(); DoubleSided(mesh); mesh.RecalculateBounds();
            var t = Group(root, "PleatedCostume"); t.position = g.TransformPoint(p);
            Undo.AddComponent<MeshFilter>(t.gameObject).sharedMesh = Persist(mesh); Undo.AddComponent<MeshRenderer>(t.gameObject).sharedMaterial = Mat("V4_Harlequin");
        }
        static Transform Clone(Transform parent, string name, Transform src, Vector3 at, float yaw = 0, float scale = 1, bool solid = false) => Clone(parent, name, new[] { src }, at, yaw, scale, solid);
        static Transform Clone(Transform parent, string name, Transform[] sources, Vector3 at, float yaw = 0, float scale = 1, bool solid = false)
        {
            var root = Group(parent, name); root.position = Vector3.zero;
            foreach (var src in sources)
            {
                var copy = Object.Instantiate(src.gameObject, root); Undo.RegisterCreatedObjectUndo(copy, "실내 소품 복제");
                copy.transform.SetPositionAndRotation(src.position, src.rotation); copy.transform.localScale = src.lossyScale; copy.SetActive(true);
                foreach (var c in copy.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                foreach (var c in copy.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(c);
                foreach (var c in copy.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(c);
                foreach (var c in copy.GetComponentsInChildren<TextMesh>(true)) Object.DestroyImmediate(c.gameObject);
            }
            var b = Bounds(root); var offset = new Vector3(b.center.x, b.min.y, b.center.z);
            foreach (Transform t in root) t.position -= offset;
            root.localRotation = Quaternion.Euler(0, yaw, 0); root.localScale = Vector3.one * scale; root.position = g.TransformPoint(at);
            if (solid)
            {
                b = Bounds(root); var localBounds = new Bounds(root.InverseTransformPoint(b.center), Vector3.zero);
                for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                    localBounds.Encapsulate(root.InverseTransformPoint(b.center + Vector3.Scale(b.extents, new Vector3(x, y, z))));
                var c = Undo.AddComponent<BoxCollider>(root.gameObject); c.center = localBounds.center; c.size = localBounds.size;
            }
            return root;
        }
        static Transform Box(Transform parent, string name, Vector3 at, Vector3 size, string material, bool solid, Vector3 rotation = default)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); Undo.RegisterCreatedObjectUndo(go, "급조 구조 배치");
            go.name = name; go.transform.SetParent(parent, false); go.transform.position = g.TransformPoint(at); go.transform.localScale = size; go.transform.rotation = g.rotation * Quaternion.Euler(rotation);
            go.GetComponent<Renderer>().sharedMaterial = Mat(material);
            if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>()); return go.transform;
        }
        static Material Mat(string name) => source.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).First(m => m && m.name == name);
        static Transform Src(string path) => source.Find(path) ?? throw new InvalidOperationException("원본 없음 " + path);
        static Transform Kit(string path) => kit.Find(path) ?? throw new InvalidOperationException("원본 없음 " + path);
        static Transform Find(string path) => g.Find(path);
        static string Path(Transform t) => AnimationUtility.CalculateTransformPath(t, g);
        static Vector3 Local(Transform t) => g.InverseTransformPoint(t.position);
        static Renderer[] Renderers(Transform t) => t.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer) && !r.GetComponent<TextMesh>()).ToArray();
        static Bounds Bounds(Transform t) { var rs = Renderers(t); var b = rs[0].bounds; foreach (var r in rs.Skip(1)) b.Encapsulate(r.bounds); return b; }
        static Transform Group(Transform p, string n) { var go = new GameObject(n); Undo.RegisterCreatedObjectUndo(go, "실내 아트"); go.transform.SetParent(p, false); return go.transform; }
        static void MoveDelta(Transform t, Vector3 delta) { if (!t) return; Undo.RecordObject(t, "실내 압축"); t.position += g.TransformVector(delta); }
        static void MoveTo(Transform t, Vector3 at) { if (!t) return; Undo.RecordObject(t, "실내 배치"); t.position = g.TransformPoint(at); }
        static void AtBottom(string path, Vector3 at) { var t = Find(path); if (!t) return; var b = Bounds(t); MoveDelta(t, at - g.InverseTransformPoint(new Vector3(b.center.x, b.min.y, b.center.z))); }
        static void Mute(string path) { var t = Find(path); if (!t) return; Undo.RecordObject(t.gameObject, "교체 대상 숨김"); t.gameObject.SetActive(false); }
        static void SetBox(string path, Vector3 p, Vector3 size) { var t = Find(path) ?? throw new InvalidOperationException(path); MoveTo(t, p); Undo.RecordObject(t, "실내 치수"); t.localScale = size; }
        static void Zone(string name, Vector3 p, Vector3 size) { var t = Find("CameraZones/" + name); if (!t) return; MoveTo(t, p); var c = t.GetComponent<BoxCollider>(); Undo.RecordObject(c, "카메라 구역 압축"); c.size = size; }
        static string ProtectedSignature() => Signature(protectedPaths.Select(p => Find(p)).Where(t => t));
        static string Signature(IEnumerable<Transform> roots)
        {
            var s = new StringBuilder();
            foreach (var root in roots) foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                s.Append(t.GetInstanceID()).Append(t.name).Append(t.gameObject.activeSelf).Append(EditorJsonUtility.ToJson(t));
                foreach (var c in t.GetComponents<Component>()) if (c && !(c is Transform)) s.Append(EditorJsonUtility.ToJson(c));
            }
            return Hash128.Compute(s.ToString()).ToString();
        }
    }
}
