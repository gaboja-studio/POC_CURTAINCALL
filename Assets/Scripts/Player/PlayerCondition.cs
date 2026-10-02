using System;
using UnityEngine;

namespace CurtainCall.Player
{
    /// <summary>
    /// 플레이어의 신체 상태(데미지로 잃은 부위). 모든 묘기·콘텐츠가 같은 상태를 본다.
    /// 지금은 준비만 되어 있다(2026-10-02 PM): 데미지를 주는 기능, 온라인 공유, 모델 표현(잘린 부위)은 아직 없다.
    /// - 디메리트는 콘텐츠마다 다르므로 여기서 정하지 않는다. 각 콘텐츠의 조작 규칙(<see cref="PlayerControlScheme"/>)이
    ///   <see cref="PlayerControlContext.Condition"/>을 읽어 자기 규칙대로 적용한다(예: 외줄에서 다리를 잃으면 옆줄 이동 불가).
    /// - 온라인: 부위 손실은 호스트가 판정하고, 추락 상태처럼 NetworkPlayer가 모두에게 공유하도록 붙인다(그때 <see cref="SetLostParts"/>를 부른다).
    /// - 플레이어 프리팹에 붙이면 사용된다. 없으면 <see cref="PlayerControlContext.Condition"/>은 null이다.
    /// </summary>
    public sealed class PlayerCondition : MonoBehaviour
    {
        [Tooltip("잃은 부위. 데미지 기능 전에는 여기서 테스트한다.")]
        [SerializeField] BodyPart lostParts;

        /// <summary>잃은 부위(여러 개 가능).</summary>
        public BodyPart LostParts => lostParts;

        /// <summary>잃은 부위 수.</summary>
        public int LostCount
        {
            get
            {
                int count = 0;
                for (int bits = (int)lostParts; bits != 0; bits &= bits - 1) count++;
                return count;
            }
        }

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
