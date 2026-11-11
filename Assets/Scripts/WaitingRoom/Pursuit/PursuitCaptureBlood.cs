using CurtainCall.Damage;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit
{
    [DefaultExecutionOrder(1200)]
    public sealed class PursuitCaptureBlood : MonoBehaviour
    {
        public PursuitVictimPose Pose;
        public bool Visible { get; private set; }
        public int EmittedBursts { get; private set; }
        ParticleSystem spray;
        GameObject screen;
        readonly UnityEngine.UI.Image[] drops = new UnityEngine.UI.Image[12];
        readonly float[] life = new float[12];
        readonly Vector2[] origins = new Vector2[12];
        float nextBurst;
        int nextDrop;
        void LateUpdate()
        {
            var rules = GameSettings.WaitingRoom.Pursuit;
            Visible = Pose != null && !Pose.Hidden && Pose.Elapsed >= rules.KnockdownSeconds && InjuryBloodEffects.IsVisible;
            if (!Visible) { Clear(); return; }
            var director = PursuitDirector.Current;
            if (spray == null && director != null && director.CaptureBloodPrefab != null) {
                var go = Instantiate(director.CaptureBloodPrefab, transform);
                spray = go.GetComponent<ParticleSystem>();
                spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (spray != null) {
                spray.transform.SetPositionAndRotation(Pose.BloodPosition, Quaternion.LookRotation(Vector3.up + Pose.HeadDirection * .35f));
                if (!spray.isPlaying) { var main = spray.main; main.loop = true; spray.Play(); }
            }
            // 화면 피: 잡힌 본인, 또는 이 사람을 관전 중인 화면(다음 대상으로 바꾸면 지운다, 사용자 2026-11-11).
            bool lens = Pose.IsLocalVictim || IsSpectatedHere;
            if (lens && screen == null && director != null && director.CaptureBloodSprite != null) CreateScreen(director.CaptureBloodSprite);
            if (screen != null && screen.activeSelf != lens) { ClearLens(); screen.SetActive(lens); }
            if (Pose.Elapsed >= nextBurst) {
                nextBurst = Pose.Elapsed + Mathf.Max(.1f, rules.CaptureBloodInterval);
                float amount = rules.CaptureBloodAmount * GameSettings.Base.BodyDamage.BloodAmount;
                if (spray != null && amount > 0) { spray.Emit(Mathf.CeilToInt(32 * amount)); EmittedBursts++; }
                if (screen != null && lens && rules.CaptureScreenBlood > 0) AddDrop();
            }
            for (int i = 0; i < drops.Length; i++) if (drops[i] != null) {
                life[i] = Mathf.Max(0, life[i] - Time.unscaledDeltaTime);
                drops[i].color = new Color(.38f, .005f, .012f, Mathf.Min(1, life[i]) * rules.CaptureScreenBlood);
                drops[i].rectTransform.anchoredPosition = origins[i] + Vector2.down * (3 - life[i]) * 16;
            }
        }
        void CreateScreen(Sprite sprite)
        {
            screen = new GameObject("PursuitBloodOnLens", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            var canvas = screen.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 71;
            var scaler = screen.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            for (int i = 0; i < drops.Length; i++) {
                var go = new GameObject("BloodSplash", typeof(RectTransform), typeof(UnityEngine.UI.Image)); go.transform.SetParent(screen.transform, false);
                drops[i] = go.GetComponent<UnityEngine.UI.Image>(); drops[i].sprite = sprite; drops[i].raycastTarget = false; drops[i].color = Color.clear;
            }
        }
        void AddDrop()
        {
            int i = nextDrop++ % drops.Length;
            // Keep a useful central window while allowing a few smaller flecks across the lens.
            float side = (nextDrop % 2 == 0 ? 1 : -1);
            origins[i] = new Vector2(side * Random.Range(350, 900), Random.Range(-480, 480));
            life[i] = Random.Range(2f, 3f);
            var rect = drops[i].rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.anchoredPosition = origins[i];
            rect.sizeDelta = Vector2.one * Random.Range(160, 360); rect.localRotation = Quaternion.Euler(0, 0, Random.Range(0, 360));
        }
        /// <summary>내 화면이 이 사람을 관전 중인가.</summary>
        bool IsSpectatedHere => CurtainCall.Cameras.CameraSpectator.IsSpectating
            && CurtainCall.Cameras.CameraSpectator.Target != null && CurtainCall.Cameras.CameraSpectator.Target.gameObject == gameObject;
        void Clear()
        {
            if (spray != null && !spray.isStopped) spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ClearLens();
        }
        void ClearLens()
        {
            for (int i = 0; i < drops.Length; i++) { life[i] = 0; if (drops[i] != null) drops[i].color = Color.clear; }
        }
        void OnDisable() { Clear(); }
        void OnDestroy() { if (spray != null) Destroy(spray.gameObject); if (screen != null) Destroy(screen); }
    }
}
