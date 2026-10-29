using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>
    /// 균형 위험 게이지가 기준 이상 차면 게이지의 흰 테두리를 빨강으로 깜빡여 알린다.
    /// 외부 HUD(P1 Balance) 코드는 바꾸지 않는다. 기존 트랙 색을 바꾸고, 같은 자리에 두께를 정할 수 있는 테두리를 겹친다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BalanceRiskBlink : MonoBehaviour
    {
        const string TrackName = "60_DwellTrack";
        const string FillName = "61_DwellFill";
        const string RingName = "60_DwellTrackWarning";

        [Tooltip("이 비율 이상 차면 깜빡이기 시작한다.")]
        [SerializeField, Range(0f, 1f)] float threshold = 0.7f;
        [Tooltip("기준에 막 닿았을 때와 가득 찼을 때의 깜빡임 횟수(초당).")]
        [SerializeField] Vector2 blinkHz = new(3f, 7f);
        [Tooltip("깜빡일 때 테두리 색.")]
        [SerializeField] Color color = new(1f, 0.08f, 0.05f, 1f);
        [Tooltip("테두리 두께(px). 트랙 바깥선에서 안쪽으로 그린다. 채움 막대까지 여백은 4px.")]
        [SerializeField, Range(1f, 6f)] float thickness = 4f;

        Graphic track;
        Image fill;
        HudRing ring;
        Color normal;
        float phase;
        bool blinking;

        public bool IsBlinking => blinking;

        void Awake()
        {
            var trackRect = transform.Find(TrackName);
            var fillRect = transform.Find(FillName);
            if (trackRect == null || fillRect == null || !trackRect.TryGetComponent(out track) || !fillRect.TryGetComponent(out fill))
            {
                enabled = false;
                return;
            }
            normal = track.color;
            // 모서리 반경은 트랙 둥근 모서리에 맞춘다.
            ring = new HudRing(trackRect, RingName, thickness, 6f);
            ring.Image.gameObject.SetActive(false);
        }

        void Update()
        {
            float amount = fill.fillAmount;
            bool warn = amount >= threshold;
            if (blinking != warn)
            {
                blinking = warn;
                phase = 0f;
                ring.Image.gameObject.SetActive(warn);
                if (!warn) track.color = normal;
            }
            if (!warn) return;
            // 기준에서 가득 찰수록 빨라진다. 위상을 누적해 속도가 바뀌어도 튀지 않는다. 0에서 시작해 바로 빨강을 보인다.
            float urgency = Mathf.InverseLerp(threshold, 1f, amount);
            phase += Time.unscaledDeltaTime * Mathf.Lerp(blinkHz.x, blinkHz.y, urgency);
            float pulse = 0.5f + 0.5f * Mathf.Cos(phase * Mathf.PI * 2f);
            track.color = Color.Lerp(normal, color, pulse);
            var c = color;
            c.a *= pulse;
            ring.Image.color = c;
        }

        void OnDisable()
        {
            if (track != null && blinking) track.color = normal;
            if (ring?.Image != null) ring.Image.gameObject.SetActive(false);
            blinking = false;
        }

        void OnDestroy() => ring?.Dispose();
    }
}
