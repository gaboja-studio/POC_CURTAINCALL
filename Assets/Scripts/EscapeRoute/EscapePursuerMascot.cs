using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CurtainCall.EscapeRoute
{
    /// <summary>Humanoid appearance layered over the host's existing movement and combat.</summary>
    [DefaultExecutionOrder(1100)]
    public sealed class EscapePursuerMascot : MonoBehaviour
    {
        public EscapeUnderstageChase chase;
        public Animator animator;
        // Idle, walk, run, crouch, prepare, grab, throw, jump, land, hit.
        public AnimationClip[] clips;
        public Renderer[] legacyMeshes;
        public BoxCollider[] hitboxes;
        public Transform[] starts, ends;
        public GameObject[] stumps;
        public EscapePursuerBoss boss;
        public float walkCycleDistance = 2.4f, runCycleDistance = 3.2f;
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable[] motions;
        Vector3 rest;
        Quaternion restRotation;
        Transform head, hand, forearm, forearmTwist;
        Vector3 handRest, twistRest;

        void OnEnable()
        {
            if (!animator || !animator.avatar || !animator.avatar.isValid || !animator.isHuman) return;
            rest = animator.transform.localPosition;
            restRotation = animator.transform.localRotation;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.fireEvents = false;
            graph = PlayableGraph.Create("Escape pursuer humanoid");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, clips.Length);
            motions = new AnimationClipPlayable[clips.Length];
            for (int i = 0; i < clips.Length; i++)
            {
                motions[i] = AnimationClipPlayable.Create(graph, clips[i]);
                motions[i].SetApplyFootIK(i <= 3);
                motions[i].SetSpeed(0);
                graph.Connect(motions[i], 0, mixer, i);
            }
            AnimationPlayableOutput.Create(graph, "Mascot", animator).SetSourcePlayable(mixer);
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            forearm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            if (hand) handRest = hand.localPosition;
            if (forearm)
                foreach (Transform child in forearm)
                    if (child.name.Contains("twist")) { forearmTwist = child; twistRest = child.localPosition; break; }
            graph.Play();
        }

        void LateUpdate()
        {
            if (!graph.IsValid() || !chase) return;
            foreach (var mesh in legacyMeshes) if (mesh) mesh.enabled = false;
            var pose = chase.Appearance;
            for (int i = 0; i < motions.Length; i++) mixer.SetInputWeight(i, 0);
            if (pose.Action > 0) Sample(pose.Action, pose.Time, 1);
            else if (pose.Resting) Sample(0, Mathf.Repeat(Time.time * .3f, 1), 1);
            else
            {
                Sample(1, pose.Stride, (1 - pose.Duck) * (1 - pose.Run));
                Sample(2, pose.Stride, (1 - pose.Duck) * pose.Run);
                Sample(3, pose.Stride, pose.Duck);
            }
            // Twist bones are not driven by Humanoid retargeting. Restore them every frame.
            if (hand) hand.localPosition = handRest;
            if (forearmTwist) forearmTwist.localPosition = twistRest;
            graph.Evaluate(0);
            animator.transform.localPosition = rest;
            animator.transform.localRotation = restRotation;
            if (pose.Look && head)
            {
                Vector3 direction = pose.Aim - head.position;
                if (direction.sqrMagnitude > .01f)
                {
                    var turn = Quaternion.FromToRotation(animator.transform.forward, direction.normalized);
                    head.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.RotateTowards(Quaternion.identity, turn, 50), .7f) * head.rotation;
                }
            }
            if (pose.Stretch && hand && forearm)
            {
                Vector3 current = hand.position - forearm.position;
                Vector3 desired = pose.Hand - forearm.position;
                if (current.sqrMagnitude > .001f && desired.sqrMagnitude > .001f)
                {
                    forearm.rotation = Quaternion.FromToRotation(current, desired) * forearm.rotation;
                    float stretch = Mathf.Clamp(desired.magnitude / current.magnitude, .25f, 45f);
                    // Move both the wrist and its weighted twist joint. Nonuniform bone scale
                    // shears the rotated fingers and makes this asset's hand look flat.
                    if (forearmTwist) forearmTwist.localPosition *= stretch;
                    hand.position = pose.Hand;
                }
            }
            for (int i = 0; i < hitboxes.Length; i++)
            {
                if (!hitboxes[i] || !starts[i] || !ends[i]) continue;
                Vector3 delta = ends[i].position - starts[i].position;
                hitboxes[i].transform.SetPositionAndRotation((starts[i].position + ends[i].position) * .5f,
                    Quaternion.LookRotation(delta.sqrMagnitude > .001f ? delta : transform.forward));
                Vector3 worldSize = new Vector3(i < 4 ? .6f : .8f, i < 4 ? .6f : .8f, Mathf.Max(.45f, delta.magnitude));
                Vector3 scale = hitboxes[i].transform.lossyScale;
                hitboxes[i].size = new Vector3(worldSize.x / Mathf.Abs(scale.x), worldSize.y / Mathf.Abs(scale.y), worldSize.z / Mathf.Abs(scale.z));
                if (stumps != null && i < stumps.Length && stumps[i])
                {
                    stumps[i].SetActive(boss && boss.IsCut(i));
                    stumps[i].transform.SetPositionAndRotation(starts[i].position, Quaternion.FromToRotation(Vector3.up, delta.normalized));
                }
            }
        }

        void Sample(int index, float normalized, float weight)
        {
            index = Mathf.Clamp(index, 0, motions.Length - 1);
            motions[index].SetTime(Mathf.Clamp01(normalized) * clips[index].length);
            motions[index].SetDone(false);
            mixer.SetInputWeight(index, weight);
        }

        void OnDisable()
        {
            if (graph.IsValid()) graph.Destroy();
            if (hand) hand.localPosition = handRest;
            if (forearmTwist) forearmTwist.localPosition = twistRest;
        }
    }
}
