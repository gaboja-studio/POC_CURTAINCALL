using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.Editor
{
    public static partial class WaitingRoomLayoutA
    {
        // The 16 m legacy ring grows 2.5x in plan only (deck height, rails, tower and rope heights stay).
        // The tightrope stretches with it; the human cannon and a trampoline stand beside it on parallel lanes;
        // three tiers of seating fill the band between the ring and the 26 m hall wall.
        const float Ring40Scale = 2.5f, Ring40R = 20f, OldRingR = 8f;
        const float Ring40TowerR = 23.6f, Ring40EntryAngle = -40f;
        static readonly float[] Ring40StairAngles = { -90f, 180f };

        public static string EnlargePracticeRing()
        {
            var (scene, target) = Begin(true); int undo = StartUndo("Practice ring x2.5 with seating");
            try
            {
                var room = target.Find("S09_PracticeRoom") ?? throw new InvalidOperationException("Practice room missing");
                var lr = room.Find("LegacyRing") ?? throw new InvalidOperationException("Legacy ring missing");
                if (Quaternion.Angle(lr.rotation, Quaternion.identity) > .01f) throw new InvalidOperationException("Ring frame is rotated; offsets assume world axes");
                var ring = lr.Find("PracticeRing"); var deck = ring.Find("DeckCollider");
                if (deck.localScale.x > OldRingR * 2 + .5f) throw new InvalidOperationException("Ring is already enlarged; rebuild the room before running again");
                var c = lr.position;

                ScaleRingSurface(ring, c);
                RebuildRingPerimeter(ring, c);
                MoveStairs(lr.Find("RaisedRingAccess"), c);
                StretchTightrope(lr, c);
                var stations = ArrangeCannonAndTrampoline(lr, c);
                MoveLightTowers(lr.Find("CanopyAndRig"), c);
                AimStationLights(room, lr, c, stations);
                var arena = room.Find("RoundArena");
                var oldStand = arena.Find("Stand");
                if (oldStand != null) { Undo.RecordObject(oldStand.gameObject, "Retire small stand inside the new ring"); oldStand.gameObject.SetActive(false); }
                Remove(arena, "RingSeating");
                int seats = BuildRingSeating(Group(arena, "RingSeating", c), c);

                Physics.SyncTransforms();
                Undo.CollapseUndoOperations(undo); EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
                return $"Ring {Ring40R * 2} m (was {OldRingR * 2} m), tightrope {(OldRopeLength * Ring40Scale):F1} m, {seats} seating segments";
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        static Vector3 RingPoint(Vector3 c, float r, float deg, float y) => new(c.x + Mathf.Cos(deg * Mathf.Deg2Rad) * r, y, c.z + Mathf.Sin(deg * Mathf.Deg2Rad) * r);
        static float RingAngle(Vector3 c, Vector3 p) => Mathf.Atan2(p.z - c.z, p.x - c.x) * Mathf.Rad2Deg;
        static float RingRadius(Vector3 c, Vector3 p) => new Vector2(p.x - c.x, p.z - c.z).magnitude;

        static void Move(Transform t, Vector3 position, Quaternion? rotation = null)
        {
            Undo.RecordObject(t, "Enlarge practice ring");
            t.position = position;
            if (rotation != null) t.rotation = rotation.Value;
        }

        // Radially symmetric pieces: scale plan dimensions about the ring centre.
        static void ScaleRingSurface(Transform ring, Vector3 c)
        {
            foreach (var n in new[] { "DeckCollider", "StarFloor", "WoodenRim" })
            {
                var t = ring.Find(n); Undo.RecordObject(t, "Scale ring surface");
                var s = t.localScale; t.localScale = new Vector3(s.x * Ring40Scale, s.y, s.z * Ring40Scale);
                var p = t.position; t.position = new Vector3(c.x + (p.x - c.x) * Ring40Scale, p.y, c.z + (p.z - c.z) * Ring40Scale);
            }
        }

        static bool InStairGap(float deg, float halfWidth) => Ring40StairAngles.Any(a => Mathf.Abs(Mathf.DeltaAngle(deg, a)) < halfWidth);

        // Perimeter pieces keep their size and spacing; the count grows with the circumference.
        // Each kind is re-laid from one existing template so material, collider and facing carry over.
        static void RebuildRingPerimeter(Transform ring, Vector3 c)
        {
            float k = Ring40R / OldRingR;
            float postAngle = RingAngle(c, ring.Find("RailPost").position), postStep = 6f;
            float postArc = Ring40R * postStep * Mathf.Deg2Rad;
            // Skirt panels: a closed band around the deck edge, as wide as their arc.
            Relay(ring, c, "HarlequinSkirt", 120, k, null, _ => true, arc => arc + .02f);
            // Rail posts and the rails between them, open where the stairs land.
            Relay(ring, c, "RailPost", 60, k, postAngle, deg => !InStairGap(deg, 2.6f), null);
            Relay(ring, c, "RailTop", 60, k, postAngle + postStep * .5f, deg => !InStairGap(deg, 4f), _ => postArc - .04f);
            Relay(ring, c, "RailPennant", 60, k, postAngle + postStep * .5f, deg => !InStairGap(deg, 4f), _ => postArc - .2f);
            Relay(ring, c, "Bulb", 20, k, postAngle, deg => !InStairGap(deg, 3f), null);
        }

        static void Relay(Transform parent, Vector3 c, string name, int count, float radiusScale, float? baseAngle, Func<float, bool> keep, Func<float, float> length)
        {
            var old = parent.Cast<Transform>().Where(t => t.name == name).ToArray();
            var template = old[0];
            float r0 = RingRadius(c, template.position), a0 = RingAngle(c, template.position), start = baseAngle ?? a0;
            float step = 360f / count, r = r0 * radiusScale;
            for (int i = 0; i < count; i++)
            {
                float deg = start + i * step;
                if (!keep(deg)) continue;
                var copy = Object.Instantiate(template.gameObject, parent).transform; copy.name = name;
                Undo.RegisterCreatedObjectUndo(copy.gameObject, "Ring perimeter");
                copy.position = RingPoint(c, r, deg, template.position.y);
                copy.rotation = Quaternion.Euler(0, -(deg - a0), 0) * template.rotation;
                if (length != null) copy.localScale = new Vector3(length(r * step * Mathf.Deg2Rad), template.localScale.y, template.localScale.z);
            }
            foreach (var t in old) Undo.DestroyObjectImmediate(t.gameObject);
        }

        static void MoveStairs(Transform access, Vector3 c)
        {
            float push = Ring40R - OldRingR;
            foreach (Transform t in access)
            {
                var p = t.position; float deg = RingAngle(c, p);
                var dir = Quaternion.Euler(0, -deg, 0) * Vector3.right;
                Move(t, p + dir * push);
            }
        }

        const float OldRopeLength = 11.5f;

        // Towers, ladders, ladder backstops and anchors slide out along the rope; the rope and its floor tape stretch.
        static void StretchTightrope(Transform lr, Vector3 c)
        {
            float shift = OldRopeLength * (Ring40Scale - 1) * .5f;
            var lanes = lr.Find("PracticeLanes");
            var sideParts = lanes.Cast<Transform>().Where(t => t.name.StartsWith("Tower") || t.name == "SM_Ladder1" || t.name == "RopeAnchor")
                .Concat(lr.Find("CollisionRepairs").Cast<Transform>());
            foreach (var t in sideParts) Move(t, t.position + Vector3.right * Mathf.Sign(t.position.x - c.x) * shift);
            var rope = lanes.Find("Rope"); Undo.RecordObject(rope, "Stretch rope");
            rope.localScale = new Vector3(rope.localScale.x, rope.localScale.y * Ring40Scale, rope.localScale.z);
            foreach (var tape in lanes.Cast<Transform>().Where(t => t.name == "FloorTape" && Mathf.Abs(t.position.z - c.z) < 1f))
            {
                Undo.RecordObject(tape, "Stretch rope tape");
                if (Mathf.Abs(tape.position.x - c.x) < 1f) tape.localScale = new Vector3(tape.localScale.x * Ring40Scale, tape.localScale.y, tape.localScale.z);
                else tape.position += Vector3.right * Mathf.Sign(tape.position.x - c.x) * shift;
            }
        }

        // The cannon lane (launch platform, landing mats, net and their tape) turns to fire east along a lane south of
        // the rope; a trampoline stands on the north side. Returns spotlight targets.
        static Vector3[] ArrangeCannonAndTrampoline(Transform lr, Vector3 c)
        {
            var lanes = lr.Find("PracticeLanes");
            Vector3 oldLaunch = lanes.Find("CannonLaunch").position, oldLanding = lanes.Find("CannonLanding").position;
            const float laneZ = -6.5f;
            Vector3 launch = new(c.x - 8f, oldLaunch.y, c.z + laneZ), landing = new(c.x + 5f, oldLanding.y, c.z + laneZ);
            var turn = Quaternion.Euler(0, 90, 0); // +Z (old firing direction) to +X
            foreach (var t in lanes.Cast<Transform>().ToArray())
            {
                if (t.name.StartsWith("Tower") || t.name is "SM_Ladder1" or "RopeAnchor" or "Rope" or "TightropeLane" or "FittingsAtRaisedDeck") continue;
                if (t.name == "FloorTape" && Mathf.Abs(t.position.z - c.z) < 1f) continue;
                bool launchSide = t.position.z < c.z;
                Vector3 from = launchSide ? oldLaunch : oldLanding, to = launchSide ? launch : landing;
                var offset = t.position - from; offset.y = 0;
                Move(t, to + turn * offset + Vector3.up * (t.position.y - from.y), turn * t.rotation);
            }
            var deckY = lanes.Find("FittingsAtRaisedDeck").position.y;
            var platform = lanes.Find("CannonPlatform").GetComponent<Renderer>().bounds;
            var cannon = PlaceKit("PerformanceArtKit/HumanCannon", lanes, new Vector3(launch.x, platform.max.y, launch.z), 90, 1f);
            if (cannon != null) { cannon.name = "HumanCannon"; Undo.RegisterCreatedObjectUndo(cannon.gameObject, "Human cannon"); }
            var trampolinePos = new Vector3(c.x, deckY, c.z + 6.5f);
            var trampoline = PlaceKit("PerformanceArtKit/CarryTrampoline", lanes, trampolinePos, 0, 1f);
            if (trampoline != null) { trampoline.name = "PracticeTrampoline"; Undo.RegisterCreatedObjectUndo(trampoline.gameObject, "Trampoline"); }
            return new[] { new Vector3(c.x, deckY, c.z), launch, landing, trampolinePos };
        }

        // Light towers stood inside the new ring: move them into gaps in the seating, the one by the entrance
        // swings clear of the doorway. Bulb strings and guy ropes from the canopy stretch out to them.
        static void MoveLightTowers(Transform rig, Vector3 c)
        {
            var heads = rig.Cast<Transform>().Where(t => t.name == "LightTowerHead").ToArray();
            var plans = heads.Select(h =>
            {
                float a = RingAngle(c, h.position), r = RingRadius(c, h.position);
                float to = Mathf.Abs(Mathf.DeltaAngle(a, -45f)) < 5f ? -62f : a;
                return (oldAngle: a, newAngle: to, oldR: r, centre: h.position);
            }).ToArray();
            foreach (var t in rig.Cast<Transform>().Where(t => t.name.StartsWith("LightTower") || t.name == "TowerSpot").ToArray())
            {
                var plan = plans.OrderBy(p => (new Vector2(p.centre.x, p.centre.z) - new Vector2(t.position.x, t.position.z)).sqrMagnitude).First();
                var spin = Quaternion.Euler(0, -(plan.newAngle - plan.oldAngle), 0);
                var newCentre = RingPoint(c, Ring40TowerR, plan.newAngle, plan.centre.y);
                Move(t, newCentre + spin * (t.position - plan.centre), spin * t.rotation);
            }
            // Strings run from the canopy (r≈4 m) to the head's inner edge; remap that span linearly.
            float inner = 4.1f, oldEnd = plans[0].oldR - .5f, newEnd = Ring40TowerR - .5f;
            Vector3 Remap(Vector3 p)
            {
                float r = RingRadius(c, p), a = RingAngle(c, p);
                var plan = plans.OrderBy(q => Mathf.Abs(Mathf.DeltaAngle(q.oldAngle, a))).First();
                float nr = r < inner ? r : inner + (r - inner) * (newEnd - inner) / (oldEnd - inner);
                return RingPoint(c, nr, a + (plan.newAngle - plan.oldAngle), p.y);
            }
            foreach (var t in rig.Cast<Transform>().Where(t => t.name == "Bulb").ToArray()) Move(t, Remap(t.position));
            foreach (var t in rig.Cast<Transform>().Where(t => t.name == "Rope").ToArray())
            {
                if (RingRadius(c, t.position) < inner) continue; // canopy ties
                float half = t.localScale.y; // unit cylinder is 2 m tall
                Vector3 a = Remap(t.position - t.up * half), b = Remap(t.position + t.up * half);
                Undo.RecordObject(t, "Stretch canopy string");
                t.position = (a + b) * .5f; t.rotation = Quaternion.FromToRotation(Vector3.up, b - a);
                t.localScale = new Vector3(t.localScale.x, (b - a).magnitude * .5f, t.localScale.z);
            }
        }

        static void AimStationLights(Transform room, Transform lr, Vector3 c, Vector3[] stations)
        {
            var station = room.Find("RoundArena/Lights").Cast<Transform>().Where(t => t.name == "StationSpot").ToArray();
            for (int i = 0; i < station.Length && i < stations.Length; i++)
            {
                var p = stations[i]; var at = new Vector3(c.x + (p.x - c.x) * .6f, station[i].position.y, c.z + (p.z - c.z) * .6f);
                Move(station[i], at, Quaternion.LookRotation(p - at));
            }
            var rig = lr.Find("CanopyAndRig");
            foreach (var (name, i) in new[] { ("RingSpot_Centre", 0), ("RingSpot_Launch", 1), ("RingSpot_Landing", 2) })
            {
                var t = rig.Find(name); if (t == null) continue;
                Move(t, t.position, Quaternion.LookRotation(stations[i] - t.position));
            }
        }

        // Three stepped tiers of benches between the ring and the hall wall. Gaps: the hall entrance, the two stair
        // approaches and the light towers.
        static int BuildRingSeating(Transform g, Vector3 c)
        {
            var gaps = new List<(float angle, float half)> { (Ring40EntryAngle, 13f) };
            gaps.AddRange(Ring40StairAngles.Select(a => (a, 5f)));
            gaps.AddRange(new[] { 45f, 135f, -135f, -62f }.Select(a => (a, 4.5f)));
            var tiers = new[] { (r: 22.9f, h: .45f), (r: 24.0f, h: .9f), (r: 25.1f, h: 1.35f) };
            const float step = 3f; int n = 0;
            for (float deg = -180; deg < 180; deg += step)
            {
                float mid = deg + step * .5f;
                if (gaps.Any(gp => Mathf.Abs(Mathf.DeltaAngle(mid, gp.angle)) < gp.half)) continue;
                var along = Quaternion.Euler(0, -mid, 0) * Vector3.forward;
                foreach (var (r, h) in tiers)
                {
                    float arc = r * step * Mathf.Deg2Rad;
                    var p = RingPoint(c, r, mid, h * .5f);
                    Box(g, "SeatTier", p, new Vector3(1.1f, h, arc + .02f), Mat("V4_Wood_Old"), true).rotation = Quaternion.LookRotation(along);
                    Box(g, "SeatCushion", p + Vector3.up * (h * .5f + .03f) + (p - new Vector3(c.x, p.y, c.z)).normalized * .2f, new Vector3(.5f, .06f, arc - .06f), Mat("LA_TentRed")).rotation = Quaternion.LookRotation(along);
                }
                // Back rail on the top tier.
                var top = tiers[^1];
                Box(g, "SeatBackRail", RingPoint(c, top.r + .5f, mid, top.h + .55f), new Vector3(.06f, .06f, top.r * step * Mathf.Deg2Rad + .02f), Mat("V4_Iron_Dark")).rotation = Quaternion.LookRotation(along);
                n++;
            }
            return n;
        }
    }
}
