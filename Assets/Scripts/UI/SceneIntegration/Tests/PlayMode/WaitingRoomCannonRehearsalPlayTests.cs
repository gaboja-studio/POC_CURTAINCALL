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
        const string CannonRehearsalType = "CurtainCall.WaitingRoom.WaitingRoomCannonRehearsal";
        static object CannonLesson => T(CannonRehearsalType).GetField("instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
        static bool CannonFlag(string property) => (bool)Static(CannonRehearsalType, property);
        static object CannonStatic(string method, params object[] args) => T(CannonRehearsalType).GetMethod(method).Invoke(null, args);
        static IEnumerator CannonWait(Func<bool> condition, float seconds, string message)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(condition(), message);
        }
        static bool OnCannonFloor(object floor)
        {
            var b = ((Collider)floor).bounds; var p = LocalPlayer.transform.position;
            return Mathf.Abs(p.y - b.max.y) < .4f && p.x > b.min.x && p.x < b.max.x && p.z > b.min.z && p.z < b.max.z;
        }
        IEnumerator OpenCannonWaitingRoom(int day, string phase)
        {
            WaitingVariantField.SetValue(WaitingSpaces, Enum.Parse(WaitingVariantField.FieldType, "Integrated"));
            yield return Launch(day, phase, 0, phase == "Night" ? "" : "HumanCannon", "WaitingRoom");
            var run = Static("CurtainCall.Campaign.CampaignRun", "Instance");
            Call(run, "DiscoverTrick", "HumanCannon");
            yield return CannonWait(() => (bool)Field(State, "CannonUnlocked"), 3, "The original discovery unlock must propagate.");
        }

        [UnityTest]
        public IEnumerator W6_CannonRehearsal_AcquireWithFAndConfirmWithPointer()
        {
            WaitingVariantField.SetValue(WaitingSpaces, Enum.Parse(WaitingVariantField.FieldType, "Integrated"));
            yield return Launch(1, "Between", 1, "Tightrope", "WaitingRoom");
            yield return WaitQuestReady("second-pick");
            var board = Static("CurtainCall.WaitingRoom.ScheduleBoard", "Instance");
            Assert.IsFalse((bool)Field(State, "CannonUnlocked"), "Start this path before discovery, not with a HumanCannon dev launch.");
            Assert.IsFalse(CannonFlag("CanPractice"));
            Assert.AreEqual("Locked", Call(board, "Set", 1, 1, "HumanCannon", 1).ToString());
            var discovery = Find("CurtainCall.WaitingRoom.PerformancePropDiscovery");
            Assert.IsNotNull(discovery, "The integrated map must contain the real discoverable prop set.");
            var props = ((System.Collections.IEnumerable)Get(discovery, "PracticeProps")).Cast<GameObject>().ToArray();
            Assert.IsNotEmpty(props);
            Assert.IsTrue(props.All(p => p != null && !p.activeInHierarchy));
            var pickup = (Component)UnityEngine.Object.FindObjectsByType(T("CurtainCall.WaitingRoom.PerformancePropPickup"), FindObjectsSortMode.None)
                .First(p => ReferenceEquals(Priv(p, "owner"), discovery));
            var point = (Vector3)Get(pickup, "PresentationPoint");
            var mover = LocalPlayer.GetComponent(T("CurtainCall.Player.PlayerMover"));
            Call(mover, "Relocate", new Vector3(point.x, pickup.transform.position.y + .1f, point.z - 2f), Quaternion.identity);
            yield return new WaitForSecondsRealtime(.4f);
            Assert.GreaterOrEqual((float)Call(pickup, "Reach", LocalPlayer.transform.position), 0f, "The real pickup must be reachable.");
            yield return FlowTap();
            yield return CannonWait(() => (bool)Field(State, "CannonUnlocked"), 4, "F on the storage prop must unlock the cannon.");
            yield return null;
            Assert.IsFalse(pickup.gameObject.activeInHierarchy);
            Assert.IsTrue(props.All(p => p.activeInHierarchy));
            Assert.IsNotNull(Find("CurtainCall.WaitingRoom.RehearsalCannonPracticeStation"));
            Assert.IsTrue(CannonFlag("CanPractice"));
            yield return OpenSchedule();
            yield return CannonWait(() => !(bool)Get(Find("Unity.Cinemachine.CinemachineBrain"), "IsBlending"), 5,
                "Wait for the real board close-up before aiming the pointer.");
            var ui = Find("CurtainCall.UI.SceneIntegration.WaitingRoomScheduleUI");
            var pointer = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            try
            {
                var edits = (UnityEngine.UI.Button[])Priv(ui, "editButtons");
                yield return ClickScheduleButtonWithMouse(pointer, edits[1]);
                Assert.AreEqual("HumanCannon", Call(Get(board, "Book"), "Get", 1, 1));
                yield return ClickScheduleButtonWithMouse(pointer, (UnityEngine.UI.Button)Priv(ui, "confirmButton"));
                yield return CannonWait(() => (bool)Priv(CannonLesson, "prompt"), 3, "Actual confirm click must offer rehearsal.");
                Assert.IsFalse((bool)Get(Get(board, "Book"), "FirstDayConfirmed"));
                var choices = (UnityEngine.UI.Button[])Priv(Priv(CannonLesson, "votePanel"), "buttons");
                yield return ClickScheduleButtonWithMouse(pointer, choices[1]);
                Assert.IsFalse(CannonFlag("IsActive"));
                Assert.IsFalse((bool)Get(Get(board, "Book"), "FirstDayConfirmed"));
                yield return CannonWait(() => !(bool)Get(Find("Unity.Cinemachine.CinemachineBrain"), "IsBlending"), 5,
                    "Declining rehearsal must return the camera to the editable board.");
                yield return ClickScheduleButtonWithMouse(pointer, (UnityEngine.UI.Button)Priv(ui, "confirmButton"));
                yield return CannonWait(() => (bool)Priv(CannonLesson, "prompt"), 3, "Confirm must remain available after declining.");
                yield return ClickScheduleButtonWithMouse(pointer, choices[0]);
                yield return CannonWait(() => CannonFlag("IsActive") && Get(CannonLesson, "Course") != null
                    && !(bool)Static("CurtainCall.Player.InputGate", "IsBlocked"), 6, "Yes must enter the native lesson and release placement input.");
                Assert.IsFalse((bool)Get(Get(board, "Book"), "FirstDayConfirmed"));
            }
            finally { UnityEngine.InputSystem.InputSystem.RemoveDevice(pointer); }
            CannonStatic("VoteToAbandon", true);
            yield return CannonWait(() => !CannonFlag("IsActive") && !(bool)Static("CurtainCall.Player.InputGate", "IsBlocked"), 7, "Abandon must return to the unconfirmed schedule.");
            Assert.IsFalse(CannonFlag("HasMastered"));
        }

        [UnityTest]
        public IEnumerator W6_CannonRehearsal_SkipButtonRecordsMasteryBeforeArrival()
        {
            yield return OpenCannonWaitingRoom(1, "Night");
            var board = Static("CurtainCall.WaitingRoom.ScheduleBoard", "Instance");
            for (int day = 2; day <= 3; day++) for (int slot = 0; slot < 2; slot++)
                Assert.AreEqual("None", Call(board, "Set", day, slot, day == 2 && slot == 0 ? "HumanCannon" : "Tightrope", 1).ToString());
            Assert.IsTrue((bool)CannonStatic("StartPractice"));
            yield return CannonWait(() => Get(CannonLesson, "Course") != null
                && !(bool)Static("CurtainCall.Player.InputGate", "IsBlocked"), 6, "Skip must be offered after initial placement.");
            Assert.AreEqual(0, CannonStatic("Checkpoint", LocalId));
            Assert.IsFalse((bool)CannonStatic("HasArrived", LocalId));
            var panel = Priv(CannonLesson, "votePanel");
            var buttons = (UnityEngine.UI.Button[])Priv(panel, "buttons");
            Assert.IsTrue(buttons.All(b => b.gameObject.activeInHierarchy && b.IsInteractable()), "Skip and abandon remain distinct choices.");
            CursorTestKey("Escape"); yield return null; yield return null; CursorTestKey(null);
            yield return CursorState(true);
            var pointer = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            int actualClicks = 0;
            buttons[0].onClick.AddListener(() => actualClicks++);
            try { yield return ClickScheduleButtonWithMouse(pointer, buttons[0]); }
            finally { UnityEngine.InputSystem.InputSystem.RemoveDevice(pointer); }
            Assert.AreEqual(1, actualClicks, "The skip click must reach the real button exactly once.");
            yield return CannonWait(() => !CannonFlag("IsActive") && !(bool)Static("CurtainCall.Player.InputGate", "IsBlocked"), 7, "One of one is a majority, even before arrival.");
            Assert.IsTrue(CannonFlag("HasMastered"));
            Assert.IsFalse((bool)Get(Static("CurtainCall.UI.SceneIntegration.CommonMenuUI", "Current"), "IsOpen"), "Return within the same scene must close the ESC menu used to click the vote.");
            Assert.IsTrue((bool)Field(Priv(CannonLesson, "shared"), "skipped"));
            Assert.AreEqual("None", Call(board, "Confirm").ToString(), "Skipped rehearsal must unlock normal schedule confirmation.");
            Assert.Less(Vector3.Distance(LocalPlayer.transform.position, (Vector3)Priv(CannonLesson, "entrance")), 3f);
            Assert.IsTrue((bool)CannonStatic("StartPractice"));
            yield return CannonWait(() => Get(CannonLesson, "Course") != null
                && !(bool)Static("CurtainCall.Player.InputGate", "IsBlocked"), 6, "Skip retains optional reentry.");
            Assert.AreEqual(0, CannonStatic("Checkpoint", LocalId));
            CursorTestKey("Escape"); yield return null; yield return null; CursorTestKey(null);
            yield return CursorState(true);
            pointer = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            try { yield return ClickScheduleButtonWithMouse(pointer, buttons[1]); }
            finally { UnityEngine.InputSystem.InputSystem.RemoveDevice(pointer); }
            yield return CannonWait(() => !CannonFlag("IsActive") && !(bool)Static("CurtainCall.Player.InputGate", "IsBlocked"), 7, "Abandoning subsequent practice must return control.");
            Assert.IsTrue(CannonFlag("HasMastered"), "Abandon does not erase an existing mastery record.");
        }

        [UnityTest]
        public IEnumerator W6_CannonRehearsal_ConfirmNativeFlightAndCheckpointRecovery()
        {
            yield return OpenCannonWaitingRoom(1, "Night");
            var board = Static("CurtainCall.WaitingRoom.ScheduleBoard", "Instance");
            for (int day = 2; day <= 3; day++) for (int slot = 0; slot < 2; slot++)
                Assert.AreEqual("None", Call(board, "Set", day, slot, day == 2 && slot == 0 ? "HumanCannon" : "Tightrope", 1).ToString());
            Assert.AreEqual("RehearsalRequired", Call(board, "Confirm").ToString());
            Assert.IsFalse((bool)Get(Get(board, "Book"), "IsConfirmedTonight"));
            CannonStatic("AnswerSchedulePrompt", false);
            Assert.IsFalse(CannonFlag("IsActive"));
            Assert.IsFalse((bool)Get(Get(board, "Book"), "IsConfirmedTonight"), "No must not confirm the schedule.");
            Assert.AreEqual("RehearsalRequired", Call(board, "Confirm").ToString());
            CannonStatic("AnswerSchedulePrompt", true);
            yield return CannonWait(() => CannonFlag("IsActive") && Get(CannonLesson, "Course") != null, 6, "The cannon lesson must start in the same waiting room.");
            yield return CannonWait(() => !(bool)Static("CurtainCall.Player.InputGate", "IsBlocked"), 4, "Placement acknowledgement must release movement.");
            var course = (Component)Get(CannonLesson, "Course");
            var cannon = Field(course, "Cannon"); var transport = Field(course, "Transport");
            Assert.IsTrue((bool)Get(cannon, "PracticeMode"));
            Assert.AreEqual(4, Get(cannon, "StationCount"));
            Assert.IsFalse((bool)Get(transport, "OverloadTrap"), "Latest actual performance rejects overload without dropping the floor.");
            Assert.AreEqual(new Vector2(2.75f, 2f), Get(transport, "HalfSize"), "Do not replace the actual scene footprint with the prepared prefab default.");
            Assert.AreEqual("WaitingRoom", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            var station = (Component)Call(cannon, "Station", 0);
            var boarding = (Transform)Get(station, "BoardingPoint");
            var mover = LocalPlayer.GetComponent(T("CurtainCall.Player.PlayerMover"));
            Call(mover, "Relocate", boarding.position, boarding.rotation);
            yield return new WaitForSecondsRealtime(.3f);
            yield return FlowTap();
            yield return CannonWait(() => (bool)Call(cannon, "IsSeated", LocalId), 4, "Native F must board the copied original cannon.");
            float fireDeadline = Time.realtimeSinceStartup + 8f;
            while (!(bool)Call(cannon, "IsFlying", LocalId) && Time.realtimeSinceStartup < fireDeadline)
            { yield return new WaitForSecondsRealtime(.8f); yield return FlowTap(); }
            Assert.IsTrue((bool)Call(cannon, "IsFlying", LocalId), "A fresh F must fire through the original pilot.");
            yield return CannonWait(() => (int)CannonStatic("Checkpoint", LocalId) == 1, 8, "Original no-steering shot must safely reach the middle checkpoint, including ceiling clearance.");
            Assert.IsTrue(OnCannonFloor(Field(course, "MiddleFloor")));
            Assert.IsNotNull(Field(course, "DeviceUi"), "Reuse the original cannon contextual binder.");
            Call(mover, "Relocate", (Vector3)Get(transport, "StartPosition"), Quaternion.identity);
            yield return CannonWait(() => (bool)Get(Field(course, "Tooltip"), "HasRuntimePresentation"), 3,
                "Original transport status tooltip must also render in the unscored waiting-room lesson.");
            foreach (var label in ((Component)Field(course, "Tooltip")).GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                foreach (char expected in label.text)
                    if (expected >= '\uAC00' && expected <= '\uD7A3')
                        Assert.IsTrue(label.font.HasCharacter(expected, true, true), "Missing original Korean glyph: " + expected);
                label.ForceMeshUpdate(true);
                for (int i = 0; i < label.textInfo.characterCount; i++)
                {
                    var character = label.textInfo.characterInfo[i];
                    if (character.character >= '\uAC00' && character.character <= '\uD7A3')
                        Assert.AreEqual((uint)character.character, character.textElement.unicode,
                            "Original contextual Korean text must not render as missing-glyph squares: " + label.text);
                }
            }
            var condition = Get(LocalPlayer, "LostParts");
            T("CurtainCall.HumanCannon.HumanCannonSystem").GetMethod("ServerFallInjury").Invoke(null, new object[] { LocalPlayer });
            Assert.AreEqual("Fallen", Get(LocalPlayer, "State").ToString());
            yield return CannonWait(() => Get(LocalPlayer, "State").ToString() == "Normal", 4, "Practice death must recover after its death animation.");
            Assert.AreEqual(condition, Get(LocalPlayer, "LostParts"));
            Assert.IsTrue(OnCannonFloor(Field(course, "MiddleFloor")), "Recovery must use the acquired internal checkpoint.");
            Assert.IsFalse(CannonFlag("HasMastered"), "Middle arrival alone is not mastery.");
            CannonStatic("VoteToAbandon", true);
            yield return CannonWait(() => !CannonFlag("IsActive") && !(bool)Static("CurtainCall.Player.InputGate", "IsBlocked"), 7, "Majority abandon must return and unlock all controls.");
            Assert.IsFalse(CannonFlag("HasMastered"));
            Assert.IsFalse((bool)Get(Get(board, "Book"), "IsConfirmedTonight"));
            Assert.IsTrue((bool)Get(mover, "Simulated"));
            Assert.IsTrue((bool)Get(mover, "ControlEnabled"));
            Assert.Less(Vector3.Distance(LocalPlayer.transform.position, (Vector3)Priv(CannonLesson, "entrance")), 3f,
                $"Returned feet={LocalPlayer.transform.position}, entrance={Priv(CannonLesson, "entrance")}, shared={JsonUtility.ToJson(Priv(CannonLesson, "shared"))}, ending={Priv(CannonLesson, "endingRound")}, returned={Priv(CannonLesson, "returnedRound")}");
        }

        [UnityTest]
        public IEnumerator W6_CannonRehearsal_NewRoomResetsReturnTransition()
        {
            for (int room = 0; room < 2; room++)
            {
                yield return OpenCannonWaitingRoom(2, "Morning");
                Assert.IsTrue((bool)CannonStatic("StartPractice"));
                yield return CannonWait(() => Get(CannonLesson, "Course") != null
                    && !(bool)Static("CurtainCall.Player.InputGate", "IsBlocked"), 6, "A new room must place its own first lesson.");
                CannonStatic("VoteToAbandon", true);
                yield return CannonWait(() => !CannonFlag("IsActive") && !(bool)Static("CurtainCall.Player.InputGate", "IsBlocked"), 7,
                    "Abandon must return control in each room.");
                Assert.Less(Vector3.Distance(LocalPlayer.transform.position, (Vector3)Priv(CannonLesson, "entrance")), 3f,
                    $"Room {room}: a previous room's return marker must not suppress this return. Feet={LocalPlayer.transform.position}, shared={JsonUtility.ToJson(Priv(CannonLesson, "shared"))}, ending={Priv(CannonLesson, "endingRound")}, returned={Priv(CannonLesson, "returnedRound")}");
                if (room == 0)
                {
                    var session = Static("CurtainCall.Network.Session.NetworkSessionManager", "Instance");
                    var leaving = (System.Threading.Tasks.Task)Call(session, "LeaveAsync");
                    while (!leaving.IsCompleted) yield return null;
                    yield return null;
                }
            }
        }

        [UnityTest]
        public IEnumerator W6_CannonRehearsal_RightFirstShotClearsParkedTrampoline()
        {
            yield return OpenCannonWaitingRoom(2, "Morning");
            Assert.IsTrue((bool)CannonStatic("StartPractice"));
            yield return CannonWait(() => Get(CannonLesson, "Course") != null && !(bool)Static("CurtainCall.Player.InputGate", "IsBlocked"), 6, "Initial course placement must finish.");
            var course = (Component)Get(CannonLesson, "Course");
            var cannon = Field(course, "Cannon");
            var trampoline = Field(course, "Trampoline");
            int bounces = (int)Get(trampoline, "LocalBounceCount");
            var station = (Component)Call(cannon, "Station", 1);
            var boarding = (Transform)Get(station, "BoardingPoint");
            Call(LocalPlayer.GetComponent(T("CurtainCall.Player.PlayerMover")), "Relocate", boarding.position, boarding.rotation);
            yield return new WaitForSecondsRealtime(.3f);
            yield return FlowTap();
            yield return CannonWait(() => (bool)Call(cannon, "IsSeated", LocalId), 4, "F must board the right first cannon.");
            float fireDeadline = Time.realtimeSinceStartup + 8f;
            while (!(bool)Call(cannon, "IsFlying", LocalId) && Time.realtimeSinceStartup < fireDeadline)
            { yield return new WaitForSecondsRealtime(.8f); yield return FlowTap(); }
            Assert.IsTrue((bool)Call(cannon, "IsFlying", LocalId));
            yield return CannonWait(() => (int)CannonStatic("Checkpoint", LocalId) == 1, 9, "The parked trampoline must not intercept the right first shot before its middle checkpoint.");
            Assert.IsTrue(OnCannonFloor(Field(course, "MiddleFloor")));
            Assert.AreEqual(bounces, Get(trampoline, "LocalBounceCount"), "Both first cannons need a clear landing before the trampoline lesson.");
            CannonStatic("VoteToAbandon", true);
            yield return CannonWait(() => !CannonFlag("IsActive") && !(bool)Static("CurtainCall.Player.InputGate", "IsBlocked"), 7, "Cleanup must return control.");
        }

        [UnityTest]
        public IEnumerator W6_CannonRehearsal_AbandonReentryPreservesExistingBody()
        {
            yield return OpenCannonWaitingRoom(2, "Morning");
            Call(LocalPlayer, "ServerSetOneArm");
            var condition = Get(LocalPlayer, "LostParts");
            Assert.AreNotEqual(0, Convert.ToInt32(condition));
            Assert.IsTrue((bool)CannonStatic("StartPractice"));
            yield return CannonWait(() => Get(CannonLesson, "Course") != null && !(bool)Static("CurtainCall.Player.InputGate", "IsBlocked"), 6, "Native course placement must finish.");
            var course = (Component)Get(CannonLesson, "Course");
            T("CurtainCall.HumanCannon.HumanCannonSystem").GetMethod("ServerFallInjury").Invoke(null, new object[] { LocalPlayer });
            yield return CannonWait(() => Get(LocalPlayer, "State").ToString() == "Normal", 4, "Training death must recover.");
            Assert.AreEqual(condition, Get(LocalPlayer, "LostParts"), "Recovery must neither add injuries nor heal pre-existing ones.");
            Assert.IsTrue(OnCannonFloor(Field(course, "StartFloor")));
            var middle = ((Collider)Field(course, "MiddleFloor")).bounds;
            var mover = LocalPlayer.GetComponent(T("CurtainCall.Player.PlayerMover"));
            Call(mover, "Relocate", new Vector3(middle.center.x, middle.max.y, middle.center.z), Quaternion.identity);
            yield return CannonWait(() => (int)CannonStatic("Checkpoint", LocalId) == 1, 3, "Prepared checkpoint fixture must be accepted by the host.");
            T("CurtainCall.HumanCannon.HumanCannonSystem").GetMethod("ServerFallInjury").Invoke(null, new object[] { LocalPlayer });
            Assert.AreEqual("Fallen", Get(LocalPlayer, "State").ToString());
            CannonStatic("VoteToAbandon", true);
            yield return CannonWait(() => !CannonFlag("IsActive") && !(bool)Static("CurtainCall.Player.InputGate", "IsBlocked"), 7, "Abandon must finish without mastery.");
            Assert.IsFalse(CannonFlag("HasMastered"));
            Assert.AreEqual("Normal", Get(LocalPlayer, "State").ToString(), "An abandon during the death animation must not leave a dead player at the entrance.");
            Assert.AreEqual(condition, Get(LocalPlayer, "LostParts"));
            Assert.IsTrue((bool)CannonStatic("StartPractice"));
            yield return CannonWait(() => Get(CannonLesson, "Course") != null && !(bool)Static("CurtainCall.Player.InputGate", "IsBlocked"), 6, "Reentry must place at the first pad again.");
            Assert.AreEqual(0, CannonStatic("Checkpoint", LocalId));
            Assert.IsTrue(OnCannonFloor(Field(Get(CannonLesson, "Course"), "StartFloor")));
            CannonStatic("VoteToAbandon", true);
            yield return CannonWait(() => !CannonFlag("IsActive") && !(bool)Static("CurtainCall.Player.InputGate", "IsBlocked"), 7, "Cleanup must unlock the player.");
            FlowKeys();
        }
    }
}
