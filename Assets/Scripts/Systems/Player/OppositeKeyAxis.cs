using UnityEngine.InputSystem;

namespace CurtainCall.Player
{
    /// <summary>
    /// 반대 방향 키 한 쌍(W/S, A/D, Q/E)을 마지막에 누른 쪽 방향으로 읽는다.
    /// 둘 다 누르고 있으면 나중에 누른 키 방향, 나중 키를 떼면 아직 누르고 있는 먼저 키 방향으로 돌아간다.
    /// 같은 프레임에 둘 다 누르면 어느 한쪽을 떼거나 다시 누를 때까지 0이다.
    /// 1D Axis 합성의 기본 동작(동시 입력 = 0, 정지)을 대신해 빠른 방향 전환을 허용한다.
    /// </summary>
    public sealed class OppositeKeyAxis
    {
        bool negativeHeld, positiveHeld;
        int lastPressed; // 둘 다 눌렸을 때 쓸 방향: 마지막에 누른 쪽(-1/1), 동시에 누르면 0

        /// <summary>이번 프레임의 키 상태를 넣고 -1/0/1을 받는다.</summary>
        public int Update(bool negative, bool positive)
        {
            bool negativeDown = negative && !negativeHeld, positiveDown = positive && !positiveHeld;
            if (negativeDown || positiveDown) lastPressed = negativeDown == positiveDown ? 0 : (positiveDown ? 1 : -1);
            negativeHeld = negative; positiveHeld = positive;
            if (negative && positive) return lastPressed;
            return positive ? 1 : negative ? -1 : 0;
        }

        public void Reset()
        {
            negativeHeld = positiveHeld = false;
            lastPressed = 0;
        }

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
