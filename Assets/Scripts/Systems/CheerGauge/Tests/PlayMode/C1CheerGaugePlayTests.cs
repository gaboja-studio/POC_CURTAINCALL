using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.CheerGauge.Tests
{
    public sealed class C1CheerGaugePlayTests
    {
        GameObject root;
        Component view;
        Component binding;
        static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name).Invoke(target, args);
        static object Get(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
        UnityEngine.UI.Image Fill(int slot) => root.transform.Find("RingRoot/FillP" + slot).GetComponent<UnityEngine.UI.Image>();
        string TotalText => root.transform.Find("RingRoot/Total").GetComponent<TMP_Text>().text;
        bool Set(long[] scores, long capacity, bool animate = false) => (bool)Call(view, "SetScores", scores, capacity, animate);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var prefab = Resources.Load<GameObject>("Prefabs/UIs/CheerGauge/C1CheerGauge");
            Assert.IsNotNull(prefab, "생성된 재사용 프리팹이 필요합니다.");
            root = Object.Instantiate(prefab);
            view = root.GetComponent(GameType("CurtainCall.CheerGauge.C1CheerGaugeView"));
            binding = root.GetComponent(GameType("CurtainCall.CheerGauge.C1CheerGaugeBinding"));
            ((Behaviour)binding).enabled = false;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root != null) Object.Destroy(root);
            foreach (string name in new[] { "CurtainCall.Reward.PerformanceRewards", "CurtainCall.Campaign.CampaignRun", "CurtainCall.Network.Session.NetworkSessionManager" })
            {
                var type = GameType(name);
                var instance = type.GetProperty("Instance")?.GetValue(null) as Component;
                if (instance != null) Object.Destroy(instance.gameObject);
            }
            var driver = GameObject.Find("OfflinePerformanceDriver");
            if (driver != null) Object.Destroy(driver);
            var preview = GameObject.Find("LivePreviewTest");
            if (preview != null) Object.Destroy(preview);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SampleAndDifficultyCapsPreserveActualTotal()
        {
            Assert.AreEqual("—", TotalText, "프리팹에 샘플 점수가 남지 않아야 합니다.");
            Assert.IsTrue(Set(new long[] { 60, 45, 30, 20 }, 400));
            Assert.AreEqual("155", TotalText);
            Assert.AreEqual(0.15f, Fill(1).fillAmount, 0.0001f);
            Assert.AreEqual(0.1125f, Fill(2).fillAmount, 0.0001f);
            Assert.AreEqual(-54, Mathf.DeltaAngle(0, Fill(2).rectTransform.localEulerAngles.z), 0.001f);
            Assert.IsTrue(Set(new long[] { 200, 200, 200, 200 }, 800));
            Assert.AreEqual("800", TotalText);
            for (int i = 1; i <= 4; i++) Assert.AreEqual(0.25f, Fill(i).fillAmount, 0.0001f);
            Assert.IsTrue(Set(new long[] { 150 }, 150));
            Assert.AreEqual("150", TotalText);
            Assert.AreEqual(1, Fill(1).fillAmount);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AnimationRepeatedSnapshotAndZeroSegments()
        {
            Set(new long[] { 60, 45, 30, 20 }, 400);
            Assert.IsTrue((bool)Call(view, "FlashFall", 1));
            Set(new long[] { 60, 0, 30, 20 }, 400, true);
            for (int i = 0; i < 20; i++)
            {
                Set(new long[] { 60, 0, 30, 20 }, 400, true);
                yield return new WaitForSecondsRealtime(0.02f);
            }
            Assert.AreEqual("110", TotalText);
            Assert.AreEqual(0, Fill(2).fillAmount);
            Assert.IsFalse(Fill(2).enabled);
            root.SetActive(false);
            root.SetActive(true);
            Assert.AreEqual("110", TotalText);
            Assert.IsFalse(Set(new long[] { -1 }, 100));
            Assert.AreEqual("—", TotalText);
            Assert.IsTrue(root.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).All(g => !g.raycastTarget));
        }

        [UnityTest]
        public IEnumerator LateRewardServiceBindsAndPublicInputsRefreshGauge()
        {
            ((Behaviour)binding).enabled = true;
            yield return null;
            Assert.AreEqual("—", TotalText);
            var go = new GameObject("LivePreviewTest");
            var preview = go.AddComponent(GameType("CurtainCall.CheerGauge.C1CheerGaugePreview"));
            Call(preview, "Configure", view, binding, null);
            yield return null;
            Call(preview, "BeginLive");
            yield return null;
            yield return null;
            Assert.IsTrue((bool)Call(preview, "BeginLive"));
            yield return null;
            Assert.AreEqual("0", TotalText);
            Assert.IsTrue((bool)Call(preview, "ChangeScore", 0, true));
            Assert.IsTrue((bool)Call(preview, "ChangeScore", 1, true));
            Assert.AreEqual("20", TotalText);
            Assert.IsTrue((bool)Call(preview, "ChangeScore", 0, false));
            Assert.AreEqual("15", TotalText);
            Assert.IsTrue((bool)Call(preview, "KillFirst"));
            // 사망자 호응 5는 지우지 않고 절반(2)으로 인정해 보여 준다.
            Assert.AreEqual("12", TotalText);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.Greater(Fill(2).fillAmount, 0);
            root.SetActive(false);
            root.SetActive(true);
            yield return null;
            Assert.AreEqual("12", TotalText);
            var rewardType = GameType("CurtainCall.Reward.PerformanceRewards");
            var previous = (Component)rewardType.GetProperty("Instance").GetValue(null);
            Object.Destroy(previous.gameObject);
            yield return null;
            yield return null;
            Assert.AreEqual("—", TotalText, "서비스가 사라지면 이전 점수를 지웁니다.");
            var replacement = (Component)rewardType.GetMethod("EnsureExists").Invoke(null, null);
            Assert.IsNotNull(replacement);
            yield return null;
            Assert.AreEqual("—", TotalText, "새 서비스에 공연 데이터가 없으면 대기합니다.");
        }
    }
}
