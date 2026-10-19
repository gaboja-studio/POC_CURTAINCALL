using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.Tightrope.SystemIntegration.Tests
{
    public sealed partial class TightropeCampaignPlayTests
    {
        [UnityTest]
        public IEnumerator TeamPassagePlateOpensOnlyLocalHazardsAndResumesTheirPhase()
        {
            yield return BeginPerformance();
            ((Behaviour)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current")).enabled = false;
            var saws = (Component)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current");
            var settings = Get(saws, "Settings");
            yield return StandOnRopeForSawTest(1, (float)Call(settings, "GetHorizontalDistance", 0) + 2f);
            float originalDuration = (float)Get(settings, "teamPassageDuration");
            Set(settings, "teamPassageDuration", 1f);
            try
            {
                PutFrontalSawBeforePlayer(saws, settings);
                yield return Until(() => (bool)Get(saws, "IsTeamPassageOpen"), 2f, "실제 접지 후 협동 통로 개방");
                Assert.IsTrue((bool)Call(saws, "IsHorizontalSafeForTeamPassage", 0));
                Assert.IsFalse((bool)Call(saws, "IsHorizontalSafeForTeamPassage", 5), "먼 구간은 계속 위험해야 한다.");
                Assert.IsFalse((bool)Call(saws, "IsHorizontalMoving", 0));
                Vector3 before = (Vector3)Call(saws, "GetHorizontalPosition", 0);
                yield return new WaitForSeconds(0.25f);
                Assert.That(Vector3.Distance(before, (Vector3)Call(saws, "GetHorizontalPosition", 0)), Is.LessThan(0.001f));
                Assert.IsFalse((bool)Get(saws, "IsVerticalActive"));
                Assert.IsTrue(saws.transform.Find("Generated/Plate").gameObject.activeSelf, "정면 톱이 사라져도 남은 시간 동안 발판 표시를 남긴다.");
                Assert.That(saws.transform.Find("Generated/HorizontalSaw01").position.y, Is.LessThan(before.y - 0.1f));
                yield return Until(() => !(bool)Get(saws, "IsTeamPassageOpen"), 2f, "협동 통로 만료");
                Assert.IsTrue((bool)Call(saws, "IsHorizontalMoving", 0));
                Assert.IsFalse((bool)Call(saws, "IsHorizontalSafeForTeamPassage", 0));
                Assert.That(Vector3.Distance(before, (Vector3)Call(saws, "GetHorizontalPosition", 0)), Is.LessThan(0.4f), "멈춘 위치에서 이어져야 한다.");
                yield return new WaitForSeconds(0.1f);
                Assert.That(Vector3.Distance(before, (Vector3)Call(saws, "GetHorizontalPosition", 0)), Is.GreaterThan(0.1f));
                Assert.AreEqual(0, Convert.ToInt32(Get(player, "LostParts")));
            }
            finally { Set(settings, "teamPassageDuration", originalDuration); }
        }

        [UnityTest]
        public IEnumerator TeamPassageRejectsAirbornePressAndRestoresHostSnapshotThenResets()
        {
            yield return BeginPerformance();
            ((Behaviour)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current")).enabled = false;
            yield return StandOnRopeForSawTest(1, 14f);
            var saws = (Component)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current");
            var settings = Get(saws, "Settings");
            Call(mover, "RequestJump");
            yield return Until(() => (bool)Get(mover, "IsAirborne"), 2f, "발판 위 점프");
            PutFrontalSawBeforePlayer(saws, settings);
            Assert.IsFalse((bool)Call(saws, "IsAnyoneOnPlate"), "공중에서 발판 근처를 스쳐도 누르지 않는다.");
            Assert.IsFalse((bool)Get(saws, "IsTeamPassageOpen"));
            yield return Until(() => !(bool)Get(mover, "IsAirborne"), 3f, "발판 줄 착지");
            PutFrontalSawBeforePlayer(saws, settings);
            yield return Until(() => (bool)Get(saws, "IsTeamPassageOpen"), 2f, "착지한 동료의 발판 눌림");

            // 실제 이름 메시지의 바이트 코덱을 재생한다. 다중 클라이언트 실행 검사는 아니다.
            object writer = Call(saws, "WriteTeamPassages");
            byte[] bytes;
            try { bytes = (byte[])Call(writer, "ToArray"); }
            finally { ((IDisposable)writer).Dispose(); }
            Vector3 before = (Vector3)Call(saws, "GetHorizontalPosition", 0);
            Call(saws, "ClearTeamPassages");
            ReplayTeamPassage(saws, bytes, 123UL);
            Assert.IsFalse((bool)Get(saws, "IsTeamPassageOpen"), "클라이언트가 안전 통로를 확정할 수 없다.");
            ReplayTeamPassage(saws, bytes, 0UL);
            Assert.IsTrue((bool)Get(saws, "IsTeamPassageOpen"));
            Assert.That(Vector3.Distance(before, (Vector3)Call(saws, "GetHorizontalPosition", 0)), Is.LessThan(0.001f));
            int oldRound = (int)Get(run, "Round");
            var hud = Object.FindAnyObjectByType(GameType("CurtainCall.Tightrope.UI.TightropeCampaignHud"));
            Assert.IsTrue((bool)Call(hud, "RestartCourseByHost"));
            yield return Until(() => (int)Get(run, "Round") > oldRound, 5f, "협동 상태 포함 전체 리셋");
            Assert.IsFalse((bool)Get(saws, "IsTeamPassageOpen"));
            ReplayTeamPassage(saws, bytes, 0UL);
            Assert.IsFalse((bool)Get(saws, "IsTeamPassageOpen"), "이전 시도의 늦은 메시지가 새 시도를 열면 안 된다.");
        }

        void PutFrontalSawBeforePlayer(Component saws, object settings)
        {
            float plate = (float)Call(course, "GetDistance", player.transform.position);
            float elapsed = ((float)Get(settings, "verticalSpawnDistance") - plate + (float)Get(settings, "plateOffset"))
                / (float)Get(settings, "verticalSpeed");
            Call(saws, "SetVertical", true, 1, 1, elapsed, -1f);
        }

        static void ReplayTeamPassage(Component saws, byte[] bytes, ulong sender)
        {
            object temporaryAllocator = Enum.Parse(GameType("Unity.Collections.Allocator"), "Temp");
            var reader = Activator.CreateInstance(GameType("Unity.Netcode.FastBufferReader"), bytes, temporaryAllocator, -1, 0);
            try { Call(saws, "HandleTeamPassageMessage", sender, reader); }
            finally { ((IDisposable)reader).Dispose(); }
        }
    }
}
