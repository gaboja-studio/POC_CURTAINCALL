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
        public void SharedClockHidesOneRopeOnlyInsideHiddenWindowAndRestoresIt()
        {
            foreach (double time in new[] { 0d, 3.999d, 7d, 10.999d, -1d, double.NaN })
                Assert.That(Call(coop, "HiddenRopeAt", time), Is.EqualTo(-1));
            int hidden = (int)Call(coop, "HiddenRopeAt", 4d);
            Assert.That(hidden, Is.InRange(0, 3));
            Assert.That(Call(coop, "HiddenRopeAt", 6.999d), Is.EqualTo(hidden));
            Call(coop, "ApplyRopeVisibility", hidden, null);
            Assert.That(supports.Count(s => !s.enabled), Is.EqualTo(1));
            Call(coop, "ApplyRopeVisibility", -1, null);
            Assert.That(supports.All(s => s.enabled), Is.True);
        }

        [Test]
        public void DeterministicShuffleAvoidsAdjacentRepeatsAcrossBlocksAndLateJoin()
        {
            var selection = T(CoopType).GetMethod("SelectRope", BindingFlags.NonPublic | BindingFlags.Static);
            var orders = new HashSet<string>();
            int previous = -1;
            for (int block = 0; block < 100; block++)
            {
                var order = new List<int>();
                for (int step = 0; step < 4; step++)
                {
                    long cycle = block * 4 + step;
                    int value = (int)selection.Invoke(null, new object[] { cycle, 4 });
                    Assert.That(value, Is.Not.EqualTo(previous)); previous = value; order.Add(value);
                    Assert.That(Call(coop, "HiddenRopeAt", cycle * 7d + 4.5d), Is.EqualTo(value));
                }
                Assert.That(order.Distinct().Count(), Is.EqualTo(4)); orders.Add(string.Join(",", order));
            }
            Assert.That(orders.Count, Is.GreaterThan(2));
            int late = (int)Call(coop, "HiddenRopeAt", 70004.5d);
            Set(coop, "selectedCycle", -1L); Set(coop, "selectedRope", -1);
            Assert.That(Call(coop, "HiddenRopeAt", 70004.5d), Is.EqualTo(late));
            Assert.That(Call(coop, "HiddenRopeAt", 4.5d), Is.EqualTo(selection.Invoke(null, new object[] { 0L, 4 })));
        }

        [Test]
        public void FlatSiblingSegmentsSocketsAndChildMeshesFollowTheirOwnSupport()
        {
            visuals[8].enabled = false;
            Set(coop, "initialVisuals", visuals.Select(v => v.enabled).ToArray());
            for (int hidden = 0; hidden < supports.Length; hidden++)
            {
                Call(coop, "ApplyRopeVisibility", hidden, null);
                for (int i = 0; i < visuals.Length; i++)
                    Assert.That(visuals[i].enabled, Is.EqualTo(i / 3 != hidden && i != 8), "visual " + i);
            }
            Call(coop, "ApplyRopeVisibility", -1, null);
            Assert.That(visuals[8].enabled, Is.False);
        }

        [Test]
        public void DisabledAuthoredSupportsStayDisabledAndAreNeverSelected()
        {
            Call(coop, "OnDisable"); supports[1].enabled = false; Call(coop, "OnEnable");
            for (int cycle = 0; cycle < 30; cycle++)
                Assert.That(Call(coop, "HiddenRopeAt", cycle * 7d + 4.5d), Is.Not.EqualTo(1));
            Call(coop, "ApplyRopeVisibility", 2, null); Call(coop, "ApplyRopeVisibility", -1, null);
            Assert.That(supports[1].enabled, Is.False);
        }

        [Test]
        public void RemovingOtherRopeKeepsSupportButRemovingActualRopeReleasesIt()
        {
            var mover = SurfaceFixture();
            Assert.That(Call(coop, "ApplyRopeVisibility", 1, mover), Is.False);
            Assert.That(Call(coop, "ApplyRopeVisibility", 0, mover), Is.True);
            Assert.That(supports[1].enabled, Is.True, "A skipped restore frame still restores the old hidden rope.");
        }

        [Test]
        public void OverlappingOrdinaryPlatformDoesNotTriggerAuthoredDrop()
        {
            var mover = SurfaceFixture();
            var platform = Root("Supporting ordinary floor").AddComponent<BoxCollider>();
            platform.center = origin + new Vector3(0, -.1f, 6); platform.size = new Vector3(2, .2f, 4);
            Physics.SyncTransforms();
            Assert.That(Call(coop, "ApplyRopeVisibility", 0, mover), Is.False);
        }

        [Test]
        public void FirstWorkerKeepsReservationAndSimultaneousEntrantsUseLowestId()
        {
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
            var player = Player(2); Set(player, "IsOwner", true); Call(coop, "SetWorker", 1UL);
            Assert.That(Call(coop, "CanReturnOverflow", player), Is.False);
            Set(coop, "synchronized", true); Assert.That(Call(coop, "CanReturnOverflow", player), Is.True);
            Set(player, "IsOwner", false); Assert.That(Call(coop, "CanReturnOverflow", player), Is.False);
            Set(player, "IsOwner", true); Call(coop, "SetWorker", 2UL);
            Assert.That(Call(coop, "CanReturnOverflow", player), Is.False);
            Call(coop, "SetWorker", 1UL);
            var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover"));
            Call(mover, "ResetExternalMotion", true);
            int teleports = 0;
            mover.GetType().GetEvent("Teleported").AddEventHandler(mover, (Action)(() => teleports++));
            Call(coop, "ReturnLocalOverflow", player);
            Assert.That(player.transform.position, Is.EqualTo(overflow.position));
            Assert.That(Get(mover, "CurrentJump").ToString(), Is.EqualTo("None"));
            Assert.That(Get(player, "State").ToString(), Is.EqualTo("Normal"));
            Assert.That(teleports, Is.EqualTo(1));
        }

        [Test]
        public void LateJoinStateRejectsOlderRevisionAndRequiresRestartBarrierToken()
        {
            Assert.That(Call(coop, "ApplyState", 5U, false, 7UL, 0, 0U), Is.True);
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
            Call(coop, "SetHiddenRope", 2); Call(coop, "SetWorker", 3UL);
            Set(coop, "pendingOverflow", 12U); Set(coop, "firstOverflowRequest", 10U);
            Call(coop, "Detach");
            Assert.That(supports.All(s => s.enabled), Is.True); Assert.That(visuals.All(v => v.enabled), Is.True);
            Assert.That(Get(coop, "WorkerOwner"), Is.EqualTo(ulong.MaxValue));
            Assert.That(Get(coop, "pendingOverflow"), Is.EqualTo(0U)); Assert.That(lowerGate.activeSelf, Is.False);
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

        Component Player(ulong id)
        {
            var actor = Root("Worker " + id, false);
            actor.transform.position = origin + new Vector3(3, -3, 14);
            var player = actor.AddComponent(T(PlayerType));
            var mover = actor.GetComponent(T("CurtainCall.Player.PlayerMover")); Call(mover, "Awake");
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
