using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.WaitingRoom;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration.Editor
{
    public sealed partial class WaitingRoomHudProfileEditor
    {
        const string RehearsalTab="CurtainCall.HudEditor.Rehearsal";
        sealed class Lesson
        {
            public string group,role,label,when,title,body;
        }
        static readonly List<Lesson> lessons=BuildLessons();
        static List<Lesson> BuildLessons()
        {
            var list=new List<Lesson>();string group="외줄타기",prefix="rehearsal.";
            const string guide="학습 가이드",objective="팀 임무",vote="팀 투표",notice="시스템 알림";
            void Add(string role,string label,string when,string title,string body)=>list.Add(new Lesson{
                group=group,role=role,label=label,when=when,title=title==null?null:prefix+title,body=body==null?null:prefix+body});
            void Quest(string key,string label,string when,string role=objective)=>Add(role,label,when,"quest."+key+".title","quest."+key);
            Add(guide,"첫 리허설 시작","첫 공연 전 외줄 리허설에 진입했을 때","intro-title","intro");
            Add(guide,"자유 연습 시작 · 복귀 켬","재연습을 시작하고 사망 후 복귀가 켜져 있을 때","practice-title","practice-intro");
            Add(guide,"자유 연습 시작 · 복귀 끔","재연습을 시작하고 사망 후 복귀가 꺼져 있을 때","practice-title","practice-intro-no-recovery");
            // 표시 번호는 1~5 단일 번호. 문구 키는 내부 구간 ID(2-1 등)를 그대로 쓴다.
            foreach(var (id,no,name) in new[]{("1","1","외줄타기"),("2-1","2","인간 그네"),("2-2","3","연속 그네"),("2-3","4","트리거 그네"),("3","5","목마와 공중그네")})
            {
                Add(guide,no+" "+name+" 소개","이 구간 코스가 준비됐을 때. 첫 구간은 시작 소개가 끝난 뒤 표시합니다.","stage."+id+".guide-title","stage."+id+".intro");
                Add(objective,no+" "+name+" 목표","구간에서 장치 조작 가이드가 뜨는 동안 오른쪽에 유지됩니다.","stage."+id+".title","stage."+id+".goal");
            }
            Add(guide,"옆줄 건너기","첫 구간 소개가 끝난 뒤, 또는 끊어진 줄의 체크포인트에 처음 도달했을 때","lane-guide-title","lane-guide");
            Add(guide,"목마 상호작용","올라탈 수 있는 동료를 처음 가까이에서 찾았을 때","mount-title","mount");
            Add(guide,"추락 후 복귀","추락 후 출발대로 복귀했을 때","fall-title","fall");
            Add(guide,"무대로 이동","마지막 소구간을 마치고 무대 입구 길안내가 시작될 때","complete-title","complete");
            Quest("pole-grab","그네 잡기","단일·연속 그네 진입 5m 전부터 건너편까지, 아직 잡지 않은 상태",guide);
            Quest("pole-single","단일 그네 도약","단일 그네를 잡았거나 그네에서 도약했을 때",guide);
            Quest("pole-next","다음 그네 잡기","연속 그네를 잡았거나 다음 봉을 향해 비행 중일 때",guide);
            Quest("trigger-hold","트리거 유지","내가 트리거 그네를 잡아 동료의 줄을 연결하는 동안",guide);
            Quest("trigger-grab","트리거 잡기","첫째 줄에서 출발점으로부터 5m 이상 나아갔을 때",guide);
            Quest("trigger-open","연결된 줄","둘째 줄의 끊긴 구간에 접근했고 동료가 줄을 연결했을 때",guide);
            Quest("trigger-closed","끊긴 줄","둘째 줄의 끊긴 구간에 접근했고 아직 줄이 연결되지 않았을 때",guide);
            Quest("hang-help","도움 요청","내가 줄에 매달려 구조를 기다릴 때","학습 가이드 + 팀 임무");
            Quest("rescue-team","동료 구조","아직 도착하지 않았고 다른 단원이 줄에 매달려 구조를 기다릴 때","학습 가이드 + 팀 임무");
            Quest("rescue-pull","끌어올리기","내가 매달린 동료를 끌어올리는 동안","학습 가이드 + 팀 임무");
            Quest("balance","균형 잡기","일반 외줄 진행 중 다른 장치·도착 조건이 없을 때");
            Quest("wait-team","도착 대기","내가 도착해 다른 단원을 기다릴 때");
            Quest("fallen","추락 후 복귀 대기","추락 후 소구간 출발대로 복귀할 때까지");
            Quest("practice-fallen","사망 후 대기","재연습에서 사망 후 복귀가 꺼져 있을 때");
            Quest("lane-jump","옆줄 이동","첫째 줄의 체크포인트를 지나 옆줄로 건너야 할 때");
            Quest("lane-platform","중간 발판","옆줄 구간의 중간 발판에서 자유 이동 중일 때",guide);
            Quest("trapeze","공중그네 상태","공중그네를 잡거나 도약한 동안. {hint}는 장치의 현재 상태 설명입니다.");
            Quest("trapeze-lane","공중그네 줄 찾기","공중그네 입구 가까이에서 다른 줄에 있을 때",guide);
            Quest("trapeze-mount","목마 만들기","공중그네 진입 위치에서 첫 봉을 잡을 목마가 없을 때",guide);
            Quest("trapeze-grab","첫 봉 잡기","공중그네 입구에서 목마 위 사람이 첫 봉을 잡을 수 있을 때",guide);
            Quest("trapeze-attach","동료에게 매달리기","근처에서 봉을 잡은 동료에게 매달릴 수 있을 때",guide);
            Quest("gather","무대 입구 집결","리허설 완료 후 아직 입구 통로 밖에 있을 때");
            Quest("gathered","집결 인원 대기","완료 후 내가 입구 통로 안에서 동료를 기다릴 때");
            Add(vote,"구간 넘기기","현재 구간의 넘기기 투표가 열려 있는 동안","skip.title","skip.body");
            Add(vote,"재연습 요청","연습실 기물로 재연습을 요청한 뒤 전원 동의를 기다릴 때","practice-vote.title","practice-vote.body");
            Add(notice,"재연습 취소","재연습에 동의하지 않은 단원이 있을 때","practice-vote.cancel-title","practice-vote.cancel");
            Add(notice,"넘기기 성립","넘기기가 가결되어 리허설 종료 연출에 들어갈 때","skip-ending",null);
            Add("상호작용","재연습 기물","연습실의 외줄 재연습 기물을 조준했을 때",null,"practice-station.action");
            void Button(string key,string label,string when)=>Add("투표·버튼",label,when,null,key);
            Button("skip.agree","넘기기 동의","현재 구간을 넘기는 데 아직 동의하지 않았을 때");
            Button("skip.cancel","넘기기 동의 취소","이미 넘기기에 동의했을 때");
            Button("practice-vote.agree","재연습 동의","재연습 투표 전");Button("practice-vote.agreed","동의함","이미 재연습에 동의했을 때");
            Button("practice-vote.disagree","재연습 거절","재연습 요청에 응답할 때");
            Add(vote,"선택 방법 안내","버튼에는 현재 설정한 키가 자동 표시됩니다. 이 문구는 투표 카드 아래에 표시됩니다.",null,"vote-panel.hint");

            group="인간대포";prefix="cannon-rehearsal.";
            Add(guide,"시작 소개 · 복귀 켬","연습을 시작하고 사망 후 복귀가 켜져 있을 때","title","intro");
            Add(guide,"시작 소개 · 복귀 끔","연습을 시작하고 사망 후 복귀가 꺼져 있을 때","title","intro-no-recovery");
            void Cannon(string key,string label,string when)=>Add(guide,label,when,"guide."+key+".title","guide."+key+".body");
            Cannon("cannon","대포 탑승·발사","대포 탑승 가능 거리 안에 들어가거나 이미 탑승했을 때");
            Cannon("trampoline","트램펄린 잡기","중간 도착을 기록한 뒤 트램펄린 4m 안에 들어왔을 때");
            Cannon("carry","트램펄린 운반","내가 트램펄린 운반자가 됐을 때");
            Cannon("catch","동료 받아주기","트램펄린을 든 채 운송 발판에 탔을 때");
            Cannon("transport","운송 발판","내가 운송 발판을 타고 있을 때");
            Cannon("bounce","반동 착지","내가 트램펄린 반동을 받은 기록이 생겼을 때");
            Cannon("safe","안전 지역","중간 도착 이후 아래층 안전 구역에 들어갔을 때");
            Add("시스템 경고","사망 · 복귀 켬","내가 사망했고 체크포인트로 복귀할 예정일 때","fall-title","fall-recover");
            Add("시스템 경고","사망 · 복귀 끔","내가 사망했고 자동 복귀하지 않을 때","fall-title","fall");
            Add(guide,"다시 연습하기","넘기기 가결 후 대기실로 돌아오면 이수 결과와 별도로 표시합니다.","retry-title","retry-body");
            Add(objective,"첫 대포","중간 도착대에 도착하기 전","first-title","first");
            Add(objective,"다음 지점","중간 도착 이후 아래층 안전 구역 밖에 있을 때","quest-title","second");
            Add(objective,"안전 지역 이동","중간 도착 이후 아래층 안전 구역에 있을 때","safe-title","safe-body");
            Add(objective,"체크포인트 복귀 대기","사망 후 체크포인트에서 다시 시작할 때까지","fall-title","fall-recover");
            Add(objective,"사망 후 대기 · 복귀 끔","리허설에 남아 넘기기·포기에 투표할 수 있는 동안","fall-title","fall");
            Add(objective,"이수 후 목표","성공으로 리허설이 끝난 뒤 대기실에 돌아왔을 때","completed-title","complete");
            Add(vote,"공연 발견 후 확인","인간대포 공연을 처음 해금했을 때","unlocked-title","unlocked-body");
            Add(vote,"공연 선택 전 확인","인간대포를 선택하려는데 리허설 이수 기록이 없을 때","required-title","required");
            Add(vote,"넘기기·포기 투표","인간대포 연습이 진행 중이고 종료 단계에 들어가기 전","vote-title","vote-body");
            Add("즉시 피드백","시작 실패","일정 확인에서 예를 눌렀지만 연습을 시작하지 못했을 때","title","unavailable");
            Add(notice,"이수 결과","참가자 전원이 최종 도착대에 도착하여 대기실에 복귀했을 때","end-title","complete");
            Add(notice,"넘기기 결과","과반수가 넘기기에 동의하여 복귀했을 때","end-title","skipped");
            Add(notice,"포기 결과","과반수가 포기에 동의하여 미이수로 복귀했을 때","end-title","abandoned");
            Add("상호작용","인간대포 연습 기물","연습실의 인간대포 기물을 조준했을 때",null,"station");
            foreach(var (key,label,when) in new[]{("yes","연습 시작","편성 전 확인"),("no","편성표로 복귀","편성 전 확인"),
                ("skip","넘기기","내가 넘기기에 아직 동의하지 않았을 때"),("cancel-skip","넘기기 취소","이미 넘기기에 동의했을 때"),
                ("abandon","포기","내가 포기에 아직 동의하지 않았을 때"),("cancel-abandon","포기 취소","이미 포기에 동의했을 때")})Button(key,label,when);
            return list;
        }

        internal static IEnumerable<string> RehearsalKeys=>lessons.SelectMany(l=>new[]{l.title,l.body}).Where(k=>k!=null).Distinct();
        int lessonGroup,lessonIndex;
        void DrawRehearsal()
        {
            Section("리허설 HUD 편집","연습과 안내를 고르면 표시 조건과 역할을 확인할 수 있습니다. 제목과 내용에 {forward}, {balance}, {move}, {side}, {interact}, {jump}를 쓸 수 있습니다. 문구를 비우면 원문으로 돌아갑니다. 편집 후 저장하고 Play를 다시 시작하세요.");
            int group=GUILayout.Toolbar(lessonGroup,new[]{"외줄타기","인간대포"});
            if(group!=lessonGroup){lessonGroup=group;lessonIndex=0;}
            var choices=lessons.Where(x=>x.group==(group==0?"외줄타기":"인간대포")).ToArray();
            lessonIndex=EditorGUILayout.Popup("편집할 안내",Mathf.Clamp(lessonIndex,0,choices.Length-1),
                choices.Select(x=>x.label+" · "+x.role).ToArray());
            var item=choices[lessonIndex];
            Help("언제: "+item.when+"\n표시: "+item.role);
            var rows=P("rehearsalCopy");
            string Edit(string key,string label)
            {
                if(key==null)return "";
                var basis=OriginalCopy(key);
                Help("원래 "+label+": "+basis);
                int index=-1;
                for(int i=0;i<rows.arraySize;i++)if(Id(rows.GetArrayElementAtIndex(i))==key){index=i;break;}
                string old=index<0?"":Read(rows.GetArrayElementAtIndex(index),"text");
                EditorGUILayout.LabelField(L(label+" 덮어쓰기","같은 원문 키를 사용하는 안내에 함께 반영됩니다: "+key));
                string edit=EditorGUILayout.TextArea(old,GUILayout.MinHeight(label=="제목"?20:42));
                if(edit!=old)
                {
                    if(string.IsNullOrWhiteSpace(edit)){if(index>=0)rows.DeleteArrayElementAtIndex(index);}
                    else
                    {
                        if(index<0){index=rows.arraySize++;rows.GetArrayElementAtIndex(index).FindPropertyRelative("id").stringValue=key;}
                        rows.GetArrayElementAtIndex(index).FindPropertyRelative("text").stringValue=edit;
                    }
                }
                return Effective(edit,basis);
            }
            string title=Edit(item.title,"제목"),body=Edit(item.body,"내용");
            string preview=RehearsalSample(item.body,body);
            if(item.role=="학습 가이드")
            {
                DrawCard(RehearsalGuide.Format(title,"F","Space"),RehearsalGuide.Format(preview,"F","Space"),"왼쪽 학습 가이드 · 예시 키");
            }
            else if(item.role.Contains("+"))
            {
                DrawCard(RehearsalGuide.Format(title,"F","Space"),RehearsalGuide.Format(preview,"F","Space"),"왼쪽 학습 가이드 · 예시 키");
                DrawCard(WaitingRoomHudProfile.FormatKeys(title),WaitingRoomHudProfile.FormatKeys(preview),"오른쪽 팀 임무");
            }
            else DrawCard(WaitingRoomHudProfile.FormatKeys(title),
                WaitingRoomHudProfile.FormatKeys(preview),"키·인원·줄 번호·장치 상태는 예시입니다.");
        }
        internal static string RehearsalSample(string key,string raw)
        {
            (string,object)[] args=key switch {
                "rehearsal.quest.gather" or "rehearsal.quest.gathered"=>new (string,object)[]{("inside",2),("alive",3)},
                "rehearsal.skip.body"=>new (string,object)[]{("votes",1),("required",2)},
                "rehearsal.practice-vote.body"=>new (string,object)[]{("votes",1),("total",3)},
                "cannon-rehearsal.vote-body"=>new (string,object)[]{("skip",1),("abandon",0),("required",2)},
                "rehearsal.quest.trapeze"=>new (string,object)[]{("hint","반동을 모아 다음 봉으로 도약하세요.")},
                "rehearsal.quest.trapeze-lane" or "rehearsal.quest.trigger-hold" or "rehearsal.quest.trigger-grab"=>new (string,object)[]{("lane",2)},
                _=>null
            };
            return WaitingRoomText.Fill(raw,args);
        }
    }
}
