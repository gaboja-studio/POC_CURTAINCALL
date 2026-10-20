using System;
using CurtainCall.UI.TooltipsV1;
using TMPro;
using UnityEngine;

namespace CurtainCall.Tooltips
{
    public sealed class TooltipPreview : MonoBehaviour
    {
        [SerializeField] TooltipView tooltip;
        [SerializeField] TMP_Text status;
        [SerializeField] Camera previewCamera;
        bool paused, light;
        float previousScale = 1;
        public int InvocationCount { get; private set; }
        public TooltipView Tooltip => tooltip;
        public const string LongDescription = "설명 내용의 길이에 따라 받침 높이가 늘어납니다.\n한계 높이 이후에는 말줄임 처리됩니다. 글자 크기는 줄이지 않습니다. 이 문구는 긴 내용을 확인하기 위한 예시입니다. 아래 꼬리는 고정 크기로 유지됩니다.";
        public static readonly Vector2 NearbyPosition = new Vector2(48, 408);
        public static readonly Vector2 PanelPosition = new Vector2(1184, 400);
        public void Configure(TooltipView view, TMP_Text label, Camera camera) { tooltip = view; status = label; previewCamera = camera; }
        void Awake() { ResetState(); }
        void OnDisable() { RestoreTime(); }
        void OnDestroy() { RestoreTime(); }
        void Run(Action action)
        {
            InvocationCount++;
            try { action(); UpdateStatus(); }
            catch { RestoreTime(); throw; }
        }
        public void Nearby() => Run(() => tooltip.ShowNearby("낡은 수화기", "필요한 물품을 주문합니다.\n배달은 잠시 뒤 도착합니다.", NearbyPosition, 384, 96));
        public void Panel() => Run(() => tooltip.ShowPanel("빵", "품목의 효과와 사용 조건을\n이곳에 표시합니다.", PanelPosition));
        public void Narrow() => Run(() => tooltip.ShowNearby("긴 품목 설명이 표시되는 예제", LongDescription, NearbyPosition, 256));
        public void Wide() => Run(() => tooltip.ShowNearby("긴 품목 설명이 표시되는 예제", LongDescription, NearbyPosition, 480));
        public void PanelLong() => Run(() => tooltip.ShowPanel("긴 품목 설명이 표시되는 예제", LongDescription, PanelPosition, 384));
        public void WidthLow() => Run(() => tooltip.ShowNearby("최소 폭", LongDescription, NearbyPosition, -100));
        public void WidthHigh() => Run(() => tooltip.ShowNearby("최대 폭", LongDescription, NearbyPosition, 10000));
        public void TailLeft() => Run(() => tooltip.ShowNearby("낡은 수화기", "아래 꼬리는 고정 크기로 유지됩니다.", NearbyPosition, 384, -100));
        public void TailRight() => Run(() => tooltip.ShowNearby("낡은 수화기", "아래 꼬리는 고정 크기로 유지됩니다.", NearbyPosition, 384, 10000));
        public void TailCenter() => Run(() => tooltip.ShowNearby("낡은 수화기", "아래 꼬리는 고정 크기로 유지됩니다.", NearbyPosition));
        public void Move() => Run(() => tooltip.SetPosition(new Vector2(640, 400)));
        public void SetTail() => Run(() => tooltip.SetTailCenter(160));
        public void RepeatShow() => Run(() => { tooltip.ShowNearby("낡은 수화기", LongDescription, NearbyPosition); tooltip.ShowNearby("낡은 수화기", LongDescription, NearbyPosition); });
        public void Controls() => Run(() => tooltip.ShowNearby("낡은\r\n수화기\t예제", "첫 줄\r\n둘째 줄\r셋째 줄" + (char)0x2028 + "넷째 줄" + (char)0x2029 + "다섯째 줄\t예제", NearbyPosition));
        public void Literal() => Run(() => tooltip.ShowNearby("<b>낡은 수화기</b>", @"<color=red>물품</color> \n 문자 …", NearbyPosition));
        public void Hide() => Run(() => tooltip.Hide());
        public void HideImmediate() => Run(() => tooltip.HideImmediate());
        public void TogglePause() => Run(() => { if (paused) RestoreTime(); else { previousScale = Time.timeScale; paused = true; Time.timeScale = 0; } });
        public void ToggleBackground() => Run(() => { light = !light; ApplyBackground(); });
        public void ResetPreview() => Run(ResetState);
        void ResetState() { RestoreTime(); light = false; ApplyBackground(); if (tooltip != null) tooltip.ResetForRuntime(); UpdateStatus(); }
        void RestoreTime() { if (!paused) return; Time.timeScale = previousScale; paused = false; }
        void ApplyBackground() { if (previewCamera != null) previewCamera.backgroundColor = light ? new Color32(224, 226, 218, 255) : new Color32(14, 24, 28, 255); }
        void UpdateStatus()
        {
            if (status != null) status.text = "C15 / 패널 품목 설명 · 합성 표시 / 게임 연결 없음 · " + (tooltip == null ? "미설정" : tooltip.Mode + " " + tooltip.BodySizeUi) + (paused ? " · 시간 정지" : "");
        }
    }
}
