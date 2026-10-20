using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Image=UnityEngine.UI.Image;
using Button=UnityEngine.UI.Button;
using Object=UnityEngine.Object;

namespace CurtainCall.Spectator.Tests
{
    public sealed class SpectatorPlayTests
    {
        const string ViewType="CurtainCall.UI.SpectatorV1.SpectatorView";
        GameObject instance;
        MonoBehaviour view;
        float previousScale;
        GameObject Prefab=>Resources.Load<GameObject>("Prefabs/UIs/Spectator/Spectator");
        RectTransform Root=>(RectTransform)view.transform;
        CanvasGroup Group=>view.GetComponent<CanvasGroup>();
        TMP_Text[] Labels=>instance.GetComponentsInChildren<TMP_Text>(true);
        Image[] Edges=>instance.transform.Find("00_EdgeEffects/C11_EdgeEffects").GetComponentsInChildren<Image>(true);
        Transform Previous=>view.transform.Find("07_PreviousKey");
        Transform Next=>view.transform.Find("08_NextKey");
        TMP_Text Detail=>view.transform.Find("06_Detail").GetComponent<TMP_Text>();
        object Call(string name,params object[] args)=>view.GetType().GetMethod(name).Invoke(view,args);
        object Property(string name)=>view.GetType().GetProperty(name).GetValue(view);
        bool Flag(string name)=>(bool)Property(name);
        object State(int value)=>Enum.ToObject(view.GetType().GetMethod("Present").GetParameters()[0].ParameterType,value);
        bool Present(int state=2,string status="관전 중",string detail="단원 2",string previous="Q",string next="E",bool canSwitch=true,bool immediate=false)
            =>(bool)Call("Present",State(state),status,detail,previous,next,canSwitch,immediate);
        IEnumerator Settle(){yield return new WaitForSecondsRealtime(.18f);}
        void Hidden()
        {
            Assert.AreEqual("Hidden",Property("State").ToString());Assert.AreEqual(0,Group.alpha);
            Assert.IsFalse(Flag("OccupiesBottomSlot"));Assert.IsFalse(Flag("HasRuntimePresentation"));
            Assert.IsTrue(view.gameObject.activeSelf);foreach(var edge in Edges){Assert.AreEqual(0,edge.color.a);Assert.IsFalse(edge.enabled);}
        }
        [UnitySetUp] public IEnumerator SetUp()
        {
            previousScale=Time.timeScale;Time.timeScale=1;Assert.NotNull(Prefab);instance=Object.Instantiate(Prefab);
            view=instance.GetComponentsInChildren<MonoBehaviour>(true).Single(x=>x!=null&&x.GetType().FullName==ViewType);yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            try{if(instance!=null)Object.Destroy(instance);yield return null;}finally{Time.timeScale=previousScale;}
        }
        [UnityTest] public IEnumerator ExactSelfContainedStructureAndHidden()
        {
            Hidden();Assert.IsTrue(Flag("IsConfigured"));Assert.AreEqual(11,instance.GetComponentsInChildren<Image>(true).Length);Assert.AreEqual(4,Labels.Length);
            Assert.AreEqual(new Vector2(360,64),Root.sizeDelta);Assert.AreEqual(new Vector2(0,160),Root.anchoredPosition);
            Assert.AreEqual(new Vector2(.5f,0),Root.anchorMin);Assert.AreEqual(Root.anchorMin,Root.anchorMax);Assert.AreEqual(Root.anchorMin,Root.pivot);
            Assert.AreEqual(0,instance.transform.Find("00_EdgeEffects").GetSiblingIndex());Assert.AreEqual(1,instance.transform.Find("20_ContextHUD").GetSiblingIndex());
            foreach(var tr in instance.GetComponentsInChildren<Transform>(true))foreach(var c in tr.GetComponents<Component>())Assert.NotNull(c);
            foreach(var label in Labels)Assert.IsEmpty(label.text);yield return null;
        }
        [UnityTest] public IEnumerator CrossBranchReferencesStayInsideClone()
        {
            foreach(string name in new[]{"leftEdge","rightEdge","visualRoot","visualGroup","stateIcon","statusLabel","detailLabel","previousKeyRoot","nextKeyRoot","previousKeyLabel","nextKeyLabel"})
            {
                var c=(Component)view.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(view);Assert.NotNull(c);Assert.IsTrue(c.transform.IsChildOf(instance.transform));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator TypographyIsStaticLiteralFixedAndFullyKorean()
        {
            foreach(var label in Labels)
            {
                Assert.NotNull(label.font);Assert.AreEqual(AtlasPopulationMode.Static,label.font.atlasPopulationMode);Assert.IsFalse(label.font.isMultiAtlasTexturesEnabled);
                Assert.IsTrue(label.font.HasCharacters("가나다힣…ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789"));
                Assert.AreEqual(TextWrappingModes.NoWrap,label.textWrappingMode);Assert.AreEqual(TextOverflowModes.Ellipsis,label.overflowMode);
                Assert.IsFalse(label.richText);Assert.IsFalse(label.parseCtrlCharacters);Assert.IsFalse(label.enableAutoSizing);
                Assert.AreEqual(label.name=="05_Status"?18:24,label.fontSize);
            }
            Assert.IsTrue(Present(previous:"F",next:"E",immediate:true));Canvas.ForceUpdateCanvases();
            foreach(var key in new[]{Previous,Next})
            {
                Assert.AreEqual(new Vector2(40,40),((RectTransform)key).sizeDelta);var label=key.GetComponentInChildren<TMP_Text>(true);
                Assert.AreEqual(new Vector2(32,32),label.rectTransform.sizeDelta);label.ForceMeshUpdate();Assert.Greater(label.textInfo.characterCount,0);Assert.LessOrEqual(label.preferredWidth,32.01f);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator StateOpacityIconAndKeyContract()
        {
            for(int state=1;state<=3;state++)
            {
                Assert.IsTrue(Present(state,immediate:true));Assert.AreEqual(1,Group.alpha);Assert.IsTrue(Flag("OccupiesBottomSlot"));
                foreach(var edge in Edges)Assert.AreEqual(state==1?.7f:.4f,edge.color.a,.0001f);
                var icon=view.transform.Find("04_Icon").GetComponent<Image>();Assert.That(icon.sprite.name.Contains(state==1?"skull":"eye"));
                Assert.AreEqual(state==2,Previous.gameObject.activeSelf);Assert.AreEqual(state==2,Next.gameObject.activeSelf);
            }
            Assert.IsTrue(Present(0,immediate:true));Hidden();yield return null;
        }
        [UnityTest] public IEnumerator LongKeySuppressesBothWithoutResizing()
        {
            Assert.IsTrue(Present(previous:"Left Shift",next:"E",immediate:true));Assert.IsTrue(Flag("SwitchGuideSuppressedForKeyLength"));
            Assert.IsFalse(Previous.gameObject.activeSelf);Assert.IsFalse(Next.gameObject.activeSelf);
            Assert.AreEqual(24,Previous.GetComponentInChildren<TMP_Text>(true).fontSize);Assert.AreEqual(new Vector2(360,64),Root.sizeDelta);yield return null;
        }
        [UnityTest] public IEnumerator OneEmptyKeySuppressesBothButNotLengthFlag()
        {
            Assert.IsTrue(Present(previous:"Q",next:"",immediate:true));Assert.IsFalse(Flag("SwitchGuideSuppressedForKeyLength"));Assert.IsFalse(Previous.gameObject.activeSelf);Assert.IsFalse(Next.gameObject.activeSelf);yield return null;
        }
        [UnityTest] public IEnumerator CanSwitchFalseSuppressesBoth()
        {
            Assert.IsTrue(Present(canSwitch:false,immediate:true));Assert.IsFalse(Previous.gameObject.activeSelf);Assert.IsFalse(Next.gameObject.activeSelf);Assert.IsTrue(Flag("IsVisible"));yield return null;
        }
        [UnityTest] public IEnumerator LongNameEllipsisRetainsFullOriginalAndNoWrap()
        {
            string full=string.Concat(Enumerable.Repeat("긴 관전 대상 이름 ",50));Assert.IsTrue(Present(detail:full,immediate:true));
            Detail.ForceMeshUpdate();Assert.AreEqual(full,Property("FullDetailText"));Assert.AreEqual(24,Detail.fontSize);Assert.LessOrEqual(Detail.textInfo.lineCount,1);
            Assert.AreEqual(TextOverflowModes.Ellipsis,Detail.overflowMode);Assert.IsTrue(Detail.isTextTruncated);yield return null;
        }
        [UnityTest] public IEnumerator DisplayNormalizesControlsWithoutChangingOriginal()
        {
            string full="<b>대상</b>\\n\r\n\t이름"+(char)0x2028+(char)0x2029;
            Assert.IsTrue(Present(status:full,detail:full,immediate:true));Assert.AreEqual(full,Property("FullStatusText"));Assert.AreEqual(full,Property("FullDetailText"));
            Assert.That(Detail.text.Contains("<b>대상</b>\\n"));Assert.IsFalse(Detail.text.Any(c=>char.IsControl(c)||c==0x2028||c==0x2029));yield return null;
        }
        [UnityTest] public IEnumerator NoRaycastOrGameplayComponents()
        {
            Assert.IsFalse(Group.interactable);Assert.IsFalse(Group.blocksRaycasts);Assert.IsEmpty(instance.GetComponentsInChildren<GraphicRaycaster>(true));
            foreach(var g in instance.GetComponentsInChildren<Graphic>(true))Assert.IsFalse(g.raycastTarget);
            Assert.IsEmpty(instance.GetComponentsInChildren<Camera>(true));Assert.IsEmpty(instance.GetComponentsInChildren<Button>(true));yield return null;
        }
        [UnityTest] public IEnumerator ExitKeepsSlotUntilFadeCompletes()
        {
            Assert.IsTrue(Present(immediate:true));Call("Hide");Assert.AreEqual("Hidden",Property("State").ToString());Assert.IsTrue(Flag("OccupiesBottomSlot"));Assert.IsFalse(Flag("HasRuntimePresentation"));
            yield return Settle();Hidden();Assert.IsEmpty((string)Property("FullDetailText"));foreach(var label in Labels)Assert.IsEmpty(label.text);
        }
        [UnityTest] public IEnumerator HideImmediateClearsAndCanPresentAgain()
        {
            Assert.IsTrue(Present(immediate:true));Call("HideImmediate");Hidden();Assert.IsTrue(Present(1,immediate:true));Assert.IsTrue(Flag("IsVisible"));yield return null;
        }
        [UnityTest] public IEnumerator DisableAndReenableNeverRestoreOldState()
        {
            Assert.IsTrue(Present(immediate:true));view.enabled=false;Hidden();Assert.IsFalse(Present());view.enabled=true;yield return null;Hidden();
            Assert.IsTrue(Present(immediate:true));instance.SetActive(false);Assert.IsFalse(Present());instance.SetActive(true);yield return null;Hidden();
        }
        [UnityTest] public IEnumerator UnscaledAlphaOnlyMotionCompletes()
        {
            Time.timeScale=0;Vector2 position=Root.anchoredPosition;Assert.IsTrue(Present());Assert.AreEqual(0,Group.alpha);
            yield return Settle();Assert.AreEqual(1,Group.alpha,.001f);Assert.AreEqual(position,Root.anchoredPosition);
            Call("Hide");yield return Settle();Hidden();Assert.AreEqual(position,Root.anchoredPosition);Assert.AreEqual(0,Time.timeScale);
        }
        [UnityTest] public IEnumerator RepeatedPresentDoesNotRestartTransition()
        {
            Assert.IsTrue(Present());var field=view.GetType().GetField("elapsed",BindingFlags.NonPublic|BindingFlags.Instance);
            field.SetValue(view,.06f);Assert.IsTrue(Present(detail:"대상 갱신"));Assert.AreEqual(.06f,(float)field.GetValue(view));yield return Settle();Assert.AreEqual(1,Group.alpha,.001f);
        }
        [UnityTest] public IEnumerator ReverseStartsFromCurrentAlpha()
        {
            Assert.IsTrue(Present(immediate:true));Call("Hide");yield return null;float before=Group.alpha;
            Assert.IsTrue(Present());Assert.AreEqual(before,Group.alpha,.00001f);yield return Settle();Assert.AreEqual(1,Group.alpha,.001f);
        }
        [UnityTest] public IEnumerator InvalidStateAndEmptyStatusPreserveGoodPresentation()
        {
            Assert.IsTrue(Present(immediate:true));var before=Labels.Select(x=>x.text).ToArray();
            Assert.IsFalse(Present(999));Assert.IsFalse(Present(status:"\r\n\t"));CollectionAssert.AreEqual(before,Labels.Select(x=>x.text));Assert.AreEqual("Spectating",Property("State").ToString());Assert.AreEqual(1,Group.alpha);yield return null;
        }
        [UnityTest] public IEnumerator NonFiniteSettingsAreRejectedAtomically()
        {
            Assert.IsTrue(Present(immediate:true));foreach(float f in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity})
            {
                Assert.IsFalse((bool)Call("SetEdgeOpacity",f,.7f));Assert.IsFalse((bool)Call("SetEdgeOpacity",.4f,f));
                Assert.IsFalse((bool)Call("SetEdgeMultipliers",f,1f));Assert.IsFalse((bool)Call("SetEdgeMultipliers",1f,f));
            }
            foreach(var edge in Edges)Assert.AreEqual(.4f,edge.color.a,.001f);Assert.AreEqual(1,Group.alpha);yield return null;
        }
        [UnityTest] public IEnumerator MissingReferencesRejectWithoutClearingGoodState()
        {
            Assert.IsTrue(Present(immediate:true));var field=view.GetType().GetField("detailLabel",BindingFlags.NonPublic|BindingFlags.Instance);var old=field.GetValue(view);
            try{field.SetValue(view,null);Assert.IsFalse(Present(1));Assert.AreEqual(1,Group.alpha);Assert.AreEqual("Spectating",Property("State").ToString());Assert.AreEqual("단원 2",Property("FullDetailText"));}
            finally{field.SetValue(view,old);}yield return null;
        }
        [UnityTest] public IEnumerator InvalidSerializedNumbersFreezeRatherThanClearGoodState()
        {
            Assert.IsTrue(Present(immediate:true));var field=view.GetType().GetField("enterSeconds",BindingFlags.NonPublic|BindingFlags.Instance);var old=field.GetValue(view);
            try{field.SetValue(view,float.NaN);Assert.IsFalse(Present(1));Assert.AreEqual(1,Group.alpha);Assert.AreEqual("Spectating",Property("State").ToString());}
            finally{field.SetValue(view,old);}yield return null;
        }
        [UnityTest] public IEnumerator EdgeOffAndIndependentSidesRetainInformation()
        {
            Assert.IsTrue(Present(immediate:true));Assert.IsTrue((bool)Call("SetEdgeMultipliers",1f,0f));Assert.AreEqual(.4f,Edges[0].color.a,.001f);Assert.AreEqual(0,Edges[1].color.a);
            Assert.IsTrue((bool)Call("SetEdgeMultipliers",0f,1f));Assert.AreEqual(0,Edges[0].color.a);Assert.AreEqual(.4f,Edges[1].color.a,.001f);
            Assert.IsTrue((bool)Call("SetEdgeOpacity",0f,0f));yield return Settle();foreach(var edge in Edges)Assert.AreEqual(0,edge.color.a);Assert.AreEqual(1,Group.alpha);Assert.IsTrue(Flag("OccupiesBottomSlot"));
        }
        [UnityTest] public IEnumerator PrefabAssetDisplayAPIsRefuseMutation()
        {
            var asset=Prefab.GetComponentsInChildren<MonoBehaviour>(true).Single(x=>x!=null&&x.GetType().FullName==ViewType);var method=asset.GetType().GetMethod("Present");
            Assert.IsFalse((bool)method.Invoke(asset,new[]{State(1),(object)"사망","원본", "", "",false,true}));
            Assert.IsFalse((bool)asset.GetType().GetMethod("SetEdgeOpacity").Invoke(asset,new object[]{0f,0f}));
            asset.GetType().GetMethod("HideImmediate").Invoke(asset,null);Assert.AreEqual(0,asset.GetComponent<CanvasGroup>().alpha);
            Assert.IsEmpty(asset.transform.Find("06_Detail").GetComponent<TMP_Text>().text);yield return null;
        }
        [UnityTest] public IEnumerator TypesAreIsolatedAndEditorPreviewCannotLightAssets()
        {
            Assert.AreEqual(ViewType,view.GetType().FullName);Assert.IsNull(view.GetType().Assembly.GetType("CurtainCall.UI.SpectatorView"));
#if UNITY_EDITOR
            var asset=Prefab.GetComponentsInChildren<MonoBehaviour>(true).Single(x=>x!=null&&x.GetType().FullName==ViewType);
            Assert.IsFalse((bool)asset.GetType().GetMethod("PreviewEditImmediate").Invoke(asset,new[]{State(1),(object)"사망","원본","","",false}));
#endif
            Hidden();yield return null;
        }
        [UnityTest] public IEnumerator NewVignetteImportersAndCanonicalReadOnlyPaths()
        {
#if UNITY_EDITOR
            foreach(var edge in Edges)
            {
                string path=UnityEditor.AssetDatabase.GetAssetPath(edge.sprite);Assert.That(path.StartsWith("Assets/_ThirdParty/curtaincall_spectator_v1/",StringComparison.Ordinal));
                var t=(UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(path);t.GetSourceTextureWidthAndHeight(out int w,out int h);
                Assert.AreEqual(3840,w);Assert.AreEqual(2160,h);Assert.AreEqual(4096,t.maxTextureSize);Assert.AreEqual(200,t.spritePixelsPerUnit);
                Assert.AreEqual(UnityEditor.TextureImporterNPOTScale.None,t.npotScale);Assert.AreEqual(UnityEditor.TextureImporterCompression.Uncompressed,t.textureCompression);
                Assert.IsFalse(t.mipmapEnabled);Assert.IsFalse(t.isReadable);Assert.IsTrue(t.sRGBTexture);Assert.AreEqual(FilterMode.Bilinear,t.filterMode);Assert.AreEqual(TextureWrapMode.Clamp,t.wrapMode);
                var settings=new UnityEditor.TextureImporterSettings();t.ReadTextureSettings(settings);Assert.AreEqual(SpriteMeshType.FullRect,settings.spriteMeshType);
            }
            foreach(var image in instance.GetComponentsInChildren<Image>(true).Except(Edges))
            {
                if(image.sprite==null)continue;string path=UnityEditor.AssetDatabase.GetAssetPath(image.sprite);
                Assert.IsTrue(path.StartsWith("Assets/_ThirdParty/curtaincall_performance_hud_v1/",StringComparison.Ordinal)||path.StartsWith("Assets/_ThirdParty/curtaincall_icons_v1/",StringComparison.Ordinal));
            }
#endif
            yield return null;
        }
        [UnityTest] public IEnumerator DisabledPreviewCallbacksCannotPauseAgain()
        {
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(x=>x.GetType("CurtainCall.Spectator.SpectatorPreview")).FirstOrDefault(x=>x!=null);Assert.NotNull(type);
            var preview=(MonoBehaviour)instance.AddComponent(type);
            type.GetMethod("Configure").Invoke(preview,new object[]{view,null,null,null,null});
            Time.timeScale=.37f;type.GetMethod("TogglePause").Invoke(preview,null);Assert.AreEqual(0,Time.timeScale);
            preview.enabled=false;Assert.AreEqual(.37f,Time.timeScale);
            int count=(int)type.GetProperty("ActionCount").GetValue(preview);
            type.GetMethod("TogglePause").Invoke(preview,null);Assert.AreEqual(.37f,Time.timeScale);Assert.AreEqual(count,type.GetProperty("ActionCount").GetValue(preview));
            preview.enabled=true;type.GetMethod("TogglePause").Invoke(preview,null);Assert.AreEqual(0,Time.timeScale);
            type.GetMethod("ResetPreview").Invoke(preview,null);Assert.AreEqual(.37f,Time.timeScale);
            yield return null;
        }
        [UnityTest] public IEnumerator BuilderRejectsPlayWithoutChangingExistingOutputs()
        {
#if UNITY_EDITOR
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(x=>x.GetType("CurtainCall.Spectator.Editor.SpectatorProjectBuilder")).FirstOrDefault(x=>x!=null);Assert.NotNull(type);
            string path="Assets/Resources/Prefabs/UIs/Spectator/Spectator.prefab";string before=UnityEditor.AssetDatabase.AssetPathToGUID(path);
            var ex=Assert.Throws<TargetInvocationException>(()=>type.GetMethod("Build").Invoke(null,null));Assert.IsInstanceOf<InvalidOperationException>(ex.InnerException);
            Assert.AreEqual(before,UnityEditor.AssetDatabase.AssetPathToGUID(path));
#endif
            Hidden();yield return null;
        }
    }
}
