using System.Linq;
using CurtainCall.Settings;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public sealed class UnifiedWaitingRoomSceneTests
    {
        static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        static string Placement(PursuitPlacement p) => JsonUtility.ToJson(new PlacementSnapshot {
            name = p.name, position = p.transform.position, rotation = p.transform.rotation,
            prefab = AssetDatabase.GetAssetPath(p.Prefab), size = (int)p.Size, stage = p.ActivationStage,
            roomPatrol = p.UseRoomPatrol, detach = p.RouteDetachDistance,
            route = p.Route(p.OwnerRoom()).Select(t => t.position).ToArray()
        });
        [System.Serializable] class PlacementSnapshot {
            public string name, prefab; public Vector3 position; public Quaternion rotation;
            public int size, stage; public bool roomPatrol; public float detach; public Vector3[] route;
        }

        [Test]
        public void UnifiedWaitingRoom_PreservesPursuitAuthoringAndSceneReferences()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            Assert.IsFalse(Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty));
            try {
                var original = EditorSceneManager.OpenScene(WaitingRoomScenes.PursuitPath);
                var placements = All<PursuitPlacement>(original).Select(Placement).ToArray();
                var originalDirector = All<PursuitDirector>(original).Single();
                var graph = originalDirector.BehaviourGraph;
                var blood = originalDirector.CaptureBloodPrefab;
                var roomNames = originalDirector.Rooms.Select(r => r.name).ToArray();
                var scene = EditorSceneManager.OpenScene(WaitingRoomScenes.IntegratedPath);
                Assert.AreEqual(WaitingRoomScenes.IntegratedPath, PursuitAuthoring.ScenePath);
                var director = All<PursuitDirector>(scene).Single();
                CollectionAssert.AreEqual(roomNames, director.Rooms.Select(r => r.name));
                // 통합 대기실은 배치를 방 공간 프리팹에 두므로 하이어라키 순서는 추격 씬과 다르다.
                CollectionAssert.AreEquivalent(placements, All<PursuitPlacement>(scene).Select(Placement));
                Assert.AreEqual(graph, director.BehaviourGraph);
                Assert.AreEqual(blood, director.CaptureBloodPrefab);
                Assert.AreEqual(22, All<PursuitDoorObstacle>(scene).Length);
                foreach (var room in director.Rooms) {
                    Assert.AreEqual(scene, room.Area.gameObject.scene);
                    foreach (var light in room.Lights) Assert.AreEqual(scene, light.gameObject.scene);
                    foreach (var point in room.SpawnPoints.Concat(room.PatrolPoints)) Assert.AreEqual(scene, point.gameObject.scene);
                }
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        Assert.AreEqual(0, GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject), t.name);
                Assert.AreEqual(1, All<CurtainCall.UI.SceneIntegration.WaitingRoomUiBinding>(scene).Length);
                Assert.AreEqual(1, All<CurtainCall.Cameras.CameraViewDirector>(scene).Length);
            } finally {
                if (setup.Any(s => s.isLoaded && s.isActive && !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            }
        }
    }
}
