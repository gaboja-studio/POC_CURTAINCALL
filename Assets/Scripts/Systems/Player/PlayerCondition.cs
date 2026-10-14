using System;
using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.Player
{
    /// <summary>
    /// 플레이어의 신체 상태(데미지로 잃은 부위). 모든 묘기·콘텐츠가 같은 상태를 본다. 규칙: Harness/Project/Decisions/injury-rules.md
    /// - 공통 절단 순서는 왼다리 → 오른다리 → 왼팔 → 오른팔(<see cref="NextFixedCut"/>), 신체 복구는 역순(<see cref="NextRestore"/>).
    ///   잃은 팔다리 수 0 온전한 몸 / 1·2 부상 / 3 외팔 상태(<see cref="OneArmParts"/>) / 4 사망.
    /// - 네 팔다리를 모두 잃으면 사망(<see cref="AllLimbsLost"/>).
    /// - <see cref="ResolveCut"/>·<see cref="NextLegThenArmCut"/>는 외줄 톱날 전용(닿은 쪽·위치 기준). 4단계에서 공통 순서로 바꾼다.
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

        /// <summary>외팔 상태(잃은 팔다리 3, 오른팔만 남음)에서 잃은 부위. 부활·백스테이지에서 잡혔을 때의 몸. injury-rules.md #2</summary>
        public const BodyPart OneArmParts = BodyPart.LeftLeg | BodyPart.RightLeg | BodyPart.LeftArm;

        /// <summary>외팔 상태의 잃은 팔다리 수.</summary>
        public const int OneArmLostCount = 3;

        /// <summary>
        /// 공통 절단 순서(2026-10-13 부상 기획): 어디를 맞든 왼다리 → 오른다리 → 왼팔 → 오른팔. 신체 복구는 이 역순.
        /// 이 순서를 지키면 잃은 팔다리 수(<see cref="LostCount"/>)만으로 몸 상태가 정해진다.
        /// </summary>
        static readonly BodyPart[] cutOrder = { BodyPart.LeftLeg, BodyPart.RightLeg, BodyPart.LeftArm, BodyPart.RightArm };

        /// <summary>공통 절단 순서(왼다리 → 오른다리 → 왼팔 → 오른팔).</summary>
        public static IReadOnlyList<BodyPart> CutOrder => cutOrder;

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

        /// <summary>남은 팔 개수(0~2). 기물 소지 칸 수 계산에 쓴다.</summary>
        public int ArmCount => 2 - LostArmCount;

        /// <summary>네 팔다리를 모두 잃었는지(사망 조건).</summary>
        public bool AllLimbsLost => IsAllLimbsLost(lostParts);

        /// <summary>외팔 상태(잃은 팔다리 3)인지.</summary>
        public bool IsOneArm => LostCount == OneArmLostCount;

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

        /// <summary>잃은 팔다리 수(0~4).</summary>
        public static int CountLimbs(BodyPart lost) => CountBits(lost & AllLimbs);

        /// <summary>공통 절단 순서에서 다음에 잘릴 부위. 모두 잃었으면 None. 순서를 어긴 손실(외줄 톱날 등)이 있어도 남은 부위 중 순서가 앞선 것.</summary>
        public static BodyPart NextFixedCut(BodyPart lost)
        {
            foreach (var part in cutOrder)
                if ((lost & part) == 0) return part;
            return BodyPart.None;
        }

        /// <summary>신체 복구에서 다음에 되찾을 부위(절단 역순: 왼팔 → 오른다리 → 왼다리, 오른팔은 사망 상태에서만). 잃은 부위가 없으면 None.</summary>
        public static BodyPart NextRestore(BodyPart lost)
        {
            for (int i = cutOrder.Length - 1; i >= 0; i--)
                if ((lost & cutOrder[i]) != 0) return cutOrder[i];
            return BodyPart.None;
        }

        /// <summary>잃은 팔다리 수(0~4)에 해당하는 공통 순서의 몸 상태(앞에서부터 그 수만큼 잃음).</summary>
        public static BodyPart PartsForLostCount(int count)
        {
            BodyPart parts = BodyPart.None;
            for (int i = 0; i < cutOrder.Length && i < count; i++) parts |= cutOrder[i];
            return parts;
        }

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
