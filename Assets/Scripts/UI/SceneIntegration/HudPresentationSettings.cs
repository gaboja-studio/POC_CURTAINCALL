using System;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration
{
    public enum HudRepeatMode
    {
        [InspectorName("교체 · 최신 하나")] Replace,
        [InspectorName("쌓기 · 아래로")] StackDown,
        [InspectorName("쌓기 · 위로")] StackUp
    }

    [Serializable]
    public struct HudRepeatRule
    {
        [InspectorName("같은 역할이 다시 표시될 때")] public HudRepeatMode mode;
        [InspectorName("쌓을 수 있는 최대 개수"), Min(1)] public int limit;
        public HudRepeatRule(HudRepeatMode mode,int limit=3){this.mode=mode;this.limit=limit;}
        public int Capacity => mode==HudRepeatMode.Replace ? 1 : Mathf.Max(1,limit);
    }

    [CreateAssetMenu(menuName="Curtain Call/UI/HUD 표시 규칙")]
    public sealed class HudPresentationSettings : ScriptableObject
    {
        public const string ResourcePath="Settings/UI/HudPresentationSettings";
        [InspectorName("팀 임무")] public HudRepeatRule objective=new(HudRepeatMode.Replace);
        [InspectorName("시스템 사건")] public HudRepeatRule systemEvent=new(HudRepeatMode.Replace);
        [InspectorName("학습 가이드")] public HudRepeatRule guide=new(HudRepeatMode.StackDown);
        [InspectorName("상호작용")] public HudRepeatRule interaction=new(HudRepeatMode.Replace);
        [InspectorName("즉시 피드백")] public HudRepeatRule feedback=new(HudRepeatMode.Replace);
        [InspectorName("상태 정보")] public HudRepeatRule status=new(HudRepeatMode.Replace);
        [InspectorName("팀 투표")] public HudRepeatRule vote=new(HudRepeatMode.Replace);
        [InspectorName("결과")] public HudRepeatRule outcome=new(HudRepeatMode.Replace);
        [InspectorName("구역 이름")] public HudRepeatRule zoneName=new(HudRepeatMode.Replace);
        public HudRepeatRule For(HudRole role)=>role switch
        {
            HudRole.Objective=>objective,HudRole.SystemEvent=>systemEvent,HudRole.Guide=>guide,
            HudRole.Interaction=>interaction,HudRole.Feedback=>feedback,HudRole.Status=>status,
            HudRole.Vote=>vote,HudRole.Outcome=>outcome,HudRole.ZoneName=>zoneName,
            _=>throw new ArgumentOutOfRangeException(nameof(role))
        };
    }
}
