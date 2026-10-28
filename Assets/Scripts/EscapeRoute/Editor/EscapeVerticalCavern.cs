using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        const string VerticalGroup = "16_VerticalFacility";

        // A-only revision, authored in world metres. Legacy geometry stays inactive for comparison.
        [MenuItem("Tools/CurtainCall/탈출로/A 첫 공동 수직 설비실 재생성")]
        public static void BuildVerticalCavern()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("저장된 A 씬 편집 모드가 필요합니다.");
            var background = scene.GetRootGameObjects().First(g => g.name == RootName).transform;
            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("A vertical facility");
            foreach (string name in new[] { "03_CannonRopeVoid_50x25", RevealGroup })
            {
                var previous = background.Find(name);
                if (previous != null) { Undo.RecordObject(previous.gameObject, "Preserve previous cavern"); previous.gameObject.SetActive(false); }
            }
            // Remove only old first-cavern ceilings, dressing and scale markers from the active view.
            foreach (string name in new[] { "10_Ceilings_ToggleForPlanView", "11_DepthAndLandmarks", "12_ConcreteLighting", "13_PlayerScaleReferences" })
            {
                var parent = background.Find(name);
                if (parent == null) continue;
                foreach (Transform child in parent)
                {
                    var renderer = child.GetComponentsInChildren<Renderer>().FirstOrDefault();
                    Vector3 p = renderer != null ? renderer.bounds.center : child.position;
                    if (p.x < 22.5f || p.x > 73 || p.z < 62.4f || p.z > 114) continue;
                    Undo.RecordObject(child.gameObject, "Preserve superseded cavern dressing");
                    child.gameObject.SetActive(false);
                }
            }
            var old = background.Find(VerticalGroup);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var g = new GameObject(VerticalGroup).transform;
            Undo.RegisterCreatedObjectUndo(g.gameObject, "Vertical facility");
            g.SetParent(background, false);
            g.localScale = new Vector3(1 / background.lossyScale.x, 1 / background.lossyScale.y, 1 / background.lossyScale.z);
            var wall = AssetDatabase.LoadAssetAtPath<Material>(Materials + "escape-V4_Wall_DamagedConcrete.mat");
            var floor = AssetDatabase.LoadAssetAtPath<Material>(Materials + "escape-V4_Floor_WetConcrete.mat");
            var steel = AssetDatabase.LoadAssetAtPath<Material>(Materials + "ref-M_Crane_Beam.mat");
            var bulb = AssetDatabase.LoadAssetAtPath<Material>(Materials + "concrete-service-bulb.mat");
            var depth = RevealMaterial("vertical-depth-concrete", wall, new Color(.38f, .40f, .42f));
            var machine = RevealMaterial("vertical-machine", steel, new Color(.27f, .30f, .31f));
            var darkSteel = RevealMaterial("vertical-dark-steel", steel, new Color(.12f, .145f, .16f));
            var shell = VerticalChild(g, "01_Shell_50x50_134m");
            var route = VerticalChild(g, "02_Route_0_to_minus6_to_plus2");
            var plant = VerticalChild(g, "03_MainPlant");
            var layers = VerticalChild(g, "04_ServiceLevels");
            var lightGroup = VerticalChild(g, "05_Lighting");

            // Continuous envelope; the northward extension reveals a second depth beyond the route.
            RevealBlock(shell, "북측 외벽", new Vector3(47.5f, -23, 113.25f), new Vector3(53, 134, 1.5f), depth);
            RevealBlock(shell, "동측 외벽", new Vector3(73.25f, -23, 87.5f), new Vector3(1.5f, 134, 53), wall);
            RevealBlock(shell, "서측 하부 외벽", new Vector3(21.75f, -45, 87.5f), new Vector3(1.5f, 90, 53), depth);
            RevealBlock(shell, "서측 상부 외벽", new Vector3(21.75f, 24, 87.5f), new Vector3(1.5f, 40, 53), wall);
            RevealBlock(shell, "입구 남측 벽", new Vector3(21.75f, 2, 67.25f), new Vector3(1.5f, 4, 9.5f), wall);
            RevealBlock(shell, "입구 북측 벽", new Vector3(21.75f, 2, 95.25f), new Vector3(1.5f, 4, 34.5f), wall);
            RevealBlock(shell, "남측 하부 외벽", new Vector3(47.5f, -44, 61.75f), new Vector3(50, 92, 1.5f), depth);
            RevealBlock(shell, "남측 상부 외벽", new Vector3(47.5f, 25, 61.75f), new Vector3(50, 38, 1.5f), wall);
            RevealBlock(shell, "남측 출구 옆벽", new Vector3(42.25f, 4, 61.75f), new Vector3(39.5f, 4, 1.5f), wall);
            RevealBlock(shell, "밀폐 천장", new Vector3(47.5f, 44.6f, 87.5f), new Vector3(53, 1.2f, 53), wall);
            RevealBlock(shell, "90m 아래 집수 바닥", new Vector3(47.5f, -90.5f, 87.5f), new Vector3(50, 1, 50), depth);
            // Close the approach's north end, leaving the existing right turn into the new aperture.
            RevealBlock(shell, "진입로 끝막이", new Vector3(15, 14, 85), new Vector3(16, 28, 1.25f), wall);
            RevealBlock(shell, "3.6m 진입 압축 천장", new Vector3(24.5f, 4.3f, 75), new Vector3(7, 1.4f, 6), depth);

            VerticalDeck(route, "진입 캔틸레버", 22.5f, 32, 70, 82, 0, floor, steel);
            VerticalDeck(route, "6m 아래 고립 정비 발판", 44, 52, 71, 80, -6, floor, steel);
            VerticalDeck(route, "아래층 도착부", 62, 72.5f, 79, 88, -6, floor, steel);
            VerticalDeck(route, "기존 04 연결 착지", 62, 72.5f, 62.5f, 65, 2, floor, steel);
            foreach (float z in new[] { 71f, 81f })
                VerticalStrut(route, "진입 발판 경사 지지", new Vector3(22.5f, -7, z), new Vector3(31, -1, z), .75f, steel);
            foreach (float z in new[] { 80f, 87f })
                VerticalStrut(route, "도착 발판 경사 지지", new Vector3(72.4f, -14, z), new Vector3(62.5f, -7, z), .8f, steel);
            foreach (float x in new[] { 65f, 71.8f })
                VerticalStrut(route, "계단 구조 거더", new Vector3(x, -6.6f, 79), new Vector3(x, 1.4f, 65), .65f, steel);
            // Forty 0.2m risers restore the original +2m exit without changing section 04.
            for (int i = 0; i < 40; i++)
            {
                float y = -6 + (i + 1) * .2f;
                RevealBlock(route, "정비 계단 " + (i + 1), new Vector3(68.5f, y - .18f, 79 - (i + .5f) * .35f),
                    new Vector3(8, .36f, .35f), floor);
            }
            RevealBlock(route, "대포 설치 자리", new Vector3(29, .3f, 75), new Vector3(3, .6f, 3), darkSteel);
            VerticalCable(route, "외줄 연결 시각 표시", new Vector3(51, -5, 77), new Vector3(63, -5, 82), .055f, steel);
            foreach (float z in new[] { 70f, 82f }) RevealRail(route, new Vector3(23, 0, z), new Vector3(31, 0, z), steel);
            RevealRail(route, new Vector3(44, -6, 71), new Vector3(52, -6, 71), steel);
            RevealRail(route, new Vector3(44, -6, 80), new Vector3(50, -6, 80), steel);
            RevealRail(route, new Vector3(63, -6, 88), new Vector3(72, -6, 88), steel);
            VerticalCable(route, "계단 내측 손잡이", new Vector3(64.5f, -4.9f, 79), new Vector3(64.5f, 3.1f, 65), .07f, steel);
            for (int i = 0; i <= 10; i++)
                VerticalCable(route, "계단 난간 기둥", new Vector3(64.5f, -6 + i * .8f, 79 - i * 1.4f), new Vector3(64.5f, -4.9f + i * .8f, 79 - i * 1.4f), .06f, steel);

            // A single immense vertical assembly, with buttresses and pipes that explain its support.
            RevealBlock(plant, "주 설비 콘크리트 기초", new Vector3(49, -84, 94), new Vector3(20, 12, 22), wall);
            for (int i = 0; i < 6; i++)
            {
                float y = -78 + i * 20;
                VerticalCylinder(plant, "주 설비 원통 " + i, new Vector3(49, y + 10, 94), 12, 20, machine);
                VerticalCylinder(plant, "주 설비 플랜지 " + i, new Vector3(49, y, 94), 13.2f, .6f, darkSteel);
                for (int b = 0; b < 16; b++)
                {
                    float a = b * Mathf.PI / 8;
                    RevealBlock(plant, "플랜지 체결부", new Vector3(49 + Mathf.Cos(a) * 6.35f, y + .45f, 94 + Mathf.Sin(a) * 6.35f), new Vector3(.22f, .35f, .22f), steel, false, false);
                }
            }
            foreach (float x in new[] { 37.5f, 60.5f })
            {
                RevealBlock(plant, "설비 지지 거대 교각", new Vector3(x, -22, 100.5f), new Vector3(4, 132, 9), wall);
                RevealAsset(plant, "수직 공급 주관", Industrial + "SM_Pipe_Big_01.prefab", new Vector3(x, -76, 107), new Vector3(3, 116, 3), steel, Quaternion.Euler(0, 0, 90));
                foreach (float y in new[] { -66f, -42f, -18f, 18f })
                    RevealAsset(plant, "벽에서 주 설비로 이어지는 집관", Industrial + "SM_Pipe_Big_01.prefab", new Vector3(x, y, 99), new Vector3(2, 2, 23), steel, Quaternion.Euler(0, 90, 0));
            }
            RevealBlock(plant, "상부 하중 전달 보", new Vector3(49, 32, 100.5f), new Vector3(28, 6, 9), wall);
            foreach (float z in new[] { 80f, 108f })
            {
                RevealAsset(plant, "천장 크레인 주행 레일", Industrial + "SM_Crane_Beam.prefab", new Vector3(47.5f, 39, z), new Vector3(49, 1.6f, 1.2f), steel);
            }
            RevealAsset(plant, "천장 반출 크레인 횡보", Industrial + "SM_Crane_Beam.prefab", new Vector3(58, 37, 94), new Vector3(1.8f, 1.6f, 30), steel, Quaternion.Euler(0, 90, 0));
            VerticalCable(plant, "권상 체인", new Vector3(58, 38, 85), new Vector3(58, 12, 85), .12f, darkSteel);
            RevealAsset(plant, "권상 블록", Industrial + "SM_Pipe_Big_Ring_01.prefab", new Vector3(58, 11, 85), new Vector3(.5f, 1.5f, 1.5f), steel);

            foreach (float y in new[] { 16f, -18f, -42f, -68f })
            {
                var level = VerticalChild(layers, "점검층 " + y + "m");
                RevealAsset(level, "후면 점검 갤러리", Industrial + "SM_Scaffolding_Flooring.prefab", new Vector3(47.5f, y - .25f, 110.5f), new Vector3(45, .25f, 2), steel);
                RevealRail(level, new Vector3(25, y, 109.5f), new Vector3(70, y, 109.5f), steel);
                RevealAsset(level, "주 설비 전면 점검대", Industrial + "SM_Scaffolding_Flooring.prefab", new Vector3(49, y - .25f, 86.5f), new Vector3(17, .25f, 2), steel);
                RevealRail(level, new Vector3(41, y, 85.5f), new Vector3(57, y, 85.5f), steel);
                foreach (float x in new[] { 41.5f, 56.5f })
                    RevealAsset(level, "설비 측면 점검대", Industrial + "SM_Scaffolding_Flooring.prefab", new Vector3(x, y - .25f, 98), new Vector3(2, .25f, 24), steel);
                RevealBlock(level, "사람 크기 닫힌 점검문", new Vector3(69.5f, y + 1.1f, 112.4f), new Vector3(1.05f, 2.2f, .1f), darkSteel, false, false);
                ContextSign(level, "층 표식", "S " + y.ToString("+00;-00;00"), new Vector3(67.5f, y + 2.4f, 112.3f), 0, 2);
                VerticalPractical(lightGroup, "주 설비 점검등 " + y, new Vector3(45, y + 3, 85), new Vector3(49, y, 87), 135, 21, 95, bulb);
                VerticalStrut(level, "점검등 지지대", new Vector3(45, y, 85.5f), new Vector3(45, y + 3, 85), .07f, steel);
                VerticalPractical(lightGroup, "후면 점검문 등 " + y, new Vector3(68.5f, y + 3, 109), new Vector3(69, y + 1, 112), 260, 12, 70, bulb);
            }
            // Small familiar fittings reveal scale without filling the concrete planes with clutter.
            RevealBlock(route, "도착부 점검함", new Vector3(72.2f, -4.9f, 84), new Vector3(.35f, 1.2f, .8f), steel, false, false);
            ContextSign(route, "출구 방향", "04  >", new Vector3(71.9f, -3.8f, 83), 90, 1.5f);
            foreach (float x in new[] { 30f, 67f })
                VerticalPractical(lightGroup, "벽체 수직 사광", new Vector3(x, 22, 76), new Vector3(x, -25, 64), 1600, 65, 52, bulb);
            VerticalPractical(lightGroup, "주 설비 상부 사광", new Vector3(31, 30, 83), new Vector3(49, 15, 94), 2400, 65, 65, bulb);
            VerticalPractical(lightGroup, "하부 설비 사광", new Vector3(32, -22, 78), new Vector3(49, -52, 94), 1800, 70, 55, bulb);
            VerticalPractical(lightGroup, "최하층 잔광", new Vector3(60, -68, 82), new Vector3(49, -84, 94), 800, 35, 65, bulb);
            VerticalPractical(lightGroup, "진입 바닥 조명", new Vector3(26, 3.3f, 74), new Vector3(30, 0, 75), 160, 12, 75, bulb);
            VerticalPractical(lightGroup, "고립 발판 조명", new Vector3(45, 2, 72), new Vector3(48, -6, 75), 350, 18, 58, bulb);
            VerticalPractical(lightGroup, "도착부 조명", new Vector3(70, 1, 85), new Vector3(66, -6, 83), 300, 18, 65, bulb);
            VerticalPractical(lightGroup, "상행 계단 조명", new Vector3(71, 7, 66), new Vector3(68, -1, 72), 350, 21, 70, bulb);
            var reference = background.Find("13_PlayerScaleReferences").Cast<Transform>()
                .First(t => t.name.StartsWith("현재 플레이어 모델", StringComparison.Ordinal));
            var person = UnityEngine.Object.Instantiate(reference.gameObject, route);
            Undo.RegisterCreatedObjectUndo(person, "Human scale reference");
            person.name = "ScaleReference_1.73m";
            person.transform.localScale = reference.lossyScale;
            person.transform.position = new Vector3(68, -6, 85);
            var body = person.GetComponentsInChildren<Renderer>().First(r => r.enabled);
            person.transform.position += Vector3.up * (-6 - body.bounds.min.y);
            EnsureReviewLabels(background.gameObject);
            AssetDatabase.SaveAssetIfDirty(font);
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undo);
        }

        static Transform VerticalChild(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            Undo.RegisterCreatedObjectUndo(child.gameObject, "Facility group");
            child.SetParent(parent, false);
            return child;
        }

        static void VerticalDeck(Transform p, string name, float x1, float x2, float z1, float z2, float y, Material slab, Material steel)
        {
            RevealBlock(p, name, new Vector3((x1 + x2) / 2, y - .45f, (z1 + z2) / 2), new Vector3(x2 - x1, .9f, z2 - z1), slab);
            foreach (float z in new[] { z1 + 1, z2 - 1 })
                RevealAsset(p, "발판 하중 보", Industrial + "SM_Crane_Beam.prefab", new Vector3((x1 + x2) / 2, y - 1.8f, z), new Vector3(x2 - x1, .9f, .7f), steel);
            if (name.Contains("고립"))
                foreach (float x in new[] { x1 + 1, x2 - 1 })
                    RevealBlock(p, "고립 정비대 지지각", new Vector3(x, -48.45f, (z1 + z2) / 2), new Vector3(1.3f, 83.1f, 1.8f), slab);
        }

        static void VerticalStrut(Transform p, string name, Vector3 a, Vector3 b, float width, Material material)
        {
            var go = RevealBlock(p, name, (a + b) / 2, new Vector3(width, (b - a).magnitude, width), material, false, false);
            go.transform.up = (b - a).normalized;
        }

        static void VerticalCylinder(Transform p, string name, Vector3 centre, float diameter, float height, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Undo.RegisterCreatedObjectUndo(go, "Facility vessel");
            go.name = name; go.transform.SetParent(p, false); go.transform.localPosition = centre;
            go.transform.localScale = new Vector3(diameter, height / 2, diameter);
            go.GetComponent<Renderer>().sharedMaterial = material;
            Undo.DestroyObjectImmediate(go.GetComponent<Collider>());
        }

        static void VerticalCable(Transform p, string name, Vector3 a, Vector3 b, float diameter, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Undo.RegisterCreatedObjectUndo(go, "Facility cable");
            go.name = name; go.transform.SetParent(p, false); go.transform.localPosition = (a + b) / 2;
            go.transform.up = (b - a).normalized; go.transform.localScale = new Vector3(diameter, (b - a).magnitude / 2, diameter);
            go.GetComponent<Renderer>().sharedMaterial = material;
            Undo.DestroyObjectImmediate(go.GetComponent<Collider>());
        }

        static void VerticalPractical(Transform p, string name, Vector3 source, Vector3 target, float intensity, float range, float angle, Material bulb)
        {
            RevealLight(p, name, source, target, intensity, range, angle);
            RevealBlock(p, name + " 광원", source, new Vector3(.6f, .12f, .18f), bulb, false, false);
        }
    }
}
