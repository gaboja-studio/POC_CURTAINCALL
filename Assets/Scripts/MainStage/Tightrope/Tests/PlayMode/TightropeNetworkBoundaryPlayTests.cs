using System;
using System.Collections;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.Tightrope.SystemIntegration.Tests
{
    public sealed partial class TightropeCampaignPlayTests
    {


        [UnityTest]
        public IEnumerator Multiplayer_SawSnapshotAcceptsFullOneBasedRangeAndRejectsOldRound()
        {
            yield return BeginPerformance();
            var saws = (Component)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current");
            int count = ((Array)Get(saws, "tracks")).Length, round = (int)Get(run, "Round");
            Assert.That(count, Is.GreaterThan(0));
            void Receive(int generation)
            {
                using var writer = new FastBufferWriter(25, Allocator.Temp);
                writer.WriteValueSafe(123UL); writer.WriteValueSafe(generation); writer.WriteValueSafe(1);
                writer.WriteValueSafe((byte)0); writer.WriteValueSafe((short)1); writer.WriteValueSafe((short)count);
                writer.WriteValueSafe(0f);
                using var reader = new FastBufferReader(writer, Allocator.Temp); reader.Seek(8);
                Call(saws, "HandleHorizontalMessage", NetworkManager.ServerClientId, reader);
            }
            Call(saws, "ResetObstacles");
            Receive(round - 1);
            Assert.IsFalse((bool)Call(saws, "IsHorizontalMoving", 0));
            Receive(round);
            Assert.IsTrue((bool)Call(saws, "IsHorizontalMoving", 0));
            Assert.IsTrue((bool)Call(saws, "IsHorizontalMoving", count - 1));
        }



        [UnityTest]
        public IEnumerator Multiplayer_RestartRejectsOldPositionsAndStalePlacementUntilOwnerAndHostAgree()
        {
            yield return BeginPerformance();
            var other = CreatePracticeBotTestPartner(1, 130f);
            var saws = (Component)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current");
            var hud = UnityEngine.Object.FindAnyObjectByType(GameType("CurtainCall.Tightrope.UI.TightropeCampaignHud"));
            int oldRound = (int)Get(run, "Round"), oldRestart = (int)Get(other, "RestartVersion");
            try
            {
                Assert.IsTrue((bool)Call(hud, "RestartCourseByHost"));
                yield return Until(() => (int)Get(run, "Round") > oldRound, 5, "새 공연 회차 시작");
                Assert.IsFalse((bool)Call(run, "IsParticipantReady", other));
                Assert.IsFalse((bool)Call(other, "ServerCanTakeDamage"));
                Assert.IsFalse((bool)Call(run, "HasCampaignArrival", other));
                Assert.That((float)Call(saws, "GetLeaderDistance"), Is.LessThan(1f), "원격의 이전 130m 위치는 선두가 아니다.");
                var start = (Pose)Call(course, "GetStartPose", 1);
                void Placement(int round, int version)
                {
                    using var writer = new FastBufferWriter(29, Allocator.Temp);
                    writer.WriteValueSafe(123UL); writer.WriteValueSafe(false); writer.WriteValueSafe(round);
                    writer.WriteValueSafe(version); writer.WriteValueSafe(start.position);
                    using var reader = new FastBufferReader(writer, Allocator.Temp); reader.Seek(8);
                    Call(run, "HandlePlacement", 779UL, reader);
                }
                int newRound = (int)Get(run, "Round"), newRestart = (int)Get(other, "RestartVersion");
                Placement(newRound, newRestart);
                Assert.IsFalse((bool)Call(run, "IsParticipantReady", other), "소유자 알림만 먼저 오고 위치는 옛날이면 계속 대기한다.");
                other.transform.position = start.position;
                Placement(oldRound, newRestart);
                Placement(newRound, oldRestart);
                Assert.IsFalse((bool)Call(run, "IsParticipantReady", other), "옛 회차/복구 번호를 거절한다.");
                Placement(newRound, newRestart);
                Assert.IsTrue((bool)Call(run, "IsParticipantReady", other));
                Assert.IsTrue((bool)Call(other, "ServerCanTakeDamage"), "정상 복귀한 뒤에는 위험 판정을 다시 허용한다.");
            }
            finally { DestroyPracticeBotTestPartner(other); }
        }
    }
}
