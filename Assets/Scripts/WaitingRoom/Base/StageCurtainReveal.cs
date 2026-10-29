using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// ⑦ 공연 스테이지 입구 연출. 집결 구역(<see cref="StageEntranceGather"/>)에 한 명이라도 들어오면
    /// 붉은 천 커튼을 좌우로 걷고 뒤의 황금빛·빛줄기·조명을 켜고 금가루를 바닥에 뿌린다. 모두 떠나면 다시 닫고 빛을 줄인다.
    /// 인원은 집결 판정이 세는 위치 기반 값을 그대로 쓰므로 원격 캐릭터도 각 화면에서 같은 결과로 보인다. 공연 전환에는 관여하지 않는다.
    /// </summary>
    public sealed class StageCurtainReveal : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("인원을 세는 무대 입구 집결 판정.")]
        public StageEntranceGather gather;
        [Tooltip("좌우로 걷히는 천 커튼.")]
        public CurtainController curtain;

        [Header("황금빛")]
        [Tooltip("열릴 때 켜지는 조명. 저장된 세기가 최대치다.")]
        public Light[] lights;
        [Tooltip("커튼 뒤 황금빛 면. 닫혀 있어도 희미하게 남는다. _BaseColor에 밝기를 곱한다.")]
        public Renderer[] glows;
        [Tooltip("커튼 앞으로 뻗는 빛줄기. 닫히면 완전히 꺼진다.")]
        public Renderer[] beams;
        [ColorUsage(true, true)] public Color glowColor = new(4f, 2.6f, 0.9f, 1f);
        [Tooltip("닫혀 있을 때 남는 밝기(커튼 틈으로 비치는 정도).")]
        [Range(0f, 1f)] public float closedGlow = 0.12f;
        [Tooltip("빛이 차오르고 줄어드는 시간(초).")]
        public float fadeSeconds = 1.4f;

        [Header("금가루")]
        [Tooltip("열려 있는 동안 계속 흩뿌리는 입자.")]
        public ParticleSystem[] dust;
        [Tooltip("열리는 순간 한 번 터지는 입자.")]
        public ParticleSystem[] bursts;

        float[] maxIntensity;
        MaterialPropertyBlock block;
        float level;
        bool open;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            maxIntensity = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++)
                if (lights[i] != null) maxIntensity[i] = lights[i].intensity;
            foreach (var ps in dust) if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var ps in bursts) if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Apply(0f);
        }

        void Update()
        {
            bool someone = gather != null && gather.isActiveAndEnabled && gather.InsideCount > 0;
            if (someone != open) SetOpen(someone);
            float target = open ? 1f : 0f;
            if (!Mathf.Approximately(level, target))
                Apply(Mathf.MoveTowards(level, target, Time.deltaTime / Mathf.Max(0.01f, fadeSeconds)));
        }

        void SetOpen(bool value)
        {
            open = value;
            if (curtain != null)
            {
                if (open)
                {
                    // 접속 뒤 생긴 캐릭터도 천을 밀 수 있게 다시 등록한다.
                    curtain.RegisterColliders();
                    curtain.Open();
                }
                else curtain.Rest();
            }
            foreach (var ps in dust)
            {
                if (ps == null) continue;
                if (open) ps.Play(true);
                else ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            if (!open) return;
            foreach (var ps in bursts)
                if (ps != null) { ps.Clear(true); ps.Play(true); }
        }

        void Apply(float value)
        {
            level = value;
            float eased = value * value * (3f - 2f * value);
            for (int i = 0; i < lights.Length; i++)
            {
                var l = lights[i];
                if (l == null) continue;
                l.intensity = maxIntensity[i] * eased;
                l.enabled = eased > 0.001f;
            }
            Tint(glows, Mathf.Lerp(closedGlow, 1f, eased));
            Tint(beams, eased);
        }

        void Tint(Renderer[] renderers, float brightness)
        {
            var color = glowColor * brightness;
            color.a = 1f;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.enabled = brightness > 0.001f;
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, color);
                r.SetPropertyBlock(block);
            }
        }
    }
}
