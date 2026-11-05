using System;
using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 리허설 넘기기 종료 연출(사용자 2026-11-04): 화면을 검게 덮고(페이드 아웃) 다음 화면이 준비되면 걷어 낸다(페이드 인).
    /// 이 화면 전용. 씬 전환 중에도 남도록 리허설 진행기(DontDestroyOnLoad) 아래에 둔다.
    /// </summary>
    public sealed class RehearsalScreenFader : MonoBehaviour
    {
        const float FadeSeconds = .8f, MaxHoldSeconds = 25f;
        GameObject canvasRoot;
        Image cover;
        float alpha, target, holdStarted;
        Func<bool> ready;

        public bool IsActive => canvasRoot != null && canvasRoot.activeSelf;

        /// <summary>검게 덮은 뒤 <paramref name="reveal"/>이 참이 되면(또는 오래 걸리면) 다시 걷는다.</summary>
        public void Play(Func<bool> reveal)
        {
            if (canvasRoot == null) Build();
            ready = reveal; target = 1f; holdStarted = -1f;
            canvasRoot.SetActive(true);
        }

        /// <summary>A departed room cannot keep its fade or completion predicate over the next room.</summary>
        public void Cancel()
        {
            ready = null; alpha = target = 0f; holdStarted = -1f;
            if (cover != null) cover.color = Color.clear;
            if (canvasRoot != null) canvasRoot.SetActive(false);
        }

        void Build()
        {
            canvasRoot = new GameObject("RehearsalFadeCanvas", typeof(RectTransform), typeof(Canvas));
            canvasRoot.transform.SetParent(transform, false);
            var canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30000;
            var go = new GameObject("Cover", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(canvasRoot.transform, false);
            var rect = (RectTransform)go.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            cover = go.GetComponent<Image>(); cover.color = new Color(0f, 0f, 0f, 0f); cover.raycastTarget = false;
            canvasRoot.SetActive(false);
        }

        void Update()
        {
            if (!IsActive) return;
            alpha = Mathf.MoveTowards(alpha, target, Time.unscaledDeltaTime / FadeSeconds);
            cover.color = new Color(0f, 0f, 0f, alpha);
            if (target > 0f && alpha >= 1f)
            {
                if (holdStarted < 0f) holdStarted = Time.unscaledTime;
                bool timedOut = Time.unscaledTime - holdStarted > MaxHoldSeconds;
                if (timedOut || ready == null || ready()) target = 0f;
            }
            else if (target <= 0f && alpha <= 0f) { canvasRoot.SetActive(false); ready = null; }
        }

        void OnDestroy() { if (canvasRoot != null) Destroy(canvasRoot); }
    }
}
