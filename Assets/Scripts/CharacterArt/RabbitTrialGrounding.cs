using System;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.CharacterArt
{
    /// <summary>토끼 표면의 지상 포즈 침투 보정. 원점 평면 기준이며 지형 IK나 이동 판정은 수행하지 않는다.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class RabbitTrialGrounding : MonoBehaviour
    {
        [Serializable]
        public struct SupportPoint
        {
            public Vector3 Position;
            public BoneWeight Weight;
        }

        [Serializable]
        public sealed class SupportSet
        {
            public SupportPoint[] Points;
        }

        public HumanoidModel Model;
        public Transform VisualRoot;
        public Vector3 RestLocalPosition;
        public SupportSet[] Stages;
        public float Clearance = .002f;
        public float MaxLift = .25f;
        public float Lift { get; private set; }
        public int SampleCount { get; private set; }

        Matrix4x4[] matrices;
        Matrix4x4[] bindposes;
        Mesh sourceMesh;
        SkinnedMeshRenderer sourceSkin;
        Transform[] bones;
        bool[] pelvisBones, footBones, leftArmBones, rightArmBones;

        void LateUpdate() => CorrectPose();

        public void CorrectPose()
        {
            // 물리로 넘어간 뒤에는 보정용 부모를 움직이지 않는다.
            if (Model == null || VisualRoot == null || Model.Animator == null || !Model.Animator.enabled) return;
            VisualRoot.localPosition = RestLocalPosition;
            Lift = 0;
            SampleCount = 0;
            var animator = Model.Animator;
            var current = animator.GetCurrentAnimatorStateInfo(0);
            var next = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : current;
            float blend = animator.IsInTransition(0) ? Mathf.Clamp01(animator.GetAnimatorTransitionInfo(0).normalizedTime) : 0;
            float grounded = Mathf.Lerp(GroundedMotion(current) ? 1 : 0, GroundedMotion(next) ? 1 : 0, blend);
            if (grounded <= 0) return;
            int stage = Model.Stage;
            if (Stages == null || stage >= Stages.Length || Stages[stage]?.Points == null) return;
            var skin = Model.Original;
            if (skin == null || skin.sharedMesh == null) return;
            if (sourceSkin != skin || sourceMesh != skin.sharedMesh || matrices == null)
            {
                sourceSkin = skin;
                sourceMesh = skin.sharedMesh;
                bones = skin.bones;
                bindposes = sourceMesh.bindposes;
                matrices = new Matrix4x4[bones.Length];
                pelvisBones = new bool[bones.Length]; footBones = new bool[bones.Length];
                leftArmBones = new bool[bones.Length]; rightArmBones = new bool[bones.Length];
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                var leftThigh = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                var rightThigh = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                var foot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                var toes = animator.GetBoneTransform(HumanBodyBones.RightToes);
                var leftArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                var rightArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                for (int i = 0; i < bones.Length; i++)
                {
                    pelvisBones[i] = bones[i] == hips || bones[i] == leftThigh || bones[i] == rightThigh;
                    footBones[i] = bones[i] == foot || bones[i] == toes;
                    leftArmBones[i] = leftArm != null && bones[i].IsChildOf(leftArm);
                    rightArmBones[i] = rightArm != null && bones[i].IsChildOf(rightArm);
                }
            }
            if (bindposes.Length != matrices.Length) return;
            for (int i = 0; i < matrices.Length; i++)
                matrices[i] = bones[i].localToWorldMatrix * bindposes[i];
            float lowest = float.PositiveInfinity;
            float support = float.PositiveInfinity;
            foreach (var point in Stages[stage].Points)
            {
                var w = point.Weight;
                var p = SkinPoint(point);
                lowest = Mathf.Min(lowest, p.y);
                if (stage > 0 && SupportWeight(w, stage >= 2 ? pelvisBones : footBones) > .5f)
                    support = Mathf.Min(support, p.y);
            }
            SampleCount = Stages[stage].Points.Length;
            if (!float.IsFinite(lowest)) return;
            float floor = transform.position.y + Clearance;
            float circus = Mathf.Lerp(CircusSupport(current) ? 1 : 0, CircusSupport(next) ? 1 : 0, blend);
            // A touching fingertip is not proof that a legless torso is supported.
            // Settle the pelvis (or surviving sole), then keep the arms above the floor.
            // humanScale bounds the correction for avatars with a deep skirt; the old
            // .25m lift limit was too small for their seated pose.
            float limit = Mathf.Max(MaxLift, animator.humanScale);
            float ordinary = Mathf.Clamp(floor - lowest, 0, MaxLift) * grounded;
            float seated = Mathf.Clamp(floor - (float.IsFinite(support) ? support : lowest), -limit, limit);
            Lift = Mathf.Lerp(ordinary, seated * grounded, circus);
            VisualRoot.position += Vector3.up * Lift;
            if (stage >= 2 && circus > 0)
            {
                KeepHandAboveFloor(false, floor, circus);
                if (stage == 2) KeepHandAboveFloor(true, floor, circus);
            }
        }

        static float SupportWeight(BoneWeight w, bool[] selected) =>
            (selected[w.boneIndex0] ? w.weight0 : 0) + (selected[w.boneIndex1] ? w.weight1 : 0)
            + (selected[w.boneIndex2] ? w.weight2 : 0) + (selected[w.boneIndex3] ? w.weight3 : 0);

        Vector3 SkinPoint(SupportPoint point)
        {
            var w = point.Weight;
            var p = matrices[w.boneIndex0].MultiplyPoint3x4(point.Position) * w.weight0;
            if (w.weight1 > 0) p += matrices[w.boneIndex1].MultiplyPoint3x4(point.Position) * w.weight1;
            if (w.weight2 > 0) p += matrices[w.boneIndex2].MultiplyPoint3x4(point.Position) * w.weight2;
            if (w.weight3 > 0) p += matrices[w.boneIndex3].MultiplyPoint3x4(point.Position) * w.weight3;
            return p;
        }

        void KeepHandAboveFloor(bool left, float floor, float weight)
        {
            var animator = Model.Animator;
            var selected = left ? leftArmBones : rightArmBones;
            // Use actual remaining skin, including thick gloves/fingertips, rather
            // than a wrist-height guess. Never lift the entire torso for a hand.
            for (int iteration = 0; iteration < 3; iteration++)
            {
                for (int i = 0; i < matrices.Length; i++) matrices[i] = bones[i].localToWorldMatrix * bindposes[i];
                float bottom = float.PositiveInfinity;
                foreach (var point in Stages[Model.Stage].Points)
                    if (SupportWeight(point.Weight, selected) > .5f) bottom = Mathf.Min(bottom, SkinPoint(point).y);
                if (!float.IsFinite(bottom) || bottom >= floor - .001f) return;
                Vector3 target = HumanoidPlayerPresentation.Palm(animator, left) + Vector3.up * ((floor - bottom) * weight);
                HumanoidPlayerPresentation.AlignArm(animator, left, target);
            }
        }

        static bool CircusSupport(AnimatorStateInfo state) => CircusGrounded(state, "FlightLand")
            || CircusGrounded(state, "RopePull") || CircusGrounded(state, "TrampolineCarry") || ItemGrounded(state);

        static bool GroundedMotion(AnimatorStateInfo state) =>
            state.IsName("Idle") || state.IsName("Walk") || state.IsName("Run") ||
            state.IsName("Crouch") || state.IsName("CrouchWalk") || state.IsName("Crawl") ||
            CircusGrounded(state, "FlightLand") || CircusGrounded(state, "RopePull") || CircusGrounded(state, "TrampolineCarry") || ItemGrounded(state);

        static bool ItemGrounded(AnimatorStateInfo state)
        {
            foreach (string motion in CurtainCall.ItemArt.ItemVisualAssets.Motions)
                if (CircusGrounded(state,motion)) return true;
            return false;
        }

        static bool CircusGrounded(AnimatorStateInfo state, string name) => state.IsName(name)
            || state.IsName(name + "_Cut1") || state.IsName(name + "_Cut2") || state.IsName(name + "_Cut3");
    }
}
