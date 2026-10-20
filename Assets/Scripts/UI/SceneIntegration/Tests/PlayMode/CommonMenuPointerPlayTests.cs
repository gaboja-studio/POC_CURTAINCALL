using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        IEnumerator ClickMenuButton(Mouse mouse, Component owner, string name)
        {
            var button = owner.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
            Assert.IsTrue(button.IsInteractable());
            var rect = (RectTransform)button.transform;
            var position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            var events = EventSystem.current; Assert.IsNotNull(events);
            var hits = new System.Collections.Generic.List<RaycastResult>();
            events.RaycastAll(new PointerEventData(events) { position = position }, hits);
            Assert.IsNotEmpty(hits);
            Assert.AreEqual(button.gameObject, ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), "Frontmost hit must be the menu button.");
            InputSystem.QueueStateEvent(mouse,new MouseState { position=position }); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState { position=position,buttons=1 }); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState { position=position }); yield return new WaitForSecondsRealtime(.25f);
        }
        [UnityTest]
        public IEnumerator CommonMenu_WaitingRoom_RealPointerClicksAndCompactMicrophone()
        {
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return Launch(2,"Morning",0,"Tightrope","WaitingRoomBase"); yield return MenuReady();
            var mouse = InputSystem.AddDevice<Mouse>();
            var voice = Find("CurtainCall.Voice.VoiceChat"); bool muted = (bool)Get(voice,"IsMicMuted");
            try
            {
                var owner=Find("CurtainCall.UI.SceneIntegration.CommonMenuUI");
                var menu=(Component)Get(owner,"Menu");
                Assert.AreEqual(1,Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length);
                yield return MenuEscape();
                yield return ClickMenuButton(mouse,menu,"Button_Settings");
                var settings=Get(owner,"Settings"); var main=(Component)Get(settings,"Main");
                Assert.IsTrue((bool)Get(main,"IsVisible"));
                yield return ClickMenuButton(mouse,main,"50_Cancel");
                Assert.IsTrue((bool)Get(menu,"IsMenuOpen"));
                yield return ClickMenuButton(mouse,menu,"Button_RequestLeave");
                Assert.IsTrue((bool)Get(menu,"IsConfirmationOpen"));
                yield return ClickMenuButton(mouse,menu,"Button_ReturnToMenu");
                Assert.IsFalse((bool)Get(menu,"IsConfirmationOpen"));
                yield return ClickMenuButton(mouse,menu,"Button_Resume");
                Assert.IsFalse((bool)Get(owner,"IsOpen"));
                yield return CursorState(false);
                var hud=Find("CurtainCall.UI.SceneIntegration.VoiceStatusUI");
                var rect=(RectTransform)hud.transform.Find("MicrophoneStatus");
                Assert.LessOrEqual(rect.rect.width,80); Assert.LessOrEqual(rect.rect.height,40);
                Assert.AreEqual("M",hud.GetComponentInChildren<TMPro.TMP_Text>().text);
                T("CurtainCall.StartLobby.StartLobbyFlow").GetMethod("EnsureExists").Invoke(null,null);
                yield return null;
                CursorTestKey("M"); yield return null; yield return null; CursorTestKey(null); yield return null;
                Assert.AreEqual(!muted,Get(voice,"IsMicMuted"));
                var icon=hud.GetComponentInChildren(T("CurtainCall.UI.SceneIntegration.MicrophoneStatusIcon"));
                Assert.IsNotNull(icon.GetComponent<CanvasRenderer>());
                Assert.AreEqual(!muted,icon.GetType().GetField("muted",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(icon));
                ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(".harness/local/compact-microphone.png")); yield return null;
            }
            finally { InputSystem.RemoveDevice(mouse); if(voice!=null) Call(voice,"SetMicMuted",muted); }
        }
    }
}
