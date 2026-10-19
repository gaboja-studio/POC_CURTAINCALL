using System.Collections.Generic;
using System.Reflection;
using CurtainCall.Damage;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.Tightrope.EditorTools.Tests
{
    /// <summary>외줄이 공용 조작·부상 정책을 빌렸다가 안전하게 반환하는 연결 회귀 검사.</summary>
    public sealed class CommonSystemBindingsTests
    {
        readonly List<Object> objects = new();
        PlayerControlScheme previousDefault;
        CourseControlSwitcher switcher;
        GameObject playerObject;
        NetworkPlayer player;
        PlayerController controller;
        PlayerMover mover;
        PlayerBalance balance;
        BodyDamageEffects effects;
        TightropeCourse course;
        TightropeCourse previousCourse;

        [SetUp]
        public void SetUp()
        {
            previousDefault = ControlSettings.Current.DefaultScheme;
            previousCourse = (TightropeCourse)typeof(TightropeCourse).GetField("current", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            ControlSettings.Current.DefaultScheme = Make<PlatformControlScheme>();
            playerObject = Track(new GameObject("Binding test player"));
            mover = playerObject.AddComponent<PlayerMover>();
            balance = playerObject.AddComponent<PlayerBalance>();
            playerObject.AddComponent<PlayerInteraction>();
            playerObject.AddComponent<PlayerCondition>();
            effects = playerObject.AddComponent<BodyDamageEffects>();
            controller = playerObject.AddComponent<PlayerController>();
            player = playerObject.AddComponent<NetworkPlayer>();
            Invoke(controller, "Awake");
            Invoke(effects, "Awake");
            var courseObject = Track(new GameObject("Binding test course"));
            switcher = courseObject.AddComponent<CourseControlSwitcher>();
            course = courseObject.GetComponent<TightropeCourse>();
            typeof(TightropeCourse).GetField("current", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, course);
            Invoke(switcher, "Awake");
            Invoke(switcher, "Bind", player);
        }

        [TearDown]
        public void TearDown()
        {
            // 규칙 에셋보다 컴포넌트를 먼저 정리한다.
            if (switcher != null)
            {
                Invoke(switcher, "OnDisable");
                Invoke(switcher, "OnDestroy");
            }
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] is GameObject gameObject) Object.DestroyImmediate(gameObject);
            ControlSettings.Current.DefaultScheme = previousDefault;
            typeof(TightropeCourse).GetField("current", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, previousCourse);
            foreach (var value in objects)
                if (value != null) Object.DestroyImmediate(value);
            objects.Clear();
        }

        [Test]
        public void Platform_UsesCommonDefaultAndFollowsItsChanges()
        {
            Invoke(switcher, "Apply", false);
            Assert.That(controller.HasAssignedScheme, Is.False);
            Assert.That(controller.Scheme, Is.SameAs(ControlSettings.Default));
            Assert.That(mover.FreeMovement, Is.True);
            var next = Make<PlatformControlScheme>();
            ControlSettings.Current.DefaultScheme = next;
            Invoke(controller, "Update");
            Assert.That(controller.Scheme, Is.SameAs(next));
        }

        [Test]
        public void Platform_ExplicitContentOverrideRemainsSupported()
        {
            var custom = Make<PlatformControlScheme>();
            SetField(switcher, "platformScheme", custom);
            Invoke(switcher, "Apply", false);
            Assert.That(controller.HasAssignedScheme, Is.True);
            Assert.That(controller.Scheme, Is.SameAs(custom));
        }

        [Test]
        public void LeaveCourse_ReturnsOwnedRopeControlsToCommonDefault()
        {
            Invoke(switcher, "Apply", true);
            Assert.That(controller.Scheme, Is.InstanceOf<TightropeControlScheme>());
            Assert.That(balance.IsActive, Is.True);
            Invoke(switcher, "Unbind");
            Assert.That(controller.HasAssignedScheme, Is.False);
            Assert.That(controller.Scheme, Is.SameAs(ControlSettings.Default));
            Assert.That(balance.IsActive, Is.False);
            Assert.That(mover.FreeMovement, Is.True);
        }

        [Test]
        public void LeaveCourse_PreservesControlsAssignedLaterByAnotherFeature()
        {
            Invoke(switcher, "Apply", true);
            var external = Make<PlatformControlScheme>();
            controller.SetScheme(external);
            mover.FreeMovement = false;
            balance.SetBalanceActive(true);
            Invoke(switcher, "Unbind");
            Assert.That(controller.Scheme, Is.SameAs(external));
            Assert.That(controller.HasAssignedScheme, Is.True);
            Assert.That(mover.FreeMovement, Is.False);
            Assert.That(balance.IsActive, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LeaveCourse_RestoresPreviousInjuryPolicy(bool hadPolicy)
        {
            var previous = hadPolicy ? Make<TightropeBodyPenalty>() : null;
            effects.SetPolicy(previous);
            Invoke(switcher, "BindInjuryPolicy", effects);
            Assert.That(effects.Policy, Is.InstanceOf<TightropeBodyPenalty>());
            Assert.That(effects.Policy, Is.Not.SameAs(previous));
            Invoke(switcher, "ReleaseInjuryPolicies");
            Assert.That(effects.Policy, Is.SameAs(previous));
        }

        [Test]
        public void LeaveCourse_PreservesInjuryPolicyAssignedLaterByAnotherFeature()
        {
            Invoke(switcher, "BindInjuryPolicy", effects);
            var external = Make<TightropeBodyPenalty>();
            effects.SetPolicy(external);
            Invoke(switcher, "BindInjuryPolicy", effects);
            Assert.That(effects.Policy, Is.SameAs(external), "매 프레임 외부 정책을 다시 덮어쓰지 않는다");
            Invoke(switcher, "ReleaseInjuryPolicies");
            Assert.That(effects.Policy, Is.SameAs(external));
        }

        [Test]
        public void LeaveCourse_RemovesOnlyContentPenaltyAndKeepsCommonCrawling()
        {
            effects.SetPolicy(null);
            playerObject.GetComponent<PlayerCondition>().SetLostParts(PlayerCondition.OneArmParts);
            Invoke(switcher, "BindInjuryPolicy", effects);
            Invoke(effects, "Update");
            Invoke(switcher, "ReleaseInjuryPolicies");
            Invoke(effects, "Update");
            Assert.That(effects.IsCrawling, Is.True);
            Assert.That(mover.SpeedMultiplier, Is.EqualTo(GameSettings.Base.BodyDamage.CrawlSpeedMultiplier));
            Assert.That(playerObject.GetComponent<PlayerInteraction>().ReachMultiplier, Is.EqualTo(1f));
        }

        [TestCase(BodyPart.None, 0)]
        [TestCase(BodyPart.LeftArm, 1)]
        [TestCase(BodyPart.LeftArm | BodyPart.RightArm, 2)]
        public void PiggybackReach_UsesClimbersCommonInjuryMultiplier(BodyPart lost, int arms)
        {
            playerObject.GetComponent<PlayerCondition>().SetLostParts(lost);
            Invoke(switcher, "BindInjuryPolicy", effects);
            Invoke(effects, "Update");
            float reach = (float)typeof(PiggybackSystem).GetMethod("LinkReach", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { player });
            float expected = GameSettings.Tightrope.Piggyback.LinkDistance
                * Mathf.Max(0f, 1f - GameSettings.Tightrope.BodyPenalty.ReachLossPerArm * arms);
            Assert.That(reach, Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void DestroyCourse_ReleasesGeneratedSchemeAndPolicy()
        {
            Invoke(switcher, "Apply", true);
            var generatedScheme = controller.Scheme;
            Invoke(switcher, "BindInjuryPolicy", effects);
            var generatedPolicy = effects.Policy;
            Invoke(switcher, "OnDisable");
            Invoke(switcher, "OnDestroy");
            Object.DestroyImmediate(switcher.gameObject);
            Assert.That(generatedScheme == null, Is.True);
            Assert.That(generatedPolicy == null, Is.True);
            Assert.That(controller.HasAssignedScheme, Is.False);
            Assert.That(effects.Policy == null, Is.True);
        }

        [Test]
        public void LaneRules_IndependentCourseStillBindsAndRestoresEarlierCallbacks()
        {
            var rules = Track(new GameObject("Lane binding test")).AddComponent<RopeLaneJumpRules>();
            System.Func<int, bool> previousFilter = direction => false;
            System.Func<int, float> previousShift = direction => 2f;
            mover.LaneJumpFilter = previousFilter;
            mover.LaneLandingShift = previousShift;
            mover.BlockLaneJumpOntoPlayer = true;
            Assert.That(course.IsGameplayActive, Is.True, "브리지 없는 독립 코스 호환");
            Invoke(rules, "Bind", player);
            Assert.That(mover.LaneJumpFilter, Is.Not.SameAs(previousFilter));
            Assert.That(mover.BlockLaneJumpOntoPlayer, Is.False);
            course.enabled = false;
            typeof(TightropeCourse).GetField("current", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, course);
            Invoke(rules, "Update");
            Assert.That(mover.LaneJumpFilter, Is.SameAs(previousFilter));
            Assert.That(mover.LaneLandingShift, Is.SameAs(previousShift));
            Assert.That(mover.BlockLaneJumpOntoPlayer, Is.True);
            Assert.That(rules.CanLaneJump(1), Is.True, "비활성 코스는 외부 점프를 막지 않는다");
        }

        [Test]
        public void LaneRules_LeavingPreservesCallbacksAssignedByAnotherFeature()
        {
            var rules = Track(new GameObject("Lane ownership test")).AddComponent<RopeLaneJumpRules>();
            Invoke(rules, "Bind", player);
            System.Func<int, bool> externalFilter = direction => false;
            System.Func<int, float> externalShift = direction => 4f;
            mover.LaneJumpFilter = externalFilter;
            mover.LaneLandingShift = externalShift;
            mover.BlockLaneJumpOntoPlayer = false;
            course.enabled = false;
            typeof(TightropeCourse).GetField("current", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, course);
            Invoke(rules, "Update");
            Assert.That(mover.LaneJumpFilter, Is.SameAs(externalFilter));
            Assert.That(mover.LaneLandingShift, Is.SameAs(externalShift));
            Assert.That(mover.BlockLaneJumpOntoPlayer, Is.False);
        }

        [Test]
        public void Piggyback_LeavingClearsLinksAndReleasesInteractionSubscription()
        {
            var system = Track(new GameObject("Piggyback scope test")).AddComponent<PiggybackSystem>();
            Invoke(system, "Bind", player);
            Assert.That(GetField(system, "interaction"), Is.SameAs(playerObject.GetComponent<PlayerInteraction>()));
            var below = (Dictionary<ulong, ulong>)GetField(system, "belowOf");
            var above = (Dictionary<ulong, ulong>)GetField(system, "aboveOf");
            below[10] = 20;
            above[20] = 10;
            course.enabled = false;
            typeof(TightropeCourse).GetField("current", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, course);
            Invoke(system, "LeaveGameplay");
            Assert.That(below, Is.Empty);
            Assert.That(above, Is.Empty);
            Assert.That(GetField(system, "interaction"), Is.Null);
            Invoke(system, "Bind", player);
            Assert.That(GetField(system, "interaction"), Is.Null, "다른 시간대에서 재바인딩하지 않는다");
            Assert.That(Invoke(system, "ServerTryLink", 0UL), Is.EqualTo("거부: 외줄 공연이 진행 중이 아님"));
        }

        T Make<T>() where T : ScriptableObject => Track(ScriptableObject.CreateInstance<T>());

        T Track<T>(T value) where T : Object
        {
            value.hideFlags = HideFlags.DontSave;
            objects.Add(value);
            return value;
        }

        static object Invoke(object target, string method, params object[] args) => target.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

        static void SetField(object target, string field, object value) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        static object GetField(object target, string field) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }
}
