using CurtainCall.Campaign;
using CurtainCall.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 탐색 제한 시간 화면 표시(임시 연출, 2026-10-16 대기실 담당 결정). 모든 화면에서 각자 그린다.
    /// - 우측 하단: 원형 시계 아이콘(남은 비율 채움) + mm:ss. 쉬는 시간 동안만 보이고, 아무도 나가지 않아 멈춘 동안에도 전체 시간을 보여 준다.
    /// - 시계 옆: 경보·방송 문구(<see cref="ExplorationTimerAnnouncer.Announced"/>)를 잠시 띄운다.
    /// - 소집 경보면 종소리. 클립이 비어 있으면 코드로 만든 임시 '딩동'을 쓴다.
    /// UI는 실행 때 코드로 만든다(프리팹 없음).
    /// </summary>
    public sealed class ExplorationTimerView : MonoBehaviour
    {
        const string FontResource = "Fonts/Pretendard-Medium/Pretendard-Medium SDF";

        [SerializeField] ExplorationTimerAnnouncer announcer;
        [Tooltip("비우면 Resources의 Pretendard 폰트.")]
        [SerializeField] TMP_FontAsset font;
        [Tooltip("소집 경보 종소리. 비우면 임시 '딩동'을 만든다.")]
        [SerializeField] AudioClip bellClip;
        [SerializeField, Range(0f, 1f)] float bellVolume = 0.8f;
        [Tooltip("문구가 떠 있는 시간(초).")]
        [SerializeField, Min(0.5f)] float messageDuration = 4f;

        [Header("색")]
        [SerializeField] Color normalColor = new(0.95f, 0.9f, 0.75f);
        [SerializeField] Color alarmColor = new(1f, 0.35f, 0.3f);
        [SerializeField] Color pausedColor = new(0.7f, 0.7f, 0.7f);

        CampaignRun run;
        GameObject root;
        Image clockFill;
        TextMeshProUGUI timeText;
        TextMeshProUGUI messageText;
        AudioSource audioSource;
        float messageUntil;

        void Awake()
        {
            if (announcer == null) announcer = FindFirstObjectByType<ExplorationTimerAnnouncer>();
            BuildUi();
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            if (bellClip == null) bellClip = MakeBell();
        }

        void OnEnable()
        {
            if (announcer != null) announcer.Announced += Show;
        }

        void OnDisable()
        {
            if (announcer != null) announcer.Announced -= Show;
        }

        void Start() => run = CampaignRun.EnsureExists();

        void LateUpdate()
        {
            var s = run != null ? run.State : default;
            bool visible = s.Phase == CampaignPhase.RestTime;
            if (root.activeSelf != visible) root.SetActive(visible);
            if (!visible) return;

            var rules = GameSettings.Base.Run;
            float total = s.Rest == RestKind.Guide ? rules.GuideRestDuration : rules.RestDuration;
            float remaining = Mathf.Max(0f, s.TimeRemaining);
            int seconds = Mathf.CeilToInt(remaining);

            timeText.text = $"{seconds / 60}:{seconds % 60:00}";
            clockFill.fillAmount = total > 0f ? Mathf.Clamp01(remaining / total) : 0f;

            var color = s.Timer != RestTimerState.Running ? pausedColor : s.AlarmRaised ? alarmColor : normalColor;
            timeText.color = color;
            clockFill.color = color;

            float left = messageUntil - Time.unscaledTime;
            messageText.alpha = Mathf.Clamp01(left / 0.5f);
        }

        void Show(ExplorationAnnouncement a)
        {
            messageText.text = a.Message;
            messageText.color = a.IsRecallAlarm ? alarmColor : normalColor;
            messageUntil = Time.unscaledTime + messageDuration;
            if (a.IsRecallAlarm && bellClip != null) audioSource.PlayOneShot(bellClip, bellVolume);
        }

        // ── UI 만들기 ────────────────────────────────────────

        void BuildUi()
        {
            if (font == null) font = Resources.Load<TMP_FontAsset>(FontResource);

            var canvasGo = new GameObject("ExplorationTimerCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // 우측 하단 묶음: [문구][mm:ss][시계]
            root = new GameObject("ExplorationTimer", typeof(RectTransform));
            var rootRect = (RectTransform)root.transform;
            rootRect.SetParent(canvasGo.transform, false);
            rootRect.anchorMin = rootRect.anchorMax = rootRect.pivot = new Vector2(1f, 0f);
            rootRect.anchoredPosition = new Vector2(-40f, 40f);
            rootRect.sizeDelta = new Vector2(900f, 96f);

            var disc = MakeDisc(128);
            var face = MakeImage("ClockFace", rootRect, disc, new Color(0f, 0f, 0f, 0.6f));
            Place(face.rectTransform, new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(96f, 96f));

            clockFill = MakeImage("ClockFill", face.rectTransform, disc, normalColor);
            clockFill.type = Image.Type.Filled;
            clockFill.fillMethod = Image.FillMethod.Radial360;
            clockFill.fillOrigin = (int)Image.Origin360.Top;
            clockFill.fillClockwise = false;
            Place(clockFill.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80f, 80f));

            var hub = MakeImage("ClockHub", face.rectTransform, disc, new Color(0f, 0f, 0f, 0.8f));
            Place(hub.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 16f));

            timeText = MakeText("Time", rootRect, 56f, TextAlignmentOptions.MidlineRight);
            Place(timeText.rectTransform, new Vector2(1f, 0.5f), new Vector2(-112f, 0f), new Vector2(170f, 96f));

            messageText = MakeText("Message", rootRect, 30f, TextAlignmentOptions.MidlineRight);
            Place(messageText.rectTransform, new Vector2(1f, 0.5f), new Vector2(-300f, 0f), new Vector2(600f, 96f));
            messageText.alpha = 0f;

            root.SetActive(false);
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(anchor.x, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static Image MakeImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        TextMeshProUGUI MakeText(string name, Transform parent, float size, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            text.outlineWidth = 0.15f;
            text.outlineColor = new Color32(0, 0, 0, 200);
            return text;
        }

        // 가장자리를 부드럽게 한 흰 원(시계 판·채움·중심 공용).
        static Sprite MakeDisc(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                byte a = (byte)(Mathf.Clamp01(r - d) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        // 임시 종소리: 두 음(딩 E5 → 동 C5), 배음 조금, 지수 감쇠.
        static AudioClip MakeBell()
        {
            const int rate = 44100;
            const float noteLength = 0.9f, gap = 0.45f;
            int total = (int)(rate * (gap + noteLength));
            var data = new float[total];
            AddTone(data, rate, 0, 659.25f, noteLength);
            AddTone(data, rate, (int)(rate * gap), 523.25f, noteLength);
            var clip = AudioClip.Create("TempRecallBell", total, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static void AddTone(float[] data, int rate, int start, float freq, float length)
        {
            int count = Mathf.Min((int)(rate * length), data.Length - start);
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Exp(-4f * t) * Mathf.Clamp01(t * 200f);
                float w = Mathf.Sin(2f * Mathf.PI * freq * t)
                          + 0.4f * Mathf.Sin(2f * Mathf.PI * freq * 2.76f * t) * Mathf.Exp(-3f * t)
                          + 0.2f * Mathf.Sin(2f * Mathf.PI * freq * 5.4f * t) * Mathf.Exp(-6f * t);
                data[start + i] += 0.35f * env * w;
            }
        }
    }
}
