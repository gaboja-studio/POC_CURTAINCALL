using System.Collections.Generic;
using System.Reflection;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.Tightrope.EditorTools.Tests
{
    public sealed class TightropeSurfaceControlTests
    {
        readonly List<Object> objects = new();
        readonly Vector3 origin = new(9000f, 9000f, 9000f);
        TightropeCourse previousCourse, course;
        CourseControlSwitcher switcher;
        NetworkPlayer player;
        PlayerMover mover;
        PlayerBalance balance;
        PlayerController controller;
        PlayerControlScheme previousDefault;

        [SetUp]
        public void SetUp()
        {
            previousCourse = (TightropeCourse)typeof(TightropeCourse)
                .GetField("current", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            previousDefault = ControlSettings.Current.DefaultScheme;
            ControlSettings.Current.DefaultScheme = Track(ScriptableObject.CreateInstance<PlatformControlScheme>());
            var courseObject = MakeObject("Surface test course");
            courseObject.transform.position = origin;
            switcher = courseObject.AddComponent<CourseControlSwitcher>();
            course = courseObject.GetComponent<TightropeCourse>();
            Invoke(switcher, "Awake");
            var playerObject = MakeObject("Surface test player");
            playerObject.transform.position = origin + new Vector3(0f, 0f, -1f);
            mover = playerObject.AddComponent<PlayerMover>();
            balance = playerObject.AddComponent<PlayerBalance>();
            controller = playerObject.AddComponent<PlayerController>();
            player = playerObject.AddComponent<NetworkPlayer>();
            Invoke(mover, "Awake");
            Invoke(balance, "Awake");
            Invoke(controller, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            if (switcher != null) { Invoke(switcher, "OnDisable"); Invoke(switcher, "OnDestroy"); }
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] is GameObject gameObject) Object.DestroyImmediate(gameObject);
            ControlSettings.Current.DefaultScheme = previousDefault;
            typeof(TightropeCourse).GetField("current", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, previousCourse);
            foreach (var value in objects) if (value != null) Object.DestroyImmediate(value);
            objects.Clear();
            Physics.SyncTransforms();
        }

        [Test]
        public void LocalSpawn_DisablesSharedPrefabDefaultBalanceBeforeFirstGroundedFrame()
        {
            Assert.That(balance.IsActive, Is.True, "공용 프리팹의 activeOnStart 기본값 재현");
            Invoke(switcher, "Bind", player);
            Assert.That(balance.IsActive, Is.False);
            Assert.That(controller.HasAssignedScheme, Is.False);
        }

        [Test]
        public void EnterActualRope_SnapsToCenterAndEnablesRopeControlsAndBalance()
        {
            int lane = Mathf.Min(1, course.LaneCount - 1);
            float side = course.Layout.GetLaneSide(lane);
            AddRope(lane, side);
            MovePlayer(new Vector3(side + 0.12f, 0f, 4f));
            Invoke(switcher, "Bind", player);
            Invoke(switcher, "UpdateSurfaceMode");
            Assert.That(switcher.IsLocalOnRope, Is.True);
            Assert.That(controller.Scheme, Is.InstanceOf<TightropeControlScheme>());
            Assert.That(balance.IsActive, Is.True);
            Assert.That(mover.FreeMovement, Is.False);
            Assert.That(course.GetSide(player.transform.position), Is.EqualTo(side).Within(0.01f));
        }

        [Test]
        public void LeaveRopeForPlatform_DisablesBalanceAndRestoresSharedMovement()
        {
            AddBox("Platform", new Vector3(0f, -0.25f, -3f), new Vector3(20f, 0.5f, 6f));
            Invoke(switcher, "Bind", player);
            Invoke(switcher, "Apply", true);
            Invoke(switcher, "UpdateSurfaceMode");
            Assert.That(switcher.IsLocalOnRope, Is.False);
            Assert.That(balance.IsActive, Is.False);
            Assert.That(controller.HasAssignedScheme, Is.False);
            Assert.That(mover.FreeMovement, Is.True);
            balance.SetBalanceActive(true); // 외부 재초기화가 기본 활성값을 다시 넣어도 안전해야 한다.
            Invoke(switcher, "UpdateSurfaceMode");
            Assert.That(balance.IsActive, Is.False);
        }

        [Test]
        public void OrdinaryFloorInsideRopeCoordinates_IsNotARopeSurface()
        {
            int lane = Mathf.Min(1, course.LaneCount - 1);
            float side = course.Layout.GetLaneSide(lane);
            AddRope(lane, side);
            AddBox("Raised floor", new Vector3(side, 0f, 4f), new Vector3(2f, 0.2f, 2f));
            MovePlayer(new Vector3(side, 0.1f, 4f));
            Assert.That(course.IsOnRope(player.transform.position, mover.BodyRadius, out _), Is.True,
                "이전 높이·좌표 검사로는 다른 바닥을 줄로 오인한다");
            Assert.That(course.TryGetSupportingRope(mover, out _), Is.False);
        }

        [Test]
        public void FloorAtExactlyRopeHeight_TakesPrecedenceOverOverlappingRope()
        {
            int lane = Mathf.Min(1, course.LaneCount - 1);
            float side = course.Layout.GetLaneSide(lane);
            AddRope(lane, side);
            AddBox("Coincident floor", new Vector3(side, -0.1f, 4f), new Vector3(2f, 0.2f, 2f));
            MovePlayer(new Vector3(side, 0f, 4f));
            Assert.That(course.TryGetSupportingRope(mover, out _), Is.False);
        }

        [Test]
        public void AirborneOrCarriedPlayer_PreservesExistingRopeBalance()
        {
            Invoke(switcher, "Bind", player);
            Invoke(switcher, "Apply", true);
            mover.Simulated = false;
            MovePlayer(new Vector3(0f, 1.5f, 4f));
            Invoke(switcher, "UpdateSurfaceMode");
            Assert.That(switcher.IsLocalOnRope, Is.True);
            Assert.That(balance.IsActive, Is.True);
        }

        [Test]
        public void AnotherContentAssignedControls_AreNotReplacedOrBalancedOff()
        {
            var external = Track(ScriptableObject.CreateInstance<PlatformControlScheme>());
            controller.SetScheme(external);
            Invoke(switcher, "Bind", player);
            Invoke(switcher, "UpdateSurfaceMode");
            Assert.That(controller.Scheme, Is.SameAs(external));
            Assert.That(balance.IsActive, Is.True);
        }

        [TestCase(CampaignPhase.None, false, false, true)]
        [TestCase(CampaignPhase.Intro, false, false, true)]
        [TestCase(CampaignPhase.Performance, true, true, true)]
        [TestCase(CampaignPhase.Performance, true, false, false)]
        [TestCase(CampaignPhase.Performance, false, true, false)]
        [TestCase(CampaignPhase.RestTime, true, true, false)]
        [TestCase(CampaignPhase.Night, true, true, false)]
        [TestCase(CampaignPhase.Escape, true, true, false)]
        public void TraversalScope_SeparatesPracticeFromOtherCampaignPhases(
            CampaignPhase phase, bool tightrope, bool open, bool expected) =>
            Assert.That(TightropeCourse.AllowsTraversal(phase, tightrope, open), Is.EqualTo(expected));

        void AddRope(int lane, float side)
        {
            var rope = AddBox("Rope", new Vector3(side, -0.05f, 4f), new Vector3(0.4f, 0.1f, 8f));
            var surfaces = (Dictionary<Collider, int>)typeof(TightropeCourse)
                .GetField("ropeSurfaces", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(course);
            surfaces.Add(rope, lane);
        }

        BoxCollider AddBox(string name, Vector3 center, Vector3 size)
        {
            var go = MakeObject(name);
            go.transform.position = origin + center;
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            Physics.SyncTransforms();
            return box;
        }

        void MovePlayer(Vector3 position)
        {
            mover.Body.enabled = false;
            player.transform.position = origin + position;
            mover.Body.enabled = true;
            Physics.SyncTransforms();
        }

        GameObject MakeObject(string name) => Track(new GameObject(name));
        T Track<T>(T value) where T : Object { value.hideFlags = HideFlags.DontSave; objects.Add(value); return value; }
        static object Invoke(object target, string method, params object[] args) => target.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    }
}
