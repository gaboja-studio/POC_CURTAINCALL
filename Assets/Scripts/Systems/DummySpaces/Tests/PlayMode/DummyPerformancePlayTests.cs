using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.DummySpaces.Tests
{
    /// <summary>실제 소유 씬 + 공개 상황 API 회귀. Assembly-CSharp는 기존 카메라 테스트처럼 이름으로 호출한다. 실제 입력 검증은 아니다.</summary>
    public sealed class DummyPerformancePlayTests
    {
        Component dummy;
        Component campaign;
        Component rewards;

        static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(name)).First(t => t != null);
        static object Get(object target, string name) => target.GetType().GetProperty(name) is PropertyInfo property
            ? property.GetValue(target) : target.GetType().GetField(name).GetValue(target);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name).Invoke(target, args);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/Tests/Systems/DummySpaces/DummyPerformance.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            dummy = Object.FindAnyObjectByType(GameType("CurtainCall.DummySpaces.DummyPerformance")) as Component;
            Assert.IsNotNull(dummy, "소유 씬에 상황 입력 컴포넌트가 저장됨");
            campaign = (Component)GameType("CurtainCall.Campaign.CampaignRun").GetProperty("Instance").GetValue(null);
            rewards = (Component)Get(dummy, "Rewards");
            Assert.IsTrue((bool)Call(dummy, "StartOfflineRun"));
            Assert.IsTrue((bool)Call(dummy, "BeginPerformance"));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (campaign != null) Call(campaign, "StopOfflineRun");
            var session = GameType("CurtainCall.Network.Session.NetworkSessionManager").GetProperty("Instance").GetValue(null) as Component;
            var network = session != null ? Get(session, "NetworkManager") as Component : null;
            if (dummy != null) Object.Destroy(dummy.gameObject);
            if (rewards != null) Object.Destroy(rewards.gameObject);
            if (campaign != null) Object.Destroy(campaign.gameObject);
            if (session != null) Object.Destroy(session.gameObject);
            if (network != null) Object.Destroy(network.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BothArrive_ConfirmsResultOnceAndLets031Transition()
        {
            Assert.IsTrue((bool)Call(dummy, "Arrive", "Dummy-0"));
            Assert.IsNull(Get(rewards, "Latest"), "한 명 도착만으로 조기 성공하지 않음");
            Assert.IsTrue((bool)Call(dummy, "Arrive", "Dummy-1"));
            var result = Get(rewards, "Latest");
            Assert.IsTrue((bool)Get(result, "Succeeded"));
            Assert.AreEqual("Completed", Get(result, "EndReason"));
            Assert.IsFalse((bool)Call(dummy, "Arrive", "Dummy-1"), "결과 중복 확정 거절");
            Assert.IsFalse((bool)Call(dummy, "TimeExpired"));
            Assert.AreEqual("RestTime", Get(Get(campaign, "State"), "Phase").ToString());
            yield return null;
        }

        [UnityTest]
        public IEnumerator CutThenTimeout_PreservesInjuryAndConfirmsFailure()
        {
            Assert.IsTrue((bool)Call(dummy, "Cut", "Dummy-0"));
            Assert.IsTrue((bool)Call(dummy, "TimeExpired"));
            var result = Get(rewards, "Latest");
            Assert.IsFalse((bool)Get(result, "Succeeded"));
            Assert.AreEqual("TimeExpired", Get(result, "EndReason"));
            var participants = ((IEnumerable)Get(result, "Participants")).Cast<object>().ToArray();
            Assert.AreEqual(1, Get(participants[0], "EndingLostCount"));
            Assert.AreEqual(0L, Get(result, "Income"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ArriveThenOtherDies_EndsPerformanceInsteadOfWaiting()
        {
            Assert.IsTrue((bool)Call(dummy, "Arrive", "Dummy-0"));
            Assert.IsNull(Get(rewards, "Latest"));
            Assert.IsTrue((bool)Call(dummy, "Kill", "Dummy-1"));
            var result = Get(rewards, "Latest");
            Assert.IsNotNull(result, "도착 뒤 남은 사람이 죽어도 공연이 멈춰 있지 않음");
            bool devMode = (bool)GameType("CurtainCall.Dev.DevMode").GetProperty("Enabled").GetValue(null);
            if (devMode)
            {
                Assert.IsTrue((bool)Get(result, "Succeeded"), "개발자 모드(생존 1명 허용): 남은 생존자가 모두 도착 → 성공");
                Assert.AreEqual("Completed", Get(result, "EndReason"));
            }
            else Assert.AreEqual("InsufficientSurvivors", Get(result, "Failure").ToString(), "생존 2명 미만 실패가 먼저");
            Assert.AreEqual("RestTime", Get(Get(campaign, "State"), "Phase").ToString());
            yield return null;
        }

        [UnityTest]
        public IEnumerator Death_Uses031SurvivorFailureWithoutDummySettlement()
        {
            Assert.IsTrue((bool)Call(dummy, "Kill", "Dummy-0"));
            bool devMode = (bool)GameType("CurtainCall.Dev.DevMode").GetProperty("Enabled").GetValue(null);
            if (devMode)
            {
                Assert.IsNull(Get(rewards, "Latest"), "개발자 모드는 생존 1명 허용");
                Assert.IsTrue((bool)Call(dummy, "Kill", "Dummy-1"));
            }
            var result = Get(rewards, "Latest");
            Assert.IsNotNull(result);
            Assert.IsFalse((bool)Get(result, "Succeeded"));
            Assert.AreEqual("InsufficientSurvivors", Get(result, "Failure").ToString());
            Assert.AreEqual(0L, Get(result, "Income"));
            Assert.IsFalse((bool)Call(dummy, "Cut", "missing"));
            yield return null;
        }
    }
}
