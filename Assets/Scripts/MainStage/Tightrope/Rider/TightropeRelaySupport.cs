using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    /// <summary>기존 F 준비를 양쪽 버팀 발판에 연결한다. 비행 물리는 바꾸지 않으며 준비·성공 공유는 CatchSystem을 쓴다.</summary>
    public sealed class TightropeRelaySupport : MonoBehaviour
    {
        public static TightropeRelaySupport Current { get; private set; }
        static TightropeSettings.HandholdSettings Rules => GameSettings.Tightrope.Handhold;
        TightropeCourse Course => TightropeCourse.Current;
        bool Active => isActiveAndEnabled && Rules.RelayBraceEnabled && Course != null && !Course.HasRelayGap && Course.IsGameplayActive;
        public float EntryDistance => Course.SwingGapFrom - Rules.RelaySourceOffset;
        public float ExitDistance => Course.SwingGapTo + SwingPoleMath.ExitOffset;
        void OnEnable() => Current = this;
        void OnDisable() { if (Current == this) Current = null; }

        public bool TryGetPad(NetworkPlayer player, out int lane, out bool far)
        {
            lane = -1; far = false;
            if (!Active || player == null || !player.IsSpawned || player.State != PlayerState.Normal
                || player.IsAirborne || !player.TryGetComponent(out PlayerMover mover) || mover.PassThrough
                || (player.IsLocal && mover.IsAirborne) || !Course.TryGetSupportingRope(mover, out lane)
                || !Course.HasSwingGapOnLane(lane) || !Course.IsLaneUsable(lane, Course.GetDistance(player.transform.position))
                || (TightropeSwingPole.Current != null && TightropeSwingPole.Current.IsCrossing(player))
                || (PiggybackSystem.Current != null && PiggybackSystem.Current.IsInStack(player))) return false;
            if (player.TryGetComponent(out PlayerBalance balance) && balance.HasFallen) return false;
            float distance = Course.GetDistance(player.transform.position);
            if (Mathf.Abs(distance - EntryDistance) <= Rules.RelayPadRadius) return true;
            far = true;
            return Mathf.Abs(distance - ExitDistance) <= Rules.RelayPadRadius;
        }

        public NetworkPlayer PreparedHelper(int lane, bool far)
        {
            var catches = TightropeCatchSystem.Current;
            if (!Active || catches == null) return null;
            foreach (var other in NetworkPlayer.All)
                if (catches.IsReady(other) && TryGetPad(other, out int supportLane, out bool supportFar)
                    && supportLane == lane && supportFar == far) return other;
            return null;
        }

        public bool CanSupportLanding(NetworkPlayer jumper, NetworkPlayer helper, int lane, float distance)
        {
            return Active && jumper != null && helper != null && jumper != helper
                && jumper.State == PlayerState.Normal && TightropeCatchSystem.Current != null
                && TightropeCatchSystem.Current.IsReady(helper)
                && TryGetPad(helper, out int helperLane, out bool far) && !far && helperLane == lane
                && distance >= Course.SwingGapTo && distance <= ExitDistance + Rules.ActiveDistance
                && Course.IsLaneUsable(lane, distance);
        }

        public bool TryGetLandingSupport(NetworkPlayer jumper, int lane, float distance, out NetworkPlayer helper)
        {
            helper = PreparedHelper(lane, false);
            if (CanSupportLanding(jumper, helper, lane, distance)) return true;
            helper = null; return false;
        }

        bool HasPartner(NetworkPlayer helper, int lane, bool far)
        {
            foreach (var other in NetworkPlayer.All)
            {
                if (other == null || other == helper || !other.IsSpawned || other.State != PlayerState.Normal
                    || !Course.TryGetRopePoint(other.transform.position, out int otherLane, out float distance) || otherLane != lane) continue;
                if (far ? distance >= EntryDistance - 6f && distance < Course.SwingGapFrom
                    : distance > EntryDistance + 1f && distance <= ExitDistance + Rules.ActiveDistance) return true;
            }
            return false;
        }

        /// <summary>호스트가 승인한 F 준비 한 번에만 호출한다. 기존 6초 통로가 열려 있으면 중복 연장하지 않는다.</summary>
        public void OnPrepared(NetworkPlayer helper)
        {
            var session = NetworkSessionManager.Instance;
            var saws = Saws.TightropeSaws.Current;
            if (session == null || !session.IsHost || saws == null || saws.IsTeamPassageOpen
                || TightropeCatchSystem.Current == null || !TightropeCatchSystem.Current.IsReady(helper)
                || !TryGetPad(helper, out int lane, out bool far) || !HasPartner(helper, lane, far)) return;
            saws.ServerOpenRelayPassage(lane, EntryDistance, ExitDistance);
        }

        public string LocalHint
        {
            get
            {
                var local = NetworkPlayer.Local;
                if (!Active || local == null || local.State != PlayerState.Normal) return null;
                var catches = TightropeCatchSystem.Current;
                if (TryGetPad(local, out int lane, out bool far))
                    return catches != null && catches.IsReady(local)
                        ? $"{(far ? "맞은편 받기" : "뒤에서 줄 버팀")} {catches.ReadyRemaining(local):0.0}초 · A/D 균형 유지 · 이동하면 해제"
                        : far ? "맞은편 초록 발판 · F로 받을 준비 → 남은 동료의 출발 구간도 보호"
                            : "주황 버팀 발판 · 동료가 앞으로 간 뒤 F → 주변 톱 정지 · 동료 착지 50% 완충";
                if (!Course.TryGetRopePoint(local.transform.position, out lane, out float distance)
                    || !Course.HasSwingGapOnLane(lane) || distance < EntryDistance || distance > ExitDistance + 2f) return null;
                var helper = PreparedHelper(lane, false);
                return helper != null && helper != local
                    ? $"뒤의 동료가 버티고 있어요! {catches.ReadyRemaining(helper):0.0}초 안에 착지 · 맞은편에서 F로 교대"
                    : "봉 협동: 뒤에서 F 버팀 → 한 명 먼저 건너기 → 맞은편 F 받기로 교대";
            }
        }
    }
}
