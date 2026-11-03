using System;
using CurtainCall.HumanCannon;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    // Presentation only. Normal gameplay supplies the same course clock on all peers.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class EscapeConveyorProcession : MonoBehaviour
    {
        [Serializable]
        struct Carrier
        {
            public Transform hanger;
            public Transform opposite;
            public GameObject load;
            public Renderer costume;
            public Color outfitColor;
            public int outfitPattern;
        }

        [SerializeField] Carrier[] carriers = Array.Empty<Carrier>();
        [SerializeField] Transform saw;
        [SerializeField, Min(0.01f)] float trackLength = 36f;
        [SerializeField] float cutAt = 18f;
        [SerializeField, Min(0.01f)] float speed = 2.25f;
        const float DropSeconds = 0.52f;
        const float DropAcceleration = 13f;
        static readonly int VariantColor = Shader.PropertyToID("_VariantColor");
        static readonly int VariantPattern = Shader.PropertyToID("_VariantPattern");

        readonly Plane[] frustum = new Plane[6];
        Bounds motionBounds;
        double lastTime = double.NaN;
        Camera viewer;

        void OnEnable()
        {
            RefreshOutfits();
            if (!Application.IsPlaying(gameObject)) return;
            if (!saw) { enabled = false; return; }
            lastTime = double.NaN;
            motionBounds = new Bounds(saw.position, Vector3.zero);
            foreach (var blade in saw.GetComponentsInChildren<Renderer>(true))
            {
                float radius = Vector3.Distance(saw.position, blade.bounds.center) + blade.bounds.extents.magnitude;
                motionBounds.Encapsulate(new Bounds(saw.position, Vector3.one * (2f * radius)));
            }
            foreach (var carrier in carriers)
            {
                AddTravelBounds(carrier.hanger, trackLength, false);
                if (carrier.opposite) AddTravelBounds(carrier.opposite, trackLength, false);
                AddTravelBounds(carrier.load.transform, cutAt, true);
            }
        }

        // The production characters share outfit materials. Store their catalog selection
        // with the scene and restore the two masked costume slots once on load.
        public void RefreshOutfits()
        {
            var properties = new MaterialPropertyBlock();
            foreach (var carrier in carriers)
            {
                if (!carrier.costume) continue;
                for (int slot = 0; slot < 2; slot++)
                {
                    carrier.costume.GetPropertyBlock(properties, slot);
                    properties.SetColor(VariantColor, carrier.outfitColor);
                    properties.SetFloat(VariantPattern, carrier.outfitPattern);
                    carrier.costume.SetPropertyBlock(properties, slot);
                }
            }
        }

        // The line is fixed scene geometry. Sweep the actual moving renderers at startup.
        // Include hidden loads and the drop so an empty stretch in view still wakes the line.
        void AddTravelBounds(Transform part, float end, bool falls)
        {
            foreach (var renderer in part.GetComponentsInChildren<Renderer>(true))
            {
                var local = renderer.localBounds;
                var matrix = renderer.localToWorldMatrix;
                var atStart = transform.position - part.position;
                var extents = local.extents;
                for (int i = 0; i < 8; i++)
                {
                    var corner = local.center + new Vector3((i & 1) == 0 ? -extents.x : extents.x,
                        (i & 2) == 0 ? -extents.y : extents.y, (i & 4) == 0 ? -extents.z : extents.z);
                    var start = matrix.MultiplyPoint3x4(corner) + atStart;
                    var finish = start + transform.TransformVector(Vector3.right * end);
                    motionBounds.Encapsulate(start);
                    motionBounds.Encapsulate(finish);
                    if (falls)
                        motionBounds.Encapsulate(finish + transform.TransformVector(Vector3.down * (DropAcceleration * DropSeconds * DropSeconds)));
                }
            }
        }

        void LateUpdate()
        {
            if (!Application.IsPlaying(gameObject)) return;
            if (!viewer || !viewer.isActiveAndEnabled) viewer = Camera.main;
            if (!viewer || motionBounds.SqrDistance(viewer.transform.position) > 100f * 100f) return;
            GeometryUtility.CalculateFrustumPlanes(viewer, frustum);
            if (!GeometryUtility.TestPlanesAABB(frustum, motionBounds)) return;

            var course = HumanCannonHazardCourse.Current;
            double time = course != null ? course.Elapsed : Time.timeAsDouble;
            if (time == lastTime) return;
            lastTime = time;
            Present(time);
        }

        void Present(double time)
        {
            float travel = (float)((time * speed) % trackLength);
            saw.localRotation = Quaternion.AngleAxis((float)(time % 1d) * 360f, Vector3.forward);
            for (int i = 0; i < carriers.Length; i++)
            {
                var carrier = carriers[i];
                float span = carrier.opposite ? trackLength * .5f : trackLength;
                float x = Mathf.Repeat(travel + 2f + i * span / carriers.Length, trackLength);
                carrier.hanger.localPosition = new Vector3(x, 0f, 0f);
                float loadedX = x;
                if (carrier.opposite)
                {
                    float otherX = Mathf.Repeat(x + span, trackLength);
                    carrier.opposite.localPosition = new Vector3(otherX, 0f, 0f);
                    loadedX = Mathf.Min(x, otherX);
                }

                // The cut and drop lie in the first half of the line. Only that hanger
                // needs the pair's load. Its empty counterpart continues on the same rail.
                float dropTime = Mathf.Max(0f, loadedX - cutAt) / speed;
                bool visible = dropTime < DropSeconds;
                if (visible)
                {
                    float loadX = Mathf.Min(loadedX, cutAt);
                    var load = carrier.load.transform;
                    if (load.parent == carrier.hanger) loadX -= x;
                    var position = new Vector3(loadX, -DropAcceleration * dropTime * dropTime, 0f);
                    if (load.localPosition != position) load.localPosition = position;
                }
                if (carrier.load.activeSelf != visible) carrier.load.SetActive(visible);
            }
        }
    }
}
