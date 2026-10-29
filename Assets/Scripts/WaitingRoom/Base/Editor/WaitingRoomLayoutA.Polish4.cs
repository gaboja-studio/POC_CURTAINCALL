using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.Editor
{
    // Fourth round: cement walls instead of block, the lounge moved off the spawn side, a door frame on the stairwell
    // corridor, walkable steel mezzanines (logistics hall and lobby) with collapsed steelwork, and a fuller stage-gear store.
    public static partial class WaitingRoomLayoutA
    {
        // ================= Cement and rust walls =================

        static Material WallCementRustMaterial()
        {
            const string name = "LA_WallCementRust";
            if (Materials.TryGetValue(name, out var cached) && cached != null) return cached;
            // One 4 m tile (the shell's wall UV scale): cast cement with formwork seams and tie holes, rust bleeding down from above.
            var tex = GeneratedTexture("LA_WallCementRust.png", 1024, false, (u, v) =>
            {
                float mottle = Fbm(u * 6 + 2, v * 6 + 7), fine = Mathf.PerlinNoise(u * 140, v * 140);
                float k = .5f + .14f * mottle + .05f * fine;
                var c = new Color(k, k * .98f, k * .95f);
                // Formwork panel seams every 1 m vertically, 1.33 m across, with tie holes at the corners.
                float sx = Mathf.Repeat(u * 3, 1), sy = Mathf.Repeat(v * 4, 1);
                if (sx < .006f || sy < .006f) c *= .82f;
                if (new Vector2(Mathf.Min(sx, 1 - sx) * 3, Mathf.Min(sy, 1 - sy) * 4).magnitude < .045f) c *= .45f;
                // Rust runs: thin streaks starting near the top, widening and fading downward.
                float lane = Mathf.Repeat(u * 9 + Mathf.PerlinNoise(v * 2, 7) * .3f, 1);
                float strength = Mathf.Pow(Mathf.PerlinNoise(Mathf.Floor(u * 9) * 3.1f, 1.7f), 2.5f) * 1.8f;
                float run = Mathf.Clamp01(1 - Mathf.Abs(lane - .5f) * (14 - v * 6)) * strength * Mathf.SmoothStep(0f, .9f, v);
                var rust = new Color(.42f, .2f, .08f);
                c = Color.Lerp(c, rust, Mathf.Clamp01(run) * .7f);
                // Damp rust tide-mark along the foot of the wall.
                float foot = Mathf.SmoothStep(.18f, 0f, v + (Fbm(u * 10, 4) - .5f) * .08f);
                c = Color.Lerp(c, new Color(.3f, .17f, .09f), foot * .55f);
                return new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b));
            });
            var m = Lit(name, Color.white, .1f, tex, FactoryTex("Concrete/T_Concrete_N.TGA"));
            Materials[name] = m; return m;
        }

        public static string CementRustWalls() => RunPass("Block walls to cement and rust", target =>
        {
            var cement = WallCementRustMaterial(); int swapped = 0;
            foreach (var r in target.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials; bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null && (mats[i].name == "LA_CinderBare")) { mats[i] = cement; changed = true; }
                if (!changed) continue;
                Undo.RecordObject(r, "Cement wall"); r.sharedMaterials = mats; swapped++;
            }
            return $"{swapped} wall renderers now cement with rust";
        });

        // ================= Lounge onto the machine's spot by the dormitory =================

        public static string RelocateLoungeToMachineSpot() => RunPass("Lounge to the dormitory side", target =>
        {
            var lobby = target.Find("S01_Lobby"); var factory = lobby.Find("Frame8Lobby/AbandonedFactory");
            var machine = factory.Cast<Transform>().FirstOrDefault(t => t.name == "SM_Machinery_03" && t.gameObject.activeSelf) ?? throw new InvalidOperationException("Machine already gone");
            var mb = VisibleBounds(machine);
            Undo.RecordObject(machine.gameObject, "Remove machine"); machine.gameObject.SetActive(false);
            int extra = 0;
            foreach (var t in lobby.GetComponentsInChildren<Transform>().Where(t => t != machine && (t.name.Contains("OFFLINE") || (t.GetComponent<TextMesh>() != null && t.GetComponent<TextMesh>().text == "OFFLINE"))).ToArray())
                if (mb.SqrDistance(t.position) < 1.5f) { Undo.RecordObject(t.gameObject, "Remove machine label"); t.gameObject.SetActive(false); extra++; }

            var lounge = target.Find("LG_Lounge"); var lb = VisibleBounds(lounge);
            var floor = lobby.Find("Frame8Lobby/RoundFloor").GetComponent<Renderer>().bounds; var hub = new Vector3(floor.center.x, 0, floor.center.z); float domeR = floor.extents.x - .6f;
            var dorm = VisibleBounds(lobby.Find("Frame8Lobby/Passage_F8_Dorm"));
            var tent = VisibleBounds(lobby.Find("ShabbyChristmasTent/SlumpedConeTent")); tent.Expand(new Vector3(2.6f, 0, 2.6f)); // cone plus the props around its hem
            var lamp = lobby.Find("FallenSpawnLamp"); var lampB = lamp != null ? VisibleBounds(lamp) : new Bounds(); lampB.Expand(1.2f);
            // Path to the dormitory: a 3 m lane from the hall centre to the passage mouth stays clear.
            Vector3 mouth = new Vector3(dorm.min.x, 0, dorm.center.z);
            bool BlocksDorm(Bounds b) { for (int i = 0; i <= 20; i++) { var p = Vector3.Lerp(hub, mouth, i / 20f); var q = new Bounds(new Vector3(p.x, .5f, p.z), new Vector3(3, 1, 3)); if (q.Intersects(b)) return true; } return false; }
            // Keep the hanging fabric and its overhead anchors where they belong: they don't travel with the furniture.
            foreach (var t in lounge.Cast<Transform>().Where(t => t.name is "FabricTopBinding" or "SuspensionTie" or "AnchorEye").ToArray()) { Undo.RecordObject(t.gameObject, "Leave anchors"); t.gameObject.SetActive(false); }
            lb = VisibleBounds(lounge);
            Vector3 best = Vector3.zero; float bestYaw = 0, bestScore = float.MaxValue;
            foreach (float yaw in new[] { 0f, 90f, 180f, 270f })
            {
                var size = Mathf.Abs(Mathf.Repeat(yaw, 180)) < 1 ? lb.size : new Vector3(lb.size.z, lb.size.y, lb.size.x);
                for (float dx = -4; dx <= 4; dx += .5f)
                for (float dz = -4; dz <= 4; dz += .5f)
                {
                    var c = new Vector3(mb.center.x + dx, 0, mb.center.z + dz); var b = new Bounds(new Vector3(c.x, lb.center.y, c.z), size);
                    bool inside = new[] { new Vector3(b.min.x, 0, b.min.z), new Vector3(b.max.x, 0, b.min.z), new Vector3(b.min.x, 0, b.max.z), new Vector3(b.max.x, 0, b.max.z) }.All(p => (p - hub).magnitude < domeR);
                    if (!inside || b.Intersects(tent) || b.Intersects(lampB) || BlocksDorm(b)) continue;
                    // Walls and structure are hard limits; loose wall-side dressing may be cleared to make room.
                    var probe = Physics.OverlapBox(new Vector3(c.x, 1.2f, c.z), new Vector3(size.x * .5f, .9f, size.z * .5f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore).Where(col => !col.transform.IsChildOf(lounge) && !Clearable(col.transform)).ToArray();
                    if (probe.Length > 0) continue;
                    float score = (c - new Vector3(mb.center.x, 0, mb.center.z)).sqrMagnitude;
                    if (score < bestScore) { bestScore = score; best = c; bestYaw = yaw; }
                }
            }
            if (bestScore == float.MaxValue) throw new InvalidOperationException("No room for the lounge by the dormitory");
            int cleared = 0;
            Undo.RecordObject(lounge, "Move lounge");
            var pivot = new Vector3(lb.center.x, 0, lb.center.z);
            lounge.RotateAround(pivot, Vector3.up, bestYaw);
            var nb = VisibleBounds(lounge); lounge.position += new Vector3(best.x - nb.center.x, 0, best.z - nb.center.z);
            Physics.SyncTransforms(); nb = VisibleBounds(lounge); nb.Expand(new Vector3(.3f, 0, .3f));
            foreach (var col in Physics.OverlapBox(new Vector3(nb.center.x, 1.2f, nb.center.z), new Vector3(nb.extents.x, .9f, nb.extents.z), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
            {
                if (col.transform.IsChildOf(lounge) || !Clearable(col.transform)) continue;
                var item = ClearableRoot(col.transform); if (!item.gameObject.activeSelf) continue;
                Undo.RecordObject(item.gameObject, "Clear dressing for lounge"); item.gameObject.SetActive(false); cleared++;
            }
            return $"Machine removed (+{extra} label), {cleared} wall-side props cleared; lounge moved to {best:F1}, turned {bestYaw}°, {Mathf.Sqrt(bestScore):F1} m from the machine's spot; dormitory lane clear";
        });

        // Loose dressing in the lobby that can be cleared for furniture: wall-bay clutter and free-standing factory props, not structure.
        static bool Clearable(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
            {
                if (p.name.StartsWith("DerelictWallBay_")) return true;
                if (p.parent != null && p.parent.name == "AbandonedFactory")
                    return !(p.name.Contains("Deck") || p.name.Contains("Stair") || p.name.Contains("Column") || p.name.Contains("Rail") || p.name.Contains("Beam") || p.name.Contains("Pipe") || p.name.Contains("Truss"));
            }
            return false;
        }
        static Transform ClearableRoot(Transform t)
        {
            for (var p = t; p != null; p = p.parent) { if (p.name.StartsWith("DerelictWallBay_")) return t.parent != null && t.parent != p ? t.parent : t; if (p.parent != null && p.parent.name == "AbandonedFactory") return p; }
            return t;
        }

        // ================= Stairwell corridor door frame (door left open) =================

        public static string StairwellDoorFrame() => RunPass("Stairwell door frame", target =>
        {
            var st = target.Find("ST_Stairwell");
            var reveals = target.GetComponentsInChildren<Transform>().Where(t => t.name == "DoorReveal" && Mathf.Abs(t.position.z - 101.8f) < .3f && t.position.x > 2 && t.position.x < 8).Select(t => t.GetComponent<Renderer>().bounds).Where(b => b.max.y > 2).OrderBy(b => b.center.x).ToArray();
            if (reveals.Length < 2) throw new InvalidOperationException("Stairwell opening not found");
            float xL = reveals[0].max.x, xR = reveals[1].min.x, z = reveals[0].center.z, top = reveals[0].max.y, w = xR - xL;
            Remove(st, "StairwellDoorway");
            var g = Group(st, "StairwellDoorway", new Vector3((xL + xR) * .5f, 0, z)); g.rotation = Quaternion.identity;
            Material steel = Mat("LA_SteelPlate"), dark = Mat("V4_Iron_Dark"), rust = Mat("V4_RustyMetal");
            const float doorH = 3.0f, jamb = .12f;
            // Heavy steel frame: jambs, head and a vented transom panel filling the opening above the doors.
            foreach (float s in new[] { -1f, 1f }) LBox(g, "FrameJamb", new Vector3(s * (w * .5f - jamb * .5f), doorH * .5f, 0), new Vector3(jamb, doorH, .28f), steel, true);
            LBox(g, "FrameHead", new Vector3(0, doorH + .08f, 0), new Vector3(w, .16f, .28f), steel, true);
            LBox(g, "TransomPanel", new Vector3(0, (doorH + .16f + top) * .5f, 0), new Vector3(w, top - doorH - .16f, .06f), rust, true);
            for (int i = 0; i < 6; i++) LBox(g, "TransomVent", new Vector3(-w * .3f + i * w * .12f, (doorH + top) * .5f + .1f, -.035f), new Vector3(w * .08f, .04f, .01f), dark);
            LBox(g, "Threshold", new Vector3(0, .015f, 0), new Vector3(w, .03f, .3f), steel);
            // Two leaves swung wide open against the stairwell walls (into the stairwell side).
            float leafW = (w - jamb * 2) * .5f;
            foreach (float s in new[] { -1f, 1f })
            {
                var hinge = Group(g, s < 0 ? "LeafHingeL" : "LeafHingeR", g.TransformPoint(new Vector3(s * (w * .5f - jamb), 0, .14f)));
                hinge.localRotation = Quaternion.Euler(0, s * 95, 0);
                var leaf = LBox(hinge, s < 0 ? "LockedDoorLeafL" : "LockedDoorLeafR", new Vector3(-s * leafW * .5f, doorH * .5f - .02f, .03f), new Vector3(leafW - .02f, doorH - .06f, .06f), steel, true);
                for (int k = 0; k < 3; k++) LBox(hinge, "LeafStiffener", new Vector3(-s * leafW * .5f, .5f + k * 1.0f, -.005f), new Vector3(leafW - .1f, .08f, .02f), dark);
                foreach (float y in new[] { .4f, 1.5f, 2.6f }) LShape(hinge, PrimitiveType.Cylinder, "Hinge", new Vector3(0, y, .03f), new Vector3(.05f, .1f, .05f), dark);
                LBox(hinge, "PushBar", new Vector3(-s * leafW * .5f, 1.05f, -.04f), new Vector3(leafW * .7f, .05f, .05f), Mat("LA_HazardPaint"));
            }
            // Lock hardware waiting for the locked-door feature: hasp on the right jamb, padlock hanging open.
            var lockPoint = Group(g, "DoorLockPoint", g.TransformPoint(new Vector3(w * .5f - jamb - .02f, 1.1f, -.15f)));
            LBox(lockPoint, "HaspPlate", Vector3.zero, new Vector3(.04f, .18f, .08f), dark);
            LShape(lockPoint, PrimitiveType.Cube, "OpenPadlock", new Vector3(-.03f, -.16f, 0), new Vector3(.06f, .07f, .03f), Mat("LA_Bronze"));
            LRod(lockPoint, "PadlockShackle", new Vector3(-.03f, -.12f, 0), new Vector3(-.03f, -.06f, .02f), .006f, steel);
            Physics.SyncTransforms();
            bool clear = !Physics.CheckCapsule(new Vector3(g.position.x, .5f, z - 1), new Vector3(g.position.x, .5f, z + 1), .4f, ~0, QueryTriggerInteraction.Ignore);
            return $"Door frame {w:F2}×{doorH} m in the stairwell opening (leaves open), walkway clear: {clear}";
        });

        // ================= Steel mezzanine kit =================

        // A straight run of deck against a wall: `a`→`b` along the wall face, `inward` points into the room.
        struct DeckRun { public Vector3 a, b, inward; public float width; }

        static List<Transform> BuildDeck(Transform g, DeckRun run, float deckY, float ceiling, HashSet<int> openRail, System.Random rng, string tag)
        {
            Material plate = DiamondPlateMaterial(), steel = Mat("LA_SteelPlate"), dark = Mat("V4_Iron_Dark"), rust = Mat("V4_RustyMetal");
            var along = (run.b - run.a); float len = along.magnitude; along /= len; var n = run.inward;
            int segs = Mathf.Max(1, Mathf.RoundToInt(len / 2f)); float segLen = len / segs; var made = new List<Transform>();
            for (int i = 0; i < segs; i++)
            {
                var c = run.a + along * (segLen * (i + .5f)) + n * (run.width * .5f); c.y = deckY;
                var seg = Group(g, $"{tag}_Deck_{i}", c); seg.rotation = Quaternion.LookRotation(n, Vector3.up); made.Add(seg);
                LBox(seg, "DeckPlate", new Vector3(0, -.03f, 0), new Vector3(segLen - .02f, .06f, run.width), plate, true);
                LBox(seg, "EdgeBeam", new Vector3(0, -.23f, run.width * .5f - .1f), new Vector3(segLen, .34f, .18f), steel);
                LBox(seg, "WallBeam", new Vector3(0, -.2f, -run.width * .5f + .08f), new Vector3(segLen, .28f, .14f), dark);
                LBox(seg, "Joist", new Vector3(-segLen * .5f + .06f, -.18f, 0), new Vector3(.12f, .24f, run.width - .2f), dark);
                // Knee brace from the wall below and a tie rod up to the roof steel.
                LRod(seg, "KneeBrace", new Vector3(-segLen * .5f + .06f, -1.8f, -run.width * .5f + .05f), new Vector3(-segLen * .5f + .06f, -.3f, run.width * .35f), .04f, rust);
                LBox(seg, "WallCleat", new Vector3(-segLen * .5f + .06f, -1.8f, -run.width * .5f + .05f), new Vector3(.22f, .3f, .04f), dark);
                if (i % 2 == 0) LRod(seg, "TieRod", new Vector3(-segLen * .5f + .1f, 0, run.width * .5f - .1f), new Vector3(-segLen * .5f + .1f, ceiling - deckY - .1f, run.width * .5f - .1f), .015f, dark);
                // Railing on the open edge, left out where a stair arrives.
                if (!openRail.Contains(i))
                {
                    foreach (float x in new[] { -segLen * .5f + .05f, 0f }) LBox(seg, "RailPost", new Vector3(x, .55f, run.width * .5f - .05f), new Vector3(.05f, 1.1f, .05f), steel, true);
                    LBox(seg, "TopRail", new Vector3(0, 1.1f, run.width * .5f - .05f), new Vector3(segLen, .05f, .05f), steel);
                    LBox(seg, "MidRail", new Vector3(0, .55f, run.width * .5f - .05f), new Vector3(segLen, .035f, .035f), steel);
                    LBox(seg, "ToeBoard", new Vector3(0, .07f, run.width * .5f - .04f), new Vector3(segLen, .14f, .02f), Mat("LA_HazardPaint"));
                    LBox(seg, "RailCollider", new Vector3(0, .55f, run.width * .5f - .05f), new Vector3(segLen, 1.1f, .06f), steel, true).GetComponent<Renderer>().enabled = false;
                }
            }
            return made;
        }

        // Straight steel stair from floor point `foot` rising along `dir` to deck height. Walkable through a sloped collider.
        static Transform SteelStair(Transform parent, string name, Vector3 foot, Vector3 dir, float rise, float width = 1.3f)
        {
            Material steel = Mat("LA_SteelPlate"), plate = DiamondPlateMaterial(), dark = Mat("V4_Iron_Dark");
            float run = rise / Mathf.Tan(36 * Mathf.Deg2Rad); int steps = Mathf.CeilToInt(rise / .26f);
            var s = Group(parent, name, foot); s.rotation = Quaternion.LookRotation(dir);
            foreach (float x in new[] { -width * .5f, width * .5f })
            {
                LRod(s, "Stringer", new Vector3(x, .05f, 0), new Vector3(x, rise - .05f, run), .07f, dark);
                LRod(s, "Handrail", new Vector3(x, 1.0f, 0), new Vector3(x, rise + 1.0f, run), .025f, steel);
                for (int k = 0; k <= 4; k++) { float f = k / 4f; LRod(s, "HandrailPost", new Vector3(x, rise * f, run * f), new Vector3(x, rise * f + 1.0f, run * f), .02f, steel); }
            }
            for (int k = 0; k < steps; k++) { float f = (k + .5f) / steps; LBox(s, "Tread", new Vector3(0, rise * f, run * f), new Vector3(width - .08f, .04f, run / steps + .04f), plate); }
            var ramp = new GameObject("StairRamp").transform; ramp.SetParent(s, false);
            ramp.localPosition = new Vector3(0, rise * .5f - .08f, run * .5f); ramp.localRotation = Quaternion.Euler(-Mathf.Atan2(rise, run) * Mathf.Rad2Deg, 0, 0);
            ramp.gameObject.AddComponent<BoxCollider>().size = new Vector3(width, .12f, Mathf.Sqrt(rise * rise + run * run));
            Undo.RegisterCreatedObjectUndo(ramp.gameObject, "Stair ramp");
            return s;
        }

        // A stair that gave way: the top flight hangs from the deck, the bottom flight lies broken on the floor.
        static Transform CollapsedStair(Transform parent, string name, Vector3 top, Vector3 dir, float rise, System.Random rng)
        {
            var s = Group(parent, name, top); s.rotation = Quaternion.LookRotation(-dir);
            Material dark = Mat("V4_Iron_Dark"), plate = DiamondPlateMaterial(), rust = Mat("V4_RustyMetal");
            float run = rise / Mathf.Tan(36 * Mathf.Deg2Rad);
            var upper = Group(s, "HangingFlight", top); upper.localRotation = Quaternion.Euler(58, 0, 4);
            foreach (float x in new[] { -.65f, .65f }) LRod(upper, "BentStringer", Vector3.zero, new Vector3(x * .9f, 0, 2.6f), .07f, dark);
            for (int k = 0; k < 7; k++) LBox(upper, "Tread", new Vector3(0, -.02f, .2f + k * .34f), new Vector3(1.2f, .04f, .26f), plate).localRotation = Quaternion.Euler(0, 0, (float)rng.NextDouble() * 10 - 5);
            var lowerAt = top + dir * (run * .62f); lowerAt.y = FloorAt(new Vector3(lowerAt.x, top.y - rise + .5f, lowerAt.z), .5f) + .05f;
            var lower = Group(s, "FallenFlight", lowerAt); lower.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(-14, 22, 9);
            foreach (float x in new[] { -.65f, .65f }) LRod(lower, "Stringer", new Vector3(x, 0, -1.5f), new Vector3(x, .9f, 1.6f), .07f, dark);
            for (int k = 0; k < 8; k++) LBox(lower, "Tread", new Vector3(0, .1f + k * .1f, -1.3f + k * .38f), new Vector3(1.2f, .04f, .26f), plate);
            lower.gameObject.AddComponent<BoxCollider>().size = new Vector3(1.4f, .8f, 3.2f);
            for (int k = 0; k < 6; k++) { var t = LBox(s, "LooseTread", s.InverseTransformPoint(lowerAt + new Vector3((float)rng.NextDouble() * 2 - 1, .02f, (float)rng.NextDouble() * 2 - 1)), new Vector3(1.2f, .04f, .26f), plate); t.localRotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360, (float)rng.NextDouble() * 20); }
            LRod(s, "SnappedHandrail", s.InverseTransformPoint(top + Vector3.up * 1.0f), s.InverseTransformPoint(top + Vector3.up * .2f + dir * 1.4f + Vector3.down * 2.2f), .025f, rust);
            return s;
        }

        // Collapsed steelwork: a truss segment fallen across the deck and over the edge, an I-beam leaning down to the
        // floor, a toppled frame and bent rails.
        static void CollapsedSteel(Transform parent, Vector3 deckPoint, Vector3 inward, Vector3 along, float deckY, float floorY, System.Random rng, string tag)
        {
            Material dark = Mat("V4_Iron_Dark"), rust = Mat("V4_RustyMetal"), steel = Mat("LA_SteelPlate");
            var g = Group(parent, tag + "_CollapsedSteel", deckPoint);
            // Truss lying diagonally, one end jammed against the wall, the other jutting past the broken rail.
            var truss = Group(g, "FallenTruss", deckPoint + Vector3.up * .25f); truss.rotation = Quaternion.LookRotation(along * .7f + inward * .7f) * Quaternion.Euler(-6, 0, 8);
            foreach (float y in new[] { 0f, .5f }) foreach (float x in new[] { -.25f, .25f }) LRod(truss, "TrussChord", new Vector3(x, y, -2.2f), new Vector3(x, y, 2.4f), .04f, rust);
            for (int k = 0; k < 6; k++) { float z = -2f + k * .8f; LRod(truss, "TrussWeb", new Vector3(-.25f, 0, z), new Vector3(.25f, .5f, z + .4f), .025f, dark); LRod(truss, "TrussWeb", new Vector3(.25f, 0, z), new Vector3(-.25f, .5f, z + .4f), .025f, dark); }
            // I-beam leaning from the deck edge down to the floor.
            var edge = deckPoint + inward * 2.0f + along * 3f; var floorEnd = edge + inward * 2.6f + along * .8f; floorEnd.y = floorY + .12f;
            var beam = Group(g, "LeaningIBeam", (edge + floorEnd) * .5f); beam.rotation = Quaternion.LookRotation(floorEnd - edge) * Quaternion.Euler(0, 0, 12);
            float bl = (floorEnd - edge).magnitude + .4f;
            LBox(beam, "Web", Vector3.zero, new Vector3(.03f, .3f, bl), steel, true); foreach (float y in new[] { -.15f, .15f }) LBox(beam, "Flange", new Vector3(0, y, 0), new Vector3(.18f, .025f, bl), steel);
            // Toppled scaffold frame and a stack of bent channel on the deck by the wall.
            var frame = Group(g, "ToppledFrame", deckPoint - along * 2.2f - inward * .6f + Vector3.up * .1f); frame.rotation = Quaternion.LookRotation(along) * Quaternion.Euler(0, 0, 80);
            foreach (float x in new[] { -.6f, .6f }) LRod(frame, "FrameTube", new Vector3(x, 0, -1f), new Vector3(x, 0, 1f), .03f, rust);
            for (int k = 0; k < 3; k++) LRod(frame, "FrameRung", new Vector3(-.6f, 0, -1f + k), new Vector3(.6f, 0, -1f + k), .025f, rust);
            LRod(frame, "FrameBrace", new Vector3(-.6f, 0, -1f), new Vector3(.6f, 0, 1f), .02f, dark);
            for (int k = 0; k < 5; k++) { var ch = LBox(g, "BentChannel", g.InverseTransformPoint(deckPoint - inward * .9f + along * (-.6f + k * .05f) + Vector3.up * (.05f + k * .07f)), new Vector3(.12f, .06f, 2.4f), dark); ch.rotation = Quaternion.LookRotation(along) * Quaternion.Euler(0, (float)rng.NextDouble() * 12 - 6, k * 3); }
        }

        // Pipes along the wall at deck level with brackets, dropping down past the deck edge here and there.
        static void WallPipes(Transform parent, Vector3 a, Vector3 b, Vector3 inward, float y, System.Random rng)
        {
            Material galv = LayoutMat("LA_GalvanisedPipe"), rust = Mat("V4_RustyMetal"), dark = Mat("V4_Iron_Dark");
            foreach (var (off, r, m) in new[] { (.22f, .09f, rust), (.42f, .06f, galv), (.6f, .12f, rust) })
            {
                var p0 = a + inward * .2f + Vector3.up * (y + off * 2); var p1 = b + inward * .2f + Vector3.up * (y + off * 2);
                Rod(parent, "WallPipe", p0, p1, r, m);
                for (float t = 0; t <= 1; t += 2.5f / (b - a).magnitude) Prim(parent, PrimitiveType.Cube, "PipeBracket", Vector3.Lerp(p0, p1, t) - inward * (r + .02f), new Vector3(.08f, r * 2.6f, .08f), dark);
            }
        }

        // ================= Logistics hall: L-shaped mezzanine (stage gear store + cargo bay) =================

        public static string LogisticsMezzanine() => RunPass("Logistics hall mezzanine", target =>
        {
            var hall = target.Find("S11_StageGear/LogisticsHall"); var hb = VisibleBounds(hall);
            Remove(target.Find("S11_StageGear"), "HallMezzanine");
            var g = Group(target.Find("S11_StageGear"), "HallMezzanine", hb.center); g.rotation = Quaternion.identity;
            var rng = new System.Random(4500);
            float zS = Physics.Raycast(new Vector3(-43, 6, 40), Vector3.back, out var hs, 30, ~0, QueryTriggerInteraction.Ignore) ? hs.point.z : 28.39f;
            float xE = Physics.Raycast(new Vector3(-43, 6, 40), Vector3.right, out var he, 30, ~0, QueryTriggerInteraction.Ignore) ? he.point.x : -26.99f;
            float xW = Physics.Raycast(new Vector3(-43, 6, 33), Vector3.left, out var hw, 30, ~0, QueryTriggerInteraction.Ignore) ? hw.point.x : -60.11f;
            float ceiling = Physics.Raycast(new Vector3(-43, 6, 40), Vector3.up, out var hc, 20, ~0, QueryTriggerInteraction.Ignore) ? hc.point.y : hb.max.y;
            const float W = 3f, deckY = 5.6f, eastEnd = 55f;
            // South wall (whole length) and east wall up to the dock shutter: an L the gear store and cargo bay share.
            var south = new DeckRun { a = new Vector3(xW, 0, zS), b = new Vector3(xE - W, 0, zS), inward = Vector3.forward, width = W };
            var east = new DeckRun { a = new Vector3(xE, 0, zS), b = new Vector3(xE, 0, eastEnd), inward = Vector3.left, width = W };
            // Stair arrives on the east arm about two thirds along; its rail segment is left open.
            float stairZ = 47.5f; int eastSegs = Mathf.Max(1, Mathf.RoundToInt((eastEnd - zS) / 2f)); int stairSeg = Mathf.FloorToInt((stairZ - zS) / ((eastEnd - zS) / eastSegs));
            var eastDecks = BuildDeck(g, east, deckY, ceiling, new HashSet<int> { stairSeg }, rng, "East");
            // The west end of the south arm has fallen in: those segments are not built, their remains lie below.
            int southSegs = Mathf.Max(1, Mathf.RoundToInt(((south.b - south.a).magnitude) / 2f)); int fallen = 3;
            var southRun = south; southRun.a = south.a + Vector3.right * ((south.b - south.a).magnitude / southSegs * fallen);
            var southDecks = BuildDeck(g, southRun, deckY, ceiling, new HashSet<int>(), rng, "South");
            // Corner plate joining the arms.
            var corner = Group(g, "CornerDeck", new Vector3(xE - W * .5f, deckY, zS + W * .5f));
            LBox(corner, "DeckPlate", new Vector3(0, -.03f, 0), new Vector3(W, .06f, W), DiamondPlateMaterial(), true);
            // Open ends: closed with a rail; the collapsed end gets a bent, broken rail and a hanging plate.
            var brokenEdge = southRun.a + Vector3.forward * W * .5f; brokenEdge.y = deckY;
            var barrier = Group(g, "BrokenEndRail", brokenEdge);
            LRod(barrier, "BentRail", new Vector3(0, 1.1f, -W * .5f), new Vector3(-.4f, .6f, W * .3f), .025f, Mat("LA_SteelPlate"));
            LRod(barrier, "BentRail", new Vector3(0, .55f, -W * .5f), new Vector3(-.6f, -.4f, W * .2f), .02f, Mat("LA_SteelPlate"));
            LBox(barrier, "EndBarrierCollider", new Vector3(0, .6f, 0), new Vector3(.08f, 1.2f, W), Mat("V4_Iron_Dark"), true).GetComponent<Renderer>().enabled = false;
            var hanging = Group(g, "HangingDeckPlate", brokenEdge); hanging.localRotation = Quaternion.Euler(0, 0, 62);
            LBox(hanging, "TornPlate", new Vector3(-1f, 0, 0), new Vector3(2f, .06f, W - .3f), DiamondPlateMaterial());
            float floorY = FloorAt(new Vector3(xW + 3, .5f, zS + 1.5f), .5f);
            for (int k = 0; k < 4; k++)
            {
                var p = new Vector3(xW + 1.2f + k * 1.4f, floorY, zS + .8f + (float)rng.NextDouble() * 1.8f);
                if (Physics.CheckBox(p + Vector3.up * .3f, new Vector3(.9f, .25f, .9f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;
                var d = LBox(g, "FallenDeckPlate", g.InverseTransformPoint(p + Vector3.up * (.1f + k * .02f)), new Vector3(2f, .06f, 2.6f), DiamondPlateMaterial(), true); d.rotation = Quaternion.Euler((float)rng.NextDouble() * 25, (float)rng.NextDouble() * 360, (float)rng.NextDouble() * 15);
            }
            // Usable stair onto the east arm, rising east from the gear-store floor.
            float stairFloor = FloorAt(new Vector3(xE - W - 6, .5f, stairZ), .5f);
            var stair = SteelStair(g, "MezzanineStair", new Vector3(xE - W - deckY / Mathf.Tan(36 * Mathf.Deg2Rad), stairFloor, stairZ), Vector3.right, deckY - stairFloor);
            // A second stair on the south arm gave way.
            CollapsedStair(g, "CollapsedStair", new Vector3(-46.5f, deckY, zS + W), Vector3.forward, deckY - floorY, rng);
            // Wrecked steel on the decks, pipes along the walls above them.
            CollapsedSteel(g, new Vector3(-40f, deckY, zS + .8f), Vector3.forward, Vector3.right, deckY, floorY, rng, "South");
            CollapsedSteel(g, new Vector3(xE - .8f, deckY, 36f), Vector3.left, Vector3.forward, deckY, floorY, rng, "East");
            WallPipes(g, new Vector3(xW + .5f, 0, zS), new Vector3(xE, 0, zS), Vector3.forward, deckY + 1.6f, rng);
            WallPipes(g, new Vector3(xE, 0, zS), new Vector3(xE, 0, eastEnd + 2), Vector3.left, deckY + 1.6f, rng);
            // Stored goods along the wall side of the deck, leaving a 2 m walkway clear.
            int goods = 0;
            foreach (var (run, decks) in new[] { (southRun, southDecks), (east, eastDecks) })
                foreach (var seg in decks.Where((_, i) => i % 3 == 1))
                {
                    var c = seg.position - run.inward * (W * .5f - .45f);
                    var crate = PutPrefab(g, rng.Next(2) == 0 ? Apo + "Props/SM_Prop_Crate_01" : Gen + "SM_Gen_Prop_Crate_01", c, (float)rng.NextDouble() * 360, .8f); goods++;
                    if (rng.Next(2) == 0) { PutPrefab(g, Apo + "Props/SM_Prop_Spool_01", c + run.b.normalized * 0 + (run.b - run.a).normalized * 1.0f, (float)rng.NextDouble() * 360, .7f); goods++; }
                }
            foreach (var r in g.GetComponentsInChildren<Renderer>()) if (r.bounds.size.magnitude < .35f) r.shadowCastingMode = ShadowCastingMode.Off;
            Physics.SyncTransforms();
            // Walk check along the deck centre line and up the stair.
            int blocked = 0, samples = 0;
            foreach (var (s0, s1) in new[] { (new Vector3(xE - W * .5f, deckY, zS + 1.5f), new Vector3(xE - W * .5f, deckY, eastEnd - .5f)), (new Vector3(southRun.a.x + 1, deckY, zS + 1.8f), new Vector3(xE - 1.5f, deckY, zS + 1.8f)) })
                for (int i = 0; i <= 30; i++) { var p = Vector3.Lerp(s0, s1, i / 30f); samples++; if (Physics.CheckCapsule(p + Vector3.up * .4f, p + Vector3.up * 1.7f, .28f, ~0, QueryTriggerInteraction.Ignore)) blocked++; }
            return $"Mezzanine at {deckY} m: south {southDecks.Count} + east {eastDecks.Count} segments ({fallen} fallen), stair at z {stairZ}, collapsed stair, {goods} goods; deck walk blocked {blocked}/{samples}";
        });

        // ================= Stage gear store: fuller floor =================

        public static string DensifyStageGearStore() => RunPass("Stage gear store density", target =>
        {
            var gear = target.Find("S11_StageGear"); var zone = gear.Find("Zone11");
            Remove(gear, "StoreClutter");
            var room = VisibleBounds(zone); room.Expand(new Vector3(-.2f, 0, -.2f)); room.max = new Vector3(room.max.x, room.max.y, Mathf.Min(room.max.z, 53.4f));
            float floor = FloorAt(new Vector3(room.center.x, room.min.y + .3f, room.center.z), .5f);
            var g = Group(gear, "StoreClutter", new Vector3(room.center.x, floor, room.center.z));
            var placer = new WallPlacer(room, floor, target.GetComponentsInChildren<Renderer>());
            foreach (var cand in zone.Cast<Transform>().Where(t => t.name.StartsWith("PerformanceObjectCandidate_") && !t.name.Contains("_Mark") && !t.name.Contains("_Cross")))
            { var b = VisibleBounds(cand); placer.KeepOut.Add((b.center, Mathf.Max(b.extents.x, b.extents.z) + 1.4f)); }
            // Walkways: from both west doors across the store, and the stair foot.
            for (float x = room.min.x; x < room.max.x; x += 1.5f) { placer.KeepOut.Add((new Vector3(x, 0, 44f), 1.3f)); }
            var stair = gear.Find("HallMezzanine/MezzanineStair"); if (stair != null) { var sb = VisibleBounds(stair); placer.KeepOut.Add((sb.center, Mathf.Max(sb.extents.x, sb.extents.z) + 1.2f)); placer.KeepOut.Add((stair.position - stair.forward * 1.2f, 1.6f)); }
            var rng = new System.Random(1111); float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var placed = new List<string>();
            bool FreeSpot(Vector3 near, Vector2 size, float h, out Vector3 at, out float yaw)
            {
                for (int tries = 0; tries < 400; tries++)
                {
                    var p = near + new Vector3(R(-7, 7), 0, R(-6, 6)); p.y = floor; yaw = R(0, 360);
                    if (!room.Contains(new Vector3(p.x, room.center.y, p.z))) continue;
                    if (placer.Free(p, size, h, Quaternion.Euler(0, yaw, 0))) { at = p; return true; }
                }
                at = default; yaw = 0; return false;
            }
            void Add(string label, Vector3 near, Vector2 size, float h, bool wall, Func<Vector3, float, Transform> build)
            {
                Vector3 at; float yaw; bool ok = wall ? placer.WallSpot(near, size, h, out at, out yaw) : FreeSpot(near, size, h, out at, out yaw);
                if (!ok) return; var t = build(at, yaw); placer.Take(t); placed.Add(label);
            }
            var c = room.center;
            Add("circus wagon", c + new Vector3(-6, 0, -2), new Vector2(3.8f, 2.0f), 2.8f, false, (at, yaw) => CircusWagon(g, at, yaw, rng));
            Add("scaffold tower", c + new Vector3(-10, 0, 4), new Vector2(1.6f, 1.6f), 4.2f, true, (at, yaw) => ScaffoldTower(g, at, yaw));
            for (int i = 0; i < 3; i++) Add("stacked stage modules", c + new Vector3(R(-12, 12), 0, R(-8, 6)), new Vector2(2.2f, 2.2f), 1.4f, false, (at, yaw) => { var s = Group(g, "StackedStageModules", at); s.rotation = Quaternion.Euler(0, yaw, 0); float y = 0; for (int k = 0; k < 3; k++) { var m = PutPrefab(s, Kit + "PerformanceArtKit/FloorModule", at + Vector3.up * y, yaw + R(-6, 6), 2f); y = VisibleBounds(m).max.y - floor; } return s; });
            for (int i = 0; i < 4; i++) Add("crate pile", c + new Vector3(R(-14, 14), 0, R(-10, 8)), new Vector2(1.8f, 1.6f), 2.0f, true, (at, yaw) => { var s = Group(g, "CratePile", at); s.rotation = Quaternion.Euler(0, yaw, 0); PutPrefab(s, Apo + "Props/SM_Prop_Crate_Large_01", at - s.right * .4f, yaw, 1.2f); PutPrefab(s, Gen + "SM_Gen_Prop_Crate_01", at + s.right * .5f, yaw + 10, .8f); var top = PutPrefab(s, Apo + "Props/SM_Prop_Crate_01", at - s.right * .4f, yaw + 25, .7f); var b = VisibleBounds(s.GetChild(0)); top.position += Vector3.up * (b.max.y - VisibleBounds(top).min.y); return s; });
            for (int i = 0; i < 3; i++) Add("rolled carpets", c + new Vector3(R(-12, 12), 0, R(-9, 7)), new Vector2(2.6f, 1.0f), .6f, false, (at, yaw) => { var s = Group(g, "RolledCarpets", at); s.rotation = Quaternion.Euler(0, yaw, 0); for (int k = 0; k < 3; k++) LShape(s, PrimitiveType.Cylinder, "CarpetRoll", new Vector3(0, .17f + (k == 2 ? .28f : 0), k == 2 ? 0 : (k - .5f) * .34f), new Vector3(.32f, 1.2f, .32f), k % 2 == 0 ? StageVelvet() : Mat("LA_CarpetWorn"), new Vector3(0, 0, 90)); return s; });
            Add("costume trunks", c + new Vector3(8, 0, -6), new Vector2(1.6f, 1.0f), 1.3f, true, (at, yaw) => { var s = Group(g, "TrunkStack", at); s.rotation = Quaternion.Euler(0, yaw, 0); var a = PutPrefab(s, Gen + "SM_Gen_Prop_Chest_01", at, yaw, .9f); var b = PutPrefab(s, Apo + "Props/SM_Prop_Luggage_Retro_01", at, yaw + 15, .7f); b.position += Vector3.up * (VisibleBounds(a).max.y - VisibleBounds(b).min.y); return s; });
            for (int i = 0; i < 2; i++) Add("barrels", c + new Vector3(R(-14, 14), 0, R(-9, 7)), new Vector2(1.4f, 1.4f), 1.2f, true, (at, yaw) => { var s = Group(g, "BarrelCluster", at); s.rotation = Quaternion.Euler(0, yaw, 0); for (int k = 0; k < 3; k++) PutPrefab(s, Gen + "SM_Gen_Prop_Barrel_Wood_01", at + s.right * ((k - 1) * .6f) + s.forward * (k == 1 ? .4f : 0), R(0, 360), .9f); return s; });
            Add("A-frame ladder", c + new Vector3(10, 0, 4), new Vector2(1.0f, 1.2f), 2.6f, true, (at, yaw) => { var s = Group(g, "AFrameLadder", at); s.rotation = Quaternion.Euler(0, yaw, 0); foreach (float z in new[] { -.4f, .4f }) foreach (float x in new[] { -.3f, .3f }) LRod(s, "LadderRail", new Vector3(x, 0, z), new Vector3(x, 2.4f, 0), .025f, Mat("V4_Wood_Old")); for (int k = 1; k < 7; k++) { float f = k / 7f; LRod(s, "LadderRung", new Vector3(-.3f, 2.4f * f, -.4f * (1 - f)), new Vector3(.3f, 2.4f * f, -.4f * (1 - f)), .018f, Mat("V4_Wood_Old")); } return s; });
            Add("ring curb sections", c + new Vector3(-12, 0, -4), new Vector2(2.4f, 1.0f), .8f, true, (at, yaw) => { var s = Group(g, "RingCurbStack", at); s.rotation = Quaternion.Euler(0, yaw, 0); for (int k = 0; k < 4; k++) LBox(s, "CurbSection", new Vector3(R(-.1f, .1f), .16f + k * .31f, 0), new Vector3(2.2f, .3f, .32f), Mat(k % 2 == 0 ? "V4_Harlequin" : "LA_TentRed")).localRotation = Quaternion.Euler(0, R(-6, 6), 0); return s; });
            Add("floodlight", c + new Vector3(4, 0, 6), new Vector2(1.2f, 1.2f), 2.6f, false, (at, yaw) => PutPrefab(g, Apo + "Props/SM_Prop_Floodlights_01", at, yaw, 2.4f));
            Add("tarp-covered pile", c + new Vector3(-3, 0, 6), new Vector2(2.4f, 2.0f), 1.6f, false, (at, yaw) => PutPrefab(g, Apo + "Props/SM_Prop_BarrelStack_01_Tarp", at, yaw, 2.2f));
            Add("cable spools", c + new Vector3(6, 0, -8), new Vector2(1.6f, 1.0f), 1.0f, true, (at, yaw) => { var s = Group(g, "CableSpools", at); s.rotation = Quaternion.Euler(0, yaw, 0); PutPrefab(s, Apo + "Props/SM_Prop_Spool_01", at - s.right * .4f, yaw, .9f); PutPrefab(s, Apo + "Props/SM_Prop_Spool_01", at + s.right * .5f, yaw + 40, .7f); return s; });
            return $"Stage gear store: {placed.Count} more groups ({string.Join(", ", placed.GroupBy(p => p).Select(k => k.Count() > 1 ? $"{k.Key}×{k.Count()}" : k.Key))})";
        });

        // Old show wagon with painted panels, one wheel off and leaning against it.
        static Transform CircusWagon(Transform parent, Vector3 at, float yaw, System.Random rng)
        {
            var w = Group(parent, "CircusWagon", at); w.rotation = Quaternion.Euler(0, yaw, 0);
            Material wood = Mat("V4_Wood_Old"), dark = Mat("V4_Iron_Dark"), gold = Mat("LA_Bronze"), stripes = Mat("V4_CircusStripes");
            const float L = 3.4f, Wd = 1.6f, H = 1.5f, axle = .55f;
            LBox(w, "WagonBody", new Vector3(0, axle + .25f + H * .5f, 0), new Vector3(L, H, Wd), wood, true);
            foreach (float s in new[] { -1f, 1f }) { LBox(w, "PaintedPanel", new Vector3(0, axle + .25f + H * .5f, s * (Wd * .5f + .01f)), new Vector3(L - .3f, H - .3f, .01f), stripes); LBox(w, "GoldTrim", new Vector3(0, axle + .25f + H - .05f, s * (Wd * .5f + .02f)), new Vector3(L, .08f, .02f), gold); }
            var roof = LBox(w, "CanvasRoof", new Vector3(0, axle + .25f + H + .12f, 0), new Vector3(L + .2f, .14f, Wd + .3f), Mat("LA_TentCream")); roof.localRotation = Quaternion.Euler(0, 0, 2);
            LBox(w, "Chassis", new Vector3(0, axle + .12f, 0), new Vector3(L - .2f, .14f, Wd - .3f), dark);
            LBox(w, "Shaft", new Vector3(L * .5f + .8f, axle, 0), new Vector3(1.8f, .06f, .08f), wood).localRotation = Quaternion.Euler(0, 0, -12);
            foreach (var (x, z, attached) in new[] { (-1.1f, 1f, true), (1.1f, 1f, true), (-1.1f, -1f, true), (1.1f, -1f, false) })
            {
                var wheelAt = attached ? new Vector3(x, axle, z * (Wd * .5f + .08f)) : new Vector3(x + .6f, axle - .05f, z * (Wd * .5f + .45f));
                var wheel = Group(w, attached ? "SpokedWheel" : "LooseWheel", w.TransformPoint(wheelAt)); wheel.localRotation = attached ? Quaternion.identity : Quaternion.Euler(0, 0, -14) * Quaternion.Euler(12, 0, 0);
                var rim = MeshObject(wheel, "Rim", wheel.position, SaveMesh(TorusMesh(axle - .03f, .04f, 24, 6)), wood); rim.transform.localRotation = Quaternion.identity;
                for (int k = 0; k < 10; k++) { float a = k * Mathf.PI / 5; LRod(wheel, "Spoke", Vector3.zero, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * (axle - .05f), .015f, wood); }
                LShape(wheel, PrimitiveType.Cylinder, "Hub", Vector3.zero, new Vector3(.14f, .06f, .14f), gold, new Vector3(90, 0, 0));
            }
            if (rng.Next(2) == 0) w.localRotation *= Quaternion.Euler(0, 0, 3); // sagging on the missing wheel
            return w;
        }

        static Transform ScaffoldTower(Transform parent, Vector3 at, float yaw)
        {
            var s = Group(parent, "ScaffoldTower", at); s.rotation = Quaternion.Euler(0, yaw, 0);
            Material rust = Mat("V4_RustyMetal"), wood = Mat("V4_Wood_Old");
            const float w = 1.4f, h = 4f;
            foreach (float x in new[] { -w * .5f, w * .5f }) foreach (float z in new[] { -w * .5f, w * .5f }) LRod(s, "Standard", new Vector3(x, 0, z), new Vector3(x, h, z), .03f, rust);
            for (float y = .8f; y <= h; y += 1.6f)
            {
                foreach (float z in new[] { -w * .5f, w * .5f }) LRod(s, "Ledger", new Vector3(-w * .5f, y, z), new Vector3(w * .5f, y, z), .025f, rust);
                foreach (float x in new[] { -w * .5f, w * .5f }) LRod(s, "Transom", new Vector3(x, y, -w * .5f), new Vector3(x, y, w * .5f), .025f, rust);
                LBox(s, "Plank", new Vector3(0, y + .04f, 0), new Vector3(w, .05f, w * .9f), wood, true);
            }
            LRod(s, "Brace", new Vector3(-w * .5f, 0, -w * .5f), new Vector3(w * .5f, h * .5f, -w * .5f), .02f, rust);
            LRod(s, "Brace", new Vector3(w * .5f, h * .5f, w * .5f), new Vector3(-w * .5f, h, w * .5f), .02f, rust);
            return s;
        }

        // ================= Lobby: mezzanine in an empty stretch of the hall wall =================

        public static string LobbyMezzanine() => RunPass("Lobby mezzanine in an empty wall arc", target =>
        {
            var lobby = target.Find("S01_Lobby"); Remove(lobby, "LobbyMezzanine");
            var floorB = lobby.Find("Frame8Lobby/RoundFloor").GetComponent<Renderer>().bounds; var hub = new Vector3(floorB.center.x, floorB.max.y, floorB.center.z);
            // Wall radius and openings, measured at walking height every 3°.
            // The round floor reaches the drum wall; a ray could stop on the existing stair instead.
            float wallR = floorB.extents.x - .05f;
            var solid = new bool[120];
            for (int i = 0; i < 120; i++)
            {
                var d = Quaternion.Euler(0, -i * 3, 0) * Vector3.right;
                bool low = Physics.Raycast(hub + Vector3.up * 1.5f + d * 6, d, out var h1, wallR + 2, ~0, QueryTriggerInteraction.Ignore) && (h1.point - hub).magnitude > wallR - 1.2f;
                bool high = Physics.Raycast(hub + Vector3.up * 5.5f + d * 6, d, out var h2, wallR + 2, ~0, QueryTriggerInteraction.Ignore) && (h2.point - hub).magnitude > wallR - 1.2f;
                solid[i] = low && high;
            }
            // Existing deck, lounge and wall clutter claim their angles too.
            const float W = 2.8f, deckY = 4.6f;
            float innerR = wallR - W;
            var taken = new List<Bounds>();
            foreach (var path in new[] { "Frame8RetainedFactory", "Frame8Lobby/AbandonedFactory", "AbandonedFactoryDressing", "ShabbyChristmasTent", "FallenSpawnLamp" }) { var t = lobby.Find(path); if (t != null) foreach (var r in t.GetComponentsInChildren<Renderer>()) if (r.bounds.size.y > 1.2f && r.bounds.size.magnitude < 12) taken.Add(r.bounds); }
            var lounge = target.Find("LG_Lounge"); if (lounge != null) taken.Add(VisibleBounds(lounge));
            for (int i = 0; i < 120; i++)
            {
                if (!solid[i]) continue; var d = Quaternion.Euler(0, -i * 3, 0) * Vector3.right;
                var band = new Bounds(hub + d * (wallR - W * .5f) + Vector3.up * (deckY + .5f), new Vector3(W, 2.2f, W));
                var foot = new Bounds(hub + d * (innerR - .8f) + Vector3.up * 1f, new Vector3(1.5f, 2f, 1.5f));
                if (taken.Any(b => b.Intersects(band) || b.Intersects(foot))) solid[i] = false;
            }
            // Longest clear arc (wrapping), trimmed by one step at each end.
            int bestStart = -1, bestLen = 0;
            for (int s = 0; s < 120; s++) { if (!solid[s] || solid[(s + 119) % 120]) continue; int l = 0; while (l < 120 && solid[(s + l) % 120]) l++; if (l > bestLen) { bestLen = l; bestStart = s; } }
            if (bestLen < 10) throw new InvalidOperationException("No free wall arc for a lobby mezzanine");
            int span = Mathf.Min(bestLen - 2, 20); float a0 = (bestStart + 1) * 3f, a1 = a0 + span * 3f;
            var g = Group(lobby, "LobbyMezzanine", hub); g.rotation = Quaternion.identity;
            var rng = new System.Random(77);
            float ceiling = Physics.Raycast(hub + Vector3.up * 2, Vector3.up, out var ch, 20, ~0, QueryTriggerInteraction.Ignore) ? ch.point.y : hub.y + 10;
            Vector3 Dir(float deg) => Quaternion.Euler(0, -deg, 0) * Vector3.right;
            // Chord segments following the wall; columns at the inner corners carry it (the dome wall is not loadbearing here).
            int segs = Mathf.Max(2, Mathf.RoundToInt((a1 - a0) / 8f)); float step = (a1 - a0) / segs;
            var decks = new List<Transform>();
            for (int i = 0; i < segs; i++)
            {
                float d0 = a0 + i * step, d1 = d0 + step;
                Vector3 w0 = hub + Dir(d0) * (wallR - .05f), w1 = hub + Dir(d1) * (wallR - .05f);
                var run = new DeckRun { a = new Vector3(w0.x, 0, w0.z), b = new Vector3(w1.x, 0, w1.z), inward = -Dir((d0 + d1) * .5f), width = W };
                bool collapsedEnd = i == segs - 1;
                if (collapsedEnd) continue; // the far end has fallen
                decks.AddRange(BuildDeck(g, run, deckY, ceiling, i == 0 ? new HashSet<int> { 0 } : new HashSet<int>(), rng, $"Lobby{i}"));
                var colAt = hub + Dir(d0) * (innerR + .15f); colAt.y = hub.y;
                Box(g, "Column", colAt + Vector3.up * (deckY * .5f - .1f), new Vector3(.22f, deckY - .2f, .22f), Mat("LA_SteelPlate"), true);
                Box(g, "BasePlate", colAt + Vector3.up * .02f, new Vector3(.45f, .04f, .45f), Mat("V4_Iron_Dark"));
            }
            // Stair rises along the wall line to a landing beside the first deck segment; step off the landing onto the deck.
            float stairRun = deckY / Mathf.Tan(36 * Mathf.Deg2Rad);
            var radial = Dir(a0 + step * .5f); var land = hub + radial * (innerR - .05f); land.y = hub.y;
            var tangent = Vector3.Cross(Vector3.up, -radial).normalized;
            var landingC = land - radial * .7f;
            Box(g, "StairLanding", landingC + Vector3.up * (deckY - .03f), new Vector3(1.4f, .06f, 1.4f), DiamondPlateMaterial(), true).rotation = Quaternion.LookRotation(radial);
            var stairTop = landingC + tangent * .7f; var stairFoot = stairTop + tangent * stairRun;
            SteelStair(g, "LobbyMezzanineStair", stairFoot, -tangent, deckY);
            // Broken far end: hanging plate, fallen steel, bent rails; pipes on the wall above the whole run.
            float aEnd = a1 - step; var endPoint = hub + Dir(aEnd) * (wallR - W * .5f); endPoint.y = hub.y + deckY;
            var hang = Group(g, "HangingEndPlate", endPoint); hang.rotation = Quaternion.LookRotation(Dir(aEnd)) * Quaternion.Euler(0, 90, 55);
            LBox(hang, "TornPlate", new Vector3(-1f, 0, 0), new Vector3(2.2f, .06f, W - .3f), DiamondPlateMaterial());
            LBox(g, "EndBarrierCollider", g.InverseTransformPoint(endPoint + Vector3.up * .6f), new Vector3(.08f, 1.2f, W), Mat("V4_Iron_Dark"), true).rotation = Quaternion.LookRotation(Dir(aEnd));
            var mid = hub + Dir((a0 + aEnd) * .5f) * (wallR - .9f); mid.y = hub.y + deckY;
            CollapsedSteel(g, mid, -Dir((a0 + aEnd) * .5f), Vector3.Cross(Vector3.up, -Dir((a0 + aEnd) * .5f)).normalized, deckY + hub.y, hub.y, rng, "Lobby");
            for (int i = 0; i < segs; i++)
            {
                Vector3 w0 = hub + Dir(a0 + i * step) * (wallR - .05f), w1 = hub + Dir(a0 + (i + 1) * step) * (wallR - .05f);
                WallPipes(g, new Vector3(w0.x, 0, w0.z), new Vector3(w1.x, 0, w1.z), -Dir(a0 + (i + .5f) * step), hub.y + deckY + 1.6f, rng);
            }
            // A fallen frame and rubble on the floor under the broken end.
            var below = hub + Dir(a1 - step * .5f) * (wallR - 1.6f); below.y = hub.y;
            if (!Physics.CheckBox(below + Vector3.up * .4f, new Vector3(1.2f, .35f, 1.2f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
            { var fallen = LBox(g, "FallenDeckPlate", g.InverseTransformPoint(below + Vector3.up * .15f), new Vector3(2f, .06f, 2.4f), DiamondPlateMaterial(), true); fallen.rotation = Quaternion.Euler(18, rng.Next(360), 9); }
            Physics.SyncTransforms();
            int blocked = 0, samples = 0;
            for (int i = 0; i <= 20; i++) { float deg = Mathf.Lerp(a0 + 1, aEnd - .5f, i / 20f); var p = hub + Dir(deg) * (wallR - W * .55f) + Vector3.up * deckY; samples++; if (Physics.CheckCapsule(p + Vector3.up * .4f, p + Vector3.up * 1.7f, .28f, ~0, QueryTriggerInteraction.Ignore)) blocked++; }
            return $"Lobby mezzanine: arc {a0:F0}°–{a1:F0}° (wall r {wallR:F1}), {decks.Count} deck segments at {deckY} m, stair + broken end; deck walk blocked {blocked}/{samples}";
        });
    }
}
