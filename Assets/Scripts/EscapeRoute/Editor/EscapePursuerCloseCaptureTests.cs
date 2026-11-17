using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.Survival;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Editor
{
    [Category("CommonProgressionRegression"), Category("EscapePursuerComplete")]
    public sealed class EscapePursuerCloseCaptureTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const string Prefab = "Assets/Resources/Prefabs/Characters/Enemies/UnderstagePursuer/EscapeAPursuer.prefab";
        readonly List<GameObject> roots = new();
        EscapeUnderstageChase chase;
        NetworkPlayer player;
        CharacterController capsule;
        SkinnedMeshRenderer torso;
        List<NetworkPlayer> roster;
        NetworkPlayer[] previousRoster;
        SurvivalPlace previousPlace;
        Func<NetworkPlayer, PlayerState, bool> previousGuard;
        static object Call(object value, string name, params object[] args) => value.GetType().GetMethod(name, Flags).Invoke(value, args);
        static object Get(object value, string name) => value.GetType().GetField(name, Flags).GetValue(value);
        static void Set(object value, string name, object data) => value.GetType().GetField(name, Flags).SetValue(value, data);
        static void Property(object value, string name, object data)
        {
            for (var type = value.GetType(); type != null; type = type.BaseType)
            {
                var property = type.GetProperty(name, Flags | BindingFlags.DeclaredOnly);
                if (property?.GetSetMethod(true) == null) continue;
                property.SetValue(value, data); return;
            }
            throw new MissingMemberException(name);
        }
        void State(PlayerState state) => Property(Get(player, "state"), "Value", state);

        [SetUp] public void Setup()
        {
            var holder = new GameObject("Close capture fixture"); holder.SetActive(false); roots.Add(holder);
            holder.transform.position = new Vector3(61000, 1000, 61000);
            var clone = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab), holder.transform);
            chase = clone.GetComponentInChildren<EscapeUnderstageChase>(true);
            chase.transform.SetPositionAndRotation(holder.transform.position, Quaternion.identity);
            Set(chase, "body", chase.GetComponent<CapsuleCollider>());
            chase.GetComponent<CapsuleCollider>().enabled = true; // Runtime enables it after the entrance.
            foreach (var field in new[] { "fullRoute", "bypass", "boss", "refuge" }) Set(chase, field, null);
            torso = chase.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s => s.name == "Body");
            var actor = new GameObject("Guest near model"); actor.SetActive(false); roots.Add(actor);
            player = actor.AddComponent<NetworkPlayer>();
            capsule = actor.GetComponent<CharacterController>(); capsule.height = 1.8f; capsule.radius = .25f;
            Set(actor.GetComponent<PlayerMover>(), "controller", capsule);
            Property(player, "IsSpawned", true); Property(player, "IsServer", true); Property(player, "OwnerClientId", 3UL);
            roster = (List<NetworkPlayer>)typeof(NetworkPlayer).GetField("all", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            previousRoster = roster.ToArray(); roster.Clear(); roster.Add(player);
            previousPlace = SurvivalRules.ServerPlace; SurvivalRules.ServerPlace = SurvivalPlace.EscapeRoute;
            previousGuard = NetworkPlayer.ServerCanChangeState; NetworkPlayer.ServerCanChangeState = null;
        }

        [TearDown] public void Cleanup()
        {
            NetworkPlayer.ServerCanChangeState = previousGuard; SurvivalRules.ServerPlace = previousPlace;
            if (roster != null) { roster.Clear(); roster.AddRange(previousRoster); }
            if (player) { Property(player, "IsSpawned", false); Property(player, "IsServer", false); }
            foreach (var root in roots) if (root) Object.DestroyImmediate(root);
            roots.Clear();
        }

        void AtEdge(float angle, float gap)
        {
            var direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            var bounds = torso.localBounds;
            // The real prefab torso is wider than its physical capsule. Use its
            // widest horizontal side as the expected circular body radius.
            float radius = Mathf.Max(bounds.extents.x, bounds.extents.z) * torso.transform.lossyScale.x;
            float offset = Vector3.ProjectOnPlane(torso.transform.TransformPoint(bounds.center) - chase.transform.position, Vector3.up).magnitude;
            var point = chase.transform.position + direction * (radius + offset + gap);
            point.y = chase.transform.position.y;
            player.transform.position = point;
        }
        bool Candidate() => (bool)Call(chase, "CloseCaptureCandidate", player, Vector3.zero);
        bool Capture() => (bool)Call(chase, "TryCloseCapture", 100d, Vector3.zero);
        void Phase(string phase)
        {
            var cue = Get(chase, "attack");
            var field = cue.GetType().GetField("Phase"); field.SetValue(cue, Enum.Parse(field.FieldType, phase));
            Set(chase, "attack", cue);
        }
        BoxCollider Obstacle(Vector3 center, Vector3 size)
        {
            var root = new GameObject("Actual barrier"); roots.Add(root); root.transform.position = center;
            var box = root.AddComponent<BoxCollider>(); box.size = size; Physics.SyncTransforms(); return box;
        }

        [TestCase(0f)][TestCase(45f)][TestCase(90f)][TestCase(135f)]
        [TestCase(180f)][TestCase(225f)][TestCase(270f)][TestCase(315f)]
        public void AuthoredTorsoCapturesFromEverySideDuringMissRecovery(float angle)
        {
            AtEdge(angle, GameSettings.EscapeRoute.Pursuer.closeCaptureDistance + capsule.radius - .04f);
            Phase("Retract"); Set(chase, "nextAim", 10000d);
            Assert.That(Candidate(), Is.True);
            Assert.That(Capture(), Is.True);
            Assert.That(player.State, Is.EqualTo(PlayerState.EscapeCaptured));
            Assert.That(Get(Get(chase, "attack"), "Phase").ToString(), Is.EqualTo("Grab"));
            Assert.That(Capture(), Is.False, "Only one capture at a time.");
        }

        [TestCase(0f)][TestCase(45f)][TestCase(90f)][TestCase(135f)]
        [TestCase(180f)][TestCase(225f)][TestCase(270f)][TestCase(315f)]
        public void OutsideModelMarginRemainsDodgeable(float angle)
        {
            AtEdge(angle, GameSettings.EscapeRoute.Pursuer.closeCaptureDistance + capsule.radius + .06f);
            Assert.That(Candidate(), Is.False); Assert.That(Capture(), Is.False);
        }

        [TestCase(.35f, 0f)][TestCase(.9f, 0f)][TestCase(1.8f, 1.2f)][TestCase(1.8f, 2f)]
        public void ShortInjuredCrouchedJumpingAndCarriedBodiesStillHaveContact(float height, float lift)
        {
            capsule.height = height; capsule.radius = Mathf.Min(.15f, height * .4f);
            AtEdge(180, .25f); player.transform.position += Vector3.up * lift;
            Assert.That(Candidate(), Is.True); Assert.That(Capture(), Is.True);
        }

        [TestCase("wall")][TestCase("floor")][TestCase("origin")]
        public void RealBarriersStillPreventNearCapture(string barrier)
        {
            AtEdge(0, .25f);
            var origin = chase.transform.position;
            if (barrier == "wall")
                Obstacle(Vector3.Lerp(origin, player.transform.position, .55f) + Vector3.up, new Vector3(8, 4, .1f));
            else if (barrier == "floor")
            {
                player.transform.position = origin + Vector3.up * 3f;
                Obstacle(origin + Vector3.up * 2.9f, new Vector3(8, .1f, 8));
            }
            else Obstacle(origin + Vector3.up * .9f, Vector3.one * .2f);
            Assert.That(Candidate(), Is.False); Assert.That(Capture(), Is.False);
            Assert.That(player.State, Is.EqualTo(PlayerState.Normal));
        }

        [TestCase("dead")][TestCase("arrived")][TestCase("waiting")][TestCase("refuge")]
        [TestCase("jumping")][TestCase("client")][TestCase("damageGuard")]
        [TestCase("hiddenBody")]
        public void NearCapturePreservesStateProtectionAndHostAuthority(string reason)
        {
            AtEdge(180, .25f);
            if (reason == "dead") State(PlayerState.Retired);
            if (reason == "arrived") State(PlayerState.Arrived);
            if (reason == "waiting") ((HashSet<ulong>)Get(chase, "conveyorWaiting")).Add(player.OwnerClientId);
            if (reason == "refuge") Set(chase, "refuge", Obstacle(player.transform.position, Vector3.one));
            if (reason == "jumping") Set(chase, "jumping", true);
            if (reason == "client") Property(player, "IsServer", false);
            if (reason == "damageGuard") NetworkPlayer.ServerCanChangeState = (_, _) => false;
            if (reason == "hiddenBody") chase.GetComponent<CapsuleCollider>().enabled = false;
            Assert.That(Capture(), Is.False);
        }

        [TestCase("None")][TestCase("Warning")][TestCase("Strike")][TestCase("Retract")]
        public void ActualAttackUpdateCapturesBehindModelBeforeFacingAndCooldownChecks(string phase)
        {
            AtEdge(180, .25f); Phase(phase); Set(chase, "nextAim", double.MaxValue);
            var timer = roots[0].AddComponent<EscapeRouteTimer>();
            var current = typeof(EscapeRouteTimer).GetProperty("Current");
            var previous = current.GetValue(null);
            try
            {
                current.SetValue(null, timer); Set(chase, "attackRound", timer.StartedAt);
                Assert.That(Call(chase, "UpdateAttack", false, false, Vector3.zero, Vector3.zero, null), Is.True);
                Assert.That(player.State, Is.EqualTo(PlayerState.EscapeCaptured));
                Assert.That(Get(Get(chase, "attack"), "Actor"), Is.EqualTo(3UL));
            }
            finally { current.SetValue(null, previous); }
        }

        [Test] public void ModelScaleAndRotationDriveTheEnvelopeAndOtherLimbsDoNotExpandIt()
        {
            chase.transform.rotation = Quaternion.Euler(0, 90, 0); chase.transform.localScale *= 1.5f;
            AtEdge(0, .25f); Assert.That(Candidate(), Is.True);
            var hand = chase.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(s => s.name == "Hand.R");
            hand.localBounds = new Bounds(Vector3.zero, Vector3.one * 100);
            torso.localBounds = new Bounds(Vector3.zero, Vector3.one * 100);
            player.transform.position += Vector3.right * 10;
            Assert.That(Candidate(), Is.False, "Extended hands and animation-updated bounds must not enlarge cached body contact.");
        }
    }
}
