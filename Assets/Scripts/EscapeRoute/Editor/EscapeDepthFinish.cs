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
        public static void FinishDepthExpansion()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || Application.isPlaying) throw new InvalidOperationException("A 편집 씬이 필요합니다.");
            var background = GameObject.Find(RootName).transform;
            var root = background.Find(DepthGroup);
            if (root == null || root.Find("07_ServiceFinish") != null) throw new InvalidOperationException("확장 구조와 미적용 마감이 필요합니다.");
            bConcrete = RunnerMaterial("brutal-concrete"); bRecess = RunnerMaterial("brutal-recess");
            bSteel = RunnerMaterial("brutal-steel"); bBulb = RunnerMaterial("concrete-service-bulb");
            wornSteel = RunnerMaterial("working-steel"); wornPipe = RunnerMaterial("working-pipe");
            maintenancePaint = RunnerMaterial("working-faded-ochre"); leakWear = RunnerMaterial("working-leak");
            floorWear = RunnerMaterial("working-floor-dirt");
            var p = VerticalChild(root, "07_ServiceFinish");
            // The shared rail helper is oriented for X. Replace these new Z modules with continuous rails.
            foreach (var rail in root.GetComponentsInChildren<Transform>().Where(t => t.name == "1.1m 점검 난간").ToArray())
                Undo.DestroyObjectImmediate(rail.gameObject);
            foreach (var toe in root.GetComponentsInChildren<Transform>().Where(t => t.name == "거친 갤러리 물막이"))
            {
                var bounds = toe.GetComponent<Renderer>().bounds;
                var axis = bounds.size.x > bounds.size.z ? Vector3.right : Vector3.forward;
                float length = Mathf.Max(bounds.size.x, bounds.size.z) - .2f;
                var centre = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                var a = centre - axis * length * .5f; var b = centre + axis * length * .5f;
                VerticalCable(p, "연속 갤러리 손잡이", a + Vector3.up * 1.1f, b + Vector3.up * 1.1f, .065f, wornSteel);
                VerticalCable(p, "연속 갤러리 중간대", a + Vector3.up * .6f, b + Vector3.up * .6f, .045f, wornSteel);
                int count = Mathf.CeilToInt(length / 3);
                for (int i = 0; i <= count; i++)
                {
                    var at = Vector3.Lerp(a, b, i / (float)count);
                    VerticalCable(p, "갤러리 난간 기둥", at, at + Vector3.up * 1.1f, .06f, wornSteel);
                }
            }
            PreserveDepthObject(background.Find("18_WorkingFacilityFinish/04_RepairAndDistribution").gameObject);
            foreach (var t in background.Find("18_WorkingFacilityFinish/04_07_WornServiceAreas").Cast<Transform>())
                if ((t.position.x > 55 && t.position.x < 79 && t.position.z > 10 && t.position.z < 62)
                    || (t.position.x > 114 && t.position.x < 140 && t.position.z > 49 && t.position.z < 120))
                    PreserveDepthObject(t.gameObject);
            foreach (float y in new[] { -18f, -14.5f })
                ServicePipe(p, new Vector3(60, y, 3), new Vector3(60, y, -62), y == -18 ? .8f : .45f);
            CableTray(p, new Vector3(71.8f, -18.3f, -10), new Vector3(71.8f, -18.3f, -51));
            foreach (float z in new[] { -14f, -36f, -57f })
            {
                BMass(p, "벽 배관 지지 브래킷", new Vector3(59, -18.8f, z), new Vector3(3.3f, .4f, .5f), wornSteel, false);
                BMass(p, "코어 통풍구 강철 테두리", new Vector3(77.8f, -15, z == -57 ? -39 : z), new Vector3(.4f, 8, 5), wornSteel, false);
                WallWear(p, new Vector3(58.02f, -13, z), new Vector2(8, 16), -90);
                FloorWear(p, new Vector3(68, -21.98f, z), new Vector2(4, 8), 17);
            }
            for (int i = 0; i < 9; i++)
                BMass(p, "펌프 코어 수평 배기 루버", new Vector3(77.54f, -18.4f + i * .8f, -36), new Vector3(.7f, .22f, 4.5f), bRecess, false);
            BMass(p, "펌프 기계 지지 캔틸레버", new Vector3(75.6f, -20, -27), new Vector3(5, 2, 10), bRecess);
            RevealAsset(p, "기존 산업 펌프 장치", Industrial + "SM_Machinery_04.prefab", new Vector3(75.7f, -19, -27), new Vector3(3.6f, 6, 7), wornSteel);
            RevealAsset(p, "현장 압력 계기", Industrial + "SM_Pipe_Big_Display.prefab", new Vector3(73.4f, -17.8f, -24), new Vector3(.8f, 1.3f, 1), maintenancePaint);
            ServicePipe(p, new Vector3(75, -13, -27), new Vector3(75, 28, -27), 1.1f);
            foreach (float y in new[] { -2f, -44f, -65f })
            {
                BMass(p, "서측 다른 층 정비 선반", new Vector3(60, y - .6f, -29), new Vector3(3.8f, 1.2f, 61), bRecess, false);
                BMass(p, "다른 층 닫힌 정비문", new Vector3(58.1f, y + 1.1f, -23), new Vector3(.2f, 2.2f, 1.1f), wornSteel, false);
                BLight(p, new Vector3(62, y + 3, -22), new Vector3(59, y, -25), 250, 22);
            }
            BLight(p, new Vector3(63, -12, -24), new Vector3(76, -15, -28), 700, 25);
            ServiceLamp(p, new Vector3(86, -17, -62), 180, 24);
            BMass(p, "남측 조명 지지대", new Vector3(86, -17.15f, -64.5f), new Vector3(.15f, .15f, 5), wornSteel, false);
            FloorWear(p, new Vector3(91, -21.98f, -58), new Vector2(9, 4), 10);
            RepairJoint(p, new Vector3(92, -21.99f, -58), 4);
            // Human-sized control equipment and surface wear sit beside the route, outside body clearance.
            BFloor(p, "북측 제어반 작업 포켓", 116, 120, 79, 85, -8, 1.5f);
            RevealAsset(p, "기존 설비 제어반", Industrial + "SM_Control_Panel.prefab", new Vector3(117.5f, -8, 82), new Vector3(1.1f, 1.8f, 2.4f), wornSteel, Quaternion.Euler(0, 90, 0));
            ServicePipe(p, new Vector3(116.4f, -3, 52), new Vector3(116.4f, -3, 114), .5f);
            CableTray(p, new Vector3(125.7f, -3.8f, 53), new Vector3(125.7f, -3.8f, 105));
            foreach (float z in new[] { 66f, 86f, 106f })
            {
                WallWear(p, new Vector3(115.52f, -2, z), new Vector2(5, 14), -90);
                FloorWear(p, new Vector3(122.5f, -7.98f, z), new Vector2(3.5f, 7), 12);
                BMass(p, "북측 배관 벽체 지지", new Vector3(116, -3.5f, z), new Vector3(1.4f, .3f, .35f), wornSteel, false);
            }
            // Remove coincident A07 floor faces from the newly added connection.
            var deck = root.Find("06_NorthServiceLoop/A07 연결 발판");
            PreserveDepthObject(deck.gameObject);
            DepthDeck(p, "A07 접합 발판", 137.5f, 140, 74, 82, 0);
            foreach (var wall in root.Find("06_NorthServiceLoop").Cast<Transform>().Where(t => t.name == "타설 콘크리트 벽" && Mathf.Abs(t.position.x - 141) < .01f && Mathf.Abs(t.position.y + 27.5f) < .01f))
            { Undo.RecordObject(wall, "Separate buried wall face"); wall.position += Vector3.down * .05f; }
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
