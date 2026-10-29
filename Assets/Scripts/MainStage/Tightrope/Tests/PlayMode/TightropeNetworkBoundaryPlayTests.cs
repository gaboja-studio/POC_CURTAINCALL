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
        public IEnumerator Multiplayer_RelayGrabWaitsForPositionButExpiresAndCancelsAcrossReset()
        {
            JsonUtility.FromJsonOverwrite(previousCourseJson, legacyCourseSettings);
            Call(course, "ImportShape", previousCourseJson);
            yield return BeginPerformance();
            var relay = (Component)Static("CurtainCall.Tightrope.TightropeRelayGate", "Current");
            int lane = (int)Call(relay, "PoleLane", 0);
            float entry = (float)Get(relay, "EntryDistance");
            // 현재 봉 잡기 반경(부상 보정 포함) 밖에서 요청을 보내 위치 도착 대기를 검사한다.
            var other = CreatePracticeBotTestPartner(lane, entry - 4f);
            try
            {
                yield return null;
                var pending = (IDictionary)Get(relay, "pendingGrabs");
                uint Revision() => (uint)Get(Get(((Array)Get(relay, "stations")).GetValue(0), "State"), "Revision");
                void Send(byte operation, uint revision)
                {
                    using var writer = new FastBufferWriter(18, Allocator.Temp);
                    writer.WriteValueSafe(123UL); writer.WriteValueSafe(operation);
                    writer.WriteValueSafe((int)Get(run, "Round")); writer.WriteValueSafe((byte)0); writer.WriteValueSafe(revision);
                    using var reader = new FastBufferReader(writer, Allocator.Temp); reader.Seek(8);
                    Call(relay, "ReadRequest", 779UL, reader);
                }
                Send(0, Revision());
                Assert.That(pending.Count, Is.EqualTo(1), $"active={Get(relay, "IsActive")}, ready={Call(run, "IsParticipantReady", other)}, available={Call(relay, "IsPoleAvailable", 0)}, held={Call(relay, "HeldPole", other)}, pos={other.transform.position}, state={Get(other, "State")}");
                double deadline = (double)Get(pending[779UL], "Until");
                Send(0, Revision());
                Assert.That((double)Get(pending[779UL], "Until"), Is.EqualTo(deadline), "중복 요청이 기한을 연장하면 안 된다.");
                other.transform.position = (Vector3)Call(relay, "PoleFeet", 0);
                Physics.SyncTransforms();
                Assert.IsTrue((bool)Call(relay, "CanUsePole", 0, other));
                Call(relay, "ProcessPendingGrabs");
                Assert.AreEqual(779UL, (ulong)Call(relay, "HolderAt", 0));
                Assert.That(pending.Count, Is.Zero);
                Send(1, Revision());
                other.transform.position -= (Vector3)Get(course, "Forward") * 4f;
                Physics.SyncTransforms();
                Send(0, Revision());
                Assert.That(pending.Count, Is.EqualTo(1));
                Set(pending[779UL], "Until", (double)Get(run, "Clock") - 1d);
                Call(relay, "ProcessPendingGrabs");
                Assert.That(pending.Count, Is.Zero, "위치가 오지 않으면 유효 시간이 끝난다.");
                Send(0, Revision());
                Send(1, Revision());
                Assert.That(pending.Count, Is.Zero, "놓기 요청은 보류된 잡기도 취소한다.");
                Send(0, Revision());
                Assert.That(pending.Count, Is.EqualTo(1));
                var hud = UnityEngine.Object.FindAnyObjectByType(GameType("CurtainCall.Tightrope.UI.TightropeCampaignHud"));
                int round = (int)Get(run, "Round");
                Assert.IsTrue((bool)Call(hud, "RestartCourseByHost"));
                yield return Until(() => (int)Get(run, "Round") > round, 5, "보류 요청 중 전체 재시작");
                yield return null;
                Assert.That(pending.Count, Is.Zero);
                Assert.AreEqual(ulong.MaxValue, (ulong)Call(relay, "HolderAt", 0));
            }
            finally { DestroyPracticeBotTestPartner(other); }
        }

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
        public IEnumerator Multiplayer_RelayRequestReadsOnlyRemainingNamedMessageBytes()
        {
            JsonUtility.FromJsonOverwrite(previousCourseJson, legacyCourseSettings);
            Call(course, "ImportShape", previousCourseJson);
            yield return BeginPerformance();
            var relay = (Component)Static("CurtainCall.Tightrope.TightropeRelayGate", "Current");
            var other = CreatePracticeBotTestPartner((int)Call(relay, "PoleLane", 0), (float)Get(relay, "EntryDistance"));
            try
            {
                yield return null;
                var remoteMover = other.GetComponent(GameType("CurtainCall.Player.PlayerMover"));
                var support = new object[] { remoteMover, 0 };
                bool supported = (bool)Call(course, "TryGetSupportingRope", support);
                Assert.IsTrue((bool)Call(relay, "CanUsePole", 0, other), $"active={Get(relay, "IsActive")}, ready={Call(run, "IsParticipantReady", other)}, "
                    + $"available={Call(relay, "IsPoleAvailable", 0)}, pos={other.transform.position}, pole={Call(relay, "PoleFeet", 0)}, "
                    + $"support={supported}/{support[1]}, controls={Get(remoteMover, "ControlEnabled")}, state={Get(other, "State")}, air={Get(other, "IsAirborne")}");
                using var writer = new FastBufferWriter(18, Allocator.Temp);
                writer.WriteValueSafe(123UL); // NamedMessage.Deserialize에서 이미 소비되는 8바이트 해시
                writer.WriteValueSafe((byte)0); writer.WriteValueSafe((int)Get(run, "Round"));
                var station = ((Array)Get(relay, "stations")).GetValue(0);
                writer.WriteValueSafe((byte)0); writer.WriteValueSafe((uint)Get(Get(station, "State"), "Revision"));
                using var reader = new FastBufferReader(writer, Allocator.Temp);
                reader.Seek(8);
                Call(relay, "ReadRequest", 779UL, reader);
                Assert.AreEqual(779UL, (ulong)Call(relay, "HolderAt", 0));
                Assert.IsTrue((bool)Get(relay, "OutboundOpen"));
                // 출발 봉은 잡을 때 봉이 없는 줄 하나를 무작위로 연다. 그 줄만 이어진다.
                int opened = (int)Call(relay, "OpenedLane", 0);
                int laneCount = (int)Get(course, "LaneCount");
                Assert.That(opened, Is.InRange(0, laneCount - 1));
                Assert.AreNotEqual((int)Call(relay, "PoleLane", 0), opened, "출발 봉이 있는 줄은 열지 않는다.");
                Assert.AreNotEqual((int)Call(relay, "PoleLane", 1), opened, "출발 봉이 있는 줄은 열지 않는다.");
                for (int lane = 0; lane < laneCount; lane++)
                    Assert.AreEqual(lane == opened, (bool)Call(relay, "IsLaneConnected", lane), $"{lane + 1}번 줄");
            }
            finally { DestroyPracticeBotTestPartner(other); }
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
