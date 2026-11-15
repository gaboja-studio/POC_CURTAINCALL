using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
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
        const string TicketCaptures = "Tasks/Active/Task-20261114-132-schedule-ui-redesign/Captures";
        static IEnumerator TicketCapture(string name)
        {
            Directory.CreateDirectory(TicketCaptures);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(TicketCaptures + "/" + name + ".png"));
            yield return null; yield return null;
        }

        [UnityTest, Category("ScheduleTicketV2")]
        public IEnumerator ScheduleTicket_RealPointerArrowsSlotsOcclusionAndRestore()
        {
            yield return Launch(1, "Night", 0, "", "WaitingRoomBase");
            int startFrame = Time.frameCount;
            var run = Static("CurtainCall.Campaign.CampaignRun", "Instance");
            Call(run, "RecordClear", "Tightrope", 1);
            var rehearsal = Find("CurtainCall.WaitingRoom.WaitingRoomCannonRehearsal");
            var shared = Private(rehearsal, "shared");
            shared.GetType().GetField("mastered").SetValue(shared, true); // Test fixture only; no game rule/config changes.
            Call(run, "DiscoverTrick", "HumanCannon");
            Call(run, "RecordClear", "HumanCannon", 1);
            yield return null;
            var initialSchedule = Static("CurtainCall.WaitingRoom.ScheduleBoard", "Instance");
            Assert.AreEqual("None", Call(initialSchedule, "Set", 2, 0, "Tightrope", 1).ToString());
            Call(initialSchedule, "Set", 2, 1, "HumanCannon", 1);
            Call(initialSchedule, "Set", 3, 0, "Tightrope", 1);
            Call(initialSchedule, "Set", 3, 1, "HumanCannon", 1);
            yield return OpenSchedule();
            yield return CannonWait(() => !(bool)Get(Find("Unity.Cinemachine.CinemachineBrain"), "IsBlending"), 5, "Board camera");
            var ui = Find("CurtainCall.UI.SceneIntegration.WaitingRoomScheduleUI");
            var board = Find("CurtainCall.WaitingRoom.ScheduleBoardView");
            var schedule = Static("CurtainCall.WaitingRoom.ScheduleBoard", "Instance");
            var book = Get(schedule, "Book");
            var view = (Component)Get(ui, "View");
            var canvas = view.GetComponentInParent<Canvas>();
            var cards = view.GetComponentsInChildren(T("CurtainCall.UI.SceneIntegration.ScheduleTicketCard"), true);
            var arrows = (Button[,])Private(ui, "tierButtons");
            var slots = (Button[])Private(ui, "editButtons");
            Assert.AreEqual(6, cards.Length);
            foreach (var card in cards)
            {
                var title = (TMP_Text)Field(card, "performanceName");
                var tier = (TMP_Text)Field(card, "tierLabel");
                Assert.GreaterOrEqual(title.fontSize, tier.fontSize * 1.8f);
                title.ForceMeshUpdate(); Assert.IsFalse(title.isTextTruncated, title.text);
                var eyebrow = (TMP_Text)Field(card, "slotLabel"); eyebrow.ForceMeshUpdate();
                Assert.IsFalse(eyebrow.isTextTruncated, "Show order must remain visible: " + eyebrow.text);
            }
            Assert.IsFalse(arrows[0, 0].interactable); Assert.IsFalse(arrows[0, 1].interactable);
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
            Assert.Greater(Time.frameCount, startFrame, "Verification must run on advancing frames.");

            var camera = Camera.main;
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var blocker = new GameObject("ScheduleTest_OtherCharacterVisualAndCollider");
            var meshes = new System.Collections.Generic.List<Mesh>();
            var physicsRaycaster = camera.gameObject.AddComponent<PhysicsRaycaster>();
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                // Bake the real player's skin into a render-only second character. No extra network actor is spawned.
                foreach (var skin in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var mesh = new Mesh(); skin.BakeMesh(mesh); meshes.Add(mesh);
                    var part = new GameObject(skin.name, typeof(MeshFilter), typeof(MeshRenderer));
                    part.transform.SetParent(blocker.transform, false);
                    part.transform.SetPositionAndRotation(skin.transform.position, skin.transform.rotation);
                    part.transform.localScale = skin.transform.lossyScale;
                    part.GetComponent<MeshFilter>().sharedMesh = mesh;
                    part.GetComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                }
                Assert.Greater(meshes.Count, 0, "Use actual character geometry for obstruction proof.");
                var bounds = new Bounds(); bool first = true;
                foreach (var renderer in blocker.GetComponentsInChildren<Renderer>())
                { if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds); }
                blocker.transform.position += camera.transform.position + camera.transform.forward * 1.2f - bounds.center;
                var collider = blocker.AddComponent<BoxCollider>(); collider.center = blocker.transform.InverseTransformPoint(camera.transform.position + camera.transform.forward * 1.2f); collider.size = Vector3.one * 1.2f;
                yield return null;
                // World-only camera proof: the second-character mesh really lies between camera and board.
                var rt = RenderTexture.GetTemporary(1280, 720, 24);
                var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
                var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                try
                {
                    camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                    image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply(); Directory.CreateDirectory(TicketCaptures);
                    File.WriteAllBytes(Path.GetFullPath(TicketCaptures + "/occluder-world-camera.png"), image.EncodeToPNG());
                }
                finally { camera.targetTexture = oldTarget; RenderTexture.active = oldActive; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.Destroy(image); }
                string beforeTrick = (string)Call(book, "Get", 2, 0);
                Assert.AreEqual(1, Call(book, "Difficulty", 2, 0));
                yield return ClickScheduleButtonWithMouse(mouse, arrows[2, 1]);
                Assert.AreEqual(2, Call(book, "Difficulty", 2, 0));
                Assert.AreEqual(beforeTrick, Call(book, "Get", 2, 0), "Arrow must not bubble into the performance slot.");
                Assert.IsFalse(arrows[2, 1].interactable, "Locked third tier disables increase.");
                Assert.AreEqual("2단계 적용됨", ((TMP_Text)Field(cards[2], "feedback")).text);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(10, 10) }); yield return null;
                yield return TicketCapture("focused-tier-applied-with-character");
                yield return ClickScheduleButtonWithMouse(mouse, slots[2]);
                Assert.AreEqual("HumanCannon", Call(book, "Get", 2, 0), "Ticket body changes the show.");
                Assert.AreEqual(2, Call(book, "Difficulty", 2, 0), "Changing to a trick with this tier unlocked preserves difficulty.");
                yield return ClickScheduleButtonWithMouse(mouse, arrows[2, 0]);
                Assert.AreEqual(1, Call(book, "Difficulty", 2, 0));
                Assert.AreEqual("HumanCannon", Call(book, "Get", 2, 0));
                Assert.IsFalse(arrows[2, 0].interactable, "Tier one has no wraparound.");
                Call(run, "RecordClear", "HumanCannon", 2);
                yield return new WaitForSecondsRealtime(.2f);
                yield return ClickScheduleButtonWithMouse(mouse, arrows[2, 1]);
                yield return ClickScheduleButtonWithMouse(mouse, arrows[2, 1]);
                Assert.AreEqual(3, Call(book, "Difficulty", 2, 0));
                Assert.IsFalse(arrows[2, 1].interactable, "Maximum tier has no wraparound.");
                yield return ClickScheduleButtonWithMouse(mouse, arrows[2, 0]);
                yield return new WaitForSecondsRealtime(1.6f);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(10, 10) }); yield return null;
                yield return TicketCapture("focused-final-layout");

                // Render the client/read-only contract, using the same component as network clients.
                Call(cards[2], "Present", 0, "인간 대포", 2, 3, false, false, false, true);
                Assert.IsFalse(slots[2].interactable); Assert.IsFalse(arrows[2, 0].interactable); Assert.IsFalse(arrows[2, 1].interactable);
                Assert.AreEqual("호스트 전용", ((TMP_Text)Field(cards[2], "feedback")).text);
                yield return TicketCapture("readonly-client-presentation");
                ui.GetType().GetMethod("RefreshControls", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(ui, null);

                Call(view, "Close"); yield return new WaitForSecondsRealtime(.3f);
                Assert.IsFalse((bool)Get(board, "IsOpen"));
                Assert.AreEqual(RenderMode.WorldSpace, canvas.renderMode);
                Assert.IsTrue(canvas.transform.IsChildOf(board.transform));
                Assert.IsFalse(canvas.GetComponent<GraphicRaycaster>().enabled);
                Assert.IsFalse((bool)Static("CurtainCall.WaitingRoom.ScheduleScreenHold", "IsHeld"));
                foreach (var renderer in blocker.GetComponentsInChildren<Renderer>()) Assert.IsTrue(renderer.enabled, "No remote renderer is changed by focus.");
                Call(board, "SetOpen", true); yield return new WaitForSecondsRealtime(.3f);
                Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode, "Reopening restores overlay and interaction.");
                yield return ClickScheduleButtonWithMouse(mouse, arrows[2, 0]);
                Assert.AreEqual(1, Call(book, "Difficulty", 2, 0));
                Call(view, "Close"); yield return null;
            }
            finally
            {
                InputSystem.RemoveDevice(mouse); UnityEngine.Object.Destroy(physicsRaycaster); UnityEngine.Object.Destroy(blocker);
                foreach (var mesh in meshes) UnityEngine.Object.Destroy(mesh);
            }
        }
    }
}
