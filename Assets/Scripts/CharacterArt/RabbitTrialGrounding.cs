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

        void LateUpdate() => CorrectPose();

        public void CorrectPose()
        {
            // 물리로 넘어간 뒤에는 보정용 부모를 움직이지 않는다.
            if (Model == null || VisualRoot == null || Model.Animator == null || !Model.Animator.enabled) return;
            VisualRoot.localPosition = RestLocalPosition;
            Lift = 0;
            SampleCount = 0;
            var animator = Model.Animator;
            if (!GroundedMotion(animator.GetCurrentAnimatorStateInfo(0))) return;
            if (animator.IsInTransition(0) && !GroundedMotion(animator.GetNextAnimatorStateInfo(0))) return;
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
            }
            if (bindposes.Length != matrices.Length) return;
            for (int i = 0; i < matrices.Length; i++)
                matrices[i] = bones[i].localToWorldMatrix * bindposes[i];
            float lowest = float.PositiveInfinity;
            foreach (var point in Stages[stage].Points)
            {
                var w = point.Weight;
                var p = matrices[w.boneIndex0].MultiplyPoint3x4(point.Position) * w.weight0;
                if (w.weight1 > 0) p += matrices[w.boneIndex1].MultiplyPoint3x4(point.Position) * w.weight1;
                if (w.weight2 > 0) p += matrices[w.boneIndex2].MultiplyPoint3x4(point.Position) * w.weight2;
                if (w.weight3 > 0) p += matrices[w.boneIndex3].MultiplyPoint3x4(point.Position) * w.weight3;
                lowest = Mathf.Min(lowest, p.y);
            }
            SampleCount = Stages[stage].Points.Length;
            if (!float.IsFinite(lowest)) return;
            // Authored legless recoveries have no foot chain to establish their floor height.
            // Settle their remaining support down as well as up; ordinary locomotion keeps its old lift-only rule.
            bool settle = CircusGrounded(animator.GetCurrentAnimatorStateInfo(0), "FlightLand")
                || CircusGrounded(animator.GetCurrentAnimatorStateInfo(0), "RopePull")
                || CircusGrounded(animator.GetCurrentAnimatorStateInfo(0), "TrampolineCarry");
            Lift = Mathf.Clamp(transform.position.y + Clearance - lowest, settle ? -MaxLift : 0, MaxLift);
            VisualRoot.position += Vector3.up * Lift;
        }

        static bool GroundedMotion(AnimatorStateInfo state) =>
            state.IsName("Idle") || state.IsName("Walk") || state.IsName("Run") ||
            state.IsName("Crouch") || state.IsName("CrouchWalk") || state.IsName("Crawl") ||
            CircusGrounded(state, "FlightLand") || CircusGrounded(state, "RopePull") || CircusGrounded(state, "TrampolineCarry");

        static bool CircusGrounded(AnimatorStateInfo state, string name) => state.IsName(name)
            || state.IsName(name + "_Cut1") || state.IsName(name + "_Cut2") || state.IsName(name + "_Cut3");
    }
}
