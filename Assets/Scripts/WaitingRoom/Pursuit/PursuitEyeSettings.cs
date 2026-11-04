using System;
using UnityEngine;

namespace CurtainCall.Settings
{
    [Serializable]
    public sealed class PursuitEyeSettings
    {
        public bool Enabled = true;
        [Tooltip("실제 흰 눈 그림의 월드 너비(m). 기본값은 마네킹 머리보다 조금 작습니다.")]
        [Min(.02f)] public float Width = .18f;
        [Tooltip("Head 뼈 위치에서 눈 중심까지의 높이(m).")]
        public float HeadOffset = .42f;
        [Min(0)] public float FadeSeconds = .5f;
        [Min(0)] public float TransitionSeconds = .5f;
        [ColorUsage(true, true)] public Color NormalColor = Color.white;
        [ColorUsage(true, true)] public Color AlertColor = new(1f, .78f, .12f, 1f);
        [ColorUsage(true, true)] public Color ChaseColor = new(.55f, .025f, .045f, 1f);
        [Min(0)] public float NormalEmission;
        [Min(0)] public float AlertEmission;
        [Min(0)] public float ChaseEmission = .35f;
        [Tooltip("발광 시 눈 주변의 약한 번짐. 0이면 HDR 밝기만 적용합니다. Bloom 없이도 보입니다.")]
        [Range(0, .5f)] public float GlowOpacity = .12f;

        public Color ColorFor(WaitingRoom.Pursuit.PursuitEyeState state) => state == WaitingRoom.Pursuit.PursuitEyeState.Chase
            ? ChaseColor : state == WaitingRoom.Pursuit.PursuitEyeState.Alert ? AlertColor : NormalColor;
        public float EmissionFor(WaitingRoom.Pursuit.PursuitEyeState state) => Mathf.Max(0, state == WaitingRoom.Pursuit.PursuitEyeState.Chase
            ? ChaseEmission : state == WaitingRoom.Pursuit.PursuitEyeState.Alert ? AlertEmission : NormalEmission);
    }
}
