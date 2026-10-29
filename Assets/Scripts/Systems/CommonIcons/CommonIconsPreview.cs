using TMPro;
using UnityEngine;

namespace CurtainCall.CommonIcons
{
    public sealed class CommonIconsPreview : MonoBehaviour
    {
        static readonly Color32[] Tints = {
            new Color32(0xF0,0xEE,0xDC,255), new Color32(0x73,0xC7,0xA1,255),
            new Color32(0xE0,0x6C,0x78,255), new Color32(0xF2,0xB4,0x5B,255),
            new Color32(0xAA,0xB0,0xAD,255), new Color32(0x4C,0x8D,0xDB,255),
            new Color32(0xDE,0x92,0x53,255), new Color32(0xCD,0xA6,0xD8,255),
            new Color32(0xBA,0xE5,0xDA,255), new Color32(255,255,255,255)
        };
        static readonly string[] TintNames = { "기본", "성공", "위험", "경고", "비활성 정보", "단원 1", "단원 2", "단원 3", "단원 4", "흰색" };
        static readonly int[] Sizes = { 32, 48, 16, 24 };
        [SerializeField] CommonIconCatalog catalog;
        [SerializeField] UnityEngine.UI.Image[] samples;
        [SerializeField] TMP_Text status;
        Vector2[] originalPositions;
        int tintIndex;
        int sizeIndex;
        int clicks;

        public CommonIconCatalog Catalog => catalog;
        public int SampleCount => samples.Length;
        public int TintIndex => tintIndex;
        public int DisplaySize => Sizes[sizeIndex];
        public int Clicks => clicks;
        public UnityEngine.UI.Image GetSample(int index) => samples[index];

        public void Configure(CommonIconCatalog source, UnityEngine.UI.Image[] images, TMP_Text statusText)
        {
            catalog = source;
            samples = images;
            status = statusText;
            CapturePositions();
            RefreshStatus();
        }

        void Awake()
        {
            CapturePositions();
            RefreshStatus();
        }

        void CapturePositions()
        {
            if (samples == null) return;
            originalPositions = new Vector2[samples.Length];
            for (int i = 0; i < samples.Length; i++)
                originalPositions[i] = samples[i].rectTransform.anchoredPosition;
        }

        public void NextTint()
        {
            tintIndex = (tintIndex + 1) % Tints.Length;
            foreach (var sample in samples) sample.color = Tints[tintIndex];
            RefreshStatus();
        }

        public void NextSize()
        {
            sizeIndex = (sizeIndex + 1) % Sizes.Length;
            ApplySize();
            RefreshStatus();
        }

        public void ResetPreview()
        {
            tintIndex = sizeIndex = clicks = 0;
            foreach (var sample in samples) sample.color = Tints[0];
            ApplySize();
            RefreshStatus();
        }

        public void CountSampleClick()
        {
            clicks++;
            RefreshStatus();
        }

        void ApplySize()
        {
            if (originalPositions == null) CapturePositions();
            for (int i = 0; i < samples.Length; i++)
            {
                var rect = samples[i].rectTransform;
                rect.sizeDelta = Vector2.one * DisplaySize;
                rect.anchoredPosition = originalPositions[i] + new Vector2((32 - DisplaySize) * 0.5f, -(32 - DisplaySize) * 0.5f);
            }
        }

        void RefreshStatus()
        {
            if (status != null)
                status.text = $"{TintNames[tintIndex]} · {DisplaySize}px · 버튼 입력 {clicks} · 그림 승인 전 확인용";
        }
    }
}
