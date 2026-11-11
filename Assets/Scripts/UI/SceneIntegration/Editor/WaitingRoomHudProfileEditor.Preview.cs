using System;
using System.Linq;
using CurtainCall.WaitingRoom;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration.Editor
{
    public sealed partial class WaitingRoomHudProfileEditor
    {
        static string Effective(string edit,string original)=>string.IsNullOrWhiteSpace(edit)?original:edit;
        static string Read(SerializedProperty row,string name)=>row.FindPropertyRelative(name).stringValue;
        string OriginalCopy(string id)=>phrases.TryGetValue(id,out var text)?text:id switch {
            "first-day.quest.prepare.title"=>"[tutorial] 다음 공연 준비하기",
            "first-day.quest.prepare.body"=>"대기실에서 다른 공연 준비자들이 남긴 기록을 발견할 수 있습니다. 그러나 공연은 계속되어야 합니다. 다음 공연을 준비하세요.",
            _=>""
        };

        static void DrawCard(string title,string body,string note)
        {
            using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("플레이어 문구 미리보기",EditorStyles.miniBoldLabel);
                var heading=new GUIStyle(EditorStyles.boldLabel){wordWrap=true,richText=false};
                if(!string.IsNullOrEmpty(title))GUILayout.Label(title,heading);
                if(!string.IsNullOrEmpty(body))GUILayout.Label(body,EditorStyles.wordWrappedLabel);
                if(!string.IsNullOrEmpty(note))
                    GUILayout.Label(note,new GUIStyle(EditorStyles.miniLabel){wordWrap=true});
            }
        }

        static void PreviewGuide(SerializedProperty row,TutorialEntry original=null)
        {
            var title=Effective(Read(row,"title"),original?.title??"");
            var body=Effective(Read(row,"body"),original?.body??"");
            string note=body.Contains("{interact}") || body.Contains("{uv}") ? "예시 키: 상호작용 F · UV 우클릭":"";
            DrawCard(title,WaitingRoomTutorialBook.Format(body,"F","우클릭"),note);
        }

        // Only replace values supplied by the actual caller for this field. Unbound tokens stay visible.
        internal static string FormatSample(string id,string template)
        {
            (string,object)[] values=id switch {
                "first-day.quest.prepare.body"=>null,
                _ when id.StartsWith("first-day.quest.") && id.EndsWith(".body")=>new (string,object)[]{("ready",2),("total",3),("inside",2),("alive",3)},
                "tutorial.team-wait" or "night.bed-lie-down" or "night.bed-get-up"=>new (string,object)[]{("ready",2),("total",3)},
                "prompt.pick-up-floor-item" or "prompt.receive-delivery" or "notice.item-broken" or "notice.item-broken-title"=>new (string,object)[]{("item","은색 열쇠")},
                "prompt.receive-delivery-helper"=>new (string,object)[]{("order",3),("player","단원 1")},
                "prompt.inspect-clue"=>new (string,object)[]{("title","낡은 공연 포스터")},
                "prompt.performance-prop-pickup" or "narration.performance-unlocked-title" or "narration.performance-unlocked-body" or "narration.cannon-unlocked-body"=>new (string,object)[]{("trick",id.Contains("cannon")?"인간대포":"외줄타기")},
                "narration.expert-booked-body"=>new (string,object)[]{("n",1)},_=>null
            };
            return WaitingRoomText.Fill(template,values);
        }

        // Return the other half of the same message, even when only one field is being edited.
        internal static (string title,string body) KeysFor(string id)
        {
            if(id.StartsWith("first-day.quest.") || id.StartsWith("schedule.entry-blocked."))
            {
                var prefix=id.Substring(0,id.LastIndexOf('.')+1);return(prefix+"title",prefix+"body");
            }
            if(id.StartsWith("narration."))
            {
                if(id=="narration.cannon-unlocked-body")return("narration.performance-unlocked-title",id);
                var prefix=id.Substring(0,id.LastIndexOf('-')+1);return(prefix+"title",prefix+"body");
            }
            return id switch {
                "banner.night-title" or "banner.night-timed"=>("banner.night-title","banner.night-timed"),
                "banner.night-unlimited"=>("banner.night-title",id),
                "banner.broadcast-title"=>(id,null),
                "banner.recall-body" or "banner.recall-title"=>("banner.recall-title","banner.recall-body"),
                "tutorial.team-wait" or "tutorial.team-wait-title"=>("tutorial.team-wait-title","tutorial.team-wait"),
                "notice.item-broken" or "notice.item-broken-title"=>("notice.item-broken-title","notice.item-broken"),
                "night.bed-title" or "night.bed-unscheduled-notice"=>("night.bed-title","night.bed-unscheduled-notice"),
                "prompt.receive-delivery" or "prompt.receive-delivery-helper"=>("prompt.receive-delivery","prompt.receive-delivery-helper"),
                _=>(null,id)
            };
        }
        void PreviewCopy(SerializedProperty rows,string id)
        {
            var keys=KeysFor(id);
            string Resolve(string key)
            {
                if(key==null)return "";
                var basis=OriginalCopy(key);
                for(int i=0;i<rows.arraySize;i++)
                {
                    var row=rows.GetArrayElementAtIndex(i);
                    if(Id(row)==key)return Effective(Read(row,"text"),basis);
                }
                return basis;
            }
            var t=Resolve(keys.title);var b=Resolve(keys.body);
            string title=FormatSample(keys.title??"",t),body=FormatSample(keys.body??"",b);
            string note=title!=t || body!=b?"인원·이름·기물 등 변동값은 예시입니다.":"";
            if(id=="banner.broadcast-title"){body="잠시 후 공연이 시작됩니다.";note="본문은 예시입니다. 실제로는 그때 수신한 방송이 표시됩니다.";}
            if(id=="banner.night-title")note="시간 제한이 있는 밤의 예시입니다.";
            if(WaitingRoomText.Names(title).Count+WaitingRoomText.Names(body).Count>0)
                note+=" 이 문구에 전달되지 않는 {변수}는 그대로 표시됩니다.";
            DrawCard(title,body,note.Trim());
        }
    }
}
