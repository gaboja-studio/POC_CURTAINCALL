using UnityEngine.InputSystem;

namespace CurtainCall.Player
{
    /// <summary>
    /// 반대 방향 키 한 쌍(W/S, A/D, Q/E)을 마지막에 누른 쪽 방향으로 읽는다.
    /// 다른 쪽을 누르고 있는 중에 새 키를 누르면, 먼저 누른 키는 뗀 것으로 본다. 그 키를 다시 누르기 전까지는 방향을 내지 않는다.
    /// 1D Axis 합성의 기본 동작(동시 입력 = 0, 정지)을 대신해 빠른 방향 전환을 허용한다.
    /// </summary>
    public sealed class OppositeKeyAxis
    {
        bool negativeHeld, positiveHeld, negativeReleased, positiveReleased;

        /// <summary>이번 프레임의 키 상태를 넣고 -1/0/1을 받는다.</summary>
        public int Update(bool negative, bool positive)
        {
            bool negativeDown = negative && !negativeHeld, positiveDown = positive && !positiveHeld;
            if (!negative) negativeReleased = false;
            if (!positive) positiveReleased = false;
            if (negativeDown && !positiveDown && positive) positiveReleased = true;
            if (positiveDown && !negativeDown && negative) negativeReleased = true;
            negativeHeld = negative; positiveHeld = positive;
            return (positive && !positiveReleased ? 1 : 0) - (negative && !negativeReleased ? 1 : 0);
        }

        public void Reset() => negativeHeld = positiveHeld = negativeReleased = positiveReleased = false;

        /// <summary>1D Axis 합성의 "negative"/"positive" 부분 키가 눌렸는지. 키 재설정 뒤에도 부분 이름으로 찾는다.</summary>
        public static bool PartPressed(InputAction action, string part)
        {
            var controls = action.controls;
            for (int i = 0; i < controls.Count; i++)
            {
                int index = action.GetBindingIndexForControl(controls[i]);
                if (index < 0) continue;
                var binding = action.bindings[index];
                if (binding.isPartOfComposite && binding.name == part && controls[i].IsActuated(0.5f)) return true;
            }
            return false;
        }

        /// <summary>합성 키 쌍 우선. 둘 다 안 눌렸으면 합성이 아닌 바인딩(스틱 등) 값을 그대로 쓴다.</summary>
        public float Read(InputAction action)
        {
            bool negative = PartPressed(action, "negative"), positive = PartPressed(action, "positive");
            int keys = Update(negative, positive);
            return negative || positive ? keys : action.ReadValue<float>();
        }
    }
}
