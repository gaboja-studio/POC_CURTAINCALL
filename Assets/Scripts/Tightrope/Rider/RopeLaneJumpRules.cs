using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    /// <summary>
    /// 내 캐릭터의 옆줄 점프(Q/E 홀드 + Space)에 코스 규칙을 붙인다.
    /// 1) 뛸 수 있는지: 뛰는 지점과 착지 예상 지점이 모두 옆줄 이동 가능 구간 안, 착지 예상 거리에 그 방향 옆 줄이 있고 막히지 않음(불타는 구간 포함).
    ///    하나라도 아니면 입력을 무시한다(아무 일도 없음).
    /// 2) 착지 자리: 도착 줄의 착지 자리에 다른 플레이어가 있으면 그 뒤쪽(코스 진행 반대쪽)으로 착지 지점을 당긴다. 뒤에 또 있으면 맨 뒤 사람 뒤로.
    ///    당긴 자리가 그 줄을 쓸 수 없는 곳(줄 시작 전·막힌 구간·이동 불가 구간)이면 원래 자리로 뛰고 착지하는 순간 추락한다(2026-10-02 결정).
    ///    목마 합체가 되는 경우는 목마 기능(006)이 합체로 바꾼다.
    /// 3) 손잡기: 착지 지점이 같은 줄 동료와 앞뒤로 가까우면(세팅 손잡기 거리) 착지 충격·흔들림을 줄인다(<see cref="PlayerBalance.SetLaneLandingReduction"/>).
    /// 판정은 조작하는 소유자 화면에서만 하고(뛰는 순간 한 번), 결과 움직임·추락은 007 공유를 그대로 쓴다.
    /// 씬에 코스(<see cref="TightropeCourse.Current"/>)가 없으면 막지도 보정하지도 않는다. 씬에 하나 둔다(보통 코스 옆).
    /// </summary>
    public sealed class RopeLaneJumpRules : MonoBehaviour
    {
        PlayerMover mover;
        PlayerBalance balance;
        bool blockOntoPlayerBefore;
        bool failOnLanding; // 이번 옆줄 점프는 착지할 자리가 없어 착지 순간 추락

        void OnEnable()
        {
            NetworkPlayer.LocalSpawned += Bind;
            NetworkPlayer.LocalDespawned += Unbind;
            if (NetworkPlayer.Local != null) Bind(NetworkPlayer.Local);
        }

        void OnDisable()
        {
            NetworkPlayer.LocalSpawned -= Bind;
            NetworkPlayer.LocalDespawned -= Unbind;
            Unbind();
        }

        void Bind(NetworkPlayer local)
        {
            Unbind();
            mover = local.GetComponent<PlayerMover>();
            balance = local.GetComponent<PlayerBalance>();
            if (mover == null) return;

            mover.LaneJumpFilter = CanLaneJump;
            mover.LaneLandingShift = GetLandingShift;
            mover.AirborneChanged += HandleAirborne;
            // 동료 자리로 뛰는 것을 막던 임시 동작 대신 뒤쪽 착지로 처리한다
            blockOntoPlayerBefore = mover.BlockLaneJumpOntoPlayer;
            mover.BlockLaneJumpOntoPlayer = false;
        }

        void Unbind()
        {
            if (mover != null)
            {
                if (mover.LaneJumpFilter == CanLaneJump) mover.LaneJumpFilter = null;
                if (mover.LaneLandingShift == GetLandingShift) mover.LaneLandingShift = null;
                mover.AirborneChanged -= HandleAirborne;
                mover.BlockLaneJumpOntoPlayer = blockOntoPlayerBefore;
            }
            mover = null;
            balance = null;
            failOnLanding = false;
        }

        /// <summary>내 캐릭터가 지금 그 방향(-1 왼쪽, +1 오른쪽)으로 옆줄 점프해도 되는지.</summary>
        public bool CanLaneJump(int direction)
        {
            var course = TightropeCourse.Current;
            if (course == null || mover == null) return true;

            Vector3 position = mover.transform.position;
            float from = course.GetDistance(position);
            float to = course.GetDistance(mover.GetLaneLandingPosition(direction));
            if (!course.IsLaneChangeAllowed(from) || !course.IsLaneChangeAllowed(to)) return false;

            // 옆 위치는 지금 그대로, 앞뒤만 착지 예상 거리로 옮겨 그 거리의 옆 줄을 찾는다
            Vector3 along = position + course.Forward * (to - from);
            return course.FindLandingLane(along, direction) != TightropeCourse.NoLane;
        }

        /// <summary>
        /// 내 캐릭터가 지금 그 방향으로 옆줄 점프할 때 착지할 코스 거리. 착지 자리에 다른 플레이어가 있으면 그 뒤쪽 거리.
        /// 그 줄에 설 수 없는 거리면 false(착지 실패).
        /// </summary>
        public bool TryGetLandingDistance(int direction, out float distance) => TryGetLanding(direction, out _, out distance);

        bool TryGetLanding(int direction, out int lane, out float distance)
        {
            var course = TightropeCourse.Current;
            lane = TightropeCourse.NoLane;
            distance = 0f;
            if (course == null || mover == null) return true;

            Vector3 landing = mover.GetLaneLandingPosition(direction);
            distance = course.GetDistance(landing);
            float side = course.GetSide(landing);

            // 자리를 차지한 사람이 없을 때까지 그 사람들 뒤로 당긴다. 한 번에 한 명 이상 뒤로 가므로 인원수만큼이면 끝난다
            int count = PlayerMover.All.Count;
            for (int i = 0; i <= count; i++)
            {
                if (!TryFindBehindBlockers(course, landing.y, side, distance, out float behind)) break;
                distance = behind;
            }

            Vector3 along = mover.transform.position + course.Forward * (distance - course.GetDistance(mover.transform.position));
            lane = course.FindLandingLane(along, direction);
            return lane != TightropeCourse.NoLane && course.IsLaneChangeAllowed(distance);
        }

        /// <summary>
        /// 도착 줄의 그 거리에서 내 몸과 겹치는 다른 플레이어가 있으면, 그 사람들 모두의 바로 뒤(닿기 직전 + 몸 간격) 거리.
        /// 위아래로 떨어진 사람(목마 위층 등)과 통과 상태(사망 래그돌 등)는 보지 않는다.
        /// </summary>
        bool TryFindBehindBlockers(TightropeCourse course, float height, float side, float distance, out float behind)
        {
            behind = distance;
            bool found = false;
            float gap = GameSettings.Base.Character.PlayerGap;

            foreach (var other in PlayerMover.All)
            {
                if (other == mover || other.PassThrough) continue;
                Vector3 position = other.transform.position;
                if (Mathf.Abs(position.y - height) >= Mathf.Max(mover.BodyHeight, other.BodyHeight)) continue;

                float reach = mover.BodyRadius + other.BodyRadius;
                float dSide = course.GetSide(position) - side;
                if (Mathf.Abs(dSide) >= reach) continue; // 다른 줄

                float dAlong = course.GetDistance(position) - distance;
                float contact = Mathf.Sqrt(reach * reach - dSide * dSide);
                if (Mathf.Abs(dAlong) >= contact) continue; // 겹치지 않음

                behind = Mathf.Min(behind, distance + dAlong - contact - gap);
                found = true;
            }
            return found;
        }

        /// <summary>
        /// 그 줄의 그 거리에 착지하면 손잡기인지: 같은 줄(위아래로 떨어지지 않은) 다른 플레이어가 앞뒤로 손잡기 거리 안에 있음.
        /// 통과 상태(사망 래그돌 등)는 보지 않는다.
        /// </summary>
        public bool IsHandholdLanding(int lane, float distance)
        {
            var course = TightropeCourse.Current;
            if (course == null || mover == null || lane == TightropeCourse.NoLane) return false;

            float reach = GameSettings.Tightrope.Handhold.Distance;
            float height = mover.transform.position.y;
            foreach (var other in PlayerMover.All)
            {
                if (other == mover || other.PassThrough) continue;
                Vector3 position = other.transform.position;
                if (Mathf.Abs(position.y - height) >= Mathf.Max(mover.BodyHeight, other.BodyHeight)) continue;
                if (!course.TryGetRopePoint(position, out int otherLane, out float otherDistance) || otherLane != lane) continue;
                if (Mathf.Abs(otherDistance - distance) <= reach) return true;
            }
            return false;
        }

        float GetLandingShift(int direction)
        {
            failOnLanding = false;
            var course = TightropeCourse.Current;
            if (course == null || mover == null) return 0f;

            float original = course.GetDistance(mover.GetLaneLandingPosition(direction));
            if (!TryGetLanding(direction, out int lane, out float distance))
            {
                failOnLanding = true;
                return 0f;
            }

            // 균형은 착지 알림을 먼저 받으므로 감소율은 뛰는 순간 넣어 둔다(착지하면 균형이 0으로 되돌린다)
            if (balance != null && IsHandholdLanding(lane, distance))
                balance.SetLaneLandingReduction(GameSettings.Tightrope.Handhold.Reduction);
            return distance - original;
        }

        void HandleAirborne(bool airborne)
        {
            if (airborne || !failOnLanding) return;
            failOnLanding = false;
            if (balance != null) balance.ForceFall();
        }
    }
}
