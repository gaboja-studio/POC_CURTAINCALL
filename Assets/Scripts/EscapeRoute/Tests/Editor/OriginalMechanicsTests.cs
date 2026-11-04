using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    /// <summary>입력·이동·네트워크 플레이 없이 구간 연결의 수명과 원본 기본값을 검사한다.</summary>
    public sealed class OriginalMechanicsTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        readonly List<Object> created = new();
        static Type TypeOf(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(name)).First(t => t != null);
        static object Get(object target, string name) => target.GetType().GetProperty(name, Flags)?.GetValue(target)
            ?? target.GetType().GetField(name, Flags)?.GetValue(target);
        static void Set(object target, string name, object value)
        {
            var property = target.GetType().GetProperty(name, Flags);
            if (property != null) property.SetValue(target, value);
            else target.GetType().GetField(name, Flags).SetValue(target, value);
        }
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
        GameObject Root(string name) { var root = new GameObject(name); root.SetActive(false); created.Add(root); return root; }
        Component Add(GameObject root, string name) => root.AddComponent(TypeOf(name));
        ScriptableObject Scheme(string name) { var scheme = ScriptableObject.CreateInstance(TypeOf(name)); created.Add(scheme); return scheme; }

        [TearDown]
        public void Cleanup()
        {
            // 게임용 Pilot은 Destroy를 사용하므로 EditMode에서는 소유 참조를 먼저 분리한다.
            foreach (var root in created.OfType<GameObject>())
            {
                var pilot = root != null ? root.GetComponent(TypeOf("CurtainCall.HumanCannon.HumanCannonPilot")) : null;
                if (pilot != null) Set(pilot, "scheme", null);
            }
            foreach (var item in created) if (item != null) Object.DestroyImmediate(item);
            created.Clear();
        }

        [Test]
        public void InspectionRejectsRetirementAsWellAsFallsAndArrivalOnlyForInspectedActor()
        {
            var session = Add(Root("Inspection state fixture"), "CurtainCall.EscapeRoute.EscapeInspectionSession");
            var actor = Add(Root("Inspected actor"), "CurtainCall.Network.PlayerSync.NetworkPlayer");
            ulong id = (ulong)Get(actor, "OwnerClientId");
            ((HashSet<ulong>)Get(session, "inspecting")).Add(id);
            var state = TypeOf("CurtainCall.Network.PlayerSync.PlayerState");
            foreach (string name in new[] { "Fallen", "Retired", "Arrived" })
                Assert.IsFalse((bool)Call(session, "AllowState", actor, Enum.Parse(state, name)), name);
            Assert.IsTrue((bool)Call(session, "AllowState", actor, Enum.Parse(state, "Normal")));
            ((HashSet<ulong>)Get(session, "inspecting")).Clear();
            Assert.IsTrue((bool)Call(session, "AllowState", actor, Enum.Parse(state, "Retired")));
        }

        Component Course(bool authored)
        {
            var root = Root("Authored rope fixture");
            root.transform.SetPositionAndRotation(new Vector3(51, -6, 77), Quaternion.LookRotation(new Vector3(13, 0, 5)));
            var course = Add(root, "CurtainCall.Tightrope.TightropeCourse");
            Set(course, "useSceneGeometry", authored);
            var data = new SerializedObject(course);
            data.FindProperty("sceneShape").FindPropertyRelative("laneCount").intValue = 1;
            data.ApplyModifiedPropertiesWithoutUndo();
            return course;
        }

        [Test]
        public void AuthoredGeometryPreservesSpawnAndRegistersRotatedRopeWithoutBuildingStage()
        {
            var course = Course(true);
            var spawn = Root("Unrelated spawn").transform;
            spawn.position = new Vector3(26, .05f, 75);
            Set(course, "spawnPoints", new[] { spawn });
            var support = Root("Support").AddComponent<BoxCollider>();
            Set(course, "sceneRopes", new Collider[] { support });
            Call(course, "Rebuild");
            Assert.That(spawn.position, Is.EqualTo(new Vector3(26, .05f, 75)));
            Assert.That(course.transform.Find("Generated"), Is.Null);
            Assert.That(((IDictionary)Get(course, "ropeSurfaces")).Contains(support), Is.True);
            float distance = (float)Call(course, "GetDistance", new Vector3(64, -6, 82));
            Assert.That(distance, Is.EqualTo(Mathf.Sqrt(194)).Within(.001f));
            Assert.That(Vector3.Distance((Vector3)Call(course, "GetLanePosition", 0, distance), new Vector3(64, -6, 82)),
                Is.LessThan(.001f));
        }

        [Test]
        public void RopeHandoffRestoresExactCannonInstanceAndPreviousCourseAxis()
        {
            var course = Course(true);
            var switcher = Add(course.gameObject, "CurtainCall.Tightrope.CourseControlSwitcher");
            Call(switcher, "Awake");
            var actor = Root("Unspawned player fixture");
            var mover = Add(actor, "CurtainCall.Player.PlayerMover");
            Call(mover, "Awake");
            var controller = Add(actor, "CurtainCall.Player.PlayerController");
            Call(controller, "Awake");
            var player = Add(actor, "CurtainCall.Network.PlayerSync.NetworkPlayer");
            var pilot = Add(actor, "CurtainCall.HumanCannon.HumanCannonPilot");
            var cannon = Scheme("CurtainCall.HumanCannon.HumanCannonControlScheme");
            Set(pilot, "scheme", cannon);
            Call(controller, "SetScheme", cannon);
            Call(switcher, "Bind", player);
            Set(mover, "CourseForwardOverride", Vector3.left);
            Set(mover, "LaneSpacing", 7f);
            Assert.That(Get(switcher, "CanBorrowCannonControls"), Is.True);
            Call(switcher, "Apply", true);
            Assert.That(Get(controller, "Scheme").GetType().Name, Is.EqualTo("TightropeControlScheme"));
            Assert.That((Vector3)Get(mover, "CourseForward"), Is.EqualTo((Vector3)Get(course, "Forward")));
            Call(switcher, "ReleaseControls");
            Assert.That(Get(controller, "Scheme"), Is.SameAs(cannon));
            Assert.That((Vector3)Get(mover, "CourseForward"), Is.EqualTo(Vector3.left));
            Assert.That(Get(mover, "LaneSpacing"), Is.EqualTo(7f));
            Set(pilot, "stationIndex", 0);
            Assert.That(Get(switcher, "CanBorrowCannonControls"), Is.False, "대포 점유 중에는 조작을 빌리지 않는다.");
            Set(pilot, "stationIndex", -1);
        }

        [Test]
        public void AuthoredRunDoesNotRejectDeathsAndOriginalCourseRemainsOptIn()
        {
            var course = Course(true);
            var run = Add(course.gameObject, "CurtainCall.Tightrope.TightropeRun");
            Set(run, "course", course);
            var fallen = Enum.Parse(TypeOf("CurtainCall.Network.PlayerSync.PlayerState"), "Fallen");
            Assert.That(Call(run, "CanChangeState", null, fallen), Is.True,
                "외줄 공연이 시작되지 않았다는 이유로 탈출로의 사망을 막지 않는다.");
            Assert.That(Get(Course(false), "UsesSceneGeometry"), Is.False);
        }

        [Test]
        public void IndependentRopesUseTheirOwnDirectionHeightLengthAndCannotJumpAcrossTheMap()
        {
            var course = Course(true);
            Set(course, "independentSceneRopes", true);
            var a = Root("A03 support").AddComponent<BoxCollider>();
            a.transform.SetPositionAndRotation(new Vector3(51,-6,77), Quaternion.LookRotation(new Vector3(13,0,5)));
            a.center = new Vector3(0,-.1f,Mathf.Sqrt(194)/2); a.size = new Vector3(.4f,.2f,Mathf.Sqrt(194));
            var b = Root("A05 support").AddComponent<BoxCollider>();
            b.transform.SetPositionAndRotation(new Vector3(89,0,28),Quaternion.Euler(0,90,0));
            b.center = new Vector3(0,-.1f,3.6f); b.size = new Vector3(.4f,.2f,7.2f);
            Set(course, "sceneRopes", new Collider[] {a,b});
            var data = new SerializedObject(course); var shape = data.FindProperty("sceneShape");
            shape.FindPropertyRelative("laneCount").intValue = 2;
            var ends = shape.FindPropertyRelative("laneEndDistances"); ends.arraySize = 2;
            ends.GetArrayElementAtIndex(0).floatValue = Mathf.Sqrt(194); ends.GetArrayElementAtIndex(1).floatValue = 7.2f;
            data.ApplyModifiedPropertiesWithoutUndo(); Call(course,"Rebuild");
            Assert.That(Vector3.Distance((Vector3)Call(course,"GetLanePosition",0,Mathf.Sqrt(194)),new Vector3(64,-6,82)),Is.LessThan(.001f));
            Assert.That(Vector3.Distance((Vector3)Call(course,"GetLanePosition",1,7.2f),new Vector3(96.2f,0,28)),Is.LessThan(.001f));
            Assert.That(Call(course,"RopeHeightAt",new Vector3(92,0,28)),Is.EqualTo(0f).Within(.001f));
            Assert.That(Call(course,"GetDistance",new Vector3(92,0,28)),Is.EqualTo(3f).Within(.001f));
            Assert.That(Vector3.Dot((Vector3)Call(course,"ForwardAt",new Vector3(92,0,28)),Vector3.right),Is.GreaterThan(.999f));
            object[] on = { new Vector3(92,0,28), .25f, 0 };
            Assert.That(Call(course,"IsOnRope",on),Is.False,"비활성 소실줄은 지지줄이 아니다.");
            a.gameObject.SetActive(true); b.gameObject.SetActive(true);
            Assert.That(Call(course,"IsOnRope",on),Is.True); Assert.That(on[2],Is.EqualTo(1));
            object[] below = { new Vector3(92,-6,28), .25f, 0 };
            Assert.That(Call(course,"IsOnRope",below),Is.False);
            Assert.That(Call(course,"FindLandingLane",new Vector3(92,0,28),-1),Is.EqualTo(-1));
            Assert.That(Call(course,"PastSceneRopeEnd",new Vector3(97,-.2f,28),.3f),Is.True);
            Assert.That(Call(course,"PastSceneRopeEnd",new Vector3(92,0,28),.3f),Is.False);
        }

        [Test]
        public void PlatformPoleFlightSurvivesSnapshotAndRejectsStaleSegment()
        {
            var type = TypeOf("CurtainCall.HumanCannon.CannonFlightState");
            var host = Activator.CreateInstance(type); var client = Activator.CreateInstance(type);
            Call(host,"Begin",7UL,-2,9U);
            Assert.That(Call(host,"TryRebound",7UL,-2,9U,0U),Is.True);
            Assert.That(Call(host,"TryRebound",7UL,-2,9U,0U),Is.False);
            Assert.That(Call(client,"Restore",Call(host,"Capture"),1),Is.True);
            Assert.That(Call(client,"Matches",7UL,-2,9U,1U),Is.True);
            Assert.That(Call(client,"TryComplete",7UL,-2,9U,1U),Is.True);
            Assert.That(Call(client,"Contains",7UL),Is.False);
        }

        [Test]
        public void InspectionMovesRealPlayerAndReturnsThereWithoutReplacingRestartPose()
        {
            var actor = Root("Unspawned inspection player fixture");
            var mover = Add(actor,"CurtainCall.Player.PlayerMover"); Call(mover,"Awake");
            var player = Add(actor,"CurtainCall.Network.PlayerSync.NetworkPlayer");
            Vector3 start = new(26,.05f,73.5f);
            Set(mover,"startPosition",start); Set(mover,"startRotation",Quaternion.identity); actor.transform.position = start;
            var camera = Root("Inspection camera fixture");
            var inspection = Add(camera,"CurtainCall.EscapeRoute.EscapeInspectionCamera"); Call(inspection,"Awake");
            Set(inspection,"player",player); Set(inspection,"mover",mover);
            Set(inspection,"eyeHeight",1.6f); Set(inspection,"yaw",90f); Set(inspection,"IsInspecting",true);
            camera.transform.position = new Vector3(112,8,28);
            Call(inspection,"SyncPlayer");
            Assert.That(Vector3.Distance(actor.transform.position,new Vector3(112,6.4f,28)),Is.LessThan(.001f));
            Assert.That(Get(mover,"Simulated"),Is.False);
            Assert.That(((CharacterController)Get(mover,"Body")).enabled,Is.False);
            Call(inspection,"EndInspection",true);
            Assert.That(Vector3.Distance(actor.transform.position,new Vector3(112,6.4f,28)),Is.LessThan(.001f));
            Assert.That(Get(mover,"Simulated"),Is.True);
            Assert.That(((CharacterController)Get(mover,"Body")).enabled,Is.True);
            Assert.That(Get(mover,"startPosition"),Is.EqualTo(start));
        }
    }
}

