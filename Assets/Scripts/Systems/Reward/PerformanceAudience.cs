using System.Collections.Generic;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using CurtainCall.Tightrope.Fire;
using CurtainCall.Tightrope.Saws;
using UnityEngine;

namespace CurtainCall.Reward
{
    /// <summary>HTML 4-1 공통 공연 관찰. 호스트만 기존 보상 데이터를 갱신하며 UI 수명과 독립적이다.</summary>
    [DefaultExecutionOrder(150)]
    public sealed class PerformanceAudience : MonoBehaviour
    {
        [SerializeField] bool humanCannon;
        [SerializeField] Transform cannonStart, cannonFinish;
        [SerializeField] HumanCannonFireWall[] cannonFires;
        [SerializeField] float lowerFloorMaxY = 5, lowerFloorStartZ = 18;
        readonly Dictionary<(ulong, string), AudienceEncounter> encounters = new();
        readonly Dictionary<ulong, AudienceDuration> hanging = new();
        readonly Dictionary<ulong, int> heldPoles = new();
        readonly Dictionary<int, int> gateLanes = new();
        readonly HashSet<string> arrivals = new();
        readonly List<NetworkPlayer> lower = new();
        readonly AudienceDuration lowerTimer = new();
        PerformanceRewards rewards;
        string identity;
        AudienceReactionSettings Settings => AudienceReactionSettings.Current;
        bool Active => rewards != null && rewards.HasAuthority && rewards.State?.IsOpen == true
            && rewards.State.Performance?.TrickId == (humanCannon ? "HumanCannon" : "Tightrope");

