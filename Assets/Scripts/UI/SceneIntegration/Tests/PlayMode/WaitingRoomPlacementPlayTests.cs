using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        static void CapturePlacement(string name)
        { var dir = Environment.GetEnvironmentVariable("CURTAINCALL_CAPTURE_DIR"); if (!string.IsNullOrEmpty(dir)) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, name)); }
        // 배관함도 BreakableWall이라 화장실 벽은 ID로 고른다.
        static Component RestroomBreach() => Object.FindObjectsByType(T("CurtainCall.WaitingRoom.BreakableWall"), FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Cast<Component>().Single(w => (string)Get(w, "TargetId") == "restroom-breach");
        Component Placed(string id) => Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .First(c => c != null && T("CurtainCall.WaitingRoom.IExplorationTarget").IsInstanceOfType(c) && (string)Get(c, "TargetId") == "structure-a:" + id);
        void NearPlaced(Component target, float floor)
        {
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover"));
            mover.GetType().GetProperty("Simulated").SetValue(mover, false);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var configured = target.GetType().GetField("reach", flags);
            float range = configured != null ? (float)configured.GetValue(target) : 1f;
            var visual = target.GetType().GetProperty("VisualRoot")?.GetValue(target) as Transform ?? target.transform;
            var presentation = target.GetType().GetProperty("PresentationPoint");
            var point = presentation != null ? (Vector3)presentation.GetValue(target) : target.transform.position + Vector3.up * .125f;
            var capsule = player.GetComponent<CharacterController>(); float radius = capsule.radius, height = capsule.height;
            foreach (var distance in new[] { .65f, .85f, 1.05f, 1.3f, 1.5f })
            {
                if (distance > range) continue;
                for (int i = 0; i < 24; i++)
                {
                    float angle = i * Mathf.PI / 12;
                    var at = target.transform.position + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * distance; at.y = floor + .05f;
                    var overlaps = Physics.OverlapCapsule(at + Vector3.up * (radius + .03f), at + Vector3.up * (height - radius + .03f), radius * .95f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    if (overlaps.Any(c => c.GetComponentInParent(T("CurtainCall.Player.PlayerMover")) == null)) continue;
                    float reach = (float)T("CurtainCall.WaitingRoom.ExplorationReach").GetMethod("From").Invoke(null, new object[] { visual, point, at, range, target.GetType() == T("CurtainCall.WaitingRoom.ExplorationGate") });
                    if (reach < 0) continue;
                    Call(mover, "SetStartPose", at, Quaternion.identity); return;
                }
            }
            Assert.Fail("No collision-free interaction approach for " + target.name + " at " + target.transform.position);
        }
        IEnumerator UsePlaced(string id, float floor = 0)
        {
            var target = Placed(id); NearPlaced(target, floor); yield return new WaitForSecondsRealtime(.15f);
            Call(Static("CurtainCall.WaitingRoom.ExplorationState", "Instance"), "RequestUse", "structure-a:" + id); yield return null;
        }
        IEnumerator PickPlaced(string item, string room, string objectName = null)
        {
            var carry = Static("CurtainCall.WaitingRoom.PropCarry", "Instance");
            var spot = Object.FindObjectsByType(T("CurtainCall.WaitingRoom.FoundItemSpot"), FindObjectsSortMode.None).Cast<Component>()
                .First(s => s.transform.IsChildOf(GameObject.Find("WaitingRoom_LayoutA").transform.Find(room))
                    && (objectName == null || s.name == objectName)
                    && (string)Get(Call(s, "MakeItem", (int)Field(State, "Generation")), "ItemId") == item);
            NearPlaced(spot, 0); yield return new WaitForSecondsRealtime(.2f);
            var value = Call(spot, "MakeItem", (int)Field(State, "Generation")); Call(carry, "RequestPickUp", Get(value, "Uid")); yield return null;
            Assert.AreEqual(item, Get(Call(carry, "Selected", (ulong)Get(carry, "LocalClientId")), "ItemId"));
        }
        [UnityTest]
        public IEnumerator M2_Placement_KeysPasswordPanelAndSoloExit()
        {
            yield return Launch(2, "Morning", 0, "Tightrope", "WaitingRoomBase");
            var state = Static("CurtainCall.WaitingRoom.ExplorationState", "Instance");
            var layout = GameObject.Find("WaitingRoom_LayoutA").transform;
            var sealedDoor = layout.Find("B1_EscapeEntrance/SealedDoor");
            yield return null;
            // 탈출로 문은 숨은 문 너머라 처음부터 보인다.
            Assert.AreEqual(true, Get(Placed("escape-door"), "Visible")); Assert.IsTrue(sealedDoor.Find("EscapeDoor").gameObject.activeInHierarchy);
            // 기계실 숨겨진 문: 벽과 같은 판이 문 자리를 덮고, 직접 F할 수 없다.
            var machine = Placed("machine-hidden-door"); var machineBlock = machine.GetComponent<BoxCollider>();
            Assert.AreEqual(false, Get(machine, "Opened")); Assert.IsTrue(machineBlock.enabled); Assert.AreEqual(false, Get(machine, "IsAvailable"), "No F on the hidden door itself");
            var hiddenPanel = layout.Find("B1_MachineRoom/Exploration_A/HiddenPanel"); var panelClosed = hiddenPanel.position;
            var panelBounds = hiddenPanel.GetComponent<Renderer>().bounds;
            Assert.Less(Mathf.Abs(panelBounds.center.z - machineBlock.bounds.center.z), .05f, "Panel covers the doorway");
            Assert.Less(panelBounds.size.x, .03f, "Flush with the wall, not a protruding door");
            Assert.Greater(panelBounds.size.z, machineBlock.bounds.size.z, "Panel also covers the jambs");

            // 배관함: 말뚝 뽑개 3타 전에는 키패드를 쓸 수 없다.
            var pad = Placed("escape-pad"); var pipeBox = Placed("machine-pipe-box");
            var keypad = pipeBox.transform.Find("Keypad").GetComponent<Renderer>().bounds;
            Assert.IsTrue(pipeBox.GetComponentsInChildren<Collider>().Select(c => c.bounds).Any(b => b.max.y > keypad.max.y && b.min.y < keypad.max.y + .9f), "Keypad sits inside the pipe box");
            Assert.Less(Mathf.Abs(keypad.center.x - machineBlock.bounds.max.x), .4f, "Pipe box stands on the machine-room side of the hidden door");
            Assert.AreEqual(false, Get(pad, "Visible"));
            yield return PickPlaced("stake-puller", "S10_Workshop");
            for (int hit = 1; hit <= 3; hit++)
            {
                yield return UsePlaced("machine-pipe-box", -10); yield return new WaitForSecondsRealtime(.2f);
                Assert.AreEqual(hit, Call(state, "Hits", "structure-a:machine-pipe-box"));
            }
            Assert.AreEqual(true, Call(state, "IsOpen", "structure-a:machine-pipe-box")); Assert.AreEqual(true, Get(pad, "Visible"));
            yield return UsePlaced("steel-box"); Assert.AreEqual(false, Get(Placed("steel-box"), "Opened"));
            yield return UsePlaced("key-1"); yield return UsePlaced("steel-box"); yield return UsePlaced("management-key");
            yield return UsePlaced("management-door", -5); Assert.AreEqual(true, Get(Placed("management-door"), "Opened"));
            NearPlaced(pad, -10); yield return new WaitForSecondsRealtime(.15f);
            Call(pad, "Interact"); yield return null;
            var panel = pad.GetComponent(T("CurtainCall.WaitingRoom.ExplorationPanel")); Assert.IsNotNull(panel);
            Assert.AreEqual(true, Get(panel, "IsVisible"));
            CapturePlacement("structure-a-keypad.png");
            yield return new WaitForSecondsRealtime(.2f);
            Assert.IsTrue(panel.GetComponentsInChildren<Transform>(true).Any(t => t.name == "10_Body"), "기존 패널 몸체 재사용");
            foreach (var digit in new[] { "9", "9", "9", "9", "9" }) Call(panel, "PressDigit", digit);
            Assert.AreEqual("9999", Get(panel, "Entered")); Call(panel, "SubmitCode"); yield return new WaitForSecondsRealtime(.15f);
            Assert.AreEqual(false, Get(pad, "Opened")); Assert.AreEqual(true, Get(panel, "IsVisible"), "Wrong code keeps the keypad open for retry");
            Assert.AreEqual("", Get(panel, "Entered"), "Wrong code clears the entry"); Assert.IsNotNull(pad.GetComponent<AudioSource>(), "Wrong code plays the warning tone");
            foreach (var digit in new[] { "1", "2", "3", "4" }) Call(panel, "PressDigit", digit);
            Call(panel, "SubmitCode"); yield return null; yield return null;
            Assert.AreEqual(true, Get(pad, "Opened")); Assert.AreEqual(false, Get(panel, "IsVisible"));
            yield return null;
            Assert.AreEqual(true, Get(machine, "Opened"), "Code opens the hidden door"); Assert.IsFalse(machineBlock.enabled);
            var exit = Placed("escape-door"); Assert.AreEqual(true, Get(exit, "Visible")); Assert.AreEqual(false, Get(exit, "ConditionsMet"));
            Assert.IsTrue(new[] { "EscapeDoor", "EscapeSign" }.All(n => sealedDoor.Find(n).gameObject.activeInHierarchy));
            var hideOnOpen = (GameObject[])exit.GetType().GetField("hideOnOpen", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(exit);
            Assert.IsFalse(hideOnOpen.Any(g => g.name.StartsWith("Keypad") || g.name == "EscapeSign" || g.name == "HazardStripe" || g.name == "BeaconGlass" || g.name == "RustStain"), "Opening removes only the door and its locks");
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.Greater(panelClosed.z - hiddenPanel.position.z, 2.3f, "Hidden door slid aside after the code");
            var slotIndex = exit.GetType().GetMethod("SlotAt", new[] { typeof(Vector3) });
            foreach (var (slotName, index) in new[] { ("KeySlot_L", 0), ("KeySlot_R", 1) })
            {
                var slot = exit.transform.Find(slotName); Assert.IsTrue(slot.gameObject.activeInHierarchy);
                Assert.AreEqual(index, slotIndex.Invoke(exit, new object[] { new Vector3(slot.position.x + .7f, -10f, slot.position.z) }), slotName + " reachable in front of it");
            }
            yield return UsePlaced("escape-door", -10); Assert.AreEqual(false, Get(exit, "Opened"));
            Assert.AreEqual("RestTime", Field(State, "Phase").ToString());
        }
        [UnityTest]
        public IEnumerator M2_Placement_RealToolsUvContainersAndKey2()
        {
            yield return Launch(2, "Morning", 0, "Tightrope", "WaitingRoomBase");
            var state = Static("CurtainCall.WaitingRoom.ExplorationState", "Instance");
            var carry = Static("CurtainCall.WaitingRoom.PropCarry", "Instance");
            ulong me = (ulong)Get(carry, "LocalClientId");
            yield return PickPlaced("stake-puller", "S10_Workshop");
            int before = (int)Get(Call(carry, "Selected", me), "Uses");
            yield return UsePlaced("dormitory-box-1"); Assert.AreEqual(true, Get(Placed("dormitory-box-1"), "Opened"));
            int after = (int)Get(Call(carry, "Selected", me), "Uses"); Assert.AreEqual(before - 1, after);
            yield return UsePlaced("dormitory-box-1"); Assert.AreEqual(after, Get(Call(carry, "Selected", me), "Uses"));
            Assert.AreEqual(true, Get(Placed("dormitory-box-1").GetComponent(T("CurtainCall.WaitingRoom.ExplorationDrops")), "Pending"));
            yield return PickPlaced("magician-hairpin", "S10_Workshop");
            foreach (var id in new[] { "cabinet-1", "cabinet-2", "cabinet-3", "locked-box-1", "locked-box-2" })
            {
                for (int attempt = 0; attempt < 3 && !(bool)Call(state, "IsOpen", "structure-a:" + id); attempt++) yield return UsePlaced(id);
                Assert.AreEqual(true, Call(state, "IsOpen", "structure-a:" + id), id);
            }
            Assert.AreEqual(true, Get(Placed("map"), "Revealed"));
            yield return UsePlaced("stairs-key"); yield return UsePlaced("stairs-door");
            yield return UsePlaced("escape-key-1"); Assert.AreEqual(true, Get(Placed("stairs-door"), "Opened"));
            // 열쇠2는 획득만 유지. 기계실 숨겨진 문은 배관함 속 키패드로만 열린다(2026-10-30 결정).
            yield return UsePlaced("key-2"); Assert.AreEqual(true, Get(Placed("key-2"), "Opened"));
            Assert.AreEqual(false, Get(Placed("machine-hidden-door"), "Opened"));
        }
        [UnityTest]
        public IEnumerator M2_Placement_DrawerEscapeKeyAndSuccessOnlyDurability()
        {
            yield return Launch(2, "Morning", 0, "Tightrope", "WaitingRoomBase");
            yield return PickPlaced("stake-puller", "S10_Workshop");
            yield return UsePlaced("management-drawer", -5);
            Assert.AreEqual(true, Call(Static("CurtainCall.WaitingRoom.ExplorationState", "Instance"), "IsOpen", "structure-a:management-drawer"));
            yield return UsePlaced("escape-key-2", -5);
            Assert.AreEqual(true, Get(Placed("escape-key-2"), "Opened"));
            var carry = Static("CurtainCall.WaitingRoom.PropCarry", "Instance");
            Assert.AreEqual(0, Get(Call(carry, "Selected", (ulong)Get(carry, "LocalClientId")), "Uid"), "원래 발견품1회 내구도 유지");
        }
        [UnityTest]
        public IEnumerator M2_Placement_LobbyUvThreeMarksAndMachineMonitor()
        {
            yield return Launch(2, "Morning", 0, "Tightrope", "WaitingRoomBase");
            yield return PickPlaced("uv-lamp", "S01_Lobby", "lobby-uv-lamp-1");
            var state = Static("CurtainCall.WaitingRoom.ExplorationState", "Instance");
            yield return UsePlaced("uv-3"); Assert.AreEqual(true, Call(state, "IsOpen", "structure-a:uv-3"));
            // 2026-10-30: 장비형 UV 등은 내구도가 없어 다른 낙서도 이어서 발견한다.
            yield return UsePlaced("uv-1"); Assert.AreEqual(true, Call(state, "IsOpen", "structure-a:uv-1"));
            Assert.AreEqual(true, Get(Placed("hidden-graffiti-3"), "Revealed"));
            // 숨겨진 낙서는 비추는 동안만 켜진다. 조사하려면 빛 안에 있어야 한다.
            var mark3 = Object.FindObjectsByType(T("CurtainCall.WaitingRoom.UvMark"), FindObjectsSortMode.None).Cast<Component>().Single(m => (string)Get(m, "TargetId") == "structure-a:uv-3");
            Call(mark3, "Illuminate"); yield return null; Call(mark3, "Illuminate");
            var clue = Placed("hidden-graffiti-3"); Assert.IsTrue(clue.gameObject.activeInHierarchy, "비추는 동안 보인다");
            NearPlaced(clue, 0); Call(clue, "SetOpen", true); yield return null;
            Assert.AreEqual(true, Get(clue, "IsOpen"));
            Call(clue, "SetOpen", false);
            var monitor = Placed("monitor"); NearPlaced(monitor, 0); yield return new WaitForSecondsRealtime(.15f); Call(monitor, "Interact"); yield return null;
            var image = monitor.GetComponentInChildren<UnityEngine.UI.RawImage>(true); Assert.IsNotNull(image); Assert.IsInstanceOf<RenderTexture>(image.texture);
            var camera = (Camera)monitor.GetType().GetField("monitorCamera", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(monitor);
            Assert.IsTrue(camera.enabled); Assert.AreSame(image.texture, camera.targetTexture);
            CapturePlacement("structure-a-monitor.png");
            yield return new WaitForSecondsRealtime(.2f);
            monitor.gameObject.SetActive(false); yield return null; Assert.IsFalse(camera.enabled);
            Assert.AreEqual(false, Static("CurtainCall.Player.InputGate", "IsRequested"));
        }
    }
}
