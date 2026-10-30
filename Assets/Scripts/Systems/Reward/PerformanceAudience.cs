using System.Collections.Generic;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using CurtainCall.Tightrope.Saws;
using UnityEngine;

namespace CurtainCall.Reward
{
    /// <summary>
    /// 공통 공연 관찰. 호스트만 보상 데이터를 갱신하며 UI 수명과 독립적이다.
    /// 기준: 보상 확정 규칙 — 근접 회피 +5, 목마 협동 회피 각 +10, 오브제(트램펄린) 최초 성공 +5, 시간 끌기 15초마다 −10, 추락 −15(외줄은 매달림 시작).
    /// 도착 자체에는 환호를 주지 않고 호응도를 고정한다(완주 보너스는 정산이 지급).
    /// </summary>
    [DefaultExecutionOrder(150)]
    public sealed class PerformanceAudience : MonoBehaviour
    {
        [SerializeField] bool humanCannon;
        [SerializeField] Transform cannonStart, cannonFinish;
        readonly Dictionary<(ulong, string), AudienceEncounter> encounters = new();
        readonly Dictionary<ulong, AudienceStall> stalls = new();
        readonly HashSet<ulong> bounced = new();
        readonly Dictionary<int, int> gateLanes = new();
        readonly Dictionary<ulong, long> reactionsSeen = new();
        PiggybackSystem piggyback;
        PerformanceRewards rewards;
        string identity;
        AudienceReactionSettings Settings => AudienceReactionSettings.Current;
        bool Active => rewards != null && rewards.HasAuthority && rewards.State?.IsOpen == true
            && rewards.State.Performance?.TrickId == (humanCannon ? "HumanCannon" : "Tightrope");

