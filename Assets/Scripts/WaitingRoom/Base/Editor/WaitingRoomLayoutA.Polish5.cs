using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.Editor
{
    // Fifth round: things left hanging in the logistics hall settle onto what holds them, and the mezzanine carries on
    // round the cargo bay walls with a few broken stretches to jump.
    public static partial class WaitingRoomLayoutA
    {
        public static string SettleLogisticsHall() => RunPass("Logistics hall floating objects", target =>
        {
            var gear = target.Find("S11_StageGear"); var zone11 = gear.Find("Zone11"); var fixes = new List<string>();
            // Zone signs left in mid-air: mount them on the inner face of the loading-door headers, readable from the hall.
            var frontages = gear.Find("LogisticsFrontages");
            float wallX = Physics.Raycast(new Vector3(-50, 10, 43.6f), Vector3.left, out var wh, 20, ~0, QueryTriggerInteraction.Ignore) ? wh.point.x : -60.1f;
            foreach (var sign in frontages.Cast<Transform>().Where(t => t.name == "FacilitySign" && t.position.x > -58f).ToArray())
            {
                var tm = sign.GetComponent<TextMesh>(); float doorZ = tm != null && tm.text.StartsWith("11") ? 43.65f : 54.7f;
                Undo.RecordObject(sign, "Mount zone sign");
                sign.position = new Vector3(wallX + .04f, 9.7f, doorZ);
                sign.rotation = Quaternion.LookRotation(Vector3.left); // text faces the hall
                fixes.Add($"sign '{tm?.text}' → wall above door");
            }
            // Show wheels standing on edge: drop wheel, spokes and hub together until the rim touches the floor.
            var wheelParts = zone11.Cast<Transform>().Where(t => t.name.StartsWith("ShowWheel")).ToArray();
            foreach (var cluster in wheelParts.GroupBy(t => Mathf.Round(VisibleBounds(t).center.z)))
            {
                var parts = cluster.ToArray(); var rim = parts.FirstOrDefault(p => p.name == "ShowWheel"); if (rim == null) continue;
                var rb = VisibleBounds(rim); float floor = FloorAt(new Vector3(-43f, .5f, 44f), .5f); float gap = rb.min.y - floor;
                if (gap < .03f) continue;
                foreach (var p in parts) { Undo.RecordObject(p, "Settle wheel"); p.position += Vector3.down * gap; }
                fixes.Add($"show wheel z {cluster.Key} -{gap:F2}");
            }
            // Stacked trampolines (in the nested S11_StageGear group): the upper one rests on whatever is under it.
            var allR = gear.GetComponentsInChildren<Renderer>().Where(r => r.gameObject.activeInHierarchy).ToArray();
            foreach (var t in gear.GetComponentsInChildren<Transform>().Where(t => t.name == "CarryTrampoline").ToArray())
            {
                var b = VisibleBounds(t); float support = FloorAt(new Vector3(-43f, .5f, 44f), .5f);
                foreach (var r in allR)
                {
                    if (r.transform.IsChildOf(t)) continue; var o = r.bounds; if (o.max.y > b.min.y + .03f || o.size.y > 6) continue;
                    if (Mathf.Min(o.max.x, b.max.x) - Mathf.Max(o.min.x, b.min.x) > b.size.x * .3f && Mathf.Min(o.max.z, b.max.z) - Mathf.Max(o.min.z, b.min.z) > b.size.z * .3f) support = Mathf.Max(support, o.max.y);
                }
                float gap = b.min.y - support; if (gap < .03f) continue;
                Undo.RecordObject(t, "Settle trampoline"); t.position += Vector3.down * gap; fixes.Add($"trampoline -{gap:F2}");
            }
            // Floor stains and cracks hovering 8 cm up: lay them on the slab.
            int decals = 0;
            foreach (var d in gear.GetComponentsInChildren<Transform>().Concat(target.Find("S12_CargoBay").GetComponentsInChildren<Transform>()).Where(t => (t.name == "Stain" || t.name == "Crack" || t.name == "RustStain") && t.GetComponent<Renderer>() != null).ToArray())
            {
                var b = d.GetComponent<Renderer>().bounds; float floor = FloorAt(b.center, 1);
                if (b.size.y > .05f || b.min.y - floor < .03f || b.min.y - floor > .3f) continue;
                Undo.RecordObject(d, "Lay decal"); d.position += Vector3.down * (b.min.y - floor - .012f); decals++;
            }
            if (decals > 0) fixes.Add($"{decals} floor decals laid flat");
            return "Settled: " + string.Join("; ", fixes);
        });

        // The collapsed stair's bottom flight and loose treads were placed at deck height (the floor probe started at
        // the deck and never reached the slab). Drop them onto the hall floor.
        public static string DropCollapsedStairFlight() => RunPass("Drop collapsed stair flight", target =>
        {
            var stair = target.Find("S11_StageGear/HallMezzanine/CollapsedStair"); var fixes = new List<string>();
            foreach (var t in stair.Cast<Transform>().Where(t => t.name == "FallenFlight" || t.name == "LooseTread").ToArray())
            {
                var b = VisibleBounds(t); float floor = FloorAt(new Vector3(b.center.x, .5f, b.center.z), .5f); float gap = b.min.y - floor;
                if (gap < .03f) continue;
                Undo.RecordObject(t, "Drop stair piece"); t.position += Vector3.down * (gap - .01f); fixes.Add($"{t.name} -{gap:F2}");
            }
            return "Collapsed stair: " + (fixes.Count > 0 ? string.Join("; ", fixes) : "already on the floor");
        });

        // The Zone11 board hangs on the hall side of the sealed entry but its text faced the wall, so it read mirrored.
        public static string FlipZone11Board() => RunPass("Flip mirrored zone 11 board", target =>
        {
            var board = target.Find("S11_StageGear/Zone11/ZoneBoard"); var text = board.GetComponentInChildren<TextMesh>();
            if (text.transform.forward.x < .5f) return "Zone11 board already reads from the hall";
            // Turn only the lettering in place: it already sits on the hall side of the panel.
            Undo.RecordObject(text.transform, "Flip zone text");
            text.transform.RotateAround(text.GetComponent<Renderer>().bounds.center, Vector3.up, 180f);
            return $"Zone11 lettering turned to face the hall (text fwd {text.transform.forward})";
        });

        // Mezzanine continues round the cargo bay: east wall north of the dock, the whole north wall, and the west wall
        // down to the loading door. Three broken stretches of about 1.3 m have to be jumped (walk jump covers ~2.4 m).
        public static string CargoBayWalkway() => RunPass("Cargo bay walkway with jump gaps", target =>
        {
            var gear = target.Find("S11_StageGear"); var mezz = gear.Find("HallMezzanine") ?? throw new InvalidOperationException("Build the hall mezzanine first");
            Remove(mezz, "CargoWalkway");
            var g = Group(mezz, "CargoWalkway", mezz.position); g.rotation = Quaternion.identity;
            var rng = new System.Random(808);
            // Wall faces: probe from just inside each wall (from mid-hall the pallet racks answer first).
            float xE = Physics.Raycast(new Vector3(-29f, 6.5f, 72f), Vector3.right, out var he, 4, ~0, QueryTriggerInteraction.Ignore) ? he.point.x : -26.99f;
            float xW = Physics.Raycast(new Vector3(-58f, 6.5f, 70f), Vector3.left, out var hw, 4, ~0, QueryTriggerInteraction.Ignore) ? hw.point.x : -60.1f;
            float zN = Physics.Raycast(new Vector3(-43f, 6.5f, 78f), Vector3.forward, out var hn, 4, ~0, QueryTriggerInteraction.Ignore) ? hn.point.z : 80.1f;
            float ceiling = Physics.Raycast(new Vector3(-43, 6, 70), Vector3.up, out var hc, 20, ~0, QueryTriggerInteraction.Ignore) ? hc.point.y : 12f;
            const float W = 3f, deckY = 5.6f, eastStart = 55f, westEnd = 59.3f, gap = 1.3f;
            float floorY = FloorAt(new Vector3(-43, .5f, 70), .5f);
            // Runs with their jump gaps (start, end and gap centre along each wall).
            var runs = new List<(DeckRun run, string tag)>();
            void Split(Vector3 a, Vector3 b, Vector3 inward, float gapAt, string tag)
            {
                var dir = (b - a).normalized; var g0 = a + dir * (gapAt - gap * .5f); var g1 = a + dir * (gapAt + gap * .5f);
                runs.Add((new DeckRun { a = a, b = g0, inward = inward, width = W }, tag + "A"));
                runs.Add((new DeckRun { a = g1, b = b, inward = inward, width = W }, tag + "B"));
            }
            Split(new Vector3(xE, 0, eastStart), new Vector3(xE, 0, zN - W), Vector3.left, 8.6f, "CargoEast");
            Split(new Vector3(xE, 0, zN), new Vector3(xW, 0, zN), Vector3.back, 16.4f, "CargoNorth");
            Split(new Vector3(xW, 0, zN - W), new Vector3(xW, 0, westEnd), Vector3.right, 9.5f, "CargoWest");
            // The cargo zone board stood in the west run's path: hang it on the wall above the walkway, 60 % size.
            var board = target.Find("S12_CargoBay/Zone12/ZoneBoard");
            if (board != null)
            {
                var bb = VisibleBounds(board);
                if (bb.min.x < xW + W + .2f && bb.min.y < deckY + 2.2f)
                {
                    Undo.RecordObject(board, "Hang zone board above walkway");
                    board.localScale *= .6f; bb = VisibleBounds(board);
                    board.position += new Vector3(xW + .03f - bb.min.x, deckY + 3.2f - bb.min.y, 0);
                }
            }
            int segments = 0;
            foreach (var (run, tag) in runs) segments += BuildDeck(g, run, deckY, ceiling, new HashSet<int>(), rng, tag).Count;
            // Gap edges: torn plate ends, hazard paint, a sagging stub below and debris on the floor.
            int gaps = 0;
            for (int i = 0; i + 1 < runs.Count; i += 2)
            {
                var r = runs[i].run; var dir = (r.b - r.a).normalized;
                foreach (var (edge, sign) in new[] { (r.b, 1f), (runs[i + 1].run.a, -1f) })
                {
                    var c = edge + r.inward * (W * .5f); c.y = deckY;
                    var lip = Group(g, "GapEdge", c); lip.rotation = Quaternion.LookRotation(r.inward);
                    LBox(lip, "HazardLip", Vector3.zero, new Vector3(.08f, .012f, W - .2f), Mat("LA_HazardPaint")).position = c - dir * sign * .05f + Vector3.up * .006f;
                    var torn = LBox(lip, "TornPlateStub", Vector3.zero, new Vector3(.45f, .05f, W * .6f), DiamondPlateMaterial()); torn.position = c + dir * sign * .15f + Vector3.down * .12f + r.inward * (float)(rng.NextDouble() - .5f);
                    torn.rotation = Quaternion.LookRotation(r.inward) * Quaternion.Euler(0, 0, sign * (25 + (float)rng.NextDouble() * 20));
                }
                var mid = (r.b + runs[i + 1].run.a) * .5f + r.inward * (W * .5f);
                var down = new Vector3(mid.x, floorY, mid.z) + r.inward * .8f;
                if (!Physics.CheckBox(down + Vector3.up * .3f, new Vector3(.8f, .25f, .8f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                { var plate = LBox(g, "FallenGapPlate", g.InverseTransformPoint(down + Vector3.up * .08f), new Vector3(1.3f, .06f, 2.4f), DiamondPlateMaterial(), true); plate.rotation = Quaternion.LookRotation(r.inward) * Quaternion.Euler(14, rng.Next(360), 8); }
                gaps++;
            }
            WallPipes(g, new Vector3(xE, 0, eastStart), new Vector3(xE, 0, zN), Vector3.left, deckY + 1.6f, rng);
            WallPipes(g, new Vector3(xE, 0, zN), new Vector3(xW, 0, zN), Vector3.back, deckY + 1.6f, rng);
            WallPipes(g, new Vector3(xW, 0, zN), new Vector3(xW, 0, westEnd), Vector3.right, deckY + 1.6f, rng);
            // Some crates and drums along the wall side, out of the 2 m walkway.
            int goods = 0;
            foreach (var (run, tag) in runs)
            {
                float len = (run.b - run.a).magnitude; if (len < 4) continue;
                var at = Vector3.Lerp(run.a, run.b, .5f) + run.inward * .45f; at.y = deckY;
                PutPrefab(g, rng.Next(2) == 0 ? Apo + "Props/SM_Prop_Crate_01" : Gen + "SM_Gen_Prop_Barrel_Wood_01", at, rng.Next(360), .8f); goods++;
            }
            // Closed end where the west run stops short of the loading door.
            var endC = new Vector3(xW, deckY, westEnd) + Vector3.right * (W * .5f);
            var end = Group(g, "WestEndRail", endC);
            LBox(end, "EndRail", new Vector3(0, 1.1f, 0), new Vector3(W, .05f, .05f), Mat("LA_SteelPlate")); LBox(end, "EndMidRail", new Vector3(0, .55f, 0), new Vector3(W, .035f, .035f), Mat("LA_SteelPlate"));
            LBox(end, "EndRailCollider", new Vector3(0, .55f, 0), new Vector3(W, 1.1f, .06f), Mat("V4_Iron_Dark"), true).GetComponent<Renderer>().enabled = false;
            foreach (var r in g.GetComponentsInChildren<Renderer>()) if (r.bounds.size.magnitude < .35f) r.shadowCastingMode = ShadowCastingMode.Off;
            Physics.SyncTransforms();
            // Walk check on each run's centre line, and that every gap is open (nothing to stand on mid-gap).
            int blocked = 0, samples = 0, bridged = 0;
            foreach (var (run, _) in runs)
                for (int i = 0; i <= 12; i++) { var p = Vector3.Lerp(run.a, run.b, .05f + .9f * i / 12f) + run.inward * (W * .6f); p.y = deckY; samples++; if (Physics.CheckCapsule(p + Vector3.up * .4f, p + Vector3.up * 1.7f, .28f, ~0, QueryTriggerInteraction.Ignore)) blocked++; }
            for (int i = 0; i + 1 < runs.Count; i += 2) { var m = (runs[i].run.b + runs[i + 1].run.a) * .5f + runs[i].run.inward * (W * .6f); if (Physics.Raycast(new Vector3(m.x, deckY + .5f, m.z), Vector3.down, .8f, ~0, QueryTriggerInteraction.Ignore)) bridged++; }
            return $"Cargo walkway: {segments} deck segments along east/north/west walls, {gaps} jump gaps of {gap} m (bridged {bridged}), {goods} goods; walk blocked {blocked}/{samples}";
        });

        // ================= Freight that could not stand =================
        // Rack slots are about 2 m long but carried one 1 m pallet, so half of every load hung past it, and trunks sat
        // across two crates of different heights. Fill each slot with two pallets, then settle the freight bottom-up:
        // an item rests on the highest thing under it and its centre must lie over that support; a top item that
        // overhangs is centred on what it sits on, and one that cannot be is taken off the stack.

        static float Overlap(Bounds a, Bounds b)
            => Mathf.Max(0, Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x)) * Mathf.Max(0, Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z));

        static bool CentreOver(Bounds item, Bounds s, float margin = 0f)
            => item.center.x >= s.min.x - margin && item.center.x <= s.max.x + margin && item.center.z >= s.min.z - margin && item.center.z <= s.max.z + margin;

        sealed class Freight { public Transform Root; public List<Transform> Parts = new List<Transform>(); public bool Fixed; public Bounds B => Bounds(Parts); }

        static Bounds Bounds(List<Transform> parts)
        {
            var b = VisibleBounds(parts[0]); foreach (var p in parts.Skip(1)) b.Encapsulate(VisibleBounds(p)); return b;
        }

        // Settles `freight` bottom-up on the fixed supports and on each other. Returns (dropped, centred, removed).
        static (int dropped, int centred, int removed) SettleFreight(List<Freight> all)
        {
            int dropped = 0, centred = 0, removed = 0;
            foreach (var f in all.Where(f => !f.Fixed).OrderBy(f => f.B.min.y).ToArray())
            {
                var b = f.B; float area = Mathf.Max(b.size.x * b.size.z, 1e-4f);
                var below = all.Where(o => o != f && o.Root.gameObject.activeSelf).Select(o => (o, ob: o.B))
                    .Where(x => x.ob.max.y <= b.min.y + .06f && Overlap(b, x.ob) > area * .02f).ToList();
                if (below.Count == 0) continue;
                float h = below.Max(x => x.ob.max.y);
                var top = below.Where(x => x.ob.max.y > h - .04f).ToList();
                var span = top[0].ob; foreach (var x in top) span.Encapsulate(x.ob); // pieces resting at one height carry together
                bool stable = CentreOver(b, span) && top.Sum(x => Overlap(b, x.ob)) >= area * .5f;
                if (top.Any(x => x.o.Root.name.Contains("Sack"))) // a sagging sack pile has no flat top to carry anything
                {
                    foreach (var p in f.Parts) { Undo.RecordObject(p.gameObject, "Remove freight"); p.gameObject.SetActive(false); }
                    removed++; continue;
                }
                if (!stable)
                {
                    var s = top.OrderByDescending(x => Overlap(b, x.ob)).First();
                    bool canCentre = !s.o.Fixed && b.size.x <= s.ob.size.x * 1.2f && b.size.z <= s.ob.size.z * 1.2f;
                    if (canCentre)
                    {
                        var shift = new Vector3(s.ob.center.x - b.center.x, 0, s.ob.center.z - b.center.z);
                        var moved = new Bounds(b.center + shift + Vector3.up * (h - b.min.y), b.size);
                        moved.Expand(new Vector3(-.04f, -.04f, -.04f));
                        canCentre = !all.Any(o => o != f && o != s.o && o.Root.gameObject.activeSelf && !o.Fixed && o.B.Intersects(moved));
                        if (canCentre) { foreach (var p in f.Parts) { Undo.RecordObject(p, "Centre freight"); p.position += shift; } centred++; }
                    }
                    if (!canCentre && s.o.Fixed && top.Any(x => CentreOver(b, x.ob, .05f))) canCentre = true; // a load just past a pallet edge
                    if (!canCentre) { foreach (var p in f.Parts) Undo.RecordObject(p.gameObject, "Remove freight"); foreach (var p in f.Parts) p.gameObject.SetActive(false); removed++; continue; }
                    b = f.B;
                }
                float gap = b.min.y - h;
                if (Mathf.Abs(gap) > .01f) { foreach (var p in f.Parts) { Undo.RecordObject(p, "Settle freight"); p.position += Vector3.down * gap; } dropped++; }
            }
            return (dropped, centred, removed);
        }

        public static string SettleRackFreight() => RunPass("Rack freight that can stand", target =>
        {
            int pallets = 0; var tally = (dropped: 0, centred: 0, removed: 0);
            foreach (var rs in target.GetComponentsInChildren<Transform>().Where(t => t.name == "RackStock").ToArray())
            {
                var decks = rs.Cast<Transform>().Where(t => t.name == "RackDeck").Select(t => (t, b: VisibleBounds(t))).ToList();
                // One pallet per half slot along the rack.
                var run = decks[0].b; foreach (var d in decks) run.Encapsulate(d.b);
                bool alongX = run.size.x > run.size.z; // slots are nearly square, so read the direction from the whole rack
                foreach (var pal in rs.Cast<Transform>().Where(t => t.gameObject.activeSelf && t.name.StartsWith("SM_Prop_Pallet")).ToArray())
                {
                    var pb = VisibleBounds(pal);
                    var deck = decks.FirstOrDefault(d => CentreOver(pb, d.b) && Mathf.Abs(d.b.max.y - pb.min.y) < .1f); if (deck.t == null) continue;
                    float slot = alongX ? deck.b.size.x : deck.b.size.z, have = alongX ? pb.size.x : pb.size.z;
                    if (have > slot * .7f) continue;
                    var axis = alongX ? Vector3.right : Vector3.forward; float q = have * .5f + .01f; // the two pallets butt together
                    var along = Vector3.Scale(deck.b.center - pb.center, axis);
                    Undo.RecordObject(pal, "Pallet half slot"); pal.position += along - axis * q;
                    var src = PrefabUtility.GetCorrespondingObjectFromSource(pal.gameObject);
                    var twin = (src != null ? (GameObject)PrefabUtility.InstantiatePrefab(src, rs) : Object.Instantiate(pal.gameObject, rs)).transform;
                    twin.name = pal.name; twin.SetPositionAndRotation(pal.position + axis * (2 * q), pal.rotation); twin.localScale = pal.localScale;
                    Undo.RegisterCreatedObjectUndo(twin.gameObject, "Pallet twin"); pallets++;
                }
                var all = new List<Freight>();
                foreach (var t in rs.Cast<Transform>().Where(t => t.gameObject.activeSelf && t.name != "StepBeam" && t.name != "CartonTape" && t.name != "LoadStrap"))
                    all.Add(new Freight { Root = t, Parts = { t }, Fixed = t.name == "RackDeck" || t.name.StartsWith("SM_Prop_Pallet") });
                // Tape rides on its carton, straps on their wrapped load.
                foreach (var t in rs.Cast<Transform>().Where(t => t.gameObject.activeSelf && (t.name == "CartonTape" || t.name == "LoadStrap")))
                {
                    var host = all.Where(f => f.Root.name == (t.name == "CartonTape" ? "Carton" : "WrappedLoad")).OrderBy(f => (f.B.center - VisibleBounds(t).center).sqrMagnitude).FirstOrDefault();
                    host?.Parts.Add(t);
                }
                var r = SettleFreight(all); tally.dropped += r.dropped; tally.centred += r.centred; tally.removed += r.removed;
            }
            return $"Rack freight: {pallets} pallets added to fill slots; {tally.dropped} settled, {tally.centred} centred on their support, {tally.removed} taken off unstable stacks";
        });
    }
}
