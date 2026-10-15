using System;
using CurtainCall.Player;
using TMPro;
using UnityEngine;

namespace CurtainCall.Tightrope.UI
{
    /// <summary>
    /// 외줄 UI의 입력 차단 요청만 제공한다. 실제 막기는 공용 <see cref="InputGate"/>에 요청만 보낸다.
    /// 게임 입력·타이머·네트워크 처리는 연결 단계가 맡는다. 새 UI는 공용 <see cref="UIInputBlocker"/>를 쓴다.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class TightropeUIFocus : MonoBehaviour
    {
        [SerializeField] TMP_InputField[] fields = Array.Empty<TMP_InputField>();
        bool modal;
        public static bool IsBlocked { get; private set; }
        public static event Action<bool> Changed;
        static TightropeUIFocus owner;

        public void SetFields(params TMP_InputField[] inputs) => fields = inputs ?? Array.Empty<TMP_InputField>();
        public void SetModal(bool open) { modal = open; Refresh(); }
        void OnEnable() { owner = this; Refresh(); }
        void Update() => Refresh();

        void Refresh()
        {
            if (owner != this) return;
            bool blocked = modal;
            foreach (var field in fields)
                blocked |= field != null && field.isActiveAndEnabled && field.isFocused;
            SetBlocked(blocked);
        }

        void OnDisable()
        {
            if (owner != this) return;
            owner = null;
            SetBlocked(false);
        }

        static void SetBlocked(bool value)
        {
            if (IsBlocked == value) return;
            IsBlocked = value;
            InputGate.SetBlocking(typeof(TightropeUIFocus), value);
            Changed?.Invoke(value);
        }
    }
}
