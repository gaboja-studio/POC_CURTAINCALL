using System;
using UnityEngine;

namespace CurtainCall.Player
{
    /// <summary>
    /// 플레이어의 신체 상태(데미지로 잃은 부위). 모든 묘기·콘텐츠가 같은 상태를 본다. 규칙: Harness/Project/Decisions/body-damage.md
    /// - 네 팔다리를 모두 잃으면 사망(<see cref="AllLimbsLost"/>). 팔 둘·다리 둘만 잃으면 생존(2026-10-04 PM).
    /// - 디메리트는 콘텐츠마다 다르므로 여기서 정하지 않는다. 콘텐츠가 끼운 디메리트 정책이 이 상태를 읽어 적용한다.
    /// - 온라인: 부위 손실은 호스트가 판정하고 NetworkPlayer가 모두에게 공유해 각 화면에서 <see cref="SetLostParts"/>를 부른다.
    /// - 플레이어 프리팹에 붙이면 사용된다. 없으면 <see cref="PlayerControlContext.Condition"/>은 null이다.
    /// </summary>
    public sealed class PlayerCondition : MonoBehaviour
    {
        /// <summary>네 팔다리 전부.</summary>
        public const BodyPart AllLimbs = BodyPart.LeftArm | BodyPart.RightArm | BodyPart.LeftLeg | BodyPart.RightLeg;

        /// <summary>두 팔.</summary>
        public const BodyPart Arms = BodyPart.LeftArm | BodyPart.RightArm;

        /// <summary>두 다리.</summary>
        public const BodyPart Legs = BodyPart.LeftLeg | BodyPart.RightLeg;

        [Tooltip("잃은 부위. 플레이 중 여기서 바꿔도 테스트할 수 있다(온라인에서는 호스트 값이 덮어쓴다).")]
        [SerializeField] BodyPart lostParts;

        /// <summary>잃은 부위(여러 개 가능).</summary>
        public BodyPart LostParts => lostParts;

        /// <summary>잃은 부위 수.</summary>
        public int LostCount => CountBits(lostParts);

        /// <summary>잃은 팔 개수(0~2).</summary>
        public int LostArmCount => CountBits(lostParts & Arms);

        /// <summary>잃은 다리 개수(0~2).</summary>
        public int LostLegCount => CountBits(lostParts & Legs);

        /// <summary>네 팔다리를 모두 잃었는지(사망 조건).</summary>
        public bool AllLimbsLost => IsAllLimbsLost(lostParts);

        /// <summary>잃은 부위가 바뀌었을 때. 인자는 (이전, 지금).</summary>
        public event Action<BodyPart, BodyPart> Changed;

        /// <summary>그 부위가 남아 있는지. 여러 부위를 넘기면 모두 남아 있어야 true.</summary>
        public bool HasPart(BodyPart part) => (lostParts & part) == 0;

        /// <summary>그 부위를 잃게 한다.</summary>
        public void LosePart(BodyPart part) => SetLostParts(lostParts | part);

        /// <summary>모든 부위를 되돌린다. 언제 되돌릴지(재시작·다음 날 등)는 게임 규칙이 정한다.</summary>
        public void RestoreAll() => SetLostParts(BodyPart.None);

        /// <summary>잃은 부위를 통째로 정한다(온라인에서 받은 값 반영 등).</summary>
        public void SetLostParts(BodyPart parts)
        {
            if (lostParts == parts) return;
            var previous = lostParts;
            lostParts = parts;
            validatedParts = parts;
            Changed?.Invoke(previous, parts);
        }

        /// <summary>네 팔다리를 모두 잃은 상태인지.</summary>
        public static bool IsAllLimbsLost(BodyPart lost) => (lost & AllLimbs) == AllLimbs;

        /// <summary>
        /// 일반 절단 요청에서 실제로 잘릴 부위. 요청 부위가 있으면 그 부위, 이미 잃었으면 같은 종류의 남은 쪽, 둘 다 없으면 None.
        /// 여러 부위를 담아 요청하면 가장 낮은 비트 하나만 본다.
        /// </summary>
        public static BodyPart ResolveCut(BodyPart lost, BodyPart requested)
        {
            requested = LowestBit(requested & AllLimbs);
            if (requested == BodyPart.None) return BodyPart.None;
            if ((lost & requested) == 0) return requested;
            BodyPart kind = (requested & Arms) != 0 ? Arms : Legs;
            return LowestBit(kind & ~lost);
        }

        /// <summary>
        /// 가로 톱날 순서(2026-10-04 PM): 다리 → 남은 다리 → 팔. 다리를 다 잃었으면 팔을 자른다. 모두 잃었으면 None.
        /// 같은 종류가 둘 다 남았을 때 어느 쪽을 먼저 자를지는 미정이라 부르는 쪽이 <paramref name="leftFirst"/>로 정한다.
        /// </summary>
        public static BodyPart NextLegThenArmCut(BodyPart lost, bool leftFirst)
        {
            BodyPart kind = (lost & Legs) != Legs ? Legs : Arms;
            BodyPart remaining = kind & ~lost;
            if (remaining == BodyPart.None) return BodyPart.None;
            BodyPart left = remaining & (BodyPart.LeftArm | BodyPart.LeftLeg);
            BodyPart right = remaining & (BodyPart.RightArm | BodyPart.RightLeg);
            if (left == BodyPart.None) return right;
            if (right == BodyPart.None) return left;
            return leftFirst ? left : right;
        }

        static BodyPart LowestBit(BodyPart parts) => (BodyPart)((int)parts & -(int)parts);

        static int CountBits(BodyPart parts)
        {
            int count = 0;
            for (int bits = (int)parts; bits != 0; bits &= bits - 1) count++;
            return count;
        }

        BodyPart validatedParts;

        void Awake() => validatedParts = lostParts;

        // 플레이 중 인스펙터로 바꿔도 알림이 가게 한다(테스트용)
        void OnValidate()
        {
            if (!Application.isPlaying || validatedParts == lostParts) return;
            var previous = validatedParts;
            validatedParts = lostParts;
            Changed?.Invoke(previous, lostParts);
        }
    }
}
