using System;
using System.Collections.Generic;
using CurtainCall.UI.SceneIntegration;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    public enum RoomNotice { [InspectorName("학습 가이드")] Guide, [InspectorName("즉시 피드백")] Feedback, [InspectorName("시스템 알림")] Notice, [InspectorName("시스템 경고")] Warning, [InspectorName("시스템 보상")] Reward }
    public enum RoomPersistent { [InspectorName("팀 임무")] TeamObjective, [InspectorName("상태 정보")] PlayerStatus }
    public enum RestIntro { G01, G11 }
    public enum NightIntro { N01, N02, N04, N03 }

    public enum RoomShot { [InspectorName("현재 시점 · 글만")] Text, [InspectorName("무대 입구")] Entrance,
        [InspectorName("상점·배송구")] Shop, [InspectorName("스케줄표")] Board, [InspectorName("소품실 선반")] Shelf,
        [InspectorName("숙소 침대")] Bed, [InspectorName("직접 저장한 Scene 뷰 시점")] SceneView }
    [Serializable] public sealed class RoomAuthoredStep
    {
        public string id, after, title;
        [TextArea] public string body;
        public RoomShot shot;
        public bool hasPose;
        public Vector3 position, angles, focus;
        public float seconds=4f;
    }
    [Serializable] public sealed class RoomTutorialCue
    {
        public string id;
        public RoomNotice role;
        [Tooltip("비우면 원본 문구를 사용합니다.")] public string title;
        [TextArea] public string body;
        [Tooltip("0 = 기존 시간. 카메라 연출 중에는 카메라 시간이 우선합니다.")] [Min(0)] public float seconds;
    }

    [Serializable] public sealed class RoomCopy
    {
        public string id;
        [TextArea] public string text;
    }

    public sealed class WaitingRoomHudProfile : ScriptableObject
    {
        public const string Path="Settings/UI/WaitingRoomHud";
        static WaitingRoomHudProfile current;
        public static WaitingRoomHudProfile Current => current ? current : current=Resources.Load<WaitingRoomHudProfile>(Path);
        [Header("정해진 순서 · 첫날 / 밤 카메라 안내")]
        public RestIntro[] rest={RestIntro.G01,RestIntro.G11};
        public NightIntro[] night={NightIntro.N01,NightIntro.N02,NightIntro.N04,NightIntro.N03};
        public RoomAuthoredStep[] restExtra=Array.Empty<RoomAuthoredStep>(), nightExtra=Array.Empty<RoomAuthoredStep>();
        public RoomTutorialCue[] tutorials=Array.Empty<RoomTutorialCue>();
        [Header("조건이 유지되는 동안 · 진행 수치는 게임에서 전달")]
        public RoomPersistent mission=RoomPersistent.TeamObjective;
        public RoomPersistent waitingForTeam=RoomPersistent.PlayerStatus;
        [Header("사건이 발생한 순간")]
        public RoomNotice recall=RoomNotice.Warning, broadcast=RoomNotice.Notice, nightBegins=RoomNotice.Notice;
        public RoomNotice bedBlocked=RoomNotice.Feedback, stageBlocked=RoomNotice.Feedback;
        public RoomNotice equipmentUnlocked=RoomNotice.Reward, medicalBooked=RoomNotice.Feedback, carryResult=RoomNotice.Feedback;
        [Header("게임 문구 덮어쓰기 · 비우면 기존 문구 사용")]
        public RoomCopy[] copy=Array.Empty<RoomCopy>();
        public RoomCopy[] rehearsalCopy=Array.Empty<RoomCopy>();

        public RoomAuthoredStep Extra(string id) =>
            Array.Find(restExtra,x=>x!=null && x.id==id) ?? Array.Find(nightExtra,x=>x!=null && x.id==id);
        public string[] Sequence(bool isNight)
        {
            var required=isNight?Order(night):Order(rest);
            var extras=isNight?nightExtra:restExtra;
            var result=new List<string>();var seen=new HashSet<string>();
            void Insert(string after)
            {
                foreach(var step in extras)
                    if(step!=null && step.id?.StartsWith("extra:",StringComparison.Ordinal)==true && (step.after??"")==after && seen.Add(step.id))result.Add(step.id);
            }
            Insert("");foreach(var id in required){result.Add(id);Insert(id);}return result.ToArray();
        }
        public RoomTutorialCue Cue(string id)=>Array.Find(tutorials,x=>x!=null && x.id==id);
        public WaitingRoomTutorialBook BuildBook(string source)
        {
            var book=new WaitingRoomTutorialBook(WaitingRoomTutorialBook.ParseTexts(source??""));
            var edited=new List<TutorialEntry>();
            foreach(var cue in tutorials)
            {
                if(cue==null || string.IsNullOrEmpty(cue.id))continue;
                var basis=book.Get(cue.id);if(basis==null)continue;
                edited.Add(new TutorialEntry(basis.id,string.IsNullOrWhiteSpace(cue.title)?basis.title:cue.title,
                    string.IsNullOrWhiteSpace(cue.body)?basis.body:cue.body,cue.seconds>0?cue.seconds:basis.seconds,basis.whileBlocked));
            }
            foreach(var basis in WaitingRoomTutorialBook.Defaults)
                if(edited.TrueForAll(x=>x.id!=basis.id))edited.Add(book.Get(basis.id));
            var result=new WaitingRoomTutorialBook(edited);
            foreach(var list in new[]{restExtra,nightExtra})foreach(var step in list)
                if(step!=null && step.id?.StartsWith("extra:",StringComparison.Ordinal)==true)
                    result.Register(new TutorialEntry(step.id,step.title??"",step.body??"",Mathf.Clamp(step.seconds,1,60)));
            return result;
        }

        // A malformed Inspector list must never strand the first-day acknowledgement or skip a required camera cue.
        public static string[] Order<T>(T[] configured) where T:struct,Enum
        {
            var result=new List<string>();
            void Add(T step){if(Enum.IsDefined(typeof(T),step) && !result.Contains(step.ToString()))result.Add(step.ToString());}
            if(configured!=null)foreach(var step in configured)Add(step);
            foreach(T step in Enum.GetValues(typeof(T)))Add(step);
            return result.ToArray();
        }
        public static HudRoleCard Emit(RoomNotice role,Transform owner,string title,string body,float seconds,string key=null)
        {
            var hud=HudUIManager.Current;
            return role switch {
                RoomNotice.Guide=>hud.ShowGuide(body,owner,title,seconds,key),
                RoomNotice.Feedback=>hud.ShowFeedback(body,owner,title,seconds),
                _=>hud.ShowSystemEvent(body,owner,title,seconds,role==RoomNotice.Warning?HudEventKind.Warning:role==RoomNotice.Reward?HudEventKind.Reward:HudEventKind.Notice)
            };
        }
    }
}
