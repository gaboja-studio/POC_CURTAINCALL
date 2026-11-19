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
                Assert.That(stations.Length, Is.EqualTo(7));
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
        public void BypassRoutesUseRegisteredRopesAndSwingsInsteadOfCannons()
        {
            var scene = Open(out bool opened);
            try
            {
                var roots = scene.GetRootGameObjects();
                var stations = roots.SelectMany(r => r.GetComponentsInChildren<HumanCannonStation>(true));
                Assert.That(stations.Select(s => s.name), Has.None.EqualTo("Labyrinth_02_InspectionRelay"));
                Assert.That(stations.Select(s => s.name), Has.None.EqualTo("Labyrinth_03_ProcessingLaunch"));
                Assert.That(stations.Select(s => s.name), Has.None.EqualTo("Labyrinth_04_ExtractionLaunch"));
                var system = roots.SelectMany(r => r.GetComponentsInChildren<HumanCannonPoleSystem>()).Single();
                var poles = new SerializedObject(system).FindProperty("poles");
                foreach (string name in new[] { "Inspection service swing", "Extraction service swing", "Labyrinth lower inspection swing 1" })
                {
                    var pole = Find<HumanCannonPole>(scene, name);
                    Assert.That(Enumerable.Range(0, poles.arraySize)
                        .Count(i => poles.GetArrayElementAtIndex(i).objectReferenceValue == pole), Is.EqualTo(1), name);
                }
                var course = roots.SelectMany(r => r.GetComponentsInChildren<CurtainCall.Tightrope.TightropeCourse>()).Single();
                var ropes = new SerializedObject(course).FindProperty("sceneRopes");
                foreach (string name in new[] { "Inspection service entry wire", "Extraction service entry wire", "Inspection lower access cable" })
                {
                    var rope = Find<BoxCollider>(scene, name);
                    Assert.That(Enumerable.Range(0, ropes.arraySize)
                        .Count(i => ropes.GetArrayElementAtIndex(i).objectReferenceValue == rope), Is.EqualTo(1), name);
                }
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
        [Test]
        public void FirstCannonLandsWithoutSteeringAndReportsOffCourseFalls()
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
                Inspect(Vector2.zero, 0f, false);
                foreach (var input in new[] { Vector2.left, Vector2.right })
                    Inspect(input, .75f, true);

                void Inspect(Vector2 input, float held, bool allowFall)
                {
                    var hit = Simulate(mover, seat, input, held, out float elapsed, out float drop);
                    string label = $"{seat.name}: input {input}, {held:F2}s, hit {hit.collider?.name}, t={elapsed:F2}s, drop={drop:F2}m";
                    TestContext.WriteLine(label);
                    if (allowFall && !hit.collider)
                    {
                        var feet = mover.transform.position;
                        Assert.That(CommonDamageRules.IsBelowWorldDeathY(feet.y), Is.True, label);
                        var report = typeof(HumanCannonPilot).GetMethod("ShouldReportLanding", BindingFlags.Static | BindingFlags.NonPublic);
                        Assert.That(report.Invoke(null, new object[] { mover, CollisionFlags.None, -1f, feet }), Is.True, label);
                        return;
                    }
                    Assert.That(hit.collider, Is.Not.Null, label);
                    Assert.That(hit.collider.name, Is.EqualTo("A03_CannonLandingWire"), label);
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
                var relay = Find<HumanCannonPole>(scene, "Inspection service swing");
                var apron = Find<BoxCollider>(scene, "02 inspection relay apron");
                var receiver = Find<BoxCollider>(scene, "Inspection service swing receiver");
                Physics.SyncTransforms();
                foreach (int seat in Enumerable.Range(0, GameSettings.Poles.Capacity))
                {
                    Measure(second, third, null, seat);
                    Measure(third, null, apron, seat);
                    Measure(relay, null, receiver, seat);
                    Measure(Find<HumanCannonPole>(scene, "Extraction service swing"), null,
                        Find<BoxCollider>(scene, "Extraction service swing receiver"), seat);
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

        static RaycastHit Simulate(PlayerMover mover, HumanCannonStation station, Vector2 input, float held,
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
                air.SetInput(elapsed < held ? input : Vector2.zero, tick);
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
