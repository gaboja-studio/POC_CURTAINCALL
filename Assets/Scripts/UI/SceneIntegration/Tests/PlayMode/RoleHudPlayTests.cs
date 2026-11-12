using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.Tests
{
    // Runtime game scripts live in Assembly-CSharp; this test assembly uses their public API.
    public sealed class RoleHudPlayTests
    {
        static Type Runtime(string type) => Type.GetType("CurtainCall.UI.SceneIntegration."+type+", Assembly-CSharp",true);
        static Component Show(object manager,string method,params object[] args) =>
            (Component)manager.GetType().GetMethods(BindingFlags.Public|BindingFlags.Instance).Single(m=>m.Name==method).Invoke(manager,args);

        [UnityTest]
        public IEnumerator PrefabPositionManualRowsAndTemplatesSurviveThePresenter()
        {
            var manager=(Component)Runtime("HudUIManager").GetProperty("Current").GetValue(null);
            var catalog=(Array)manager.GetType().GetField("prefabs").GetValue(manager);
            var original=(Component)catalog.GetValue(2);
            var authored=Object.Instantiate(original);
            var owner=new GameObject("PrefabAuthoringTest");
            var type=authored.GetType();
            try
            {
                var root=(RectTransform)authored.transform;
                root.anchorMin=root.anchorMax=new Vector2(.23f,.77f); root.pivot=new Vector2(.3f,.8f);
                root.anchoredPosition=new Vector2(123,-47); root.sizeDelta=new Vector2(385,111);
                ((UnityEngine.UI.VerticalLayoutGroup)type.GetField("rows").GetValue(authored)).enabled=false;
                var title=(TMPro.TMP_Text)type.GetField("title").GetValue(authored);
                var body=(TMPro.TMP_Text)type.GetField("body").GetValue(authored);
                title.text="고정 표제"; title.color=Color.cyan;
                body.text="입력: {content}"; body.rectTransform.anchoredPosition=new Vector2(31,-27);
                var motion=(Component)type.GetField("motion").GetValue(authored);
                motion.GetType().GetField("allowDurationOverride").SetValue(motion,false);
                motion.GetType().GetField("displaySeconds").SetValue(motion,0f);
                catalog.SetValue(authored,2);
                var shown=Show(manager,"ShowGuide","점프",owner.transform,"게임에서 보낸 제목",.01f,null);
                yield return new WaitForSecondsRealtime(.4f);
                Assert.IsTrue(shown,"the prefab can keep a card visible despite a short caller duration");
                var r=(RectTransform)shown.transform;
                Assert.AreEqual(root.anchorMin,r.anchorMin); Assert.AreEqual(root.pivot,r.pivot);
                Assert.AreEqual(root.anchoredPosition,r.anchoredPosition); Assert.AreEqual(root.sizeDelta,r.sizeDelta);
                var heading=(TMPro.TMP_Text)type.GetField("title").GetValue(shown);
                var description=(TMPro.TMP_Text)type.GetField("body").GetValue(shown);
                Assert.AreEqual("고정 표제",heading.text); Assert.AreEqual(Color.cyan,heading.color);
                Assert.AreEqual("입력: 점프",description.text);
                type.GetMethod("SetText").Invoke(shown,new object[]{"놓기","다른 제목",""});
                Assert.AreEqual("입력: 놓기",description.text);
                Assert.AreEqual(body.rectTransform.anchoredPosition,description.rectTransform.anchoredPosition);
            }
            finally { catalog.SetValue(original,2); Object.Destroy(authored.gameObject); Object.Destroy(owner); }
        }

        [UnityTest]
        public IEnumerator TightropeObstacleInstructionUsesGuideWithoutClearingSystemEvent()
        {
            var controller=new GameObject("Tightrope source");
            var events=new GameObject("Event source");
            try
            {
                var manager=(Component)Runtime("HudUIManager").GetProperty("Current").GetValue(null);
                var warning=Show(manager,"ShowSystemEvent","무대에 집결",events.transform,"소집 경보",0f,Enum.Parse(Runtime("HudEventKind"),"Warning"));
                var controls=controller.AddComponent(Runtime("TightropeControlsPanel"));
                var obstacle=controls.GetType().GetMethod("Lesson",BindingFlags.NonPublic|BindingFlags.Instance);
                var cue=Activator.CreateInstance(Runtime("PerformanceHudProfile+Cue"),"점프해서 톱날을 피하세요","Space로 점프","Space");
                object[] args={"horizontal:1",cue,true};
                Assert.IsTrue((bool)obstacle.Invoke(controls,args));
                Assert.IsFalse((bool)obstacle.Invoke(controls,args),"the same obstacle must not replay the lesson");
                var cards=manager.GetComponentsInChildren(Runtime("HudRoleCard"));
                Assert.IsTrue(cards.Any(c => c.GetType().GetField("role").GetValue(c).ToString()=="Guide" &&
                    (string)c.GetType().GetProperty("Content").GetValue(c)=="Space로 점프"));
                controller.SetActive(false);
                yield return null;
                Assert.IsTrue(warning && warning.gameObject.activeSelf,"ending the lesson must not hide an event");
            }
            finally { Object.Destroy(controller); Object.Destroy(events); }
        }

        [UnityTest]
        public IEnumerator GuidesStackWithoutReplacingWarningAndExpireWhenPaused()
        {
            var manager=(Component)Runtime("HudUIManager").GetProperty("Current").GetValue(null);
            var owner=new GameObject("RoleTestOwner");
            float previous=Time.timeScale;
            try
            {
                var warning=Show(manager,"ShowSystemEvent","모이세요",owner.transform,"소집 경보",0f,Enum.Parse(Runtime("HudEventKind"),"Warning"));
                var old=Show(manager,"ShowGuide","W/S 이동",owner.transform,"첫 안내",0f,null);
                var recent=Show(manager,"ShowGuide","A/D 균형",owner.transform,"새 안내",.35f,null);
                var duplicate=Show(manager,"ShowGuide","A/D 균형",warning.transform,"새 안내",0f,null);
                Assert.AreSame(recent,duplicate,"이미 표시 중인 안내는 다른 호출자가 요청해도 중복되지 않는다.");
                var objective=Show(manager,"ShowObjective","모든 단원이 모이면 시작합니다.",owner.transform,"무대에 집결","1/3");
                yield return null;
                Assert.That(((RectTransform)recent.transform).anchoredPosition.y,Is.GreaterThan(((RectTransform)old.transform).anchoredPosition.y));
                Assert.AreEqual(0,((RectTransform)recent.transform).anchorMin.x);
                Assert.AreEqual(.5f,((RectTransform)warning.transform).anchorMin.x);
                Assert.AreEqual(1,((RectTransform)objective.transform).anchorMin.x);
                Time.timeScale=0; yield return new WaitForSecondsRealtime(.85f);
                Assert.IsTrue(recent==null,"timed guide should exit on unscaled time");
                var replay=Show(manager,"ShowGuide","A/D 균형",owner.transform,"새 안내",0f,null);
                Assert.IsTrue(replay && !ReferenceEquals(recent,replay),"사라진 안내는 다시 표시할 수 있다.");
                Assert.IsTrue(warning && old && objective,"guide expiry must not clear other roles");
                Object.Destroy(owner); yield return null; yield return null; yield return null;
                Assert.IsTrue(warning==null && old==null && objective==null,"owner unload must clean up persistent cards");
            }
            finally { Time.timeScale=previous; if(owner) Object.Destroy(owner); }
        }

        [UnityTest]
        public IEnumerator SharedRuleReplacesImmediatelyAndSupportsBoundedStacks()
        {
            var manager=(Component)Runtime("HudUIManager").GetProperty("Current").GetValue(null);
            var field=manager.GetType().GetField("presentation");var original=(ScriptableObject)field.GetValue(manager);
            var rules=Object.Instantiate(original);
            var first=new GameObject("Old mission owner");var next=new GameObject("New mission owner");
            try
            {
                field.SetValue(manager,rules);
                Component Mission(string body,GameObject owner)=>Show(manager,"ShowObjective",body,owner.transform,"팀 임무","");
                var old=Mission("집결",first);
                old.GetType().GetMethod("Close").Invoke(old,new object[]{false});
                var latest=Mission("무대 입장",next);
                Assert.IsFalse(old.gameObject.activeSelf,"퇴장 중인 이전 내용도 같은 프레임에 사라져야 한다");
                var anchor=((RectTransform)latest.transform).anchoredPosition;
                var ruleField=rules.GetType().GetField("objective");
                foreach(var direction in new[]{"StackDown","StackUp"})
                {
                    var rule=ruleField.GetValue(rules);var mode=rule.GetType().GetField("mode");
                    mode.SetValue(rule,Enum.Parse(mode.FieldType,direction));
                    rule.GetType().GetField("limit").SetValue(rule,2);ruleField.SetValue(rules,rule);
                    var second=Mission("두 번째",next);var third=Mission("세 번째",first);
                    Assert.IsFalse(latest.gameObject.activeSelf);
                    Assert.IsTrue(second.gameObject.activeSelf);
                    Assert.AreEqual(anchor,((RectTransform)third.transform).anchoredPosition);
                    Assert.AreEqual(direction=="StackUp",((RectTransform)second.transform).anchoredPosition.y>anchor.y);
                    latest=third;
                }
                yield return null;
            }
            finally{field.SetValue(manager,original);Object.Destroy(rules);Object.Destroy(first);Object.Destroy(next);}
        }

        [UnityTest]
        public IEnumerator VoteUsesRealButtonAndMovesBelowGrowingObjective()
        {
            var manager=(Component)Runtime("HudUIManager").GetProperty("Current").GetValue(null);
            var owner=new GameObject("VoteTestOwner");
            try
            {
                var objective=Show(manager,"ShowObjective","짧은 설명",owner.transform,"집결","1/2");
                var choice=Type.GetType("CurtainCall.UI.SceneIntegration.HudVoteChoice, Assembly-CSharp",true);
                var options=Array.CreateInstance(choice,2); int clicks=0;
                options.SetValue(Activator.CreateInstance(choice,"리허설 넘기기",true,(Action)(()=>clicks++)),0);
                options.SetValue(Activator.CreateInstance(choice,"리허설 포기 취소",true,(Action)(()=>clicks++)),1);
                var vote=Show(manager,"ShowVote","과반 동의 시 이동합니다.",owner.transform,"다음 구간으로?","Esc 후 선택",options);
                yield return null;
                var buttons=vote.GetComponentsInChildren<UnityEngine.UI.Button>();
                Assert.AreEqual(2,buttons.Length);
                foreach (var choiceButton in buttons)
                {
                    var label=choiceButton.GetComponentInChildren<TMPro.TMP_Text>();
                    label.ForceMeshUpdate();
                    Assert.AreEqual(1,label.textInfo.lineCount,label.text);
                    Assert.IsFalse(label.isTextTruncated || label.isTextOverflowing,label.text);
                }
                Assert.Less(buttons[1].GetComponentInChildren<TMPro.TMP_Text>().fontSize,28);
                var button=buttons[0];
                button.onClick.Invoke(); Assert.AreEqual(1,clicks);
                var before=((RectTransform)vote.transform).anchoredPosition.y;
                objective.GetType().GetMethod("SetText",BindingFlags.Public|BindingFlags.Instance)
                    .Invoke(objective,new object[]{"첫 번째 조작\n두 번째 조작\n마지막 조작","집결","1/2"});
                yield return null; yield return null;
                Assert.Less(((RectTransform)vote.transform).anchoredPosition.y,before);
            }
            finally { Object.Destroy(owner); }
        }
    }
}
