using System;
using UnityEngine;

namespace CurtainCall.Settings
{
    [Serializable]
    public sealed class PursuitEyeSettings
    {
        [InspectorName("눈 아이콘 표시")]
        [Tooltip("내가 조작하는 캐릭터 주변의 추격자 머리 위에 인식 상태 눈을 표시합니다.")]
        public bool Enabled = true;
        [InspectorName("눈 아이콘 너비 (m)")]
        [Tooltip("실제 흰 눈 그림의 월드 너비(m). 기본값은 마네킹 머리보다 조금 작습니다.")]
        [Min(.02f)] public float Width = .18f;
        [InspectorName("머리 위 높이 (m)")]
        [Tooltip("마네킹의 머리 뼈 위치에서 눈 아이콘 중심까지 위로 띄우는 거리입니다.")]
        public float HeadOffset = .42f;
        [InspectorName("표시·숨김 시간 (초)")]
        [Tooltip("일반·경계 눈이 나타나거나 인식 범위를 벗어나 사라질 때 투명도를 바꾸는 시간입니다. 0이면 즉시 표시·숨김. 빨간 공격 상태에 들어갈 때는 이 시간과 관계없이 즉시 표시합니다.")]
        [Min(0)] public float FadeSeconds = .5f;
        [InspectorName("눈 열림·상태 전환 시간 (초)")]
        [Tooltip("일반에서 경계로 눈을 뜨거나, 일반·경계 상태로 돌아올 때 눈 모양·색·추가 밝기가 변하는 시간입니다. 0이면 즉시 변경. 빨간 공격 준비/추격/포획 상태로 들어갈 때는 이 시간과 관계없이 즉시 변경합니다.")]
        [Min(0)] public float TransitionSeconds = .5f;
        [Header("상태별 색상")]
        [InspectorName("일반 색상")]
        [Tooltip("순찰·복귀 중 감은 눈의 색입니다. 기본은 흰색입니다.")]
        [ColorUsage(true, true)] public Color NormalColor = Color.white;
        [InspectorName("경계 색상")]
        [Tooltip("소리를 듣거나 조사·수색할 때 뜬 눈의 색입니다. 기본은 노란색입니다.")]
        [ColorUsage(true, true)] public Color AlertColor = new(1f, .78f, .12f, 1f);
        [InspectorName("공격 색상")]
        [Tooltip("나를 대상으로 공격 준비·추격·포획할 때 뜬 눈의 색입니다. 기본은 빨간색이며 상태 진입 즉시 적용합니다.")]
        [ColorUsage(true, true)] public Color ChaseColor = new(.55f, .025f, .045f, 1f);
        [Header("상태별 추가 밝기")]
        [InspectorName("일반 추가 밝기")]
        [Tooltip("일반 상태 눈 자체의 밝기를 더합니다. 0은 기본 색상 밝기이며 눈을 숨기지 않습니다. 주변을 비추는 조명은 아닙니다.")]
        [Min(0)] public float NormalEmission;
        [InspectorName("경계 추가 밝기")]
        [Tooltip("소리 감지·조사·수색 중인 경계 눈 자체의 밝기를 더합니다. 0은 기본 색상 밝기이며 눈을 숨기지 않습니다.")]
        [Min(0)] public float AlertEmission;
        [InspectorName("공격 추가 밝기")]
        [Tooltip("나를 공격 준비·추격·포획하는 빨간 눈 자체의 밝기를 더합니다. 0은 기본 색상 밝기, 값이 클수록 더 밝습니다. 주변을 비추는 조명은 아닙니다.")]
        [Min(0)] public float ChaseEmission = .35f;
        [InspectorName("눈 주변 번짐 강도")]
        [Tooltip("추가 밝기가 있는 눈의 테두리 주변에 흐릿한 빛을 더합니다. 0이면 번짐만 끄며 눈 자체의 밝기는 유지합니다. 해당 상태의 추가 밝기가 0이면 이 번짐도 나타나지 않습니다.")]
        [Range(0, .5f)] public float GlowOpacity = .12f;

        public Color ColorFor(WaitingRoom.Pursuit.PursuitEyeState state) => state == WaitingRoom.Pursuit.PursuitEyeState.Chase
            ? ChaseColor : state == WaitingRoom.Pursuit.PursuitEyeState.Alert ? AlertColor : NormalColor;
        public float EmissionFor(WaitingRoom.Pursuit.PursuitEyeState state) => Mathf.Max(0, state == WaitingRoom.Pursuit.PursuitEyeState.Chase
            ? ChaseEmission : state == WaitingRoom.Pursuit.PursuitEyeState.Alert ? AlertEmission : NormalEmission);
    }
}
