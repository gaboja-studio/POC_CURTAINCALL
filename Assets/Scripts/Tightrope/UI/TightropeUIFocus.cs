using System;
using TMPro;
using UnityEngine;

namespace CurtainCall.Tightrope.UI
{
    /// <summary>UI의 입력 차단 요청만 제공한다. 게임 입력·타이머·네트워크 처리는 연결 단계가 맡는다.</summary>
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
            Changed?.Invoke(value);
        }
    }
}
