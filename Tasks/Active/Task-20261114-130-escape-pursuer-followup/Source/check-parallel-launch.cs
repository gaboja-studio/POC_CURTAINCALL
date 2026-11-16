using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Settings;
using Object = UnityEngine.Object;

// Edit-mode geometry/data audit only. Does not enter play, move mechanisms, save a scene,
// send input, or mutate players. A clean result is not a multiplayer completion result.
public static class ParallelLaunchAudit
{
    const string Output = ".harness/local/pursuer-space-20261116/parallel-audit.json";
    static float[] V(Vector3 v) => new[] { v.x, v.y, v.z };
    static string Path(Transform t) => t.parent ? Path(t.parent) + "/" + t.name : t.name;
    static Object[] Registry(Object component, string field)
    {
        var list = new SerializedObject(component).FindProperty(field);
        return Enumerable.Range(0, list.arraySize).Select(i => list.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
    }
    static Vector3 FutureGrip(HumanCannonPoleLoop loop, double time, float swing = 0f, int seat = -1)
    {
        var pole = loop.GetComponentsInChildren<HumanCannonPole>(true).Single();
        loop.EvaluatePose(time, out var position, out var rotation, out _, out _);
        var turn = rotation * Quaternion.Inverse(loop.Carriage.rotation);
        var grip = position + turn * (pole.Grip.position - loop.Carriage.position);
        if (seat < 0) return grip;
        return grip + turn * (pole.BarPosition(swing) - pole.Grip.position)
            - Vector3.up * pole.HangLength + pole.SeatOffset(seat);
    }
    static IEnumerable<Collider> BodyHits(Vector3 from, Vector3 to, float radius, float height, Transform frame, Transform own)
    {
        bool Solid(Collider c) => c && c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger
            && c.gameObject.scene == frame.gameObject.scene && (!own || !c.transform.IsChildOf(own))
            && !c.GetComponentInParent<NetworkPlayer>() && !c.GetComponentInParent<HumanCannonPole>();
        var lower = Vector3.up * (radius + .01f);
        var upper = Vector3.up * (height - radius + .01f);
        foreach (var c in Physics.OverlapCapsule(to + lower, to + upper, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            if (Solid(c)) yield return c;
        var delta = to - from;
        if (delta.sqrMagnitude < .0000001f) yield break;
        foreach (var hit in Physics.CapsuleCastAll(from + lower, from + upper, radius, delta.normalized,
            delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            if (Solid(hit.collider)) yield return hit.collider;
    }

    public static object Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Read-only edit-mode audit only.");
        var scene = SceneManager.GetActiveScene();
        bool dirtyBefore = scene.isDirty;
        var failures = new List<string>();
        var checks = new List<object>();
        var flights = new List<object>();
        var rides = new List<object>();
        var report = new Dictionary<string, object> {
            ["scene"] = scene.path, ["kind"] = "static geometry and data audit; not gameplay",
            ["failures"] = failures, ["checks"] = checks, ["flights"] = flights, ["rides"] = rides,
            ["limitations"] = new[] { "No live player inputs, network snapshots, pursuit fairness or full-route completion were tested.",
                "Static capsule sweeps exclude players and moving pole meshes. Future grip spacing is checked separately.",
                "Capture and ride calculations use neutral first-seat capture; actual F timing, swing input and passenger-first disembark require play verification.",
                "Known original six cannon and six moving-pole indices are checked; other original registry entries are recorded, not compared with a pre-edit scene." }
        };
        var watched = new Dictionary<Object, string>();
        void Check(string name, bool pass, object evidence = null)
        { checks.Add(new { name, pass, evidence }); if (!pass) failures.Add(name); }
        try
        {
            if (!scene.path.EndsWith("/EscapeRouteLabyrinthPrototype.unity", StringComparison.Ordinal))
                throw new InvalidOperationException("Open the copied labyrinth; the audit never opens or changes scenes.");
            var frame = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Single(t => t.name == "EscapeCampaignFrame");
            var cannon = frame.GetComponentsInChildren<HumanCannonSystem>(true).Single();
            var poles = frame.GetComponentsInChildren<HumanCannonPoleSystem>(true).Single();
            var cannonArray = Registry(cannon, "stations"); var poleArray = Registry(poles, "poles");
            string[] stationNames = { "A04B_ConveyorCannon", "Labyrinth_03_ParallelConveyorCannon_2", "Labyrinth_03_ParallelConveyorCannon_3", "Labyrinth_03_ParallelConveyorCannon_4" };
            var stations = stationNames.Select(n => frame.GetComponentsInChildren<HumanCannonStation>(true).Single(c => c.name == n)).ToArray();
            var loops = frame.GetComponentsInChildren<HumanCannonPoleLoop>(true).Where(l => l.name.StartsWith("LoopTrack_", StringComparison.Ordinal)
                || l.name.StartsWith("Labyrinth_03_ParallelConveyorLoop_", StringComparison.Ordinal)).ToArray();
            foreach (var obj in new Object[] { cannon, poles }.Concat(stations).Concat(loops)) watched[obj] = EditorJsonUtility.ToJson(obj);
            foreach (var t in stations.SelectMany(s => s.GetComponentsInChildren<Transform>(true)).Concat(loops.SelectMany(l => l.GetComponentsInChildren<Transform>(true))))
                watched[t] = EditorJsonUtility.ToJson(t);
            Check("one shared hazard clock", frame.GetComponentsInChildren<HumanCannonHazardCourse>(true).Length == 1);
            Check("twelve configured active loops", loops.Length == 12 && loops.All(l => l.IsConfigured && l.isActiveAndEnabled));
            if (loops.Length != 12) throw new InvalidOperationException("Twelve loops required before checking synchronized captures.");
            var loop0 = loops.Single(l => l.name == "LoopTrack_0");
            var data = new SerializedObject(loop0);
            float speed = data.FindProperty("metersPerSecond").floatValue;
            float length = loop0.Length, spacing = length / 12f;
            double period = length / speed;
            var phase = loops.ToDictionary(l => l, l => new SerializedObject(l).FindProperty("phase01").floatValue);
            Check("uniform twelve phases", phase.Values.OrderBy(v => v).Select((v, i) => Mathf.Abs(v - i / 12f) < .0001f).All(v => v));
            Check("six original phases preserved", Enumerable.Range(0, 6).All(i => Mathf.Abs(phase[loops.Single(l => l.name == "LoopTrack_" + i)] - i / 6f) < .0001f));
            Check("registry references unique and non-null", cannonArray.All(x => x) && poleArray.All(x => x)
                && cannonArray.Distinct().Count() == cannonArray.Length && poleArray.Distinct().Count() == poleArray.Length);
            // Verified against Prototype-before.unity's six serialized station references.
            string[] oldCannons = { "03_Cannon", "04_ReturnCannon", "A04B_ConveyorCannon", "A06_CoreEndAscentCannon", "Labyrinth_02_InspectionRelay", "Labyrinth_04_ExtractionLaunch" };
            Check("original six cannon indices", cannonArray.Length >= 6 && oldCannons.Select((n, i) => cannonArray[i] && cannonArray[i].name == n).All(v => v), cannonArray.Select((o, i) => new { index = i, name = o ? o.name : "null" }).ToArray());
            Check("original six moving pole indices", poleArray.Length >= 9 && Enumerable.Range(0, 6).All(i => poleArray[i + 3] && poleArray[i + 3].name == "A04B_CirculatingGrip_" + i));
            Check("new devices appended", stations.Skip(1).All(s => Array.IndexOf(cannonArray, s) >= 6)
                && loops.Where(l => l.name.StartsWith("Labyrinth_", StringComparison.Ordinal)).All(l => Array.IndexOf(poleArray, l.GetComponentInChildren<HumanCannonPole>(true)) >= 25));
            report["poleRegistry"] = poleArray.Select((o, i) => new { index = i, path = o is Component c ? Path(c.transform) : "null" }).ToArray();

            float gravity = GameSettings.Base.Character.Gravity, radius = GameSettings.Base.Character.CapsuleRadius, height = GameSettings.Base.Character.CapsuleHeight;
            Vector3 sourceGrip = frame.InverseTransformPoint(FutureGrip(loop0, 0));
            Vector3 start0 = frame.InverseTransformPoint(stations[0].Muzzle.position), velocity0 = frame.InverseTransformVector(stations[0].LaunchVelocity);
            float flightSeconds = (sourceGrip.z - start0.z) / velocity0.z;
            Check("positive flight and gravity", flightSeconds > 0 && gravity > 0 && radius > 0 && height >= radius * 2);
            double captureAt = double.PositiveInfinity;
            float cannonX = frame.InverseTransformPoint(stations[0].transform.position).x;
            foreach (var loop in loops)
            {
                double t = ((sourceGrip.x - cannonX) / speed - phase[loop] * period) % period;
                if (t < 0) t += period;
                if (t < flightSeconds) t += period;
                captureAt = Math.Min(captureAt, t);
            }
            report["timing"] = new { length, speed, period, spacing, arrivalsEvery = spacing / speed, flightSeconds,
                launchAtHazardElapsed = captureAt - flightSeconds, captureAtHazardElapsed = captureAt, gravity, capsuleRadius = radius, capsuleHeight = height,
                commonHoldSeconds = GameSettings.Poles.HoldSeconds, commonSeatCapacity = GameSettings.Poles.Capacity };
            var assigned = new List<HumanCannonPoleLoop>();
            Physics.SyncTransforms();
            for (int i = 0; i < stations.Length; i++)
            {
                var station = stations[i];
                Vector3 Position(float t) => station.Muzzle.position + station.LaunchVelocity * t - Vector3.up * (.5f * gravity * t * t);
                var end = Position(flightSeconds); var desiredGrip = end + Vector3.up * GameSettings.Poles.HangLength;
                var closest = loops.OrderBy(l => (FutureGrip(l, captureAt) - desiredGrip).sqrMagnitude).First();
                float miss = Vector3.Distance(FutureGrip(closest, captureAt), desiredGrip);
                assigned.Add(closest);
                Check("capture alignment " + station.name, miss < .08f, new { loop = closest.name, miss });
                var seen = new HashSet<Collider>(); var hits = new List<object>(); var previous = Position(0);
                int steps = Mathf.CeilToInt(flightSeconds / .02f);
                for (int step = 0; step <= steps; step++)
                {
                    float t = flightSeconds * step / steps; var feet = Position(t);
                    foreach (var c in BodyHits(previous, feet, radius, height, frame, station.transform))
                        if (seen.Add(c)) hits.Add(new { time = t, feet = V(frame.InverseTransformPoint(feet)), collider = Path(c.transform) });
                    previous = feet;
                }
                Check("flight body clearance " + station.name, hits.Count == 0);
                bool boardSupported = Physics.RaycastAll(station.BoardingPoint.position + Vector3.up * .15f, Vector3.down, .4f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore).Any(h => h.collider.gameObject.scene == scene && h.normal.y >= .8f && !h.collider.transform.IsChildOf(station.transform));
                Check("boarding floor support " + station.name, boardSupported);
                flights.Add(new { station = station.name, start = V(frame.InverseTransformPoint(station.Muzzle.position)), end = V(frame.InverseTransformPoint(end)), velocity = V(frame.InverseTransformVector(station.LaunchVelocity)), loop = closest.name, miss, hits });

                double unloadingAfter = -1, receiverAfter = -1;
                var rideHits = new List<object>(); var rideSeen = new HashSet<string>();
                for (double t = 0; t <= GameSettings.Poles.HoldSeconds; t += .05)
                {
                    if (unloadingAfter < 0 && closest.AtUnloadingBay(captureAt + t)) unloadingAfter = t;
                    var neutral = FutureGrip(closest, captureAt + t, 0, 0);
                    if (receiverAfter < 0 && frame.InverseTransformPoint(neutral).z <= -65.4f) receiverAfter = t;
                    if (unloadingAfter >= 0) break;
                    foreach (float swing in new[] { -GameSettings.Poles.MaxSwingAngle, 0f, GameSettings.Poles.MaxSwingAngle })
                    {
                        var feet = FutureGrip(closest, captureAt + t, swing, 0);
                        foreach (var c in BodyHits(feet, feet, radius, height, frame, null))
                        {
                            string key = swing + "|" + Path(c.transform);
                            if (rideSeen.Add(key)) rideHits.Add(new { time = t, swing, feet = V(frame.InverseTransformPoint(feet)), collider = Path(c.transform) });
                        }
                    }
                }
                // A separate search reaches the actual receiver center beyond the unloading threshold.
                if (receiverAfter < 0)
                    for (double t = Math.Max(0, unloadingAfter); t < GameSettings.Poles.HoldSeconds; t += .02)
                        if (frame.InverseTransformPoint(FutureGrip(closest, captureAt + t, 0, 0)).z <= -65.4f) { receiverAfter = t; break; }
                Check("unloading before hold expiry " + station.name, unloadingAfter >= 0 && receiverAfter >= 0);
                Check("ride neutral and full-swing body clearance " + station.name, rideHits.Count == 0);
                rides.Add(new { station = station.name, loop = closest.name, unloadingAfter, receiverAfter,
                    holdMarginAtReceiver = receiverAfter < 0 ? -1 : GameSettings.Poles.HoldSeconds - receiverAfter, rideHits });
            }
            Check("four simultaneous shots use four distinct grips", assigned.Distinct().Count() == 4);
            float minimumGap = float.PositiveInfinity;
            for (double t = 0; t < period; t += .1)
                for (int a = 0; a < loops.Length; a++) for (int b = a + 1; b < loops.Length; b++)
                    minimumGap = Mathf.Min(minimumGap, Vector3.Distance(FutureGrip(loops[a], t), FutureGrip(loops[b], t)));
            float maxSwingReach = GameSettings.Poles.SwingLength;
            Check("neighbor grip envelopes separated", minimumGap > 2 * maxSwingReach + 2 * radius,
                new { minimumGap, twoRiderSwingEnvelope = 2 * maxSwingReach + 2 * radius, note = "Analytic body envelope, not a rendered mesh intersection test." });

            var seats = Enumerable.Range(0, 4).Select(_ => new CannonSeatState()).ToArray();
            bool separate = true;
            for (int i = 0; i < 4; i++) separate &= seats[i].TryApply(CannonAction.Board, (ulong)i, 0);
            separate &= seats[0].TryApply(CannonAction.Launch, 0, seats[0].Revision);
            separate &= !seats[0].IsOccupied && Enumerable.Range(1, 3).All(i => seats[i].Occupant == (ulong)i && seats[i].Revision == 1);
            var purePoles = Enumerable.Range(0, 4).Select(_ => new PoleGateState(GameSettings.Poles.Capacity)).ToArray();
            for (int i = 0; i < 4; i++) separate &= purePoles[i].TryGrab((ulong)i, 0, 0, GameSettings.Poles.HoldSeconds);
            separate &= purePoles[0].TryRelease(0, purePoles[0].Revision, 1, 2);
            separate &= purePoles[0].Count == 0 && Enumerable.Range(1, 3).All(i => purePoles[i].OccupantAt(0) == (ulong)i && purePoles[i].Revision == 1);
            Check("pure per-device seat and pole state independence", separate, "New isolated state objects only; no scene or network runtime was exercised.");

            var entryNames = new[] { "03_Cannon", "Labyrinth_01_ParallelEntryCannon_2", "Labyrinth_01_ParallelEntryCannon_3", "Labyrinth_01_ParallelEntryCannon_4" };
            var entry = entryNames.Select(n => frame.GetComponentsInChildren<HumanCannonStation>(true).Single(s => s.name == n)).ToArray();
            var entryReports = new List<object>();
            report["entryFlights"] = entryReports;
            var durations = new float[4];
            var endpoints = new Vector3[4];
            var landingZones = frame.GetComponentsInChildren<HumanCannonLandingZone>(true);
            var intendedX = new[] { 47.5f, 44.8f, 51.5f, 55.5f };
            for (int i = 0; i < entry.Length; i++)
            {
                var station = entry[i];
                watched[station] = EditorJsonUtility.ToJson(station);
                float drop = frame.InverseTransformPoint(station.Muzzle.position).y + 6f;
                float up = station.LaunchVelocity.y;
                float duration = (up + Mathf.Sqrt(up * up + 2f * gravity * drop)) / gravity;
                durations[i] = duration;
                Vector3 Position(float t) => station.Muzzle.position + station.LaunchVelocity * t - Vector3.up * (.5f * gravity * t * t);
                var end = Position(duration); endpoints[i] = end;
                var target = frame.TransformPoint(new Vector3(intendedX[i], -6, 75));
                Check("entry ballistic target " + station.name, Vector3.Distance(end, target) < .15f, V(frame.InverseTransformPoint(end)));
                var hits = new List<object>(); var seen = new HashSet<Collider>(); var previous = Position(0);
                int steps = Mathf.CeilToInt(duration / .02f);
                for (int step = 0; step <= steps; step++)
                {
                    float t = duration * step / steps; var feet = Position(t);
                    foreach (var c in BodyHits(previous, feet, radius, height, frame, station.transform))
                        if (seen.Add(c)) hits.Add(new { time = t, feet = V(frame.InverseTransformPoint(feet)), collider = Path(c.transform) });
                    previous = feet;
                }
                Check("entry flight body clearance " + station.name, hits.Count == 0);
                var support = landingZones.Where(z => z.isActiveAndEnabled && z.SupportsLanding(end)).Select(z => Path(z.transform)).ToArray();
                Check("entry common landing support " + station.name, support.Length > 0, support);
                Check("entry registered " + station.name, Array.IndexOf(cannonArray, station) >= (i == 0 ? 0 : 6));
                entryReports.Add(new { station = station.name, duration, end = V(frame.InverseTransformPoint(end)), support, hits });
            }
            float entryCapsuleGap = float.PositiveInfinity;
            for (float t = 0; t <= durations.Max(); t += .01f)
                for (int a = 0; a < entry.Length; a++) for (int b = a + 1; b < entry.Length; b++)
                {
                    Vector3 At(int k)
                    {
                        if (t >= durations[k]) return endpoints[k];
                        return entry[k].Muzzle.position + entry[k].LaunchVelocity * t - Vector3.up * (.5f * gravity * t * t);
                    }
                    var first = At(a); var second = At(b);
                    float vertical = Mathf.Max(0, Mathf.Abs(first.y - second.y) - (height - 2 * radius));
                    float horizontal = Vector3.ProjectOnPlane(first - second, Vector3.up).sqrMagnitude;
                    entryCapsuleGap = Mathf.Min(entryCapsuleGap, Mathf.Sqrt(horizontal + vertical * vertical) - 2 * radius);
                }
            Check("entry four simultaneous player capsules separated", entryCapsuleGap > .05f,
                new { minimumSurfaceGap = entryCapsuleGap, note = "Same-clock neutral input model; does not test arbitrary delayed launches or air steering." });
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
        return new { report = Output, staticPass = failures.Count == 0, failures, checks = checks.Count, gameplayTested = false };
    }
}
