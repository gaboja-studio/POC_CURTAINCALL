using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        [UnityTest] public IEnumerator StageHud_LiveRopeAndCannon()
        {
            var profile=Static("CurtainCall.UI.SceneIntegration.PerformanceHudProfile","Current");
            string Copy(string group)=> (string)Field(Field(profile,group),"body");
            yield return Launch(1,"Performance",0,"Tightrope","Tightrope");
            yield return CannonWait(()=>VisibleLessonCard("Objective",Copy("ropeGoal")),10,"외줄 목표");
            yield return CannonWait(()=>VisibleLessonCard("Guide",Copy("approach")),8,"외줄 출발 안내");
            yield return Launch(2,"Performance",1,"HumanCannon","HumanCannon");
            yield return CannonWait(()=>VisibleLessonCard("Objective",Copy("cannonGoal")),10,"인간대포 목표");
            var cannon=Static("CurtainCall.HumanCannon.HumanCannonSystem","Current");
            var seat=(Transform)Get(Call(cannon,"Station",0),"BoardingPoint");
            Call(LocalPlayer.GetComponent(T("CurtainCall.Player.PlayerMover")),"Relocate",seat.position,seat.rotation);
            var reader=LocalPlayer.GetComponent(T("CurtainCall.Player.PlayerInputReader"));
            string prompt=Copy("cBoard").Replace("{interact}",(string)Call(reader,"BindingLabel","Interact","F"));
            yield return CannonWait(()=>VisibleLessonCard("Guide",prompt),8,"탑승 범위에서 가이드");
            Assert.IsFalse(VisibleLessonCard("SystemEvent",prompt));
            Assert.IsFalse(VisibleLessonCard("Objective",Copy("ropeGoal")),"이전 공연 목표 제거");
        }

        [UnityTest] public IEnumerator StageHud_VoteHotkeysApplyWithoutCursor()
        {
            yield return Launch(1,"Rehearsal",0,"Tightrope","WaitingRoomBase");
            var prefs=T("CurtainCall.Player.InputBindingPreferences");
            string prefsKey=(string)prefs.GetField("PlayerPrefsKey").GetRawConstantValue();
            bool had=PlayerPrefs.HasKey(prefsKey);string saved=PlayerPrefs.GetString(prefsKey);
            var draft=(InputActionAsset)prefs.GetMethod("CreateDefaultDraft").Invoke(null,null);
            var keyboard=InputSystem.AddDevice<Keyboard>();var owner=new GameObject("Vote source");
            int first=0,second=0;
            var choices=Array.CreateInstance(T("CurtainCall.UI.SceneIntegration.HudVoteChoice"),2);
            void Choice(int i,bool enabled,Action action)=>choices.SetValue(Activator.CreateInstance(choices.GetType().GetElementType(),i==0?"예":"아니오",enabled,action),i);
            IEnumerator Press(Key k) {InputSystem.QueueStateEvent(keyboard,new KeyboardState(k));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;}
            void Apply(){object[] args={draft,null};Assert.IsTrue((bool)prefs.GetMethod("TryApply").Invoke(null,args),args[1] as string);}
            try {
                Apply();Choice(0,true,()=>first++);Choice(1,true,()=>second++);
                var manager=Static("CurtainCall.UI.SceneIntegration.HudUIManager","Current");
                var vote=(Component)Call(manager,"ShowVote","선택",owner.transform,"투표","",choices);
                yield return null;yield return null;
                yield return Press(Key.Z);yield return Press(Key.X);
                Assert.AreEqual((1,1),(first,second));
                draft.FindAction("Common/VotePrimary").ApplyBindingOverride(0,"<Keyboard>/j");Apply();
                Assert.That(vote.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text.Contains("[J]") && t.text.Contains("예")));
                yield return Press(Key.Z);yield return Press(Key.J);Assert.AreEqual(2,first);
                Choice(1,false,()=>second++);Call(vote,"SetChoices",choices);yield return Press(Key.X);Assert.AreEqual(1,second);
                Call(vote,"Close",true);yield return Press(Key.J);Assert.AreEqual(2,first);
                yield return OpenCannonWaitingRoom(1,"Night");
                var board=Static("CurtainCall.WaitingRoom.ScheduleBoard","Instance");
                for(int day=2; day<=3; day++) for(int slot=0;slot<2;slot++)
                    Call(board,"Set",day,slot,day==2 && slot==0 ? "HumanCannon" : "Tightrope",1);
                Assert.AreEqual("RehearsalRequired",Call(board,"Confirm").ToString());
                yield return null;yield return null;yield return Press(Key.X);
                Assert.IsFalse((bool)Priv(CannonLesson,"prompt"),"거절 키로 실제 투표가 닫힘");
                Assert.IsFalse(CannonFlag("IsActive"));
                Assert.AreEqual("RehearsalRequired",Call(board,"Confirm").ToString());
                yield return null;yield return null;yield return Press(Key.J);
                yield return CannonWait(()=>CannonFlag("IsActive"),5,"변경한 수락 키로 실제 리허설 시작");
            } finally {
                if(had)PlayerPrefs.SetString(prefsKey,saved);else PlayerPrefs.DeleteKey(prefsKey);
                PlayerPrefs.Save();
                // Notify active readers from the restored profile, without rewriting user preferences.
                ((Action)prefs.GetField("Changed",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).GetValue(null))?.Invoke();
                InputSystem.RemoveDevice(keyboard);UnityEngine.Object.Destroy(owner);UnityEngine.Object.Destroy(draft);
            }
        }
    }
}
