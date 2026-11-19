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
        static Collider Blocker(Component owner) => new SerializedObject(owner).FindProperty("blocker")?.objectReferenceValue as Collider;

        /// <summary>
        /// 2026-11-17 갱신: 통합 대기실이 추격의 기준 씬이다(옛 추격 전용 씬은 NavMesh가 없는 폐기 씬이라 비교 기준에서 뺀다).
        /// 방·배치·경로 그래프 참조와, 부서지는 벽·상자·문이 추격 장애물로 등록되어 부수기 전후 경로가 바뀌는지를 본다.
        /// </summary>
        [Test]
        public void UnifiedWaitingRoom_PreservesPursuitAuthoringAndSceneReferences()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            Assert.IsFalse(Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty));
            try {
                var scene = EditorSceneManager.OpenScene(WaitingRoomScenes.Path);
                Assert.AreEqual(WaitingRoomScenes.Path, PursuitAuthoring.ScenePath);
                var director = All<PursuitDirector>(scene).Single();
                Assert.IsNotNull(director.BehaviourGraph);
                Assert.IsNotNull(director.CaptureBloodPrefab);
                var names = director.Rooms.Select(r => r.name).ToArray();
                CollectionAssert.AllItemsAreNotNull(director.Rooms);
                CollectionAssert.AllItemsAreUnique(names);
                // 사용자 보고 구역: 환풍구 출구 계단·무대 장치실·화물 통로·공연장 복도는 방으로 등록되어 있어야 한다.
                CollectionAssert.IsSubsetOf(new[] { "N4_MachineEntry", "S11_StageGear", "S12_CargoBay", "S06_StageCorridor", "S02_Dormitory", "B1_MachineRoom" }, names);
                Assert.IsTrue(director.Rooms.Single(r => r.name == "N4_MachineEntry").ExtraFloors.Contains(0f), "환풍구 출구 계단은 배치 창 0층에서도 보인다.");
                foreach (var room in director.Rooms) {
                    Assert.IsNotNull(room.Area, room.name);
                    Assert.AreEqual(scene, room.Area.gameObject.scene);
                    foreach (var light in room.Lights) Assert.AreEqual(scene, light.gameObject.scene);
                    foreach (var point in room.SpawnPoints.Concat(room.PatrolPoints)) Assert.AreEqual(scene, point.gameObject.scene);
                    foreach (var placement in room.Placements.Where(p => p != null)) {
                        Assert.AreEqual(scene, placement.gameObject.scene, placement.name);
                        Assert.IsNotNull(placement.Prefab, placement.name);
                        Assert.IsNotNull(placement.Prefab.GetComponent<PursuitActor>(), placement.name);
                    }
                }
                // 부서지는 벽·상자·잠긴 문·출입구가 막는 동안만 NavMesh를 깎고, 부서지면 길이 열리도록 모두 장애물로 등록되어 있다.
                var obstacles = All<PursuitDoorObstacle>(scene);
                var covered = obstacles.Select(o => o.Source).Where(s => s != null).ToHashSet();
                var blockers = All<ExplorationGate>(scene).Select(Blocker).Concat(All<BreakableWall>(scene).Select(Blocker))
                    .Where(c => c != null && c.enabled && !c.isTrigger).ToArray();
                Assert.IsNotEmpty(blockers);
                foreach (var blocker in blockers) Assert.IsTrue(covered.Contains(blocker), $"추격 장애물 미등록: {blocker.name}");
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
