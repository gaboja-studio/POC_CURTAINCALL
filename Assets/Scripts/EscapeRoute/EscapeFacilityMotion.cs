using CurtainCall.HumanCannon;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    // All authored curves move decorative children only. Game devices remain authoritative.
    public sealed class EscapeFacilityMotion : MonoBehaviour
    {
        [SerializeField] AnimationClip motion;
        [SerializeField] EscapeCooperativeSpan bridge;
        [SerializeField] HumanCannonHazardCue flame;
        [SerializeField] EscapeExitSaw exit;
        [SerializeField] ParticleSystem brakeSparks;
        [SerializeField] ParticleSystem pressureRelease;

        Bounds installationBounds;
        float previous = -1f;
        float fanSpeed = 1f;
        float turns;
        bool sawBridgeMoving;
        bool visibleLastFrame;

        void OnEnable()
        {
            previous = -1f;
            sawBridgeMoving = false;
            visibleLastFrame = false;
        }

        void Awake()
        {
            // The fixed frame, rails and pipe flanges enclose each clip's full stroke.
            installationBounds = new Bounds(transform.position, Vector3.zero);
            foreach (var part in GetComponentsInChildren<MeshRenderer>())
                installationBounds.Encapsulate(part.bounds);
            installationBounds.Expand(1f);
        }

        void LateUpdate()
        {
            if (!motion || motion.length <= 0f) return;
            if (!EscapeSceneryView.InRange(installationBounds, 80f))
            {
                sawBridgeMoving = false;
                visibleLastFrame = false;
                return;
            }
            var course = HumanCannonHazardCourse.Current;
            double clock = course != null ? course.Clock : Time.timeAsDouble;
            PresentVisible(course, clock, Time.deltaTime);
        }

        void PresentVisible(HumanCannonHazardCourse course, double clock, float deltaTime)
        {
            float phase;
            if (bridge)
            {
                phase = bridge.DeploymentFraction;
                if (phase > 0 && phase < 1) sawBridgeMoving = true;
                if (phase >= 1 && sawBridgeMoving && brakeSparks) brakeSparks.Emit(12);
                if (phase <= 0 || phase >= 1) sawBridgeMoving = false;
            }
            else if (flame)
            {
                double t = course != null ? flame.Cycle(course.Elapsed) : -1;
                phase = course == null || !course.IsSynchronized || t < 0 ? 0 :
                    Mathf.SmoothStep(0, 1, Mathf.Clamp01((float)t / flame.warningSeconds)) *
                    (1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(((float)t - flame.warningSeconds - flame.activeSeconds) / .55f)));
                if (visibleLastFrame && previous >= .99f && phase < .99f && t >= flame.warningSeconds + flame.activeSeconds && pressureRelease)
                    pressureRelease.Emit(12);
            }
            else if (exit)
            {
                float targetSpeed = exit.DoorOpen ? 0f : 1f;
                fanSpeed = visibleLastFrame
                    ? Mathf.MoveTowards(fanSpeed, targetSpeed, deltaTime / 2.5f)
                    : targetSpeed;
                turns = Mathf.Repeat(turns + fanSpeed * deltaTime * 1.2f, 1);
                phase = turns;
            }
            else phase = (float)((clock % motion.length) / motion.length);

            visibleLastFrame = true;
            if (phase == previous) return;
            previous = phase;
            motion.SampleAnimation(gameObject, phase * motion.length);
        }
    }
}
