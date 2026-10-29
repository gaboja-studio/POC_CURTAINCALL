using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
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
