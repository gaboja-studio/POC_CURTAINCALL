using System;
using CurtainCall.Network.PlayerSync;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    /// <summary>Opaque maintenance cladding hides the route; only the bay beside the creature opens.</summary>
    public sealed class EscapePursuerScreens : MonoBehaviour
    {
        [Serializable]
        public sealed class Bay
        {
            public Transform panel;
            public Collider barrier;
            public Vector3 closedPosition;
            public Vector3 feet;
            public float height;
            public bool transit;
            [NonSerialized] public float opening;
            [NonSerialized] public Renderer renderer;
            [NonSerialized] public Vector3[] corners;
            [NonSerialized] public Vector3[] rearCorners;
            [NonSerialized] public bool requested;
        }

        [SerializeField] EscapeUnderstageChase actor;
        [SerializeField] EscapeUnderstageBreach breach;
        [SerializeField] Bay[] bays = Array.Empty<Bay>();
        double round = -1;
        int chosen = -1;
        public int OpenBay => chosen;
        public int BayCount => bays.Length;

        void OnEnable()
        {
            foreach (var bay in bays)
            {
                bay.renderer = bay.panel.GetComponent<Renderer>();
                var vertices = bay.panel.GetComponent<MeshFilter>().sharedMesh.vertices;
                bay.corners = new Vector3[4];
                for (int i = 0; i < 4; i++) bay.corners[i] = vertices[i] + bay.closedPosition;
                if (vertices.Length >= 8)
                {
                    bay.rearCorners = new Vector3[4];
                    for (int i = 0; i < 4; i++) bay.rearCorners[i] = vertices[i + 4] + bay.closedPosition;
                }
            }
            ResetPanels();
        }

        void Update()
        {
            var clock = EscapeRouteTimer.Current;
            double epoch = clock && clock.HasStarted ? clock.StartedAt : -1;
            if (round != epoch) { ResetPanels(); round = epoch; }
            bool active = epoch >= 0 && breach && breach.EntranceComplete && actor &&
                actor.gameObject.activeInHierarchy && !actor.InShowcase && EscapeCampaignContent.IsTraversalActive;
            int nearest = -1;
            float score = float.PositiveInfinity;
            var feet = active ? transform.InverseTransformPoint(actor.transform.position) : Vector3.zero;
            foreach (var bay in bays) bay.requested = false;
            if (active)
            {
                float reach = CurtainCall.Settings.GameSettings.EscapeRoute.Pursuer.armRange;
                for (int i = 0; i < bays.Length; i++)
                {
                    var bay = bays[i];
                    float distance = (feet - bay.feet).sqrMagnitude;
                    if (distance > reach * reach) continue;
                    var window = transform.TransformPoint(bay.feet);
                    bool nearbyRunner = actor.HoldingRunner && distance < 49f;
                    foreach (var player in NetworkPlayer.All)
                        if (player && player.IsSpawned && player.State == PlayerState.Normal &&
                            (player.transform.position - window).sqrMagnitude < 22f * 22f &&
                            (CrossesExpandedPanel(bay.corners, feet + Vector3.up * 3.4f,
                                transform.InverseTransformPoint(player.transform.position) + Vector3.up * 1.1f, .45f) ||
                            CrossesExpandedPanel(bay.rearCorners, feet + Vector3.up * 3.4f,
                                transform.InverseTransformPoint(player.transform.position) + Vector3.up * 1.1f, .45f)))
                        { nearbyRunner = true; break; }
                    if (!nearbyRunner) continue;
                    bay.requested = true;
                    if (distance < score) { score = distance; nearest = i; }
                }
            }
            // The already replicated actor pose drives these environmental panels on every peer.
            // They never control damage; the host's physical line-of-sight check still gates attacks.
            chosen = nearest;
            for (int i = 0; i < bays.Length; i++)
            {
                var bay = bays[i];
                bool transit = active && bay.transit && (feet - bay.feet).sqrMagnitude < 64f;
                bool open = transit || bay.requested || chosen >= 0 && (i == chosen || (bay.feet - bays[chosen].feet).sqrMagnitude < 16f);
                float next = Mathf.MoveTowards(bay.opening, open ? 1f : 0f, Time.deltaTime / .7f);
                if (next == bay.opening) continue;
                bay.opening = next;
                Present(bay);
            }
        }

        static void Present(Bay bay)
        {
            if (!bay.panel) return;
            float open = Mathf.SmoothStep(0, 1, bay.opening);
            bay.panel.localPosition = bay.closedPosition + Vector3.up * (bay.height * .5f * open);
            bay.panel.localScale = new Vector3(1, Mathf.Max(.001f, 1f - open), 1);
            if (bay.barrier) bay.barrier.enabled = open < .98f;
            // A fully rolled-up leaf has no thin flickering sliver at the roof seam.
            if (bay.renderer) bay.renderer.enabled = open < .999f;
        }

        internal static bool CrossesPanel(Vector3[] corners, Vector3 from, Vector3 to)
            => CrossesExpandedPanel(corners, from, to, 0);

        static bool CrossesExpandedPanel(Vector3[] corners, Vector3 from, Vector3 to, float margin)
        {
            if (corners == null || corners.Length != 4) return false;
            var normal = Vector3.Cross(corners[1] - corners[0], corners[2] - corners[0]).normalized;
            float denominator = Vector3.Dot(normal, to - from);
            if (Mathf.Abs(denominator) < .0001f) return false;
            float t = Vector3.Dot(normal, corners[0] - from) / denominator;
            if (t < 0 || t > 1) return false;
            var point = Vector3.Lerp(from, to, t);
            for (int i = 0; i < 4; i++)
                if (Vector3.Dot(normal, Vector3.Cross(corners[(i + 1) % 4] - corners[i], point - corners[i]))
                    < -.001f - margin * (corners[(i + 1) % 4] - corners[i]).magnitude)
                    return false;
            return true;
        }

        void ResetPanels()
        {
            chosen = -1;
            foreach (var bay in bays) { bay.opening = 0; Present(bay); }
        }

        void OnDisable() { ResetPanels(); round = -1; }
    }
}
