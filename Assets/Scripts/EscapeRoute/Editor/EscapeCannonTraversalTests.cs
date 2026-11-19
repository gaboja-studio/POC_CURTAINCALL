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
        public void FinalApproachSawsAreStaggeredAndLeaveTheLaunchersClear()
        {
            var scene = Open(out bool opened);
            try
            {
                Physics.SyncTransforms();
                var roots = scene.GetRootGameObjects();
                var course = roots.SelectMany(r => r.GetComponentsInChildren<HumanCannonHazardCourse>(true)).Single();
                var registry = new SerializedObject(course).FindProperty("hazards");
                var sweeps = roots.SelectMany(r => r.GetComponentsInChildren<HumanCannonHazard>(true))
                    .Where(h => h.transform.parent.name.StartsWith("11_FinalApproachSaw_")).ToArray();
                var launchers = roots.SelectMany(r => r.GetComponentsInChildren<EscapeExitSaw>(true))
                    .Where(s => s.FinaleBoss).ToArray();
                Assert.That(sweeps.Length, Is.EqualTo(2));
                Assert.That(launchers, Is.Not.Empty);
                var cues = sweeps.Select(s => s.GetComponent<HumanCannonHazardCue>()).ToArray();
                foreach (var saw in sweeps)
                {
                    Assert.That(Enumerable.Range(0, registry.arraySize)
                        .Count(i => registry.GetArrayElementAtIndex(i).objectReferenceValue == saw), Is.EqualTo(1));
                    Assert.That(saw.UsesAuthoredPath && saw.WorldUiVisual, Is.True);
                    var cue = saw.GetComponent<HumanCannonHazardCue>();
                    Assert.That(cue.warning && cue.warningSeconds > 1f, Is.True);
                    Assert.That(Mathf.Abs(Vector3.Dot(saw.BladeNormal, Vector3.up)), Is.GreaterThan(.999f));
                    Assert.That(cue.origin.y, Is.EqualTo(cue.destination.y).Within(.005f));
                    Assert.That(cue.origin.y - cue.warning.position.y, Is.InRange(.20f, .40f));
                    double active = cue.warningSeconds + cue.activeSeconds * .5 - cue.phase;
                    if (active < 0) active += cue.warningSeconds + cue.activeSeconds + cue.restSeconds;
                    var bladeCenter = saw.Position(active, 1f);
                    var physicalBlade = saw.WorldUiVisual.GetComponentsInChildren<MeshFilter>(true).Single();
                    // The asset's thin axis is local Y. It must match the damage disc through the full spin.
                    var previousPosition = saw.WorldUiVisual.position;
                    var previousRotation = saw.WorldUiVisual.rotation;
                    try
                    {
                        foreach (double spinTime in new[] { active, active + .1, active + .3 })
                        {
                            saw.Present(spinTime, 1f);
                            Assert.That(Mathf.Abs(Vector3.Dot(physicalBlade.transform.up, saw.BladeNormal)), Is.GreaterThan(.999f));
                        }
                    }
                    finally { saw.WorldUiVisual.SetPositionAndRotation(previousPosition, previousRotation); }
                    var character = GameSettings.Base.Character;
                    Assert.That(saw.Touches(bladeCenter - Vector3.up * .35f, character.CapsuleHeight,
                        character.CapsuleRadius, active, 1f), Is.True);
                    Assert.That(saw.Touches(bladeCenter + Vector3.up * .5f, character.CapsuleHeight,
                        character.CapsuleRadius, active, 1f), Is.False);
                    for (int sample = 0; sample <= 48; sample++)
                    {
                        var center = Vector3.Lerp(cue.origin, cue.destination, sample / 48f);
                        Assert.That(Physics.Raycast(center, Vector3.down, out var floor, 2f, Physics.DefaultRaycastLayers,
                            QueryTriggerInteraction.Ignore), Is.True, $"No walkable surface at {center}");
                        Assert.That(floor.distance, Is.InRange(.26f, .5f), $"Blade height over {floor.collider.name} at {center}");
                        var extents = new Vector3(saw.Radius, saw.Radius, .1f);
                        Assert.That(Physics.OverlapBox(center, extents, saw.BladeRotation, Physics.DefaultRaycastLayers,
                            QueryTriggerInteraction.Ignore), Is.Empty, $"Saw collides with scene geometry at {center}");
                        Assert.That(launchers.All(launcher => Vector3.Distance(center, launcher.GrabPoint) > 4f), Is.True);
                    }
                }
                for (int sample = 0; sample < 710; sample++)
                    Assert.That(cues.Count(cue => cue.Live(sample * .01)), Is.LessThanOrEqualTo(1));
                Assert.That(launchers.All(s => s.FinaleBoss.BeyondFireBoundary(s.GrabPoint)), Is.True);
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

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
                foreach (string name in new[] { "Extraction service swing", "Labyrinth lower inspection swing 1" })
                {
                    var pole = Find<HumanCannonPole>(scene, name);
                    Assert.That(Enumerable.Range(0, poles.arraySize)
                        .Count(i => poles.GetArrayElementAtIndex(i).objectReferenceValue == pole), Is.EqualTo(1), name);
                }
                var course = roots.SelectMany(r => r.GetComponentsInChildren<CurtainCall.Tightrope.TightropeCourse>()).Single();
                Assert.That(FindAllPoles(scene).Any(p => p.name == "Inspection service swing"), Is.False);
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
        public void FirstCannonLeftBypassHasContinuousSupportAndClearStandingAccess()
        {
            var scene = Open(out bool opened);
            try
            {
                Physics.SyncTransforms();
                var frame = scene.GetRootGameObjects().Single().transform;
                var character = GameSettings.Base.Character;
                var ropes = new[] { "A03_CannonLandingWire", "Labyrinth 02 inspection fork",
                    "Labyrinth 02 inspection approach cable", "Inspection service entry wire" }
                    .Concat(Enumerable.Range(1, 7).Select(i => $"Inspection service descent {i}"));
                foreach (string name in ropes)
                {
                    var rope = Find<BoxCollider>(scene, name);
                    Vector3 end = Vector3.forward * (.5f * rope.size.z - .025f);
                    Vector3 top = rope.center + Vector3.up * (.5f * rope.size.y);
                    CheckSegment(name, rope.transform.TransformPoint(top - end), rope.transform.TransformPoint(top + end));
                }

                CheckLocal("fork to approach", new(64.3f,-6,84.3f), new(70,-6,84));
                CheckLocal("approach to first swing", new(75.5f,-6,84), new(79.35f,-6,85.9f));
                CheckLocal("relay apron to entry wire", new(101,-6,85), new(99,-6,76.05f));
                CheckLocal("entry wire to service swing", new(96,-6,70.2f), new(95,-6,67.85f));
                CheckLocal("receiver to descent wire", new(89,-9,63), new(89,-9,60.6f));
                CheckLocal("last descent wire to conveyor apron", new(77,-22,16.95f), new(75,-22,14.5f));

                AssertReach("Labyrinth Inspection swing 01", new(79.35f,-6,85.9f));

                void AssertReach(string name, Vector3 localFeet)
                {
                    var pole = Find<HumanCannonPole>(scene, name);
                    var center = frame.TransformPoint(localFeet) + Vector3.up * (.5f * character.CapsuleHeight);
                    Assert.That(Vector3.Distance(center, pole.Grip.position), Is.LessThan(GameSettings.Poles.GrabRadius), name);
                    Assert.That(PlayerInteraction.ClearPathFrom(center, pole.Grip.position, pole.transform, ignoreRope: true), Is.True, name);
                }
                void CheckLocal(string label, Vector3 start, Vector3 end) =>
                    CheckSegment(label, frame.TransformPoint(start), frame.TransformPoint(end));
                void CheckSegment(string label, Vector3 start, Vector3 end)
                {
                    int steps = Mathf.CeilToInt(Vector3.Distance(start, end) / .1f);
                    for (int i = 0; i <= steps; ++i)
                    {
                        var feet = Vector3.Lerp(start, end, (float)i / steps);
                        Assert.That(Physics.Raycast(feet + Vector3.up * .12f, Vector3.down, .32f,
                            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), Is.True, $"{label} unsupported at {feet:F2}");
                        float radius = character.CapsuleRadius;
                        var blocked = Physics.OverlapCapsule(feet + Vector3.up * (radius + .08f),
                            feet + Vector3.up * (character.CapsuleHeight - radius + .08f), radius,
                            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                            .Where(c => c.bounds.max.y > feet.y + .20f);
                        Assert.That(blocked.Select(c => c.name), Is.Empty, $"{label} blocked at {feet:F2}");
                        if (label.StartsWith("Inspection service descent"))
                            Assert.That(Physics.Raycast(feet + Vector3.up * .1f, Vector3.up, out var ceiling, 2.25f,
                                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), Is.False,
                                $"{label} low ceiling at {frame.InverseTransformPoint(feet):F2}: {ceiling.collider?.name}");
                    }
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
                var first = Find<HumanCannonPole>(scene, "Labyrinth Inspection swing 01");
                var second = Find<HumanCannonPole>(scene, "Labyrinth Inspection swing 02");
                var third = Find<HumanCannonPole>(scene, "Labyrinth Inspection swing 03");
                var apron = Find<BoxCollider>(scene, "02 inspection relay apron");
                Physics.SyncTransforms();
                foreach (int seat in Enumerable.Range(0, GameSettings.Poles.Capacity))
                {
                    Measure(first, second, null, seat);
                    Measure(second, third, null, seat);
                    Measure(third, null, apron, seat);
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

        [Test]
        public void InspectionServiceLandingIsReachableWithoutASwingOrSprinting()
        {
            var scene = Open(out bool opened);
            var probe = new GameObject("Inspection service normal jump fixture");
            SceneManager.MoveGameObjectToScene(probe, scene);
            try
            {
                var mover = probe.AddComponent<PlayerMover>();
                typeof(PlayerMover).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(mover, null);
                var frame = scene.GetRootGameObjects().Single(t => t.name == "EscapeCampaignFrame").transform;
                var landing = Find<BoxCollider>(scene, "Inspection service swing receiver");
                var rules = GameSettings.Base.Character;
                Physics.SyncTransforms();
                foreach (float offset in new[] { 0f, .12f, .24f })
                {
                    const float dt = 1f / 60f;
                    Vector3 start = frame.TransformPoint(new Vector3(94.58f + offset, -5.96f, 68.05f + offset));
                    Vector3 aim = frame.TransformPoint(new Vector3(91.35f, -9, 65.15f));
                    Vector3 velocity = Vector3.ProjectOnPlane(aim - start, Vector3.up).normalized * rules.FreeMoveSpeed;
                    velocity += Vector3.up * Mathf.Sqrt(2f * rules.Gravity * rules.JumpHeight);
                    mover.Body.enabled = false;
                    mover.transform.position = start;
                    mover.Body.enabled = true;
                    Collider support = null;
                    for (int tick = 0; tick < 180; ++tick)
                    {
                        var flags = mover.Body.Move(BallisticMotion.Step(ref velocity, Vector3.zero, rules.Gravity, dt));
                        Assert.That(flags & CollisionFlags.Above, Is.EqualTo(CollisionFlags.None), "Jump hits the ceiling");
                        if (velocity.y >= 0f || (flags & CollisionFlags.Below) == 0) continue;
                        if (WalkableSupport.TryGetSupport(mover, mover.transform.position, out var hit)) support = hit.collider;
                        break;
                    }
                    Assert.That(support, Is.EqualTo(landing), $"Unable to land from {start:F2}");
                }
            }
            finally
            {
                Object.DestroyImmediate(probe);
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static HumanCannonPole[] FindAllPoles(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<HumanCannonPole>(true)).ToArray();

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
