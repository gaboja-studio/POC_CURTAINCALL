using DG.Tweening;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration
{
    public enum HudRole { Objective, SystemEvent, Guide, Interaction, Feedback, Status, Vote, Outcome, ZoneName }
    public enum HudEffect { Fade, Rise, Slide, Zoom, Pop }

    // Animate the inner surface only; the layout group owns the outer rect.
    public sealed class HudRoleMotion : MonoBehaviour
    {
        public HudEffect enter = HudEffect.Rise, exit = HudEffect.Fade;
        [Range(0, 1)] public float opacity = 1;
        [Min(0)] public float enterSeconds = .18f, exitSeconds = .14f;
        [Tooltip("0 = visible until explicitly dismissed"), Min(0)] public float displaySeconds = 4;
        [Tooltip("끄면 코드가 초를 지정해도 이 프리팹의 Display Seconds를 적용합니다.")]
        public bool allowDurationOverride = true;
        public Vector2 riseOffset=new Vector2(0,-16), slideOffset=new Vector2(-28,0);
        [Min(.01f)] public float zoomScale=.90f, popScale=.80f;
        public CanvasGroup group;
        public RectTransform surface;
        Tween tween;
        bool captured;
        Vector2 restPosition;
        Vector3 restScale;

        public void Play(bool opening, System.Action complete = null)
        {
            Stop();
            if (!captured) { restPosition=surface.anchoredPosition; restScale=surface.localScale; captured=true; }
            var style = opening ? enter : exit;
            var offset = style == HudEffect.Rise ? riseOffset :
                style == HudEffect.Slide ? slideOffset : Vector2.zero;
            var scale = style == HudEffect.Zoom ? zoomScale : style == HudEffect.Pop ? popScale : 1f;
            // An early dismissal starts at the current pose.
            if (opening) { group.alpha = 0; surface.anchoredPosition = restPosition+offset; surface.localScale = restScale * scale; }
            float a = group.alpha;
            Vector2 p = surface.anchoredPosition;
            Vector3 s = surface.localScale;
            void Sample(float t)
            {
                group.alpha = Mathf.Lerp(a, opening ? opacity : 0, t);
                surface.anchoredPosition = Vector2.Lerp(p, opening ? restPosition : restPosition+offset, t);
                surface.localScale = Vector3.LerpUnclamped(s, restScale * (opening ? 1 : scale), t);
            }
            float duration = opening ? enterSeconds : exitSeconds;
            if (duration <= 0) { Sample(1); complete?.Invoke(); return; }
            tween = DOVirtual.Float(0,1,duration,Sample).SetUpdate(true)
                .SetEase(opening && style == HudEffect.Pop ? Ease.OutBack : Ease.OutQuad)
                .OnComplete(() => { tween = null; complete?.Invoke(); });
        }
        void Stop() { tween?.Kill(); tween = null; }
        void OnDisable() => Stop();
    }
}
