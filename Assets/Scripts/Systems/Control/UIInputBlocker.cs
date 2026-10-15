using System;
using TMPro;
using UnityEngine;

namespace CurtainCall.Player
{
    /// <summary>
    /// UI가 열려 있는 동안 게임 입력을 막는다(<see cref="InputGate"/>). 어느 공간 UI든 패널에 붙이면 된다.
    /// 막는 때: 이 컴포넌트가 켜져 있고 (<see cref="BlockWhileActive"/>가 켜져 있거나, <see cref="SetOpen"/>(true)이거나, 입력칸에 글자를 넣는 중일 때).
    /// 끄거나 오브젝트를 숨기면 요청을 거둔다. 다른 UI가 열려 있으면 그쪽 요청이 남아 계속 막힌다.
    /// </summary>
    [DefaultExecutionOrder(-200)] // 입력(-100)보다 먼저 막힘을 정한다
    public sealed class UIInputBlocker : MonoBehaviour
    {
        [Tooltip("켜면 이 오브젝트가 보이는 동안 항상 막는다(패널에 붙일 때). 끄면 SetOpen(true)이거나 입력칸 입력 중일 때만 막는다.")]
        [SerializeField] bool blockWhileActive = true;

        [Tooltip("글자를 넣는 동안 막을 입력칸.")]
        [SerializeField] TMP_InputField[] fields = Array.Empty<TMP_InputField>();

        bool open;

        /// <summary>보이는 동안 항상 막을지.</summary>
        public bool BlockWhileActive
        {
            get => blockWhileActive;
            set { blockWhileActive = value; Refresh(); }
        }

        /// <summary>지금 이 UI가 입력을 막고 있는지.</summary>
        public bool IsBlocking { get; private set; }

        /// <summary>창 열림/닫힘을 직접 알린다(오브젝트를 켜 둔 채 쓰는 창).</summary>
        public void SetOpen(bool value) { open = value; Refresh(); }

        public void SetFields(params TMP_InputField[] inputs) => fields = inputs ?? Array.Empty<TMP_InputField>();

        void OnEnable() => Refresh();
        void Update() => Refresh();

        void OnDisable()
        {
            IsBlocking = false;
            InputGate.SetBlocking(this, false);
        }

        void Refresh()
        {
            if (!isActiveAndEnabled) return;
            bool blocking = blockWhileActive || open;
            foreach (var field in fields)
                blocking |= field != null && field.isActiveAndEnabled && field.isFocused;
            if (IsBlocking == blocking) return;
            IsBlocking = blocking;
            InputGate.SetBlocking(this, blocking);
        }
    }
}
