using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    /// <summary>실제로 연결된 줄의 하중과 연속적인 진동을 소유자의 균형에 반영한다.</summary>
    [DefaultExecutionOrder(-10)]
    public sealed class TightropeRopeSway : MonoBehaviour
    {
        TightropeCourse course;
        PlayerBalance target;
        PlayerMover previousMover;
        float load, motion, envelope;
        float previousDistance;
        int previousLane = TightropeCourse.NoLane;

        void Awake() => course = GetComponent<TightropeCourse>();

        void Update()
        {
            var local = NetworkPlayer.Local;
            var balance = local != null ? local.GetComponent<PlayerBalance>() : null;
            if (target != balance) { Clear(); target = balance; }
            if (course == null || !course.IsGameplayActive || target == null || !target.enabled
                || !target.IsActive || target.IsAirborne || target.HasFallen
                || !TrySupport(local, out var mover, out int lane, out float distance))
            { Clear(); return; }

            if (lane != previousLane)
            { load = motion = envelope = 0f; previousMover = null; previousLane = lane; }
            var rules = GameSettings.Tightrope.RopeMovement;
            var spans = course.GetUsableSpans(lane);
            var stack = PiggybackSystem.Current;
            float crowd = 0f;
            foreach (var other in NetworkPlayer.All)
            {
                // 목마는 바닥 사람만 하중을 전달하며 같은 목마의 무게는 기존 배율이 처리한다.
                if (other == local || (stack != null && (stack.GetBelow(other) != null || stack.IsSameStack(local, other)))
                    || !TrySupport(other, out _, out int otherLane, out float otherDistance) || lane != otherLane
                    || !LandingWaveMath.SharesSupport(spans, distance, otherDistance)) continue;
                float weight = 1f - Mathf.Clamp01(Mathf.Abs(otherDistance - distance) / rules.SharedSwayRange);
                crowd += weight * (stack != null ? stack.GetStackSize(other) : 1);
            }

            float blend = 1f - Mathf.Exp(-5f * Time.deltaTime);
            load = Mathf.Lerp(load, Mathf.Min(.75f, crowd * rules.SharedSwayPerPlayer), blend);
            // 목마 위층에서는 아래 동료의 원격 mover가 IsMoving을 계산하지 않는다.
            float travel = previousMover == mover ? Mathf.Abs(distance - previousDistance) : 0f;
            bool walking = mover.Simulated ? mover.IsMoving
                : Time.deltaTime > 0f && travel < 2f && travel / Time.deltaTime > .15f;
            previousMover = mover;
            previousDistance = distance;
            motion = Mathf.Lerp(motion, walking ? 1f : 0f, blend);
            envelope = Mathf.MoveTowards(envelope, 1f, Time.deltaTime * 3f);
            var session = NetworkSessionManager.Instance;
            double clock = session != null && session.NetworkManager != null && session.NetworkManager.IsListening
                ? session.NetworkManager.ServerTime.Time : Time.timeAsDouble;
            // 동일 줄에서는 같은 박자로 움직인다. 매 프레임 무작위 충격을 주지 않는다.
            double cycle = clock * rules.DynamicSwayFrequency;
            float slow = (float)(cycle % 1d) * Mathf.PI * 2f + lane * .7f;
            float fast = (float)((cycle * 2.17d) % 1d) * Mathf.PI * 2f + lane * .7f + .6f;
            float wave = (Mathf.Sin(slow) + .35f * Mathf.Sin(fast)) / 1.35f;
            float strength = Mathf.Lerp(rules.DynamicIdleSway, rules.DynamicMovingSway, motion);
            target.SetSurfaceSway(1f + load, wave * strength * (1f + load) * envelope);
        }

        bool TrySupport(NetworkPlayer player, out PlayerMover mover, out int lane, out float distance)
        {
            mover = null; lane = TightropeCourse.NoLane; distance = 0f;
            if (player == null || !player.IsSpawned || player.State != PlayerState.Normal) return false;
            var stack = PiggybackSystem.Current;
            var basePlayer = player;
            for (int i = 0; i < 4 && stack != null; i++)
            {
                var below = stack.GetBelow(basePlayer);
                if (below == null) break;
                basePlayer = below;
            }
            if (!basePlayer.IsSpawned || basePlayer.State != PlayerState.Normal
                || !basePlayer.TryGetComponent(out mover) || mover.PassThrough
                || (basePlayer.IsLocal ? mover.IsAirborne : basePlayer.IsAirborne)
                || (TightropeSwingPole.Current != null && TightropeSwingPole.Current.IsCrossing(basePlayer))
                || !course.TryGetSupportingRope(mover, out lane)) return false;
            if (basePlayer.TryGetComponent(out PlayerBalance balance) && balance.HasFallen) return false;
            distance = course.GetDistance(basePlayer.transform.position);
            return course.IsLaneUsable(lane, distance);
        }

        void Clear()
        {
            if (target != null) target.SetSurfaceSway(1f, 0f);
            load = motion = envelope = 0f;
            previousLane = TightropeCourse.NoLane;
            previousMover = null;
        }

        void OnDisable() => Clear();
    }
}
