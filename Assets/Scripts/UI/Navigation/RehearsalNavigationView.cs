using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.UI.Wayfinding
{
    /// <summary>Presentation only. Caller supplies a validated ground path; no NavMesh or game rules live here.
    /// Each marker owns a fixed distance slot from the destination. In Play Mode refreshed paths glide and fade instead of snapping.</summary>
    public sealed class RehearsalNavigationView : MonoBehaviour
    {
        [SerializeField] Transform markerPrefab;
        [SerializeField] Transform destination;
        [SerializeField] List<Transform> markers = new();
        [SerializeField, Min(.8f)] float spacing = 1.15f;
        [SerializeField, Min(.02f)] float groundOffset = .045f;
        [SerializeField, Min(0)] float startClearance = .7f;
        [SerializeField, Min(.8f)] float destinationClearance = 1f;
        [SerializeField, Range(1, 256)] int maximumMarkers = 128;
        [Tooltip("새로 생기는 화살표의 페이드 인 시간(초). 경로에서 벗어난 화살표는 즉시 사라진다.")]
        [SerializeField, Min(.02f)] float fadeSeconds = .25f;
        [Tooltip("이 거리 이상 바뀌거나 벽을 넘는 위치 변경은 미끄러지지 않고 즉시 사라진 뒤 새 위치에서 나타난다.")]
        [SerializeField, Min(.5f)] float relocateDistance = 6f;
        readonly List<Vector3> route = new();
        readonly List<float> distances = new();
        readonly List<Vector3> placed = new();
        readonly List<Slot> slots = new();
        readonly Slot goalSlot = new() { OwnsActive = true }, startSlot = new();
        static readonly RaycastHit[] Hits = new RaycastHit[8];

        float followOverride = -1;
        const float StartFadeMeters = 1f;
        float startGap;
        Transform liveStart;
        Vector3 startPoint, smoothedStart;
        bool hasStart;
        [SerializeField, HideInInspector] int visibleMarkerCount;
        [SerializeField, HideInInspector] float routeLength;
        public int VisibleMarkerCount => visibleMarkerCount;
        public IReadOnlyList<Transform> MarkerInstances => markers;
        public Transform Destination => destination;
        /// <summary>Path length of the latest valid route, in metres.</summary>
        public float RouteLength => routeLength;
        public float DestinationPresence => Presence(goalSlot, destination);
        public float StartPresence => Presence(startSlot, startSlot.Transform);
        public event System.Action PresentationChanged;
        /// <summary>Path correction time constant. Defaults to GameSettings > Base > 네비게이션 안내 > 경로 보정 시간.</summary>
        public float FollowSeconds => followOverride >= 0 ? followOverride : GameSettings.Base.Navigation.FollowSeconds;
        /// <summary>Negative returns to the common setting; 0 snaps immediately.</summary>
        public void SetFollowOverride(float seconds) => followOverride = float.IsFinite(seconds) && seconds >= 0 ? Mathf.Min(seconds, 2) : -1;

        sealed class Slot
        {
            public Transform Transform;
            public Vector3 Position, TargetPosition;
            public Quaternion Rotation = Quaternion.identity, TargetRotation = Quaternion.identity;
            public float Presence;
            public bool Wanted, Placed, OwnsActive;
        }

        public void Configure(Transform prototype, Transform goal)
        {
            markerPrefab = prototype;
            destination = goal;
        }

        /// <summary>Distance from the destination of a marker slot. Stable while the start moves, so pulses do not slide.</summary>
        public float GetMarkerGoalDistance(int index) => Mathf.Max(.8f, destinationClearance) + index * Mathf.Max(.8f, spacing);

        public float GetMarkerPresence(int index)
        {
            if (index < 0 || index >= markers.Count) return 0;
            return Presence(index < slots.Count ? slots[index] : null, markers[index]) * StartFade(markers[index].position);
        }

        /// <summary>For player starts: no arrows within <paramref name="gap"/> metres of the feet, then a 1m distance fade.
        /// <paramref name="live"/> (optional) is read every frame; otherwise the smoothed route start is used.</summary>
        public void SetPlayerStartGap(float gap, Transform live = null)
        {
            startGap = float.IsFinite(gap) ? Mathf.Clamp(gap, 0, 5) : 0;
            liveStart = startGap > 0 ? live : null;
        }

        float StartFade(Vector3 marker)
        {
            if (startGap <= 0 || !hasStart) return 1;
            var from = liveStart != null && liveStart.gameObject.activeInHierarchy ? liveStart.position : smoothedStart;
            var offset = marker - from; offset.y = 0;
            return Mathf.SmoothStep(0, 1, (offset.magnitude - startGap) / StartFadeMeters);
        }

        /// <summary>World-space floor points, in travel order. Destination-anchored spacing avoids sliding as the start moves.</summary>
        public void SetPath(IReadOnlyList<Vector3> floorPoints, bool projectToGround = false)
        {
            route.Clear(); distances.Clear(); placed.Clear();
            if (floorPoints == null || markerPrefab == null || destination == null) { Hide(); return; }
            float total = 0;
            foreach (var point in floorPoints)
            {
                if (!Finite(point)) { Hide(); return; }
                if (route.Count > 0)
                {
                    float length = Vector3.Distance(route[route.Count - 1], point);
                    if (length < .001f) continue;
                    total += length;
                }
                route.Add(point); distances.Add(total);
            }
            if (route.Count < 2 || total < .05f) { Hide(); return; }
            bool immediate = !Application.isPlaying;
            int poolSize = markers.Count;
            routeLength = total;
            goalSlot.Transform = destination;
            Target(goalSlot, route[route.Count - 1], Vector3.forward, projectToGround, immediate);
            if (startSlot.Transform != null) Target(startSlot, route[0], Vector3.forward, true, immediate);
            startPoint = route[0];
            if (!hasStart || immediate) smoothedStart = startPoint;
            hasStart = true;
            float clearance = Mathf.Max(startClearance, startGap);
            int slot = 0, visible = 0;
            // Sample on the original path, never smooth positions across a wall or a corner.
            for (float d = total - Mathf.Max(.8f, destinationClearance); d >= clearance && slot < maximumMarkers; d -= Mathf.Max(.8f, spacing), slot++)
            {
                if (slot == markers.Count)
                {
                    var copy = Instantiate(markerPrefab, transform);
                    copy.name = $"Route marker {slot + 1:00}";
                    copy.gameObject.SetActive(false);
                    markers.Add(copy);
                }
                EnsureSlots();
                Vector3 p = Sample(d);
                bool overlaps = false;
                foreach (var previous in placed)
                    if ((previous - p).sqrMagnitude < .8f * .8f) { overlaps = true; break; }
                var heading = Vector3.ProjectOnPlane(Sample(Mathf.Min(total, d + .3f)) - Sample(Mathf.Max(0, d - .3f)), Vector3.up);
                if (overlaps || Vector3.Distance(p, route[route.Count - 1]) < .9f || heading.sqrMagnitude < .0001f)
                {
                    Release(slots[slot], immediate);
                    continue;
                }
                Target(slots[slot], p, heading, projectToGround, immediate);
                placed.Add(p);
                visible = slot + 1;
            }
            EnsureSlots();
            for (int i = slot; i < markers.Count; i++) Release(slots[i], immediate);
            visibleMarkerCount = visible;
            if (immediate || markers.Count != poolSize) PresentationChanged?.Invoke();
        }

        /// <summary>Lets a ground effect (for example the departure wave) follow the route start with the same smoothing.</summary>
        internal void TrackStartEffect(Transform effect, Vector3 point)
        {
            if (startSlot.Transform != effect) { startSlot.Transform = effect; startSlot.Placed = false; startSlot.Presence = 0; }
            if (effect != null) Target(startSlot, point, Vector3.forward, true, !Application.isPlaying);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float followSeconds = FollowSeconds;
            float follow = followSeconds <= 0 ? 1 : 1 - Mathf.Exp(-dt / followSeconds);
            float fade = dt / Mathf.Max(.02f, fadeSeconds);
            if (hasStart) smoothedStart = Vector3.Lerp(smoothedStart, startPoint, follow);
            Step(goalSlot, follow, fade);
            Step(startSlot, follow, fade);
            for (int i = 0; i < slots.Count; i++) Step(slots[i], follow, fade);
        }

        void EnsureSlots()
        {
            while (slots.Count < markers.Count) slots.Add(new Slot { Transform = markers[slots.Count], OwnsActive = true });
        }

        void Target(Slot slot, Vector3 point, Vector3 heading, bool project, bool immediate)
        {
            if (slot.Transform == null) return;
            Pose(point, heading, project, out var position, out var rotation);
            slot.TargetPosition = position; slot.TargetRotation = rotation; slot.Wanted = true;
            // New slots and jumps across walls never leave a trace at the old pose: hide at once, fade in at the new pose.
            if (immediate || !slot.Placed || Teleports(slot.Position, position))
            {
                slot.Position = position; slot.Rotation = rotation;
                slot.Presence = immediate ? 1 : 0; slot.Placed = true;
                if (slot.OwnsActive) slot.Transform.gameObject.SetActive(true);
                slot.Transform.SetPositionAndRotation(position, rotation);
            }
        }

        /// <summary>Arrows that leave the route disappear immediately instead of lingering off the path.</summary>
        static void Release(Slot slot, bool immediate)
        {
            slot.Wanted = false; slot.Placed = false; slot.Presence = 0;
            if (slot.OwnsActive && slot.Transform != null) slot.Transform.gameObject.SetActive(false);
        }

        static void Step(Slot slot, float follow, float fade)
        {
            if (slot.Transform == null || !slot.Placed || !slot.Wanted) return;
            slot.Presence = Mathf.MoveTowards(slot.Presence, 1, fade);
            slot.Position = Vector3.Lerp(slot.Position, slot.TargetPosition, follow);
            slot.Rotation = Quaternion.Slerp(slot.Rotation, slot.TargetRotation, follow);
            slot.Transform.SetPositionAndRotation(slot.Position, slot.Rotation);
        }

        static float Presence(Slot slot, Transform target)
        {
            if (slot != null && slot.Placed) return slot.Presence;
            return target != null && target.gameObject.activeSelf && (slot == null || slot.Transform == null) ? 1 : 0;
        }

        /// <summary>Gliding is only safe for continuous motion. Large jumps or a wall between the poses fade instead.</summary>
        bool Teleports(Vector3 from, Vector3 to)
        {
            var delta = to - from;
            float distance = delta.magnitude;
            if (distance > relocateDistance) return true;
            if (distance < .05f) return false;
            int count = Physics.RaycastNonAlloc(from + Vector3.up * .5f, delta / distance, Hits, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (Hits[i].collider.GetComponentInParent<NetworkPlayer>() == null) return true;
            return false;
        }

        Vector3 Sample(float distance)
        {
            for (int i = 1; i < route.Count; i++)
                if (distance <= distances[i])
                    return Vector3.Lerp(route[i - 1], route[i], (distance - distances[i - 1]) / (distances[i] - distances[i - 1]));
            return route[route.Count - 1];
        }

        static bool Finite(Vector3 p) => float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z);

        void Pose(Vector3 point, Vector3 heading, bool project, out Vector3 position, out Quaternion rotation)
        {
            var normal = Vector3.up;
            if (project)
            {
                float nearest = float.PositiveInfinity;
                foreach (var hit in Physics.RaycastAll(point + Vector3.up * .6f, Vector3.down, 1.4f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.normal.y < .45f || hit.distance >= nearest || hit.collider.GetComponentInParent<NetworkPlayer>() != null) continue;
                    nearest = hit.distance; point = hit.point; normal = hit.normal;
                }
            }
            heading = Vector3.ProjectOnPlane(heading, normal);
            position = point + normal * groundOffset;
            rotation = Quaternion.LookRotation(heading, normal);
        }

        public void Hide()
        {
            EnsureSlots();
            foreach (var slot in slots) Release(slot, true);
            Release(goalSlot, true);
            if (destination != null) destination.gameObject.SetActive(false);
            startSlot.Placed = false; startSlot.Presence = 0; startSlot.Wanted = false;
            hasStart = false;
            foreach (var marker in markers) if (marker != null) marker.gameObject.SetActive(false);
            visibleMarkerCount = 0;
            PresentationChanged?.Invoke();
        }
    }
}
