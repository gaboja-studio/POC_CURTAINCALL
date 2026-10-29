using System;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        const string BrutalGroup = "17_BrutalistRoute";
        const string BrutalMeshes = "Assets/Scenes/Levels/EscapeRouteMeshes";
        static Material bConcrete, bRecess, bSteel, bBulb;

        [MenuItem("Tools/CurtainCall/탈출로/A 전체 브루탈리즘 구간 재생성")]
        public static void BuildBrutalistRoute()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("A 편집 모드가 필요합니다.");
            var background = GameObject.Find(RootName).transform;
            if (background.Find(VerticalGroup) == null) throw new InvalidOperationException("A 첫 수직 공동이 필요합니다.");
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("A brutalist route");
            foreach (string name in new[] { "01_WaitingUnderstage_30x22.5", "02_ConcreteApproach_15x60", "04_BendAndShutters_20x45",
                "05_SwingVoid_50x37.5", "06_Regroup_25x42.5", "07A_FinalRun_LBend", "07B_ExitHall_35x37.5",
                "10_Ceilings_ToggleForPlanView", "11_DepthAndLandmarks", "12_ConcreteLighting", "13_PlayerScaleReferences" })
            {
                var legacy = background.Find(name);
                if (legacy == null) continue;
                Undo.RecordObject(legacy.gameObject, "Preserve old A structure"); legacy.gameObject.SetActive(false);
            }
            var old = background.Find(BrutalGroup);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var g = VerticalChild(background, BrutalGroup);
            g.localScale = new Vector3(1 / background.lossyScale.x, 1 / background.lossyScale.y, 1 / background.lossyScale.z);
            if (!AssetDatabase.IsValidFolder(BrutalMeshes)) AssetDatabase.CreateFolder("Assets/Scenes/Levels", "EscapeRouteMeshes");
            var source = AssetDatabase.LoadAssetAtPath<Material>(Materials + "escape-V4_Wall_DamagedConcrete.mat");
            bConcrete = RevealMaterial("brutal-concrete", source, new Color(.53f, .54f, .54f));
            bRecess = RevealMaterial("brutal-recess", source, new Color(.26f, .28f, .29f));
            bSteel = RevealMaterial("brutal-steel", source, new Color(.12f, .14f, .15f));
            bBulb = AssetDatabase.LoadAssetAtPath<Material>(Materials + "concrete-service-bulb.mat");
            foreach (var material in new[] { bConcrete, bRecess, bSteel })
            {
                material.SetFloat("_Metallic", 0); material.SetFloat("_Smoothness", .025f); material.SetFloat("_BumpScale", .16f);
                EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
            }
            BrutalStart(VerticalChild(g, "01_Understage"));
            BrutalApproach(VerticalChild(g, "02_CompressionGallery"));
            BrutalFirstCavern(VerticalChild(g, "03_ConcreteContainment"), background.Find(VerticalGroup));
            BrutalBend(VerticalChild(g, "04_DeepServiceCut"));
            BrutalSwing(VerticalChild(g, "05_TransferChasm"));
            BrutalRegroup(VerticalChild(g, "06_AscendingWorks"));
            BrutalRun(VerticalChild(g, "07A_DispatchTrench"));
            BrutalExit(VerticalChild(g, "07B_FinalBulkheads"));
            var personSource = background.Find(VerticalGroup + "/02_Route_0_to_minus6_to_plus2/ScaleReference_1.73m");
            foreach (var feet in new[] { new Vector3(121, -8, 30), new Vector3(187, 0, 30) })
            {
                var person = UnityEngine.Object.Instantiate(personSource.gameObject, g);
                Undo.RegisterCreatedObjectUndo(person, "Human scale reference");
                person.name = "ScaleReference_1.73m"; person.transform.position = feet;
                var body = person.GetComponentsInChildren<Renderer>().First(r => r.enabled);
                person.transform.position += Vector3.up * (feet.y - body.bounds.min.y);
            }
            EnsureReviewLabels(background.gameObject);
            AssetDatabase.SaveAssetIfDirty(font);
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undo);
        }

        static GameObject BMass(Transform p, string name, Vector3 centre, Vector3 size, Material material = null, bool collision = true)
        {
            var go = RevealBlock(p, name, centre, size, material ?? bConcrete, false, collision);
            var filter = go.GetComponent<MeshFilter>();
            // Tile the existing concrete at physical scale, including the large vertical faces.
            string key = string.Join("x", new[] { size.x, size.y, size.z }.Select(n => n.ToString("0.###", CultureInfo.InvariantCulture)));
            string path = BrutalMeshes + "/block-" + key + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = UnityEngine.Object.Instantiate(filter.sharedMesh); mesh.name = "ConcreteBlock_" + key;
                var points = mesh.vertices; var normals = mesh.normals; var uv = mesh.uv;
                for (int i = 0; i < uv.Length; i++)
                {
                    var point = Vector3.Scale(points[i], size);
                    uv[i] = Mathf.Abs(normals[i].y) > .5f ? new Vector2(point.x, point.z) / 3 :
                        Mathf.Abs(normals[i].x) > .5f ? new Vector2(point.z, point.y) / 3 : new Vector2(point.x, point.y) / 3;
                }
                mesh.uv = uv; mesh.RecalculateTangents(); AssetDatabase.CreateAsset(mesh, path);
            }
            filter.sharedMesh = mesh; return go;
        }

        static void BFloor(Transform p, string name, float x1, float x2, float z1, float z2, float y, float thickness = 1.6f)
            => BMass(p, name, new Vector3((x1 + x2) / 2, y - thickness / 2, (z1 + z2) / 2), new Vector3(x2 - x1, thickness, z2 - z1));

        static void BLight(Transform p, Vector3 source, Vector3 target, float power = 400, float range = 24)
        {
            RevealLight(p, "매입 정비등", source, target, power, range, 85);
            p.GetChild(p.childCount - 1).GetComponent<Light>().innerSpotAngle = 22;
            BMass(p, "작업등 함체", source + Vector3.up * .08f, new Vector3(.8f, .25f, .35f), bSteel, false);
            RevealBlock(p, "작업등 광원", source, new Vector3(.55f, .08f, .18f), bBulb, false, false);
        }

        static void BWallX(Transform p, float z, float x1, float x2, float bottom, float top, float thickness = 2)
            => BMass(p, "타설 콘크리트 벽", new Vector3((x1 + x2) / 2, (bottom + top) / 2, z), new Vector3(x2 - x1, top - bottom, thickness));
        static void BWallZ(Transform p, float x, float z1, float z2, float bottom, float top, float thickness = 2)
            => BMass(p, "타설 콘크리트 벽", new Vector3(x, (bottom + top) / 2, (z1 + z2) / 2), new Vector3(thickness, top - bottom, z2 - z1));
        static void BPortalZ(Transform p, float z, float x1, float x2, float clear, float top, float thickness)
        {
            BMass(p, "두꺼운 문틀 좌측", new Vector3(x1 - 1.5f, top / 2, z), new Vector3(3, top, thickness));
            BMass(p, "두꺼운 문틀 우측", new Vector3(x2 + 1.5f, top / 2, z), new Vector3(3, top, thickness));
            BMass(p, "깊은 인방", new Vector3((x1 + x2) / 2, (clear + top) / 2, z), new Vector3(x2 - x1, top - clear, thickness));
        }

        static void BrutalStart(Transform p)
        {
            BFloor(p, "무대 하부 바닥", 0, 30, 0, 22.5f, 0);
            BWallX(p, -1, 0, 30, -12, 14); BWallZ(p, -1, 0, 22.5f, -12, 14); BWallZ(p, 31, 0, 22.5f, -12, 14);
            BWallX(p, 23, 0, 10, 0, 14); BWallX(p, 23, 20, 30, 0, 14);
            BMass(p, "무대 리프트 기초 천장", new Vector3(15, 11, 11), new Vector3(32, 6, 25));
            foreach (float z in new[] { 4f, 13f, 20f }) BMass(p, "무대 하중 전이 보", new Vector3(15, 6.7f, z), new Vector3(30, 3, 3.5f));
            foreach (float x in new[] { 4f, 26f }) BMass(p, "무대 하중 교각", new Vector3(x, 4, 10), new Vector3(5, 8, 15));
            BPortalZ(p, 21, 11, 19, 3.8f, 9, 3);
            BLight(p, new Vector3(7, 4.5f, 8), new Vector3(15, 0, 12), 280, 22);
            BLight(p, new Vector3(19, 3.4f, 21), new Vector3(15, 0, 24), 120, 12);
            ContextSign(p, "하부 작업 구역", "UNDERSTAGE  /  01", new Vector3(9.9f, 2.2f, 21), 0, 3);
        }

        static void BrutalApproach(Transform p)
        {
            BFloor(p, "진입 보행면", 10, 22.5f, 22.5f, 83, 0);
            BWallZ(p, 6.5f, 22.5f, 84, -18, 32); BWallZ(p, 23.5f, 22.5f, 62.5f, -18, 32);
            BFloor(p, "깊은 측면 배수로", 7.5f, 10, 22.5f, 83, -15);
            BMass(p, "긴 진입로 상부 덩어리", new Vector3(15, 31, 50), new Vector3(18, 4, 64));
            foreach (float z in new[] { 32f, 50f, 66f })
            {
                BMass(p, "엇갈린 거대 벽기둥", new Vector3(8.6f, 12, z), new Vector3(3.2f, 30, 6));
                BMass(p, "낮은 횡단 보", new Vector3(15, 7, z), new Vector3(18, 5, 5));
                BLight(p, new Vector3(20.5f, 4.2f, z - 2), new Vector3(14, 0, z + 4), 190, 18);
            }
            BMass(p, "상부의 막힌 설비 공간", new Vector3(18, 19, 45), new Vector3(8, 14, 11), bRecess);
            for (int i = 0; i < 4; i++) BMass(p, "배수로 가로 받침", new Vector3(8.5f, -7, 28 + i * 15), new Vector3(3, 1.4f, 2), bRecess);
            BLight(p, new Vector3(8, -5, 58), new Vector3(8.5f, -14, 66), 100, 15);
        }

        static void BrutalFirstCavern(Transform p, Transform original)
        {
            // Retain the approved depth and route; concrete cheeks partly bury the metallic vessel.
            foreach (var renderer in original.GetComponentsInChildren<Renderer>())
            {
                if (renderer.sharedMaterials.Any(m => m != null && (m.name.Contains("concrete") || m.name.Contains("DamagedConcrete"))))
                { Undo.RecordObject(renderer, "Matte first cavern"); renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => bConcrete).ToArray(); }
            }
            foreach (float x in new[] { 44.8f, 53.2f })
            {
                for (int i = 0; i < 6; i++)
                    BMass(p, "주 설비를 감싸는 타설 코어", new Vector3(x, -68 + i * 20, 94), new Vector3(6, 19.92f, 14));
            }
            for (int i = 0; i < 6; i++)
                BMass(p, "코어 깊은 수평 인방", new Vector3(49, -58 + i * 20, 89), new Vector3(14.4f, 2.8f, 4));
            BMass(p, "편심 상부 콘크리트 캔틸레버", new Vector3(39, 26, 93), new Vector3(17, 7, 17));
            BMass(p, "하부 사선 버팀벽", new Vector3(61, -54, 98), new Vector3(6, 54, 13), bRecess);
        }

        static void BrutalBend(Transform p)
        {
            BFloor(p, "04 상부 연결", 62, 72.5f, 60.5f, 62.5f, 2);
            for (int i = 0; i < 10; i++) BFloor(p, "04 하강 단 " + i, 64, 72, 60.5f - (i + 1) * .5f, 60.5f - i * .5f, 2 - (i + 1) * .2f, .5f);
            BFloor(p, "수로 옆 정비 선반", 64, 72, 17.5f, 55.5f, 0, 2.5f);
            BFloor(p, "다음 공동으로 꺾이는 통로", 72, 79, 22, 35, 0, 2.5f);
            BWallZ(p, 56.5f, 17.5f, 62.5f, -34, 34); BWallZ(p, 78.5f, 35, 62.5f, -34, 34);
            BWallZ(p, 78.5f, 17.5f, 22, -34, 34); BWallZ(p, 78.5f, 22, 35, 5, 34);
            BWallX(p, 16.5f, 57.5f, 77.5f, -34, 34);
            BMass(p, "수로 천장", new Vector3(67.5f, 35, 40), new Vector3(24, 4, 47));
            BFloor(p, "34m 하부 수로 바닥", 57.5f, 77.5f, 17.5f, 62.5f, -34);
            BPortalZ(p, 45, 64, 72, 4.2f, 24, 5);
            BMass(p, "측면의 거대 막힌 정비구", new Vector3(60, 10, 28), new Vector3(5, 20, 13));
            foreach (float z in new[] { 38f, 52f })
            {
                BMass(p, "정비 선반 하중 지지", new Vector3(63, -15, z), new Vector3(4, 30, 4));
                BLight(p, new Vector3(72, 6, z), new Vector3(68, 0, z - 5), 240, 21);
            }
            BMass(p, "수로 옆 낮은 콘크리트 흉벽", new Vector3(63.8f, .55f, 29), new Vector3(.4f, 1.1f, 20));
            BLight(p, new Vector3(59, -14, 48), new Vector3(60, -31, 39), 240, 30);
            BLight(p, new Vector3(74, 4.7f, 25), new Vector3(83, 0, 25), 200, 18);
            ContextSign(p, "하부 설비 방향", "05  >", new Vector3(67, 2.5f, 18), 180, 1.5f);
        }
    }
}
