using System.Linq;
using System.Reflection;
using CurtainCall.Damage;
using CurtainCall.HumanCannon;
using CurtainCall.Player;
using CurtainCall.Settings;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.EditorTools.Tests
{
    public sealed class EscapeCannonTraversalTests
    {
        const string Path = "Assets/Scenes/Levels/escape_route.unity";
        const string Art = "Assets/Resources/Models/Objects/HumanCannonProps";

        [Test]
        public void AllSavedStationsUseTheSharedTouringMeshesAndFireBurst()
        {
            var scene = EditorSceneManager.OpenPreviewScene(Path);
            try
            {
                var stations = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<HumanCannonStation>()).ToArray();
                Assert.That(stations.Length, Is.EqualTo(10));
                var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_ThirdParty/Task158/TouringCannon.fbx");
                var meshes = model.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh).ToArray();
                var paint = AssetDatabase.LoadAssetAtPath<Material>(Art + "/Materials/TouringCannonPaint.mat");
                var fire = AssetDatabase.LoadAssetAtPath<AnimationClip>(Art + "/Animations/TouringCannon_FireDisable.anim");
                var burstAsset = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/Prefabs/MuzzleBurst.prefab");
                foreach (var station in stations)
                {
                    var view = station.GetComponentsInChildren<CannonBarrelPresentation>(true).Single();
                    var data = new SerializedObject(view);
                    var root = (GameObject)data.FindProperty("animationRoot").objectReferenceValue;
                    Assert.That(meshes.All(m => root.GetComponentsInChildren<MeshFilter>(true)
                        .Any(f => f.sharedMesh == m && f.GetComponent<MeshRenderer>().sharedMaterial == paint)), Is.True, station.name);
                    var burst = view.BarrelPivot.Find("MuzzleBurst");
                    Assert.That(burst, Is.Not.Null, station.name);
                    Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(burst.gameObject), Is.EqualTo(burstAsset), station.name);
                    Assert.That(burst.GetComponentsInChildren<Collider>(true), Is.Empty, station.name);
                    Assert.That(data.FindProperty("fireDisable").objectReferenceValue, Is.EqualTo(fire), station.name);
                    view.Sample(CannonBarrelPhase.FireDisable, .05f / fire.length);
                    Assert.That(burst.gameObject.activeSelf, Is.True, station.name);
                    view.Sample(CannonBarrelPhase.Recover, .1f);
                    Assert.That(burst.gameObject.activeSelf, Is.False, station.name);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void InspectionRelayAimsThroughItsRequiredMidairSwing()
        {
            var scene = Open(out bool opened);
            var probe = new GameObject("Inspection relay flight fixture");
            SceneManager.MoveGameObjectToScene(probe, scene);
            try
            {
                var station = Find<HumanCannonStation>(scene, "Labyrinth_02_InspectionRelay");
                var swing = Find<HumanCannonPole>(scene, "Inspection cannon midair transfer swing");
                Assert.That(station.RequiredTransferPole, Is.SameAs(swing));
                Assert.That(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<HumanCannonPole>(true))
                    .Any(p => p.name == "Inspection flight recovery swing"), Is.False);
                var system = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<HumanCannonPoleSystem>()).Single();
                var registrations = new SerializedObject(system).FindProperty("poles");
                Assert.That(Enumerable.Range(0, registrations.arraySize)
                    .Count(i => registrations.GetArrayElementAtIndex(i).objectReferenceValue == swing), Is.EqualTo(1));

                var mover = probe.AddComponent<PlayerMover>();
                typeof(PlayerMover).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(mover, null);
                probe.transform.position = station.Muzzle.position;
                Physics.SyncTransforms();
                var velocity = station.LaunchVelocity;
                const float dt = 1f / 60f;
                int reachableFrames = 0;
                for (int i = 0; i < 240; i++)
                {
                    var flags = mover.Body.Move(BallisticMotion.Step(ref velocity, Vector3.zero, GameSettings.Base.Character.Gravity, dt));
                    if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0) velocity.y = 0;
                    if ((flags & CollisionFlags.Below) != 0 && velocity.y <= 0) break;
                    var center = PlayerInteraction.Center(mover);
                    if (Vector3.Distance(center, swing.Grip.position) < GameSettings.Poles.GrabRadius
                        && PlayerInteraction.ClearPathFrom(center, swing.Grip.position, swing.transform, mover.transform, true))
                        reachableFrames++;
                }
                Assert.That(reachableFrames, Is.GreaterThanOrEqualTo(12), "The normal shot needs a usable airborne grab window.");
            }
            finally
            {
                Object.DestroyImmediate(probe);
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }
        [Test]
        public void InspectionRelaySteeringCannotLandOnTheDisappearingWires()
        {
            var scene = Open(out bool opened);
            var probe = new GameObject("Inspection relay shortcut fixture");
            SceneManager.MoveGameObjectToScene(probe, scene);
            try
            {
                var mover = probe.AddComponent<PlayerMover>();
                typeof(PlayerMover).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(mover, null);
                var station = Find<HumanCannonStation>(scene, "Labyrinth_02_InspectionRelay");
                var timedRopes = scene.GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<EscapeCooperativeRopes>(true)).Single();
                for (int i = 0; i < 16; i++)
                {
                    float angle = i * Mathf.PI / 8f;
                    var input = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    foreach (float reverseAt in new[] { .6f, 1.2f, 1.8f, 2.4f, 3f, float.PositiveInfinity })
                    {
                        var hit = Simulate(mover, station, input, 10f, reverseAt, out _, out _);
                        if (hit.collider != null)
                            Assert.That(timedRopes.IsTimedSupport(hit.collider), Is.False,
                                $"Steering {input} with reversal at {reverseAt:F1}s reached {hit.collider.name}.");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(probe);
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void InspectionRelayAirBoundaryBlocksTheTimedWireApproachAtAnyHeight()
        {
            var scene = Open(out bool opened);
            try
            {
                var station = Find<HumanCannonStation>(scene, "Labyrinth_02_InspectionRelay");
                var receiver = Find<BoxCollider>(scene, "03 conveyor eastern decision apron");
                Assert.That(station.TransferLanding, Is.SameAs(receiver));
                var frame = scene.GetRootGameObjects().Single().transform;
                foreach (float y in new[] { -12f, 0f, 25f })
                {
                    var start = frame.TransformPoint(new Vector3(78f, y, 25f));
                    var approach = frame.TransformVector(new Vector3(30f, -2f, 0f));
                    var step = station.ConstrainTransferFlight(start, approach, .45f);
                    Assert.That(frame.InverseTransformPoint(start + step).x, Is.LessThan(85f));
                    Assert.That(step.y, Is.EqualTo(approach.y));
                }
                var release = station.RequiredTransferPole.Grip.position;
                var intended = receiver.bounds.center - release;
                Assert.That(station.ConstrainTransferFlight(release, intended, .45f), Is.EqualTo(intended));
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void FirstCannonLandsUnderNeutralSteeringAndRecoveryInputs()
        {
            var scene = Open(out bool opened);
            var probe = new GameObject("Edit-mode cannon collision fixture");
            SceneManager.MoveGameObjectToScene(probe, scene);
            try
            {
                var mover = probe.AddComponent<PlayerMover>();
                typeof(PlayerMover).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(mover, null);
                Physics.SyncTransforms();
                var seat = Find<HumanCannonStation>(scene, "03_Cannon");
                var zones = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<HumanCannonLandingZone>()).ToArray();
                var tested = new[] { Vector2.zero, Vector2.left, Vector2.right, Vector2.down, Vector2.up, new Vector2(-1,-1), new Vector2(1,-1) };
                foreach (var input in tested)
                {
                    Check(seat, input, .75f, "A03_CannonLandingWire", "Entry wire short-shot recovery", "Entry wire west recovery", "Entry wire east recovery");
                }
                Check(seat, Vector2.down, 10f, "Entry wire short-shot recovery");
                foreach (var input in new[] { Vector2.left, Vector2.right, Vector2.down })
                {
                    Inspect(seat, input, .8f, .4f, new[] { "A03_CannonLandingWire", "Entry wire short-shot recovery",
                        "Entry wire west recovery", "Entry wire east recovery" });
                }

                void Check(HumanCannonStation station, Vector2 input, float held, params string[] surfaces)
                    => Inspect(station, input, held, float.PositiveInfinity, surfaces);

                void Inspect(HumanCannonStation station, Vector2 input, float held, float reverseAt, string[] surfaces)
                {
                    var hit = Simulate(mover, station, input, held, reverseAt, out float elapsed, out float drop);
                    string label = $"{station.name}: input {input}, {held:F2}s, reverse {reverseAt:F2}s, hit {hit.collider?.name}, t={elapsed:F2}s, drop={drop:F2}m";
                    TestContext.WriteLine(label);
                    Assert.That(hit.collider, Is.Not.Null, label);
                    Assert.That(surfaces, Does.Contain(hit.collider.name), label);
                    Assert.That(CommonDamageRules.Fall(drop) == FallOutcome.Safe ||
                        GameSettings.Base.BodyDamage.SafeLandingZones && zones.Any(z => z.SupportsLanding(hit.point)), Is.True, label);
                }
            }
            finally
            {
                Object.DestroyImmediate(probe);
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void RealSwingInputReachesTheWidenedTransferAndBothIntendedLandings()
        {
            var scene = Open(out bool opened);
            var probe = new GameObject("Edit-mode swing collision fixture");
            SceneManager.MoveGameObjectToScene(probe, scene);
            try
            {
                var mover = probe.AddComponent<PlayerMover>();
                typeof(PlayerMover).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(mover, null);
                var second = Find<HumanCannonPole>(scene, "Labyrinth Inspection swing 02");
                var third = Find<HumanCannonPole>(scene, "Labyrinth Inspection swing 03");
                var relay = Find<HumanCannonPole>(scene, "Inspection cannon midair transfer swing");
                var apron = Find<BoxCollider>(scene, "02 inspection relay apron");
                var receiver = Find<BoxCollider>(scene, "03 conveyor eastern decision apron");
                Physics.SyncTransforms();
                foreach (int seat in Enumerable.Range(0, GameSettings.Poles.Capacity))
                {
                    Measure(second, third, null, seat);
                    Measure(third, null, apron, seat);
                    Measure(relay, null, receiver, seat);
                }

                void Measure(HumanCannonPole source, HumanCannonPole target, Collider landing, int seat)
                {
                    const float dt = 1f / 60f;
                    float gravity = GameSettings.Base.Character.Gravity;
                    var body = mover.Body;
                    float angle = 0f, angularSpeed = 0f, closest = float.PositiveInfinity;
                    string nearest = "";
                    for (int tick = 1; tick <= 60 * 6; tick++)
                    {
                        source.Step(ref angle, ref angularSpeed, 1f, dt, gravity);
                        if (tick % 3 != 0 || angle < 0f) continue;
                        Vector3 velocity = source.ReleaseVelocity(angle, angularSpeed, seat, 1f);
                        body.enabled = false;
                        Vector3 start = source.HangingPosition(angle, seat);
                        mover.transform.position = start;
                        body.enabled = true;
                        for (int flight = 1; flight <= 240; flight++)
                        {
                            var flags = body.Move(BallisticMotion.Step(ref velocity, Vector3.zero, gravity, dt));
                            var feet = mover.transform.position;
                            var center = feet + Vector3.up * (GameSettings.Base.Character.CapsuleHeight * .5f);
                            if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f) velocity.y = 0f;
                            float gap = target ? Vector3.Distance(center, target.Grip.position)
                                : Vector3.Distance(feet, landing.bounds.ClosestPoint(feet));
                            if (gap < closest)
                            {
                                closest = gap;
                                nearest = feet.ToString("F2");
                            }
                            bool caught = target && gap < GameSettings.Poles.GrabRadius
                                && PlayerInteraction.ClearPathFrom(center, target.Grip.position, target.transform, mover.transform, true);
                            bool supported = landing && (flags & CollisionFlags.Below) != 0 && velocity.y <= 0f
                                && WalkableSupport.TryGetSupport(mover, feet, out var hit) && hit.collider == landing;
                            if (caught || supported)
                            {
                                TestContext.WriteLine($"{source.name}, seat {seat + 1}: release {tick * dt:F2}s at {angle:F1}°, "
                                    + $"travel {Vector3.Distance(start, feet):F2}m in {flight * dt:F2}s");
                                return;
                            }
                            if ((flags & CollisionFlags.Below) != 0 && velocity.y <= 0f || CommonDamageRules.IsBelowWorldDeathY(feet.y)) break;
                        }
                    }
                    Assert.Fail($"{source.name} seat {seat + 1}: no release reached {(target ? target.name : landing.name)}; closest {closest:F2}m at {nearest}");
                }
            }
            finally
            {
                Object.DestroyImmediate(probe);
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static RaycastHit Simulate(PlayerMover mover, HumanCannonStation station, Vector2 input, float held, float reverseAt,
            out float elapsed, out float drop)
        {
            const float dt = 1f / 60f;
            var velocity = station.LaunchVelocity;
            var forward = Vector3.ProjectOnPlane(velocity, Vector3.up).normalized;
            float limit = Vector3.ProjectOnPlane(velocity, Vector3.up).magnitude + station.AirControlSpeed;
            var air = new CannonAirControl();
            air.BeginPoleFlight(0);
            mover.Body.enabled = false;
            mover.transform.position = station.Muzzle.position;
            mover.Body.enabled = true;
            float peak = mover.transform.position.y;
            elapsed = drop = 0f;
            for (int tick = 1; tick <= 360; tick++)
            {
                elapsed = tick * dt;
                Vector2 direction = elapsed < reverseAt ? input : -input;
                air.SetInput(elapsed < held ? direction : Vector2.zero, tick);
                var correction = air.ConsumePoleVelocity(tick, false, forward, station.AirControlSpeed, velocity, limit, dt);
                var flags = mover.Body.Move(BallisticMotion.Step(ref velocity, correction, GameSettings.Base.Character.Gravity, dt));
                if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f) velocity.y = 0f;
                Vector3 feet = mover.transform.position;
                peak = Mathf.Max(peak, feet.y);
                drop = peak - feet.y;
                if (CommonDamageRules.IsBelowWorldDeathY(feet.y)) break;
                if ((flags & CollisionFlags.Below) != 0 && velocity.y <= 0f && WalkableSupport.TryGetSupport(mover, feet, out var hit))
                    return hit;
            }
            return default;
        }

        static Scene Open(out bool opened)
        {
            var scene = SceneManager.GetSceneByPath(Path);
            opened = !scene.IsValid() || !scene.isLoaded;
            return opened ? EditorSceneManager.OpenScene(Path, OpenSceneMode.Additive) : scene;
        }

        static T Find<T>(Scene scene, string name) where T : Component => scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<T>()).Single(c => c.name == name);
    }
}
