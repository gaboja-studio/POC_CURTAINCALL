using CurtainCall.Campaign;
using CurtainCall.EscapeRoute;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    // Scene-owned circuit: preserve original light states, including deliberately unlit lamps.
    public sealed class NightPower : MonoBehaviour
    {
        [SerializeField] Light[] circuit;
        [Tooltip("이 배선이 잘리는 순간 회로 전체가 한 번 꺼졌다가 다시 켜집니다.")]
        [SerializeField] EscapeCuttableCable[] blackoutCables = System.Array.Empty<EscapeCuttableCable>();
        [SerializeField, Min(0)] float blackoutSeconds = 3f;
        [Tooltip("이 탐색 대상이 열리는 순간(회로 고장) 회로 전체가 잠깐 깜빡거리다가 원래대로 돌아온다(2026-11-14 사용자).")]
        [SerializeField] string[] flickerOnOpen = System.Array.Empty<string>();
        [SerializeField, Min(0)] float flickerSeconds = 3f;
        bool[] flickerWas; bool flickerSeen; float flickerUntil;
        [Header("밤 조명(2026-11-13 사용자: 끄지 않고 어두운 푸른 톤의 은은한 빛만 남긴다)")]
        [Tooltip("밤에 회로 조명이 띠는 색.")]
        [SerializeField] Color nightColor = new(.22f, .38f, 1f);
        [Tooltip("밤에 남기는 밝기 비율(원래 밝기 대비). 0이면 예전처럼 완전히 꺼진다.")]
        [SerializeField, Range(0f, 1f)] float nightBrightness = .2f;
        bool[] original, wasCut;
        float[] originalIntensity;
        Color[] dayColor;
        bool cut, dimmed;
        float blackoutUntil;
        public bool IsCut => cut;
        void Awake()
        {
            original = new bool[circuit.Length];
            originalIntensity = new float[circuit.Length];
            dayColor = new Color[circuit.Length];
            for (int i = 0; i < circuit.Length; i++)
            {
                original[i] = circuit[i] != null && circuit[i].enabled;
                if (circuit[i] != null) originalIntensity[i] = circuit[i].intensity;
            }
            wasCut = new bool[blackoutCables.Length];
        }
        void LateUpdate()
        {
            var state = CampaignRun.Instance != null ? CampaignRun.Instance.State : default;
            bool night = CurtainCall.Settings.GameSettings.Base.Run.IsExplorationLocked(state);
            cut = night || state.Phase == CampaignPhase.Dawn;
            // 밤에는 끄지 않고 어둡고 푸르게 낮춘다. 새벽(추격)과 배선 정전은 예전처럼 완전히 끈다.
            bool dim = night && state.Phase != CampaignPhase.Dawn && nightBrightness > 0f;
            if (dim != dimmed) SetDimmed(dim);
            if (dim)
                for (int i = 0; i < circuit.Length; i++)
                    if (circuit[i] != null) { circuit[i].color = nightColor; circuit[i].intensity = originalIntensity[i] * nightBrightness; }
            // 배선 상태는 모든 참가자에게 동기화되므로 각자 화면에서 같은 순간에 꺼진다.
            for (int i = 0; i < wasCut.Length; i++)
            {
                bool now = blackoutCables[i] != null && blackoutCables[i].IsCut;
                if (now && !wasCut[i]) blackoutUntil = Time.time + blackoutSeconds;
                wasCut[i] = now;
            }
            // 들어왔을 때 이미 열려 있던 것은 깜빡이지 않는다.
            if (flickerOnOpen.Length > 0 && ExplorationState.Instance != null)
            {
                flickerWas ??= new bool[flickerOnOpen.Length];
                for (int i = 0; i < flickerOnOpen.Length; i++)
                {
                    bool now = ExplorationState.Instance.IsOpen(flickerOnOpen[i]);
                    if (flickerSeen && now && !flickerWas[i]) flickerUntil = Time.time + flickerSeconds;
                    flickerWas[i] = now;
                }
                flickerSeen = true;
            }
            bool flickerOff = Time.time < flickerUntil && Mathf.PerlinNoise(Time.time * 11f, 2.3f) < .5f;
            // 조명만 꺼서는 주변광 때문에 거의 티가 나지 않는다(기계실). 정전·깜빡임 동안에는 주변광도 함께 낮춘다(2026-11-14 사용자: 탈출로 배선 정전이 안 보임).
            SetAmbientDimmed(Time.time < blackoutUntil || flickerOff);
            bool dark = (cut && !dim) || Time.time < blackoutUntil || flickerOff;
            for (int i = 0; i < circuit.Length; i++) if (circuit[i] != null) circuit[i].enabled = !dark && original[i];
        }
        // 밤에 들어갈 때의 색을 기억했다가 아침에 돌려놓는다. 밝기는 처음 값으로 돌린다(깜박임 조명은 스스로 다시 정한다).
        void SetDimmed(bool on)
        {
            dimmed = on;
            for (int i = 0; i < circuit.Length; i++)
            {
                if (circuit[i] == null) continue;
                if (on) dayColor[i] = circuit[i].color;
                else { circuit[i].color = dayColor[i]; circuit[i].intensity = originalIntensity[i]; }
            }
        }
        [Tooltip("배선 정전·회로 깜빡임 동안 남기는 주변광 비율.")]
        [SerializeField, Range(0f, 1f)] float blackoutAmbient = .12f;
        bool ambientDimmed; Color ambientSky, ambientEquator, ambientGround;
        float ambientIntensity, reflectionIntensity; UnityEngine.Rendering.SphericalHarmonicsL2 ambientProbe;
        // 추격 조명(PursuitLighting)과 같은 항목을 낮춘다. 시작할 때 값을 기억했다가 끝나면 그대로 돌려놓는다.
        void SetAmbientDimmed(bool on)
        {
            if (on == ambientDimmed) return;
            ambientDimmed = on;
            if (on)
            {
                ambientSky = RenderSettings.ambientSkyColor; ambientEquator = RenderSettings.ambientEquatorColor; ambientGround = RenderSettings.ambientGroundColor;
                ambientIntensity = RenderSettings.ambientIntensity; ambientProbe = RenderSettings.ambientProbe; reflectionIntensity = RenderSettings.reflectionIntensity;
            }
            float m = on ? blackoutAmbient : 1f;
            RenderSettings.ambientSkyColor = ambientSky * m; RenderSettings.ambientEquatorColor = ambientEquator * m; RenderSettings.ambientGroundColor = ambientGround * m;
            RenderSettings.ambientIntensity = ambientIntensity * m; RenderSettings.ambientProbe = ambientProbe * m; RenderSettings.reflectionIntensity = reflectionIntensity * m;
        }
        void OnDisable()
        {
            SetAmbientDimmed(false);
            if (original == null) return;
            if (dimmed) SetDimmed(false);
            for (int i = 0; i < circuit.Length; i++) if (circuit[i] != null) circuit[i].enabled = original[i];
            cut = false; blackoutUntil = 0;
        }
    }
}
