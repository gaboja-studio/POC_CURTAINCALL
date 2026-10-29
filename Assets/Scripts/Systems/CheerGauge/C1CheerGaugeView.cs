using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace CurtainCall.CheerGauge
{
    /// <summary>게임 규칙을 계산하지 않는 C1 호응 게이지 표시.</summary>
    [DisallowMultipleComponent]
    public sealed class C1CheerGaugeView : MonoBehaviour
    {
        public const int MaximumPlayers = 4;
        [SerializeField] UnityEngine.UI.Image[] fills;
        [SerializeField] UnityEngine.UI.Image[] highlights;
        [SerializeField] TMP_Text totalText;
        [SerializeField] TMP_Text labelText;
        [SerializeField, Min(0)] float transitionSeconds = 0.2f;
        [SerializeField] bool reduceFlashing;

        static readonly Color32[] Colors =
        {
            new(0x4C, 0x8D, 0xDB, 255), new(0xDE, 0x92, 0x53, 255),
            new(0xCD, 0xA6, 0xD8, 255), new(0xBA, 0xE5, 0xDA, 255)
        };
        readonly float[] shown = new float[MaximumPlayers];
        readonly float[] from = new float[MaximumPlayers];
        readonly float[] targets = new float[MaximumPlayers];
        readonly float[] flashUntil = new float[MaximumPlayers];
        float started;
        bool transitioning;
        public bool HasData { get; private set; }
        public long Total { get; private set; }
        public long Capacity { get; private set; }
        public int ParticipantCount { get; private set; }
        public bool ReduceFlashing { get => reduceFlashing; set => reduceFlashing = value; }

        void Awake() => SetUnavailable();
        void OnEnable()
        {
            if (HasData) Snap();
        }
        void OnDisable()
        {
            transitioning = false;
            ClearHighlights();
        }
        void Update()
        {
            if (transitioning) Step();
            if (highlights == null) return;
            for (int i = 0; i < highlights.Length; i++)
            {
                if (highlights[i] == null || !highlights[i].enabled) continue;
                float left = flashUntil[i] - Time.unscaledTime;
                if (left <= 0) { highlights[i].enabled = false; continue; }
                float pulse = reduceFlashing ? 0.5f : 0.5f + 0.5f * Mathf.Cos((0.8f - left) * Mathf.PI * 5);
                highlights[i].color = new Color(240f / 255, 238f / 255, 220f / 255, 0.65f * pulse * left / 0.8f);
            }
        }

        public void Configure(UnityEngine.UI.Image[] playerFills, UnityEngine.UI.Image[] fallHighlights,
            TMP_Text total, TMP_Text label)
        {
            if (playerFills == null || playerFills.Length != MaximumPlayers ||
                fallHighlights == null || fallHighlights.Length != MaximumPlayers || total == null || label == null)
                throw new ArgumentException("채움/강조 4개와 합계/라벨이 필요합니다.");
            for (int i = 0; i < MaximumPlayers; i++)
                if (playerFills[i] == null || fallHighlights[i] == null)
                    throw new ArgumentException("이미지 참조가 누락되었습니다.");
            fills = (UnityEngine.UI.Image[])playerFills.Clone();
            highlights = (UnityEngine.UI.Image[])fallHighlights.Clone();
            totalText = total;
            labelText = label;
            labelText.text = "호응";
            for (int i = 0; i < MaximumPlayers; i++)
            {
                ConfigureRadial(fills[i]);
                ConfigureRadial(highlights[i]);
            }
            SetUnavailable();
        }

        public bool SetScores(IReadOnlyList<long> scores, long capacity, bool animate = true)
        {
            if (!Ready() || scores == null || scores.Count < 1 || scores.Count > MaximumPlayers || capacity <= 0)
                return Reject();
            long sum = 0;
            for (int i = 0; i < scores.Count; i++)
            {
                if (scores[i] < 0 || scores[i] > capacity - sum) return Reject();
                sum += scores[i];
            }
            bool sameLayout = HasData && Capacity == capacity && ParticipantCount == scores.Count;
            if (transitioning) Step();
            bool changed = false;
            for (int i = 0; i < MaximumPlayers; i++)
            {
                float next = i < scores.Count ? (float)((double)scores[i] / capacity) : 0;
                changed |= targets[i] != next;
            }
            if (sameLayout && !changed && Total == sum)
            {
                if (!animate) Snap();
                return true;
            }
            for (int i = 0; i < MaximumPlayers; i++)
            {
                from[i] = shown[i];
                targets[i] = i < scores.Count ? (float)((double)scores[i] / capacity) : 0;
            }
            if (!sameLayout) ClearHighlights();
            HasData = true;
            Capacity = capacity;
            ParticipantCount = scores.Count;
            Total = sum;
            totalText.text = sum.ToString(CultureInfo.InvariantCulture);
            if (!sameLayout || !animate || !Application.isPlaying || !isActiveAndEnabled || transitionSeconds <= 0)
                Snap();
            else if (changed)
            {
                transitioning = true;
                started = Time.unscaledTime;
            }
            return true;
        }

        public void SetUnavailable()
        {
            HasData = false;
            Total = Capacity = 0;
            ParticipantCount = 0;
            transitioning = false;
            Array.Clear(shown, 0, shown.Length);
            Array.Clear(targets, 0, targets.Length);
            if (fills != null)
                foreach (var image in fills)
                    if (image != null) { image.fillAmount = 0; image.enabled = false; }
            ClearHighlights();
            if (totalText != null) totalText.text = "—";
        }

        /// <summary>실제 추락 판정에서 새 점수 적용 전에 호출한다. 야유에는 호출하지 않는다.</summary>
        public bool FlashFall(int index)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || !HasData || !Ready() ||
                index < 0 || index >= ParticipantCount || highlights == null || highlights[index] == null)
                return false;
            if (transitioning) Step();
            var source = fills[index];
            if (source.fillAmount <= 0) return false;
            var target = highlights[index];
            target.fillAmount = source.fillAmount;
            target.rectTransform.localRotation = source.rectTransform.localRotation;
            target.color = new Color(240f / 255, 238f / 255, 220f / 255, reduceFlashing ? 0.325f : 0.65f);
            target.enabled = true;
            flashUntil[index] = Time.unscaledTime + 0.8f;
            return true;
        }

        bool Reject() { SetUnavailable(); return false; }
        bool Ready()
        {
            if (fills == null || fills.Length != MaximumPlayers || totalText == null || labelText == null) return false;
            foreach (var image in fills) if (image == null) return false;
            return true;
        }
        void Snap()
        {
            transitioning = false;
            Array.Copy(targets, shown, MaximumPlayers);
            Draw();
        }
        void Step()
        {
            float t = Mathf.Clamp01((Time.unscaledTime - started) / Mathf.Max(transitionSeconds, 0.0001f));
            float eased = t * t * (3 - 2 * t);
            for (int i = 0; i < MaximumPlayers; i++) shown[i] = Mathf.Lerp(from[i], targets[i], eased);
            Draw();
            if (t >= 1) transitioning = false;
        }
        void Draw()
        {
            if (!Ready()) return;
            float prefix = 0;
            for (int i = 0; i < MaximumPlayers; i++)
            {
                fills[i].fillAmount = shown[i];
                fills[i].rectTransform.localRotation = Quaternion.Euler(0, 0, -360 * prefix);
                fills[i].color = Colors[i];
                fills[i].enabled = shown[i] > 0;
                prefix += shown[i];
            }
        }
        void ClearHighlights()
        {
            Array.Clear(flashUntil, 0, flashUntil.Length);
            if (highlights != null)
                foreach (var image in highlights) if (image != null) image.enabled = false;
        }
        static void ConfigureRadial(UnityEngine.UI.Image image)
        {
            image.type = UnityEngine.UI.Image.Type.Filled;
            image.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
            image.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
            image.fillClockwise = true;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }
    }
}
