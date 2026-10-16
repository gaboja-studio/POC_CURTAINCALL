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
        FieldInfo devModeCache;
        object previousDevMode;

        static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(name)).First(t => t != null);
        static object Get(object target, string name) => target.GetType().GetProperty(name) is PropertyInfo property
            ? property.GetValue(target) : target.GetType().GetField(name).GetValue(target);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name).Invoke(target, args);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // 로컬 EditorPrefs는 바꾸지 않고 정식 인원 규칙을 검증한다.
            devModeCache = GameType("CurtainCall.Dev.DevMode").GetField("_enabled", BindingFlags.Static | BindingFlags.NonPublic);
            previousDevMode = devModeCache.GetValue(null);
            devModeCache.SetValue(null, false);
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
            devModeCache?.SetValue(null, previousDevMode);
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
        public IEnumerator OneDeath_NormalModeKeepsPerformanceOpenAndAwardsSurvivor()
        {
            Assert.IsTrue((bool)Call(dummy, "Kill", "Dummy-0"));
            yield return null;
            AssertSingleSurvivorCanFinish();
        }

        [UnityTest]
        public IEnumerator OneDisconnect_NormalModeKeepsPerformanceOpenAndAwardsSurvivor()
        {
            Assert.IsTrue((bool)Call(rewards, "ObserveParticipant", "Dummy-0", 0, false, false));
            yield return null;
            AssertSingleSurvivorCanFinish();
        }

        void AssertSingleSurvivorCanFinish()
        {
            Assert.IsFalse((bool)GameType("CurtainCall.Dev.DevMode").GetProperty("Enabled").GetValue(null));
            Assert.IsNull(Get(rewards, "Latest"), "정식 모드에서도 생존 1명이면 자동 실패하지 않음");
            Assert.IsTrue((bool)Get(Get(rewards, "State"), "IsOpen"));
            Assert.AreEqual("Performance", Get(Get(campaign, "State"), "Phase").ToString());
            Assert.IsTrue((bool)Call(dummy, "Cheer", "Dummy-1"));
            var survivor = ((IEnumerable)Get(Get(rewards, "State"), "Segments")).Cast<object>().Single(
                participant => (string)Get(participant, "ParticipantId") == "Dummy-1");
            long earned = (long)Get(survivor, "Amount");
            Assert.Greater(earned, 0L);
            Assert.IsTrue((bool)Call(dummy, "Arrive", "Dummy-1"));
            var result = Get(rewards, "Latest");
            Assert.IsTrue((bool)Get(result, "Succeeded"), "공통 최종 정산도 생존 1명의 성공을 허용");
            Assert.AreEqual("None", Get(result, "Failure").ToString());
            Assert.AreEqual(earned, Get(result, "Income"));
            Assert.AreEqual(earned, Get(rewards, "Balance"));
            Assert.AreEqual("RestTime", Get(Get(campaign, "State"), "Phase").ToString());
        }

        [UnityTest]
        public IEnumerator AllDead_AutomaticallyFailsWithoutAward()
        {
            Assert.IsTrue((bool)Call(dummy, "Cheer", "Dummy-1"));
            Assert.IsTrue((bool)Call(dummy, "Kill", "Dummy-0"));
            Assert.IsNull(Get(rewards, "Latest"));
            Assert.IsTrue((bool)Call(dummy, "Kill", "Dummy-1"));
            AssertNoSurvivorFailure();
            Assert.IsFalse((bool)Call(dummy, "Cut", "missing"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator LastSurvivorDisconnects_AutomaticallyFailsWithoutAward()
        {
            Assert.IsTrue((bool)Call(dummy, "Cheer", "Dummy-1"));
            Assert.IsTrue((bool)Call(dummy, "Kill", "Dummy-0"));
            Assert.IsNull(Get(rewards, "Latest"));
            Assert.IsTrue((bool)Call(rewards, "ObserveParticipant", "Dummy-1", 0, false, false));
            AssertNoSurvivorFailure();
            yield return null;
        }

        void AssertNoSurvivorFailure()
        {
            var result = Get(rewards, "Latest");
            Assert.IsNotNull(result);
            Assert.IsFalse((bool)Get(result, "Succeeded"));
            Assert.AreEqual("NoSurvivors", Get(result, "EndReason"));
            Assert.AreEqual("InsufficientSurvivors", Get(result, "Failure").ToString());
            Assert.AreEqual(0L, Get(result, "Income"));
            Assert.AreEqual(0L, Get(rewards, "Balance"));
            Assert.IsFalse((bool)Get(Get(rewards, "State"), "IsOpen"));
            Assert.AreEqual("RestTime", Get(Get(campaign, "State"), "Phase").ToString());
            Assert.IsFalse((bool)Call(dummy, "TimeExpired"), "공통 자동 실패 뒤 중복 종료 거절");
        }
    }
}
