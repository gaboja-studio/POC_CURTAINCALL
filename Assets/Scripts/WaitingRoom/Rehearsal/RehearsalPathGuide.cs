using CurtainCall.UI.Wayfinding;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>Each player follows their own pulse route to the stage after rehearsal.
    /// Color, brightness, correction time and pulse interval follow GameSettings > Base > 네비게이션 안내 (live).</summary>
    public sealed class RehearsalPathGuide : MonoBehaviour
    {
        NavigationHandle navigation;
        Vector3? target;
        internal event System.Action<bool> VisibilityChanged;
        public bool IsVisible => navigation != null && navigation.IsVisible;
        public Vector3[] Path => navigation?.Path ?? System.Array.Empty<Vector3>();

        public void SetTarget(Vector3? destination)
        {
            if (target == destination && (destination == null || navigation != null && !navigation.IsDisposed)) return;
            target = destination;
            if (!destination.HasValue) { Release(); return; }
            if (navigation == null || navigation.IsDisposed)
            {
                navigation = WorldNavigation.Show(destination.Value);
                navigation.VisibilityChanged += OnVisibility;
            }
            else navigation.Update(destination.Value);
        }

        void OnVisibility(bool visible) => VisibilityChanged?.Invoke(visible);
        void Release()
        {
            if (navigation == null) return;
            navigation.Dispose(); navigation.VisibilityChanged -= OnVisibility; navigation = null;
        }
        void OnDisable() { target = null; Release(); }
        void OnDestroy() => Release();
    }
}
