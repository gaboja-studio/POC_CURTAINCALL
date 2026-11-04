using CurtainCall.HumanCannon;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    // Both rigs are visual machinery. A client entering mid-cycle samples the current course time.
    public sealed class EscapeShaftMachine : MonoBehaviour
    {
        [SerializeField] Transform movingPart;
        [SerializeField] Transform linkage;
        [SerializeField] bool disposal;
        [SerializeField, Min(1f)] float dropDepth = 48f;
        [SerializeField] ParticleSystem receiverVent;
        [SerializeField] Transform freightPlatform, freightLoad, freightLines;
        [SerializeField, Min(.1f)] float freightRise = 6f;
        Vector3 platformHome, loadHome, linesHome, linesScale;
        float previousPhase = -1f;
        Vector3 home, linkageHome, linkageScale;
        Bounds travel;

        void OnEnable() => previousPhase = -1f;

        void Awake()
        {
            if (!movingPart || !linkage) { enabled = false; return; }
            home = movingPart.localPosition;
            linkageHome = linkage.localPosition;
            linkageScale = linkage.localScale;
            if (disposal)
            {
                // The fixed housing and receiver surround the entire drop, including the turning drum.
                travel = new Bounds(transform.position, Vector3.zero);
                foreach (var surface in GetComponentsInChildren<MeshRenderer>(true))
                    travel.Encapsulate(surface.bounds);
                travel.Expand(2f);
            }
            else travel = new Bounds(transform.position, new Vector3(10, 28, 14));
            if (freightPlatform && freightLoad && freightLines)
            {
                platformHome = freightPlatform.localPosition;
                loadHome = freightLoad.localPosition;
                linesHome = freightLines.localPosition;
                linesScale = freightLines.localScale;
                travel.Encapsulate(freightLoad.position);
                travel.Expand(3f);
            }
        }

        void LateUpdate()
        {
            if (!EscapeSceneryView.InRange(travel, 75f))
            {
                previousPhase = -1f;
                return;
            }
            var course = HumanCannonHazardCourse.Current;
            Sample(course != null ? course.Clock : Time.timeAsDouble);
        }

        void Sample(double seconds)
        {
            if (disposal)
            {
                float phase = (float)(seconds % 7.5) / 7.5f;
                // The receiving vat vents after the drum enters it. Joining a running cycle does not replay a burst.
                if (previousPhase >= 0f && previousPhase < .87f && phase >= .87f && receiverVent)
                    receiverVent.Emit(16);
                previousPhase = phase;
                float open = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.12f, .25f, phase))
                           - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.42f, .56f, phase));
                linkage.localPosition = linkageHome + Vector3.left * (open * 2.25f);
                float fall = Mathf.InverseLerp(.25f, .87f, phase);
                bool visible = phase >= .12f && phase < .87f;
                if (movingPart.gameObject.activeSelf != visible) movingPart.gameObject.SetActive(visible);
                movingPart.localPosition = home + Vector3.down * (fall * fall * dropDepth);
                movingPart.localRotation = Quaternion.Euler(0, 0, -22f + 155f * fall);
                return;
            }

            float lift = freightPlatform && freightLoad && freightLines
                ? RunFreight((float)(seconds % 12) / 12f)
                : 6f * Mathf.Sin((float)(seconds % 24) * Mathf.PI / 12f);
            movingPart.localPosition = home + Vector3.up * lift;
            // The cable mesh has unit height and a centered pivot. Its upper end stays on the crosshead.
            linkage.localPosition = linkageHome + Vector3.up * (lift * .5f);
            linkage.localScale = linkageScale - Vector3.up * lift;
        }

        float RunFreight(float phase)
        {
            float rise = freightRise * (Stroke(.20f, .49f, phase) - Stroke(.73f, .97f, phase));
            freightPlatform.localPosition = platformHome + Vector3.up * rise;
            freightLines.localPosition = linesHome + Vector3.up * (rise * .5f);
            freightLines.localScale = linesScale - Vector3.up * rise;

            // The two covered landings are stacked. Only an empty platform comes back down.
            bool carrying = phase < .72f;
            if (freightLoad.gameObject.activeSelf != carrying) freightLoad.gameObject.SetActive(carrying);
            if (carrying)
            {
                float aboard = Stroke(0, .18f, phase) - Stroke(.51f, .71f, phase);
                var position = loadHome + Vector3.up * rise;
                position.z = Mathf.Lerp(loadHome.z, platformHome.z, aboard);
                freightLoad.localPosition = position;
            }
            return .5f * freightRise - rise;
        }

        static float Stroke(float start, float end, float phase)
            => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(start, end, phase));
    }
}
