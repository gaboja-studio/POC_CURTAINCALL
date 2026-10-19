using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.Tightrope.SystemIntegration.Tests
{
    public sealed partial class TightropeCampaignPlayTests
    {
        [UnityTest]
        public IEnumerator LandingWaveObservesRealLandingAndImpactsOnlyNearbySupportedPeersOnce()
        {
            yield return BeginPerformance();
            ((Behaviour)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current")).enabled = false;
            ((Behaviour)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current")).enabled = false;
            yield return StandOnRopeForSawTest(1, 4f);
            var waves = (Component)Static("CurtainCall.Tightrope.TightropeLandingWaves", "Current");
            Assert.IsNotNull(waves, "기존 저장 씬에도 런타임으로 파동 시스템이 연결된다.");
            yield return null;
            Call(mover, "RequestJump");
            yield return Until(() => (bool)Get(mover, "IsAirborne"), 2f, "파동 원인이 되는 실제 점프");
            yield return Until(() => !(bool)Get(mover, "IsAirborne"), 3f, "줄 위 실제 착지");
            yield return Until(() => (int)Get(waves, "EmittedCount") == 1, 1f, "호스트가 착지를 한 번 확정");
            yield return new WaitForSeconds(1f);
            Assert.That((int)Get(waves, "LocalImpactCount"), Is.Zero, "자기 착지 충격을 파동으로 중복 적용하지 않는다.");

            Call(mover, "RequestJump");
            yield return Until(() => (bool)Get(mover, "IsAirborne"), 2f, "공중 순간 이동 검사");
            Call(mover, "SetStartPose", (Vector3)Call(course, "GetLanePosition", 1, 4f),
                Quaternion.LookRotation((Vector3)Get(course, "Forward")), true);
            yield return new WaitForSeconds(.4f);
            Assert.That((int)Get(waves, "EmittedCount"), Is.EqualTo(1), "점프 중 출발 위치 복구는 착지 파동을 만들지 않는다.");

            Call(waves, "Emit", 777UL, 1, 6f);
            yield return Until(() => (int)Get(waves, "LocalImpactCount") == 1, 2f, "같은 줄 2m 거리의 동료 착지 파동");
            Assert.That((float)Get(waves, "LastImpact"), Is.EqualTo(4f).Within(.1f));
            yield return new WaitForSeconds(1f);
            Assert.That((int)Get(waves, "LocalImpactCount"), Is.EqualTo(1), "파동이 남아 있어도 한 번만 맞는다.");
            Call(waves, "Emit", 777UL, 2, 4f);
            yield return new WaitForSeconds(1f);
            Assert.That((int)Get(waves, "LocalImpactCount"), Is.EqualTo(1), "다른 줄 착지는 충격을 주지 않는다.");

            var catches = (Component)Static("CurtainCall.Tightrope.TightropeCatchSystem", "Current");
            var request = catches.GetType().GetNestedType("Request", BindingFlags.NonPublic);
            Call(catches, "ServerHandle", (ulong)Get(player, "OwnerClientId"), Enum.Parse(request, "Prepare"), 0UL);
            Assert.IsTrue((bool)Call(catches, "IsReady", player));
            Call(waves, "Emit", 777UL, 1, 6f);
            yield return Until(() => (int)Get(waves, "LocalImpactCount") == 2, 2f, "받기 준비 중인 동료의 완충");
            Assert.That((float)Get(waves, "LastImpact"), Is.EqualTo(.4f).Within(.05f));
            int generation = (int)Get(run, "Round");
            var hud = UnityEngine.Object.FindAnyObjectByType(GameType("CurtainCall.Tightrope.UI.TightropeCampaignHud"));
            Assert.IsTrue((bool)Call(hud, "RestartCourseByHost"));
            yield return Until(() => (int)Get(run, "Round") > generation, 5f, "파동을 포함해 코스 재시작");
            yield return null;
            Assert.That((int)Get(waves, "ActivePulseCount"), Is.Zero);
            Assert.IsNull(Get(waves, "LocalFeedback"));
        }
    }
}
