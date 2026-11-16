using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.HumanCannon;
using CurtainCall.Player;
using CurtainCall.Settings;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

// Scene-read-only authoring probe. Run after the scene owner has synchronized transforms.
// Writes only its report under .harness/local; never changes scene objects or assets.
// Final B coordinates: the island is north of the retained Y=-18.5 floor beginning at Z=-65.
// Calls the common pendulum/ballistic rules, not a scene-specific movement replacement.
public static class IndependentPoleValidation
{
    static readonly Vector3[] Grips = {
        new(80,-24.5f,-12), new(76,-23,-20), new(72,-21.5f,-28),
        new(68,-20,-36), new(64,-18.5f,-43), new(60,-17.7f,-49), new(56,-17.7f,-54.5f)
    };
    static readonly Vector3 Landing = new(52,-19.2f,-60.5f);
    const float Dt = .002f;

    public static string Run()
    {
        var transforms = SceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var frame = transforms.Single(t => t.name == "EscapeCampaignFrame");
        if (Vector3.Distance(frame.lossyScale, Vector3.one) > .0001f || Vector3.Dot(frame.up, Vector3.up) < .99999f)
            throw new InvalidOperationException("This proposal uses metre coordinates in an upright, unit-scale campaign frame.");
        var rules = GameSettings.Poles;
        var body = GameSettings.Base.Character;
        var actualPoles = transforms.Select(t => t.GetComponent<HumanCannonPole>()).Where(p => p && !p.Loop).ToArray();
        var links = new List<object>();
        var solid = new List<object>();
        for (int i = 0; i < Grips.Length - 1; i++)
        {
            Vector3 forward = Vector3.ProjectOnPlane(Grips[i + 1] - Grips[i], Vector3.up).normalized;
            foreach (float angle in new[] { 30f, 45f, 60f, 90f })
                for (int seat = 0; seat < 2; seat++)
                {
                    Vector3 feet = Feet(Grips[i], forward, angle, seat);
                    Vector3 velocity = Velocity(forward, angle);
                    float first = -1, last = -1, closest = float.PositiveInfinity;
                    for (int step = 0; step <= 900; step++)
                    {
                        float time = step * Dt;
                        float distance = Vector3.Distance(feet + Vector3.up * (body.CapsuleHeight * .5f), Grips[i + 1]);
                        closest = Mathf.Min(closest, distance);
                        if (distance <= rules.GrabRadius) { if (first < 0) first = time; last = time; }
                        feet += BallisticMotion.Step(ref velocity, Vector3.zero, body.Gravity, Dt);
                    }
                    links.Add(new { from = i + 1, to = i + 2, angle, seat,
                        horizontal = Vector3.ProjectOnPlane(Grips[i + 1] - Grips[i], Vector3.up).magnitude,
                        rise = Grips[i + 1].y - Grips[i].y,
                        speed = Vector3.ProjectOnPlane(Velocity(forward, angle), Vector3.up).magnitude,
                        first, last, closest, window = first < 0 ? 0 : last - first });
                }

            // A representative 60-degree release is checked against the existing scene.
            // Includes whichever final supports/colliders the owner has authored in the loaded scene.
            for (int seat = 0; seat < 2; seat++)
            {
                Vector3 feet = Feet(Grips[i], forward, 60, seat), velocity = Velocity(forward, 60);
                var own = actualPoles.FirstOrDefault(p => Vector3.Distance(frame.InverseTransformPoint(p.Grip.position), Grips[i]) < .05f);
                var receiver = actualPoles.FirstOrDefault(p => Vector3.Distance(frame.InverseTransformPoint(p.Grip.position), Grips[i + 1]) < .05f);
                var names = new HashSet<string>();
                float reachableAt = -1;
                bool clearInteraction = false;
                for (int step = 0; step <= 900; step++)
                {
                    Vector3 world = frame.TransformPoint(feet);
                    var center = world + Vector3.up * (body.CapsuleHeight * .5f);
                    Vector3 target = frame.TransformPoint(Grips[i + 1]);
                    if (Vector3.Distance(center, target) <= rules.GrabRadius
                        && PlayerInteraction.ClearPathFrom(center, target, receiver ? receiver.transform : null, null, true))
                    { reachableAt = step * Dt; clearInteraction = true; break; }
                    Vector3 delta = BallisticMotion.Step(ref velocity, Vector3.zero, body.Gravity, Dt);
                    Vector3 lower = world + Vector3.up * body.CapsuleRadius;
                    Vector3 upper = world + Vector3.up * (body.CapsuleHeight - body.CapsuleRadius);
                    bool Obstacle(Collider c) => c && c.gameObject.scene == frame.gameObject.scene && !c.isTrigger
                        && (!own || !c.transform.IsChildOf(own.transform)) && !c.GetComponentInParent<PlayerMover>();
                    foreach (var c in Physics.OverlapCapsule(lower, upper, body.CapsuleRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                        if (Obstacle(c)) names.Add(Path(c.transform));
                    Vector3 worldDelta = frame.TransformVector(delta);
                    if (worldDelta.sqrMagnitude > .000001f)
                        foreach (var hit in Physics.CapsuleCastAll(lower, upper, body.CapsuleRadius, worldDelta.normalized,
                            worldDelta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                            if (Obstacle(hit.collider)) names.Add(Path(hit.collider.transform));
                    feet += delta;
                }
                solid.Add(new { from = i + 1, to = i + 2, seat, angle = 60, reachableAt, clearInteraction,
                    authoredSourceFound = own != null, authoredReceiverFound = receiver != null,
                    obstacles = names.OrderBy(n => n).ToArray() });
            }
        }
        var landings = new List<object>();
        var finalForward = Vector3.ProjectOnPlane(Landing - Grips[6], Vector3.up).normalized;
        foreach (float angle in new[] { 30f, 45f, 60f, 90f })
            for (int seat = 0; seat < 2; seat++)
            {
                Vector3 from = Feet(Grips[6], finalForward, angle, seat), velocity = Velocity(finalForward, angle);
                float discriminant = velocity.y * velocity.y + 2 * body.Gravity * (from.y - Landing.y);
                if (discriminant < 0) { landings.Add(new { angle, seat, reachable = false }); continue; }
                float time = (velocity.y + Mathf.Sqrt(discriminant)) / body.Gravity;
                Vector3 at = from + velocity * time + Vector3.down * (.5f * body.Gravity * time * time);
                // A 6x8 island includes a complete body radius, not only its centre.
                bool inside = Mathf.Abs(at.x - Landing.x) <= 3 - body.CapsuleRadius
                    && Mathf.Abs(at.z - Landing.z) <= 4 - body.CapsuleRadius;
                float apexToFloor = from.y + velocity.y * velocity.y / (2 * body.Gravity) - Landing.y;
                landings.Add(new { angle, seat, reachable = true, time, at = Point(at), inside, apexToFloor,
                    retainedFloorCapsuleGap = at.z - body.CapsuleRadius + 65 });
            }
        Vector3 approach = new(80, -24, -9.4f); // .3m inside the proposed Z=-9.7 lip.
        float entryDistance = Vector3.Distance(approach + Vector3.up * body.CapsuleHeight * .5f, Grips[0]);
        float movingLowestFeet = -17 - rules.HangLength - rules.PassengerSeparation;
        float firstFlightTop = Grips[0].y + rules.SwingLength * (1 - Mathf.Cos(rules.MaxSwingAngle * Mathf.Deg2Rad))
            - rules.HangLength + body.CapsuleHeight + rules.ReleaseUpwardSpeed * rules.ReleaseUpwardSpeed / (2 * body.Gravity);
        string result = JsonConvert.SerializeObject(new {
            scope = "Planning evidence, not gameplay PASS. Neutral empty receiver, healthy standing body, no wind/air steering, turning-point release at angular speed zero. Host latency, occupancy/next-pole swing, support geometry, pursuit and live inputs require separate play.",
            settings = new { body.Gravity, body.CapsuleHeight, body.CapsuleRadius, rules.GrabRadius, rules.HangLength,
                rules.PassengerSeparation, rules.SwingLength, rules.ReleaseUpwardSpeed, rules.HorizontalSpeedLimit },
            Grips = Grips.Select(Point).ToArray(), entry = new { approach = Point(approach), entryDistance, bodyRadiusInsideLip = body.CapsuleRadius },
            rail = new { movingLowestFeet, firstFlightTop, verticalGap = movingLowestFeet - firstFlightTop,
                limitation = "Y envelope assumes the stated moving grip -17; live rail collider and all moving positions still need scene audit." },
            links, representativeSceneChecks = solid, landing = new { center = Point(Landing), width = 6, depth = 8, landings }
        });
        string output = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath),
            ".harness/local/pursuer-space-20261116/independent-poles.json");
        System.IO.File.WriteAllText(output, result);
        return result;
    }

    static Vector3 Feet(Vector3 grip, Vector3 forward, float angle, int seat)
    {
        var r = GameSettings.Poles;
        return grip + PoleSwingMotion.Offset(angle, r.SwingLength, forward) + Vector3.up * r.SwingLength
            - Vector3.up * (r.HangLength + seat * r.PassengerSeparation);
    }
    static Vector3 Velocity(Vector3 forward, float angle)
    {
        var r = GameSettings.Poles;
        float stored = PoleSwingMotion.StoredSpeed(angle, 0, r.SwingLength, GameSettings.Base.Character.Gravity,
            r.SwingMotionSpeed, r.MaxSwingAngle, r.MaxSwingSpeed);
        return CannonAirControl.LimitPoleHorizontalSpeed(forward * stored + Vector3.up * r.ReleaseUpwardSpeed, r.HorizontalSpeedLimit);
    }
    static string Path(Transform t) => t.parent ? Path(t.parent) + "/" + t.name : t.name;
    static object Point(Vector3 p) => new { x = p.x, y = p.y, z = p.z };
}
