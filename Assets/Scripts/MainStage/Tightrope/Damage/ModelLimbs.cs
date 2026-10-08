using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.Damage
{
    /// <summary>
    /// 모델의 팔·다리 위치 표시. 잘릴 때 이 오브젝트(와 그 아래 전부)가 몸에서 떨어진다.
    /// 모델 프리팹 루트에 붙이고 네 칸을 채운다. 비운 칸은 Animator가 휴머노이드면 그 뼈(위팔·허벅지)를 쓴다.
    /// 그래서 휴머노이드 모델은 이 컴포넌트 없이도 동작하고, 휴머노이드가 아닌 모델(BlockDoll 등)은 칸을 채워야 한다.
    /// 모델 종류: 부위마다 렌더러가 따로 있으면 그대로 떼어 내고, 스킨 메시(한 덩어리)면 그 뼈에 묶인 면만 잘라 새 조각으로 만든다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ModelLimbs : MonoBehaviour
    {
        [Tooltip("왼팔 시작 오브젝트(어깨 쪽). 비우면 휴머노이드 뼈(LeftUpperArm).")]
        [SerializeField] Transform leftArm;

        [Tooltip("오른팔 시작 오브젝트(어깨 쪽). 비우면 휴머노이드 뼈(RightUpperArm).")]
        [SerializeField] Transform rightArm;

        [Tooltip("왼다리 시작 오브젝트(엉덩이 쪽). 비우면 휴머노이드 뼈(LeftUpperLeg).")]
        [SerializeField] Transform leftLeg;

        [Tooltip("오른다리 시작 오브젝트(엉덩이 쪽). 비우면 휴머노이드 뼈(RightUpperLeg).")]
        [SerializeField] Transform rightLeg;

        /// <summary>이 컴포넌트에 직접 지정한 부위. 없으면 null.</summary>
        public Transform GetAssigned(BodyPart part) => part switch
        {
            BodyPart.LeftArm => leftArm,
            BodyPart.RightArm => rightArm,
            BodyPart.LeftLeg => leftLeg,
            BodyPart.RightLeg => rightLeg,
            _ => null,
        };

        /// <summary>
        /// 모델에서 그 부위의 시작 오브젝트를 찾는다. 직접 지정 → 휴머노이드 뼈 순서. 못 찾으면 null.
        /// </summary>
        public static Transform Find(GameObject model, BodyPart part)
        {
            if (model == null) return null;

            var limbs = model.GetComponentInChildren<ModelLimbs>(true);
            if (limbs != null)
            {
                var assigned = limbs.GetAssigned(part);
                if (assigned != null) return assigned;
            }

            var animator = model.GetComponentInChildren<Animator>(true);
            if (animator == null || !animator.isHuman) return null;
            return part switch
            {
                BodyPart.LeftArm => animator.GetBoneTransform(HumanBodyBones.LeftUpperArm),
                BodyPart.RightArm => animator.GetBoneTransform(HumanBodyBones.RightUpperArm),
                BodyPart.LeftLeg => animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg),
                BodyPart.RightLeg => animator.GetBoneTransform(HumanBodyBones.RightUpperLeg),
                _ => null,
            };
        }
    }
}
