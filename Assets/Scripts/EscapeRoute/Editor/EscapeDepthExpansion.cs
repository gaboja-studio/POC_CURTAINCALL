using System;
using System.Linq;
using CurtainCall.HumanCannon;
using CurtainCall.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        const string DepthGroup = "26_DeepServiceExtension_A";

        [MenuItem("Tools/CurtainCall/탈출로/A 중반 깊이 확장")]
        public static void BuildDepthExpansion()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("현재 A 편집 씬이 필요합니다.");
            var background = GameObject.Find(RootName).transform;
            if (background.Find(DepthGroup) != null) throw new InvalidOperationException("이미 확장했습니다. 기존 구조를 직접 편집하세요.");
            var system = UnityEngine.Object.FindAnyObjectByType<HumanCannonSystem>();
            if (system == null || system.StationCount != 1) throw new InvalidOperationException("기존 A03 대포 연결이 필요합니다.");
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("A deep service route expansion");
            foreach (string path in new[] { "17_BrutalistRoute/04_DeepServiceCut", "17_BrutalistRoute/06_AscendingWorks" })
                PreserveDepthObject(background.Find(path).gameObject);
            foreach (var support in background.Find("21_PlacementPolish").Cast<Transform>()
                .Where(t => t.name.StartsWith("04 계단 받침") || t.name.StartsWith("06 계단 받침")))
                PreserveDepthObject(support.gameObject);
            bConcrete = AssetDatabase.LoadAssetAtPath<Material>(Materials + "brutal-concrete.mat");
            bRecess = AssetDatabase.LoadAssetAtPath<Material>(Materials + "brutal-recess.mat");
            bSteel = AssetDatabase.LoadAssetAtPath<Material>(Materials + "brutal-steel.mat");
            bBulb = AssetDatabase.LoadAssetAtPath<Material>(Materials + "concrete-service-bulb.mat");
            if (bConcrete == null || bSteel == null) throw new InvalidOperationException("기존 시설 머티리얼이 필요합니다.");
            var root = VerticalChild(background, DepthGroup);
            root.localScale = new Vector3(1 / background.lossyScale.x, 1 / background.lossyScale.y, 1 / background.lossyScale.z);
            BuildDepthDescent(VerticalChild(root, "04A_Descent_plus2_to_minus22"));
            BuildDepthPumpHall(VerticalChild(root, "04B_PumpHall_Perimeter"));
            BuildDepthReturn(VerticalChild(root, "04C_OriginalCannonReturn"), background, system);
            BuildDepthRegroup(VerticalChild(root, "06_NorthServiceLoop"));
            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undo);
        }

        static void PreserveDepthObject(GameObject go)
        {
            Undo.RecordObject(go, "Preserve replaced route"); go.SetActive(false);
        }

        static void DepthDeck(Transform p, string name, float x1, float x2, float z1, float z2, float y)
            => BFloor(p, name, x1, x2, z1, z2, y, 1.5f);

        static void DepthStairs(Transform p, string name, float x, float z, float y, int count, bool down)
        {
            for (int i = 0; i < count; i++)
                BFloor(p, name + " " + (i + 1), x - 2.5f, x + 2.5f, z - (i + 1) * .35f, z - i * .35f,
                    y + (down ? -1 : 1) * (i + 1) * .2f, .45f);
            var end = new Vector3(x, y + (down ? -1 : 1) * count * .2f, z - count * .35f);
            foreach (float dx in new[] { -2.55f, 2.55f })
            {
                VerticalStrut(p, "계단 하부 철근 콘크리트 보", new Vector3(x + dx, y - 1, z), end + new Vector3(dx, -1, 0), .65f, bConcrete);
                VerticalCable(p, "연속 계단 손잡이", new Vector3(x + dx, y + 1.1f, z), end + new Vector3(dx, 1.1f, 0), .06f, bSteel);
            }
        }

        static void BuildDepthDescent(Transform p)
        {
            DepthDeck(p, "A03 연결 참", 62, 72.5f, 60.5f, 62.5f, 2);
            for (int flight = 0; flight < 3; flight++)
            {
                float z = 60.5f - flight * 19, y = 2 - flight * 8;
                DepthStairs(p, "하행 " + (flight + 1), 68, z, y, 40, true);
                DepthDeck(p, "하행 중간 참 " + flight, 65.5f, 70.5f, z - 19, z - 14, y - 8);
                BLight(p, new Vector3(72, y + 1, z - 12), new Vector3(68, y - 6, z - 11), 330, 22);
                BMass(p, "매립된 횡단 설비 보", new Vector3(67, y + 8, z - 10), new Vector3(23, 6, 4));
            }
            BWallZ(p, 56.5f, 6, 62.5f, -86, 36, 3);
            BWallZ(p, 74, 6, 60.5f, -86, 36, 3);
            BMass(p, "하행 설비동 덮개", new Vector3(65, 37.5f, 34), new Vector3(20, 3, 59));
            BFloor(p, "하행 공동 집수 바닥", 58, 72.5f, 6, 62.5f, -86, 3);
            // The former direct entrance into A05 is sealed; its original wall remains as the outer skin.
            BMass(p, "폐쇄된 상부 우회문", new Vector3(76, 2.5f, 28.5f), new Vector3(3, 5, 13), bRecess);
            foreach (float z in new[] { 20f, 40f, 57f })
            {
                BMass(p, "하행 벽 수직 설비 홈", new Vector3(59, -25, z), new Vector3(1.2f, 105, 2), bRecess, false);
                VerticalCylinder(p, "내려가는 압력관", new Vector3(60.5f, -25, z), .7f, 105, bSteel);
            }
            FusionFloorMarks(p, new Vector3(68, 2.02f, 61.5f), Vector3.back);
        }

        static void BuildDepthReturn(Transform p, Transform background, HumanCannonSystem system)
        {
            DepthDeck(p, "두 번째 대포 정비대", 81, 89, -9, 2, -22);
            var cannon = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CannonPrefab), p);
            Undo.RegisterCreatedObjectUndo(cannon, "Original return cannon");
            cannon.name = "04_ReturnCannon";
            cannon.transform.SetPositionAndRotation(new Vector3(85, -22, -3), Quaternion.identity);
            var station = cannon.GetComponentInChildren<HumanCannonStation>();
            Vector3 target = new Vector3(85, 0, 25), delta = target - station.Muzzle.position;
            const float flightSeconds = 3.3f;
            var data = new SerializedObject(station);
            data.FindProperty("forwardSpeed").floatValue = new Vector2(delta.x, delta.z).magnitude / flightSeconds;
            data.FindProperty("upwardSpeed").floatValue = (delta.y + .5f * GameSettings.Base.Character.Gravity * flightSeconds * flightSeconds) / flightSeconds;
            data.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(station);
            PrefabUtility.RecordPrefabInstancePropertyModifications(cannon.transform);
            var settings = new SerializedObject(system);
            var stations = settings.FindProperty("stations"); stations.arraySize = 2;
            stations.GetArrayElementAtIndex(1).objectReferenceValue = station;
            settings.FindProperty("flightFloorHeight").floatValue = -80;
            settings.ApplyModifiedProperties();
            var a05 = background.Find("17_BrutalistRoute/05_TransferChasm");
            var southWall = a05.Cast<Transform>().Single(t => t.name == "타설 콘크리트 벽" && Mathf.Abs(t.position.z - 6) < .01f);
            PreserveDepthObject(southWall.gameObject);
            BWallX(p, 6, 76, 80, -100, 48, 3); BWallX(p, 6, 90, 129, -100, 48, 3);
            BWallX(p, 6, 80, 90, -100, -14, 3); BWallX(p, 6, 80, 90, 14, 48, 3);
            Ensure<HumanCannonLandingZone>(a05.Find("트램폴린 진입 대").gameObject);
            ChildAt(p, "Landing_A05_Feet", target, Quaternion.Euler(0, 90, 0));
            BLight(p, new Vector3(81, -16, -7), new Vector3(85, -22, -3), 300, 17);
            BLight(p, new Vector3(89, 10, 8), new Vector3(85, 0, 22), 650, 30);
            FusionFloorMarks(p, new Vector3(88, .02f, 25), Vector3.right);
        }
    }
}
