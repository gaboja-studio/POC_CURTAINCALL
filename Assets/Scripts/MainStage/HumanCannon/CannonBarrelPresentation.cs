using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>Samples Blender-authored rigid barrel clips. Never moves gameplay anchors or colliders.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class CannonBarrelPresentation : MonoBehaviour
    {
        [SerializeField] GameObject animationRoot;
        [SerializeField] Transform barrelPivot;
        [SerializeField] AnimationClip active;
        [SerializeField] AnimationClip inactive;
        [SerializeField] AnimationClip fireDisable;
        [SerializeField] AnimationClip recover;
        HumanCannonStation station;
        CannonBarrelPhase previous;
        bool initialized;
        float blendRemaining;
        Vector3 blendPosition;
        Quaternion blendRotation;
        public bool Preview { get; set; }
        public CannonBarrelPhase Phase { get; private set; }
        public Transform BarrelPivot => barrelPivot;

        void OnEnable() { station = GetComponentInParent<HumanCannonStation>(); initialized = false; }
        void LateUpdate()
        {
            if (Preview) return;
            float progress = 0;
            var phase = station != null
                ? station.SampleBarrel(out progress) : CannonBarrelPhase.Active;
            if (initialized && phase != previous && (phase == CannonBarrelPhase.Inactive || phase == CannonBarrelPhase.Recover))
            {
                blendPosition = barrelPivot.localPosition;
                blendRotation = barrelPivot.localRotation;
                blendRemaining = .16f;
            }
            Sample(phase, progress);
            if (blendRemaining > 0)
            {
                blendRemaining = Mathf.Max(0, blendRemaining - Time.deltaTime);
                float t = 1 - blendRemaining / .16f;
                t = t * t * (3 - 2 * t);
                barrelPivot.localPosition = Vector3.Lerp(blendPosition, barrelPivot.localPosition, t);
                barrelPivot.localRotation = Quaternion.Slerp(blendRotation, barrelPivot.localRotation, t);
            }
            previous = phase; initialized = true;
        }

        public void Sample(CannonBarrelPhase phase, float progress)
        {
            Phase = phase;
            var clip = phase switch
            {
                CannonBarrelPhase.Inactive => inactive,
                CannonBarrelPhase.FireDisable => fireDisable,
                CannonBarrelPhase.Recover => recover,
                _ => active
            };
            if (animationRoot != null && clip != null)
                clip.SampleAnimation(animationRoot, Mathf.Clamp01(progress) * clip.length);
        }
    }
}
