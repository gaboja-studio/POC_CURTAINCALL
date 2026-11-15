using System.Collections.Generic;
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
        // 정비 시간에 따로 끄는 무대 입구 통로 조명(Task-129). 회로에 없는 통로 조명도 여기서 함께 다룬다.
        readonly HashSet<Light> corridor = new();
        Light[] corridorExtra = System.Array.Empty<Light>();
        bool[] corridorExtraOriginal = System.Array.Empty<bool>();
        public bool IsCut => cut;
        bool allCutSeen, allCutWas;
        static float BlackoutUntilUnscaled;
        /// <summary>배선 정전 연출이 진행 중인가. 나가는 길 개방 연출이 이 뒤에 이어지도록 기다린다.</summary>
        public static bool BlackoutPlaying => Time.unscaledTime < BlackoutUntilUnscaled;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetBlackout() => BlackoutUntilUnscaled = 0f;
        /// <summary>정비 시간이라 무대 입구 통로 조명을 꺼 두었는가.</summary>
        public bool IsStageCorridorDark { get; private set; }
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
            corridor.UnionWith(StageCorridorClosure.CorridorLights(transform.parent));
            var inCircuit = new HashSet<Light>(circuit);
            var extra = new List<Light>();
            foreach (var light in corridor) if (light != null && !inCircuit.Contains(light)) extra.Add(light);
            corridorExtra = extra.ToArray();
            corridorExtraOriginal = new bool[corridorExtra.Length];
            for (int i = 0; i < corridorExtra.Length; i++) corridorExtraOriginal[i] = corridorExtra[i].enabled;
        }
        void Start() => StageCorridorClosure.InstallGate(transform);
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
            bool wasCorridorDark = IsStageCorridorDark;
            IsStageCorridorDark = corridor.Count > 0 && StageCorridorClosure.IsClosed(state);
            // 배선 상태는 모든 참가자에게 동기화되므로 각자 화면에서 같은 순간에 꺼진다.
            // 정전은 선 하나마다가 아니라 모든 선이 끊어졌을 때 한 번만 난다(2026-11-15 사용자). 들어왔을 때 이미 다 끊겨 있었으면 나지 않는다.
            bool allCut = wasCut.Length > 0;
            for (int i = 0; i < wasCut.Length; i++) allCut &= blackoutCables[i] != null && blackoutCables[i].IsCut;
            if (allCutSeen && allCut && !allCutWas) { blackoutUntil = Time.time + blackoutSeconds; BlackoutUntilUnscaled = Mathf.Max(BlackoutUntilUnscaled, Time.unscaledTime + blackoutSeconds); }
            allCutSeen = true; allCutWas = allCut;
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
            bool corridorDark = dark || IsStageCorridorDark;
            for (int i = 0; i < circuit.Length; i++)
                if (circuit[i] != null) circuit[i].enabled = !(corridor.Contains(circuit[i]) ? corridorDark : dark) && original[i];
            // 회로 밖 통로 조명은 다른 연출이 켜고 끌 수 있어, 정비 시간에만 끄고 끝날 때 한 번 원래대로 돌린다.
            if (IsStageCorridorDark || wasCorridorDark)
                for (int i = 0; i < corridorExtra.Length; i++)
                    if (corridorExtra[i] != null) corridorExtra[i].enabled = !IsStageCorridorDark && corridorExtraOriginal[i];
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
            for (int i = 0; i < corridorExtra.Length; i++) if (corridorExtra[i] != null) corridorExtra[i].enabled = corridorExtraOriginal[i];
            cut = false; blackoutUntil = 0; IsStageCorridorDark = false;
        }
    }
}
