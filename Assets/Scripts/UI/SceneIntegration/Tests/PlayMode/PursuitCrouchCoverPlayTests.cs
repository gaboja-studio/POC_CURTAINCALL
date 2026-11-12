#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    /// <summary>
    /// 앉은 플레이어가 장애물에 설정 비율(GameSettings 대기실 추격 CrouchHiddenCoverage, 기본 80%) 이상 가려지면
    /// 추격자 시야에 보이지 않는다. 실제 PlayerMover 앉은 캡슐 높이와 추격자 시야 판정(HasVisualContact)으로 검사한다.
    /// </summary>
    public sealed class PursuitCrouchCoverPlayTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        static object Get(object o, string n) => o.GetType().GetProperty(n, Flags)?.GetValue(o) ?? o.GetType().GetField(n, Flags)?.GetValue(o);
        static void Set(object o, string n, object value) { var p = o.GetType().GetProperty(n, Flags); if (p != null) p.SetValue(o, value); else o.GetType().GetField(n, Flags).SetValue(o, value); }
        static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, Flags).Invoke(o, args);

        [UnityTest]
        public IEnumerator CrouchedPlayerHiddenOnlyWhenCoverReachesSettingRatio()
        {
            var directorType = T("CurtainCall.WaitingRoom.Pursuit.PursuitDirector");
            Assert.IsTrue(directorType.GetProperty("Current").GetValue(null) == null, "기존 추격자가 없는 격리 실행이어야 한다.");
            var original = SceneManager.GetActiveScene();
            var scene = SceneManager.CreateScene("Pursuit crouch cover fixture"); SceneManager.SetActiveScene(scene);
            var origin = new Vector3(-26000, 0, 26000);
            var root = new GameObject("격리 앉아 숨기 검사"); root.SetActive(false);
            NavMeshData data = null; NavMeshDataInstance instance = default;
            var rules = Get(T("CurtainCall.Settings.GameSettings").GetProperty("WaitingRoom").GetValue(null), "Pursuit");
            float savedCoverage = (float)Get(rules, "CrouchHiddenCoverage");
            try
            {
                Assert.AreEqual(.8f, savedCoverage, .0001f, "공통 설정 기본값: 80% 가려지면 숨음");
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.SetParent(root.transform);
                floor.transform.position = origin + new Vector3(0, -.25f, 0); floor.transform.localScale = new Vector3(16, .5f, 16);
                var sources = new List<NavMeshBuildSource> { new() { shape = NavMeshBuildSourceShape.Box,
                    transform = Matrix4x4.TRS(floor.transform.position, Quaternion.identity, Vector3.one), size = floor.transform.localScale } };
                var settings = NavMesh.GetSettingsByIndex(0);
                data = NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(origin, new Vector3(18, 6, 18)), Vector3.zero, Quaternion.identity);
                instance = NavMesh.AddNavMeshData(data);

                var room = root.AddComponent(T("CurtainCall.WaitingRoom.Pursuit.PursuitRoom")); Set(room, "Count", 0);
                var director = (Behaviour)root.AddComponent(directorType); director.enabled = false;
                var rooms = Array.CreateInstance(room.GetType(), 1); rooms.SetValue(room, 0); Set(director, "Rooms", rooms);
                Set(director, "BehaviourGraph", AssetDatabase.LoadAllAssetsAtPath("Assets/_ThirdParty/curtaincall/Pursuit/Behaviour/PursuitBehaviour.asset").First(a => a.GetType().Name == "BehaviorGraph"));
                root.SetActive(true); Set(director, "active", true); Set(director, "Elapsed", 0f); Set(director, "forcedStage", 0);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_ThirdParty/curtaincall/Pursuit/Prefabs/Pursuit_Male_02.prefab");
                var actorGo = Object.Instantiate(prefab, root.transform); actorGo.transform.SetPositionAndRotation(origin, Quaternion.identity);
                var actor = actorGo.GetComponent(T("CurtainCall.WaitingRoom.Pursuit.PursuitActor")); Call(actor, "Initialize", director, room, null);
                Assert.IsTrue((bool)Get(actor, "IsActivated"));

                // 정면 4m에 앉은 플레이어(남의 캐릭터처럼 소유자가 보낸 앉은 상태를 받는다).
                var playerGo = new GameObject("앉은 플레이어"); playerGo.transform.SetParent(root.transform);
                playerGo.transform.position = origin + new Vector3(0, 0, 4);
                var mover = playerGo.AddComponent(T("CurtainCall.Player.PlayerMover"));
                Set(mover, "Simulated", false);
                var stage = T("CurtainCall.Settings.PursuitStage").GetConstructor(Type.EmptyTypes).Invoke(null);
                Set(stage, "SightRadius", 10f); Set(stage, "FieldOfView", 120f);
                bool Visible() { Physics.SyncTransforms(); return (bool)Call(director, "HasVisualContact", actor, playerGo, stage); }
                float Covered() { Physics.SyncTransforms(); return (float)Call(director, "OccludedFraction", actorGo.transform.position + Vector3.up * 1.5f, mover); }

                Call(mover, "SetRemoteCrouched", true);
                yield return null; yield return null; // 앉은 몸통 높이 적용
                var body = (CharacterController)Get(mover, "Body");
                Assert.IsTrue((bool)Get(mover, "IsCrouched"));
                Assert.Less(body.height, 1.3f, "앉은 캡슐이 낮아져야 한다");

                Assert.AreEqual(0f, Covered(), .0001f); Assert.IsTrue(Visible(), "가림 없음: 보인다");

                // 몸 앞 전체를 가리는 상자: 숨는다.
                var crate = GameObject.CreatePrimitive(PrimitiveType.Cube); crate.name = "가림 상자"; crate.transform.SetParent(root.transform);
                crate.transform.position = origin + new Vector3(0, .75f, 3.5f); crate.transform.localScale = new Vector3(2, 1.5f, .3f);
                Assert.GreaterOrEqual(Covered(), .8f); Assert.IsFalse(Visible(), "80% 이상 가려진 앉은 플레이어는 보이지 않는다");

                // 같은 자리에서 일어서면 앉아 숨기 대상이 아니다(기존 몸 중심 판정). 상자 위로 머리가 보이도록 낮춘다.
                crate.transform.position = origin + new Vector3(0, .45f, 3.5f); crate.transform.localScale = new Vector3(2, .9f, .3f);
                Call(mover, "SetRemoteCrouched", false); yield return null; yield return null;
                Assert.IsTrue(Visible(), "서 있으면 낮은 상자 너머로 보인다");
                Call(mover, "SetRemoteCrouched", true); yield return null; yield return null;

                // 몸 한쪽(폭 2/3)만 가리는 높은 상자: 약 67% 가림 → 80% 기준에서는 보이고, 설정을 60%로 낮추면 숨는다.
                crate.transform.position = origin + new Vector3(-.95f, .75f, 3.6f); crate.transform.localScale = new Vector3(2, 1.5f, .2f);
                float partial = Covered();
                Assert.That(partial, Is.InRange(.5f, .79f), $"부분 가림 비율 {partial:0.00}");
                Assert.IsTrue(Visible(), "가림이 설정 비율보다 적으면 보인다");
                Set(rules, "CrouchHiddenCoverage", .6f);
                Assert.IsFalse(Visible(), "설정 비율을 낮추면 같은 가림에서 숨는다");
                crate.transform.position = origin + new Vector3(0, .75f, 3.5f); crate.transform.localScale = new Vector3(2, 1.5f, .3f);
                Set(rules, "CrouchHiddenCoverage", 0f);
                // 0이면 앉아 숨기를 끄고 기존 몸 중심(+1m) 한 점 판정만 쓴다. 1.5m 상자는 그 점도 가린다.
                Assert.IsFalse(Visible(), "끈 상태: 기존 판정");
                crate.transform.position = origin + new Vector3(0, .3f, 3.5f); crate.transform.localScale = new Vector3(2, .6f, .3f);
                Assert.IsTrue(Visible(), "끈 상태: 몸 중심이 보이면 보인다");

                // 벽 너머 탐지 단계는 단계 규칙(투시)을 따른다.
                Set(rules, "CrouchHiddenCoverage", .8f);
                crate.transform.position = origin + new Vector3(0, .75f, 3.5f); crate.transform.localScale = new Vector3(2, 1.5f, .3f);
                Set(stage, "SeeThroughWalls", true);
                Assert.IsTrue(Visible(), "투시 단계는 가려도 본다");
                Debug.Log($"[PursuitCrouchCover] 앉은 캡슐 {body.height:0.00}m, 전체 가림 숨음, 부분 가림 {partial:0.00} 보임, 설정·투시 단계 확인");
            }
            finally
            {
                Set(rules, "CrouchHiddenCoverage", savedCoverage);
                Object.DestroyImmediate(root); instance.Remove();
                if (data != null) Object.DestroyImmediate(data);
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                SceneManager.UnloadSceneAsync(scene);
            }
        }
    }
}
#endif
