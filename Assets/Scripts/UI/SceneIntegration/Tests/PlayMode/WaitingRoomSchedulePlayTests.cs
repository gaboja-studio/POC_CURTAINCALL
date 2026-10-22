using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        IEnumerator OpenSchedule()
        {
            var board = Find("CurtainCall.WaitingRoom.ScheduleBoardView");
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var point = (Vector3)Get(board, "PresentationPoint");
            Call(player.GetComponent(T("CurtainCall.Player.PlayerMover")), "SetStartPose", new Vector3(point.x, .1f, point.z), Quaternion.identity);
            Call(board, "Interact");
            yield return new WaitForSecondsRealtime(.3f);
            Assert.AreEqual(false, Private(board, "showLegacyUi"));
            Assert.AreEqual(true, Get(Get(Find("CurtainCall.UI.SceneIntegration.WaitingRoomScheduleUI"), "View"), "IsVisible"));
        }
        IEnumerator ReadyToMorning(int day, object ui)
        {
            Call(Get(Private(ui, "readyButton"), "onClick"), "Invoke");
            Call(Get(ui, "View"), "Close");
            float until = Time.realtimeSinceStartup + 15;
            while ((Field(State, "Phase").ToString() == "Night" || Field(State, "Phase").ToString() == "Dawn")
                && Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual(day, Field(State, "Day"));
            Assert.AreEqual(day == 4 && Application.version == "0.1.0" ? "DemoEnding" : "RestTime", Field(State, "Phase").ToString());
            yield return new WaitForSecondsRealtime(2.2f);
        }
        [UnityTest]
        public IEnumerator W3_EditConfirmReady_ThreeNightsAndFinalMorning()
        {
            yield return Launch(1, "Night", 0, "", "WaitingRoomBase");
            yield return OpenSchedule();
            var ui = Find("CurtainCall.UI.SceneIntegration.WaitingRoomScheduleUI");
            var schedule = Find("CurtainCall.WaitingRoom.ScheduleBoard");
            var book = Get(schedule, "Book");
            Assert.AreEqual(3, ((IEnumerable)Get(Call(ui, "Build"), "Days")).Cast<object>().Count());
            Assert.AreEqual(false, Get(Private(ui, "readyButton"), "interactable"));
            Call(ui, "Confirm");
            Assert.AreEqual(false, Get(book, "IsConfirmedTonight"));
            var edits = (Array)Private(ui, "editButtons");
            Assert.IsFalse(ui.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("EditDay")), "Separate editing buttons must be removed.");
            for (int i = 2; i < 6; i++)
            {
                var card = (UnityEngine.UI.Button)edits.GetValue(i);
                var events = UnityEngine.EventSystems.EventSystem.current;
                var rect = (RectTransform)card.transform;
                var pointer = new UnityEngine.EventSystems.PointerEventData(events) {
                    position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)),
                    button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };
                var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                events.RaycastAll(pointer, hits);
                Assert.IsNotEmpty(hits);
                var clicked = UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerClickHandler>(hits[0].gameObject);
                Assert.AreEqual(card.gameObject, clicked, "The visible schedule entry must receive the click.");
                UnityEngine.EventSystems.ExecuteEvents.Execute(clicked, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            }
            Assert.AreEqual("Tightrope", Call(book, "Get", 2, 0));
            Call(Get(edits.GetValue(3), "onClick"), "Invoke");
            Assert.AreEqual("HumanCannon", Call(book, "Get", 2, 1));
            Call(Get(Private(ui, "confirmButton"), "onClick"), "Invoke");
            yield return new WaitForSecondsRealtime(.2f);
            Assert.AreEqual(true, Get(book, "IsConfirmedTonight"));
            Assert.AreEqual(true, Call(book, "IsLocked", 2));
            Assert.AreEqual(false, Call(book, "IsLocked", 3));
            Assert.IsTrue(((Component)edits.GetValue(2)).gameObject.activeInHierarchy, "Locked entries stay visible.");
            Assert.AreEqual(false, Get(edits.GetValue(2), "interactable"));
            Call(ui, "Cycle", 2, 0);
            Assert.AreEqual("Tightrope", Call(book, "Get", 2, 0));
            yield return ReadyToMorning(2, ui);
            Call(Get(ui, "View"), "Close");
            yield return null;
            Assert.AreEqual(false, Get(Find("CurtainCall.WaitingRoom.ScheduleBoardView"), "IsOpen"));
            Assert.AreEqual(false, Static("CurtainCall.Player.InputGate", "IsRequested"));

            yield return Launch(2, "Night", 0, "", "WaitingRoomBase");
            yield return OpenSchedule();
            ui = Find("CurtainCall.UI.SceneIntegration.WaitingRoomScheduleUI");
            schedule = Find("CurtainCall.WaitingRoom.ScheduleBoard"); book = Get(schedule, "Book");
            Assert.AreEqual(false, Call(book, "IsEditable", 2));
            Call(ui, "Cycle", 3, 0); Call(ui, "Cycle", 3, 1); Call(ui, "Confirm");
            Assert.AreEqual(true, Call(book, "IsLocked", 3));
            yield return ReadyToMorning(3, ui);

            yield return Launch(3, "Night", 0, "", "WaitingRoomBase");
            yield return OpenSchedule();
            ui = Find("CurtainCall.UI.SceneIntegration.WaitingRoomScheduleUI");
            schedule = Find("CurtainCall.WaitingRoom.ScheduleBoard"); book = Get(schedule, "Book");
            Assert.AreEqual(true, Get(book, "IsConfirmedTonight"));
            Assert.AreEqual("", Call(book, "Get", 4, 0));
            Assert.IsFalse(((Component)Private(ui, "confirmButton")).gameObject.activeSelf);
            Assert.AreEqual(true, Get(Private(ui, "readyButton"), "interactable"));
            // Older wire data must not reintroduce day-four performances or a confirmation gate.
            var oldSlots = (string[])Call(book, "ExportSlots"); oldSlots[6] = "Tightrope"; oldSlots[7] = "HumanCannon";
            Assert.AreEqual(true, Call(book, "Load", 3, false, oldSlots, new bool[4]));
            Assert.AreEqual("", Call(book, "Get", 4, 0)); Assert.AreEqual(true, Get(book, "IsConfirmedTonight"));
            yield return ReadyToMorning(4, ui);
            Assert.AreEqual(Application.version != "0.1.0", Field(State, "IsFinalMorning"));
        }
    }
}
