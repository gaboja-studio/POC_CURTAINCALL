using CurtainCall.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CurtainCall.Tightrope.Prototype
{
    /// <summary>
    /// 공용 입력 막기(<see cref="InputGate"/>)로 넘기는 외줄 쪽 창구. 외줄 코드가 지금처럼 부를 수 있게 남겨 둔다(2026-10-14, Task-037).
    /// 새 코드는 <see cref="InputGate"/>를 직접 쓴다. 외줄 개발 키(P·O·B·R·M·K·L)도 놓을 때까지 다시 받지 않게 등록한다.
    /// </summary>
    public static class TightropeInputGate
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] // 공용 초기화(SubsystemRegistration) 다음
        static void RegisterDevKeys() =>
            InputGate.AddReleaseKeys(Key.P, Key.O, Key.B, Key.R, Key.M, Key.K, Key.L);

        public static bool IsBlocked => InputGate.IsBlocked;

        public static void RequireRelease() => InputGate.RequireRelease();
    }
}
