using CurtainCall.Cameras;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.UI.SceneIntegration;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    /// <summary>Reads the local player's existing replicated limb state. Never applies damage or changes gameplay.</summary>
    [DefaultExecutionOrder(2000)]
    public sealed class EscapeBodyStatusHud : MonoBehaviour
    {
        [SerializeField] CanvasGroup visibility;
        [SerializeField] HumanBodyDiagramGraphic diagram;
        [SerializeField] CanvasGroup aimDot;
        BodyPart shown = (BodyPart)(-1);
        public BodyPart DisplayedParts => shown == (BodyPart)(-1) ? BodyPart.None : shown & PlayerCondition.AllLimbs;
        public bool IsVisible => visibility != null && visibility.alpha > .5f;
        public bool IsAimDotVisible => IsVisible && aimDot != null && aimDot.alpha > .5f;

        void Awake() => Hide();
        void LateUpdate()
        {
            var player = NetworkPlayer.Local;
            bool visible = EscapePerspectiveToggle.Current != null && EscapePerspectiveToggle.Current.InEscapeScene
                && player != null && player.IsSpawned;
            if (!visible) { Hide(); return; }
            visibility.alpha = 1;
            visibility.blocksRaycasts = visibility.interactable = false;
            var director = CameraViewDirector.Current;
            SetAimDot(director != null && director.isActiveAndEnabled && director.IsFirstPerson
                && director.LocalPlayer == player && player.State == PlayerState.Normal && !SceneCurtainTransition.IsActive);
            Present(player.LostParts);
        }
        public void Present(BodyPart parts)
        {
            parts &= PlayerCondition.AllLimbs;
            if (shown == parts) return;
            shown = parts;
            if (diagram != null) diagram.Present(parts);
        }
        void SetAimDot(bool visible)
        {
            if (aimDot == null) return;
            aimDot.alpha = visible ? 1 : 0;
            aimDot.blocksRaycasts = aimDot.interactable = false;
        }
        void Hide() { SetAimDot(false); if (visibility != null) { visibility.alpha = 0; visibility.blocksRaycasts = visibility.interactable = false; } }
        void OnDisable() => Hide();
    }
}
