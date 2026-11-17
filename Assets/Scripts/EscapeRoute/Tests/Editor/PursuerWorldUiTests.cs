using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    [Category("EscapePursuerComplete")]
    public sealed class PursuerWorldUiTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static Type TypeOf(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(name)).First(t => t != null);
        static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
        static void Phase(object target, string name) => Set(target, "Phase", Enum.Parse(target.GetType().GetField("Phase").FieldType, name));
        static bool Visible(Component view) => view && (bool)view.GetType().GetProperty("Visible").GetValue(view);
        readonly List<Object> owned = new();
        readonly Vector3 origin = new(9000, 3000, 9000);
        GameObject root, fixtures;
        Component chase, boss, timer, breach;
        CapsuleCollider body;
        BoxCollider floor;
        ScriptableObject settings;
        FieldInfo currentSettings;
        PropertyInfo currentTimer, currentChase, currentBoss;
        object oldSettings, oldTimer, oldChase, oldBoss;
        IList players;
        object[] oldPlayers;
        readonly List<Component> testPlayers = new();

        T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        Transform Child(string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(root.transform, false);
            return child;
        }
        AnimationClip Clip(string name)
        {
            var clip = Own(new AnimationClip { name = name, legacy = true });
            clip.SetCurve("Pose", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0, 0, 1, 0));
            return clip;
        }
        Component Prefab(string name)
        {
            var go = new GameObject(name); go.transform.SetParent(fixtures.transform);
            return go.AddComponent(TypeOf("CurtainCall.WorldIndicators." + name));
        }

        [SetUp] public void Setup()
        {
            players = (IList)TypeOf("CurtainCall.Network.PlayerSync.NetworkPlayer").GetField("all", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            oldPlayers = players.Cast<object>().ToArray(); players.Clear();
            var gameSettings = TypeOf("CurtainCall.Settings.GameSettings");
            currentSettings = gameSettings.GetField("current", BindingFlags.Static | BindingFlags.NonPublic);
            oldSettings = currentSettings.GetValue(null);
            var isolated = Own(ScriptableObject.CreateInstance(gameSettings));
            settings = Own(ScriptableObject.CreateInstance(TypeOf("CurtainCall.Settings.WorldUiSettings")));
            Set(isolated, "worldUi", settings);
            Set(isolated, "escapeRoute", Own(ScriptableObject.CreateInstance(TypeOf("CurtainCall.Settings.EscapeRouteSettings"))));
            currentSettings.SetValue(null, isolated);
            Set(settings, "enabled", true); Set(settings, "maxDistance", 1000000f);
            Set(settings, "style", Own(ScriptableObject.CreateInstance(TypeOf("CurtainCall.WorldIndicators.WorldIndicatorStyle"))));
            fixtures = Own(new GameObject("World UI test fixtures")); fixtures.SetActive(false);
            Set(settings, "circlePrefab", Prefab("AreaIndicatorView"));
            Set(settings, "volumePrefab", Prefab("SweptVolumeIndicatorView"));
            Set(settings, "pathPrefab", Prefab("PathIndicatorView"));
            Set(settings, "targetPrefab", Prefab("TargetIndicatorView"));
            timer = fixtures.AddComponent(TypeOf("CurtainCall.EscapeRoute.EscapeRouteTimer"));
            currentTimer = timer.GetType().GetProperty("Current"); oldTimer = currentTimer.GetValue(null);
            currentTimer.SetValue(null, timer);
            Set(timer, "startedAt", 0d); Set(timer, "synchronized", true);

            root = Own(new GameObject("Isolated pursuer adapter")); root.SetActive(false); root.transform.position = origin;
            chase = root.AddComponent(TypeOf("CurtainCall.EscapeRoute.EscapeUnderstageChase"));
            currentChase = chase.GetType().GetProperty("Current"); oldChase = currentChase.GetValue(null);
            Child("Pose");
            var animation = root.AddComponent<Animation>(); Set(chase, "gait", animation);
            foreach (string name in new[] { "upright", "crouched", "jumpClip", "landingClip" })
            {
                var clip = Clip(name); animation.AddClip(clip, name); Set(chase, name, clip);
            }
            Set(chase, "footstep", root.AddComponent<AudioSource>());
            foreach (string name in new[] { "grabArm", "grabTip", "extendedForearm", "graspingHand" }) Set(chase, name, Child(name));
            Set(chase, "captureCable", Child("Cable").gameObject.AddComponent<LineRenderer>());
            Set(chase, "aimRing", Child("Ring").gameObject.AddComponent<LineRenderer>());
            body = root.GetComponent<CapsuleCollider>(); body.radius = 1.4f; body.height = 5; body.center = Vector3.up * 2.5f;
            breach = fixtures.AddComponent(TypeOf("CurtainCall.EscapeRoute.EscapeUnderstageBreach"));
            Set(breach, "entrance", Clip("Entrance")); Set(breach, "lastSample", 2f); Set(chase, "breach", breach);
            boss = root.AddComponent(TypeOf("CurtainCall.EscapeRoute.EscapePursuerBoss")); Set(boss, "chase", chase);
            currentBoss = boss.GetType().GetProperty("Current"); oldBoss = currentBoss.GetValue(null);
            Set(boss, "rig", Child("Boss rig"));
            Set(boss, "landingRing", Child("Boss ring").gameObject.AddComponent<LineRenderer>());
            root.SetActive(true);
            // Ordinary MonoBehaviours do not receive PlayMode initialization in EditMode.
            if (Get(chase, "agent") == null) Call(chase, "Awake");
            Call(boss, "Awake");
            ((Behaviour)Get(chase, "agent")).enabled = false;
            var ground = Own(new GameObject("Supported landing"));
            ground.transform.position = origin - Vector3.up * .5f;
            floor = ground.AddComponent<BoxCollider>(); floor.size = new Vector3(80, 1, 80);
            Physics.SyncTransforms();
        }

        [TearDown] public void Cleanup()
        {
            foreach (var player in testPlayers)
                if (player) player.GetType().GetProperty("IsSpawned", Flags).SetValue(player, false);
            // Destroy actors while their isolated settings still exist; restore all process-wide state.
            if (root) Object.DestroyImmediate(root);
            currentChase?.SetValue(null, oldChase); currentBoss?.SetValue(null, oldBoss);
            currentTimer?.SetValue(null, oldTimer); currentSettings?.SetValue(null, oldSettings);
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i]) Object.DestroyImmediate(owned[i]);
            owned.Clear();
            testPlayers.Clear();
            if (players != null && oldPlayers != null)
            { players.Clear(); foreach (var player in oldPlayers) players.Add(player); }
        }

        object Attack(string phase)
        {
            var cue = Get(chase, "attack"); Phase(cue, phase);
            Set(cue, "Origin", origin + Vector3.up * 2); Set(cue, "Aim", origin + new Vector3(0, 2, 10));
            Set(cue, "MarkedFeet", origin + Vector3.forward * 10); Set(cue, "Drop", origin + Vector3.forward * 10);
            Set(cue, "Started", 0d);
            return cue;
        }
        Component View(object owner, string name) => (Component)Get(owner, name);
        Bounds BoundsOf(Component view, string layer = "Fill")
        {
            var filter = view.transform.Find(layer).GetComponent<MeshFilter>();
            var points = filter.sharedMesh.vertices.Select(filter.transform.TransformPoint).ToArray();
            Assert.That(points.Length, Is.GreaterThan(0));
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var point in points) bounds.Encapsulate(point);
            return bounds;
        }
        void ShowRoute() => Call(chase, "PresentRouteLandingWorldUi", .5f, origin, origin + Vector3.forward * 10);
        void ShowBoss(string phase)
        {
            var state = Get(boss, "state"); Phase(state, phase); Set(state, "Round", 0d);
            Set(state, "From", origin); Set(state, "Goal", origin + Vector3.forward * 10); Set(boss, "state", state);
            Call(boss, "PresentBossWorldUi", .1f);
        }

        Component PreparingPlayer()
        {
            var go = new GameObject("Moving grab target"); go.transform.SetParent(fixtures.transform);
            go.transform.position = origin + Vector3.forward * 6;
            var player = go.AddComponent(TypeOf("CurtainCall.Network.PlayerSync.NetworkPlayer"));
            player.GetType().GetProperty("IsSpawned", Flags).SetValue(player, true);
            player.GetType().GetProperty("OwnerClientId", Flags).SetValue(player, 72UL);
            player.GetType().GetProperty("IsOwner", Flags).SetValue(player, false);
            var mover = go.GetComponent(TypeOf("CurtainCall.Player.PlayerMover"));
            var controller = go.GetComponent<CharacterController>(); controller.height = 1.8f;
            Set(mover, "controller", controller);
            testPlayers.Add(player); players.Add(player);
            Set(chase, "victim", player); Set(chase, "attackRound", 0d);
            ((Transform)Get(chase, "grabArm")).localPosition = Vector3.up * 2;
            ((Transform)Get(chase, "grabTip")).localPosition = new Vector3(0, 2, 1);
            var cue = Attack("Preparing"); Set(cue, "Actor", 72UL);
            Set(cue, "Reach", Enum.Parse(cue.GetType().GetField("Reach").FieldType, "Wire"));
            Set(cue, "Started", 10d); Set(chase, "attack", cue);
            return player;
        }

        void AdvanceArm(double now) => Call(chase, "AdvanceArm", new object[] { now, Vector3.zero });
        float Preparation => (float)chase.GetType().GetProperty("PrepareTime", Flags).GetValue(chase);

        [Test] public void PreparationLocksTheLatestUnpredictedPositionThenWaitsTheFullStartup()
        {
            var player = PreparingPlayer();
            Assert.That(Preparation, Is.EqualTo(.35f).Within(.0001f));
            AdvanceArm(10.1d);
            Assert.That(Get(Get(chase, "attack"), "Phase").ToString(), Is.EqualTo("Preparing"));
            player.transform.position = origin + new Vector3(2, 0, 8);
            var mover = player.GetComponent(TypeOf("CurtainCall.Player.PlayerMover"));
            Set(mover, "groundVelocity", Vector3.right * 80);
            Vector3 expectedAim = player.transform.position + Vector3.up * (1.8f * .68f);
            Vector3 expectedOrigin = (Vector3)Call(chase, "GrabWindupOrigin");
            double lockedAt = 10d + Preparation;
            AdvanceArm(lockedAt);
            var locked = Get(chase, "attack");
            Assert.That(Get(locked, "Phase").ToString(), Is.EqualTo("Warning"));
            Assert.That(Get(locked, "Started"), Is.EqualTo(lockedAt));
            Assert.That(Get(locked, "Aim"), Is.EqualTo(expectedAim));
            Assert.That(Get(locked, "Origin"), Is.EqualTo(expectedOrigin));
            Assert.That(Get(locked, "MarkedFeet"), Is.EqualTo(player.transform.position));
            float warning = (float)Call(chase, "Warning", Get(locked, "Reach"));
            player.transform.position += Vector3.right * 5;
            AdvanceArm(lockedAt + warning - .001d);
            Assert.That(Get(Get(chase, "attack"), "Phase").ToString(), Is.EqualTo("Warning"));
            AdvanceArm(lockedAt + warning + .001d);
            Assert.That(Get(Get(chase, "attack"), "Phase").ToString(), Is.EqualTo("Strike"));
            Assert.That(Get(Get(chase, "attack"), "Aim"), Is.EqualTo(expectedAim));
        }

        [TestCase("despawn")][TestCase("out of reach")][TestCase("blocked")]
        public void PreparationRevalidatesBeforeCommitting(string invalidation)
        {
            var player = PreparingPlayer(); AdvanceArm(10.1d);
            if (invalidation == "despawn") player.GetType().GetProperty("IsSpawned", Flags).SetValue(player, false);
            else if (invalidation == "out of reach") player.transform.position += Vector3.forward * 100;
            else
            {
                var wall = Own(new GameObject("New obstruction")).AddComponent<BoxCollider>();
                wall.transform.position = origin + new Vector3(0, 2, 3); wall.size = new Vector3(8, 6, .25f);
                Physics.SyncTransforms();
            }
            AdvanceArm(10d + Preparation);
            Assert.That(Get(Get(chase, "attack"), "Phase").ToString(), Is.EqualTo("None"));
            Assert.That(Get(chase, "nextSend"), Is.EqualTo(0d));
        }

        [TestCase(1.8f)][TestCase(.9f)]
        public void TargetFollowsBodyAimThenLocksAndPathIsOffByDefaultAndNeverAppearsDuringPreparation(float bodyHeight)
        {
            var player = PreparingPlayer();
            player.GetComponent<CharacterController>().height = bodyHeight;
            AdvanceArm(10.1d);
            var preparing = Get(chase, "attack");
            Assert.That(Get(settings, "showGrabPath"), Is.False);
            Call(chase, "PresentAttack", preparing, 10.1d);
            var target = View(chase, "grabTarget"); Assert.That(Visible(target), Is.True);
            var before = BoundsOf(target, "Boundary");
            Set(chase, "victim", null); // A remote presenter resolves the replicated actor ID through All.
            player.transform.position += new Vector3(2, 1, 0);
            Call(chase, "PresentAttack", preparing, 10.2d);
            var after = BoundsOf(target, "Boundary");
            Assert.That(Vector3.Distance(after.center, before.center), Is.GreaterThan(1f));
            object[] point = { preparing, Vector3.zero };
            Assert.That(Call(chase, "TryAttackTargetPoint", point), Is.True);
            Assert.That((Vector3)point[1], Is.EqualTo(player.transform.position + Vector3.up * (bodyHeight * .68f)));
            AdvanceArm(10d + Preparation);
            var locked = Get(chase, "attack");
            Assert.That(Get(locked, "Phase").ToString(), Is.EqualTo("Warning"));
            Assert.That(Get(locked, "Aim"), Is.EqualTo(point[1]), "The reticle and committed attack must aim at the same body point.");
            Call(chase, "PresentAttack", locked, .1d);
            Assert.That(Visible(View(chase, "attackVolume")), Is.False);
            Assert.That(((LineRenderer)Get(chase, "captureCable")).enabled, Is.False);
            var fixedBounds = BoundsOf(target, "Boundary");
            player.transform.position += Vector3.right * 5;
            Call(chase, "PresentAttack", locked, .2d);
            Assert.That(BoundsOf(target, "Boundary").center, Is.EqualTo(fixedBounds.center));
            Set(settings, "showGrabPath", true);
            Call(chase, "PresentAttack", locked, .2d);
            var volume = View(chase, "attackVolume"); Assert.That(Visible(volume), Is.True);
            Call(chase, "PresentAttack", preparing, 10.2d);
            Assert.That(Visible(volume), Is.False);
            Assert.That(((LineRenderer)Get(chase, "captureCable")).enabled, Is.False);
            Call(chase, "ClearAttack"); Assert.That(Visible(target), Is.False);
            Call(chase, "PresentAttack", preparing, 10.2d); Assert.That(Visible(target), Is.True);
            Call(chase, "PresentRemoteCues", timer); Assert.That(Visible(target), Is.False);
        }

        [Test] public void CameraTargetQueryRequiresAnActiveTargetingPhaseAndCurrentRoundOnBothPeers()
        {
            var player = PreparingPlayer(); var cue = Get(chase, "attack");
            foreach (string phase in new[] { "Preparing", "Warning", "Strike", "Retract", "Grab", "Throw", "None" })
            {
                Phase(cue, phase); Set(chase, "attack", cue);
                Assert.That(Call(chase, "TargetsPlayer", player), Is.EqualTo(phase == "Preparing" || phase == "Warning" || phase == "Strike"), phase);
            }
            Phase(cue, "Warning"); Set(chase, "attack", cue);
            Set(chase, "attackRound", -1d); Assert.That(Call(chase, "TargetsPlayer", player), Is.False);
            Set(chase, "attackRound", 0d);
            var state = JumpSnapshot(11, .5f); Set(state, "Attack", cue);
            Assert.That(Call(chase, "Accept", state, 0d), Is.True);
            Assert.That(Call(chase, "TargetsPlayerAt", player, timer, true), Is.True);
            Set(timer, "startedAt", 1d);
            Assert.That(Call(chase, "TargetsPlayerAt", player, timer, true), Is.False);
            Assert.That(Call(chase, "TargetsPlayer", player), Is.False);
            Set(timer, "startedAt", 0d);
            player.GetType().GetProperty("IsSpawned", Flags).SetValue(player, false);
            Assert.That(Call(chase, "TargetsPlayer", player), Is.False);
        }

        [Test] public void PreparingAppendsToTheWireEnumAndOutOfRangePhasesAreRejected()
        {
            var cue = Attack("Preparing"); var phaseType = Get(cue, "Phase").GetType();
            string[] existing = { "None", "Warning", "Strike", "Retract", "Grab", "Throw", "Preparing" };
            for (int i = 0; i < existing.Length; i++) Assert.That(Convert.ToInt32(Enum.Parse(phaseType, existing[i])), Is.EqualTo(i));
            var state = JumpSnapshot(1, .5f); Set(state, "Attack", cue);
            Assert.That(Call(chase, "Accept", state, 0d), Is.True);
            Set(state, "Time", 2d); Set(cue, "Phase", Enum.ToObject(phaseType, 7)); Set(state, "Attack", cue);
            Assert.That(Call(chase, "Accept", state, 0d), Is.False);
        }

        [Test] public void ShowcaseUsesPreparationThenKeepsItsLockedAimAcrossMovingSamples()
        {
            chase.GetType().GetProperty("InShowcase", Flags).SetValue(chase, true);
            var dummy = Child("Showcase target");
            object[] sample = { .1f, origin + Vector3.forward * 6, Vector3.right, dummy, false, false };
            Call(chase, "SampleAttackPreview", sample);
            Assert.That(Get(Get(chase, "previewAttack"), "Phase").ToString(), Is.EqualTo("Preparing"));
            object[] point = { Get(chase, "previewAttack"), Vector3.zero };
            Assert.That(Call(chase, "TryAttackTargetPoint", point), Is.True);
            Assert.That(point[1], Is.EqualTo(Get(Get(chase, "previewAttack"), "Aim")));
            sample[0] = Preparation + .01f; sample[1] = origin + new Vector3(1, 0, 7);
            Call(chase, "SampleAttackPreview", sample);
            var locked = Get(chase, "previewAttack");
            Assert.That(Get(locked, "Phase").ToString(), Is.EqualTo("Warning"));
            sample[0] = Preparation + .1f; sample[1] = origin + new Vector3(4, 0, 7);
            Call(chase, "SampleAttackPreview", sample);
            Assert.That(Get(Get(chase, "previewAttack"), "Aim"), Is.EqualTo(Get(locked, "Aim")));
            Assert.That(Get(Get(chase, "previewAttack"), "Origin"), Is.EqualTo(Get(locked, "Origin")));
        }

        [Test] public void ActualStrikeShrinksTheSweepWithoutDependingOnLegacyRenderers()
        {
            Set(settings, "showGrabPath", true);
            Set(chase, "captureCable", null); Set(chase, "aimRing", null);
            Call(chase, "PresentAttack", Attack("Warning"), .1d);
            var volume = View(chase, "attackVolume"); Assert.That(Visible(volume), Is.True);
            var warning = BoundsOf(volume, "Boundary");
            float strikeTime = (float)chase.GetType().GetProperty("StrikeTime", Flags).GetValue(chase);
            Call(chase, "PresentAttack", Attack("Strike"), (double)(strikeTime * .5f));
            var strike = BoundsOf(volume, "Boundary");
            Assert.That(strike.min.z - warning.min.z, Is.EqualTo(5f).Within(.03f));
            Assert.That(strike.max.z, Is.EqualTo(warning.max.z).Within(.03f));
            Assert.That(((Transform)Get(chase, "graspingHand")).position.z, Is.EqualTo(origin.z + 5).Within(.01f));
            Call(chase, "PresentAttack", Attack("Retract"), .1d); Assert.That(Visible(volume), Is.False);
            Call(chase, "PresentAttack", Attack("Warning"), .1d); Assert.That(Visible(volume), Is.True);
            Call(chase, "ClearAttack"); Assert.That(Visible(volume), Is.False);
        }

        [Test] public void MissingKitUsesLegacyWithoutStoppingTheArm()
        {
            Set(settings, "showGrabPath", true);
            Set(settings, "volumePrefab", null);
            float duration = (float)chase.GetType().GetProperty("StrikeTime", Flags).GetValue(chase);
            Call(chase, "PresentAttack", Attack("Strike"), (double)(duration * .5f));
            Assert.That(((LineRenderer)Get(chase, "captureCable")).enabled, Is.True);
            Assert.That(((Transform)Get(chase, "graspingHand")).position.z, Is.EqualTo(origin.z + 5).Within(.01f));
        }

        [Test] public void RouteContactUsesPhysicalDiameterAndNeverProjectsOntoAnAbyssFloor()
        {
            ShowRoute(); var view = View(chase, "routeLanding"); Assert.That(Visible(view), Is.True);
            Assert.That(BoundsOf(view).size.x, Is.EqualTo(body.radius * 2).Within(.01f));
            floor.transform.position -= Vector3.up * 20; Physics.SyncTransforms();
            ShowRoute(); Assert.That(Visible(view), Is.False);
            floor.transform.position += Vector3.up * 20; Physics.SyncTransforms();
            ShowRoute(); Assert.That(Visible(view), Is.True);
            Call(chase, "PresentRouteLandingWorldUi", 1f, origin, origin + Vector3.forward * 10);
            Assert.That(Visible(view), Is.False, "Contact ends the warning without changing jump completion timing.");
        }

        [TestCase(false)][TestCase(true)]
        public void ThrownEndpointsUsePhysicalRadiusAndDisappearWithoutSupport(bool debris)
        {
            var cue = debris ? Get(chase, "threat") : Attack("Throw");
            if (debris)
            {
                Phase(cue, "DebrisWarning"); Set(cue, "Duration", 1f);
                Set(cue, "To", origin + Vector3.forward * 10 + Vector3.up);
            }
            string method = debris ? "PresentThreat" : "PresentAttack";
            string field = debris ? "debrisLanding" : "torsoLanding";
            Call(chase, method, cue, .1d);
            var view = View(chase, field); Assert.That(Visible(view), Is.True);
            var tuning = chase.GetType().GetProperty("Tuning", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            float radius = debris ? (float)Get(tuning, "debrisRadius") : .55f;
            Assert.That(BoundsOf(view).size.x, Is.EqualTo(radius * 2).Within(.01f));
            floor.enabled = false; Physics.SyncTransforms();
            Call(chase, method, cue, .1d); Assert.That(Visible(view), Is.False);
            floor.enabled = true; Physics.SyncTransforms();
            Call(chase, method, cue, .1d); Assert.That(Visible(view), Is.True);
            if (debris) Phase(cue, "Impact"); else Set(cue, "Stopped", true);
            Call(chase, method, cue, .1d); Assert.That(Visible(view), Is.False);
        }

        [Test] public void ActualBossAdaptersUseBodyWidthAndHideAcrossPhases()
        {
            root.transform.localScale = new Vector3(2, 1, 3); Physics.SyncTransforms();
            ShowBoss("LeapWarning"); var leap = View(boss, "leapLanding"); Assert.That(Visible(leap), Is.True);
            Assert.That(BoundsOf(leap).size.x, Is.EqualTo(body.radius * 6).Within(.02f));
            ShowBoss("DashWarning"); var dash = View(boss, "dashCorridor");
            Assert.That(Visible(leap), Is.False); Assert.That(Visible(dash), Is.True);
            Assert.That(BoundsOf(dash).size.x, Is.EqualTo(body.radius * 6).Within(.02f));
            ShowBoss("Battle"); Assert.That(Visible(dash), Is.False);
        }

        [Test] public void StopChaseCleansTheRouteEvenWhenItsAgentEarlyReturns()
        {
            ShowRoute(); var view = View(chase, "routeLanding");
            body.enabled = false; // agent is already disabled
            Call(chase, "StopChase"); Assert.That(Visible(view), Is.False);
            body.enabled = true; ShowRoute(); Assert.That(Visible(view), Is.True);
        }

        [Test] public void DisableAndRoundInvalidationClearRealViewsAndAllowReplay()
        {
            Set(settings, "showGrabPath", true);
            Call(chase, "PresentAttack", Attack("Warning"), .1d); ShowRoute();
            var volume = View(chase, "attackVolume"); var route = View(chase, "routeLanding");
            Call(chase, "PresentRemoteCues", timer); // No current snapshot, even though the clock runs.
            Assert.That(Visible(volume) || Visible(route), Is.False);
            Call(chase, "PresentAttack", Attack("Warning"), .1d); ShowRoute();
            ((Behaviour)chase).enabled = false;
            Call(chase, "OnDisable");
            Assert.That(Visible(volume) || Visible(route), Is.False);
            ((Behaviour)chase).enabled = true; body.enabled = true;
            ShowRoute(); Assert.That(Visible(route), Is.True);
            Set(settings, "enabled", false); ShowRoute(); Assert.That(Visible(route), Is.False);
        }

        [TestCase("disable")][TestCase("showcase")][TestCase("restart")][TestCase("stale round")]
        public void BossLifecycleClearsRealViews(string action)
        {
            ShowBoss("LeapWarning"); var view = View(boss, "leapLanding"); Assert.That(Visible(view), Is.True);
            if (action == "disable")
            {
                ((Behaviour)boss).enabled = false;
                Call(boss, "OnDisable");
            }
            else if (action == "restart") Call(boss, "Restart", new object[] { null });
            else
            {
                if (action == "showcase") chase.GetType().GetProperty("InShowcase", Flags).SetValue(chase, true);
                else Set(timer, "startedAt", 10d);
                Call(boss, "LateUpdate");
            }
            Assert.That(Visible(view), Is.False);
        }

        object JumpSnapshot(double time, float sampled, uint revision = 1)
        {
            Set(chase, "jumping", true); Set(chase, "jumpTime", sampled); Set(chase, "jumpRevision", revision);
            Set(chase, "takeoff", origin); Set(chase, "touchdown", origin + Vector3.forward * 10);
            var state = Call(chase, "CaptureSnapshot", 0d, time, 42UL);
            Set(chase, "jumping", false); // Receiver must use the wire endpoints, not local jump state.
            Set(chase, "takeoff", Vector3.zero); Set(chase, "touchdown", Vector3.zero);
            return state;
        }

        [Test] public void RepeatedServiceJumpCommitsANewRevisionWithoutChangingItsPoseClock()
        {
            Call(chase, "BeginServiceJump", origin, origin + Vector3.forward * 10);
            var first = Call(chase, "CaptureSnapshot", 0d, 1d, 42UL);
            Call(chase, "BeginServiceJump", origin, origin + Vector3.forward * 10);
            var second = Call(chase, "CaptureSnapshot", 0d, 2d, 42UL);
            Assert.That(Get(second, "Takeoff"), Is.EqualTo(origin));
            Assert.That(Get(second, "Touchdown"), Is.EqualTo(origin + Vector3.forward * 10));
            Assert.That(Get(second, "JumpRevision"), Is.EqualTo((uint)Get(first, "JumpRevision") + 1));
            Assert.That(Get(second, "JumpTime"), Is.EqualTo(0f));
        }

        [Test] public void WireSnapshotDrivesRemoteLandingAndRejectsTruncatedOrNonServerPayloads()
        {
            var state = JumpSnapshot(1, .5f);
            Set(state, "Attack", Attack("Preparing"));
            var writerType = TypeOf("Unity.Netcode.FastBufferWriter");
            var readerType = TypeOf("Unity.Netcode.FastBufferReader");
            var allocator = Enum.Parse(TypeOf("Unity.Collections.Allocator"), "Temp");
            object writer = Activator.CreateInstance(writerType, new[] { (object)4096, allocator, -1 });
            try
            {
                var write = writerType.GetMethods().Single(m => m.Name == "WriteValueSafe" && m.IsGenericMethodDefinition
                    && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType.IsByRef
                    && m.GetParameters()[1].ParameterType.Name == "ForStructs");
                write.MakeGenericMethod(state.GetType()).Invoke(writer, new[] { state, Activator.CreateInstance(write.GetParameters()[1].ParameterType) });
                var constructor = readerType.GetConstructors().Single(c => c.GetParameters().Length > 0
                    && c.GetParameters()[0].ParameterType == writerType);
                int length = (int)writerType.GetProperty("Length").GetValue(writer);
                foreach (var entry in new[] { (Sender: 1UL, Length: length), (Sender: 0UL, Length: length - 1), (Sender: 0UL, Length: length) })
                {
                    object reader = constructor.Invoke(new[] { writer, allocator, entry.Length, 0, allocator });
                    try { Call(chase, "ReadSnapshot", entry.Sender, reader); }
                    finally { ((IDisposable)reader).Dispose(); }
                    Assert.That(Get(chase, "hasSnapshot"), Is.EqualTo(entry.Sender == 0 && entry.Length == length));
                }
            }
            finally { ((IDisposable)writer).Dispose(); }
            Assert.That(Get(Get(chase, "latest"), "Touchdown"), Is.EqualTo(origin + Vector3.forward * 10));
            Assert.That(Get(Get(chase, "latest"), "LookActor"), Is.EqualTo(42UL));
            Assert.That(Get(Get(Get(chase, "latest"), "Attack"), "Phase").ToString(), Is.EqualTo("Preparing"));
            Call(chase, "PresentRemote", timer);
            var view = View(chase, "routeLanding"); Assert.That(Visible(view), Is.True);
            Assert.That(BoundsOf(view).center.z, Is.EqualTo(origin.z + 10).Within(.01f));
            Assert.That(Get(chase, "sampledTime"), Is.EqualTo(.5f).Within(.001f));
            var landed = JumpSnapshot(2, 1f); Assert.That(Call(chase, "Accept", landed, 0d), Is.True);
            Set(chase, "previous", landed); Call(chase, "PresentRemote", timer);
            Assert.That(Visible(view), Is.False);
        }

        [Test] public void InvalidEndpointsAndReplaysCannotReplaceCommittedJumpButNewRevisionCan()
        {
            var first = JumpSnapshot(1, .7f);
            Assert.That(Call(chase, "Accept", first, 0d), Is.True);
            var bad = JumpSnapshot(2, .8f); Set(bad, "Touchdown", new Vector3(float.NaN, 0, 0));
            Assert.That(Call(chase, "Accept", bad, 0d), Is.False);
            bad = JumpSnapshot(2, .8f); Set(bad, "Takeoff", origin + Vector3.right);
            Assert.That(Call(chase, "Accept", bad, 0d), Is.False);
            Assert.That(Call(chase, "Accept", JumpSnapshot(2, .2f), 0d), Is.False);
            Assert.That(Call(chase, "Accept", first, 0d), Is.False);
            Assert.That(Call(chase, "Accept", JumpSnapshot(2, .1f, 2), 0d), Is.True);
            Assert.That(Get(Get(chase, "previous"), "JumpTime"), Is.EqualTo(.1f), "Do not blend two jumps on the same link.");
        }
    }
}
