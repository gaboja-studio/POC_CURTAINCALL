#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace CurtainCall.Tests
{
    public sealed class HudRolePlaygroundSceneTests
    {
        Component demo;
        const string Prefix="CurtainCall.UI.SceneIntegration.";
        T Field<T>(string name)=>(T)demo.GetType().GetField(name).GetValue(demo);
        bool Prop(string name)=>(bool)demo.GetType().GetProperty(name).GetValue(demo);
        void Click(string name)=>Field<Button>(name).onClick.Invoke();
        Component[] Cards()=>Object.FindObjectsByType(Type.GetType(Prefix+"HudRoleCard, Assembly-CSharp",true),FindObjectsSortMode.None).Cast<Component>().ToArray();
        [UnitySetUp]
        public IEnumerator Load()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Tests/UI/HudRolePlayground.unity",new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            demo=(Component)Object.FindFirstObjectByType(Type.GetType(Prefix+"HudRolePlayground, Assembly-CSharp",true));
            Assert.IsNotNull(demo);
        }
        [UnityTest, Category("SpaceRoleHud")]
        public IEnumerator EventThemesWrapAndLeaveRoomForZone()
        {
            var dropdown=Field<TMP_Dropdown>("role");
            dropdown.value=8;Field<TMP_InputField>("content").text="연습실";Click("output");
            var zone=Cards().Single();var zoneRect=(RectTransform)zone.transform;
            dropdown.value=1;var sub=Field<TMP_Dropdown>("eventKind"); Assert.IsTrue(sub.gameObject.activeInHierarchy);
            var titles=new[]{"방송","소집 경보","공연 보상"};
            var messages=new[]{"리허설을 준비하세요.","무대 입구로 복귀하세요.","공동 자금 +200"};
            var sprites=new System.Collections.Generic.HashSet<Sprite>();
            Component last=null;
            for(int i=0;i<3;i++)
            {
                sub.value=i;Field<TMP_InputField>("heading").text=titles[i];Field<TMP_InputField>("content").text=messages[i];
                Field<TMP_InputField>("seconds").text="0";Click("output");yield return null;
                last=Cards().Single(c=>(string)c.GetType().GetProperty("Title").GetValue(c)==titles[i]);
                Assert.AreEqual(2,Cards().Length,"구역 표지와 최신 사건 하나만 표시");
                var style=last.GetComponent(Type.GetType(Prefix+"HudEventAppearance, Assembly-CSharp",true));
                var icon=(Image)style.GetType().GetField("symbol").GetValue(style);
                var label=(TMP_Text)style.GetType().GetField("line").GetValue(style);
                Assert.IsTrue(icon.sprite && sprites.Add(icon.sprite));Assert.That(icon.color,Is.EqualTo(((TMP_Text)last.GetType().GetField("title").GetValue(last)).color));
                Assert.Greater(label.fontSize,((TMP_Text)zone.GetType().GetField("body").GetValue(zone)).fontSize);
                Assert.That(((RectTransform)last.transform).anchoredPosition.y,Is.LessThanOrEqualTo(zoneRect.anchoredPosition.y-zoneRect.rect.height));
            }
            Click("toggle");yield return new WaitForSecondsRealtime(.35f);
            yield return Capture("event-styles");
            var r=(RectTransform)last.transform;
            var update=last.GetType().GetMethod("SetText");var eventStyle=last.GetComponent(Type.GetType(Prefix+"HudEventAppearance, Assembly-CSharp",true));
            var text=(TMP_Text)eventStyle.GetType().GetField("line").GetValue(eventStyle);
            update.Invoke(last,new object[]{"획득","보상",""});yield return null;
            float shortH=r.rect.height,shortW=r.rect.width;
            update.Invoke(last,new object[]{"첫 번째 보상입니다.\n두 번째 보상입니다.","보상",""});yield return null;text.ForceMeshUpdate();
            Assert.AreEqual(3,text.textInfo.lineCount);Assert.Greater(r.rect.height,shortH);Assert.Greater(r.rect.width,shortW);float two=r.rect.height;
            update.Invoke(last,new object[]{"'인간 대포' 공연이 해금되었습니다. 스케줄표에서 처음 확정하기 전에 리허설을 이수해야 합니다.\n획득한 인간 대포·트램펄린은 연습실로 옮겨졌습니다.","인간 대포 획득!",""});yield return null;text.ForceMeshUpdate();
            Assert.Greater(r.rect.height,two);Assert.Greater(text.textInfo.lineCount,3);
            Assert.IsFalse(text.isTextTruncated);
            var lastChar=text.textInfo.characterInfo[text.textInfo.characterCount-1];
            Assert.IsTrue(lastChar.isVisible && lastChar.character=='.');
            Assert.GreaterOrEqual(lastChar.descender,text.rectTransform.rect.yMin-1f);
            Assert.AreEqual(1,text.textInfo.characterInfo.First(ch=>ch.character==(char)39).lineNumber);
            string template=Resources.Load<TextAsset>("Settings/WaitingRoom/WaitingRoomTexts").text.Split('\n')
                .Single(row=>row.StartsWith("narration.cannon-unlocked-body |")).Split('|')[1].Trim();
            update.Invoke(last,new object[]{template.Replace("{trick}","인간 대포").Replace("\\n","\n"),"인간 대포 획득!",""});
            yield return Capture("cannon-unlock-reward");
            update.Invoke(last,new object[]{"획득","보상",""});yield return null;
            Assert.AreEqual(shortH,r.rect.height,1f);Assert.AreEqual(shortW,r.rect.width,1f);
        }
        static IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            System.IO.Directory.CreateDirectory("Logs/HudPlayground");
            ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath("Logs/HudPlayground/"+name+".png"));yield return null;
        }
        [UnityTest]
        public IEnumerator RoleArtworkRendersWithOriginalSprites()
        {
            var dropdown=Field<TMP_Dropdown>("role");
            var panel=dropdown.GetComponentInParent<Canvas>().rootCanvas;
            var voice=Object.FindFirstObjectByType(Type.GetType("CurtainCall.Voice.VoiceChatDevOverlay, Assembly-CSharp",true)) as Behaviour;
            bool showVoice=voice && voice.enabled;
            System.IO.Directory.CreateDirectory("Logs/HudPlayground/Cards");
            try
            {
                panel.enabled=false;if(voice)voice.enabled=false;
                for(int i=0;i<dropdown.options.Count;i++)
                {
                    dropdown.value=i;if(i==3)Click("inside");
                    Click("output");yield return new WaitForSecondsRealtime(.4f);
                    var card=Cards().Single();
                    var art=card.transform.Find("Surface/Artwork");
                    Assert.IsNotNull(art);
                    Assert.IsTrue(art.GetComponentsInChildren<Image>().Any(im=>im.enabled && im.color.a>0 && im.sprite));
                    yield return new WaitForEndOfFrame();
                    var corners=new Vector3[4];((RectTransform)card.transform).GetWorldCorners(corners);
                    var lo=RectTransformUtility.WorldToScreenPoint(null,corners[0]);var hi=RectTransformUtility.WorldToScreenPoint(null,corners[2]);
                    int x=Mathf.Max(0,Mathf.FloorToInt(lo.x)-8),y=Mathf.Max(0,Mathf.FloorToInt(lo.y)-12);
                    int w=Mathf.Min(Screen.width,Mathf.CeilToInt(hi.x)+8)-x,h=Mathf.Min(Screen.height,Mathf.CeilToInt(hi.y)+8)-y;
                    var tex=new Texture2D(w,h,TextureFormat.RGB24,false);
                    tex.ReadPixels(new Rect(x,y,w,h),0,0);tex.Apply();
                    System.IO.File.WriteAllBytes("Logs/HudPlayground/Cards/"+i+".png",tex.EncodeToPNG());Object.Destroy(tex);
                    Click("clear");yield return null;
                }
            }
            finally{panel.enabled=true;if(voice)voice.enabled=showVoice;}
        }
        [UnityTest]
        public IEnumerator ControlBadgesUseInputAndGrowWithoutCoveringText()
        {
            var form=Field<TMP_Dropdown>("role");
            Sprite guideSprite=null;
            foreach(int i in new[]{2,3})
            {
                form.value=i;float small=0;
                foreach(string key in new[]{i==2?"":"F","Left Shift"})
                {
                    Field<TMP_InputField>("key").text=key;Click("output");yield return new WaitForSecondsRealtime(.3f);
                    var card=Cards().Single();var b=card.GetComponent(Type.GetType(Prefix+"HudControlBadge, Assembly-CSharp",true));
                    var label=(TMP_Text)b.GetType().GetField("label").GetValue(b);
                    var plate=(RectTransform)b.GetType().GetField("plate").GetValue(b);
                    Assert.AreEqual(key.Length==0?"TIP":key,label.text);
                    if(!guideSprite)guideSprite=plate.GetComponent<Image>().sprite;
                    Assert.AreSame(guideSprite,plate.GetComponent<Image>().sprite);
                    if(key=="Left Shift")Assert.Greater(plate.rect.width,small);else small=plate.rect.width;
                    var body=(TMP_Text)card.GetType().GetField("body").GetValue(card);
                    Assert.Less(plate.TransformPoint(plate.rect.max).x,body.rectTransform.TransformPoint(body.rectTransform.rect.min).x);
                    Click("clear");yield return null;
                }
            }
        }
        [UnityTest]
        public IEnumerator EachFormDispatchesItsOwnRole()
        {
            var dropdown=Field<TMP_Dropdown>("role");
            Assert.AreEqual(9,dropdown.options.Count);
            for(int i=0;i<9;i++)
            {
                dropdown.value=i;
                Assert.AreEqual(i==2 || i==3,Field<TMP_InputField>("key").gameObject.activeInHierarchy);
                Assert.AreEqual(i==6,Field<TMP_InputField>("firstChoice").gameObject.activeInHierarchy);
                var body=Field<TMP_InputField>("content"); body.text="TEST "+i;
                Click("output"); yield return null;
                var card=Cards().Single();
                Assert.AreEqual(i,(int)card.GetType().GetField("role").GetValue(card));
                Assert.AreEqual(body.text,(string)card.GetType().GetProperty("Content").GetValue(card));
                Click("clear");yield return null;
            }
        }
        [UnityTest]
        public IEnumerator WorldPromptTracksProjectionAndRejectsDistanceAndObstruction()
        {
            Field<TMP_Dropdown>("role").value=3;Click("inside");Click("output");
            yield return null;yield return null;
            Assert.IsTrue(Prop("PromptVisible"));
            var targets=Field<Transform[]>("targets"); var cam=Field<Camera>("sceneCamera");
            void CheckAnchor(int i)
            {
                var rect=(RectTransform)Cards().Single().transform;
                var prefab=Resources.Load<GameObject>("Prefabs/UIs/Roles/Interaction");
                RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)rect.parent,cam.WorldToScreenPoint(targets[i].position),null,out var projected);
                var expected=projected+((RectTransform)prefab.transform).anchoredPosition;
                Assert.Less(Vector2.Distance(rect.localPosition,expected),1f,"prefab offset from projected world target");
            }
            CheckAnchor(0);
            Click("targetRight");yield return null;yield return null;CheckAnchor(0);
            Click("outside");yield return null;
            Assert.IsFalse(Prop("PromptVisible")); Assert.IsFalse(Field<Button>("interact").interactable);
            Click("inside");Click("block");yield return null;
            Assert.IsFalse(Prop("WithinReach"));Assert.IsFalse(Prop("PromptVisible"));
            Click("block");Field<TMP_Dropdown>("target").value=1;Click("inside");
            yield return null;yield return null;CheckAnchor(1);Assert.IsTrue(Prop("PromptVisible"));
            Click("interact");StringAssert.Contains("1회",Field<TMP_Text>("message").text);
            yield return new WaitForSecondsRealtime(.25f);
            yield return new WaitForEndOfFrame();
            System.IO.Directory.CreateDirectory("Logs/HudPlayground");
            ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath("Logs/HudPlayground/interaction.png"));yield return null;
        }
    }
}
#endif
