using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed class DemoRunEndingPlayTests
    {
        Component campaign;
        GameObject doorObject, boardObject;
        Behaviour director;
        bool directorWasEnabled;
        float previousTimeScale;
        readonly List<(EventInfo Event, Delegate Handler)> sceneHandlers = new();
        static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        static object Get(object value, string name) => value.GetType().GetProperty(name).GetValue(value);
        static object Field(object value, string name) => value.GetType().GetField(name).GetValue(value);
        static object Call(object value, string name, params object[] args) => value.GetType().GetMethods()
            .Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(value, args);

        [UnityTest]
        public IEnumerator ScheduleClose_DawnTitleThenTenSecondFadeAndExplicitConfirmation()
        {
            previousTimeScale = Time.timeScale;
            campaign = (Component)T("CurtainCall.Campaign.CampaignRun").GetMethod("EnsureExists").Invoke(null, null);
            yield return null;
            director = Object.FindFirstObjectByType(T("CurtainCall.SpaceLink.SpaceLinkDirector")) as Behaviour;
            if (director != null)
            {
                directorWasEnabled = director.enabled;
                director.enabled = false;
                // 비활성 컴포넌트도 C# 이벤트를 받으므로 이 합성 검사 동안 씬 전환 구독만 보관한다.
                foreach (string name in new[] { "StepChanged", "Changed" })
                {
                    var handlers = (Delegate)campaign.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(campaign);
                    if (handlers == null) continue;
                    var evt = campaign.GetType().GetEvent(name);
                    foreach (var handler in handlers.GetInvocationList().Where(h => ReferenceEquals(h.Target, director)))
                    {
                        sceneHandlers.Add((evt, handler));
                        evt.RemoveEventHandler(campaign, handler);
                    }
                }
            }
            Assert.IsTrue((bool)Call(campaign, "StartOfflineRun"));
            // 합성 런으로 네트워크/씬 교체 없이 표시·시간·문 제어를 검증한다.
            var host = Activator.CreateInstance(T("CurtainCall.Campaign.RunState"), new object[] { true, true });
            var point = Activator.CreateInstance(T("CurtainCall.Campaign.RunStartPoint"), 3,
                Enum.Parse(T("CurtainCall.Campaign.RunStartPhase"), "Night"), 0, "");
            Assert.IsTrue((bool)Call(host, "DebugStartAt", point));
            campaign.GetType().GetField("host", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(campaign, host);
            var apply = campaign.GetType().GetMethod("Apply", BindingFlags.NonPublic | BindingFlags.Instance);
            apply.Invoke(campaign, new[] { Get(host, "Snapshot") });
            doorObject = new GameObject("DemoEndingDoorTest");
            var door = doorObject.AddComponent(T("CurtainCall.WaitingRoom.NightDoorLock"));
            Assert.AreEqual(true, Get(door, "IsLocked"));
            boardObject = new GameObject("DemoEndingScheduleTest");
            var board = (Behaviour)boardObject.AddComponent(T("CurtainCall.WaitingRoom.ScheduleBoardView"));
            board.enabled = false; // Keep the synthetic panel open without a local actor/reach update.
            Call(board, "SetOpen", true);
            Time.timeScale = 0;
            Assert.IsTrue((bool)Call(campaign, "SetReady", true));
            yield return new WaitForSecondsRealtime(2.2f);
            Assert.AreEqual("Dawn", Field(Get(campaign, "State"), "Phase").ToString());
            Assert.AreEqual(3, Field(Get(campaign, "State"), "Day"));
            var previousOverlay = campaign.transform.Find("DemoRunEnding");
            Assert.IsTrue(previousOverlay == null || !previousOverlay.gameObject.activeSelf, "Open schedule must block the fade.");
            Call(board, "SetOpen", false);
            var presentation = campaign.GetComponent(T("CurtainCall.UI.SceneIntegration.DemoRunEnding"));
            var beganField = presentation.GetType().GetField("began", BindingFlags.Instance | BindingFlags.NonPublic);
            double startDeadline = Time.realtimeSinceStartupAsDouble + 5;
            // 최초 오버레이·동적 폰트 생성 시간은 실제 페이드 시간에 포함되지 않는다.
            // Wait for Update to initialize the actual fade before measuring its midpoint.
            while ((double)beganField.GetValue(presentation) < 0 && Time.realtimeSinceStartupAsDouble < startDeadline)
                yield return null;
            double began = (double)beganField.GetValue(presentation);
            Assert.GreaterOrEqual(began, 0, "Dawn fade must start after the schedule closes.");
            yield return new WaitForSecondsRealtime(1f);
            Assert.AreEqual("Dawn", Field(Get(campaign, "State"), "Phase").ToString());
            var overlay = campaign.transform.Find("DemoRunEnding");
            var black = overlay.Find("Black").GetComponent<UnityEngine.UI.Image>();
            Assert.That(black.color.a, Is.InRange(.4f, .65f));
            while (Field(Get(campaign, "State"), "Phase").ToString() == "Dawn"
                && Time.realtimeSinceStartupAsDouble - began < 5) yield return null;
            yield return null;
            Assert.AreEqual("DemoEnding", Field(Get(campaign, "State"), "Phase").ToString());
            Assert.AreEqual(4, Field(Get(campaign, "State"), "Day"));
            Assert.AreEqual(true, Get(door, "IsFullyClosed"));
            var label = overlay.Find("Message").GetComponent<TMP_Text>();
            var button = overlay.Find("Confirm").GetComponent<UnityEngine.UI.Button>();
            Assert.AreEqual("4일차", label.text);
            Assert.IsFalse(button.gameObject.activeSelf);
            Assert.Greater(black.color.a, .9f);
            yield return new WaitForSecondsRealtime(2.5f);
            Assert.Less(black.color.a, .05f);
            Assert.AreEqual("4일차", label.text);
            Assert.Greater(label.color.a, .9f, "Day text must remain fully visible for 3 seconds.");
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.That(label.color.a, Is.InRange(.35f, .65f));
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.IsFalse(label.gameObject.activeSelf, "Ending message must stay hidden throughout the 10-second fade.");
            Assert.IsFalse(button.gameObject.activeSelf);
            double fadeBegan = (double)overlay.parent.GetComponent(T("CurtainCall.UI.SceneIntegration.DemoRunEnding")).GetType()
                .GetField("began", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(overlay.parent.GetComponent(T("CurtainCall.UI.SceneIntegration.DemoRunEnding")));
            yield return new WaitForSecondsRealtime(5f);
            Assert.AreEqual("DemoEnding", Field(Get(campaign, "State"), "Phase").ToString());
            Assert.That(black.color.a, Is.InRange(.45f, .65f));
            Assert.IsFalse(label.gameObject.activeSelf);
            Assert.AreEqual(true, Get(door, "IsLocked"));
            Assert.AreEqual("None", Field(Get(campaign, "State"), "Timer").ToString());
            Assert.AreEqual(0f, Field(Get(campaign, "State"), "TimeRemaining"));
            while (!button.gameObject.activeSelf && Time.realtimeSinceStartupAsDouble - fadeBegan < 12) yield return null;
            Assert.GreaterOrEqual(Time.realtimeSinceStartupAsDouble - fadeBegan, 10);
            Assert.IsTrue(button.gameObject.activeSelf);
            Assert.AreEqual("데모 플레이가 종료되었습니다. 플레이해주셔서 감사합니다.", label.text);
            Assert.AreEqual(1f, black.color.a);
            foreach (char c in label.text.Where(c => !char.IsWhiteSpace(c)))
                Assert.IsTrue(label.font.HasCharacter(c), $"Missing message glyph: {c}");
            yield return new WaitForSecondsRealtime(.2f);
            Assert.AreEqual("DemoEnding", Field(Get(campaign, "State"), "Phase").ToString(), "Showing the message must not automatically restart the run.");
            button.onClick.Invoke();
            var state = Get(campaign, "State");
            Assert.AreEqual("Performance", Field(state, "Phase").ToString());
            Assert.AreEqual(1, Field(state, "Day"));
            Assert.AreEqual(0, Field(state, "PerformanceIndex"));
            Assert.AreEqual("Deadline", Field(state, "LastEnd").ToString());
            Call(campaign, "StopOfflineRun");
            yield return null;
            Assert.IsFalse(overlay.gameObject.activeSelf);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = previousTimeScale;
            if (campaign != null) Call(campaign, "StopOfflineRun");
            if (doorObject != null) Object.Destroy(doorObject);
            if (boardObject != null) Object.Destroy(boardObject);
            yield return null;
            if (campaign != null && director != null)
                foreach (var handler in sceneHandlers) handler.Event.AddEventHandler(campaign, handler.Handler);
            sceneHandlers.Clear();
            if (director != null) director.enabled = directorWasEnabled;
        }
    }
}
