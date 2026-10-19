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
        public IEnumerator FinaleGateAllowsAnInjuredLastSurvivorToWalkThroughAndFinish()
        {
            yield return BeginPerformance();
            ((Behaviour)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current")).enabled = false;
            var saws = (Component)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current");
            var settings = Get(saws, "Settings");
            bool gatesBefore = (bool)Get(settings, "cooperativeGates");
            var distancesBefore = (float[])Get(settings, "cooperativeGateDistances");
            float approachBefore = (float)Get(settings, "cooperativeGateApproachDistance");
            float closedBefore = (float)Get(settings, "finaleGateClosedSeconds");
            try
            {
                Set(settings, "finaleGateClosedSeconds", .5f);
                float gate = (float)Get(course, "FinishDistance") - 5f;
                ConfigureStationaryGateForTest(saws, settings, gate);
                yield return StandOnRopeForSawTest(1, gate - 1.8f);
                Assert.IsTrue((bool)Get(saws, "IsFinaleSoloGate"));
                Assert.IsFalse((bool)Get(saws, "IsCooperativeGatePassable"));
                Call(player, "ServerCut", 2);
                yield return Until(() => (float)Get(mover, "BodyHeight") < 1f, 2f, "최후반 생존자의 실제 양다리 부상");
                Assert.That((int)Get(run, "InProgressCount"), Is.EqualTo(1));
                yield return Until(() => (bool)Get(saws, "IsCooperativeGatePassable"), 3f, "동료 없이 주기적으로 열리는 최후반 관문");
                var collider = saws.transform.Find("Generated/CooperativeGate").GetComponentInChildren<MeshCollider>();
                Assert.IsFalse(collider.enabled);
                float deadline = Time.realtimeSinceStartup + 24f;
                while (Get(bridge, "Result") == null && Time.realtimeSinceStartup < deadline)
                {
                    Call(mover, "SetMoveInput", 1f);
                    yield return null;
                    Assert.AreNotEqual("Fallen", Get(player, "State").ToString(), "기어가는 생존자도 열린 관문을 실제 보행으로 통과한다.");
                }
                Assert.IsNotNull(Get(bridge, "Result"));
                Assert.IsTrue((bool)Get(Get(bridge, "Result"), "Succeeded"));
                Assert.That(Convert.ToInt32(Get(player, "LostParts")), Is.Not.Zero, "통과를 위해 부상을 복구하지 않는다.");
            }
            finally
            {
                Call(saws, "ResetCooperativeGates");
                Set(settings, "cooperativeGates", gatesBefore);
                Set(settings, "cooperativeGateDistances", distancesBefore);
                Set(settings, "cooperativeGateApproachDistance", approachBefore);
                Set(settings, "finaleGateClosedSeconds", closedBefore);
            }
        }
    }
}
