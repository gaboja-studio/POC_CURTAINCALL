using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// SecondaryPoleValidation.Run(): read-only, edit-mode common-rule trajectory audit.
// This is sampled static authoring evidence, never a successful playthrough claim.
public static class SecondaryPoleValidation
{
    const string Output = ".harness/local/pursuer-space-20261116/secondary-poles.json";
    const float Dt = 1f / 120f;
    static readonly string[] Profiles = { "neutral", "toward", "brake-now", "brake-after-0.35", "brake-after-0.70" };
    sealed class Route
    {
        public string name;
        public HumanCannonPole source, receiver;
        public BoxCollider floor;
    }
    sealed class Flight
    {
        public readonly List<Vector3> points = new();
        public readonly List<float> times = new();
        public Vector3 initialVelocity;
    }
    sealed class Row
    {
        public string route, profile;
        public int seat, lostLegs;
        public float angle, duration, bodyHeight, airSpeed, reverseViewError, margin;
        public float[] start, end;
        public bool targetSupported, physicalSupport, bodyClear, usable, physicsChecked;
        public float catchAt = -1, reachFirst = -1, reachLast = -1;
        public List<object> hits = new();
        public string[] support = Array.Empty<string>();
    }
    static float[] V(Vector3 v) => new[] { v.x, v.y, v.z };
    static string Path(Transform t) => t.parent ? Path(t.parent) + "/" + t.name : t.name;
    static float Input(string profile, float time) => profile == "toward" ? 1
        : profile == "brake-now" || profile == "brake-after-0.35" && time >= .35f
        || profile == "brake-after-0.70" && time >= .70f ? -1 : 0;

    static Flight Predict(HumanCannonPole pole, float angle, int seat, int lostLegs, string profile, float duration, float viewSign)
    {
        var flight = new Flight();
        var feet = pole.HangingPosition(angle, seat);
        var velocity = pole.ReleaseVelocity(angle, 0, seat, 1);
        flight.initialVelocity = velocity;
        var air = new CannonAirControl();
        // W/S values are mirrored when the camera faces the other direction; world intent is identical.
        air.BeginPoleFlight(viewSign);
        flight.points.Add(feet); flight.times.Add(0);
        float airSpeed = pole.AirControlSpeed * GameSettings.Base.BodyDamage.MoveMultiplier(lostLegs);
        for (int frame = 0; flight.times[flight.times.Count - 1] < duration - .00001f; frame++)
        {
            float time = flight.times[flight.times.Count - 1], dt = Mathf.Min(Dt, duration - time);
            air.SetInput(new Vector2(0, Input(profile, time) * viewSign), frame);
            var correction = air.ConsumePoleVelocity(frame, false, pole.Forward * viewSign, airSpeed,
                velocity, GameSettings.Poles.HorizontalSpeedLimit, dt);
            feet += BallisticMotion.Step(ref velocity, correction, GameSettings.Base.Character.Gravity, dt);
            flight.points.Add(feet); flight.times.Add(time + dt);
        }
        return flight;
    }

