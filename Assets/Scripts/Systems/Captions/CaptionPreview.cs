using System;
using CurtainCall.UI.CaptionsV1;
using TMPro;
using UnityEngine;

namespace CurtainCall.Captions
{
    /// <summary>실게임과 연결하지 않는 C9 수동 확인 예시입니다.</summary>
    public sealed class CaptionPreview : MonoBehaviour
    {
        [SerializeField] CaptionView captions;
        [SerializeField] TMP_Text status;
        [SerializeField] Camera previewCamera;
        bool paused, light, wide;
        float previousScale = 1;
        string result = "준비";
        public CaptionView Captions => captions;
        public int InvocationCount { get; private set; }
        public const string DialogueBody = "잠깐, 저쪽 문 뒤에서 무슨 소리가 났어.";
        public const string BroadcastBody = "공연 준비가 끝났습니다.\n단원들은 무대로 모여 주십시오.";
        public const string NoSpeakerBody = "커튼 너머로 낡은 오르골 소리가 들려온다.";
        public const string LongSpeaker = "아주 긴 화자 이름은 화자 칸 안에서 한 줄로 말줄임됩니다";
        public const string LiteralBody = @"<b>대사</b> \n 문자 그대로 …";
        public static string OverflowBody => new string('가', 800);
        public void Configure(CaptionView view, TMP_Text label, Camera camera) { captions = view; status = label; previewCamera = camera; }
        void Awake() { ResetState(); }
        void OnDisable() { RestoreTime(); }
        void OnDestroy() { RestoreTime(); }
        void OnApplicationQuit() { RestoreTime(); }
        void Run(Action action)
        {
            if (!Application.IsPlaying(gameObject) || !isActiveAndEnabled) return;
            InvocationCount++;
            try { action(); UpdateStatus(); }
            catch { RestoreTime(); throw; }
        }
        void Result(bool accepted) { result = accepted ? "성공" : "거부: " + captions.LastIssue; }
        public void Dialogue() => Run(() => Result(captions.TryShowDialogue("단원 2", DialogueBody)));
        public void NoSpeaker() => Run(() => Result(captions.TryShowDialogue("", NoSpeakerBody)));
        public void Broadcast() => Run(() => Result(captions.TryShowBroadcast("단장 방송", BroadcastBody)));
        public void DefaultSpeaker() => Run(() => Result(captions.TryShowBroadcast("", "방송 화자 기본값 확인")));
        public void LongName() => Run(() => Result(captions.TryShowDialogue(LongSpeaker, "화자 이름만 한 줄 말줄임 확인")));
        public void ExplicitTwoLines() => Run(() => Result(captions.TryShowDialogue("단원 2", "첫 번째 줄입니다.\n두 번째 줄입니다.")));
        public void OverflowRejection() => Run(() => Result(captions.TryShowDialogue("단원 2", OverflowBody)));
        public void EmptyRejection() => Run(() => Result(captions.TryShowDialogue("단원 2", " \t\r\n")));
        public void CanFit() => Run(() => Result(captions.CanFit(CaptionKind.Broadcast, "", BroadcastBody)));
        public void WidthRange() => Run(() => { wide = !wide; Result(captions.TrySetWidths(wide ? 1536 : 960, wide ? 320 : 128)); });
        public void InvalidWidth() => Run(() => Result(captions.TrySetWidths(float.NaN, 176)));
        public void Hide() => Run(() => captions.Hide());
        public void HideImmediate() => Run(() => captions.HideImmediate());
        public void Reverse() => Run(() => { captions.Hide(); Result(captions.TryShowDialogue("단원 2", "퇴장 도중 다시 표시")); });
        public void Repeat() => Run(() => { captions.TryShowDialogue("단원 2", "반복 표시"); Result(captions.TryShowDialogue("단원 2", "반복 표시")); });
        public void Literals() => Run(() => Result(captions.TryShowDialogue("단원\r\n2", LiteralBody)));
        public void TogglePause() => Run(() => { if (paused) RestoreTime(); else { previousScale = Time.timeScale; paused = true; Time.timeScale = 0; } });
        public void ToggleBackground() => Run(() => { light = !light; ApplyBackground(); });
        public void ResetPreview() => Run(ResetState);
        void ResetState()
        {
            RestoreTime(); light = wide = false; result = "초기화"; ApplyBackground();
            if (captions != null) { captions.ResetForRuntime(); captions.TrySetWidths(1120, 176); }
            UpdateStatus();
        }
        void RestoreTime() { if (!paused) return; Time.timeScale = previousScale; paused = false; }
        void ApplyBackground()
        {
            if (previewCamera != null) previewCamera.backgroundColor = light ? new Color32(224, 226, 218, 255) : new Color32(14, 24, 28, 255);
            if (status != null) status.color = light ? new Color32(14, 24, 28, 255) : new Color32(240, 238, 220, 255);
        }
        void UpdateStatus()
        {
            if (status != null) status.text = "C9 · 합성 예시 / 게임 연결 없음 · 입력 " + InvocationCount + "회 · " +
                (captions == null ? "미설정" : captions.HasRuntimePresentation ? "표시" : "숨김") + " · " + result + (paused ? " · 시간 정지" : "");
        }
    }
}
