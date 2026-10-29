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
    }
}
