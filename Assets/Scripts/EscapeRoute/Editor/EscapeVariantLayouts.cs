using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        public const string VariantBPath = "Assets/Scenes/Levels/escape_route_b.unity";
        public const string VariantCPath = "Assets/Scenes/Levels/escape_route_c.unity";
        static string variantId;

        [MenuItem("Tools/CurtainCall/탈출로/B 구조 씬 제작 또는 재생성")]
        public static void BuildVariantB() => BuildVariant("B");
        [MenuItem("Tools/CurtainCall/탈출로/C 구조 씬 제작 또는 재생성")]
        public static void BuildVariantC() => BuildVariant("C");
        [MenuItem("Tools/CurtainCall/탈출로/B 구조 씬 열기")]
        public static void OpenVariantB() => OpenReviewScene(VariantBPath);
        [MenuItem("Tools/CurtainCall/탈출로/C 구조 씬 열기")]
        public static void OpenVariantC() => OpenReviewScene(VariantCPath);
        [MenuItem("Tools/CurtainCall/탈출로/A 구조 씬 열기")]
        public static void OpenVariantA() => OpenReviewScene(ScenePath);

        static void OpenReviewScene(string path)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("플레이 종료·현재 씬 저장 후 다른 구조 씬을 여세요.");
            EditorSceneManager.OpenScene(path);
        }

        static void BuildVariant(string id)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("플레이 종료·현재 씬 저장 후 구조를 제작하세요.");
            string path = id == "B" ? VariantBPath : VariantCPath;
            if (!File.Exists(path) && !AssetDatabase.CopyAsset(ScenePath, path))
                throw new InvalidOperationException("A의 미리보기 연결과 렌더 설정을 복사하지 못했습니다.");
            var scene = EditorSceneManager.OpenScene(path);
            variantId = id;
            var old = scene.GetRootGameObjects().First(g => g.name == RootName);
            // Preserve the existing sunny exterior, including source prefab references and scale.
            var oldOutside = old.transform.Cast<Transform>().First(t => t.name.StartsWith("08_Reeds", StringComparison.Ordinal));
            var exterior = UnityEngine.Object.Instantiate(oldOutside.gameObject, (Transform)null, true);
            exterior.name = "08_Reeds_30x37.5";
            UnityEngine.Object.DestroyImmediate(old);
            root = new GameObject(RootName).transform;
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Escape " + id + " background");
            exterior.transform.SetParent(root, true);
            // C approaches its final workroom from the north; its outdoor threshold is further north.
            exterior.transform.position = new Vector3(0, 0, id == "C" ? 32.5f : 0);
            LoadVariantMaterials();
            VGroup("00_Structure_" + id, Vector3.zero);
            BuildVariantStart(id);
            if (id == "B") BuildBMiddle(); else BuildCMiddle();
            BuildVariantEnd(id);
            EnsureReviewLabels(root.gameObject);
            ConfigureVariantPreview(id);
            VGroup("99_Ready_" + id, Vector3.zero);
            AssetDatabase.SaveAssetIfDirty(font);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            SceneView.lastActiveSceneView?.LookAtDirect(new Vector3(115, 0, 45), Quaternion.Euler(65, 0, 0), 135);
            Debug.Log("탈출로 " + id + " 구조 저장 완료. 묘기/협동/추격/런 선택은 구현하지 않은 배경 구조안입니다.");
        }

        static void LoadVariantMaterials()
        {
            font = GetFont();
            vWall = AssetDatabase.LoadAssetAtPath<Material>(Materials + "escape-V4_Wall_DamagedConcrete.mat");
            vFloor = AssetDatabase.LoadAssetAtPath<Material>(Materials + "escape-V4_Floor_WetConcrete.mat");
            vSteel = AssetDatabase.LoadAssetAtPath<Material>(Materials + "ref-M_Crane_Beam.mat");
            vFixture = AssetDatabase.LoadAssetAtPath<Material>(Materials + "concrete-service-fixture.mat");
            vBulb = AssetDatabase.LoadAssetAtPath<Material>(Materials + "concrete-service-bulb.mat");
            vDepth = RevealMaterial("cavern-depth-concrete", vWall, new Color(.11f, .125f, .14f));
            vPaint = Mat("service-marking", new Color(.46f, .49f, .47f));
            grey = AssetDatabase.LoadAssetAtPath<Material>(Materials + "placeholder.mat");
            iron = vSteel; concrete = vWall; wetFloor = vFloor;
        }

        static void BuildVariantStart(string id)
        {
            var start = VGroup("01_Understage_" + id, Vector3.zero);
            VPlatform(start, 0, 30, 0, 22.5f, 0);
            VWallX(start, 0, 0, 30, 12); VWallZ(start, 0, 0, 22.5f, 12);
            if (id == "B")
            {
                VWallZ(start, 30, 0, 22.5f, 12);
                VWallX(start, 22.5f, 0, 10, 12); VWallX(start, 22.5f, 25, 30, 12);
            }
            else
            {
                VWallX(start, 22.5f, 0, 30, 12);
                VWallZ(start, 30, 0, 6, 12); VWallZ(start, 30, 21, 22.5f, 12);
            }
            VCeiling(start, 0, 30, 0, 22.5f, 12);
            VBlock(start, "무대 리프트 하부", new Vector3(12, 9.5f, 10), new Vector3(12, 1, 10), vSteel, false);
            VService(start, new Vector3(3.5f, 0, 5), id + "-01");
            VLight(start, "하부 정비 조명", new Vector3(12, 8, 5), new Vector3(17, 0, 12), 190, 24, 85);
            VLabel(start, id + " 01 · 공연장 하부", new Vector3(15, 4, 12));

            if (id == "B")
            {
                var p = VGroup("02_Approach_B", new Vector3(10, 0, 22.5f));
                VPlatform(p, 0, 15, 0, 43.5f, 0); VWallZ(p, 0, 0, 43.5f, 28);
                VWallZ(p, 15, 0, 23.5f, 28); VWallX(p, 43.5f, 0, 15, 28);
                VCeiling(p, 0, 15, 0, 43.5f, 28);
                VBay(p, 1.5f, 8, 28, "B02"); VBay(p, 1.5f, 30, 28, "B03");
                VService(p, new Vector3(2.5f, 0, 18), "B-02");
                ContextSign(p, "다음 설비 방향", "B-03  >", new Vector3(7.5f, 2.6f, 43.1f), 0, 1.8f);
                VLabel(p, "B 02 · 긴 진입과 오른쪽 드러남", new Vector3(7.5f, 4, 15));
            }
            else
            {
                var p = VGroup("02_Approach_C", new Vector3(30, 0, 6));
                VPlatform(p, 0, 55, 0, 15, 0); VWallX(p, 0, 0, 55, 28);
                VWallX(p, 15, 0, 30, 28); VWallZ(p, 55, 0, 15, 28);
                VCeiling(p, 0, 55, 0, 15, 28);
                VBay(p, 10, 1.5f, 28, "C02"); VBay(p, 35, 1.5f, 28, "C03");
                VService(p, new Vector3(22, 0, 2), "C-02");
                VLight(p, "왼쪽 굽이의 반사광", new Vector3(48, 7, 10), new Vector3(45, 0, 14), 240, 20);
                ContextSign(p, "다음 설비 방향", "C-03", new Vector3(54.6f, 2.6f, 8), 90, 1.8f);
                VLabel(p, "C 02 · 가로 진입과 왼쪽 드러남", new Vector3(24, 4, 8));
            }
        }

        static void BuildBMiddle()
        {
            BuildVariantChamber("03_TrampolineSwing_B", new Vector3(25, 0, 46), 0, false, false, 0, 12, false);
            var bend = VGroup("04_Bend_B", new Vector3(62.5f, 0, 16));
            VPlatform(bend, 0, 20, 0, 30, 0); VWallZ(bend, 0, 0, 30, 28);
            VWallZ(bend, 20, 17, 30, 28); VWallX(bend, 0, 0, 20, 28);
            VCeiling(bend, 0, 20, 0, 30, 28); VService(bend, new Vector3(2, 0, 12), "B-04");
            VGate(bend, new Vector3(10, 0, 16), 20, 8, false);
            VBlock(bend, "진행 레버 위치", new Vector3(2.3f, .9f, 7), new Vector3(.6f, 1.5f, .6f), grey);
            VLabel(bend, "B 04 · 남하 굽이 / 진행 문과 지연 셔터", new Vector3(10, 4, 18));

            BuildVariantChamber("05_CannonRope_B", new Vector3(82.5f, 0, 8), 0, true, true, 4, 15, true);
            var regroup = VGroup("06_Regroup_B", new Vector3(120, 0, 33));
            for (int i = 0; i < 20; i++) VFloor(regroup, 0, 25, i * .6f, (i + 1) * .6f, 4 - i * .2f);
            VPlatform(regroup, 0, 25, 12, 40, 0);
            VWallZ(regroup, 0, 0, 40, 28); VWallZ(regroup, 25, 0, 20, 28);
            VWallX(regroup, 40, 0, 25, 28); VCeiling(regroup, 0, 25, 0, 40, 28);
            VBay(regroup, 2, 25, 28, "B06"); VPerson(regroup, new Vector3(12, 0, 26));
            VLabel(regroup, "B 06 · 긴 하강과 재합류", new Vector3(14, 5, 19));
        }

        static void BuildCMiddle()
        {
            BuildVariantChamber("03_CannonRope_C", new Vector3(85, 0, 21), -90, true, false, 2, 12.5f, true);
            var bend = VGroup("04_Bend_C", new Vector3(85, 0, 51));
            for (int i = 0; i < 12; i++) VFloor(bend, i, i + 1, 0, 20, 2 - i / 6f);
            VPlatform(bend, 12, 40, 0, 20, 0); VWallX(bend, 20, 0, 40, 28);
            VWallX(bend, 0, 0, 20, 28); VWallZ(bend, 40, 0, 20, 28);
            VCeiling(bend, 0, 40, 0, 20, 28); VService(bend, new Vector3(24, 0, 16), "C-04");
            VGate(bend, new Vector3(23, 0, 10), 20, 8, true);
            VBlock(bend, "진행 레버 위치", new Vector3(28, .9f, 17.8f), new Vector3(.6f, 1.5f, .6f), grey);
            VLabel(bend, "C 04 · 동쪽으로 꺾이는 하강 굽이", new Vector3(22, 4, 10));

            BuildVariantChamber("05_TrampolineSwing_C", new Vector3(105, 0, 51), 90, false, true, 0, 14, true);
            var regroup = VGroup("06_Regroup_C", new Vector3(142.5f, 0, 1));
            VPlatform(regroup, 0, 25, 0, 44, 0); VWallX(regroup, 0, 0, 25, 28);
            VWallZ(regroup, 25, 0, 44, 28); VWallZ(regroup, 0, 12.5f, 44, 28);
            VCeiling(regroup, 0, 25, 0, 44, 28);
            VBay(regroup, 2, 28, 28, "C06"); VPerson(regroup, new Vector3(14, 0, 22));
            VLabel(regroup, "C 06 · 반대편 착지와 북쪽 재합류", new Vector3(12, 4, 18));
        }

        static void BuildVariantEnd(string id)
        {
            var run = VGroup("07A_FinalRun_" + id, Vector3.zero);
            float exitZ;
            if (id == "B")
            {
                VPlatform(run, 145, 195, 53, 73, 0); VPlatform(run, 175, 195, 30, 53, 0);
                VWallX(run, 73, 145, 195, 18); VWallX(run, 53, 145, 175, 18);
                VWallZ(run, 195, 30, 73, 18); VWallZ(run, 175, 30, 53, 18);
                VCeiling(run, 145, 195, 53, 73, 18); VCeiling(run, 175, 195, 30, 53, 18);
                VService(run, new Vector3(156, 0, 69), "B-07"); exitZ = 0;
            }
            else
            {
                VPlatform(run, 147.5f, 167.5f, 45, 90, 0); VPlatform(run, 167.5f, 210, 70, 90, 0);
                VWallZ(run, 147.5f, 45, 90, 18); VWallZ(run, 167.5f, 45, 70, 18);
                VWallX(run, 90, 147.5f, 210, 18); VWallZ(run, 210, 70, 90, 18);
                VWallX(run, 70, 167.5f, 175, 18);
                VCeiling(run, 147.5f, 167.5f, 45, 90, 18); VCeiling(run, 167.5f, 210, 70, 90, 18);
                VService(run, new Vector3(151, 0, 66), "C-07"); exitZ = 32.5f;
            }
            VLight(run, "마지막 굽이의 목적지", new Vector3(id == "B" ? 183 : 193, 12, id == "B" ? 49 : 80),
                new Vector3(193, 0, id == "B" ? 34 : 70), 320, 28, 62);
            var hall = VGroup("07B_ExitWorkroom_" + id, new Vector3(175, 0, exitZ));
            float depth = id == "B" ? 30 : 37.5f;
            VPlatform(hall, 0, 35, 0, depth, 0); VWallZ(hall, 0, 0, depth, 20);
            VWallX(hall, 0, 0, 35, 20);
            if (id == "B") VWallX(hall, depth, 20, 35, 20);
            VWallZ(hall, 35, 0, 13, 20); VWallZ(hall, 35, 24.5f, depth, 20);
            VCeiling(hall, 0, 35, 0, depth, 20);
            VBlock(hall, "출구 상부 인방", new Vector3(35, 15, 18.75f), new Vector3(1.4f, 10, 11.5f), vWall);
            VBlock(hall, "출구 장치 작업 공간", new Vector3(20, .15f, 17), new Vector3(8, .3f, 8), grey);
            VBlock(hall, "출구 잠금 장치", new Vector3(34, 6, 18.75f), new Vector3(1, 2, 2), grey);
            VService(hall, new Vector3(3, 0, 7), id + "-EXIT");
            VLight(hall, "출구 쪽 작업 조명", new Vector3(24, 13, 19), new Vector3(31, 0, 19), 320, 23);
            VLabel(hall, id + " 07B · 후방 지연 / 중앙 작업 / 전방 출구", new Vector3(17, 5, 17));
        }

        static void ConfigureVariantPreview(string id)
        {
            var player = GameObject.Find("PlaySetup/Player").transform;
            player.SetPositionAndRotation(new Vector3(15, .05f, 8), Quaternion.Euler(0, id == "B" ? 0 : 90, 0));
            PrefabUtility.RecordPrefabInstancePropertyModifications(player);
            var camera = GameObject.Find("Main Camera");
            camera.transform.SetPositionAndRotation(player.position + Vector3.up * 1.5916f, player.rotation);
            if (camera.GetComponent<CurtainCall.EscapeRoute.EscapeRouteFlyCamera>() == null)
                throw new InvalidOperationException("A의 자유 비행 연결이 필요합니다.");
        }
    }
}
