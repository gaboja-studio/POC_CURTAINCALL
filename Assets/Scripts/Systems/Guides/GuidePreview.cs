using System;
using CurtainCall.UI.GuidesV1;
using TMPro;
using UnityEngine;

namespace CurtainCall.Guides
{
    /// <summary>Synthetic manual display samples, not bindings or game context detection.</summary>
    public sealed class GuidePreview : MonoBehaviour
    {
        [SerializeField] GuideView guide;
        [SerializeField] TMP_Text status;
        [SerializeField] Camera previewCamera;
        bool paused, light;
        float previousScale = 1;
        public GuideView Guide => guide;
        public int InvocationCount { get; private set; }
        public const string LongDescription = "긴 행동 안내 문구는 한 줄의 고정 영역을 넘으면 말줄임 처리됩니다. 글자 크기와 가이드 크기는 바뀌지 않습니다. 실제 입력이나 게임 연결은 없는 확인 예시입니다.";
        public void Configure(GuideView view, TMP_Text label, Camera camera) { guide = view; status = label; previewCamera = camera; }
        void Awake() { ResetState(); }
        void OnDisable() { RestoreTime(); }
        void OnDestroy() { RestoreTime(); }
        void OnApplicationQuit() { RestoreTime(); }
        void Run(Action action)
        {
            InvocationCount++;
            try { action(); UpdateStatus(); }
            catch { RestoreTime(); throw; }
        }
        public void Normal() => Run(() => guide.ShowKey("F", "행동 안내"));
        public void Wide() => Run(() => guide.ShowKey("Space", "점프", GuideKeyWidth.Wide));
        public void ExtraWide() => Run(() => guide.ShowKey("Left Shift", "달리기", GuideKeyWidth.ExtraWide));
        public void MouseLeft() => Run(() => guide.ShowMouse(GuideMouseInput.LeftClick, "주 행동"));
        public void MouseRight() => Run(() => guide.ShowMouse(GuideMouseInput.RightClick, "보조 행동"));
        public void MouseMove() => Run(() => guide.ShowMouse(GuideMouseInput.Move, "둘러보기"));
        public void LongText() => Run(() => guide.ShowKey("아주 긴 키 이름", LongDescription, GuideKeyWidth.ExtraWide));
        public void Literal() => Run(() => guide.ShowKey("<b>F</b>", @"<color=red>행동</color> \n 문자 그대로 …"));
        public void Invalid() => Run(() => {
            guide.ShowKey("F", "제어\r\n문자\t안내" + (char)0x2028 + "한 줄" + (char)0x2029 + "표시");
            guide.ShowKey(null, null, (GuideKeyWidth)(-1));
            guide.ShowMouse((GuideMouseInput)(-1), null);
        });
        public void RepeatShow() => Run(() => { guide.ShowKey("F", "반복 표시"); guide.ShowKey("F", "반복 표시"); });
        public void Block() => Run(() => { guide.SetContextBlocked(true); guide.ShowKey("F", "차단 중 표시 거부"); });
        public void Unblock() => Run(() => guide.SetContextBlocked(false));
        public void Hide() => Run(() => guide.Hide());
        public void HideImmediate() => Run(() => guide.HideImmediate());
        public void TogglePause() => Run(() => {
            if (paused) RestoreTime();
            else if (Application.isPlaying && isActiveAndEnabled) { previousScale = Time.timeScale; paused = true; Time.timeScale = 0; }
        });
        public void ToggleBackground() => Run(() => { light = !light; ApplyBackground(); });
        public void ResetPreview() => Run(ResetState);
        void ResetState() { RestoreTime(); light = false; ApplyBackground(); if (guide != null) guide.ResetForRuntime(); UpdateStatus(); }
        void RestoreTime() { if (!paused) return; Time.timeScale = previousScale; paused = false; }
        void ApplyBackground()
        {
            if (previewCamera != null) previewCamera.backgroundColor = light ? new Color32(224, 226, 218, 255) : new Color32(14, 24, 28, 255);
            if (status != null) status.color = light ? new Color32(14, 24, 28, 255) : new Color32(240, 238, 220, 255);
        }
        void UpdateStatus()
        {
            if (status != null) status.text = "C7 / 조작 가이드 · 합성 표시 / 게임 연결 없음 · " +
                (guide == null ? "미설정" : guide.IsContextBlocked ? "차단" : guide.HasRuntimePresentation ? "표시" : "숨김") +
                (paused ? " · 시간 정지" : "");
        }
    }
}
