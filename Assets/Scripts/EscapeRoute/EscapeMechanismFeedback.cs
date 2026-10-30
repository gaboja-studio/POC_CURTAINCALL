using System.Collections.Generic;
using CurtainCall.UI.NotificationsV1;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    /// <summary>호스트가 확정해 공유한 상태만 안내한다. 늦은 참가의 첫 스냅샷은 재생하지 않는다.</summary>
    public sealed class EscapeMechanismFeedback : MonoBehaviour
    {
        [SerializeField] EscapeCooperativeLocks locks;
        [SerializeField] EscapeCooperativeRopes ropes;
        [SerializeField] ToastView toast;
        [SerializeField] Renderer[] returnIndicators = System.Array.Empty<Renderer>();
        readonly Queue<int> pending = new();
        MaterialPropertyBlock tint;
        bool initialized, lastReturn;
        uint lastOpen, lastLatched;

        void Update()
        {
            uint open = 0, latched = 0;
            if (locks != null) for (int i = 0; i < locks.Count && i < 32; i++)
            {
                if (locks.IsOpen(i)) open |= 1u << i;
                if (locks.IsLatched(i)) latched |= 1u << i;
            }
            bool ready = locks != null && locks.IsReady && ropes != null && ropes.IsReady;
            bool returnOpen = ready && ropes.ReturnOpen;
            ObserveState(ready, open, latched, returnOpen);
            PresentReturn(returnOpen);
            if (!ready || toast == null || toast.IsVisible || pending.Count == 0) return;
            int index = pending.Dequeue();
            bool active = index < 0 ? returnOpen : locks.IsOpen(index);
            string message = index < 0 ? "복귀 버튼 → 복귀 계단 열림" : locks.Feedback(index);
            toast.Show(message, active ? ToastState.Success : ToastState.Normal, 2.4f,
                active ? ToastIcon.Megaphone : ToastIcon.Warning);
        }

        internal void ObserveState(bool ready, uint open, uint latched, bool returnOpen)
        {
            if (!ready) { ResetFeedback(); return; }
            if (initialized)
            {
                uint changed = (lastOpen ^ open) | (lastLatched ^ latched);
                for (int i = 0; i < 32; i++)
                    if ((changed & (1u << i)) != 0 && !pending.Contains(i)) pending.Enqueue(i);
                if (lastReturn != returnOpen && returnOpen && !pending.Contains(-1)) pending.Enqueue(-1);
            }
            lastOpen = open; lastLatched = latched; lastReturn = returnOpen; initialized = true;
        }

        void PresentReturn(bool open)
        {
            tint ??= new MaterialPropertyBlock();
            Color color = open ? Color.green : Color.red;
            tint.SetColor("_BaseColor", color); tint.SetColor("_Color", color); tint.SetColor("_EmissionColor", color * 2);
            foreach (var indicator in returnIndicators) if (indicator != null) indicator.SetPropertyBlock(tint);
        }

        void ResetFeedback()
        {
            initialized = false; pending.Clear();
            if (toast != null) toast.HideImmediate();
        }
        void OnDisable() => ResetFeedback();
    }
}
