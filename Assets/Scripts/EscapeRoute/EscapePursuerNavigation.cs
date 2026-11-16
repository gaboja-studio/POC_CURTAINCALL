using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using UnityEngine;
using UnityEngine.AI;

namespace CurtainCall.EscapeRoute
{
    /// <summary>Finds supported routes around scenery inside an authored pursuit area.</summary>
    public sealed class EscapePursuerNavigation
    {
        const float Radius = 1.4f;
        const float Cell = 1.5f;
        const float ShortcutInterval = .25f;
        const float SearchInterval = .75f;
        static readonly Vector3[] SupportOffsets = { Vector3.zero, Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
        static readonly Collider[] overlapResults = new Collider[32];
        static readonly RaycastHit[] castResults = new RaycastHit[32];
        readonly List<Vector3> path = new();
        BoxCollider area;
        Vector3 target;
        float nextSearch, nextShortcut;
        bool directPath;

        public void Reset()
        {
            path.Clear();
            area = null;
            nextSearch = nextShortcut = 0;
            directPath = false;
        }

        public Vector3 Step(Transform actor, BoxCollider bounds, Vector3 goal, float distance)
        {
            Vector3 from = actor.position;
            if (!bounds || distance <= 0) return from;
            if (area != bounds) { Reset(); area = bounds; }
            goal = Clamp(bounds, goal);
            // Follow a moving goal immediately, but validate the actual step every time.
            if (directPath && path.Count == 1) path[0] = goal;
            bool hadPath = path.Count > 0;
            bool cached = NextStep(from, distance, out var next);
            if (hadPath && !cached) nextShortcut = 0;
            if (cached && !Walkable(actor, from, next))
            {
                path.Clear();
                directPath = false;
                cached = false;
                // New scenery or missing support invalidates the cached step immediately.
                nextSearch = nextShortcut = 0;
            }
            bool movedGoal = (goal - target).sqrMagnitude > 4;
            float now = Time.time;
            if (now >= nextShortcut || movedGoal && now >= nextSearch)
            {
                nextShortcut = now + ShortcutInterval;
                if (Walkable(actor, from, goal))
                {
                    path.Clear();
                    path.Add(goal);
                    directPath = true;
                    target = goal;
                    nextSearch = now + SearchInterval;
                    return Vector3.MoveTowards(from, goal, distance);
                }
                if (directPath)
                {
                    path.Clear();
                    directPath = false;
                    cached = false;
                    nextSearch = 0;
                }
            }
            if (now >= nextSearch && (!cached || movedGoal))
            {
                target = goal;
                nextSearch = now + SearchInterval;
                Search(actor, bounds, from, goal);
                directPath = false;
                cached = NextStep(from, distance, out next);
                if (cached && !Walkable(actor, from, next)) { path.Clear(); cached = false; }
            }
            return cached ? next : from;
        }

        bool NextStep(Vector3 from, float distance, out Vector3 next)
        {
            // A direct goal may keep moving within the waypoint tolerance.
            if (!directPath)
                while (path.Count > 0 && (path[0] - from).sqrMagnitude < .04f) path.RemoveAt(0);
            next = path.Count > 0 ? Vector3.MoveTowards(from, path[0], distance) : from;
            return path.Count > 0;
        }

        public static Vector3 Clamp(BoxCollider bounds, Vector3 position)
        {
            Vector3 p = bounds.transform.InverseTransformPoint(position) - bounds.center;
            Vector3 margin = new(Radius / Mathf.Abs(bounds.transform.lossyScale.x), 0,
                Radius / Mathf.Abs(bounds.transform.lossyScale.z));
            p.x = Mathf.Clamp(p.x, -bounds.size.x * .5f + margin.x, bounds.size.x * .5f - margin.x);
            p.z = Mathf.Clamp(p.z, -bounds.size.z * .5f + margin.z, bounds.size.z * .5f - margin.z);
            // Pursuit areas have their bottom on the walking surface.
            p.y = -bounds.size.y * .5f;
            return bounds.transform.TransformPoint(p + bounds.center);
        }

        static bool Obstacle(Collider collider, Transform actor) => !collider.transform.IsChildOf(actor)
            && !collider.GetComponentInParent<NetworkPlayer>()
            && !collider.GetComponentInParent<EscapePursuerDebris>();

        public static bool Sees(Transform actor, Vector3 feet, Vector3 target)
        {
            Vector3 from = feet + Vector3.up * 3f, delta = target - from;
            if (delta.sqrMagnitude < .0001f) return true;
            var hits = castResults;
            int count = Physics.RaycastNonAlloc(from, delta.normalized, hits, delta.magnitude,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            // A full buffer cannot prove visibility: omitted hits may include a real wall.
            if (count == hits.Length)
            {
                hits = Physics.RaycastAll(from, delta.normalized, delta.magnitude,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                count = hits.Length;
            }
            for (int i = 0; i < count; i++) if (Obstacle(hits[i].collider, actor)) return false;
            return true;
        }

        // Pole riders have no navigable floor under their feet. Seek a supported attack
        // position on this deck, including around its occluders, rather than just clamping X/Z.
        public static bool TryAttackPosition(Transform actor, BoxCollider bounds, Vector3 target, float range, out Vector3 goal,
            System.Func<Vector3, bool> canStrike = null)
        {
            goal = actor.position;
            if (!bounds) return false;
            Vector3 scale = bounds.transform.lossyScale;
            int width = Mathf.Max(2, Mathf.CeilToInt(bounds.size.x * Mathf.Abs(scale.x) / Cell));
            int depth = Mathf.Max(2, Mathf.CeilToInt(bounds.size.z * Mathf.Abs(scale.z) / Cell));
            if (width * depth > 8192) return false;
            Vector3 selected = actor.position;
            float best = float.PositiveInfinity;
            void Consider(Vector3 point)
            {
                float targetDistance = Vector3.Distance(point + Vector3.up * 3f, target);
                if (targetDistance > range || !Supported(point) || !Clear(actor, point, point) || !Sees(actor, point, target)
                    || canStrike != null && !canStrike(point)) return;
                float score = Vector3.Distance(actor.position, point) + targetDistance * .15f;
                if (score < best) { best = score; selected = point; }
            }
            Consider(Clamp(bounds, actor.position));
            Consider(Clamp(bounds, target));
            for (int z = 0; z < depth; z++)
                for (int x = 0; x < width; x++)
                {
                    Vector3 local = bounds.center + new Vector3(
                        Mathf.Lerp(-bounds.size.x * .5f, bounds.size.x * .5f, (x + .5f) / width),
                        -bounds.size.y * .5f,
                        Mathf.Lerp(-bounds.size.z * .5f, bounds.size.z * .5f, (z + .5f) / depth));
                    Consider(Clamp(bounds, bounds.transform.TransformPoint(local)));
                }
            goal = selected;
            return !float.IsPositiveInfinity(best);
        }

        public static bool TryNavApproach(NavMeshAgent agent, Vector3 target, out Vector3 goal)
        {
            goal = agent.transform.position;
            if (!agent.isOnNavMesh) return false;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            var path = new NavMeshPath();
            float best = float.PositiveInfinity;
            Vector3 selected = goal;
            void Consider(Vector3 point, float radius)
            {
                if (!NavMesh.SamplePosition(point, out var hit, radius, filter) ||
                    !NavMesh.CalculatePath(agent.transform.position, hit.position, filter, path) || path.corners.Length == 0) return;
                // Partial paths still contain a real reachable approach to a suspended rider.
                Vector3 end = path.corners[path.corners.Length - 1];
                float score = Vector3.Distance(end, target);
                if (score < best) { best = score; selected = end; }
            }
            Consider(target, 2.5f);
            if (best > 2.5f)
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI * .25f;
                    Consider(target + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 6f, 8f);
                }
            goal = selected;
            return !float.IsPositiveInfinity(best);
        }

        internal static bool TryNavStrikePosition(NavMeshAgent agent, Vector3 target, float reach,
            System.Predicate<Vector3> clearStrike, out Vector3 goal)
        {
            Vector3 from = agent.transform.position;
            goal = from;
            if (!agent.isOnNavMesh) return false;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            var path = new NavMeshPath();
            Vector3 selected = from;
            float best = float.PositiveInfinity;
            float outer = Mathf.Max(.65f, reach - .4f);
            foreach (float radius in new[] { outer, outer * .5f })
                for (int i = 0; i < 12; i++)
                {
                    float angle = i * Mathf.PI / 6f;
                    Vector3 sample = target + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
                    if (!NavMesh.SamplePosition(sample, out var candidate, .85f, filter)) continue;
                    Vector3 feet = candidate.position;
                    if (Mathf.Abs(feet.y - target.y) > 1.5f ||
                        Vector3.ProjectOnPlane(feet - target, Vector3.up).sqrMagnitude > reach * reach ||
                        (feet - from).sqrMagnitude < .16f || !clearStrike(feet)) continue;
                    if (!NavMesh.CalculatePath(from, feet, filter, path) || path.status != NavMeshPathStatus.PathComplete)
                        continue;
                    float length = 0;
                    Vector3 previous = from;
                    foreach (Vector3 corner in path.corners)
                    {
                        length += Vector3.Distance(previous, corner);
                        previous = corner;
                    }
                    if (length < best) { best = length; selected = feet; }
                }
            goal = selected;
            return !float.IsPositiveInfinity(best);
        }

        public static bool Clear(Transform actor, Vector3 from, Vector3 to)
        {
            var overlaps = overlapResults;
            int count = Physics.OverlapCapsuleNonAlloc(to + Vector3.up * 1.5f, to + Vector3.up * 3.5f,
                Radius, overlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length)
            {
                overlaps = Physics.OverlapCapsule(to + Vector3.up * 1.5f, to + Vector3.up * 3.5f,
                    Radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                count = overlaps.Length;
            }
            for (int i = 0; i < count; i++) if (Obstacle(overlaps[i], actor)) return false;
            Vector3 delta = to - from;
            if (delta.sqrMagnitude < .0001f) return true;
            var hits = castResults;
            count = Physics.CapsuleCastNonAlloc(from + Vector3.up * 1.5f, from + Vector3.up * 3.5f,
                Radius, delta.normalized, hits, delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == hits.Length)
            {
                hits = Physics.CapsuleCastAll(from + Vector3.up * 1.5f, from + Vector3.up * 3.5f,
                    Radius, delta.normalized, delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                count = hits.Length;
            }
            for (int i = 0; i < count; i++) if (Obstacle(hits[i].collider, actor)) return false;
            return true;
        }

        public static bool Supported(Vector3 feet)
        {
            foreach (var offset in SupportOffsets)
                if (!Physics.Raycast(feet + offset * 1.2f + Vector3.up * .2f, Vector3.down, out var hit, .55f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || hit.normal.y < .8f) return false;
            return true;
        }

        public static bool ClearJump(Transform actor, Vector3 from, Vector3 to)
        {
            if (!Supported(from) || !Supported(to)) return false;
            float arc = EscapeUnderstageChase.ServiceJumpArc(from, to);
            int steps = Mathf.Max(8, Mathf.CeilToInt((Vector3.Distance(from, to) + arc) / .5f));
            Vector3 previous = from;
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector3 next = Vector3.Lerp(from, to, t) + Vector3.up * (arc * t * (1 - t));
                if (!Clear(actor, previous, next)) return false;
                previous = next;
            }
            return true;
        }

        static bool Walkable(Transform actor, Vector3 from, Vector3 to)
        {
            if (!Clear(actor, from, to)) return false;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from, to) / .75f));
            for (int i = 1; i <= steps; i++)
                if (!Supported(Vector3.Lerp(from, to, i / (float)steps))) return false;
            return true;
        }

        void Search(Transform actor, BoxCollider bounds, Vector3 from, Vector3 goal)
        {
            path.Clear();
            Vector3 scale = bounds.transform.lossyScale;
            int width = Mathf.Max(2, Mathf.CeilToInt(bounds.size.x * Mathf.Abs(scale.x) / Cell));
            int depth = Mathf.Max(2, Mathf.CeilToInt(bounds.size.z * Mathf.Abs(scale.z) / Cell));
            if (width * depth > 8192) return;
            var positions = new Vector3[width * depth];
            var costs = new float[positions.Length];
            var parents = new int[positions.Length];
            var valid = new sbyte[positions.Length];
            int start = 0;
            float startDistance = float.PositiveInfinity;
            for (int z = 0; z < depth; z++)
                for (int x = 0; x < width; x++)
                {
                    int id = z * width + x;
                    Vector3 local = bounds.center + new Vector3(
                        Mathf.Lerp(-bounds.size.x * .5f, bounds.size.x * .5f, (x + .5f) / width),
                        -bounds.size.y * .5f,
                        Mathf.Lerp(-bounds.size.z * .5f, bounds.size.z * .5f, (z + .5f) / depth));
                    positions[id] = Clamp(bounds, bounds.transform.TransformPoint(local));
                    costs[id] = float.PositiveInfinity;
                    parents[id] = -1;
                    float d = (positions[id] - from).sqrMagnitude;
                    if (d < startDistance && Walkable(actor, from, positions[id]))
                    {
                        startDistance = d;
                        start = id;
                    }
                }
            if (float.IsPositiveInfinity(startDistance)) return;
            var open = new SortedSet<(float score, int id)>();
            costs[start] = 0;
            open.Add((Vector3.Distance(positions[start], goal), start));
            int best = start;
            float closest = Vector3.Distance(positions[start], goal);
            var closed = new bool[positions.Length];
            while (open.Count > 0)
            {
                var entry = open.Min;
                open.Remove(entry);
                int current = entry.id;
                if (closed[current]) continue;
                closed[current] = true;
                float remaining = Vector3.Distance(positions[current], goal);
                if (remaining < closest) { closest = remaining; best = current; }
                if (remaining < Cell && Walkable(actor, positions[current], goal)) { best = current; closest = remaining; break; }
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        int x = current % width + dx, z = current / width + dz;
                        if (x < 0 || x >= width || z < 0 || z >= depth) continue;
                        int next = z * width + x;
                        if (closed[next]) continue;
                        if (valid[next] == 0) valid[next] = (sbyte)(Supported(positions[next]) && Clear(actor, positions[next], positions[next]) ? 1 : -1);
                        if (valid[next] < 0) continue;
                        float cost = costs[current] + Vector3.Distance(positions[current], positions[next]);
                        if (cost >= costs[next] || !Walkable(actor, positions[current], positions[next])) continue;
                        costs[next] = cost;
                        parents[next] = current;
                        open.Add((cost + Vector3.Distance(positions[next], goal), next));
                    }
            }
            for (int id = best; id >= 0; id = parents[id]) path.Add(positions[id]);
            path.Reverse();
            if (closest < Cell && Walkable(actor, positions[best], goal)) path.Add(goal);
        }
    }
}
