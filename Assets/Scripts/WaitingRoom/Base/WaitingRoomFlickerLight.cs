using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 대기실 철망 전구의 낡은 깜빡임. 평소엔 약하게 숨 쉬듯 흔들리고, 가끔 몇 번 툭툭 꺼졌다 켜진다.
    /// 필라멘트 잔열처럼 밝기가 목표값을 짧게 따라가므로(꺼질 때 빠르게·켜질 때 조금 느리게) 프레임 단위로 번쩍이지 않고,
    /// 주사율이 달라도 같은 모양으로 보인다. 전구 발광은 재질 원래 발광색에 같은 비율을 곱해 색이 튀지 않는다.
    /// 프로토타입의 <c>FlickerLight</c>(매 프레임 무작위 꺼짐·켜짐)를 대신한다.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public sealed class WaitingRoomFlickerLight : MonoBehaviour
    {
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        [Tooltip("밝기와 함께 발광이 줄어드는 전구 렌더러.")]
        public Renderer[] bulbs;

        [Tooltip("평균 몇 초마다 깜빡임 묶음이 오는가.")]
        [Min(1f)] public float averageGap = 7f;

        [Tooltip("한 묶음 안의 꺼짐 횟수(최소·최대).")]
        public Vector2Int dipsPerBurst = new Vector2Int(1, 4);

        [Tooltip("꺼졌을 때 밝기 범위(원래 밝기 대비).")]
        public Vector2 dipLevel = new Vector2(0.08f, 0.45f);

        [Tooltip("평소 흔들림 폭.")]
        [Range(0f, 0.2f)] public float hum = 0.05f;

        const float FallTime = 0.025f; // 꺼질 때 따라가는 시간
        const float RiseTime = 0.06f;  // 다시 켜질 때(잔열)

        Light _light;
        float _baseIntensity;
        Color _baseEmission;
        MaterialPropertyBlock _block;
        float _seed, _level = 1f, _target = 1f, _nextBurst, _stepEnd;
        int _dipsLeft;
        bool _dipped;

        void Awake()
        {
            _light = GetComponent<Light>();
            _baseIntensity = _light.intensity;
            _block = new MaterialPropertyBlock();
            _seed = Random.value * 100f;
            var source = bulbs != null && bulbs.Length > 0 && bulbs[0] ? bulbs[0].sharedMaterial : null;
            _baseEmission = source != null && source.HasProperty(EmissionId) ? source.GetColor(EmissionId) : Color.black;
            _nextBurst = Time.time + Random.Range(1f, averageGap * 2f); // 여러 전구가 같이 시작하지 않게
        }

        void Update()
        {
            float t = Time.time;
            if (_dipsLeft == 0 && t >= _nextBurst)
            {
                _dipsLeft = Random.Range(dipsPerBurst.x, dipsPerBurst.y + 1);
                _stepEnd = t;
            }

            if (_dipsLeft > 0 && t >= _stepEnd)
            {
                _dipped = !_dipped;
                if (_dipped)
                {
                    _target = Random.Range(dipLevel.x, dipLevel.y);
                    _stepEnd = t + Random.Range(0.04f, 0.14f);
                }
                else
                {
                    _target = 1f;
                    _stepEnd = t + Random.Range(0.05f, 0.25f);
                    if (--_dipsLeft == 0) _nextBurst = t + averageGap * Random.Range(0.5f, 1.5f);
                }
            }

            float tau = _target < _level ? FallTime : RiseTime;
            _level = Mathf.Lerp(_level, _target, 1f - Mathf.Exp(-Time.deltaTime / tau));
            float wobble = 1f - hum * Mathf.PerlinNoise(_seed, t * 1.7f);
            Apply(_level * wobble);
        }

        void Apply(float level)
        {
            _light.intensity = _baseIntensity * level;
            if (bulbs == null) return;
            _block.SetColor(EmissionId, _baseEmission * level);
            foreach (var r in bulbs)
                if (r) r.SetPropertyBlock(_block);
        }

        void OnDisable()
        {
            if (_light) _light.intensity = _baseIntensity;
            if (bulbs == null) return;
            foreach (var r in bulbs)
                if (r) r.SetPropertyBlock(null);
        }
    }
}
