using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>
    /// M2 원형 하루 시계 표시(참고: 원형 시간표 + 가운데 시각·일자 표시). 12시간 원판에 시간대 색 구역(쉬는 시간·공연·밤 정비)을
    /// 한 바퀴로 그리고, 바늘이 지금 시각을 가리킨다. 가운데에 오전/오후·시각·일차·시간대, 쉬는 시간이면 바깥 고리와 남은 시간을 보인다.
    /// 그림 자원 없이 코드로 원·고리 모양을 만들어 씬·프리팹을 고치지 않고 붙인다. 판정은 하지 않는다(<see cref="DayClock"/>).
    /// </summary>
    public sealed class DayClockView : MonoBehaviour
    {
        const string FontPath = "Fonts/Spectator/Pretendard-Medium SDF";
        static Sprite disc, ring;

        static readonly Color Face = new(.11f, .11f, .15f, .96f);
        static readonly Color RestColor = new(.28f, .82f, .86f, 1f);
        static readonly Color ShowColor = new(.52f, .36f, .92f, 1f);
        static readonly Color NightColor = new(.18f, .22f, .48f, 1f);
        static readonly Color TimerColor = new(.78f, .35f, .98f, 1f);
        static readonly Color AlarmColor = new(.95f, .28f, .28f, 1f);

        RectTransform hand;
        Image timerRing, timerTrack;
        TMP_Text meridiem, time, day, remaining;

        public bool IsShown => gameObject.activeSelf;
        public string TimeText => time != null ? time.text : "";
        public string DayText => day != null ? day.text : "";
        public string RemainingText => remaining != null && remaining.gameObject.activeSelf ? remaining.text : "";
        public float HandAngle => hand != null ? -hand.localEulerAngles.z : 0f;
        public float TimerFill => timerRing != null && timerRing.enabled ? timerRing.fillAmount : 0f;

        /// <summary>부모 아래에 시계를 만든다. 크기는 정사각형 <paramref name="size"/>.</summary>
        public static DayClockView Create(Transform parent, float size)
        {
            var root = new GameObject("DayClock", typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(size, size);
            var view = root.AddComponent<DayClockView>();
            view.Build(size);
            root.SetActive(false);
            return view;
        }

        void Build(float size)
        {
            var font = Resources.Load<TMP_FontAsset>(FontPath);
            timerTrack = Layer("TimerTrack", Ring(), 1f, new Color(1f, 1f, 1f, .12f));
            timerRing = Layer("TimerRing", Ring(), 1f, TimerColor);
            Radial(timerRing, 0f, 1f);
            Layer("Face", Disc(), .86f, Face);
            foreach (var (start, end, band) in DayClock.Bands)
            {
                var segment = Layer("Band" + start, Disc(), .8f, band == DayClockBand.Rest ? RestColor : band == DayClockBand.Performance ? ShowColor : NightColor);
                Radial(segment, DayClock.Angle(start), (end - start) / 12f);
            }
            for (int hour = 0; hour < 12; hour++)
            {
                var tick = Layer("Tick" + hour, null, 0f, new Color(1f, 1f, 1f, .55f));
                var t = (RectTransform)tick.transform;
                t.sizeDelta = new Vector2(size * .012f, size * .05f);
                t.localRotation = Quaternion.Euler(0, 0, -hour * 30f);
                t.anchoredPosition = Quaternion.Euler(0, 0, -hour * 30f) * new Vector3(0, size * .37f, 0);
            }
            var handImage = Layer("Hand", null, 0f, Color.white);
            hand = (RectTransform)handImage.transform;
            hand.pivot = new Vector2(.5f, 0f);
            hand.sizeDelta = new Vector2(size * .025f, size * .36f);
            Layer("Center", Disc(), .5f, Face);
            meridiem = Text("Meridiem", font, size * .07f, new Vector2(0, size * .11f));
            time = Text("Time", font, size * .14f, new Vector2(0, size * .02f));
            day = Text("Day", font, size * .055f, new Vector2(0, -size * .08f));
            remaining = Text("Remaining", font, size * .06f, new Vector2(0, -size * .3f));
        }

        Image Layer(string name, Sprite sprite, float scale, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            var size = ((RectTransform)transform).sizeDelta;
            if (scale > 0f) rect.sizeDelta = size * scale;
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static void Radial(Image image, float startAngle, float amount)
        {
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Top;
            image.fillClockwise = true;
            image.fillAmount = amount;
            image.rectTransform.localRotation = Quaternion.Euler(0, 0, -startAngle);
        }

        TMP_Text Text(string name, TMP_FontAsset font, float fontSize, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(((RectTransform)transform).sizeDelta.x * .9f, fontSize * 1.4f);
            var text = go.GetComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.color = Color.white;
            return text;
        }

        /// <summary>한 장면을 그린다.</summary>
        public void Show(in DayClockReading reading)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            hand.localRotation = Quaternion.Euler(0, 0, -DayClock.Angle(reading.Hours));
            meridiem.text = reading.Meridiem;
            time.text = reading.TimeText;
            day.text = $"{reading.Day}일차 · {reading.Period}";
            timerRing.enabled = timerTrack.enabled = reading.HasTimer;
            timerRing.fillAmount = reading.RemainingRatio;
            timerRing.color = reading.Alarm ? AlarmColor : TimerColor;
            remaining.gameObject.SetActive(reading.HasTimer);
            remaining.text = $"남은 시간 {reading.RemainingText}";
            remaining.color = reading.Alarm ? AlarmColor : Color.white;
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        static Sprite Disc() => disc != null ? disc : disc = Circle(0f);
        static Sprite Ring() => ring != null ? ring : ring = Circle(.88f);

        // 가장자리를 부드럽게 한 원(안쪽 반지름 비율이 0이면 원판, 아니면 고리).
        static Sprite Circle(float inner)
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            float radius = size * .5f, innerRadius = radius * inner;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(radius, radius));
                    float alpha = Mathf.Clamp01(radius - d) * (inner > 0f ? Mathf.Clamp01(d - innerRadius) : 1f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f);
        }
    }
}
