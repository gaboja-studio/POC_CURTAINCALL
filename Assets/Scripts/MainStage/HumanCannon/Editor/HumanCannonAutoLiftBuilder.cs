using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.HumanCannon.EditorTools
{
    /// <summary>042 자동 탑승 표시와 쉼터 측면의 분리 승강 경로를 적용한다.</summary>
    public static class HumanCannonAutoLiftBuilder
    {
        [MenuItem("Tools/CurtainCall/인간 대포/042 자동 승강과 쉼터 낙차 적용")]
        public static void UpgradeOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty || scene.path != HumanCannonTestSceneBuilder.ScenePath)
                throw new InvalidOperationException("Play 종료 후 저장된 HumanCannon 씬에서 실행하세요.");
            var roots = scene.GetRootGameObjects();
            var course = roots.Single(g => g.name == "SplitRouteCourse").transform;
            if (course.Find("ShelterLiftAccess") != null) throw new InvalidOperationException("자동 승강 배치를 이미 적용했습니다.");
            var lifts = course.GetComponentsInChildren<HumanCannonTransport>();
            if (lifts.Length != 3 || lifts.Any(t => !t.HasTrapDoors)) throw new InvalidOperationException("양문 리프트 3대가 필요합니다.");
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            foreach (var root in roots) Undo.RegisterFullObjectHierarchyUndo(root, "042 자동 승강/쉼터 측면 배치");
            try
            {
                foreach (var lift in lifts)
                {
                    var data = new SerializedObject(lift);
                    var indicators = data.FindProperty("boardingIndicators");
                    indicators.arraySize = 2;
                    indicators.GetArrayElementAtIndex(0).objectReferenceValue = lift.Platform.Find("LeftHinge/FloorPanel").GetComponent<Renderer>();
                    indicators.GetArrayElementAtIndex(1).objectReferenceValue = lift.Platform.Find("RightHinge/FloorPanel").GetComponent<Renderer>();
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                var route = course.GetComponent<HumanCannonRouteSystem>();
                var routeData = new SerializedObject(route);
                routeData.FindProperty("boardingSeconds").floatValue = 1.5f;
                routeData.ApplyModifiedPropertiesWithoutUndo();
                var oldSwitch = course.Find("ShelterUnlockSwitch");
                if (oldSwitch != null) oldSwitch.gameObject.SetActive(false);

                var firstFloor = course.Find("FirstCourseFloor");
                var deck = course.Find("ShelterDeck");
                var floorMaterial = firstFloor.GetComponentInChildren<MeshRenderer>().sharedMaterial;
                var deckMaterial = deck.GetComponentInChildren<MeshRenderer>().sharedMaterial;
                // 기존 수직 승강기 구멍은 더 이상 사용하지 않는다.
                Box(firstFloor, "FormerLiftFloor", new Vector3(-9, -0.25f, 38.5f), new Vector3(6, 0.5f, 7), floorMaterial);
                Box(deck, "FormerLiftFloor", new Vector3(-9, 3.75f, 42.5f), new Vector3(6, 0.5f, 1), deckMaterial);
                var landingData = new SerializedObject(deck.GetComponent<HumanCannonLandingZone>());
                var surfaces = deck.GetComponentsInChildren<BoxCollider>().Where(c => c.enabled).ToArray();
                var array = landingData.FindProperty("additionalSurfaces");
                array.arraySize = surfaces.Length;
                for (int i = 0; i < surfaces.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = surfaces[i];
                landingData.ApplyModifiedPropertiesWithoutUndo();

                // 위 코스만 2m 올려 첫 비행 반동으로 접근할 수 있는 높이 6m를 유지한다.
                foreach (string name in new[] { "ShelterDeck", "ShelterDeparture", "ShelterCannon1", "ShelterCannon2",
                    "ShelterCannon3", "SecondCourseFloor", "SecondCatch", "SecondSectionPoleGate", "SecondSectionFire" })
                    course.Find(name).position += Vector3.up * 2;
                roots.Single(g => g.name == "LandingCushion").transform.position += Vector3.up * 2;

                var shelterLift = lifts.Single(t => t.name == "ShelterLift");
                shelterLift.transform.Find("Start").position = new Vector3(-25, 0, 40);
                shelterLift.transform.Find("Destination").position = new Vector3(-18, 6, 48);
                foreach (var lift in lifts) lift.Present(0);
                Box(course, "ShelterLiftAccess", new Vector3(-21.5f, -0.25f, 34.5f), new Vector3(13, 0.5f, 3), floorMaterial);

                // 첫 관문(z32)을 통과한 뒤에만 측면 승강기로 걸어 나갈 수 있다.
                var fence = roots.Single(g => g.name == "PoleGateCourse").transform.Find("SideFences/LeftFence");
                var after = UnityEngine.Object.Instantiate(fence.gameObject, fence.parent).transform;
                after.name = "LeftFenceAfterLiftAccess";
                fence.position = new Vector3(-15, 9, 26.5f);
                fence.localScale = new Vector3(0.35f, 18, 13); // z20..33
                after.position = new Vector3(-15, 9, 39);
                after.localScale = new Vector3(0.35f, 18, 6); // z36..42
                Physics.SyncTransforms();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("자동 승강 씬 저장 실패");
                Undo.CollapseUndoOperations(group);
                Debug.Log("[HumanCannonRoute] 자동 탑승 표시·6m 쉼터·측면 낙하 간격·승강 진입 통로 저장");
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }

        static void Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.position = position;
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
