using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 스케줄표를 가까이서 보는 동안 다른 화면 UI(탐색 HUD·중앙 안내·알림 등)를 가린다(2026-11-04 사용자).
    /// 열 때 켜져 있던 화면 캔버스를 끄고, 닫으면 그 캔버스만 다시 켠다. 스케줄표 판(월드 캔버스)과 카드 설명 툴팁은 남긴다.
    /// 시간제 안내는 가려진 동안 남은 시간이 줄지 않고, 가려진 동안 새로 온 안내는 닫은 뒤 온 순서대로 뜬다(각 패널이 <see cref="IsHeld"/>를 본다).
    /// </summary>
    public static class ScheduleScreenHold
    {
        static readonly List<Canvas> hidden = new();
        static readonly HashSet<Canvas> keep = new();

        /// <summary>스케줄표가 열려 다른 화면 UI를 가리는 중.</summary>
        public static bool IsHeld { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { IsHeld = false; hidden.Clear(); keep.Clear(); }

        /// <summary>가리지 않을 캔버스(스케줄표 카드 설명 툴팁 등). 이미 가려졌으면 다시 켠다.</summary>
        public static void Keep(Canvas canvas)
        {
            if (canvas == null) return;
            canvas = canvas.rootCanvas;
            keep.Add(canvas);
            if (hidden.Remove(canvas)) canvas.enabled = true;
        }

        public static void Set(bool held)
        {
            if (IsHeld == held) return;
            IsHeld = held;
            if (held)
            {
                foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    Track(canvas);
                return;
            }
            foreach (var canvas in hidden) if (canvas != null) canvas.enabled = true;
            hidden.Clear();
        }

        /// <summary>가리는 동안 새로 만든 화면 캔버스를 같이 가린다(닫으면 다시 켠다).</summary>
        public static void Track(Canvas canvas)
        {
            if (!IsHeld || canvas == null || !canvas.isRootCanvas || !canvas.enabled
                || canvas.renderMode == RenderMode.WorldSpace || keep.Contains(canvas)) return;
            canvas.enabled = false;
            hidden.Add(canvas);
        }
    }
}
