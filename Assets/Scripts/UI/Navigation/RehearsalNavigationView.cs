using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.UI.Wayfinding
{
    /// <summary>Presentation only. Caller supplies a validated ground path; no NavMesh or game rules live here.</summary>
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
        readonly List<Vector3> route = new();
        readonly List<float> distances = new();
        readonly List<Vector3> placed = new();

        [SerializeField, HideInInspector] int visibleMarkerCount;
        public int VisibleMarkerCount => visibleMarkerCount;
        public IReadOnlyList<Transform> MarkerInstances => markers;
        public Transform Destination => destination;
        public event System.Action PresentationChanged;

        public void Configure(Transform prototype, Transform goal)
        {
            markerPrefab = prototype;
            destination = goal;
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
            destination.gameObject.SetActive(true);
            Place(destination, route[route.Count - 1], Vector3.forward, projectToGround);
            int count = 0;
            // Sample on the original path, never smooth positions across a wall or a corner.
            for (float d = total - Mathf.Max(.8f, destinationClearance); d >= startClearance && count < maximumMarkers; d -= Mathf.Max(.8f, spacing))
            {
                Vector3 p = Sample(d);
                bool overlaps = false;
                foreach (var previous in placed)
                    if ((previous - p).sqrMagnitude < .8f * .8f) { overlaps = true; break; }
                if (overlaps || Vector3.Distance(p, route[route.Count - 1]) < .9f) continue;
                var heading = Vector3.ProjectOnPlane(Sample(Mathf.Min(total, d + .3f)) - Sample(Mathf.Max(0, d - .3f)), Vector3.up);
                if (heading.sqrMagnitude < .0001f) continue;
                if (count == markers.Count)
                {
                    var copy = Instantiate(markerPrefab, transform);
                    copy.name = $"Route marker {count + 1:00}";
                    markers.Add(copy);
                }
                var marker = markers[count++];
                marker.gameObject.SetActive(true);
                Place(marker, p, heading, projectToGround);
                placed.Add(p);
            }
            for (int i = count; i < markers.Count; i++) markers[i].gameObject.SetActive(false);
            visibleMarkerCount = count;
            PresentationChanged?.Invoke();
        }

        Vector3 Sample(float distance)
        {
            for (int i = 1; i < route.Count; i++)
                if (distance <= distances[i])
                    return Vector3.Lerp(route[i - 1], route[i], (distance - distances[i - 1]) / (distances[i] - distances[i - 1]));
            return route[route.Count - 1];
        }

        static bool Finite(Vector3 p) => float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z);

        internal void PlaceGroundEffect(Transform target,Vector3 point) => Place(target,point,Vector3.forward,true);

        void Place(Transform target, Vector3 point, Vector3 heading, bool project)
        {
            var normal = Vector3.up;
            if (project)
            {
                float nearest = float.PositiveInfinity;
                foreach (var hit in Physics.RaycastAll(point + Vector3.up * .6f, Vector3.down, 1.4f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.normal.y < .45f || hit.distance >= nearest || hit.collider.GetComponentInParent<CurtainCall.Network.PlayerSync.NetworkPlayer>() != null) continue;
                    nearest = hit.distance; point = hit.point; normal = hit.normal;
                }
            }
            heading = Vector3.ProjectOnPlane(heading, normal);
            target.SetPositionAndRotation(point + normal * groundOffset, Quaternion.LookRotation(heading, normal));
        }

        public void Hide()
        {
            foreach (var marker in markers) if (marker != null) marker.gameObject.SetActive(false);
            if (destination != null) destination.gameObject.SetActive(false);
            visibleMarkerCount = 0;
            PresentationChanged?.Invoke();
        }
    }
}
