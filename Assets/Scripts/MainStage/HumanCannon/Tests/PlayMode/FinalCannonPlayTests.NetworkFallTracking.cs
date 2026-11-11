using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.HumanCannon.Tests
{
    public sealed partial class FinalCannonPlayTests
    {
        [UnityTest, Category("TutorialFallTracking")]
        public IEnumerator TeleportFallResetWaitsForDelayedNetworkPosition()
        {
            DisableEnhancementThreats();
            Vector3 destination = new Vector3(9500, 9500, 9500);
            Vector3 oldPosition = destination + Vector3.up * 20f;
            Call(mover, "SetStartPose", oldPosition, Quaternion.identity, true);
            int restart = (int)Get(player, "RestartVersion");
            int revive = (int)Get(Get(player, "reviveCount"), "Value");
            uint accepted = (uint)Get(player, "acceptedFallSequence");

            // Synthetic RPC-before-pose ordering on the host fixture, not a two-client transport test.
            Call(player, "ResetFallTeleportRpc", destination, restart, revive);
            Assert.IsTrue((bool)Get(player, "fallTeleportPending"));
            Call(player, "LateUpdate");
            Call(player, "ReportFallPeakRpc", oldPosition.y + 3f, restart, revive, accepted + 1u);
            Call(player, "ReportFallLandingRpc", accepted + 1u, restart, revive, oldPosition.y, oldPosition);
            Assert.IsFalse((bool)Get(player, "fallAirborne"), "옛 서버 위치에서 낙하를 다시 시작하지 않는다.");
            Assert.That(Call(player, "FallHeightAt", destination), Is.EqualTo(0f));
            Assert.That(Get(player, "acceptedFallSequence"), Is.EqualTo(accepted));
            Call(player, "ReportFallPeakRpc", destination.y + 1f, restart, revive, accepted + 1u);
            Assert.That(Call(player, "FallHeightAt", destination), Is.EqualTo(1f), "대기 중에도 유효한 소유자 최고점 보고는 기록한다.");

            // Bypass mover.Teleported deliberately: this is the later NetworkTransform position delivery.
            player.transform.position = destination;
            Physics.SyncTransforms();
            Call(player, "LateUpdate");
            Assert.IsFalse((bool)Get(player, "fallTeleportPending"));
            Assert.That(Call(player, "FallHeightAt", destination), Is.EqualTo(1f), "목적지 도착이 이미 받은 소유자 최고점을 지우면 안 된다.");
            player.transform.position = destination + Vector3.up * 2f;
            Call(player, "LateUpdate");
            Assert.That(Call(player, "FallHeightAt", destination), Is.EqualTo(2f), "위치 도착 뒤 새 낙하는 정상 추적한다.");
            yield return null;
        }

        [UnityTest, Category("TutorialFallTracking")]
        public IEnumerator TeleportDestinationFrameCanBeSkippedAndValidLandingStillCompletes()
        {
            DisableEnhancementThreats();
            var rules = Get(StaticGet("CurtainCall.Settings.GameSettings", "Base"), "BodyDamage");
            float safeHeight = (float)Get(rules, "FallInjuryHeight") * .75f;
            Vector3 contact = new Vector3(5200, 100, 5200);
            Vector3 destination = contact + Vector3.right * 4f + Vector3.up * 2f;
            var floor = new GameObject("skipped teleport pose landing floor");
            floor.transform.position = contact - Vector3.up * .5f;
            floor.AddComponent<BoxCollider>().size = new Vector3(20, 1, 20);
            try
            {
                Call(mover, "SetStartPose", destination + Vector3.up * 20f, Quaternion.identity, true);
                int restart = (int)Get(player, "RestartVersion");
                int revive = (int)Get(Get(player, "reviveCount"), "Value");
                Call(player, "ResetFallTeleportRpc", destination, restart, revive);
                // The server receives only later poses, never one within a metre of the destination.
                player.transform.position = contact + Vector3.up * 1.5f;
                Call(player, "ReportFallPeakRpc", contact.y + safeHeight, restart, revive, (uint)Get(player, "acceptedFallSequence") + 1u);
                Call(player, "LateUpdate");
                Assert.IsTrue((bool)Get(player, "fallTeleportPending"));
                Assert.That(Call(player, "FallHeightAt", contact), Is.EqualTo(safeHeight).Within(.001f));
                player.transform.position = contact;
                Physics.SyncTransforms();
                Call(player, "LateUpdate");
                Assert.IsTrue((bool)Get(player, "fallTeleportPending"));
                uint sequence = (uint)Get(player, "acceptedFallSequence") + 1u;
                Call(player, "ReportFallLandingRpc", sequence, restart, revive, contact.y + safeHeight, contact);
                Assert.That(Get(player, "acceptedFallSequence"), Is.EqualTo(sequence));
                Assert.IsFalse((bool)Get(player, "fallTeleportPending"));
                Assert.That(Call(player, "FallHeightAt", contact), Is.EqualTo(0f));
                Assert.That(Get(player, "State").ToString(), Is.EqualTo("Normal"));
                Assert.That(Convert.ToInt32(Get(player, "LostParts")), Is.EqualTo(0));
                yield return null;
            }
            finally { Object.Destroy(floor); }
        }

        [UnityTest, Category("TutorialFallTracking")]
        public IEnumerator AcceptedTeleportLandingDoesNotResampleStillDelayedServerHeight()
        {
            DisableEnhancementThreats();
            Vector3 contact = new Vector3(5400, 100, 5400);
            var floor = new GameObject("delayed landing pose floor");
            floor.transform.position = contact - Vector3.up * .5f;
            floor.AddComponent<BoxCollider>().size = new Vector3(20, 1, 20);
            try
            {
                Call(mover, "SetStartPose", contact + Vector3.up * 4.5f, Quaternion.identity, true);
                Physics.SyncTransforms();
                int restart = (int)Get(player, "RestartVersion");
                int revive = (int)Get(Get(player, "reviveCount"), "Value");
                Call(player, "ResetFallTeleportRpc", contact, restart, revive);
                uint sequence = (uint)Get(player, "acceptedFallSequence") + 1u;
                Call(player, "ReportFallLandingRpc", sequence, restart, revive, contact.y, contact);
                Assert.That(Get(player, "acceptedFallSequence"), Is.EqualTo(sequence));
                Assert.IsTrue((bool)Get(player, "fallTeleportPending"));
                Call(player, "LateUpdate");
                Assert.That(Call(player, "FallHeightAt", contact), Is.EqualTo(0f), "착지 확인 이후에도 아직 도착하지 않은 서버 높이는 샘플링하지 않는다.");
                player.transform.position = contact;
                Physics.SyncTransforms();
                Call(player, "LateUpdate");
                Assert.IsFalse((bool)Get(player, "fallTeleportPending"));
                Assert.That(Get(player, "State").ToString(), Is.EqualTo("Normal"));
                Assert.That(Convert.ToInt32(Get(player, "LostParts")), Is.EqualTo(0));
                yield return null;
            }
            finally { Object.Destroy(floor); }
        }

        [UnityTest, Category("TutorialFallTracking")]
        public IEnumerator ReviveImmediatelyClearsTrackingAndRejectsPreviousLifeFallMessages()
        {
            DisableEnhancementThreats();
            Vector3 contact = new Vector3(9500, 9500, 9500);
            Call(mover, "SetStartPose", contact, Quaternion.identity, true);
            int restart = (int)Get(player, "RestartVersion");
            var generation = Get(player, "reviveCount");
            int oldRevive = (int)Get(generation, "Value");
            Call(player, "BeginFallAt", contact + Vector3.up * 20f);
            Put(player, "fallAirborne", true);
            Put(player, "naturalFallQueued", true);
            Put(generation, "Value", oldRevive + 1);
            Assert.That(Call(player, "FallHeightAt", contact), Is.EqualTo(0f));
            Assert.IsFalse((bool)Get(player, "fallAirborne"));
            Assert.IsFalse((bool)Get(player, "naturalFallQueued"));

            Call(player, "BeginFallAt", contact + Vector3.up);
            Put(player, "fallSequence", 77u);
            Put(player, "landingPending", true);
            Call(player, "ReportFallPeakRpc", contact.y + 2f, restart, oldRevive, 78u);
            Call(player, "ResetFallTeleportRpc", contact + Vector3.up * 20f, restart, oldRevive);
            Call(player, "ConfirmFallLandingRpc", 77u, restart, oldRevive);
            Call(player, "ReleaseIntoFallRpc", restart, oldRevive);
            Call(player, "RequestNaturalFallRpc", restart, oldRevive);
            Call(player, "SpringFromGroundRpc", 10f, restart, oldRevive);
            Assert.That(Call(player, "FallHeightAt", contact), Is.EqualTo(1f));
            Assert.IsTrue((bool)Get(player, "landingPending"), "지난 생명의 응답이 현재 착지 대기를 지우면 안 된다.");
            Assert.IsFalse((bool)Get(player, "fallTeleportPending"));
            Assert.IsFalse((bool)Get(player, "IsDroppingFromSupport"));
            Call(player, "ReportFallPeakRpc", contact.y + 2f, restart, oldRevive + 1, 78u);
            Assert.That(Call(player, "FallHeightAt", contact), Is.EqualTo(2f), "현재 생명의 보고는 계속 허용한다.");
            yield return null;
        }

        [UnityTest, Category("TutorialFallTracking")]
        public IEnumerator PreviousLifeLandingCannotDamageRevivedPlayerAtValidFloor()
        {
            DisableEnhancementThreats();
            var rules = Get(StaticGet("CurtainCall.Settings.GameSettings", "Base"), "BodyDamage");
            float fatalHeight = (float)Get(rules, "FallDeathHeight") + 2f;
            Vector3 contact = new Vector3(5000, 100, 5000);
            var floor = new GameObject("fall epoch test floor");
            floor.transform.position = contact - Vector3.up * .5f;
            floor.AddComponent<BoxCollider>().size = new Vector3(20, 1, 20);
            try
            {
                Call(mover, "SetStartPose", contact, Quaternion.identity, true);
                Physics.SyncTransforms();
                int restart = (int)Get(player, "RestartVersion");
                var generation = Get(player, "reviveCount");
                int oldRevive = (int)Get(generation, "Value");
                Put(generation, "Value", oldRevive + 1);
                // A new, valid fall may have the same geometry as the delayed report from the previous life.
                Call(player, "BeginFallAt", contact + Vector3.up * fatalHeight);
                uint accepted = (uint)Get(player, "acceptedFallSequence");
                Call(player, "ReportFallLandingRpc", accepted + 1u, restart, oldRevive, contact.y + fatalHeight, contact);
                Assert.That(Get(player, "acceptedFallSequence"), Is.EqualTo(accepted));
                Assert.That(Get(player, "State").ToString(), Is.EqualTo("Normal"));
                Assert.That(Convert.ToInt32(Get(player, "LostParts")), Is.EqualTo(0));
                Call(player, "ResetFallTracking");
                yield return null;
            }
            finally { Object.Destroy(floor); }
        }
    }
}
