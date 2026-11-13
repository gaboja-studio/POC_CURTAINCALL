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
        bool[] original, wasCut;
        bool cut;
        float blackoutUntil;
        // 정비 시간에 따로 끄는 무대 입구 통로 조명(Task-129). 회로에 없는 통로 조명도 여기서 함께 다룬다.
        readonly HashSet<Light> corridor = new();
        Light[] corridorExtra = System.Array.Empty<Light>();
        bool[] corridorExtraOriginal = System.Array.Empty<bool>();
        public bool IsCut => cut;
        /// <summary>정비 시간이라 무대 입구 통로 조명을 꺼 두었는가.</summary>
        public bool IsStageCorridorDark { get; private set; }
        void Awake()
        {
            original = new bool[circuit.Length];
            for (int i = 0; i < circuit.Length; i++) original[i] = circuit[i] != null && circuit[i].enabled;
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
            cut = CurtainCall.Settings.GameSettings.Base.Run.IsExplorationLocked(state) || state.Phase == CampaignPhase.Dawn;
            bool wasCorridorDark = IsStageCorridorDark;
            IsStageCorridorDark = corridor.Count > 0 && StageCorridorClosure.IsClosed(state);
            // 배선 상태는 모든 참가자에게 동기화되므로 각자 화면에서 같은 순간에 꺼진다.
            for (int i = 0; i < wasCut.Length; i++)
            {
                bool now = blackoutCables[i] != null && blackoutCables[i].IsCut;
                if (now && !wasCut[i]) blackoutUntil = Time.time + blackoutSeconds;
                wasCut[i] = now;
            }
            bool dark = cut || Time.time < blackoutUntil;
            bool corridorDark = dark || IsStageCorridorDark;
            for (int i = 0; i < circuit.Length; i++)
                if (circuit[i] != null) circuit[i].enabled = !(corridor.Contains(circuit[i]) ? corridorDark : dark) && original[i];
            // 회로 밖 통로 조명은 다른 연출이 켜고 끌 수 있어, 정비 시간에만 끄고 끝날 때 한 번 원래대로 돌린다.
            if (IsStageCorridorDark || wasCorridorDark)
                for (int i = 0; i < corridorExtra.Length; i++)
                    if (corridorExtra[i] != null) corridorExtra[i].enabled = !IsStageCorridorDark && corridorExtraOriginal[i];
        }
        void OnDisable()
        {
            if (original == null) return;
            for (int i = 0; i < circuit.Length; i++) if (circuit[i] != null) circuit[i].enabled = original[i];
            for (int i = 0; i < corridorExtra.Length; i++) if (corridorExtra[i] != null) corridorExtra[i].enabled = corridorExtraOriginal[i];
            cut = false; blackoutUntil = 0; IsStageCorridorDark = false;
        }
    }
}
