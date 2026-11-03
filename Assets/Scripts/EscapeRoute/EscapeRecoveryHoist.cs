using CurtainCall.HumanCannon;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    // Decorative transfer line. All poses are derived from the shared course time,
    // including clients that first see the rig after the shot has already landed.
    [DisallowMultipleComponent]
    public sealed class EscapeRecoveryHoist : MonoBehaviour
    {
        [SerializeField] Transform trolley;
        [SerializeField] Transform hook;
        [SerializeField] Transform drum;
        [SerializeField] Transform cable;
        [SerializeField] Transform dock;
        [SerializeField] Transform payload;
        [SerializeField] Transform barrel;
        [SerializeField] Transform muzzle;
        [SerializeField] Transform destination;
        [SerializeField, Min(.01f)] float cableLength = 7.05f;
        [SerializeField, Min(.01f)] float drumRadius = .51f;
        [SerializeField, Min(.01f)] float stroke = 1.15f;
        [SerializeField, Min(.1f)] float period = 8f;

        const float Fire = .05f;
        const float Catch = .205f;
        const float Depart = .29f;
        const float Unload = .68f;
        const float Return = .73f;
        const float Home = .98f;
        const float ArcHeight = 1.45f;
        const float Recoil = .52f;

        readonly Plane[] planes = new Plane[6];
        Vector3 trolleyHome, hookLow, barrelHome, recoilAxis;
        Quaternion drumRest;
        Bounds travelBounds;
        Camera viewer;
        float lastPhase, lastTravel, lastLift, lastKick;
        bool wasDocked;

        void Awake()
        {
            if (!trolley || !hook || !drum || !cable || !dock || !payload || !barrel || !muzzle ||
                !destination || cableLength <= stroke || drumRadius <= 0f || period <= 0f)
            {
                enabled = false;
                return;
            }

            trolleyHome = trolley.localPosition;
            hookLow = hook.localPosition;
            drumRest = drum.localRotation;
            barrelHome = barrel.localPosition;
            recoilAxis = barrel.parent.InverseTransformVector(muzzle.forward);

            travelBounds = new Bounds(muzzle.position, Vector3.zero);
            var transfer = destination.position - trolley.position;
            var lift = hook.parent.TransformVector(Vector3.up * stroke);
            foreach (var part in trolley.GetComponentsInChildren<Renderer>(true))
            {
                var bounds = part.bounds;
                travelBounds.Encapsulate(bounds);
                bounds.center += transfer + lift;
                travelBounds.Encapsulate(bounds);
            }
            foreach (var part in barrel.GetComponentsInChildren<Renderer>(true))
                travelBounds.Encapsulate(part.bounds);

            // The cargo starts hidden in the barrel. Its local mesh bounds are valid
            // even before the renderer has appeared in a frame.
            float cargoRadius = 0f;
            foreach (var part in payload.GetComponentsInChildren<Renderer>(true))
            {
                var bounds = part.localBounds;
                float offset = Vector3.Distance(part.transform.TransformPoint(bounds.center), payload.position);
                float radius = Vector3.Scale(bounds.extents, part.transform.lossyScale).magnitude;
                cargoRadius = Mathf.Max(cargoRadius, offset + radius);
            }
            travelBounds.Encapsulate(dock.position);
            travelBounds.Encapsulate(dock.position + transfer + lift);
            travelBounds.Expand(2f * (cargoRadius + ArcHeight));
            lastPhase = lastTravel = lastLift = lastKick = float.NaN;
            wasDocked = false;
            Sample(0d);
        }

        void LateUpdate()
        {
            if (!viewer || !viewer.isActiveAndEnabled) viewer = Camera.main;
            if (!viewer || travelBounds.SqrDistance(viewer.transform.position) > 100f * 100f) return;
            GeometryUtility.CalculateFrustumPlanes(viewer, planes);
            if (!GeometryUtility.TestPlanesAABB(planes, travelBounds)) return;

            var course = HumanCannonHazardCourse.Current;
            Sample(course != null ? course.Elapsed : Time.timeAsDouble);
        }

        static float Ease(float phase, float start, float end)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, phase));
        }

        void Sample(double seconds)
        {
            float phase = (float)((seconds % period) / period);
            if (phase == lastPhase) return;
            lastPhase = phase;

            float travel = Ease(phase, Depart, Unload) - Ease(phase, Return, Home);
            bool trolleyMoved = travel != lastTravel;
            if (trolleyMoved)
            {
                trolley.localPosition = Vector3.Lerp(trolleyHome, destination.localPosition, travel);
                lastTravel = travel;
            }

            float lift = stroke * (Ease(phase, Catch + .025f, Depart + .07f) - Ease(phase, .86f, Home));
            bool hookMoved = lift != lastLift;
            if (hookMoved)
            {
                hook.localPosition = hookLow + Vector3.up * lift;
                cable.localScale = new Vector3(1f, 1f - lift / cableLength, 1f);
                drum.localRotation = drumRest * Quaternion.AngleAxis(lift / drumRadius * Mathf.Rad2Deg, Vector3.right);
                lastLift = lift;
            }

            float kick = Ease(phase, Fire, Fire + .018f) - Ease(phase, Fire + .018f, Fire + .09f);
            if (kick != lastKick)
            {
                barrel.localPosition = barrelHome - recoilAxis * (Recoil * kick);
                lastKick = kick;
            }

            bool flying = phase >= Fire && phase < Catch;
            bool docked = phase >= Catch && phase < Unload;
            if (flying)
            {
                float flight = Mathf.InverseLerp(Fire, Catch, phase);
                float height = 4f * ArcHeight * flight * (1f - flight);
                payload.position = Vector3.Lerp(muzzle.position, dock.position, flight) + transform.up * height;
            }
            else if (docked && (!wasDocked || trolleyMoved || hookMoved))
            {
                // Land directly on the eye, even on a client's first sample after impact.
                payload.position = dock.position;
            }
            wasDocked = docked;
            bool showingLoad = flying || docked;
            if (payload.gameObject.activeSelf != showingLoad)
                payload.gameObject.SetActive(showingLoad);
        }
    }
}
