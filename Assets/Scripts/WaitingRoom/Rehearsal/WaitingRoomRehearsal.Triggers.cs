using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Tightrope;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    public sealed partial class WaitingRoomRehearsal
    {
        const byte ChangedLane = 2, Carried = 4, JumpedFromShoulders = 8,
            RelayCrossed = 16, HeldFirst = 32, Flew = 64, CaughtNext = 128;
        sealed class LessonSample
        {
            public Vector3 Previous;
            public bool HasPrevious, LaneAir, RelayEntered;
            public int DepartureLane;
            public float CarryStart;
            public ulong Carrier = ulong.MaxValue;
            public bool Mounted;
            public double AwaitingJumpUntil;
            public float ReleaseDistance;
        }
        readonly Dictionary<ulong, LessonSample> lessonSamples = new();
        readonly Dictionary<ulong, byte> lessonProgress = new();
        int lessonRound = -1;
        byte Progress(ulong id) => lessonProgress.TryGetValue(id, out byte value) ? value : (byte)0;

        // 각 화면이 동일한 원본 봉의 복제 점유 상태를 읽어 메시와 지지면을 함께 바꾼다.
        void UpdateTriggerGeometry()
        {
            if (course == null || Stage?.Kind != RehearsalStageKind.Relay) return;
            var relay = course.GetComponent<TightropeRelayGate>();
            bool connected = relay != null && relay.IsLaneConnected(1);
            bool blocked = course.IsSegmentBlocked(1, 16f);
            if (connected && blocked) course.ClearBlockedSegments();
            else if (!connected && !blocked) course.BlockSegment(1, 12f, 20f, false);
        }

        // 클라이언트의 성공 요청 대신 원본의 복제된 행동 상태를 호스트가 확인한다.
        bool ObserveLesson(NetworkPlayer player)
        {
            if (lessonRound != round) { lessonRound = round; lessonSamples.Clear(); lessonProgress.Clear(); }
            ulong id = player.OwnerClientId;
            byte before = Progress(id), value = before;
            if (!lessonSamples.TryGetValue(id, out var sample)) lessonSamples[id] = sample = new LessonSample();
            if (player.State != PlayerState.Normal)
            {
                lessonSamples.Remove(id);
                // 목마에서 떨어진 뒤 혼자 걸어서 끝내지 못한다. 현재 시도의 연결/도약을 다시 배운다.
                if (Stage.Kind == RehearsalStageKind.Piggyback) value = 0;
                lessonProgress[id] = value;
                return value != before;
            }
            Vector3 position = player.transform.position;
            float distance = course.GetDistance(position);
            bool onRope = course.TryGetRopePoint(position, out int lane, out _);
            if (player.CurrentJump == JumpKind.Lane && !sample.LaneAir)
            {
                sample.LaneAir = true;
                sample.DepartureLane = course.Layout.GetLaneSlotAt(course.GetSide(sample.HasPrevious ? sample.Previous : position));
            }
            if (!player.IsAirborne && sample.LaneAir)
            {
                if (onRope && lane != sample.DepartureLane) value |= ChangedLane;
                sample.LaneAir = false;
            }
            var relay = TightropeRelayGate.Current;
            if (Stage.Kind == RehearsalStageKind.Relay && relay != null && relay.IsActive)
            {
                if (onRope && lane == 1 && relay.IsLaneConnected(1) && distance > 12f && distance < 20f) sample.RelayEntered = true;
                if (sample.RelayEntered && onRope && lane == 1 && distance >= 20f) value |= RelayCrossed;
            }
            var stack = PiggybackSystem.Current;
            var below = stack != null ? stack.GetBelow(player) : null;
            if (Stage.Kind == RehearsalStageKind.Piggyback)
            {
                if (below != null)
                {
                    if (!sample.Mounted || sample.Carrier != below.OwnerClientId)
                    { sample.CarryStart = course.GetDistance(below.transform.position); sample.Carrier = below.OwnerClientId; sample.AwaitingJumpUntil = 0; value = 0; }
                    sample.Mounted = true;
                    if (!below.IsAirborne && course.TryGetRopePoint(below.transform.position, out _, out _)
                        && course.GetDistance(below.transform.position) - sample.CarryStart >= 2f) value |= Carried;
                }
                else if (sample.Mounted)
                {
                    // 해제 RPC가 소유자의 Jump/위치 복제보다 먼저 도착할 수 있다.
                    // 원본 분리 점프의 연결 해제 유예와 같은 기간 동안 원본 비행 상태를 기다린다.
                    sample.AwaitingJumpUntil = (value & Carried) != 0 ? Clock + PiggybackFollow.LaunchTimeout : 0;
                    sample.ReleaseDistance = sample.HasPrevious ? course.GetDistance(sample.Previous) : distance;
                    sample.Mounted = false;
                }
                if (!sample.Mounted && sample.AwaitingJumpUntil > 0)
                {
                    if (player.CurrentJump == JumpKind.InPlace && distance > sample.ReleaseDistance + .1f)
                    { value |= JumpedFromShoulders; sample.AwaitingJumpUntil = 0; }
                    else if (Clock > sample.AwaitingJumpUntil
                        || !player.IsAirborne && Mathf.Abs(position.y - course.transform.position.y) < .3f)
                    { value = 0; sample.AwaitingJumpUntil = 0; } // F 하차/만료는 분리 점프가 아니다.
                }
            }
            var trapeze = TightropeTrapeze.Current;
            if (Stage.UsesPoles && trapeze != null)
            {
                int pole = trapeze.HeldPole(player);
                if (pole == 0) value |= HeldFirst;
                if ((value & HeldFirst) != 0 && trapeze.IsInFlight(player)) value |= Flew;
                if ((value & Flew) != 0 && pole == 1) value |= CaughtNext;
            }
            sample.Previous = position; sample.HasPrevious = true;
            lessonProgress[id] = value;
            return before != value;
        }

        bool LearnedSkill(NetworkPlayer player)
        {
            byte value = Progress(player.OwnerClientId);
            return Stage.Kind switch
            {
                RehearsalStageKind.Balance => true, // 짧은 외줄의 실제 도착 자체가 이동/균형 과제다.
                RehearsalStageKind.LaneChange => (value & ChangedLane) != 0,
                RehearsalStageKind.SinglePole => (value & Flew) != 0,
                RehearsalStageKind.ConsecutivePoles => (value & CaughtNext) != 0,
                RehearsalStageKind.Relay => (value & RelayCrossed) != 0,
                RehearsalStageKind.Piggyback => (value & (Carried | JumpedFromShoulders)) == (Carried | JumpedFromShoulders),
                RehearsalStageKind.Trapeze => (value & CaughtNext) != 0,
                _ => false
            };
        }
        void WriteLesson(FastBufferWriter writer)
        {
            writer.WriteValueSafe(lessonProgress.Count);
            foreach (var pair in lessonProgress) { writer.WriteValueSafe(pair.Key); writer.WriteValueSafe(pair.Value); }
        }
        void ReadLesson(FastBufferReader reader)
        {
            lessonProgress.Clear(); reader.ReadValueSafe(out int count);
            for (int i = 0; i < count; i++) { reader.ReadValueSafe(out ulong id); reader.ReadValueSafe(out byte value); lessonProgress[id] = value; }
        }

        // 기물 상태별 안내도 공연의 실제 활성 장치에서 읽는다. 리허설만의 그네/트리거 상태를 만들지 않는다.
        string NativeDeviceQuest(RehearsalStageKind kind)
        {
            questKey = kind == RehearsalStageKind.Relay ? "relay" : "trapeze";
            return kind == RehearsalStageKind.Relay ? TightropeRelayGate.Current?.LocalHint : TightropeTrapeze.Current?.LocalHint;
        }
    }
}