namespace CurtainCall.EscapeRoute.Tests
{
    public sealed class CooperativeRopesTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const string CoopType = "CurtainCall.EscapeRoute.EscapeCooperativeRopes";
        const string PlayerType = "CurtainCall.Network.PlayerSync.NetworkPlayer";
        readonly Vector3 origin = new(7000, 7000, 7000);
        readonly List<GameObject> roots = new();
        Scene testScene;
        Component coop, switcher;
        object previousCoop;
        IList allPlayers;
        object[] previousPlayers;
        Collider[] supports;
        Renderer[] visuals;
        GameObject returnGate, lowerGate;
        Transform overflow;

        static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        static object Get(object target, string name) => target.GetType().GetProperty(name, Flags)?.GetValue(target)
            ?? target.GetType().GetField(name, Flags)?.GetValue(target);
        static void Set(object target, string name, object value)
        {
            var property = target.GetType().GetProperty(name, Flags);
            if (property != null) property.SetValue(target, value);
            else target.GetType().GetField(name, Flags).SetValue(target, value);
        }
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);

        [SetUp]
        public void SetUp()
        {
            testScene = SceneManager.GetActiveScene();
            previousCoop = T(CoopType).GetProperty("Current").GetValue(null);
            T(CoopType).GetProperty("Current").SetValue(null, null);
            allPlayers = (IList)T(PlayerType).GetField("all", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            previousPlayers = allPlayers.Cast<object>().ToArray(); allPlayers.Clear();
            var root = Root("Cooperative ropes", false);
            coop = root.AddComponent(T(CoopType));
            var geometry = Root("FourOriginalRopes").transform;
            supports = new Collider[4];
            var renderers = new List<Renderer>();
            for (int i = 0; i < supports.Length; i++)
            {
                var wire = Root("TimedWire_" + i);
                wire.transform.SetParent(geometry, true);
                wire.transform.position = origin + new Vector3(i * 2, 0, 0);
                var box = wire.AddComponent<BoxCollider>();
                box.center = new Vector3(0, -.05f, 14); box.size = new Vector3(.4f, .1f, 28);
                supports[i] = box;
                renderers.Add(Visual(geometry, wire.name + " 시각선 0", origin + new Vector3(i * 2, .05f, 8)));
                renderers.Add(Visual(geometry, "외줄 고정 베이스", origin + new Vector3(i * 2, .045f, 0)));
                renderers.Add(Visual(wire.transform, "Nested rope mesh", origin + new Vector3(i * 2, .05f, 18)));
            }
            visuals = renderers.ToArray();
            returnGate = Root("Return gate", false); lowerGate = Root("Lower gate", false);
            var lower = Root("Lower worker bounds").AddComponent<BoxCollider>();
            lower.isTrigger = true; lower.center = origin + new Vector3(3, -3, 14); lower.size = new Vector3(10, 6, 32);
            overflow = Root("Overflow return").transform;
            overflow.position = origin + new Vector3(-3, .05f, -2);
            Set(coop, "ropeSupports", supports); Set(coop, "ropeVisuals", visuals);
            Set(coop, "returnGate", returnGate); Set(coop, "lowerEntryGate", lowerGate);
            Set(coop, "lowerWorkerBounds", lower); Set(coop, "overflowReturnPoint", overflow);
            Physics.SyncTransforms(); Call(coop, "OnEnable");
        }

        [TearDown]
        public void TearDown()
        {
            if (coop != null) Call(coop, "OnDisable");
            if (switcher != null) Call(switcher, "OnDestroy");
            foreach (var player in allPlayers.Cast<object>().ToArray()) Set(player, "IsSpawned", false);
            allPlayers.Clear();
            foreach (var player in previousPlayers) allPlayers.Add(player);
            for (int i = roots.Count - 1; i >= 0; i--) if (roots[i] != null) Object.DestroyImmediate(roots[i]);
            roots.Clear();
            T(CoopType).GetProperty("Current").SetValue(null, previousCoop);
            Physics.SyncTransforms();
        }

        [Test]
        public void SharedClockFlashesThreeTimesBeforeAllFourDropAndReturn()
        {
            var arcs = supports.Select(s => s.gameObject.AddComponent<LineRenderer>()).ToArray();
            foreach (var arc in arcs) arc.enabled = false;
            Set(coop, "warningArcs", arcs);
            foreach (double offset in new[] { 0d, 70000d })
                foreach (var sample in new[] { (1.99, false, true), (2.0, true, true), (2.5, false, true),
                    (2.8, true, true), (3.1, false, true), (3.5, true, true), (3.99, false, true),
                    (4.0, false, false), (6.999, false, false), (7.0, false, true) })
                {
                    Call(coop, "SetPhase", Call(coop, "PhaseAt", offset + sample.Item1));
                    Assert.That(arcs.All(a => a.enabled == sample.Item2), Is.True);
                    Assert.That(supports.All(c => c.enabled == sample.Item3), Is.True);
                    Assert.That(visuals.All(r => r.enabled == sample.Item3), Is.True);
                }
        }

        [Test]
        public void HiddenCyclePreservesAuthoredDisabledPieces()
        {
            supports[1].enabled = false; visuals[8].enabled = false;
            Call(coop, "OnEnable");
            Call(coop, "SetPhase", Call(coop, "PhaseAt", 5d));
            Call(coop, "SetPhase", Call(coop, "PhaseAt", 7d));
            Assert.That(supports[1].enabled, Is.False);
            Assert.That(visuals[8].enabled, Is.False);
        }

        [Test]
        public void RemovingAllTimedRopesReleasesTheSupportedMover()
        {
            var mover = SurfaceFixture();
            Assert.That(Call(coop, "ApplyRopeVisibility", false, mover), Is.True);
            Assert.That(supports.All(s => !s.enabled), Is.True);
        }

        [Test]
        public void OverlappingOrdinaryPlatformDoesNotTriggerAuthoredDrop()
        {
            var mover = SurfaceFixture();
            var platform = Root("Supporting ordinary floor").AddComponent<BoxCollider>();
            platform.center = origin + new Vector3(0, -.1f, 6); platform.size = new Vector3(2, .2f, 4);
            Physics.SyncTransforms();
            Assert.That(Call(coop, "ApplyRopeVisibility", false, mover), Is.False);
        }

        [Test]
        public void FirstWorkerKeepsReservationAndSimultaneousEntrantsUseLowestId()
        {
            LowerFloor();
            var first = Player(9); Call(coop, "RefreshWorker");
            Assert.That(Get(coop, "WorkerOwner"), Is.EqualTo(9UL));
            var other = Player(2); Call(coop, "RefreshWorker");
            Assert.That(Get(coop, "WorkerOwner"), Is.EqualTo(9UL));
            first.transform.position = origin + Vector3.up * 10; Physics.SyncTransforms(); Call(coop, "RefreshWorker");
            Assert.That(Get(coop, "WorkerOwner"), Is.EqualTo(2UL));
            Call(coop, "SetWorker", ulong.MaxValue); first.transform.position = other.transform.position;
            Physics.SyncTransforms(); Call(coop, "RefreshWorker");
            Assert.That(Get(coop, "WorkerOwner"), Is.EqualTo(2UL));
            var state = Get(other, "state");
            Set(state, "m_InternalValue", Enum.Parse(T("CurtainCall.Network.PlayerSync.PlayerState"), "Fallen"));
            Call(coop, "RefreshWorker"); Assert.That(Get(coop, "WorkerOwner"), Is.EqualTo(9UL));
            Set(first, "IsSpawned", false); Call(coop, "RefreshWorker");
            Assert.That(Get(coop, "WorkerOwner"), Is.EqualTo(ulong.MaxValue));
            Assert.That(lowerGate.activeSelf, Is.False);
        }

        [Test]
        public void ReturnButtonReleasesWorkerAndEntryGateIndependently()
        {
            Call(coop, "SetWorker", 3UL);
            Assert.That(lowerGate.activeSelf, Is.True); Assert.That(returnGate.activeSelf, Is.True);
            Call(coop, "SetGate", true);
            Assert.That(Get(coop, "WorkerOwner"), Is.EqualTo(ulong.MaxValue));
            Assert.That(lowerGate.activeSelf, Is.False); Assert.That(returnGate.activeSelf, Is.False);
            Player(3); Call(coop, "RefreshWorker");
            Assert.That(Get(coop, "WorkerOwner"), Is.EqualTo(ulong.MaxValue));
        }

        [Test]
        public void OverflowRequiresSynchronizationAndReturnsOnlyLocalNonOwnerWithoutDamage()
        {
            LowerFloor();
            var player = Player(2); Set(player, "IsOwner", true); Call(coop, "SetWorker", 1UL);
            Assert.That(Call(coop, "CanReturnOverflow", player), Is.False);
            Set(coop, "synchronized", true); Assert.That(Call(coop, "CanReturnOverflow", player), Is.True);
            Set(player, "IsOwner", false); Assert.That(Call(coop, "CanReturnOverflow", player), Is.False);
            Set(player, "IsOwner", true); Call(coop, "SetWorker", 2UL);
            Assert.That(Call(coop, "CanReturnOverflow", player), Is.False);
            Call(coop, "SetWorker", 1UL);
            var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover"));
            Call(mover, "ResetExternalMotion", true);
            Assert.That(Call(coop, "CanReturnOverflow", player), Is.False, "A trampoline flight is not an extra grounded worker.");
            Call(mover, "ResetExternalMotion", false);
            int teleports = 0;
            mover.GetType().GetEvent("Teleported").AddEventHandler(mover, (Action)(() => teleports++));
            Call(coop, "ReturnLocalOverflow", player);
            Assert.That(player.transform.position, Is.EqualTo(overflow.position));
            Assert.That(Get(mover, "CurrentJump").ToString(), Is.EqualTo("None"));
            Assert.That(Get(player, "State").ToString(), Is.EqualTo("Normal"));
            Assert.That(teleports, Is.EqualTo(1));
        }

        [Test]
        public void FallingTeammateCanReachTrampolineBeforeLowerWorkerLimitApplies()
        {
            LowerFloor();
            var player = Player(2); Set(player, "IsOwner", true);
            Set(coop, "synchronized", true); Call(coop, "SetWorker", 1UL);
            player.transform.position += Vector3.up * 1.5f; Physics.SyncTransforms();
            Assert.That(Call(coop, "CanReturnOverflow", player), Is.False,
                "A teammate falling toward the trampoline must not be returned before contact.");
            Call(coop, "ReturnLocalOverflow", player);
            Assert.That(player.transform.position, Is.Not.EqualTo(overflow.position));
            player.transform.position -= Vector3.up * 1.5f; Physics.SyncTransforms();
            Assert.That(Call(coop, "CanReturnOverflow", player), Is.True,
                "A second player actually standing on the lower work floor still returns.");
        }

        [Test]
        public void LateJoinStateRejectsOlderRevisionAndRequiresRestartBarrierToken()
        {
            Assert.That(Call(coop, "ApplyState", 5U, false, 7UL, 0, 0U), Is.True);
            Assert.That(Call(coop, "ApplyState", 5U, true, ulong.MaxValue, 0, 0U), Is.False);
            Assert.That(Get(coop, "workerOwner"), Is.EqualTo(7UL));
            Assert.That(Get(coop, "WorkerOwner"), Is.EqualTo(7UL)); Assert.That(lowerGate.activeSelf, Is.True);
            Assert.That(Call(coop, "ApplyState", 4U, true, ulong.MaxValue, 0, 0U), Is.False);
            int token = (int)Call(Get(coop, "resetBarrier"), "Begin");
            Assert.That(Call(coop, "ApplyState", 6U, false, ulong.MaxValue, 0, 0U), Is.False);
            Assert.That(Call(coop, "ApplyState", 6U, false, ulong.MaxValue, token, 0U), Is.True);
            Assert.That(Get(coop, "WorkerOwner"), Is.EqualTo(ulong.MaxValue)); Assert.That(lowerGate.activeSelf, Is.False);
        }

        [Test]
        public void DetachRestoresRopesAndClearsWorkerAndPendingReturn()
        {
            Call(coop, "SetPhase", Call(coop, "PhaseAt", 5d)); Call(coop, "SetWorker", 3UL);
            Set(coop, "pendingOverflow", 12U); Set(coop, "firstOverflowRequest", 10U);
            Call(coop, "Detach");
            Assert.That(supports.All(s => s.enabled), Is.True); Assert.That(visuals.All(v => v.enabled), Is.True);
            Assert.That(Get(coop, "WorkerOwner"), Is.EqualTo(ulong.MaxValue));
            Assert.That(Get(coop, "pendingOverflow"), Is.EqualTo(0U)); Assert.That(lowerGate.activeSelf, Is.False);
        }

        Component LocksFixture()
        {
            var value = Root("Cooperative locks fixture", false).AddComponent(T("CurtainCall.EscapeRoute.EscapeCooperativeLocks"));
            var checkpointType = value.GetType().GetNestedType("Checkpoint");
            var entries = Array.CreateInstance(checkpointType, 2);
            for (int i = 0; i < 2; i++) entries.SetValue(Activator.CreateInstance(checkpointType), i);
            Set(value, "checkpoints", entries);
            return value;
        }

        [Test]
        public void CooperativeLocksLateJoinKeepsBothCheckpointsAndRejectsOlderOpenState()
        {
            var locks = LocksFixture();
            Assert.That(Call(locks, "ApplyState", 8U, 1U, 2U, 0U, 0), Is.True);
            Assert.That(Call(locks, "IsLatched", 0), Is.False);
            Assert.That(Call(locks, "IsLatched", 1), Is.True);
            Assert.That(Call(locks, "ApplyState", 7U, 3U, 3U, 3U, 0), Is.False);
            Assert.That(Get(locks, "armed"), Is.EqualTo(1U));
            Assert.That(Get(locks, "latched"), Is.EqualTo(2U));
        }

        [Test]
        public void CooperativeLocksRestartRejectsDelayedSnapshotUntilMatchingReply()
        {
            var locks = LocksFixture();
            Call(locks, "ApplyState", 4U, 3U, 3U, 0U, 0);
            int token = (int)Call(Get(locks, "resetBarrier"), "Begin");
            Assert.That(Call(locks, "ApplyState", 5U, 3U, 3U, 3U, 0), Is.False);
            Assert.That(Call(locks, "ApplyState", 5U, 0U, 0U, 0U, token), Is.True);
            Assert.That(Get(locks, "latched"), Is.EqualTo(0U));
        }

        [Test]
        public void CooperativeLocksInvalidCheckpointBitsCannotReleaseRestartBarrier()
        {
            var locks = LocksFixture();
            int token = (int)Call(Get(locks, "resetBarrier"), "Begin");
            Assert.That(Call(locks, "ApplyState", 6U, 4U, 0U, 0U, token), Is.False);
            Assert.That(Get(Get(locks, "resetBarrier"), "Waiting"), Is.True);
            Assert.That(Call(locks, "ApplyState", 6U, 0U, 0U, 2U, token), Is.True);
        }

        [Test]
        public void CooperativeLocksLosingHelperDoesNotClearCompletedOtherSection()
        {
            var locks = LocksFixture();
            Call(locks, "ApplyState", 1U, 1U, 1U, 2U, 0);
            Call(locks, "ApplyState", 2U, 1U, 1U, 0U, 0);
            Assert.That(Call(locks, "IsLatched", 0), Is.True);
            Assert.That(Call(locks, "IsLatched", 1), Is.False);
            Call(locks, "Detach");
            Assert.That(Get(locks, "armed"), Is.EqualTo(0U));
            Assert.That(Get(locks, "latched"), Is.EqualTo(0U));
            Assert.That(Get(locks, "synchronized"), Is.False);
        }

        [Test]
        public void CooperativeLocksArrivalRequiresActualSupportNotOnlyTriggerOverlap()
        {
            var locks = LocksFixture(); var player = Player(27);
            var entries = (Array)Get(locks, "checkpoints");
            var zone = Root("Arrival volume").AddComponent<BoxCollider>(); zone.isTrigger = true;
            zone.transform.position = player.transform.position + Vector3.up * .5f; zone.size = Vector3.one * 2;
            Set(entries.GetValue(0), "arrival", zone);
            var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover")); Set(mover, "Simulated", true);
            Physics.SyncTransforms();
            Assert.That(Call(locks, "AtArrival", player, 0), Is.False, "A flying/unsupported player cannot complete by touching a trigger");
            var pad = Root("Real arrival slab").AddComponent<BoxCollider>();
            pad.transform.position = player.transform.position - Vector3.up * .15f; pad.size = new Vector3(3, .3f, 3);
            Physics.SyncTransforms();
            Assert.That(Call(locks, "AtArrival", player, 0), Is.True);
            Set(mover, "Simulated", false);
            Assert.That(Call(locks, "AtArrival", player, 0), Is.True, "The host does not simulate a remote owner's grounded mover.");
            pad.enabled = false; Physics.SyncTransforms();
            Assert.That(Call(locks, "AtArrival", player, 0), Is.False, "A remote actor still needs real support, not just trigger overlap.");
        }

        Component SurfaceFixture()
        {
            var courseObject = Root("Authored support course", false);
            var course = courseObject.AddComponent(T("CurtainCall.Tightrope.TightropeCourse"));
            Set(course, "useSceneGeometry", true); Set(course, "independentSceneRopes", true); Set(course, "sceneRopes", supports);
            var data = new SerializedObject(course); data.FindProperty("sceneShape").FindPropertyRelative("laneCount").intValue = 4;
            data.ApplyModifiedPropertiesWithoutUndo(); Call(course, "Rebuild");
            switcher = courseObject.AddComponent(T("CurtainCall.Tightrope.CourseControlSwitcher")); Call(switcher, "Awake");
            Set(coop, "switcher", switcher);
            var actor = Root("Surface actor", false);
            actor.transform.position = origin + new Vector3(0, 0, 6);
            var mover = actor.AddComponent(T("CurtainCall.Player.PlayerMover")); Call(mover, "Awake");
            actor.SetActive(true); Physics.SyncTransforms(); return mover;
        }

        void LowerFloor()
        {
            var floor = Root("Lower work floor").AddComponent<BoxCollider>();
            floor.center = origin + new Vector3(3, -3.1f, 14); floor.size = new Vector3(10, .2f, 32);
            Physics.SyncTransforms();
        }

        Component Player(ulong id)
        {
            var actor = Root("Worker " + id, false);
            actor.transform.position = origin + new Vector3(3, -3, 14);
            var player = actor.AddComponent(T(PlayerType));
            var mover = actor.GetComponent(T("CurtainCall.Player.PlayerMover")); Call(mover, "Awake");
            Set(player, "mover", mover);
            Set(player, "IsSpawned", true); Set(player, "OwnerClientId", id);
            actor.SetActive(true); allPlayers.Add(player); Physics.SyncTransforms(); return player;
        }

        Renderer Visual(Transform parent, string name, Vector3 at)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.hideFlags = HideFlags.DontSave;
            SceneManager.MoveGameObjectToScene(go, testScene); roots.Add(go);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, true); go.transform.position = at; go.transform.localScale = Vector3.one * .1f;
            return go.GetComponent<Renderer>();
        }

        GameObject Root(string name, bool active = true)
        {
            var value = new GameObject(name) { hideFlags = HideFlags.DontSave };
            value.SetActive(active); SceneManager.MoveGameObjectToScene(value, testScene); roots.Add(value); return value;
        }
    }
}

