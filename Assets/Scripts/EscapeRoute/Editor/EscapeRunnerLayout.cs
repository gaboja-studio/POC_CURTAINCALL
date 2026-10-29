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
        const string RunnerGroup = "19_RunnerEscape";

        [MenuItem("Tools/CurtainCall/탈출로/A 마지막 질주 260m 재생성")]
        public static void BuildRunnerEscape()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("A 씬의 편집 모드가 필요합니다.");
            var background = GameObject.Find(RootName).transform;
            var structure = background.Find(BrutalGroup);
            if (structure == null) throw new InvalidOperationException("A 브루탈리즘 구조가 필요합니다.");
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Long industrial escape run");
            bConcrete = RunnerMaterial("brutal-concrete"); bRecess = RunnerMaterial("brutal-recess");
            bSteel = RunnerMaterial("brutal-steel"); bBulb = RunnerMaterial("concrete-service-bulb");
            wornSteel = RunnerMaterial("working-steel"); wornPipe = RunnerMaterial("working-pipe");
            maintenancePaint = RunnerMaterial("working-faded-ochre"); patchConcrete = RunnerMaterial("working-concrete-repair");
            leakWear = RunnerMaterial("working-leak"); floorWear = RunnerMaterial("working-floor-dirt");
            var runWear = AssetDatabase.LoadAssetAtPath<Material>(Materials + "runner-floor-wear.mat");
            if (runWear == null)
            {
                runWear = new Material(floorWear) { name = "runner-floor-wear" };
                runWear.SetColor("_Tint", new Color(.12f, .14f, .13f, .88f));
                AssetDatabase.CreateAsset(runWear, Materials + "runner-floor-wear.mat");
            }
            floorWear = runWear;

            // Preserve the old route and its dressing, including the previous user's finish pass.
            foreach (var t in structure.Cast<Transform>().Where(t => t.name.StartsWith("07") || t.name.StartsWith("ScaleReference") && t.position.x > 140))
                RunnerDisable(t);
            foreach (string path in new[] { IndustrialFinishGroup + "/04_07_WornServiceAreas", IndustrialFinishGroup + "/04_RepairAndDistribution" })
            {
                var dressing = background.Find(path);
                if (dressing != null)
                    foreach (var t in dressing.Cast<Transform>().Where(t => t.position.x >= 140)) RunnerDisable(t);
            }
            var previous = background.Find(RunnerGroup);
            if (previous != null) Undo.DestroyObjectImmediate(previous.gameObject);
            var g = VerticalChild(background, RunnerGroup);
            g.localScale = new Vector3(1 / background.lossyScale.x, 1 / background.lossyScale.y, 1 / background.lossyScale.z);
            RunnerShell(VerticalChild(g, "00_ConcreteShell_260m"));
            RunnerAcceleration(VerticalChild(g, "01_RunUp_140_164"));
            RunnerCrossfire(VerticalChild(g, "02_SideDischarge_164_226"));
            RunnerDropBays(VerticalChild(g, "03_OverheadLoads_226_276"));
            RunnerSwingGap(VerticalChild(g, "04_SwingChasm_276_298"));
            RunnerLastRun(VerticalChild(g, "05_LastSprint_298_366"));
            RunnerExit(VerticalChild(g, "06_Exit_366_400"));
            RunnerSurfaceLife(VerticalChild(g, "07_WearRepairsAndServices"));

            var outside = background.Cast<Transform>().First(t => t.name.StartsWith("08_Reeds"));
            Undo.RecordObject(outside, "Align reeds with long escape exit");
            // Original outside begins at world X210 with centre Z18.75. Absolute offset avoids drift on rebuild.
            outside.position = new Vector3(190, 0, 59.25f);
            var personSource = background.Find(VerticalGroup + "/02_Route_0_to_minus6_to_plus2/ScaleReference_1.73m");
            foreach (var feet in new[] { new Vector3(153, 0, 81.5f), new Vector3(305, 0, 82), new Vector3(389, 0, 81.5f) })
            {
                var person = UnityEngine.Object.Instantiate(personSource.gameObject, g);
                Undo.RegisterCreatedObjectUndo(person, "Human scale reference");
                person.name = "ScaleReference_1.73m"; person.transform.position = feet;
                var body = person.GetComponentsInChildren<Renderer>().First(r => r.enabled);
                person.transform.position += Vector3.up * (feet.y - body.bounds.min.y);
            }
            EnsureReviewLabels(background.gameObject);
            if (font != null) AssetDatabase.SaveAssetIfDirty(font);
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undo);
        }

        static Material RunnerMaterial(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Materials + name + ".mat");
            if (material == null) throw new InvalidOperationException("필요한 A 재질이 없습니다: " + name);
            return material;
        }
        static void RunnerDisable(Transform t)
        {
            Undo.RecordObject(t.gameObject, "Preserve previous final run"); t.gameObject.SetActive(false);
        }
        static void RunnerAnchor(Transform p, string name, Vector3 position, Vector3 forward)
        {
            var t = VerticalChild(p, "Anchor_" + name); t.localPosition = position;
            t.localRotation = Quaternion.LookRotation(forward);
        }

        static void RunnerShell(Transform p)
        {
            BWallX(p, 55, 140, 400, -48, 54, 4);
            BWallX(p, 101, 140, 400, -48, 54, 4);
            BFloor(p, "48m 아래 설비동 바닥", 140, 400, 55, 101, -48, 3);
            // Separate masses give a low entry, a tall loading cathedral, then a compressed exit.
            BMass(p, "가속동 낮은 콘크리트 덮개", new Vector3(152, 19, 78), new Vector3(24, 4, 50));
            BMass(p, "측면 반출동 덮개", new Vector3(195, 34, 78), new Vector3(62, 4, 50));
            BMass(p, "상부 낙하 설비동 덮개", new Vector3(251, 48, 78), new Vector3(50, 4, 50));
            BMass(p, "그네 수직 공동 덮개", new Vector3(287, 55, 78), new Vector3(22, 4, 50));
            BMass(p, "마지막 반출동 덮개", new Vector3(332, 32, 78), new Vector3(68, 4, 50));
            BMass(p, "외벽 통과부 덮개", new Vector3(383, 17, 78), new Vector3(34, 4, 50));
            // Seal vertical roof steps so the facility stays enclosed instead of exposing strips of sky.
            foreach (var step in new[] { new Vector3(164, 17, 36), new Vector3(226, 32, 50), new Vector3(276, 46, 57), new Vector3(298, 30, 57), new Vector3(366, 15, 34) })
                BMass(p, "층고가 바뀌는 폐쇄 전이 벽", new Vector3(step.x, (step.y + step.z) / 2, 78), new Vector3(3, step.z - step.y, 50));
            BWallZ(p, 140, 55, 71, -48, 54); BWallZ(p, 140, 85, 101, -48, 54);
            BWallZ(p, 140, 71, 85, 6, 54);
            foreach (float x in new[] { 157f, 184f, 215f, 242f, 272f, 303f, 337f, 362f })
            {
                float top = x < 164 ? 17 : x < 226 ? 32 : x < 298 ? 46 : 30;
                BMass(p, "남측 하중 벽기둥", new Vector3(x, (top - 48) / 2, 61), new Vector3(5, top + 48, 9));
                BMass(p, "엇갈린 북측 하중 벽기둥", new Vector3(x + 4, (top - 48) / 2, 95), new Vector3(7, top + 48, 9));
                BMass(p, "거대 전이 보", new Vector3(x, top - 4, 78), new Vector3(5, 8, 38));
                if (x < 276 || x > 298)
                    BMass(p, "주행면 하부 교각", new Vector3(x, -25, 78), new Vector3(5, 45, 8));
                BMass(p, "하부 정비층 선반", new Vector3(x + 7, -23, 96), new Vector3(20, 1.4f, 4), bRecess, false);
                BMass(p, "작은 하부 점검문", new Vector3(x + 5, -21.9f, 98.8f), new Vector3(1, 2.2f, .15f), bSteel, false);
                BLight(p, new Vector3(x + 5, -19, 97), new Vector3(x + 7, -25, 92), 170, 24);
            }
            // Heavy longitudinal services stay below the track; their ends disappear into the facility.
            foreach (float z in new[] { 62f, 94f })
                ServicePipe(p, new Vector3(142, -13, z), new Vector3(395, -13, z), 1.4f);
        }

        static void RunnerAcceleration(Transform p)
        {
            BFloor(p, "06에서 이어지는 가속 바닥", 140, 164, 71, 85, 0, 2.5f);
            BMass(p, "압축 출발부 남측 기초", new Vector3(150, 4, 67), new Vector3(20, 8, 6));
            BMass(p, "압축 출발부 북측 기초", new Vector3(153, 5, 90), new Vector3(14, 10, 8));
            BMass(p, "출발부 두꺼운 낮은 인방", new Vector3(145, 8.5f, 78), new Vector3(8, 5, 22));
            BLight(p, new Vector3(151, 5.2f, 84), new Vector3(160, 0, 78), 460, 30);
            ContextSign(p, "반출구까지 거리", "OUT  /  260 m  >", new Vector3(143, 3.2f, 85.9f), 0, 4);
            RunnerAnchor(p, "RunStart", new Vector3(140, 0, 78), Vector3.right);
        }

        static void RunnerCrossfire(Transform p)
        {
            RunnerDeck(p, "측면 반출 질주면", 164, 226);
            RunnerDischargeBay(p, 176, true, "01");
            RunnerDischargeBay(p, 196, false, "02");
            RunnerDischargeBay(p, 216, true, "03");
            RunnerImpact(p, new Vector3(183, 0, 72.8f), false);
            RunnerImpact(p, new Vector3(204, 0, 83.2f), true);
            BLight(p, new Vector3(170, 6, 87), new Vector3(181, 0, 78), 650, 34);
            BLight(p, new Vector3(203, 6.8f, 68), new Vector3(215, 0, 79), 530, 32);
        }

        static void RunnerDropBays(Transform p)
        {
            RunnerDeck(p, "낙하 설비 아래 주행면", 226, 276);
            RunnerDropLoad(p, new Vector3(237, 0, 74), "01");
            RunnerDropLoad(p, new Vector3(253, 0, 82), "02");
            RunnerImpact(p, new Vector3(244, 0, 72.5f), true);
            RunnerImpact(p, new Vector3(258, 0, 83), false);
            // A damaged gantry is wedged over the course, leaving more than 5m headroom.
            var beam = BMass(p, "비틀려 걸린 반출 보", new Vector3(264, 9, 78), new Vector3(3, 3, 23), bRecess);
            beam.transform.localRotation = Quaternion.Euler(13, 0, 8);
            foreach (float z in new[] { 70f, 86f })
                VerticalCable(p, "끊어진 반출 보 지지선", new Vector3(264, 43, z), new Vector3(264, 11, z + 2), .11f, wornSteel);
            BLight(p, new Vector3(232, 6, 86), new Vector3(240, 0, 77), 620, 31);
            BLight(p, new Vector3(266, 5, 70), new Vector3(274, 0, 78), 600, 27);
            ContextSign(p, "그네 접근", "TRANSFER  /  04  >", new Vector3(267, 3.5f, 86), 0, 4);
        }

        static void RunnerSwingGap(Transform p)
        {
            // No hidden floor or collider across this gap. This is a stunt reservation, not a playable swing system.
            BMass(p, "끊어진 발진단 하중 보", new Vector3(274, -4, 78), new Vector3(4, 8, 14));
            BMass(p, "맞은편 착지단 하중 보", new Vector3(300, -4, 78), new Vector3(4, 8, 14));
            BMass(p, "그네 레일 콘크리트 받침", new Vector3(287, 25, 78), new Vector3(30, 6, 4));
            RevealAsset(p, "반출 크레인 그네 레일", Industrial + "SM_Crane_Beam.prefab", new Vector3(287, 20, 78), new Vector3(30, 2, 2), wornSteel);
            for (int i = 0; i < 3; i++)
            {
                float x = 281 + i * 7;
                float gripY = i == 1 ? 4.2f : 3.5f;
                foreach (float z in new[] { 77.2f, 78.8f })
                    VerticalCable(p, "그네 케이블 설치선", new Vector3(x, 20, z), new Vector3(x, gripY, z), .07f, wornSteel);
                BMass(p, "그네 손잡이 설치 가이드", new Vector3(x, gripY, 78), new Vector3(.16f, .16f, 2), maintenancePaint, false);
                RunnerAnchor(p, "SwingGrip_" + (i + 1), new Vector3(x, gripY, 78), Vector3.right);
            }
            foreach (float y in new[] { -12f, -29f })
            {
                BMass(p, "멀리 보이는 횡단 정비층", new Vector3(287, y, 61), new Vector3(22, 1.4f, 3), bRecess, false);
                BLight(p, new Vector3(282, y + 3, 59), new Vector3(288, y, 64), 280, 24);
            }
            RunnerAnchor(p, "SwingTakeoff", new Vector3(274, 0, 78), Vector3.right);
            RunnerAnchor(p, "SwingLanding", new Vector3(302, 0, 78), Vector3.right);
            RunnerThreshold(p, 274.5f, 71, 85);
            RunnerThreshold(p, 299, 71, 85);
            BLight(p, new Vector3(301, 5, 84), new Vector3(301, 0, 78), 850, 30);
        }

        static void RunnerLastRun(Transform p)
        {
            BFloor(p, "착지 후 18m 회복 바닥", 298, 316, 71, 85, 0, 2.5f);
            RunnerDeck(p, "출구를 향한 마지막 질주면", 316, 366);
            RunnerDischargeBay(p, 329, false, "04");
            RunnerDropLoad(p, new Vector3(349, 0, 74), "03");
            RunnerImpact(p, new Vector3(337, 0, 83), true);
            RunnerImpact(p, new Vector3(356, 0, 72.5f), false);
            BLight(p, new Vector3(318, 6, 69), new Vector3(327, 0, 78), 580, 30);
            BLight(p, new Vector3(347, 5.5f, 87), new Vector3(357, 0, 78), 560, 30);
            ContextSign(p, "마지막 외벽 방향", "OUT  /  70 m  >", new Vector3(330, 3.5f, 86), 0, 4);
        }

        static void RunnerExit(Transform p)
        {
            BFloor(p, "마지막 34m 가속과 외벽 통과", 366, 400, 71, 85, 0, 3);
            foreach (float x in new[] { 372f, 393f })
            {
                BMass(p, "거친 외벽 남측 방벽", new Vector3(x, 9, 64), new Vector3(8, 26, 14));
                BMass(p, "거친 외벽 북측 방벽", new Vector3(x, 9, 92), new Vector3(8, 26, 14));
                BMass(p, "출구 위 6m 두께 인방", new Vector3(x, 11.5f, 78), new Vector3(8, 10, 14));
            }
            BWallZ(p, 398, 55, 71, -48, 54, 4); BWallZ(p, 398, 85, 101, -48, 54, 4);
            BWallZ(p, 398, 71, 85, 8, 54, 4);
            // Parked shutter, pulled clear of the opening rather than an invisible lock.
            for (int i = 0; i < 12; i++)
                RevealBlock(p, "올라간 외벽 셔터 주름", new Vector3(389, 7.2f + i * .36f, 78), new Vector3(.35f, .25f, 13), wornSteel, false, false);
            ServicePipe(p, new Vector3(386, .5f, 86), new Vector3(386, 11, 86), .35f);
            ContextSign(p, "외부 반출구", "OUTSIDE  /  08", new Vector3(388.7f, 5.5f, 78), 90, 7);
            BLight(p, new Vector3(370, 5, 85), new Vector3(380, 0, 78), 480, 29);
            BLight(p, new Vector3(401, 7, 78), new Vector3(387, 0, 78), 1500, 32);
            RunnerAnchor(p, "ExitToReeds", new Vector3(400, 0, 78), Vector3.right);
        }
    }
}