    static IEnumerable<Collider> Body(Vector3 from, Vector3 to, float height, Transform own, Transform frame)
    {
        float radius = GameSettings.Base.Character.CapsuleRadius;
        Vector3 lower = Vector3.up * (radius + .01f), upper = Vector3.up * (height - radius + .01f);
        bool Solid(Collider c) => c && c.gameObject.scene == frame.gameObject.scene
            && !c.transform.IsChildOf(own) && !c.GetComponentInParent<NetworkPlayer>();
        foreach (var c in Physics.OverlapCapsule(to + lower, to + upper, radius,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) if (Solid(c)) yield return c;
        Vector3 delta = to - from;
        if (delta.sqrMagnitude < .000001f) yield break;
        foreach (var hit in Physics.CapsuleCastAll(from + lower, from + upper, radius, delta.normalized, delta.magnitude,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) if (Solid(hit.collider)) yield return hit.collider;
    }
    static float EdgeMargin(BoxCollider floor, Vector3 point)
    {
        var p = floor.transform.InverseTransformPoint(point) - floor.center;
        var scale = floor.transform.lossyScale;
        float radius = GameSettings.Base.Character.CapsuleRadius;
        return Mathf.Min((floor.size.x * .5f - Mathf.Abs(p.x)) * Mathf.Abs(scale.x),
            (floor.size.z * .5f - Mathf.Abs(p.z)) * Mathf.Abs(scale.z)) - radius;
    }

    public static object Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Edit-mode inspection only.");
        var scene = SceneManager.GetActiveScene(); bool dirtyBefore = scene.isDirty;
        var failures = new List<string>(); var checks = new List<object>(); var rows = new List<Row>();
        var watched = new Dictionary<Object, string>(); var summaries = new List<object>();
        var report = new Dictionary<string, object> {
            ["scene"] = scene.path, ["kind"] = "sampled common-rule/static-collider evidence", ["gameplayTested"] = false,
            ["utc"] = DateTime.UtcNow.ToString("o"), ["failures"] = failures, ["checks"] = checks,
            ["rows"] = rows, ["summary"] = summaries,
            ["limitations"] = new[] {
                "Turning-point releases at angular speed zero, selected angles and simple held input profiles only. No player input, movement, damage or mechanism state is modified.",
                "Both source seats are sampled separately. Live passenger-first release ordering and an occupied/swinging receiver are not proved; next grip is its empty neutral top seat.",
                "Lost legs use shared air-control multipliers and crawl capsule height. Arm injury, posture switching, wind, actors, network timing and full route completion require play verification.",
                "Mirrored camera forwards use opposite W/S inputs and the actual common CannonAirControl implementation; this is a numerical symmetry check, not camera input playback.",
                "A failed neutral maximum swing is informational. Critical availability checks require a 45/60 degree option for each source seat and leg condition, including a brake profile for landings.",
                "An endpoint outside the named receiving floor can still hit an adjacent rope/platform; these supports are listed, but are not counted as the intended platform landing."
            }
        };
        void Check(string name, bool pass, object evidence = null)
        { checks.Add(new { name, pass, evidence }); if (!pass) failures.Add(name); }
        void Watch(Object obj) { if (obj && !watched.ContainsKey(obj)) watched[obj] = EditorJsonUtility.ToJson(obj); }
        try
        {
            if (!scene.path.EndsWith("/EscapeRouteLabyrinthPrototype.unity", StringComparison.Ordinal))
                throw new InvalidOperationException("Open the copied final labyrinth first.");
            var frame = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .Single(t => t.name == "EscapeCampaignFrame");
            if (Vector3.Distance(frame.lossyScale, Vector3.one) > .0001f || Vector3.Dot(frame.up, Vector3.up) < .99999f)
                throw new InvalidOperationException("Expected upright, unit-scale campaign frame.");
            var poles = frame.GetComponentsInChildren<HumanCannonPole>(true);
            var boxes = frame.GetComponentsInChildren<BoxCollider>(true);
            HumanCannonPole Pole(string name) => poles.Single(p => p.name == name);
            BoxCollider Floor(string name) => boxes.Single(b => b.name == name);
            var routes = new[] {
                new Route { name = "processing first to second", source = Pole("Labyrinth processing swing 1"), receiver = Pole("Labyrinth processing swing 2") },
                new Route { name = "processing second to island", source = Pole("Labyrinth processing swing 2"), floor = Floor("Processing receiving island") },
                new Route { name = "return north to balcony", source = Pole("Labyrinth Return north swing"), floor = Floor("Return north receiving balcony") },
                new Route { name = "return direct to cable departure", source = Pole("Labyrinth Return direct swing"), floor = Floor("Return direct cable departure") }
            };
            foreach (var route in routes)
            {
                foreach (var obj in new Object[] { route.source, route.receiver, route.floor }) Watch(obj);
                foreach (var t in route.source.GetComponentsInChildren<Transform>(true)) Watch(t);
                if (route.floor) { Watch(route.floor.transform); Watch(route.floor.GetComponent<HumanCannonLandingZone>()); }
                Check(route.name + " active shared mechanisms", route.source.isActiveAndEnabled && !route.source.Loop
                    && (!route.receiver || route.receiver.isActiveAndEnabled && !route.receiver.Loop)
                    && (!route.floor || route.floor.enabled && route.floor.gameObject.activeInHierarchy && !route.floor.isTrigger));
            }
            var directFloor = routes[3].floor;
            Check("final direct island width 5.5m", Mathf.Abs(directFloor.bounds.size.x - 5.5f) < .02f,
                new { frameCenter = V(frame.InverseTransformPoint(directFloor.bounds.center)), worldSize = V(directFloor.bounds.size) });
            var body = GameSettings.Base.Character; var rules = GameSettings.Poles;
            report["settings"] = new { body.Gravity, body.CapsuleHeight, body.CapsuleRadius, rules.GrabRadius,
                rules.PassengerGrabRadius, rules.HangLength, rules.PassengerSeparation, rules.SwingLength, rules.ReleaseUpwardSpeed,
                rules.HorizontalSpeedLimit, rules.AirControlSpeed, rules.MaxSwingAngle, rules.MaxSwingSpeed, rules.SwingMotionSpeed,
                GameSettings.Base.AirControl.ResponseSeconds, GameSettings.Base.AirControl.PumpReleaseGrace,
                moveMultipliers = Enumerable.Range(0, 3).Select(GameSettings.Base.BodyDamage.MoveMultiplier).ToArray() };
            Physics.SyncTransforms();
            foreach (var route in routes)
                foreach (float angle in new[] { 30f, 45f, 60f, 90f })
                    for (int seat = 0; seat < Mathf.Min(2, rules.Capacity); seat++)
                        for (int lostLegs = 0; lostLegs < 3; lostLegs++)
                            foreach (string profile in Profiles)
                            {
                                float height = lostLegs >= 2 ? Mathf.Max(2 * body.CapsuleRadius, GameSettings.Base.BodyDamage.CrawlCapsuleHeight) : body.CapsuleHeight;
                                var origin = route.source.HangingPosition(angle, seat);
                                var velocity = route.source.ReleaseVelocity(angle, 0, seat, 1);
                                float duration = 2f;
                                if (route.floor)
                                {
                                    float top = route.floor.transform.TransformPoint(route.floor.center + Vector3.up * route.floor.size.y * .5f).y;
                                    float discriminant = velocity.y * velocity.y + 2 * body.Gravity * (origin.y - top);
                                    if (discriminant < 0) { failures.Add(route.name + " floor above release apex"); continue; }
                                    duration = (velocity.y + Mathf.Sqrt(discriminant)) / body.Gravity;
                                }
                                var f = Predict(route.source, angle, seat, lostLegs, profile, duration, 1);
                                var reverse = Predict(route.source, angle, seat, lostLegs, profile, duration, -1);
                                var end = f.points[f.points.Count - 1];
                                var row = new Row { route = route.name, angle = angle, seat = seat, lostLegs = lostLegs,
                                    profile = profile, duration = duration, bodyHeight = height,
                                    airSpeed = rules.AirControlSpeed * GameSettings.Base.BodyDamage.MoveMultiplier(lostLegs),
                                    start = V(frame.InverseTransformPoint(origin)), end = V(frame.InverseTransformPoint(end)),
                                    reverseViewError = f.points.Select((p, i) => Vector3.Distance(p, reverse.points[i])).Max() };
                                rows.Add(row);
                                int endIndex = f.points.Count - 1;
                                if (route.receiver)
                                {
                                    Vector3 grip = route.receiver.GrabPoint(0, 0);
                                    for (int i = 0; i < f.points.Count; i++)
                                    {
                                        var center = f.points[i] + Vector3.up * height * .5f;
                                        if (Vector3.Distance(center, grip) > rules.GrabRadius) continue;
                                        if (row.reachFirst < 0) row.reachFirst = f.times[i];
                                        row.reachLast = f.times[i];
                                        if (row.catchAt < 0 && PlayerInteraction.ClearPathFrom(center, grip, route.receiver.transform, null, true))
                                        { row.catchAt = f.times[i]; endIndex = i; }
                                    }
                                    row.targetSupported = row.catchAt >= 0;
                                    row.physicalSupport = row.targetSupported;
                                    row.margin = row.catchAt < 0 ? -1 : rules.GrabRadius - Vector3.Distance(f.points[endIndex] + Vector3.up * height * .5f, grip);
                                }
                                else
                                {
                                    var zone = route.floor.GetComponent<HumanCannonLandingZone>();
                                    row.targetSupported = zone && zone.SupportsLanding(end);
                                    row.margin = EdgeMargin(route.floor, end);
                                    var supports = Physics.RaycastAll(end + Vector3.up * .2f, Vector3.down, .5f,
                                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                                        .Where(h => h.collider.gameObject.scene == scene && h.normal.y >= .8f && Mathf.Abs(h.point.y - end.y) < .12f).ToArray();
                                    row.support = supports.Select(h => Path(h.collider.transform)).Distinct().ToArray();
                                    row.physicalSupport = supports.Any(h => h.collider == route.floor);
                                }
                                // A numerical miss cannot prove the route usable. Also inspect maximum-angle braking misses
                                // so adjacent ropes/rails and premature collisions are visible without treating overshoot as a bug.
                                if (row.targetSupported || angle == 90 && profile == "brake-now")
                                {
                                    row.physicsChecked = true;
                                    var seen = new HashSet<Collider>();
                                    for (int i = 0; i <= endIndex; i++)
                                        foreach (var collider in Body(f.points[Mathf.Max(0, i - 1)], f.points[i], height, route.source.transform, frame))
                                            if (seen.Add(collider)) row.hits.Add(new { time = f.times[i], feet = V(frame.InverseTransformPoint(f.points[i])), collider = Path(collider.transform) });
                                    row.bodyClear = row.hits.Count == 0;
                                }
                                row.usable = row.targetSupported && row.physicalSupport && row.physicsChecked && row.bodyClear;
                            }
            Check("camera forward and backward input equivalent", rows.All(r => r.reverseViewError <= .001f));
            foreach (var route in routes)
                for (int seat = 0; seat < Mathf.Min(2, rules.Capacity); seat++)
                    for (int lostLegs = 0; lostLegs < 3; lostLegs++)
                    {
                        var options = rows.Where(r => r.route == route.name && r.seat == seat && r.lostLegs == lostLegs && (r.angle == 45 || r.angle == 60)).ToArray();
                        var usable = options.Where(r => r.usable && (route.receiver || r.profile.StartsWith("brake-", StringComparison.Ordinal))).ToArray();
                        Check(route.name + " 45/60 option seat " + seat + " lost legs " + lostLegs, usable.Length > 0);
                        summaries.Add(new { route = route.name, seat, lostLegs, usableOptions = usable.Select(r => new { r.angle, r.profile, r.end, r.margin, r.catchAt }).ToArray(),
                            maximumNeutral = rows.Where(r => r.route == route.name && r.seat == seat && r.lostLegs == lostLegs && r.angle == 90 && r.profile == "neutral")
                                .Select(r => new { r.end, r.margin, r.targetSupported }).ToArray() });
                    }
        }
        catch (Exception e) { failures.Add(e.ToString()); }
        finally
        {
            Check("observed serialized objects unchanged", watched.All(p => p.Key && EditorJsonUtility.ToJson(p.Key) == p.Value));
            Check("scene dirty flag unchanged", scene.isDirty == dirtyBefore);
            report["sampledStaticPass"] = failures.Count == 0;
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Output));
            File.WriteAllText(Output, Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
        }
        return new { report = Output, sampledStaticPass = failures.Count == 0, failureCount = failures.Count, rows = rows.Count, gameplayTested = false };
    }
}
