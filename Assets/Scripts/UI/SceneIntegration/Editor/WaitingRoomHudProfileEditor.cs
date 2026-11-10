using System;
using System.Linq;
using CurtainCall.WaitingRoom;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration.Editor
{
    [CustomEditor(typeof(WaitingRoomHudProfile))]
    public sealed partial class WaitingRoomHudProfileEditor : UnityEditor.Editor
    {
        int tutorial,copy;
        const string Asset="Assets/Resources/Settings/UI/WaitingRoomHud.asset";
        static string Source(string file)=>Resources.Load<TextAsset>("Settings/WaitingRoom/"+file)?.text ?? "";
        // Show the real trigger next to its authored copy; edit gameplay conditions at the source.
        static string Trigger(string id)=>id switch {
            "G01" or "G11"=>"첫 휴식 안내 순서에 도달",
            "N01" or "N02" or "N03"=>"밤 안내 순서에 도달",
            "N04"=>"밤 안내 순서에 도달하거나 물려줄 선반을 조준",
            "N05"=>"밤에 다음 날 일정이 확정됨",
            "G02"=>"탐색 시간이 흐르는 구역에 들어감",
            "G03"=>"상호작용 가능한 대상을 조준",
            "G04"=>"단서 조사 화면을 엶", "G05"=>"소지 기물 개수가 증가",
            "G06a"=>"마술사의 머리핀 소지", "G06b"=>"천막 말뚝 뽑개 소지", "G06c"=>"UV 등 소지",
            "G07"=>"단서를 조사하거나 UV 흔적을 발견",
            "G08"=>"휴식 시작 60초 후에도 인간대포가 해금되지 않음",
            "G09"=>"휴식 중 상점 전화기를 조준", "G10"=>"잃은 신체가 있음",
            "G11A"=>"소집 경보가 발생했고 방송에서 아직 안내하지 않음",
            "G12"=>"두 사람이 필요한 열쇠 문을 조준",
            "S01"=>"내 캐릭터가 소생", "S02"=>"공연 중 동료가 쓰러진 뒤",
            "S03"=>"손이 가득 차서 기물을 받지 못함", "S04"=>"사용한 도구가 소진됨", _=>"게임 사건"
        };
        static string[] CopyIds()=>WaitingRoomText.Parse(Source("WaitingRoomTexts")).Keys.Where(k=>
            k.StartsWith("first-day.quest.") || k.StartsWith("tutorial.team-wait") || k.StartsWith("banner.") ||
            k.StartsWith("prompt.") || k.StartsWith("night.bed-") || k.StartsWith("notice.item-broken") ||
            k.StartsWith("schedule.entry-blocked.") || k.StartsWith("narration.")).Concat(new[]{"first-day.quest.prepare.title","first-day.quest.prepare.body"}).Distinct().OrderBy(x=>x).ToArray();

        [MenuItem("Curtain Call/UI/대기실 HUD 편집")]
        public static void Open()=>SelectProfile(false);
        [MenuItem("Curtain Call/UI/리허설 HUD 편집")]
        public static void OpenRehearsal()=>SelectProfile(true);
        static void SelectProfile(bool rehearsal)
        {
            SessionState.SetBool(RehearsalTab,rehearsal);
            var asset=AssetDatabase.LoadAssetAtPath<WaitingRoomHudProfile>(Asset);
            if(!asset)
            {
                asset=CreateInstance<WaitingRoomHudProfile>();
                asset.tutorials=WaitingRoomTutorialBook.Defaults.Select(e=>new RoomTutorialCue{id=e.id,
                    role=e.id=="G11A"?RoomNotice.Warning:RoomNotice.Guide}).ToArray();
                asset.copy=CopyIds().Select(k=>new RoomCopy{id=k,text=k=="tutorial.team-wait-title"?"동료 기다리는 중":""}).ToArray();
                AssetDatabase.CreateAsset(asset,Asset);AssetDatabase.SaveAssets();
            }
            Selection.activeObject=asset;
        }

        WaitingRoomTutorialBook defaults;
        System.Collections.Generic.Dictionary<string,string> phrases;
        string activeExtra="";
        bool showRest=true,showNight=true;
        void OnEnable()
        {
            defaults=new WaitingRoomTutorialBook(WaitingRoomTutorialBook.ParseTexts(Source("WaitingRoomTutorialTexts")));
            phrases=WaitingRoomText.Parse(Source("WaitingRoomTexts"));
        }
        SerializedProperty P(string name)=>serializedObject.FindProperty(name);
        static GUIContent L(string name,string tip)=>new GUIContent(name,tip);
        static void Help(string text)=>EditorGUILayout.HelpBox(text,MessageType.None);
        static void Section(string name,string guide)
        {
            EditorGUILayout.Space(8);EditorGUILayout.LabelField(name,EditorStyles.boldLabel);Help(guide);
        }
        static void Field(SerializedProperty row,string field,string name,string tip)
            =>EditorGUILayout.PropertyField(row.FindPropertyRelative(field),L(name,tip));
        static string Id(SerializedProperty row)=>row.FindPropertyRelative("id").stringValue;
        string NameOf(string id)=>defaults.Get(id)?.title+" ("+id+")";
        static string CameraHint(string id)=>id switch {
            "G11"=>"무대 입구 클로즈업", "N02"=>"상점·배송구 클로즈업","N03"=>"스케줄표 클로즈업",
            "N04"=>"소품실 선반 클로즈업","G01" or "N01"=>"현재 시점 · 글만 (4초)",
            "G10"=>"상점 클로즈업","N05"=>"숙소 침대 클로즈업",_=>null
        };
        string CopyLabel(string id)
        {
            string group=id.Split('.')[0] switch { "first-day"=>"첫날 임무", "banner"=>"시스템 방송",
                "prompt"=>"대상 조작", "night"=>"침대", "tutorial"=>"동료 대기", "notice"=>"기물 소진",
                "narration"=>"해금·예약", "schedule"=>"입장 거절", _=>"게임 문구" };
            string kind=id.EndsWith("title")?"제목":"내용";
            string sample=phrases.TryGetValue(id,out var raw)?raw.Split('\n')[0]: "다음 공연 준비";
            if(sample.Length>28)sample=sample.Substring(0,28)+"…";
            return group+" · "+kind+" · "+sample;
        }
        void Route(string key,string name,string tip)
        {
            var p=P(key);
            // Use Popup directly: several serialized fields share a Header attribute.
            p.enumValueIndex=EditorGUILayout.Popup(L(name,tip),p.enumValueIndex,p.enumDisplayNames.Select(n=>new GUIContent(n)).ToArray());
        }
        void PickBuiltin(string id)
        {
            var rows=P("tutorials");
            for(int i=0;i<rows.arraySize;i++)if(Id(rows.GetArrayElementAtIndex(i))==id){tutorial=i;break;}
            activeExtra="";
        }
        void DrawSequence(bool night)
        {
            string label=night?"밤 정비":"첫 휴식";
            var seq=P(night?"night":"rest");var extras=P(night?"nightExtra":"restExtra");
            var profile=(WaitingRoomHudProfile)target;
            var ids=night?WaitingRoomHudProfile.Order(profile.night):WaitingRoomHudProfile.Order(profile.rest);
            bool open=EditorGUILayout.Foldout(night?showNight:showRest,label+" · 재생 순서",true);
            if(night)showNight=open;else showRest=open;
            if(!open)return;
            int number=0;
            void Extras(string after)
            {
                for(int j=0;j<extras.arraySize;j++)
                {
                    var row=extras.GetArrayElementAtIndex(j);
                    if(row.FindPropertyRelative("after").stringValue!=after)continue;
                    EditorGUILayout.BeginHorizontal();
                    if(GUILayout.Button(L((++number)+". "+row.FindPropertyRelative("title").stringValue+"  · 추가 안내","눌러 이 자리에서 문구와 카메라를 편집합니다.")))activeExtra=Id(row);
                    EditorGUILayout.EndHorizontal();
                    if(activeExtra==Id(row))
                    {
                        Field(row,"title","화면 제목","플레이어에게 보이는 짧은 제목입니다.");
                        Field(row,"body","안내 내용","사용할 키는 {interact} 또는 {uv}로 작성하세요.");
                        PreviewGuide(row);
                        var anchor=row.FindPropertyRelative("after");
                        string[] anchors=new[]{""}.Concat(ids).ToArray();
                        int choice=EditorGUILayout.Popup(L("삽입 위치","같은 자리의 추가 안내는 작성 순서대로 재생합니다."),Math.Max(0,Array.IndexOf(anchors,anchor.stringValue)),
                            anchors.Select(k=>new GUIContent(k==""?"맨 처음":NameOf(k)+" 다음")).ToArray());
                        anchor.stringValue=anchors[choice];
                        Field(row,"shot","카메라 대상","글만, 기존 대기실 대상, 직접 저장한 시점 중에서 선택합니다.");
                        var shot=(RoomShot)row.FindPropertyRelative("shot").enumValueIndex;
                        if(shot==RoomShot.SceneView)
                        {
                            var has=row.FindPropertyRelative("hasPose");
                            Help(has.boolValue?"시점이 저장되어 있습니다. 다시 누르면 교체됩니다.":"대기실 씬을 열고 Scene 뷰를 원하는 구도로 맞춘 뒤 아래 버튼을 누르세요.");
                            var scene=SceneView.lastActiveSceneView;
                            using(new EditorGUI.DisabledScope(!scene || !scene.camera || scene.in2DMode))
                                if(GUILayout.Button("현재 Scene 뷰 구도 저장"))
                                {
                                    var cam=scene.camera.transform;
                                    row.FindPropertyRelative("position").vector3Value=cam.position;
                                    row.FindPropertyRelative("angles").vector3Value=cam.eulerAngles;
                                    row.FindPropertyRelative("focus").vector3Value=cam.position+cam.forward*Mathf.Max(1,Vector3.Dot(scene.pivot-cam.position,cam.forward));
                                    has.boolValue=true;
                                }
                        }
                        if(shot==RoomShot.Text)row.FindPropertyRelative("seconds").floatValue=EditorGUILayout.Slider(L("표시 시간 (초)","현재 시점을 유지하고 이 시간 동안 조작을 잠급니다."),row.FindPropertyRelative("seconds").floatValue,1f,60f);
                        else Help("클로즈업은 카메라 연출 시간을 따릅니다. 구도나 대상이 없으면 글만 표시합니다.");
                        if(GUILayout.Button("이 추가 안내 삭제")){extras.DeleteArrayElementAtIndex(j);activeExtra="";j--;}
                    }
                }
            }
            Extras("");
            for(int i=0;i<ids.Length;i++)
            {
                string id=ids[i];
                EditorGUILayout.BeginHorizontal();
                if(GUILayout.Button(L((++number)+". "+NameOf(id),CameraHint(id)+" · 누르면 아래 ‘기존 안내 작성’에서 편집합니다.")))PickBuiltin(id);
                void Move(int to)
                {
                    // Normalize legacy duplicate or incomplete enum arrays only on an actual edit.
                    seq.arraySize=ids.Length;
                    for(int n=0;n<ids.Length;n++)seq.GetArrayElementAtIndex(n).enumValueIndex=Array.IndexOf(seq.GetArrayElementAtIndex(n).enumNames,ids[n]);
                    seq.MoveArrayElement(i,to);
                }
                using(new EditorGUI.DisabledScope(i==0))if(GUILayout.Button(L("↑","기본 안내를 한 자리 앞당깁니다. 여기에 붙인 추가 안내도 함께 이동합니다."),GUILayout.Width(28)))Move(i-1);
                using(new EditorGUI.DisabledScope(i==ids.Length-1))if(GUILayout.Button(L("↓","기본 안내를 한 자리 늦춥니다. 여기에 붙인 추가 안내도 함께 이동합니다."),GUILayout.Width(28)))Move(i+1);
                EditorGUILayout.EndHorizontal();Extras(id);
            }
            if(GUILayout.Button("+ "+label+" 안내 추가"))
            {
                int i=extras.arraySize++;var row=extras.GetArrayElementAtIndex(i);
                activeExtra="extra:"+Guid.NewGuid().ToString("N");
                row.FindPropertyRelative("id").stringValue=activeExtra;
                row.FindPropertyRelative("after").stringValue=ids[ids.Length-1];
                row.FindPropertyRelative("title").stringValue="새 안내";
                row.FindPropertyRelative("body").stringValue="안내 내용을 입력하세요.";
                row.FindPropertyRelative("shot").enumValueIndex=(int)RoomShot.Text;
                row.FindPropertyRelative("hasPose").boolValue=false;row.FindPropertyRelative("seconds").floatValue=4;
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            bool rehearsal=GUILayout.Toolbar(SessionState.GetBool(RehearsalTab,false)?1:0,new[]{"대기실","리허설"})==1;
            SessionState.SetBool(RehearsalTab,rehearsal);
            if(rehearsal){DrawRehearsal();serializedObject.ApplyModifiedProperties();return;}
            Help("대기실에서 언제 어떤 HUD를 보여줄지 설정합니다. 항목 이름에 마우스를 올리면 사용법이 보입니다. Play를 끄고 편집한 뒤 저장하고 다시 플레이하세요.");
            Section("① 첫 휴식·밤 안내 순서","위에서 아래로 한 번씩 재생합니다. 기본 안내를 누르면 ②에서 편집합니다. 새 단계는 ‘안내 추가’로 작성합니다. 연계 중에는 조작이 잠깁니다.");
            DrawSequence(false);DrawSequence(true);
            Section("② 기존 안내 작성","단계를 고르면 표시 조건과 원문이 보입니다. G는 대기실 탐색, N은 밤 정비, S는 상황 안내입니다. 번호를 외우거나 새 ID를 작성할 필요가 없습니다.");
            var list=P("tutorials");
            if(list.arraySize>0)
            {
                tutorial=EditorGUILayout.Popup(L("편집할 안내","선택해도 재생 순서는 바뀌지 않습니다."),Mathf.Clamp(tutorial,0,list.arraySize-1),Enumerable.Range(0,list.arraySize).Select(i=>new GUIContent(NameOf(Id(list.GetArrayElementAtIndex(i))))).ToArray());
                var cue=list.GetArrayElementAtIndex(tutorial);var id=Id(cue);var original=defaults.Get(id);var shot=CameraHint(id);
                if(original!=null)Help("언제: "+Trigger(id)+(shot!=null?" · "+shot:"")+"\n원래 제목: "+original.title+"\n원래 내용: "+original.body);
                Field(cue,"role","표시할 UI","조작 설명은 학습 가이드, 행동 결과는 즉시 피드백, 팀 전체 사건은 시스템 UI를 선택하세요.");
                Field(cue,"title","제목 덮어쓰기","비우면 위의 원래 제목을 사용합니다.");
                Field(cue,"body","내용 덮어쓰기","비우면 원문을 사용합니다. 예: [{interact}]를 길게 누르세요.");
                PreviewGuide(cue,original);
                if(shot==null)Field(cue,"seconds","표시 시간 (초)","0은 원래 시간입니다. 6이면 나타난 뒤 6초 동안 보입니다.");
                else Help("표시 시간은 위의 소개·카메라 연출에 맞춰집니다.");
            }
            Section("③ 진행 중 계속 보이는 UI","조건이 유지되는 동안 같은 카드의 인원·진행 수치를 갱신합니다. 여기서는 카드 종류만 선택합니다.");
            Route("mission","팀이 할 일","스케줄 확정·입구 집결 등 현재 목표가 있는 동안 표시합니다.");
            Route("waitingForTeam","동료 확인 대기","내가 안내를 마치고 다른 단원의 확인을 기다릴 때 표시합니다.");
            Section("④ 사건이 일어났을 때의 UI","왼쪽은 발생 조건, 오른쪽은 그때 쓸 카드입니다. UI 종류를 바꿔도 게임의 성공·실패 조건은 바뀌지 않습니다.");
            Route("recall","무대 복귀 소집","제한 시간 안에 무대로 복귀하라는 소집 방송입니다.");
            Route("broadcast","일반 방송","팀 전체에 일반 방송을 전달할 때 표시합니다.");
            Route("nightBegins","밤 정비 시작","밤 정비 시간으로 전환될 때 표시합니다.");
            Route("bedBlocked","취침 거절","일정 확정 전 침대 사용을 시도했을 때 이유를 알려줍니다.");
            Route("stageBlocked","무대 입장 거절","첫날 일정 미확정 상태에서 입구에 들어오면 표시합니다.");
            Route("equipmentUnlocked","새 공연 해금","공연 장비를 발견해 공연이 해금됐을 때 표시합니다.");
            Route("medicalBooked","신체 복구 예약 완료","의료 서비스를 예약한 직후 표시합니다.");
            Route("carryResult","기물 처리 결과","기물 들기 등 요청의 결과 문구가 있을 때 표시합니다.");
            Section("⑤ 임무·방송·대상 문구","글만 바꾸는 곳입니다. 예: banner.broadcast-title은 일반 방송의 제목입니다. {ready}, {total} 같은 표시는 게임 값이므로 그대로 두세요.");
            list=P("copy");
            if(list.arraySize>0)
            {
                copy=EditorGUILayout.Popup(L("편집할 문구","title은 제목, body는 설명, prompt는 대상에 뜨는 조작 안내입니다."),Mathf.Clamp(copy,0,list.arraySize-1),Enumerable.Range(0,list.arraySize).Select(i=>new GUIContent(CopyLabel(Id(list.GetArrayElementAtIndex(i))))).ToArray());
                var row=list.GetArrayElementAtIndex(copy);
                Help("문구 ID: "+Id(row)+"\n원래 문구: "+OriginalCopy(Id(row)));
                Field(row,"text","대체 문구","비우면 원래 문구를 사용합니다. 바꾸고 싶은 글만 입력하세요.");
                PreviewCopy(list,Id(row));
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}
