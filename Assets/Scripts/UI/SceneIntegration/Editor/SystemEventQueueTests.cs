using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration.EditorTools.Tests
{
    /// <summary>
    /// 2026-11-26 Task-205(사용자): 가운데 시스템 배너는 한 번에 하나, 앞 배너가 사라지기 전에는 다음 배너를 띄우지 않고 차례대로.
    /// 실제 HUD 프리팹을 만들어 확인한다. 화면 모습·시간은 사람 확인 대상이다.
    /// </summary>
    public sealed class SystemEventQueueTests
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        HudUIManager hud;
        Transform owner;

        [SetUp]
        public void SetUp()
        {
            var prefab = Resources.Load<HudUIManager>("Prefabs/UIs/Roles/HudUIManager");
            Assume.That(prefab, Is.Not.Null);
            hud = Object.Instantiate(prefab);
            hud.gameObject.hideFlags = HideFlags.HideAndDontSave;
            typeof(HudUIManager).GetMethod("Awake", Any).Invoke(hud, null);
            owner = new GameObject("SystemEventOwner") { hideFlags = HideFlags.HideAndDontSave }.transform;
        }

        [TearDown]
        public void TearDown()
        {
            if (hud) Object.DestroyImmediate(hud.gameObject);
            if (owner) Object.DestroyImmediate(owner.gameObject);
        }

        void Tick() => typeof(HudUIManager).GetMethod("LateUpdate", Any).Invoke(hud, null);
        // Close(true)와 같은 결과(닫힘 표시 + 꺼짐). 실행 중용 Destroy는 편집 모드에서 쓰지 않는다.
        static void Gone(HudRoleCard card) { typeof(HudRoleCard).GetProperty("Closing").SetValue(card, true); card.gameObject.SetActive(false); }

        [Test]
        public void SecondBanner_WaitsUntilFirstIsGone_ThenShowsInOrder()
        {
            var first = hud.ShowSystemEvent("첫 배너", owner, "1", 5f);
            var second = hud.ShowSystemEvent("둘째 배너", owner, "2", 5f);
            var third = hud.ShowSystemEvent("셋째 배너", owner, "3", 5f);
            Assert.IsTrue(first.gameObject.activeSelf);
            Assert.IsFalse(second.gameObject.activeSelf, "앞 배너가 있는 동안 다음 배너는 나오지 않는다");
            Assert.IsFalse(third.gameObject.activeSelf);
            Assert.AreEqual(2, hud.WaitingSystemEvents);

            Tick();
            Assert.IsTrue(first.gameObject.activeSelf, "앞 배너를 밀어내지 않는다");
            Assert.IsFalse(second.gameObject.activeSelf);

            Gone(first);
            Tick();
            Assert.IsTrue(second.gameObject.activeSelf, "앞 배너가 사라지면 다음 차례");
            Assert.AreEqual("둘째 배너", second.Content);
            Assert.IsFalse(third.gameObject.activeSelf);

            Gone(second);
            Tick();
            Assert.IsTrue(third.gameObject.activeSelf);
            Assert.AreEqual(0, hud.WaitingSystemEvents);
        }

        [Test]
        public void WaitingBannerClosedByCaller_IsSkipped()
        {
            var first = hud.ShowSystemEvent("첫 배너", owner, "1", 5f);
            var second = hud.ShowSystemEvent("취소된 배너", owner, "2", 5f);
            var third = hud.ShowSystemEvent("셋째 배너", owner, "3", 5f);
            Gone(second);
            Gone(first);
            Tick();
            Assert.IsTrue(third.gameObject.activeSelf);
        }
    }
}
