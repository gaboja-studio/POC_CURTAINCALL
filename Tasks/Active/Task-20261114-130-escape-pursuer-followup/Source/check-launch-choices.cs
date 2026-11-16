using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CurtainCall.EscapeRoute;
using CurtainCall.EscapeRoute.EditorTools;
using CurtainCall.HumanCannon;
using CurtainCall.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Run: LaunchChoicesAudit.Run(). Read-only edit-mode inspection of the final A/B/C layout.
// Never opens/saves scenes, changes a pose, advances a mechanism, or creates player input.
// Writes only the JSON report. Static clearance is not a live multiplayer completion test.
public static class LaunchChoicesAudit
{
    const string Output = ".harness/local/pursuer-space-20261116/launch-choices.json";
    static readonly string[] EntryNames = {
        "03_Cannon", "Labyrinth_01_ParallelEntryCannon_2",
        "Labyrinth_01_ParallelEntryCannon_3", "Labyrinth_01_ParallelEntryCannon_4"
    };
    static float[] V(Vector3 v) => new[] { v.x, v.y, v.z };
    static string Hierarchy(Transform t) => t.parent ? Hierarchy(t.parent) + "/" + t.name : t.name;
    static Object Reference(Object target, string key)
    {
        var field = new SerializedObject(target).FindProperty(key);
        if (field == null) throw new InvalidOperationException(target.name + " lacks " + key);
        return field.objectReferenceValue;
    }
    static Object[] Registry(Object target, string key)
    {
        var field = new SerializedObject(target).FindProperty(key);
        if (field == null || !field.isArray) throw new InvalidOperationException(target.name + " lacks array " + key);
        return Enumerable.Range(0, field.arraySize).Select(i => field.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
    }

    static object AuditPlayerGuides(EscapePursuerRoute route, Transform frame, HumanCannonStation processing,
        Action<string, bool, object> check, Action<Object> watch)
    {
        var guides = route.PlayerGuides.ToArray();
        var details = new List<object>();
        var zeros = new List<int>();
        bool references = true, progress = true, positions = true, knownZeros = true;
        float last = route.Points.Count - 1;
        for (int i = 0; i < guides.Length; i++)
        {
            var g = guides[i];
            bool refs = g != null && g.from && g.to && g.from.IsChildOf(frame) && g.to.IsChildOf(frame)
                && g.from.gameObject.scene == frame.gameObject.scene && g.to.gameObject.scene == frame.gameObject.scene;
            bool range = g != null && float.IsFinite(g.fromProgress) && float.IsFinite(g.toProgress)
                && g.fromProgress >= 0 && g.fromProgress <= last && g.toProgress >= 0 && g.toProgress <= last;
            references &= refs; progress &= range;
            float lengthSquared = refs ? (g.from.position - g.to.position).sqrMagnitude : float.NaN;
            positions &= float.IsFinite(lengthSquared);
            bool zero = refs && lengthSquared <= 0f;
            bool known = false;
            if (zero)
            {
                zeros.Add(i);
                // The original guide authoring keeps duplicate course vertices here.
                // Runtime deliberately skips them when valid independent segments exist.
                known = new[] { 0, 9, 15 }.Any(n => g.from.name == "Player course " + n
                    && g.to.name == "Player course " + (n + 1) && Mathf.Abs(g.fromProgress - n) < .0001f
                    && Mathf.Abs(g.toProgress - n - 1) < .0001f);
                knownZeros &= known;
            }
            if (g != null) { watch(g.from); watch(g.to); }
            details.Add(new { index = i, from = g != null && g.from ? Hierarchy(g.from) : "null",
                to = g != null && g.to ? Hierarchy(g.to) : "null", referencesValid = refs, progressValid = range,
                zeroLengthSkipped = zero, expectedLegacyZero = known });
        }
        check("player guides present with valid service route", guides.Length > 0 && last > 0, guides.Length);
        check("all player guide endpoints are live scene-frame references", references, null);
        check("all player guide progress values finite and within route", progress, new { minimum = 0, maximum = last });
        check("all player guide segment positions finite", positions, null);
        check("only known legacy zero-length guides are skipped", knownZeros, zeros.ToArray());
        check("nonzero player guides remain available to Project", guides.Any(g => g != null && g.from && g.to
            && float.IsFinite((g.from.position - g.to.position).sqrMagnitude)
            && (g.from.position - g.to.position).sqrMagnitude > 0f), null);

        var root = frame.Find("EscapeBackground/70_LabyrinthRemake/82_IndependentTransferRoutes");
        var poles = root.GetComponentsInChildren<HumanCannonPole>(true);
        var floors = root.GetComponentsInChildren<BoxCollider>(true);
        HumanCannonPole Pole(string name) => poles.Single(p => p.name == name);
        Vector3 FloorTop(string name)
        {
            var box = floors.Single(b => b.name == name);
            watch(box); watch(box.transform);
            return box.transform.TransformPoint(box.center + Vector3.up * box.size.y * .5f);
        }
        var island = FloorTop("Inspection receiving island");
        var balcony = FloorTop("Processing launch receiving balcony");
        void Expected(string name, Vector3 from, Vector3 to, float start, float end)
        {
            var matches = guides.Where(g => g != null && g.from && g.to && g.from.name == name + " from"
                && g.to.name == name + " to").ToArray();
            bool valid = matches.Length == 1 && matches[0].from.IsChildOf(root) && matches[0].to.IsChildOf(root)
                && Vector3.Distance(matches[0].from.position, from) < .02f && Vector3.Distance(matches[0].to.position, to) < .02f
                && Mathf.Abs(matches[0].fromProgress - start) < .0001f && Mathf.Abs(matches[0].toProgress - end) < .0001f;
            check(name + " unique authored guide and progress", valid,
                new { matches = matches.Length, expectedFrom = V(frame.InverseTransformPoint(from)),
                    expectedTo = V(frame.InverseTransformPoint(to)), fromProgress = start, toProgress = end });
        }
        for (int i = 0; i < 7; i++)
        {
            var pole = Pole("Labyrinth lower inspection swing " + (i + 1));
            watch(pole); watch(pole.Grip);
            Vector3 target = i < 6 ? Pole("Labyrinth lower inspection swing " + (i + 2)).Grip.position : island;
            Expected("Lower inspection route " + i, pole.Grip.position - Vector3.up * 1.5f,
                target - Vector3.up * 1.5f, Mathf.Lerp(8, 12, i / 7f), Mathf.Lerp(8, 12, (i + 1) / 7f));
        }
        Expected("Inspection receiver merge", island, frame.TransformPoint(new Vector3(38, -19.2f, -65)), 12, 13);
        var second = Pole("Labyrinth processing swing 2");
        watch(second); watch(second.Grip);
        Expected("Processing launch route", processing.transform.position, balcony, 8, 11);
        Expected("Processing first swing route", balcony, second.Grip.position - Vector3.up * 1.5f, 11, 12);
        Expected("Processing second swing route", second.Grip.position - Vector3.up * 1.5f, island, 12, 12.5f);
        return new { count = guides.Length, skippedLegacyZeroIndices = zeros.ToArray(), rows = details,
            limitation = "Scene references, authored endpoints and progress ranges only; this does not prove runtime target selection, threat coverage or completed branch play." };
    }

    public static object Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Edit-mode geometry inspection only.");
        var scene = SceneManager.GetActiveScene();
        bool dirtyBefore = scene.isDirty;
        var failures = new List<string>();
        var checks = new List<object>();
        var flights = new List<object>();
        var watched = new Dictionary<Object, string>();
        var report = new Dictionary<string, object> {
            ["scene"] = scene.path, ["kind"] = "final launch choice static geometry and reference audit",
            ["utc"] = DateTime.UtcNow.ToString("o"), ["gameplayTested"] = false,
            ["failures"] = failures, ["checks"] = checks, ["flights"] = flights,
            ["limitations"] = new[] {
                "Neutral flight uses the current common gravity and station muzzle velocity; air steering, wind, player timing and network behavior are not exercised.",
                "Capsule sweeps reuse EscapeLabyrinthAudit.PlayerBodyObstacles at the loaded scene's current moving-object poses. They do not prove clearance over every conveyor phase.",
                "Landing-zone support and physical downward rays are both required. No synthetic landing event or protection state is applied.",
                "No mechanism cooldown, encounter behavior, four-player success, or end-to-end route completion is tested."
            }
        };
        void Check(string name, bool pass, object evidence = null)
        { checks.Add(new { name, pass, evidence }); if (!pass) failures.Add(name); }
        void Watch(Object value)
        { if (value && !watched.ContainsKey(value)) watched[value] = EditorJsonUtility.ToJson(value); }
        try
        {
            if (!scene.path.EndsWith("/EscapeRouteLabyrinthPrototype.unity", StringComparison.Ordinal))
                throw new InvalidOperationException("Open the copied labyrinth first; this audit never changes the active scene.");
            var frame = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .Single(t => t.name == "EscapeCampaignFrame");
            if (!frame.Find("EscapeBackground/70_LabyrinthRemake/82_IndependentTransferRoutes"))
                throw new InvalidOperationException("Final independent transfer routes are not present.");
            Check("upright metre campaign frame", Vector3.Distance(frame.lossyScale, Vector3.one) < .0001f
                && Vector3.Dot(frame.up, Vector3.up) > .99999f);
            var cannonSystem = frame.GetComponentsInChildren<HumanCannonSystem>(true).Single();
            var poleSystem = frame.GetComponentsInChildren<HumanCannonPoleSystem>(true).Single();
            var route = frame.GetComponentsInChildren<EscapePursuerRoute>(true).Single();
            var bypass = frame.GetComponentsInChildren<UnderstageCannonBypass>(true).Single();
            var cannonRegistry = Registry(cannonSystem, "stations");
            var poleRegistry = Registry(poleSystem, "poles");
            var allCannons = frame.GetComponentsInChildren<HumanCannonStation>(true);
            var allPoles = frame.GetComponentsInChildren<HumanCannonPole>(true);
            var entry = EntryNames.Select(n => allCannons.Single(s => s.name == n)).ToArray();
            var processing = allCannons.Single(s => s.name == "Labyrinth_03_ProcessingLaunch");
            var stations = entry.Concat(new[] { processing }).ToArray();
            var firstWire = Reference(route, "firstWire") as HumanCannonLandingZone;
            foreach (var value in new Object[] { cannonSystem, poleSystem, route, bypass, firstWire }.Concat(stations)) Watch(value);
            foreach (var t in stations.SelectMany(s => s.GetComponentsInChildren<Transform>(true))) Watch(t);
            var zones = frame.GetComponentsInChildren<HumanCannonLandingZone>(true);
            foreach (var zone in zones) Watch(zone);

            foreach (var pair in new[] { (name: "cannon", values: cannonRegistry), (name: "pole", values: poleRegistry) })
                Check(pair.name + " registry unique and non-null", pair.values.All(v => v)
                    && pair.values.Distinct().Count() == pair.values.Length);
            Check("all live frame cannons registered exactly once", allCannons.Where(c => c.isActiveAndEnabled)
                .All(c => cannonRegistry.Count(o => o == c) == 1));
            Check("all live frame poles registered exactly once", allPoles.Where(p => p.isActiveAndEnabled)
                .All(p => poleRegistry.Count(o => o == p) == 1));
            string[] originals = { "03_Cannon", "04_ReturnCannon", "A04B_ConveyorCannon", "A06_CoreEndAscentCannon", "Labyrinth_02_InspectionRelay", "Labyrinth_04_ExtractionLaunch" };
            Check("original six cannon indices retained", cannonRegistry.Length >= originals.Length
                && originals.Select((n, i) => cannonRegistry[i] && cannonRegistry[i].name == n).All(x => x));
            Check("original moving-pole indices retained", poleRegistry.Length >= 9
                && Enumerable.Range(0, 6).All(i => poleRegistry[i + 3] && poleRegistry[i + 3].name == "A04B_CirculatingGrip_" + i));
            var loops = frame.GetComponentsInChildren<HumanCannonPoleLoop>(true);
            Check("discarded parallel conveyor devices absent", !allCannons.Any(c => c.name.StartsWith("Labyrinth_03_ParallelConveyorCannon_", StringComparison.Ordinal))
                && !loops.Any(l => l.name.StartsWith("Labyrinth_03_ParallelConveyorLoop_", StringComparison.Ordinal)));
            Check("six original conveyor loops retained", loops.Count(l => l.name.StartsWith("LoopTrack_", StringComparison.Ordinal)) == 6
                && Enumerable.Range(0, 6).All(i => loops.Count(l => l.name == "LoopTrack_" + i) == 1));
            foreach (var owner in new Object[] { route, bypass })
            {
                var extras = Registry(owner, "additionalFirstCannons");
                Check(owner.GetType().Name + " entrance references", Reference(owner, "cannon") == cannonSystem
                    && Reference(owner, "firstCannon") == entry[0] && Reference(owner, "firstWire") == firstWire && firstWire
                    && extras.Length == 3 && extras.Distinct().Count() == 3
                    && entry.Skip(1).All(s => extras.Contains(s)) && !extras.Contains(processing));
            }
            report["cannonRegistry"] = cannonRegistry.Select((o, i) => new { index = i, path = o is Component c ? Hierarchy(c.transform) : "null" }).ToArray();
            report["poleRegistry"] = poleRegistry.Select((o, i) => new { index = i, path = o is Component c ? Hierarchy(c.transform) : "null" }).ToArray();
            report["entryRegistration"] = new { firstWire = firstWire ? Hierarchy(firstWire.transform) : "null",
                extraNames = entry.Skip(1).Select(s => s.name).ToArray(),
                processingExcluded = !Registry(route, "additionalFirstCannons").Contains(processing)
                    && !Registry(bypass, "additionalFirstCannons").Contains(processing) };
            report["playerGuides"] = AuditPlayerGuides(route, frame, processing, Check, Watch);

            var bodyProbe = typeof(EscapeLabyrinthAudit).GetMethod("PlayerBodyObstacles", BindingFlags.Static | BindingFlags.NonPublic,
                null, new[] { typeof(Vector3), typeof(Vector3), typeof(Transform), typeof(Transform) }, null);
            if (bodyProbe == null) throw new MissingMethodException("EscapeLabyrinthAudit.PlayerBodyObstacles");
            IEnumerable<Collider> Body(Vector3 from, Vector3 to, Transform own) =>
                (IEnumerable<Collider>)bodyProbe.Invoke(null, new object[] { from, to, frame, own });
            float gravity = GameSettings.Base.Character.Gravity;
            report["commonBody"] = new { gravity, radius = GameSettings.Base.Character.CapsuleRadius,
                height = GameSettings.Base.Character.CapsuleHeight, sampleSeconds = .02f };
            if (gravity <= 0 || !float.IsFinite(gravity)) throw new InvalidOperationException("Invalid common gravity.");
            Physics.SyncTransforms();
            foreach (var station in stations)
            {
                try
                {
                    Check(station.name + " active and registered", station.isActiveAndEnabled && cannonRegistry.Count(o => o == station) == 1);
                    Check(station.name + " independent anchors", station.Muzzle.IsChildOf(station.transform)
                        && station.Seat.IsChildOf(station.transform) && station.BoardingPoint.IsChildOf(station.transform));
                    Vector3 start = station.Muzzle.position, velocity = station.LaunchVelocity;
                    var marker = station.transform.Find("AuthoredLanding");
                    Vector3 target;
                    float duration;
                    string targetSource;
                    if (marker)
                    {
                        target = marker.position; targetSource = "AuthoredLanding";
                        var horizontal = Vector3.ProjectOnPlane(velocity, Vector3.up);
                        if (horizontal.sqrMagnitude < .0001f) throw new InvalidOperationException("Zero horizontal muzzle speed.");
                        duration = Vector3.Dot(Vector3.ProjectOnPlane(target - start, Vector3.up), horizontal) / horizontal.sqrMagnitude;
                    }
                    else if (station == entry[0] && firstWire)
                    {
                        var tile = firstWire.GetComponent<BoxCollider>();
                        if (!tile) throw new InvalidOperationException("Original first wire has no collider.");
                        float top = tile.transform.TransformPoint(tile.center + Vector3.up * (tile.size.y * .5f)).y;
                        float discriminant = velocity.y * velocity.y + 2 * gravity * (start.y - top);
                        if (discriminant < 0) throw new InvalidOperationException("Original trajectory cannot reach first-wire height.");
                        duration = (velocity.y + Mathf.Sqrt(discriminant)) / gravity;
                        target = start + velocity * duration - Vector3.up * (.5f * gravity * duration * duration);
                        targetSource = "original firstWire top; endpoint must be supported by that same zone";
                    }
                    else throw new InvalidOperationException("New launch station lacks AuthoredLanding.");
                    if (!float.IsFinite(duration) || duration <= 0 || duration > 30)
                        throw new InvalidOperationException("Invalid positive flight duration: " + duration);
                    Vector3 At(float t) => start + velocity * t - Vector3.up * (.5f * gravity * t * t);
                    Vector3 end = At(duration);
                    Check(station.name + " ballistic endpoint", float.IsFinite(end.sqrMagnitude) && Vector3.Distance(end, target) <= .1f,
                        new { miss = Vector3.Distance(end, target), duration, targetSource });
                    Check(station.name + " descending landing", velocity.y - gravity * duration <= 0);
                    var hits = new List<object>(); var seen = new HashSet<Collider>();
                    int samples = Mathf.Max(1, Mathf.CeilToInt(duration / .02f));
                    var previous = start;
                    for (int i = 0; i <= samples; i++)
                    {
                        float t = duration * i / samples; var feet = At(t);
                        foreach (var collider in Body(previous, feet, station.transform))
                            if (seen.Add(collider)) hits.Add(new { time = t, feet = V(frame.InverseTransformPoint(feet)), collider = Hierarchy(collider.transform) });
                        previous = feet;
                    }
                    Check(station.name + " body capsule clearance", hits.Count == 0, hits);
                    var endpointHits = Body(end, end, station.transform).Select(c => Hierarchy(c.transform)).Distinct().ToArray();
                    Check(station.name + " endpoint body clearance", endpointHits.Length == 0, endpointHits);
                    var support = Physics.RaycastAll(end + Vector3.up * .2f, Vector3.down, .5f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                        .Where(h => h.collider.gameObject.scene == scene && !h.collider.transform.IsChildOf(station.transform)
                            && h.normal.y >= .8f && Mathf.Abs(h.point.y - end.y) <= .1f)
                        .Select(h => new { collider = Hierarchy(h.collider.transform), point = V(frame.InverseTransformPoint(h.point)), normal = V(h.normal) }).ToArray();
                    Check(station.name + " physical endpoint support", support.Length > 0, support);
                    var safeZones = zones.Where(z => z.isActiveAndEnabled && z.SupportsLanding(end)).ToArray();
                    Check(station.name + " common safe landing zone", safeZones.Length > 0, safeZones.Select(z => Hierarchy(z.transform)).ToArray());
                    if (station != processing)
                        Check(station.name + " original first wire supports endpoint", firstWire && firstWire.SupportsLanding(end));
                    flights.Add(new { station = station.name, registryIndex = Array.IndexOf(cannonRegistry, station), targetSource, duration,
                        start = V(frame.InverseTransformPoint(start)), velocity = V(frame.InverseTransformVector(velocity)),
                        target = V(frame.InverseTransformPoint(target)), end = V(frame.InverseTransformPoint(end)),
                        support, safeZones = safeZones.Select(z => Hierarchy(z.transform)).ToArray(), hits, endpointHits });
                }
                catch (Exception e) { failures.Add(station.name + ": " + e); }
            }
        }
        catch (Exception e) { failures.Add(e.ToString()); }
        finally
        {
            Check("observed serialized objects unchanged", watched.All(p => p.Key && EditorJsonUtility.ToJson(p.Key) == p.Value));
            Check("scene dirty flag unchanged", scene.isDirty == dirtyBefore, new { before = dirtyBefore, after = scene.isDirty });
            report["staticPass"] = failures.Count == 0;
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Output));
            File.WriteAllText(Output, Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
        }
        return new { report = Output, staticPass = failures.Count == 0, failureCount = failures.Count, checks = checks.Count, gameplayTested = false };
    }
}
