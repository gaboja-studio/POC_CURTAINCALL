using System;
using CurtainCall.UI.SpectatorV1;
using CurtainCall.UI.GuidesV1;
using TMPro;
using UnityEngine;

namespace CurtainCall.Spectator
{
    /// <summary>Explicit synthetic preview only; never a game HUD arbiter or input binding.</summary>
    public sealed class SpectatorPreview : MonoBehaviour
    {
        [SerializeField] SpectatorView spectator;
        [SerializeField] TMP_Text status;
        [SerializeField] TMP_Text independentCaption;
        [SerializeField] Camera previewCamera;
        [SerializeField] GuideView guide;
        bool paused, light, guideSample, lastSlot;
        float previousScale = 1;
        public int InvocationCount { get; private set; }
        public int ActionCount => InvocationCount;
        public string LastAction { get; private set; } = string.Empty;
        public const string LongName = "아주 긴 관전 대상 이름은 고정 크기 한 줄 말줄임으로 표시되지만 전체 원문은 보존됩니다. 단원 이름 확인용 합성 예시입니다.";
        public void Configure(SpectatorView view,TMP_Text label,Camera camera,GuideView sampleGuide,TMP_Text captionLabel)
        { spectator=view; status=label; previewCamera=camera; guide=sampleGuide; independentCaption=captionLabel; }
        void Awake() { RestoreTime(); if(spectator!=null) spectator.HideImmediate(); if(guide!=null) guide.HideImmediate(); UpdateStatus(); }
        void OnDisable() { RestoreTime(); }
        void OnDestroy() { RestoreTime(); }
        void OnApplicationQuit() { RestoreTime(); }
        void Run(Action action, [System.Runtime.CompilerServices.CallerMemberName] string actionName = "")
        { if(!Application.isPlaying || !isActiveAndEnabled || spectator==null) return; InvocationCount++; try { action(); LastAction=actionName; UpdateStatus(); } catch { RestoreTime(); throw; } }
        public void Dead() => Run(()=>spectator.Present(SpectatorState.Dead,"사망","단원 1"));
        public void Spectating() => Run(()=>spectator.Present(SpectatorState.Spectating,"관전 중","단원 2","Q","E",true));
        public void NoTarget() => Run(()=>spectator.Present(SpectatorState.NoTarget,"관전","관전 대상 없음"));
        public void Hide() => Run(()=>spectator.Hide());
        public void HideImmediate() => Run(()=>spectator.HideImmediate());
        public void LongTarget() => Run(()=>spectator.Present(SpectatorState.Spectating,"관전 중",LongName,"Q","E",true));
        public void LongKey() => Run(()=>spectator.Present(SpectatorState.Spectating,"관전 중","단원 2","Left Shift","Right Arrow",true));
        public void OneEmptyKey() => Run(()=>spectator.Present(SpectatorState.Spectating,"관전 중","단원 2","Q","",true));
        public void EdgeOff() => Run(()=>spectator.SetEdgeOpacity(0,0));
        public void LeftOnly() => Run(()=>{spectator.SetEdgeOpacity(.4f,.7f);spectator.SetEdgeMultipliers(1,0);});
        public void RightOnly() => Run(()=>{spectator.SetEdgeOpacity(.4f,.7f);spectator.SetEdgeMultipliers(0,1);});
        public void BothEdges() => Run(()=>{spectator.SetEdgeOpacity(.4f,.7f);spectator.SetEdgeMultipliers(1,1);});
        public void Repeat() => Run(()=>{Spectating();Spectating();});
        public void Reverse() => Run(()=>{spectator.Hide();Spectating();});
        public void TogglePause() => Run(()=>{if(paused)RestoreTime();else {previousScale=Time.timeScale;paused=true;Time.timeScale=0;}});
        public void ToggleBackground() => Run(()=>{light=!light;ApplyBackground();});
        public void ToggleGuide() => Run(()=>{guideSample=!guideSample;ApplyGuide();});
        public void ResetPreview() => Run(()=>{RestoreTime();light=false;ApplyBackground();guideSample=false;if(guide!=null)guide.HideImmediate();spectator.HideImmediate();spectator.SetEdgeOpacity(.4f,.7f);spectator.SetEdgeMultipliers(1,1);});
        void Update() { if(spectator==null)return; if(lastSlot!=spectator.OccupiesBottomSlot)ApplyGuide(); UpdateStatus(); }
        void ApplyGuide()
        {
            lastSlot=spectator.OccupiesBottomSlot;
            if(guide==null)return;
            if(guideSample && !lastSlot)guide.ShowKey("F","C7 합성 슬롯 예시"); else guide.HideImmediate();
        }
        void RestoreTime() { if(!paused)return;Time.timeScale=previousScale;paused=false; }
        void ApplyBackground()
        {
            if(previewCamera!=null)previewCamera.backgroundColor=light?new Color32(224,226,218,255):new Color32(14,24,28,255);
            Color32 descriptionColor=light?new Color32(14,24,28,255):new Color32(240,238,220,255);
            if(status!=null)status.color=descriptionColor;
            if(independentCaption!=null)independentCaption.color=descriptionColor;
        }
        void UpdateStatus()
        {
            if(status!=null)status.text="C11 합성 표시 / 게임 연결 없음 · Game View 1920×1080 / 1920×1200에서 확인\n"+
                (spectator==null?"Hidden":spectator.State+" · slot="+spectator.OccupiesBottomSlot+" · longKey="+spectator.SwitchGuideSuppressedForKeyLength)+
                (paused?" · timeScale 0":"")+" · C7 억제는 이 데모의 명시적 호출만 사용";
        }
    }
}
