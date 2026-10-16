using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.Cameras
{
    /// <summary>IMGUI 테스트 UI 등 커서를 다시 잠그지 않을 화면 영역(왼쪽 위 기준).</summary>
    public sealed class CameraCursorExclusion : MonoBehaviour
    {
        [SerializeField] Rect screenRect = new(10f, 10f, 340f, 320f);
        static readonly List<CameraCursorExclusion> regions = new();

        void OnEnable() => regions.Add(this);
        void OnDisable() => regions.Remove(this);

        public static bool Contains(Vector2 screenPosition)
        {
            Vector2 guiPosition = new(screenPosition.x, Screen.height - screenPosition.y);
            foreach (var region in regions)
                if (region != null && region.isActiveAndEnabled && region.screenRect.Contains(guiPosition)) return true;
            return false;
        }
    }
}