        void OnEnable() => HumanCannonTrampoline.ServerBounced += Bounce;
        void Bind()
        {
            if (rewards == PerformanceRewards.Instance) return;
            if (rewards != null) rewards.BeforeSettlement -= Arrive;
            rewards = PerformanceRewards.Instance;
            if (rewards != null) rewards.BeforeSettlement += Arrive;
        }
        void ResetAttempt()
        {
            encounters.Clear(); hanging.Clear(); heldPoles.Clear(); arrivals.Clear(); gateLanes.Clear();
            lowerTimer.Tick(false, 0, 1, true);
        }
        void Update() => Bind();
        void LateUpdate()
        {
            if (!Active || Settings == null) { identity = null; ResetAttempt(); return; }
            if (identity != rewards.State.Performance.Id) { identity = rewards.State.Performance.Id; ResetAttempt(); }
            lower.Clear();
            foreach (var player in NetworkPlayer.All)
            {
                if (!Eligible(player)) continue;
                if (humanCannon) ObserveCannon(player);
                else ObserveRope(player);
            }
            if (humanCannon && lowerTimer.Tick(lower.Count >= 2, Time.timeAsDouble, Settings.lowerFloorSeconds, true))
                foreach (var player in lower) React(player.OwnerClientId, -Settings.lowerFloorJeer);
        }
        bool Eligible(NetworkPlayer player)
        {
            if (player == null || !player.IsSpawned || player.State != PlayerState.Normal) return false;
            foreach (var segment in rewards.State.Segments)
                if (segment.ClientId == player.OwnerClientId) return segment.Connected && !segment.Dead;
            return false;
        }
        void React(ulong client, int delta)
        {
            if (!Active) return;
            foreach (var segment in rewards.State.Segments)
                if (segment.ClientId == client) { rewards.AddAudienceReaction(segment.ParticipantId, delta); return; }
        }
        void Bounce(ulong client)
        {
            Bind();
            if (humanCannon && Active && Settings != null) React(client, Settings.trampoline);
        }
        void Sample(NetworkPlayer player, string key, float distance, bool touches, bool near, bool vertical = false, bool changedLane = false)
        {
            var id = (player.OwnerClientId, key);
            if (!encounters.TryGetValue(id, out var encounter)) encounters[id] = encounter = new AudienceEncounter();
            bool stacked = vertical && PiggybackSystem.Current != null && PiggybackSystem.Current.IsInStack(player);
            if (!encounter.Sample(distance, Settings.approachDistance, Settings.clearanceDistance,
                (int)player.LostParts, touches, near, stacked, changedLane)) return;
            int delta = vertical && encounter.WasStacked && !encounter.ChangedLane ? Settings.stackedVertical
                : vertical && encounter.ChangedLane ? -Settings.laneChangeJeer
                : encounter.WasClose ? Settings.closeDodge : Settings.safeDodge;
            React(player.OwnerClientId, delta);
        }
        void ObserveRope(NetworkPlayer player)
        {
            var course = TightropeCourse.Current;
            var saws = TightropeSaws.Current;
            if (course == null || saws == null || !player.TryGetComponent(out PlayerMover mover)) return;
            var body = new BodyCapsule(player.transform.position, mover.BodyHeight, mover.BodyRadius);
            var nearBody = new BodyCapsule(player.transform.position, mover.BodyHeight, mover.BodyRadius + Settings.closeDodgeMargin);
            float distance = course.GetDistance(player.transform.position);
            var s = saws.Settings;
            for (int i = 0; i < saws.HorizontalCount; i++)
            {
                if (!saws.TryGetHorizontalPosition(i, out var center)) continue;
                var disc = new SawDisc(center, Vector3.up, s.horizontalColliderDiameter * .5f, s.horizontalThickness * .5f);
                Sample(player, "horizontal:" + i, distance - s.GetHorizontalDistance(i),
                    SawMath.TryGetContact(disc, body, out _), SawMath.TryGetContact(disc, nearBody, out _));
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
            var poles = HumanCannonPoleSystem.Current;
            int pole = poles != null ? poles.HeldBy(player.OwnerClientId) : -1;
            if (!hanging.TryGetValue(player.OwnerClientId, out var timer)) hanging[player.OwnerClientId] = timer = new AudienceDuration();
            if (!heldPoles.TryGetValue(player.OwnerClientId, out int previous) || previous != pole) timer.Tick(false, 0, 1, false);
            heldPoles[player.OwnerClientId] = pole;
            if (timer.Tick(pole >= 0, Time.timeAsDouble, Settings.hangingSeconds, false)) React(player.OwnerClientId, -Settings.hangingJeer);
            var cannon = HumanCannonSystem.Current;
            if (!player.IsAirborne && pole < 0 && player.transform.position.y < lowerFloorMaxY
                && player.transform.position.z >= lowerFloorStartZ && cannon != null
                && !cannon.IsSeated(player.OwnerClientId) && !cannon.IsFlying(player.OwnerClientId)) lower.Add(player);
            var hazards = HumanCannonHazardCourse.Current;
            if (hazards == null || !hazards.IsSynchronized || cannonStart == null || cannonFinish == null
                || !player.TryGetComponent(out PlayerMover mover)) return;
            Vector3 axis = Vector3.ProjectOnPlane(cannonFinish.position - cannonStart.position, Vector3.up).normalized;
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
        void Arrive(IReadOnlyDictionary<string, string> judgements)
        {
            if (!Active || Settings == null || judgements == null) return;
            foreach (var pair in judgements)
            {
                if (pair.Value != "Arrived" || !arrivals.Add(pair.Key)) continue;
                NetworkPlayer player = null;
                foreach (var segment in rewards.State.Segments)
                    if (segment.ParticipantId == pair.Key)
                        foreach (var candidate in NetworkPlayer.All)
                            if (candidate != null && candidate.OwnerClientId == segment.ClientId) player = candidate;
                if (player == null) continue;
                bool close = false;
                if (!humanCannon)
                {
                    var fire = TightropeFire.Current;
                    var course = TightropeCourse.Current;
                    if (fire != null && fire.IsChasing && course != null)
                    {
                        float gap = course.GetDistance(player.transform.position) - fire.ChaseDistance;
                        close = gap >= 0 && gap <= Settings.closeFireDistance;
                    }
                }
                else if (cannonFires != null && HumanCannonRouteSystem.Current != null && cannonStart != null && cannonFinish != null)
                {
                    var axis = Vector3.ProjectOnPlane(cannonFinish.position - cannonStart.position, Vector3.up).normalized;
                    double now = HumanCannonRouteSystem.Current.Clock;
                    foreach (var fire in cannonFires)
                        if (fire != null && fire.isActiveAndEnabled && fire.Available && fire.Armed && !fire.Finished && fire.Advancing(now))
                        {
                            float gap = Vector3.Dot(player.transform.position - fire.Front(now), axis);
                            close |= gap >= 0 && gap <= Settings.closeFireDistance;
                        }
                }
                rewards.AddAudienceReaction(pair.Key, close ? Settings.closeArrival : Settings.safeArrival);
            }
        }
        void OnDisable()
        {
            HumanCannonTrampoline.ServerBounced -= Bounce;
            if (rewards != null) rewards.BeforeSettlement -= Arrive;
            rewards = null; identity = null; ResetAttempt();
        }
    }
}
