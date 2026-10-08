using System.Collections.Generic;
using CurtainCall.Player;
using CurtainCall.Tightrope.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CurtainCall.Tightrope.Prototype
{
    /// <summary>UI 차단 뒤 모든 게임·개발 입력을 놓은 다음 프레임에 재무장한다. 물리는 멈추지 않는다.</summary>
    public static class TightropeInputGate
    {
        static readonly HashSet<PlayerInputReader> readers = new();
        static readonly Key[] keys = { Key.W, Key.S, Key.A, Key.D, Key.Space, Key.Q, Key.E, Key.F,
            Key.P, Key.O, Key.B, Key.R, Key.M, Key.K, Key.L };
        static bool rearming;
        static int checkedFrame = -1;
        static int blockedFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            TightropeUIFocus.Changed -= OnFocusChanged;
            TightropeUIFocus.Changed += OnFocusChanged;
            readers.Clear();
            rearming = false;
            checkedFrame = blockedFrame = -1;
        }

        public static bool IsBlocked
        {
            get
            {
                if (TightropeUIFocus.IsBlocked) return true;
                if (!rearming) return blockedFrame == Time.frameCount;
                if (checkedFrame != Time.frameCount)
                {
                    checkedFrame = Time.frameCount;
                    blockedFrame = Time.frameCount;
                    bool released = true;
                    foreach (var reader in readers)
                        if (reader != null && reader.isActiveAndEnabled && !reader.ControlsReleased)
                            released = false;
                    var keyboard = Keyboard.current;
                    if (keyboard != null)
                        foreach (var key in keys) released &= !keyboard[key].isPressed;
                    if (released) rearming = false;
                }
                return true;
            }
        }

        public static void Register(PlayerInputReader reader)
        {
            readers.Add(reader);
            RequireRelease();
        }

        public static void Unregister(PlayerInputReader reader) => readers.Remove(reader);

        public static void RequireRelease()
        {
            rearming = true;
            checkedFrame = -1;
            blockedFrame = Time.frameCount;
            foreach (var reader in readers)
                if (reader != null) reader.ClearPendingInput();
        }

        static void OnFocusChanged(bool blocked) => RequireRelease();
    }
}