        void OnEnable()
        {
            HumanCannonTrampoline.ServerBounced += Bounce;
            HumanCannonSystem.ServerFell += CannonFell;
            TightropeHangSystem.ServerHangStarted += RopeFell;
            TightropeHangSystem.ServerRescueSucceeded += Cooperation;
            TightropeCatchSystem.ServerCatchSucceeded += Cooperation;
        }
        // 외줄 추락은 매달림 시작에 한 번 −15(뒤 낙사에 중복 차감 없음).
        void RopeFell(ulong client)
        {
            Bind();
            if (!humanCannon && Active && Settings != null) React(client, -Settings.fallJeer);
        }
        // 같은 추락 1회 −15(인간 대포는 바닥·낭떠러지 추락 부상 때).
        void CannonFell(ulong client)
        {
            Bind();
            if (humanCannon && Active && Settings != null) React(client, -Settings.fallJeer);
        }
        void Bind()
        {
            rewards = PerformanceRewards.Instance;
            if (piggyback == PiggybackSystem.Current) return;
            if (piggyback != null) piggyback.HandholdConfirmed -= Handhold;
            piggyback = PiggybackSystem.Current;
            if (piggyback != null) piggyback.HandholdConfirmed += Handhold;
        }
        void ResetAttempt()
        {
            encounters.Clear(); stalls.Clear(); bounced.Clear(); gateLanes.Clear(); reactionsSeen.Clear();
        }
        void Update() => Bind();
        void LateUpdate()
        {
            if (!Active || Settings == null) { identity = null; ResetAttempt(); return; }
            if (identity != rewards.State.Performance.Id) { identity = rewards.State.Performance.Id; ResetAttempt(); }
            foreach (var person in rewards.State.Segments)
            {
                reactionsSeen.TryGetValue(person.ClientId, out long previous);
                if (person.ReactionSequence > previous && person.ReactionDelta > 0
                    && stalls.TryGetValue(person.ClientId, out var stall)) stall.Reset();
                reactionsSeen[person.ClientId] = person.ReactionSequence;
            }
            foreach (var player in NetworkPlayer.All)
            {
                if (!Eligible(player)) continue;
                if (humanCannon) ObserveCannon(player);
                else ObserveRope(player);
            }
        }
        bool Eligible(NetworkPlayer player)
        {
            if (player == null || !player.IsSpawned || player.State != PlayerState.Normal) return false;
            foreach (var segment in rewards.State.Segments)
                if (segment.ClientId == player.OwnerClientId) return segment.Connected && !segment.Dead;
            return false;
        }
        string Participant(ulong client)
        {
            foreach (var segment in rewards.State.Segments)
                if (segment.ClientId == client) return segment.ParticipantId;
            return null;
        }
        void React(ulong client, int delta, bool resetInterest = false)
        {
            if (!Active || (delta == 0 && !resetInterest)) return;
            string id = Participant(client);
            if (id != null)
            {
                if (delta > 0 && stalls.TryGetValue(client, out var stall)) stall.Reset();
                rewards.AddAudienceReaction(id, delta, resetInterest);
            }
        }
        void Handhold(NetworkPlayer first, NetworkPlayer second)
        { if (first != null && second != null) Cooperation(first.OwnerClientId, second.OwnerClientId); }
        void Cooperation(ulong first, ulong second)
        {
            Bind();
            if (humanCannon || !Active || Settings == null || first == second) return;
            React(first, Settings.cooperation); React(second, Settings.cooperation);
        }
        void Bounce(ulong client)
        {
            Bind();
            // 같은 오브제의 의도 행동 성공은 공연당 최초 1회. 트램펄린은 한 사람당 처음 튀어 오른 한 번만 센다.
            if (!humanCannon || !Active || Settings == null || !bounced.Add(client)) return;
            var trampoline = HumanCannonTrampoline.Current;
            if (trampoline != null && trampoline.Carrier != CannonSeatState.Nobody && trampoline.Carrier != client)
            { React(client, Settings.cooperation); React(trampoline.Carrier, Settings.cooperation); }
            else React(client, Settings.objectFirst);
        }
        /// <summary>진행이 없을 때 시간 끌기 야유. 야유도 관심 감소 유예를 다시 센다.</summary>
        void Stall(NetworkPlayer player, bool active, float progress)
        {
            if (!stalls.TryGetValue(player.OwnerClientId, out var stall)) stalls[player.OwnerClientId] = stall = new AudienceStall();
            if (stall.Sample(active, progress, Time.timeAsDouble, Settings.stallSeconds, Settings.stallProgress))
                React(player.OwnerClientId, -Settings.stallJeer, true);
        }
        void Sample(NetworkPlayer player, string key, float distance, bool touches, bool near, bool vertical = false, bool changedLane = false)
        {
            var id = (player.OwnerClientId, key);
            if (!encounters.TryGetValue(id, out var encounter)) encounters[id] = encounter = new AudienceEncounter();
            bool stacked = vertical && PiggybackSystem.Current != null && PiggybackSystem.Current.IsInStack(player);
            if (!encounter.Sample(distance, Settings.approachDistance, Settings.clearanceDistance,
                (int)player.LostParts, touches, near, stacked, changedLane)) return;
            // 줄을 바꿔 피한 것은 환호도 야유도 없다. 다치지 않고 잘한 것 자체에 야유하지 않는다.
            int delta = vertical && encounter.ChangedLane ? 0
                : vertical && encounter.WasStacked ? Settings.cooperation
                : encounter.WasClose ? Settings.closeDodge : 0;
            React(player.OwnerClientId, delta);
        }
        void SampleProximity(NetworkPlayer player, string key, float distance, bool touches, bool near)
        {
            var id = (player.OwnerClientId, key);
            if (!encounters.TryGetValue(id, out var encounter)) encounters[id] = encounter = new AudienceEncounter();
            if (encounter.SampleProximity(distance, Settings.approachDistance, (int)player.LostParts, touches, near))
                React(player.OwnerClientId, Settings.closeDodge);
        }
        void ObserveRope(NetworkPlayer player)
        {
            var course = TightropeCourse.Current;
            var saws = TightropeSaws.Current;
            if (course == null || !player.TryGetComponent(out PlayerMover mover)) return;
            float distance = course.GetDistance(player.transform.position);
            Stall(player, RopeStallActive(player), distance);
            if (saws == null) return;
            var body = new BodyCapsule(player.transform.position, mover.BodyHeight, mover.BodyRadius);
            var nearBody = new BodyCapsule(player.transform.position, mover.BodyHeight, mover.BodyRadius + Settings.closeDodgeMargin);
            var ground = player.transform.position; ground.y = course.transform.position.y;
            var projectedBody = new BodyCapsule(ground, mover.BodyHeight, mover.BodyRadius + Settings.closeDodgeMargin);
            var s = saws.Settings;
            for (int i = 0; i < saws.HorizontalCount; i++)
            {
                if (!saws.TryGetHorizontalPosition(i, out var center)) continue;
                var disc = new SawDisc(center, Vector3.up, s.horizontalColliderDiameter * .5f, s.horizontalThickness * .5f);
                // 횡으로 지나가는 톱은 W/S 진행 거리와 무관하다. 점프 중에도 발밑 위험 영역의 통과를 추적한다.
                SampleProximity(player, "horizontal:" + i, Vector3.ProjectOnPlane(player.transform.position - center, Vector3.up).magnitude,
                    SawMath.TryGetContact(disc, body, out _), SawMath.TryGetContact(disc, nearBody, out _)
                        || player.IsAirborne && SawMath.TryGetContact(disc, projectedBody, out _));
            }
            int lane = NearestLane(course, player.transform.position, distance);
            if (saws.IsCooperativeGateActive) gateLanes[saws.CooperativeGateIndex] = saws.CooperativeGateLane;
            foreach (var gate in gateLanes)
            {
                string key = "gate:" + gate.Key;
                if (lane != gate.Value && !encounters.ContainsKey((player.OwnerClientId, key))) continue;
                bool active = saws.IsCooperativeGateActive && saws.CooperativeGateIndex == gate.Key && !saws.IsCooperativeGatePassable;
                Vector3 bottom = active ? saws.CurrentCooperativeGateBase : course.GetLanePosition(gate.Value, s.cooperativeGateDistances[gate.Key]);
                Sample(player, key, distance - s.cooperativeGateDistances[gate.Key],
                    active && CooperativeGateGeometry.TryGetContact(body, bottom, course.Right, s.cooperativeGateHeight, s.cooperativeGateThickness, out _),
                    active && CooperativeGateGeometry.TryGetContact(nearBody, bottom, course.Right, s.cooperativeGateHeight, s.cooperativeGateThickness, out _),
                    true, lane != gate.Value);
            }
            if (saws.TryGetVerticalPosition(out var vertical))
            {
                var disc = new SawDisc(vertical, course.Right, s.verticalColliderDiameter * .5f, s.verticalThickness * .5f);
                string key = "vertical:" + saws.VerticalSerial;
                // 처음부터 다른 줄에 있던 단원은 이 톱을 회피한 것으로 간주하지 않는다.
                if (lane != saws.VerticalLane && !encounters.ContainsKey((player.OwnerClientId, key))) return;
                Sample(player, key, distance - saws.VerticalDistance,
                    SawMath.TryGetContact(disc, body, out _), SawMath.TryGetContact(disc, nearBody, out _), true, lane != saws.VerticalLane);
            }
        }
        /// <summary>동료 받기 대기·목마·봉/그네 이동처럼 스스로 진행하지 않는 상태는 시간 끌기에서 뺀다.</summary>
        static bool RopeStallActive(NetworkPlayer player)
        {
            if (player.IsAirborne) return false;
            var hangs = TightropeHangSystem.Current;
            if (hangs != null && (hangs.IsHanging(player) || hangs.IsRescuing(player))) return false;
            if (PiggybackSystem.Current != null && PiggybackSystem.Current.IsInStack(player)) return false;
            if (TightropeCatchSystem.Current != null && TightropeCatchSystem.Current.IsReady(player)) return false;
            if (TightropeRelayGate.Current != null && TightropeRelayGate.Current.IsHeld(player)) return false;
            if (TightropeSwingPole.Current != null && TightropeSwingPole.Current.IsCrossing(player)) return false;
            if (TightropeTrapeze.Current != null && TightropeTrapeze.Current.IsCrossing(player)) return false;
            return true;
        }
        static int NearestLane(TightropeCourse course, Vector3 position, float distance)
        {
            float nearest = float.PositiveInfinity;
            int lane = -1;
            for (int i = 0; i < course.LaneCount; i++)
            {
                float side = Mathf.Abs(Vector3.Dot(position - course.GetLanePosition(i, distance), course.Right));
                if (side < nearest) { nearest = side; lane = i; }
            }
            return lane;
        }
        void ObserveCannon(NetworkPlayer player)
        {
            var destination = HumanCannonDestination.Current;
            if (destination != null && destination.HasArrived(player))
            {
                string id = Participant(player.OwnerClientId);
                if (id != null) rewards.MarkArrived(id);
                return;
            }
            if (cannonStart == null || cannonFinish == null) return;
            Vector3 axis = Vector3.ProjectOnPlane(cannonFinish.position - cannonStart.position, Vector3.up).normalized;
            var cannon = HumanCannonSystem.Current;
            var poles = HumanCannonPoleSystem.Current;
            bool moving = player.IsAirborne || (cannon != null && (cannon.IsSeated(player.OwnerClientId) || cannon.IsFlying(player.OwnerClientId)))
                || (poles != null && poles.HeldBy(player.OwnerClientId) >= 0);
            Stall(player, !moving, Vector3.Dot(player.transform.position - cannonStart.position, axis));
            var hazards = HumanCannonHazardCourse.Current;
            if (hazards == null || !hazards.IsSynchronized || !player.TryGetComponent(out PlayerMover mover)) return;
            for (int i = 0; i < hazards.HazardCount; i++)
            {
                var hazard = hazards.Hazard(i);
                if (hazard == null || !hazard.isActiveAndEnabled) continue;
                string key = "cannon:" + i;
                Vector3 relative = player.transform.position - hazard.transform.position;
                if (Vector3.ProjectOnPlane(relative, axis).magnitude > Settings.approachDistance
                    && !encounters.ContainsKey((player.OwnerClientId, key))) continue;
                float speed = CannonDifficulty.HazardSpeed(hazards.Difficulty);
                Sample(player, key, Vector3.Dot(relative, axis),
                    hazard.Touches(player.transform.position, mover.BodyHeight, mover.BodyRadius, hazards.Elapsed, speed),
                    hazard.Touches(player.transform.position, mover.BodyHeight, mover.BodyRadius + Settings.closeDodgeMargin, hazards.Elapsed, speed));
            }
        }
        void OnDisable()
        {
            HumanCannonTrampoline.ServerBounced -= Bounce;
            HumanCannonSystem.ServerFell -= CannonFell;
            TightropeHangSystem.ServerHangStarted -= RopeFell;
            TightropeHangSystem.ServerRescueSucceeded -= Cooperation;
            TightropeCatchSystem.ServerCatchSucceeded -= Cooperation;
            if (piggyback != null) piggyback.HandholdConfirmed -= Handhold;
            piggyback = null;
            rewards = null; identity = null; ResetAttempt();
        }
    }
}