namespace CurtainCall.EscapeRoute.Tests
{
    public sealed class EscapeCampaignFallPolicyTests
    {
        [NUnit.Framework.Test]
        public void CampaignEscapeProtectsTransferredPlayerWithoutDevelopmentLauncher()
        {
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
            var assemblies=System.AppDomain.CurrentDomain.GetAssemblies();
            var type=assemblies.Select(a=>a.GetType("CurtainCall.EscapeRoute.EscapeCampaignContent")).First(t=>t!=null);
            var policy=assemblies.Select(a=>a.GetType("CurtainCall.EscapeRoute.EscapeDevelopmentSession")).First(t=>t!=null);
            var previous=type.GetProperty("Current").GetValue(null);
            var root=new UnityEngine.GameObject("Campaign fall regression");root.SetActive(false);
            var actor=new UnityEngine.GameObject("Player belonging to another loaded scene");
            var preview=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try
            {
                var component=root.AddComponent(type);
                var body=new UnityEngine.GameObject("Escape content");body.transform.SetParent(root.transform,false);
                type.GetField("content",flags).SetValue(component,body);
                root.SetActive(true);
                type.GetProperty("Current").SetValue(null,component);
                type.GetProperty("IsCampaign").SetValue(component,true);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(actor,preview);
                var method=policy.GetMethod("FallInjuriesSuppressed");
                NUnit.Framework.Assert.That(method.Invoke(null,new object[]{actor.transform}),Is.True,"Campaign protection must not depend on a disabled development launcher or the player's original scene.");
                NUnit.Framework.Assert.That(policy.GetMethod("FallConsequencesSuppressed").Invoke(null,new object[]{actor.transform}),Is.False,"Do not disable campaign balance or authored death zones to fix cannon injuries.");
                body.SetActive(false);
                NUnit.Framework.Assert.That(method.Invoke(null,new object[]{actor.transform}),Is.False,"Dormant additive content must not change another performance's fall rules.");
                body.SetActive(true);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,preview);
                NUnit.Framework.Assert.That(method.Invoke(null,new object[]{actor.transform}),Is.False,"Only the active escape space may suppress falls.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(actor);
                type.GetProperty("Current").SetValue(null,previous);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview);
            }
        }
    }
}

