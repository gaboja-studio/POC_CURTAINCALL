using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed class PerformanceEndingPlayTests
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        GameObject space, service;
        Scene destination;
        static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        static object Get(object obj, string name) => obj.GetType().GetProperty(name, Fields)?.GetValue(obj)
            ?? obj.GetType().GetField(name, Fields).GetValue(obj);
        static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Fields).SetValue(obj, value);
        static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Fields).Invoke(obj, args);

        [UnityTest]
        public IEnumerator TeammateDeathNotifiesOnceAndLateSnapshotDoesNotReplayIt()
        {
            service = new GameObject("DeathNoticeRewards");
            var reward = (Behaviour)service.AddComponent(T("CurtainCall.Reward.PerformanceRewards"));
            reward.enabled = false;
            space = new GameObject("DeathNoticeSpace");
            space.AddComponent(T("CurtainCall.UI.SceneIntegration.PerformanceSceneBinding"));
            var type = T("CurtainCall.UI.SceneIntegration.PerformanceEndingPresentation");
            double deadline = Time.realtimeSinceStartupAsDouble + 15;
            while (type.GetProperty("Current").GetValue(null) == null && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            var ending = (Component)type.GetProperty("Current").GetValue(null);
            Assert.IsNotNull(ending);
            string session = "44444444444444444444444444444444";
            var model = Activator.CreateInstance(T("CurtainCall.Reward.PerformanceRewardModel"), session, 1);
            var id = Activator.CreateInstance(T("CurtainCall.Results.PerformanceIdentity"), session, 1, 1, 0, "HumanCannon");
            var person = Activator.CreateInstance(T("CurtainCall.Reward.RewardParticipantSeed"), "other", (ulong)9, 1, 0, false);
            var people = Array.CreateInstance(person.GetType(), 1); people.SetValue(person, 0);
            var rules = Activator.CreateInstance(T("CurtainCall.Reward.RewardRules"), 10, 5, 100, 0, 30);
            Call(model, "Begin", id, people, rules);
            Set(reward, "state", Call(model, "Snapshot", -1)); yield return null; yield return null;
            Assert.AreEqual(false, Get(ending, "DeathVisible"));
            Call(model, "Observe", "other", 0, true, true, rules);
            Set(reward, "state", Call(model, "Snapshot", -1)); yield return null;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.AreEqual(true, Get(ending, "DeathVisible"));
            Call(Get(ending, "notifications"), "ResetForRuntime");
            yield return null; yield return null;
            Assert.AreEqual(false, Get(ending, "DeathVisible"), "동일 사망 스냅샷은 다시 알리지 않는다");
            Set(ending, "observedPerformance", null); // 뒤늦게 구독한 클라이언트와 같은 초기 상태
            yield return null;
            Assert.AreEqual(false, Get(ending, "DeathVisible"));
        }

        [UnityTest]
        public IEnumerator BothOutcomesWaitShowResultCoverAndReleaseAfterDestination()
        {
            service = new GameObject("EndingTestRewards");
            var reward = (Behaviour)service.AddComponent(T("CurtainCall.Reward.PerformanceRewards"));
            reward.enabled = false; // 실제 모델의 스냅샷을 공급하고 온라인 세션 수명은 이 테스트에서 제외한다.
            space = new GameObject("EndingTestSpace");
            space.AddComponent(T("CurtainCall.UI.SceneIntegration.PerformanceSceneBinding"));
            var type = T("CurtainCall.UI.SceneIntegration.PerformanceEndingPresentation");
            double deadline = Time.realtimeSinceStartupAsDouble + 15;
            while (type.GetProperty("Current").GetValue(null) == null && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            var ending = (Component)type.GetProperty("Current").GetValue(null);
            Assert.IsNotNull(ending);
            Assert.AreEqual("DontDestroyOnLoad", ending.gameObject.scene.name);
            destination = SceneManager.CreateScene("WaitingRoomBase");
            int generation = 0;
            foreach (bool succeeded in new[] { true, false })
            {
                generation++;
                string session = "33333333333333333333333333333333";
                var model = Activator.CreateInstance(T("CurtainCall.Reward.PerformanceRewardModel"), session, generation);
                var id = Activator.CreateInstance(T("CurtainCall.Results.PerformanceIdentity"), session, generation, 1, 0, "Tightrope");
                var person = Activator.CreateInstance(T("CurtainCall.Reward.RewardParticipantSeed"), "p", (ulong)1, 0, 0, false);
                var people = Array.CreateInstance(person.GetType(), 1); people.SetValue(person, 0);
                var rules = Activator.CreateInstance(T("CurtainCall.Reward.RewardRules"), 10, 5, 100, 0, 30);
                Assert.AreEqual(true, Call(model, "Begin", id, people, rules));
                Call(model, "React", "p", 40, rules);
                var confirm = new object[] { succeeded, "TestEnd", null, 1, rules, null };
                Assert.AreEqual(true, Call(model, "Confirm", confirm));
                Set(reward, "state", Call(model, "Snapshot", -1));
                // 첫 프레임은 이전 공연 표시를 초기화하고 다음 프레임에서 새 스냅샷을 복원한다.
                yield return null; yield return null;
                Assert.AreEqual(false, Get(ending, "ResultVisible"));
                Assert.AreEqual(true, T("CurtainCall.Player.InputGate").GetProperty("IsRequested").GetValue(null));
                Set(ending, "started", Time.unscaledTimeAsDouble - 2);
                yield return null;
                Assert.AreEqual(true, Get(ending, "ResultVisible"));
                var title = Get(ending, "title");
                Assert.AreEqual(succeeded ? "공연 성공" : "공연 실패", Get(title, "text"));
                Set(ending, "started", Time.unscaledTimeAsDouble - 6);
                yield return null;
                Assert.AreEqual(true, Get(ending, "BlackVisible"));
                Assert.AreEqual(false, Get(ending, "ResultVisible"));
                Call(ending, "SceneLoaded", destination, LoadSceneMode.Single);
                Assert.AreEqual(true, Get(ending, "BlackVisible"));
                Set(ending, "loadedAt", Time.unscaledTimeAsDouble - 1);
                yield return null;
                Assert.AreEqual(false, Get(ending, "BlackVisible"));
                Assert.AreEqual(false, T("CurtainCall.Player.InputGate").GetProperty("IsRequested").GetValue(null));
                yield return null;
                Assert.AreEqual(false, Get(ending, "ResultVisible"), "같은 결과 스냅샷으로 다시 열리지 않아야 한다");
            }
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (space != null) Object.Destroy(space);
            if (service != null) Object.Destroy(service);
            foreach (string name in new[] { "CommonUiSceneLoader", "PerformanceEndingPresentation" })
            {
                var obj = Object.FindFirstObjectByType(T("CurtainCall.UI.SceneIntegration." + name)) as Component;
                if (obj != null) Object.Destroy(obj.gameObject);
            }
            yield return null;
            foreach (string path in new[] { "Assets/Scenes/UI/CommonPerformanceUI.unity", "Assets/Scenes/UI/PerformanceTransitionUI.unity" })
            {
                var scene = SceneManager.GetSceneByPath(path);
                if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            }
            if (destination.IsValid() && destination.isLoaded) yield return SceneManager.UnloadSceneAsync(destination);
        }
    }
}
