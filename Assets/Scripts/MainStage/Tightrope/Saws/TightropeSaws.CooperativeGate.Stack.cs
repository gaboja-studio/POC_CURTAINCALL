using System;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.Tightrope.Fire;
using UnityEngine;

namespace CurtainCall.Tightrope.Saws
{
    public sealed partial class TightropeSaws
    {
        const int MinimumFinaleStackSize = 2, MaximumFinaleStackSize = 4;
        readonly ulong[] cooperativeAdmittedPlayers = new ulong[MaximumFinaleStackSize];
        int cooperativeAdmittedCount;
        uint cooperativeGateRevision, receivedCooperativeGateRevision;

        public bool IsFourStackFinaleGate => IsCooperativeGateActive && IsRequiredFinaleIndex(cooperativeGateIndex);
        public bool IsFinaleFourStackGate => IsFourStackFinaleGate;
        // Legacy FourStack names/serialized setting identify the finale, not a four-player requirement.
        public int CooperativeRequiredStackSize => MinimumFinaleStackSize;
        public bool IsCooperativeStackApproved => !IsFourStackFinaleGate || ValidFinaleStackSize(cooperativeAdmittedCount);
        static bool ValidFinaleStackSize(int count) => count >= MinimumFinaleStackSize && count <= MaximumFinaleStackSize;
        public float CooperativeUpperLandingDistance => CooperativeGateStopDistance + settings.cooperativeGatePlateOffset
            + settings.cooperativeGatePlateLength * .5f + GameSettings.Base.Character.CapsuleRadius
            + settings.cooperativeGateUpperLandingOffset;

        /// <summary>호스트가 확인한 최초 목마의 층. 위층이 먼저 떠나거나 사망해도 이 관문을 해결할 때까지 유지한다.</summary>
        public int CooperativeAdmittedFloor(NetworkPlayer player)
        {
            if (player == null) return 0;
            for (int i = 0; i < cooperativeAdmittedCount; i++)
                if (cooperativeAdmittedPlayers[i] == player.NetworkObjectId) return i + 1;
            return 0;
        }

        public float CooperativeLandingDistance(NetworkPlayer player) => IsFourStackFinaleGate
            && CooperativeAdmittedFloor(player) >= 3 ? CooperativeUpperLandingDistance : CooperativePlateDistance;

        public bool CanLaunchCooperativeGate(NetworkPlayer player) => TryGetCooperativeLaunch(player, out _, out _);

        /// <summary>필수 후반 관문을 발판으로 해제하고 실제 톱 도약까지 마쳤는지.</summary>
        public bool AreRequiredGatesCleared
        {
            get
            {
                if (!settings.cooperativeGates || !settings.finaleFourStackRequired) return true;
                if (course == null) return false;
                for (int i = 0; i < (settings.cooperativeGateDistances?.Length ?? 0); i++)
                {
                    if (!IsRequiredFinaleIndex(i)) continue;
                    bool cleared = false;
                    foreach (var gate in releasedGates)
                        if (gate.Index == i && CooperativeGateClock - gate.At >= Mathf.Max(.5f, settings.cooperativeGateHopDuration))
                        { cleared = true; break; }
                    if (!cleared) return false;
                }
                return true;
            }
        }

        bool IsRequiredFinaleIndex(int index) => settings.finaleFourStackRequired && course != null
            && index >= 0 && index < (settings.cooperativeGateDistances?.Length ?? 0)
            && settings.cooperativeGateDistances[index] >= course.FinishDistance * settings.finaleStartFraction;

        void ResetCooperativeAdmission()
        {
            cooperativeAdmittedCount = 0;
            Array.Clear(cooperativeAdmittedPlayers, 0, cooperativeAdmittedPlayers.Length);
        }

        bool ServerApproveCooperativeStack()
        {
            if (!IsFourStackFinaleGate || !IsCooperativeGateStopped) return false;
            var fire = TightropeFire.Current;
            var stack = PiggybackSystem.Current;
            if (fire == null || !fire.FinaleReadyAt(CooperativeGateStopDistance) || stack == null) return false;
            foreach (var bottom in NetworkPlayer.All)
            {
                int size = stack.GetStackSize(bottom);
                if (!IsStandingStackMember(bottom) || stack.GetBelow(bottom) != null || !ValidFinaleStackSize(size)
                    || size <= cooperativeAdmittedCount
                    || !bottom.TryGetComponent(out PlayerMover mover) || !course.TryGetSupportingRope(mover, out int lane)
                    || !TryGetCooperativeGateApproachPosition(lane, out var approach)) continue;
                Vector3 offset = bottom.transform.position - approach;
                if (Mathf.Abs(Vector3.Dot(offset, course.Forward)) > settings.cooperativeGateLaunchTolerance
                    || Mathf.Abs(Vector3.Dot(offset, course.Right)) > settings.plateWidth * .5f) continue;
                int count = 0;
                // A third/fourth player may join before launch. Once any admitted member
                // departs, never replace that roster with another pair or shrink it.
                var candidates = new ulong[MaximumFinaleStackSize];
                for (var member = bottom; member != null && count < size; member = stack.GetAbove(member))
                {
                    if (!IsStandingStackMember(member)) break;
                    if (count < cooperativeAdmittedCount && cooperativeAdmittedPlayers[count] != member.NetworkObjectId) break;
                    candidates[count++] = member.NetworkObjectId;
                }
                if (count != size) continue;
                Array.Copy(candidates, cooperativeAdmittedPlayers, count);
                cooperativeAdmittedCount = count;
                return true;
            }
            return false;
        }

        bool IsStandingStackMember(NetworkPlayer player) => player != null && player.IsSpawned
            && player.isActiveAndEnabled && player.State == PlayerState.Normal && (run == null || run.IsParticipantReady(player)) && !player.IsAirborne
            && player.TryGetComponent(out PlayerMover mover)
            // 원격 이동 부품은 계산하지 않는다. 공중 여부는 공유 상태를 쓰고, 소유자만 로컬 조작 상태도 확인한다.
            && (!player.IsOwner || (!mover.IsAirborne && !mover.FreeMovement
                && (!player.TryGetComponent(out PlayerBalance balance) || !balance.HasFallen)));

        /// <summary>도약할 때 남아 있는 아래층도 승인 명부와 일치해야 한다. 원래 4→3→2 순서로 줄어드는 것은 정상이다.</summary>
        bool IsApprovedCooperativeLaunch(NetworkPlayer player, PiggybackSystem stack)
        {
            if (!IsFourStackFinaleGate) return true;
            int floor = CooperativeAdmittedFloor(player);
            if (!IsCooperativeStackApproved || floor < 2) return false;
            var member = player;
            for (int index = floor - 1; index >= 0; index--)
            {
                if (!IsStandingStackMember(member) || member.NetworkObjectId != cooperativeAdmittedPlayers[index]) return false;
                member = stack.GetBelow(member);
            }
            return member == null;
        }
    }
}