namespace CurtainCall.EscapeRoute.Tests
{
    public sealed class EscapeArrivalCameraHandoffTests
    {
        [Test]
        public void TraversalPermanentlyReleasesArrivalViewEvenWhenStairExitWasSkipped()
        {
            var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            Type T(string name)=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).First(t=>t!=null);
            var root=new GameObject("Arrival handoff fixture");root.SetActive(false);
            try
            {
                var arrival=root.AddComponent(T("CurtainCall.EscapeRoute.EscapeSceneArrival"));
                var camera=new GameObject("Shared camera");camera.transform.SetParent(root.transform,false);
                var director=camera.AddComponent(T("CurtainCall.Cameras.CameraViewDirector"));
                var view=AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Resources/Settings/Camera/FreeThirdPerson.asset");
                director.GetType().GetMethod("RequestView").Invoke(director,new object[]{arrival,view,75,Vector3.zero});
                arrival.GetType().GetField("cameraRequest",flags).SetValue(arrival,director);
                var actor=new GameObject("Local pilot");actor.transform.SetParent(root.transform,false);
                var player=actor.AddComponent(T("CurtainCall.Network.PlayerSync.NetworkPlayer"));
                var pilot=actor.AddComponent(T("CurtainCall.HumanCannon.HumanCannonPilot"));
                pilot.GetType().GetProperty("IsFlying").SetValue(pilot,true);
                var observe=arrival.GetType().GetMethod("ObserveApproachExit",flags);
                observe.Invoke(arrival,new object[]{player});
                Assert.That(arrival.GetType().GetField("reachedStairEnd",flags).GetValue(arrival),Is.True);
                Assert.That(arrival.GetType().GetField("cameraRequest",flags).GetValue(arrival),Is.Null);
                var requests=director.GetType().GetField("requests",flags).GetValue(director);
                Assert.That(requests.GetType().GetProperty("Count").GetValue(requests),Is.EqualTo(0));
                pilot.GetType().GetProperty("IsFlying").SetValue(pilot,false);
                observe.Invoke(arrival,new object[]{player});
                Assert.That(arrival.GetType().GetField("reachedStairEnd",flags).GetValue(arrival),Is.True,"Landing or a vanished rope cannot re-enable arrival framing.");
            }
            finally{Object.DestroyImmediate(root);}
        }
    }

    public sealed class EscapeSharedSystemsTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);

        [Test]
        public void ReboundHoldAndJumpGuidesReplaceEachOriginalTokenOnce()
        {
            var format = T("CurtainCall.UI.SceneIntegration.CommonInteractionUI").GetMethod("FormatControlLabels");
            Assert.That(format.Invoke(null, new object[] { "F / F 홀드 · SPACE · Space로 도약 · W/S · A/D · FPS", "Space", "F", "F/G", "H/J" }),
                Is.EqualTo("Space / Space 홀드 · F · F로 도약 · F/G · H/J · FPS"));
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void ReleasedRosterCannotRegressToPendingOrChangeDuringTheSameDeparture(int count)
        {
            var root = new GameObject("Offline transfer protocol"); root.SetActive(false);
            try
            {
                var transfer = root.AddComponent(T("CurtainCall.EscapeRoute.EscapeSceneTransfer"));
                var snapshotType = transfer.GetType().GetNestedType("Snapshot", BindingFlags.NonPublic);
                object Snapshot(int phase)
                {
                    var value = Activator.CreateInstance(snapshotType, true);
                    snapshotType.GetField("generation").SetValue(value, 2);
                    snapshotType.GetField("phase").SetValue(value, phase);
                    snapshotType.GetField("participants").SetValue(value, Enumerable.Range(0, count).Select(i => (ulong)i).ToArray());
                    snapshotType.GetField("positions").SetValue(value, new Vector3[count]);
                    snapshotType.GetField("rotations").SetValue(value, Enumerable.Repeat(Quaternion.identity, count).ToArray());
                    return value;
                }
                Assert.That(Call(transfer, "ApplySnapshot", Snapshot(1)), Is.True);
                Assert.That(Call(transfer, "ApplySnapshot", Snapshot(2)), Is.True);
                Assert.That(Call(transfer, "ApplySnapshot", Snapshot(1)), Is.False);
                var changed = Snapshot(2);
                snapshotType.GetField("participants").SetValue(changed, Enumerable.Range(10, count).Select(i => (ulong)i).ToArray());
                Assert.That(Call(transfer, "ApplySnapshot", changed), Is.False);
                Assert.That(transfer.GetType().GetProperty("Ready").GetValue(transfer), Is.True);
                Assert.That(Call(transfer, "ApplySnapshot", Snapshot(2)), Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void RepeatedEquipmentSnapshotKeepsItsOriginalDeadlineAndRemainingUses()
        {
            var root = new GameObject("Offline carry snapshot"); root.SetActive(false);
            try
            {
                var carry = root.AddComponent(T("CurtainCall.WaitingRoom.PropCarry"));
                double now = (double)carry.GetType().GetProperty("EquipmentClock", Flags).GetValue(carry);
                // Keep a fractional deadline that round-trips exactly through Unity's JSON parser.
                double until = System.Math.Ceiling(now) + 15.375;
                string json = "{\"players\":[{\"client\":7,\"selectedUid\":19,\"held\":[{\"uid\":19,\"id\":\"rescue-gloves\",\"name\":\"gloves\",\"uses\":2}],\"cooldowns\":[{\"id\":\"rescue-gloves\",\"until\":"
                    + until.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "}]}]}";
                Call(carry, "HandleState", json);
                double Deadline() => (double)((IDictionary)carry.GetType().GetField("cooldownUntil", Flags).GetValue(carry))[(7UL, "rescue-gloves")];
                Assert.That(Deadline(), Is.EqualTo(until));
                Call(carry, "HandleState", json);
                Assert.That(Deadline(), Is.EqualTo(until), "Receiving the same snapshot cannot restart its cooldown.");
                object selected = Call(carry, "Selected", 7UL);
                Assert.That(selected.GetType().GetProperty("Uid").GetValue(selected), Is.EqualTo(19));
                Assert.That(selected.GetType().GetProperty("Uses").GetValue(selected), Is.EqualTo(2));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void LostEquipmentReportRetriesUntilItsOwnAckAndHostProcessesItOnce()
        {
            var root = new GameObject("Offline equipment delivery"); root.SetActive(false);
            try
            {
                var carry = root.AddComponent(T("CurtainCall.WaitingRoom.PropCarry"));
                Call(carry, "HandleState", "{\"players\":[{\"client\":0,\"held\":[{\"uid\":21,\"id\":\"balloon-pants\",\"name\":\"pants\",\"uses\":4}]}]}");
                Assert.That(Call(carry, "EquipmentReady", 0UL, "balloon-pants"), Is.True);
                Call(carry, "ReportEquipmentUsed", "balloon-pants");
                Assert.That(Call(carry, "EquipmentReportPending", "balloon-pants"), Is.True);
                Assert.That(Call(carry, "EquipmentReady", 0UL, "balloon-pants"), Is.False, "An unacknowledged effect must not activate again while its report is being retried.");
                var first = Call(carry, "NextEquipmentReport", 10d);
                Assert.NotNull(first);
                uint sequence = (uint)first.GetType().GetField("sequence").GetValue(first);
                int generation = (int)first.GetType().GetField("generation").GetValue(first);
                Assert.IsNull(Call(carry, "NextEquipmentReport", 10.1d));
                Assert.That(Call(carry, "NextEquipmentReport", 10.3d), Is.SameAs(first));
                Assert.That(Call(carry, "AcceptEquipmentReport", 7UL, sequence, generation), Is.True);
                Assert.That(Call(carry, "AcceptEquipmentReport", 7UL, sequence, generation), Is.False);
                Assert.That(Call(carry, "AcceptEquipmentReport", 8UL, sequence, generation), Is.True);
                Call(carry, "AcknowledgeEquipmentReport", sequence + 1, generation);
                Call(carry, "AcknowledgeEquipmentReport", sequence, generation + 1);
                Assert.That(Call(carry, "NextEquipmentReport", 11d), Is.SameAs(first));
                Call(carry, "AcknowledgeEquipmentReport", sequence, generation);
                Assert.IsNull(Call(carry, "NextEquipmentReport", 12d));
                Assert.That(Call(carry, "EquipmentReportPending", "balloon-pants"), Is.False);
                Assert.That(Call(carry, "AcceptEquipmentReport", 7UL, sequence, generation), Is.False, "An old retry cannot consume after a cooldown has elapsed.");
                Assert.That(Call(carry, "AcceptEquipmentReport", 7UL, sequence + 1, generation + 1), Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
