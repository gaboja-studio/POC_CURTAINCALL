using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.Tightrope.SystemIntegration.Tests
{
    public sealed partial class TightropeCampaignPlayTests
    {
        [UnityTest]
        public IEnumerator RearButtonHopsSawOverTeammateThenKeepsMovingAndHurtingWithSyncedReset()
        {
            yield return BeginPerformance();
            ((Behaviour)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current")).enabled = false;
            var saws = (Component)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current");
            var settings = Get(saws, "Settings");
            bool enabledBefore = (bool)Get(settings, "cooperativeGates");
            var distancesBefore = (float[])Get(settings, "cooperativeGateDistances");
            float approachBefore = (float)Get(settings, "cooperativeGateApproachDistance");
            Component helper = null;
            try
            {
                ConfigureStationaryGateForTest(saws, settings, 30f);
                Set(settings, "cooperativeGateDistances", new[] { 30f, 80f });
                helper = CreatePracticeBotTestPartner(1, 27.8f);
                var waitingPlayer = helper.GetComponent(GameType("CurtainCall.Network.PlayerSync.NetworkPlayer"));
                yield return StandOnRopeForSawTest(1, 32.2f);
                yield return Until(() => (int)Get(saws, "ReleasedGateCount") == 1, 2f, "실제 뒤 발판을 밟아 톱을 튕긴다.");
                yield return Until(() => ReleasedGatePosition(saws).y > course.transform.position.y + 2f, 1f, "동료 머리 위로 포물선 이동");
                yield return Until(() => !(bool)Get(saws, "IsCooperativeGateActive"), 2f, "관문을 떠난 뒤 다음 관문과 독립");
                Assert.AreEqual("Normal", Get(waitingPlayer, "State").ToString(), "기다리던 동료를 죽이지 않고 공중으로 넘어간다.");
                var landed = ReleasedGatePosition(saws);
                Assert.That(landed.y, Is.EqualTo(course.transform.position.y).Within(.01f));
                Assert.That((float)Call(course, "GetDistance", landed), Is.InRange(23f, 24.1f));
                var view = saws.transform.Find("Generated/ReleasedGate0");
                Assert.IsTrue(view.gameObject.activeSelf);
                Assert.IsTrue(view.GetComponentInChildren<MeshCollider>().enabled, "착지 후 실제 충돌체도 계속 작동한다.");
                var guide = saws.transform.Find("Generated/Saw paths (visual only)/Released path 0");
                Assert.IsNotNull(guide);
                Assert.IsTrue(guide.GetComponent<LineRenderer>().enabled);
                Assert.That(guide.GetComponentsInChildren<Collider>(), Is.Empty);
                yield return new WaitForSeconds(.3f);
                Assert.That((float)Call(course, "GetDistance", ReleasedGatePosition(saws)),
                    Is.LessThan((float)Call(course, "GetDistance", landed) - .5f));

                object writer = Call(saws, "WriteCooperativeGate");
                byte[] snapshot;
                try { snapshot = (byte[])Call(writer, "ToArray"); }
                finally { ((IDisposable)writer).Dispose(); }
                var beforeReplay = ReleasedGatePosition(saws);
                Call(saws, "ResetCooperativeGates");
                ReplayCooperativeGate(saws, snapshot, 777UL);
                Assert.That((int)Get(saws, "ReleasedGateCount"), Is.Zero);
                ReplayCooperativeGate(saws, snapshot, 0UL);
                Assert.That(Vector3.Distance(ReleasedGatePosition(saws), beforeReplay), Is.LessThan(.01f), "중도 접속용 전체 스냅샷도 동일한 이동 위치를 복원한다.");

                float incoming = (float)Call(course, "GetDistance", ReleasedGatePosition(saws));
                yield return StandOnRopeForSawTest(1, incoming - 1f);
                yield return Until(() => Get(player, "State").ToString() != "Normal" || Get(run, "State").ToString() == "Failed",
                    2f, "착지 후에도 이동하는 정면 톱의 호스트 즉사 판정");
                int generation = (int)Get(run, "Round");
                var hud = UnityEngine.Object.FindAnyObjectByType(GameType("CurtainCall.Tightrope.UI.TightropeCampaignHud"));
                Assert.IsTrue((bool)Call(hud, "RestartCourseByHost"));
                yield return Until(() => (int)Get(run, "Round") > generation, 5f, "이동 중인 톱을 포함해 코스 재시작");
                Assert.That((int)Get(saws, "ReleasedGateCount"), Is.Zero);
                ReplayCooperativeGate(saws, snapshot, 0UL);
                Assert.That((int)Get(saws, "ReleasedGateCount"), Is.Zero, "이전 라운드의 이동 톱을 복구하지 않는다.");
            }
            finally
            {
                DestroyPracticeBotTestPartner(helper);
                Call(saws, "ResetCooperativeGates");
                Set(settings, "cooperativeGates", enabledBefore);
                Set(settings, "cooperativeGateDistances", distancesBefore);
                Set(settings, "cooperativeGateApproachDistance", approachBefore);
            }
        }

        Vector3 ReleasedGatePosition(Component saws)
        {
            object[] args = { 0, Vector3.zero };
            Assert.IsTrue((bool)Call(saws, "TryGetReleasedGatePosition", args));
            return (Vector3)args[1];
        }
    }
}
