using System;
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
    [DefaultExecutionOrder(-40)]
    [RequireComponent(typeof(TightropeCatchSystem))]
    [RequireComponent(typeof(TightropeHandholdRangeView), typeof(TightropeBalancePoseView))]
    public sealed class RopeLaneJumpRules : MonoBehaviour
    {
        PlayerMover mover;
        PlayerBalance balance;
        bool blockOntoPlayerBefore;
        bool failOnLanding; // 이번 옆줄 점프는 착지할 자리가 없어 착지 순간 추락
        NetworkPlayer laneLinkTarget;
        bool handholdLanding;
        NetworkPlayer activeCatcher;
        int landingLane = TightropeCourse.NoLane;
        float landingDistance;
        Func<int, bool> previousFilter;
        Func<int, float> previousShift;

        static bool IsGameplayActive => TightropeCourse.Current != null && TightropeCourse.Current.IsGameplayActive;

        void OnEnable()
        {
            NetworkPlayer.LocalSpawned += Bind;
            NetworkPlayer.LocalDespawned += Unbind;
            NetworkPlayer.Restarted += HandleRestarted;
            if (NetworkPlayer.Local != null) Bind(NetworkPlayer.Local);
        }

        void OnDisable()
        {
            NetworkPlayer.LocalSpawned -= Bind;
            NetworkPlayer.LocalDespawned -= Unbind;
            NetworkPlayer.Restarted -= HandleRestarted;
            Unbind();
        }

        void Bind(NetworkPlayer local)
        {
            Unbind();
            if (local == null || !IsGameplayActive) return;
            mover = local.GetComponent<PlayerMover>();
            balance = local.GetComponent<PlayerBalance>();
            if (mover == null) return;

            previousFilter = mover.LaneJumpFilter;
            previousShift = mover.LaneLandingShift;
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
                bool ownsFilter = mover.LaneJumpFilter == CanLaneJump;
                bool ownsShift = mover.LaneLandingShift == GetLandingShift;
                if (ownsFilter) mover.LaneJumpFilter = previousFilter;
                if (ownsShift) mover.LaneLandingShift = previousShift;
                mover.AirborneChanged -= HandleAirborne;
                if (ownsFilter && ownsShift && !mover.BlockLaneJumpOntoPlayer)
                    mover.BlockLaneJumpOntoPlayer = blockOntoPlayerBefore;
                if (ownsFilter && ownsShift && balance != null) balance.SetLaneLandingReduction(0f);
            }
            mover = null;
            balance = null;
            previousFilter = null;
            previousShift = null;
            ClearLanding();
        }

        void Update()
        {
            if (!IsGameplayActive) { Unbind(); return; }
            var local = NetworkPlayer.Local;
            if (local == null) { Unbind(); return; }
            if (mover == null || mover.gameObject != local.gameObject) Bind(local);
            if (mover != null && mover.IsAirborne && mover.CurrentJump == JumpKind.Lane && !failOnLanding)
            {
                RefreshLandingHelp();
            }
        }

        void ClearLanding()
        {
            failOnLanding = false;
            laneLinkTarget = null;
            handholdLanding = false;
            activeCatcher = null;
            landingLane = TightropeCourse.NoLane;
        }

        void HandleRestarted(NetworkPlayer player)
        {
            if (player != null && player.IsLocal) ClearLanding();
        }

        /// <summary>내 캐릭터가 지금 그 방향(-1 왼쪽, +1 오른쪽)으로 옆줄 점프해도 되는지.</summary>
        public bool CanLaneJump(int direction)
        {
            var course = TightropeCourse.Current;
            if (course == null || !course.IsGameplayActive || mover == null) return true;

            Vector3 position = mover.transform.position;
            float from = course.GetDistance(position);
            float to = course.GetDistance(mover.GetLaneLandingPosition(direction));
            if (!course.IsLaneChangeAllowed(from) || !course.IsLaneChangeAllowed(to)) return false;

            // 옆 위치는 지금 그대로, 앞뒤만 착지 예상 거리로 옮겨 그 거리의 옆 줄을 찾는다
            Vector3 along = position + ForwardFor(course, position) * (to - from);
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
            if (course == null || !course.IsGameplayActive || mover == null) return true;

            Vector3 landing = mover.GetLaneLandingPosition(direction);
            landing.y = LandingHeight();
            distance = course.GetDistance(landing);
            float side = course.GetSide(landing);
            float originalDistance = distance;
            Vector3 forward = ForwardFor(course, mover.transform.position);

            // 합체 가능한 동료와 겹치면 원래 도착점을 유지한다. 실제 합체 요청은 착지 순간 보낸다.
            var target = FindLaneLinkTarget(direction);
            if (target != null)
            {
                Vector3 originalAlong = mover.transform.position + forward * (distance - course.GetDistance(mover.transform.position));
                lane = course.FindLandingLane(originalAlong, direction);
                return lane != TightropeCourse.NoLane && course.IsLaneChangeAllowed(distance);
            }

            // 자리를 차지한 사람이 없을 때까지 그 사람들 뒤로 당긴다. 한 번에 한 명 이상 뒤로 가므로 인원수만큼이면 끝난다
            int count = PlayerMover.All.Count;
            for (int i = 0; i <= count; i++)
            {
                Vector3 point = landing + forward * (distance - originalDistance);
                if (!TryFindBehindBlockers(course, point, side, distance, out float behind)) break;
                distance = behind;
            }

            Vector3 along = mover.transform.position + forward * (distance - course.GetDistance(mover.transform.position));
            lane = course.FindLandingLane(along, direction);
            return lane != TightropeCourse.NoLane && course.IsLaneChangeAllowed(distance);
        }

        static Vector3 ForwardFor(TightropeCourse course, Vector3 position) => course.HasIndependentRopes
            ? course.ForwardAt(position) : course.Forward;

        /// <summary>
        /// 도착 줄의 그 거리에서 내 몸과 겹치는 다른 플레이어가 있으면, 그 사람들 모두의 바로 뒤(닿기 직전 + 몸 간격) 거리.
        /// 위아래로 떨어진 사람(목마 위층 등)과 통과 상태(사망 래그돌 등)는 보지 않는다.
        /// </summary>
        bool TryFindBehindBlockers(TightropeCourse course, Vector3 landing, float side, float distance, out float behind)
        {
            behind = distance;
            bool found = false;
            float gap = GameSettings.Base.Character.PlayerGap;
            Vector3 forward = ForwardFor(course, mover.transform.position);
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            foreach (var other in PlayerMover.All)
            {
                if (other == mover || other.PassThrough) continue;
                Vector3 position = other.transform.position;
                if (Mathf.Abs(position.y - landing.y) >= Mathf.Max(mover.BodyHeight, other.BodyHeight)) continue;

                float reach = mover.BodyRadius + other.BodyRadius;
                // 독립줄 배열 번호는 좌우 순서/공간을 뜻하지 않는다. 실제 도착점과 몸통 간격을 쓴다.
                float dSide = course.HasIndependentRopes ? Vector3.Dot(position - landing, right) : course.GetSide(position) - side;
                if (Mathf.Abs(dSide) >= reach) continue; // 다른 줄

                float dAlong = course.HasIndependentRopes ? Vector3.Dot(position - landing, forward) : course.GetDistance(position) - distance;
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
            if (course == null || !course.IsGameplayActive || mover == null || lane == TightropeCourse.NoLane) return false;

            foreach (var other in PlayerMover.All)
            {
                if (TryGetHandholdRange(other, out int otherLane, out float from, out float to)
                    && otherLane == lane && distance >= from && distance <= to
                    && (other.transform.position - course.GetLanePosition(lane, distance)).sqrMagnitude
                        <= GameSettings.Tightrope.Handhold.Distance * GameSettings.Tightrope.Handhold.Distance) return true;
            }
            return false;
        }

        /// <summary>손잡기 판정과 화면 표시가 함께 쓰는 동료의 앞뒤 범위. 줄의 사용 가능 여부는 코스가 판정한다.</summary>
        public bool TryGetHandholdRange(PlayerMover other, out int lane, out float from, out float to)
        {
            lane = TightropeCourse.NoLane;
            from = to = 0f;
            var course = TightropeCourse.Current;
            if (course == null || !course.IsGameplayActive || mover == null
                || other == null || other == mover || other.PassThrough) return false;
            Vector3 position = other.transform.position;
            if (Mathf.Abs(position.y - LandingHeight()) >= Mathf.Max(mover.BodyHeight, other.BodyHeight)) return false;
            if (!course.TryGetRopePoint(position, out lane, out float distance)) return false;
            float reach = GameSettings.Tightrope.Handhold.Distance;
            float square = reach * reach - (position - course.GetLanePosition(lane, distance)).sqrMagnitude;
            if (!float.IsFinite(reach) || reach <= 0f || square < 0f) return false;
            float along = Mathf.Sqrt(square);
            from = distance - along;
            to = distance + along;
            return true;
        }

        NetworkPlayer FindLaneLinkTarget(int direction)
        {
            var system = PiggybackSystem.Current;
            if (!IsGameplayActive || system == null || mover == null) return null;
            var local = NetworkPlayer.Local;
            bool topRelease = system.GetBelow(local) != null && system.GetAbove(local) == null;
            if (!topRelease)
            {
                var other = mover.FindPlayerAtLaneLanding(direction);
                var target = other != null ? other.GetComponent<NetworkPlayer>() : null;
                if (TightropeCatchSystem.Current != null && TightropeCatchSystem.Current.IsReady(target)) return null;
                return system.CanLaneLink(local, target) ? target : null;
            }

            Vector3 landing = mover.GetLaneLandingPosition(direction);
            landing.y = LandingHeight();
            foreach (var other in PlayerMover.All)
            {
                if (other == mover || other.PassThrough) continue;
                Vector3 offset = other.transform.position - landing;
                if (Mathf.Abs(offset.y) >= Mathf.Max(mover.BodyHeight, other.BodyHeight)) continue;
                offset.y = 0f;
                if (offset.magnitude >= mover.BodyRadius + other.BodyRadius) continue;
                var target = other.GetComponent<NetworkPlayer>();
                if (TightropeCatchSystem.Current != null && TightropeCatchSystem.Current.IsReady(target)) continue;
                if (system.CanLaneLink(local, target, true)) return target;
            }
            return null;
        }

        float LandingHeight()
        {
            var system = PiggybackSystem.Current;
            var below = system != null ? system.GetBelow(NetworkPlayer.Local) : null;
            if (below == null) return mover.transform.position.y;
            for (var next = system.GetBelow(below); next != null; next = system.GetBelow(below)) below = next;
            return below.transform.position.y; // 머리 위 출발도 도착 줄 높이의 동료를 찾는다
        }

        float GetLandingShift(int direction)
        {
            ClearLanding();
            var course = TightropeCourse.Current;
            if (course == null || !course.IsGameplayActive || mover == null) return 0f;

            float original = course.GetDistance(mover.GetLaneLandingPosition(direction));
            if (!TryGetLanding(direction, out int lane, out float distance))
            {
                failOnLanding = true;
                return 0f;
            }

            // 균형은 착지 알림을 먼저 받으므로 감소율은 뛰는 순간 넣어 둔다(착지하면 균형이 0으로 되돌린다)
            laneLinkTarget = FindLaneLinkTarget(direction);
            landingLane = lane;
            landingDistance = distance;
            RefreshLandingHelp();
            return distance - original;
        }

        void RefreshLandingHelp()
        {
            activeCatcher = null;
            var catches = TightropeCatchSystem.Current;
            if (laneLinkTarget == null && catches != null)
                catches.TryGetCatcher(NetworkPlayer.Local, landingLane, landingDistance, out activeCatcher);
            handholdLanding = laneLinkTarget != null || IsHandholdLanding(landingLane, landingDistance);
            if (balance != null) balance.SetLaneLandingReduction(activeCatcher != null ? catches.CatchReduction
                : handholdLanding ? GameSettings.Tightrope.Handhold.Reduction : 0f);
        }

        void HandleAirborne(bool airborne)
        {
            if (!IsGameplayActive) { ClearLanding(); return; }
            if (airborne || mover == null || mover.CurrentJump != JumpKind.Lane) return;
            var local = NetworkPlayer.Local;
            var target = laneLinkTarget;
            var catcher = activeCatcher;
            bool helped = handholdLanding;
            bool failed = failOnLanding;
            ClearLanding();
            if (failed)
            {
                if (balance != null) balance.ForceFall();
                return;
            }
            if (local == null || local.State != PlayerState.Normal || (balance != null && balance.HasFallen)) return;
            var system = PiggybackSystem.Current;
            if (catcher != null) TightropeCatchSystem.Current?.ReportLanding(catcher);
            else if (target != null) system?.RequestLaneLanding(target);
            else if (helped) system?.ShowHandholdLanding();
        }
    }
}
