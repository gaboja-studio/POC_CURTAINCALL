using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.Editor
{
    // Second polish round: stairwell risers, dormitory life, denser pipe zone, restroom breach, corridor velvet
    // curtains and the stage-gear store's circus equipment. Shells, doorways and service objects stay as they are.
    public static partial class WaitingRoomLayoutA
    {
        const string GenProps = Gen;

        static Material LayoutMat(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{AssetDir}/{name}.mat") ?? Mat(name);

        static Bounds VisibleBounds(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return new Bounds(t.position, Vector3.zero);
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
        }

        // Instantiates a prefab standing on `baseAt` (bottom centre), facing `yaw`, scaled so its largest side is `size` (0 = keep).
        static Transform PutPrefab(Transform parent, string path, Vector3 baseAt, float yaw, float size = 0, float scale = 1)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path + ".prefab") ?? throw new InvalidOperationException("Missing prefab " + path);
            var t = ((GameObject)PrefabUtility.InstantiatePrefab(asset, parent)).transform; Undo.RegisterCreatedObjectUndo(t.gameObject, "Place prop");
            t.rotation = Quaternion.Euler(0, yaw, 0); t.localScale = Vector3.one * scale;
            if (size > 0) { var b0 = VisibleBounds(t); t.localScale *= size / Mathf.Max(b0.size.x, Mathf.Max(b0.size.y, b0.size.z)); }
            var b = VisibleBounds(t); t.position += new Vector3(baseAt.x - b.center.x, baseAt.y - b.min.y, baseAt.z - b.center.z);
            return t;
        }

        // Finds free floor against a wall, near an anchor, for an item whose front (+Z) faces into the room.
        sealed class WallPlacer
        {
            readonly List<Bounds> blockers; readonly Bounds room; readonly float floor;
            public readonly List<(Vector3 c, float r)> KeepOut = new();
            public WallPlacer(Bounds room, float floorY, IEnumerable<Renderer> scope)
            {
                this.room = room; floor = floorY;
                blockers = scope.Where(r => r.enabled && r.gameObject.activeInHierarchy).Select(r => r.bounds)
                    .Where(b => b.size.x < 7 && b.size.z < 7 && b.max.y > floorY + .06f && b.min.y < floorY + 3.5f && room.Intersects(b)
                        && !(Mathf.Min(b.size.x, b.size.z) < .6f && b.size.y > 2.4f)).ToList();
            }
            static Vector3 Aabb(Vector3 half, Quaternion q) { var m = Matrix4x4.Rotate(q); return new Vector3(
                Mathf.Abs(m.m00) * half.x + Mathf.Abs(m.m01) * half.y + Mathf.Abs(m.m02) * half.z,
                Mathf.Abs(m.m10) * half.x + Mathf.Abs(m.m11) * half.y + Mathf.Abs(m.m12) * half.z,
                Mathf.Abs(m.m20) * half.x + Mathf.Abs(m.m21) * half.y + Mathf.Abs(m.m22) * half.z); }
            public bool Free(Vector3 c, Vector2 size, float h, Quaternion q)
            {
                var centre = new Vector3(c.x, floor + .08f + h * .5f, c.z);
                if (Physics.OverlapBox(centre, new Vector3(size.x * .5f + .03f, h * .5f, size.y * .5f + .02f), q, ~0, QueryTriggerInteraction.Ignore).Length > 0) return false;
                var box = new Bounds(centre, Aabb(new Vector3(size.x * .5f + .1f, h * .5f, size.y * .5f), q) * 2);
                if (blockers.Any(b => b.Intersects(box))) return false;
                return !KeepOut.Any(k => new Vector2(c.x - k.c.x, c.z - k.c.z).magnitude < k.r + Mathf.Max(size.x, size.y) * .5f);
            }
            public void Take(Transform t) { if (t != null && t.GetComponentsInChildren<Renderer>().Length > 0) blockers.Add(VisibleBounds(t)); }
            public void Block(Bounds b) => blockers.Add(b);
            public bool WallSpot(Vector3 anchor, Vector2 size, float h, out Vector3 pos, out float yaw, float gapFromWall = .06f)
            {
                pos = default; yaw = 0; float best = float.MaxValue; bool found = false;
                var dirs = new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
                for (float x = room.min.x + .4f; x < room.max.x - .4f; x += .5f)
                for (float z = room.min.z + .4f; z < room.max.z - .4f; z += .5f)
                {
                    var p = new Vector3(x, floor + 1.2f, z);
                    foreach (var d in dirs)
                    {
                        float reach = size.y * .5f + .45f;
                        if (!Physics.Raycast(p, d, out var hit, reach, ~0, QueryTriggerInteraction.Ignore) || Mathf.Abs(hit.normal.y) > .2f) continue;
                        var n = new Vector3(hit.normal.x, 0, hit.normal.z).normalized; var tangent = Vector3.Cross(Vector3.up, n);
                        // The wall must run on past both sides of the item: no doorway beside it.
                        bool solid = true;
                        foreach (float s in new[] { -1f, 1f })
                        {
                            var q = hit.point + n * .3f + tangent * s * (size.x * .5f + 1.3f) + Vector3.up * 0;
                            if (!Physics.Raycast(q, -n, out var h2, .6f, ~0, QueryTriggerInteraction.Ignore)) { solid = false; break; }
                        }
                        if (!solid) continue;
                        var c = new Vector3(hit.point.x, floor, hit.point.z) + n * (size.y * .5f + gapFromWall);
                        var rot = Quaternion.LookRotation(n);
                        float score = (c - anchor).sqrMagnitude;
                        if (score >= best || !Free(c, size, h, rot)) continue;
                        best = score; pos = c; yaw = rot.eulerAngles.y; found = true;
                    }
                }
                return found;
            }
        }

        static float FloorAt(Vector3 p, float from = 3f)
            => Physics.Raycast(p + Vector3.up * from, Vector3.down, out var h, from + 3, ~0, QueryTriggerInteraction.Ignore) ? h.point.y : p.y;

        // ================= 1. Stairwell risers through the mid landing =================

        public static string FixStairwellRisers() => RunPass("Stairwell risers clear of landing", target =>
        {
            var st = target.Find("ST_Stairwell"); var landing = st.Find("MidLanding");
            var lb = landing.GetComponent<Renderer>().bounds;
            var risers = st.Find("EnvironmentDetail/Stairwell");
            if (!Physics.Raycast(lb.center + Vector3.up, Vector3.right, out var wall, 6, ~0, QueryTriggerInteraction.Ignore)) throw new InvalidOperationException("No shaft wall");
            if (lb.max.x < wall.point.x - .2f) throw new InvalidOperationException("Landing already trimmed");
            // The risers keep to a 0.32 m chase against the wall; the landing stops at its edge.
            const float chase = .32f; float newMax = wall.point.x - chase;
            foreach (var t in st.Cast<Transform>().Where(t => t.name == "MidLanding" || (t.GetComponent<Collider>() != null && t.GetComponent<Renderer>() == null && t.GetComponent<Collider>().bounds.Intersects(lb) && Mathf.Abs(t.GetComponent<Collider>().bounds.center.y - lb.center.y) < .3f)))
            {
                var b = t.GetComponent<Renderer>() != null ? t.GetComponent<Renderer>().bounds : t.GetComponent<Collider>().bounds;
                if (b.max.x <= newMax) continue;
                Undo.RecordObject(t, "Trim landing for riser chase");
                int axis = Enumerable.Range(0, 3).OrderByDescending(i => Mathf.Abs(t.TransformDirection(i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward).x)).First();
                var s = t.localScale; float width = b.size.x, keep = newMax - b.min.x;
                s[axis] *= keep / width; t.localScale = s; t.position += Vector3.right * ((keep - width) * .5f);
            }
            // Pipes and their clamps move into the chase, hugging the wall.
            var pipes = risers.Cast<Transform>().Where(t => t.name == "ShaftRiser").ToArray();
            foreach (var p in risers.Cast<Transform>().ToArray())
            {
                if (p.name != "ShaftRiser" && p.name != "RiserClamp" && p.name != "LandingBranch") continue;
                var b = p.GetComponent<Renderer>().bounds; float r = p.name == "RiserClamp" ? .06f : Mathf.Min(b.size.x, b.size.y) * .5f;
                Undo.RecordObject(p, "Move riser into chase");
                p.position += Vector3.right * (wall.point.x - .03f - r - b.center.x);
            }
            // Toe angle along the trimmed edge so the cut reads as built.
            Remove(st, "RiserChaseEdge");
            var edge = Group(st, "RiserChaseEdge", new Vector3(newMax, lb.max.y, lb.center.z));
            Box(edge, "ChaseToeAngle", new Vector3(newMax - .02f, lb.max.y + .05f, lb.center.z), new Vector3(.04f, .1f, lb.size.z), Mat("V4_Iron_Dark"), true);
            Box(edge, "ChaseNosing", new Vector3(newMax - .03f, lb.max.y + .005f, lb.center.z), new Vector3(.06f, .01f, lb.size.z), Mat("LA_HazardPaint"));
            Physics.SyncTransforms();
            int through = pipes.Count(p => { var pb = p.GetComponent<Renderer>().bounds; return pb.min.x < newMax - .01f; });
            return $"Landing trimmed to x {newMax:F2} (wall {wall.point.x:F2}); {pipes.Length} risers in the chase; still crossing: {through}";
        });

        // ================= 2. Dormitory: floating boxes and lived-in furniture =================

        public static string FixDormFloatingProps() => RunPass("Dormitory floating boxes settle", target =>
        {
            var dorm = target.Find("S02_Dormitory/Dormitory");
            var all = target.Find("S02_Dormitory").GetComponentsInChildren<Renderer>().Where(r => r.gameObject.activeInHierarchy).ToArray();
            var boxy = new System.Text.RegularExpressions.Regex("(?i)box|crate|carton|case|trunk|linen|parcel");
            var moved = new List<string>();
            // Only loose boxes: clothes on bunks, vents and cabinet parts are meant to sit where they are.
            foreach (var item in dorm.GetComponentsInChildren<Transform>().Where(t => boxy.IsMatch(t.name) && t.GetComponentsInChildren<Renderer>().Length > 0 && !t.GetComponentsInParent<Transform>().Skip(1).Any(p => boxy.IsMatch(p.name))).ToArray())
            {
                var b = VisibleBounds(item); if (b.min.y < .03f || b.min.y > 2.2f) continue;
                float support = 0;
                foreach (var r in all)
                {
                    if (r.transform.IsChildOf(item)) continue;
                    var o = r.bounds; if (o.max.y > b.min.y + .03f || o.size.y > 4) continue;
                    if (o.min.x < b.max.x && o.max.x > b.min.x && o.min.z < b.max.z && o.max.z > b.min.z) support = Mathf.Max(support, o.max.y);
                }
                float gap = b.min.y - support;
                if (gap < .04f) continue;
                Undo.RecordObject(item, "Settle floating box"); item.position += Vector3.down * gap;
                moved.Add($"{item.name} -{gap:F2}m");
            }
            return "Settled: " + (moved.Count == 0 ? "none" : string.Join(", ", moved));
        });

        public static string FurnishDormLife() => RunPass("Dormitory wardrobes, desks and chairs", target =>
        {
            var root = target.Find("S02_Dormitory"); var dorm = root.Find("Dormitory");
            Remove(dorm, "LivedIn");
            var room = VisibleBounds(root.Find("Skin_Dormitory")); room.Expand(new Vector3(-.3f, 0, -.3f));
            var g = Group(dorm, "LivedIn", room.center);
            var placer = new WallPlacer(room, 0, target.GetComponentsInChildren<Renderer>());
            var beds = dorm.Find("Beds").Cast<Transform>().Where(t => t.name.StartsWith("Bed_")).Select(t => VisibleBounds(t).center).ToArray();
            var rng = new System.Random(2026); float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            int wardrobes = 0, desks = 0;
            // Wardrobes beside bunks: one stands open with costumes hanging inside.
            for (int i = 0; i < Mathf.Min(3, beds.Length); i++)
            {
                if (!placer.WallSpot(beds[i * Mathf.Max(1, beds.Length / 3)], new Vector2(1.05f, .62f), 2.15f, out var at, out var yaw)) continue;
                var w = Wardrobe(g, at, yaw, i == 0, rng); placer.Take(w); wardrobes++;
            }
            // Writing desks with a chair pulled out, lamp, books, mug, letters and a small photo.
            string[] chairPaths = { Apo + "Props/SM_Prop_Study_Chair_01", Apo + "Props/SM_Prop_Chair_Metal_01" };
            for (int i = 0; i < 2; i++)
            {
                var anchor = beds.Length > 1 ? beds[(i * 2 + 1) % beds.Length] : room.center;
                if (!placer.WallSpot(anchor, new Vector2(1.35f, 1.5f), 1.6f, out var at, out var yaw)) continue;
                var set = Group(g, "WritingDesk", at); set.rotation = Quaternion.Euler(0, yaw, 0);
                var deskAt = at - set.forward * .45f;
                var desk = WritingDeskModel(set, deskAt, yaw, i == 1);
                var db = VisibleBounds(desk);
                // Chair pulled out from the desk, turned toward it (the chair models face their local -Z).
                var chair = PutPrefab(set, chairPaths[i], deskAt + set.forward * .75f + set.right * R(-.15f, .15f), yaw + R(-25, 25), 0, 1);
                var cb = VisibleBounds(chair); if (cb.size.y > 1.3f) { chair.localScale *= 1.05f / cb.size.y; cb = VisibleBounds(chair); chair.position += Vector3.up * (0 - cb.min.y); }
                float top = db.max.y; var c = new Vector3(db.center.x, top, db.center.z);
                PutPrefab(set, Apo + "Props/SM_Prop_Study_Lamp_01", c - set.right * .42f + set.forward * .05f, yaw + 160, .45f);
                PutPrefab(set, GenProps + "SM_Gen_Prop_Mug_01", c + set.right * .35f + set.forward * .15f, R(0, 360), .1f);
                var book = PutPrefab(set, Apo + "Item/SM_Item_Book_Open_01", c + set.forward * .12f, yaw + 180 + R(-10, 10), .32f);
                for (int k = 0; k < 4; k++) { var b = LBox(set, "StackedBook", set.InverseTransformPoint(c + set.right * .12f - set.forward * .18f) + Vector3.up * (.025f + k * .045f), new Vector3(.24f, .04f, .17f), Mat(k % 2 == 0 ? "LA_TentRed" : "LA_Cardboard")); b.localRotation = Quaternion.Euler(0, R(-12, 12), 0); }
                for (int k = 0; k < 3; k++) { var l = LBox(set, "Letter", set.InverseTransformPoint(c + set.right * R(-.1f, .25f) + set.forward * R(-.05f, .2f)) + Vector3.up * (.004f + k * .002f), new Vector3(.2f, .002f, .14f), Mat("LA_Paper")); l.localRotation = Quaternion.Euler(0, R(0, 360), 0); }
                var frame = Group(set, "PhotoFrame", c - set.right * .2f - set.forward * .22f + Vector3.up * .1f); frame.rotation = Quaternion.Euler(-12, yaw, 0);
                LBox(frame, "Frame", Vector3.zero, new Vector3(.16f, .2f, .02f), Mat("LA_Bronze")); LBox(frame, "Photo", new Vector3(0, 0, .011f), new Vector3(.12f, .15f, .002f), Mat("LA_Paper"));
                // A cardigan over the chair back.
                var cardigan = LBox(chair, "CardiganOverChair", chair.InverseTransformPoint(VisibleBounds(chair).center + Vector3.up * VisibleBounds(chair).extents.y * .7f) , new Vector3(.45f, .25f, .06f), LayoutMat("LA_RetiredCostume_1"));
                cardigan.localScale = Vector3.Scale(cardigan.localScale, new Vector3(1 / chair.lossyScale.x, 1 / chair.lossyScale.y, 1 / chair.lossyScale.z));
                placer.Take(set); desks++;
            }
            // Shoes by the bunks and a laundry bag, so the room is lived in rather than staged.
            int small = 0;
            foreach (var bed in beds.Take(4))
            {
                var p = new Vector3(bed.x + R(-.6f, .6f), 0, bed.z + R(-1.1f, 1.1f));
                if (!placer.Free(p, new Vector2(.4f, .35f), .2f, Quaternion.identity)) continue;
                var pair = Group(g, "Shoes", p); pair.rotation = Quaternion.Euler(0, R(0, 360), 0);
                foreach (float dx in new[] { -.07f, .07f }) { var s = LBox(pair, "Shoe", new Vector3(dx, .045f, R(-.03f, .03f)), new Vector3(.1f, .09f, .27f), Mat("V4_Black")); s.localRotation = Quaternion.Euler(0, R(-15, 15), 0); }
                small++;
            }
            if (placer.WallSpot(room.center, new Vector2(.6f, .6f), .7f, out var bagAt, out var bagYaw))
            { var bag = PutPrefab(g, "Assets/_ThirdParty/Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Sack_01", bagAt, bagYaw, .7f); bag.name = "LaundrySack"; small++; }
            return $"Dormitory: {wardrobes} wardrobes, {desks} desks with chairs, {small} small items";
        });

        // Plain writing desk: top, apron, legs and a three-drawer pedestal (one drawer left open).
        static Transform WritingDeskModel(Transform parent, Vector3 at, float yaw, bool mirrored)
        {
            var d = Group(parent, "WritingDeskModel", at); d.rotation = Quaternion.Euler(0, yaw, 0);
            Material wood = Mat("V4_Wood_Old"), dark = Mat("V4_Iron_Dark");
            const float W = 1.2f, D = .6f, H = .76f; float ped = mirrored ? -W * .5f + .22f : W * .5f - .22f;
            LBox(d, "DeskTop", new Vector3(0, H - .02f, 0), new Vector3(W, .04f, D), wood, true);
            LBox(d, "Apron", new Vector3(0, H - .1f, -D * .5f + .03f), new Vector3(W - .08f, .12f, .02f), wood);
            foreach (float x in new[] { -W * .5f + .04f, W * .5f - .04f }) foreach (float z in new[] { -D * .5f + .04f, D * .5f - .04f })
                if (Mathf.Abs(x - ped) > .2f) LBox(d, "Leg", new Vector3(x, (H - .04f) * .5f, z), new Vector3(.045f, H - .04f, .045f), wood);
            LBox(d, "Pedestal", new Vector3(ped, (H - .04f) * .5f, 0), new Vector3(.42f, H - .04f, D - .04f), wood, true);
            for (int k = 0; k < 3; k++)
            {
                float y = .14f + k * .21f, pull = k == 1 ? .18f : 0;
                LBox(d, "DrawerFront", new Vector3(ped, y, D * .5f - .01f + pull), new Vector3(.38f, .18f, .02f), wood);
                if (pull > 0) LBox(d, "DrawerBox", new Vector3(ped, y - .02f, D * .5f - .02f + pull * .5f), new Vector3(.34f, .12f, pull + .1f), wood);
                LShape(d, PrimitiveType.Sphere, "DrawerKnob", new Vector3(ped, y, D * .5f + .015f + pull), Vector3.one * .03f, Mat("LA_Bronze"));
            }
            LBox(d, "Footrest", new Vector3(mirrored ? .2f : -.2f, .12f, -D * .5f + .12f), new Vector3(.5f, .025f, .04f), dark);
            return d;
        }

        static Transform Wardrobe(Transform parent, Vector3 at, float yaw, bool open, System.Random rng)
        {
            var w = Group(parent, "Wardrobe", at); w.rotation = Quaternion.Euler(0, yaw, 0);
            Material wood = Mat("V4_Wood_Old"), dark = Mat("V4_Iron_Dark");
            const float W = 1.0f, H = 2.05f, D = .58f, T = .03f;
            LBox(w, "Back", new Vector3(0, H * .5f + .08f, -D * .5f + T * .5f), new Vector3(W, H, T), wood, true);
            foreach (float s in new[] { -1f, 1f }) LBox(w, "Side", new Vector3(s * (W * .5f - T * .5f), H * .5f + .08f, 0), new Vector3(T, H, D), wood, true);
            LBox(w, "Top", new Vector3(0, H + .08f - T * .5f, 0), new Vector3(W, T, D), wood);
            LBox(w, "Bottom", new Vector3(0, .09f, 0), new Vector3(W, T, D), wood);
            LBox(w, "Plinth", new Vector3(0, .04f, .02f), new Vector3(W - .04f, .08f, D - .06f), dark);
            LBox(w, "Crown", new Vector3(0, H + .12f, .01f), new Vector3(W + .06f, .07f, D + .04f), wood);
            LRod(w, "HangingRail", new Vector3(-W * .5f + .05f, H - .15f, 0), new Vector3(W * .5f - .05f, H - .15f, 0), .012f, Mat("LA_Bronze"));
            string[] cloth = { "LA_RetiredCostume_0", "LA_RetiredCostume_2", "LA_TentRed", "LA_RetiredCostume_3", "LA_TentCream" };
            for (int i = 0; i < 5; i++)
            {
                float x = -.36f + i * .18f, len = .7f + (float)rng.NextDouble() * .5f;
                var g = LBox(w, "HangingGarment", new Vector3(x, H - .2f - len * .5f, 0), new Vector3(.06f, len, .42f), LayoutMat(cloth[i]));
                g.localRotation = Quaternion.Euler(0, (float)rng.NextDouble() * 10 - 5, 0);
                LRod(w, "Hanger", new Vector3(x, H - .17f, -.18f), new Vector3(x, H - .17f, .18f), .006f, dark);
            }
            // Two doors; the left one hangs open on its hinge, the right carries a mirror.
            for (int d = 0; d < 2; d++)
            {
                float hingeX = d == 0 ? -W * .5f : W * .5f, sign = d == 0 ? 1 : -1;
                var hinge = Group(w, d == 0 ? "DoorHingeL" : "DoorHingeR", w.TransformPoint(new Vector3(hingeX, 0, D * .5f)));
                hinge.localRotation = Quaternion.Euler(0, d == 0 && open ? -105 : 0, 0);
                LBox(hinge, "Door", new Vector3(sign * W * .25f, H * .5f + .08f, .012f), new Vector3(W * .5f - .01f, H - .02f, .024f), wood);
                LBox(hinge, "DoorPanel", new Vector3(sign * W * .25f, H * .5f + .08f, .026f), new Vector3(W * .5f - .12f, H - .3f, .006f), Mat("V4_Wood_Old"));
                if (d == 1) LBox(hinge, "DoorMirror", new Vector3(sign * W * .25f, H * .55f + .08f, .03f), new Vector3(W * .5f - .2f, H * .55f, .004f), Mat("LA_Mirror"));
                LShape(hinge, PrimitiveType.Sphere, "Knob", new Vector3(sign * (W * .5f - .06f), H * .5f, .045f), Vector3.one * .035f, Mat("LA_Bronze"));
            }
            // Hatbox and a battered case on top.
            LShape(w, PrimitiveType.Cylinder, "HatBox", new Vector3(-.2f, H + .24f, 0), new Vector3(.34f, .1f, .34f), Mat("LA_TentCream"));
            LBox(w, "CaseOnTop", new Vector3(.22f, H + .25f, -.02f), new Vector3(.45f, .2f, .32f), Mat("LA_Cardboard")).localRotation = Quaternion.Euler(0, 8, 0);
            return w;
        }

        // ================= 3. Denser pipe zone =================

        public static string DensifyPipeZone() => RunPass("Pipe zone pipework", target =>
        {
            var zone = target.Find("P05_PipeZone"); Remove(zone, "DensePipework");
            var shell = VisibleBounds(zone.Find("Shell"));
            float floor = FloorAt(shell.center, 2), ceiling = shell.max.y;
            // Walls either side of the long (x) axis, measured at walking height and up high.
            float midZ = shell.center.z;
            Physics.Raycast(new Vector3(shell.center.x, floor + 5, midZ), Vector3.back, out var s5, 12, ~0, QueryTriggerInteraction.Ignore);
            Physics.Raycast(new Vector3(shell.center.x, floor + 5, midZ), Vector3.forward, out var n5, 12, ~0, QueryTriggerInteraction.Ignore);
            float zS = s5.collider ? s5.point.z : shell.min.z + .5f, zN = n5.collider ? n5.point.z : shell.max.z - .5f;
            var g = Group(zone, "DensePipework", shell.center);
            var existing = zone.GetComponentsInChildren<Renderer>().Where(r => r.gameObject.activeInHierarchy && !r.transform.IsChildOf(g) && r.bounds.size.magnitude < 14 && r.bounds.max.y > floor + 2.4f).Select(r => r.bounds).ToList();
            var rng = new System.Random(505); float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Material galv = LayoutMat("LA_GalvanisedPipe"), rust = Mat("V4_RustyMetal"), dark = Mat("V4_Iron_Dark"), bronze = Mat("LA_Bronze");
            var lagging = Lit("LA_PipeLagging", new Color(.62f, .6f, .55f), .05f, FactoryTex("Concrete/T_Concrete_BC.TGA"));
            int runs = 0, valves = 0;
            bool Clear(Vector3 a, Vector3 b, float r)
            {
                for (int i = 0; i <= 12; i++) { var p = Vector3.Lerp(a, b, i / 12f); if (existing.Any(e => { var x = e; x.Expand(r * 2 + .1f); return x.Contains(p); })) return false; }
                return true;
            }
            void Pipe(Vector3 a, Vector3 b, float r, Material m, bool flanges = true)
            {
                Rod(g, "Pipe", a, b, r, m); existing.Add(new Bounds((a + b) * .5f, new Vector3(Mathf.Abs(a.x - b.x) + r * 2, Mathf.Abs(a.y - b.y) + r * 2, Mathf.Abs(a.z - b.z) + r * 2)));
                if (!flanges) return;
                float len = (b - a).magnitude; var dir = (b - a) / len;
                for (float d = 1.8f; d < len - .3f; d += 2.4f) { var f = Prim(g, PrimitiveType.Cylinder, "Flange", a + dir * d, new Vector3(r * 2.7f, .03f, r * 2.7f), dark); f.rotation = Quaternion.FromToRotation(Vector3.up, dir); }
            }
            void Elbow(Vector3 at, float r, Material m) => Prim(g, PrimitiveType.Sphere, "Elbow", at, Vector3.one * r * 2.25f, m);
            void Hanger(Vector3 at, float r) { Rod(g, "HangerRod", at + Vector3.up * r, new Vector3(at.x, ceiling - .05f, at.z), .012f, dark); var c = Prim(g, PrimitiveType.Cube, "HangerClamp", at + Vector3.up * (r + .02f), new Vector3(r * 2.4f, .04f, .06f), dark); c.rotation = Quaternion.LookRotation(Vector3.forward); }
            // Long runs near the ceiling along the zone, at staggered heights and sizes.
            float[] heights = { ceiling - .6f, ceiling - 1.2f, ceiling - 1.9f, ceiling - 2.5f };
            for (int i = 0; i < 7; i++)
            {
                float r = new[] { .09f, .14f, .06f, .2f, .11f, .07f, .16f }[i];
                float z = Mathf.Lerp(zS + .6f, zN - .6f, (i + .5f) / 7f) + R(-.2f, .2f); float y = heights[i % heights.Length] + R(-.15f, .15f);
                var a = new Vector3(shell.min.x + .6f, y, z); var b = new Vector3(shell.max.x - .6f, y, z);
                if (!Clear(a, b, r)) { y -= .7f; a.y = b.y = y; if (y < floor + 2.8f || !Clear(a, b, r)) continue; }
                Pipe(a, b, r, i == 3 ? lagging : i % 2 == 0 ? galv : rust);
                if (i == 3) for (float x = a.x + .8f; x < b.x; x += 1.1f) Prim(g, PrimitiveType.Cylinder, "LaggingBand", new Vector3(x, y, z), new Vector3(r * 2.08f, .02f, r * 2.08f), dark).rotation = Quaternion.Euler(0, 0, 90);
                for (float x = a.x + 1.5f; x < b.x; x += 3.2f) Hanger(new Vector3(x, y, z), r);
                runs++;
            }
            // Bridges crossing the walkway above head height, dropping down both walls with valves at hand height.
            for (int i = 0; i < 4; i++)
            {
                float x = Mathf.Lerp(shell.min.x + 2.2f, shell.max.x - 2.2f, i / 3f) + R(-.5f, .5f), r = R(.07f, .13f), y = floor + R(3.0f, 3.8f);
                var a = new Vector3(x, y, zS + r + .12f); var b = new Vector3(x, y, zN - r - .12f);
                if (!Clear(a + Vector3.forward * .4f, b - Vector3.forward * .4f, r)) continue;
                Pipe(a, b, r, i % 2 == 0 ? rust : galv); Elbow(a, r, dark); Elbow(b, r, dark); Hanger(new Vector3(x, y, (zS + zN) * .5f), r);
                foreach (var end in new[] { a, b })
                {
                    var foot = new Vector3(end.x, floor + .25f, end.z);
                    Pipe(end, foot, r, rust, false); Elbow(foot, r, dark);
                    var valveAt = new Vector3(end.x, floor + 1.45f, end.z) + (end.z < midZ ? Vector3.forward : Vector3.back) * (r + .06f);
                    var body = Prim(g, PrimitiveType.Cylinder, "ValveBody", new Vector3(end.x, floor + 1.45f, end.z), new Vector3(r * 2.6f, r * 1.6f, r * 2.6f), bronze);
                    var wheel = MeshObject(g, "ValveWheel", valveAt, SaveMesh(TorusMesh(.16f, .016f, 20, 5)), Mat("LA_TentRed"));
                    wheel.transform.rotation = Quaternion.LookRotation(Vector3.forward);
                    Rod(g, "ValveStem", new Vector3(end.x, floor + 1.45f, end.z), valveAt, .015f, dark);
                    // Pressure gauge on a short stub above the valve.
                    var gaugeAt = new Vector3(end.x, floor + 2.0f, end.z) + (end.z < midZ ? Vector3.forward : Vector3.back) * (r + .1f);
                    Rod(g, "GaugeStub", new Vector3(end.x, floor + 2.0f, end.z), gaugeAt, .012f, bronze);
                    var gauge = Prim(g, PrimitiveType.Cylinder, "Gauge", gaugeAt, new Vector3(.14f, .025f, .14f), Mat("LA_Paper")); gauge.rotation = Quaternion.FromToRotation(Vector3.up, end.z < midZ ? Vector3.forward : Vector3.back);
                    valves++;
                }
            }
            // Vertical risers on both walls tying the high runs to the floor, with brackets.
            for (int i = 0; i < 8; i++)
            {
                bool south = i % 2 == 0; float x = Mathf.Lerp(shell.min.x + 1.2f, shell.max.x - 1.2f, (i + .5f) / 8f) + R(-.4f, .4f), r = R(.05f, .1f);
                float z = south ? zS + r + .05f : zN - r - .05f;
                var a = new Vector3(x, floor, z); var b = new Vector3(x, ceiling - .3f, z);
                if (!Clear(a + Vector3.up * 2.5f, b, r)) continue;
                Pipe(a, b, r, i % 3 == 0 ? galv : rust);
                for (float y = floor + .8f; y < ceiling - .5f; y += 1.6f) Prim(g, PrimitiveType.Cube, "WallBracket", new Vector3(x, y, z + (south ? -r : r)), new Vector3(r * 2.6f, .05f, .08f), dark);
                runs++;
            }
            // Conduit bundle and junction boxes along one wall.
            for (int k = 0; k < 4; k++) Pipe(new Vector3(shell.min.x + .5f, floor + 2.55f + k * .07f, zN - .08f), new Vector3(shell.max.x - .5f, floor + 2.55f + k * .07f, zN - .08f), .018f, dark, false);
            for (float x = shell.min.x + 2.5f; x < shell.max.x - 1; x += 4.5f) Prim(g, PrimitiveType.Cube, "JunctionBox", new Vector3(x, floor + 2.62f, zN - .14f), new Vector3(.3f, .3f, .12f), Mat("LA_SteelPlate"));
            foreach (var r in g.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = r.bounds.size.magnitude < .5f ? ShadowCastingMode.Off : ShadowCastingMode.On;
            return $"Pipe zone: {runs} runs/risers, {valves} valve stations; walls z {zS:F1}..{zN:F1}, ceiling {ceiling:F1}";
        });

        // ================= 4. Restroom breach: passage as tall as the hole =================

        public static string RaiseRestroomBreachPassage() => RunPass("Restroom breach passage height", target =>
        {
            var p = target.Find("S01_Lobby/Frame8Lobby/Passage_F8_PipeCrawl");
            var reveal = target.Find("S02_Dormitory/Restroom/Shell/WallStructure").Cast<Transform>().Where(t => t.name == "DoorReveal").Select(t => t.GetComponent<Renderer>().bounds).ToArray();
            float h = reveal.Max(b => b.max.y) + .1f;
            var roof = p.Find("PassageRoof"); float old = roof.GetComponent<Renderer>().bounds.min.y;
            if (old >= h - .05f) throw new InvalidOperationException("Passage already raised");
            float k = h / old;
            foreach (var w in p.Cast<Transform>().Where(t => t.name == "PassageWall")) { Undo.RecordObject(w, "Raise wall"); w.localScale = new Vector3(w.localScale.x, h, w.localScale.z); w.position = new Vector3(w.position.x, h * .5f, w.position.z); }
            Undo.RecordObject(roof, "Raise roof"); roof.position += Vector3.up * (h - old);
            var shell = p.Find("PassageFloorCeiling"); Undo.RecordObject(shell, "Raise ceiling"); shell.localScale = new Vector3(shell.localScale.x, shell.localScale.y * k, shell.localScale.z);
            return $"Passage under the restroom breach raised {old:F2} → {h:F2} m";
        });

        // ================= 5. Corridor curtain bays: real velvet cloth =================

        const string CorridorClothMeshPath = AssetDir + "/LA_CorridorCurtainCloth.asset";

        public static string RebuildCorridorCurtains() => RunPass("Corridor velvet curtains", target =>
        {
            var route = target.Find("S06_StageCorridor/StageProcessionalRoute"); var composition = route.Find("GreyboxComposition");
            Remove(route, "CorridorVelvetCurtains");
            if (AssetDatabase.LoadMainAssetAtPath(CorridorClothMeshPath) != null) AssetDatabase.DeleteAsset(CorridorClothMeshPath);
            var g = Group(route, "CorridorVelvetCurtains", route.position); g.rotation = Quaternion.identity;
            var velvet = StageVelvet(); int built = 0, hidden = 0;
            var kits = composition.Cast<Transform>().Where(t => t.name.StartsWith("Curtain_CurtainRoom_")).ToArray();
            foreach (var kit in kits)
            {
                var old = kit.GetComponentsInChildren<CurtainController>(true).FirstOrDefault(); if (old == null) continue;
                var pb = old.panels.Select(x => x.GetComponent<Renderer>().bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                Vector3 span = old.transform.right, face = old.transform.forward;
                float width = Mathf.Abs(Vector3.Dot(pb.size, new Vector3(Mathf.Abs(span.x), 0, Mathf.Abs(span.z)))), top = old.transform.position.y, height = top - (pb.min.y + .02f);
                var rail = Group(g, kit.name.Replace("Curtain_CurtainRoom_", "Velvet_"), old.transform.position + face * .04f);
                rail.rotation = Quaternion.LookRotation(face);
                const float gap = .03f; float half = (width - gap) * .5f;
                string id = rail.name;
                var left = StageClothPanel(rail, id + "_Left", -width * .5f, half, height, velvet, false, built * 7 + 1, .17f, .34f, CorridorClothMeshPath, .75f, .55f);
                var right = StageClothPanel(rail, id + "_Right", gap * .5f, half, height, velvet, true, built * 7 + 4, .17f, .34f, CorridorClothMeshPath, .75f, .55f);
                var c = rail.gameObject.AddComponent<CurtainController>();
                c.panels = new[] { left, right }; c.initialOpen = old.initialOpen; c.openOnStart = false; c.draft = .9f; c.draftSpeed = .45f; c.openDuration = 1.8f;
                foreach (var panel in c.panels) panel.SetOpen(old.initialOpen);
                // Same behaviour as before: if the old curtain parted for a passer-by, so does this one.
                var proximity = old.GetComponentInChildren<CurtainProximityTrigger>(true);
                if (proximity != null)
                {
                    var trig = new GameObject("근접트리거"); trig.transform.SetParent(rail, false); trig.transform.localPosition = new Vector3(0, -height * .5f, 0);
                    var box = trig.AddComponent<BoxCollider>(); box.isTrigger = true; var ob = proximity.GetComponent<BoxCollider>();
                    box.size = new Vector3(width, height, ob != null ? Mathf.Max(ob.size.z * ob.transform.lossyScale.x, 3f) : 5f);
                    trig.AddComponent<CurtainProximityTrigger>().curtain = c;
                }
                Undo.RecordObject(old.gameObject, "Retire stretched cloth"); old.gameObject.SetActive(false);
                foreach (var r in kit.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name == "Valance")) { Undo.RecordObject(r, "Velvet valance"); r.sharedMaterial = velvet; }
                built++;
            }
            // Box folds and tie-back bunches in the bays stay as blockers; only their rendering goes.
            foreach (var room in composition.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("CurtainRoom_")))
                foreach (var r in room.Cast<Transform>().Where(t => t.name.StartsWith("Fold_") || t.name.StartsWith("Bunch_")).Select(t => t.GetComponent<MeshRenderer>()).Where(r => r != null && r.enabled))
                { Undo.RecordObject(r, "Hide box fold"); r.enabled = false; hidden++; }
            foreach (var room in composition.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("CurtainRoom_")))
                foreach (var r in room.Cast<Transform>().Where(t => t.name == "Valance").Select(t => t.GetComponent<MeshRenderer>()).Where(r => r != null))
                { Undo.RecordObject(r, "Velvet valance"); r.sharedMaterial = velvet; }
            return $"Corridor curtains: {built} velvet cloth pairs, {hidden} box folds hidden (colliders kept)";
        });

        // ================= 6. Stage-gear store: circus equipment =================

        public static string FurnishStageGearStore() => RunPass("Stage gear store circus equipment", target =>
        {
            var gear = target.Find("S11_StageGear"); var zone = gear.Find("Zone11");
            Remove(gear, "CircusEquipment");
            var room = VisibleBounds(zone); room.Expand(new Vector3(-.2f, 0, -.2f));
            // Probe from just above the slab: rigging and banners hang over the middle of the hall.
            float floor = FloorAt(new Vector3(room.center.x, room.min.y + .3f, room.center.z), .5f);
            var g = Group(gear, "CircusEquipment", new Vector3(room.center.x, floor, room.center.z));
            var placer = new WallPlacer(room, floor, target.GetComponentsInChildren<Renderer>());
            foreach (var cand in zone.Cast<Transform>().Where(t => t.name.StartsWith("PerformanceObjectCandidate_") && !t.name.Contains("_Mark") && !t.name.Contains("_Cross")))
            { var b = VisibleBounds(cand); placer.KeepOut.Add((b.center, Mathf.Max(b.extents.x, b.extents.z) + 1.6f)); }
            Vector3 Anchor(string name) { var t = zone.Find(name); return t != null ? VisibleBounds(t).center : room.center; }
            var placed = new List<string>(); var missed = new List<string>();
            void Place(string label, Vector3 anchor, Vector2 size, float h, Func<Vector3, float, Transform> build)
            {
                if (!placer.WallSpot(anchor, size, h, out var at, out var yaw)) { missed.Add(label); return; }
                var t = build(at, yaw); placer.Take(t); placed.Add(label);
            }
            var scenery = Anchor("SceneryDock"); var curtains = Anchor("CurtainRack"); var lamps = Anchor("LampPost"); var rig = Anchor("RiggingTower");
            Place("backdrop:moon", scenery, new Vector2(3.4f, 1.2f), 3.9f, (at, yaw) => BackdropFlat(g, at, yaw, "Moon"));
            Place("backdrop:clown", scenery, new Vector2(3.4f, 1.2f), 3.9f, (at, yaw) => BackdropFlat(g, at, yaw, "Clown"));
            Place("backdrop:stars", scenery, new Vector2(3.4f, 1.2f), 3.9f, (at, yaw) => BackdropFlat(g, at, yaw, "Stars"));
            Place("curtain winch", curtains, new Vector2(3.2f, 1.6f), 3.4f, (at, yaw) => CurtainWinch(g, at, yaw));
            Place("lighting rack", lamps, new Vector2(2.6f, .9f), 2.4f, (at, yaw) => LightingRack(g, at, yaw));
            Place("tightrope rig", rig, new Vector2(3.6f, 1.4f), 2.6f, (at, yaw) => TightropeRigStored(g, at, yaw));
            Place("aerial hoop & silks", rig, new Vector2(2.4f, 2.0f), 4.6f, (at, yaw) => AerialRig(g, at, yaw));
            Place("trapeze frame", rig, new Vector2(2.6f, 1.2f), 3.4f, (at, yaw) => TrapezeFrame(g, at, yaw));
            Place("show platform", scenery, new Vector2(2.4f, 2.6f), 1.0f, (at, yaw) => ShowPlatform(g, at, yaw));
            return $"Stage gear store: placed {placed.Count} ({string.Join(", ", placed)})" + (missed.Count > 0 ? $"; no room for {string.Join(", ", missed)}" : "");
        });

        static Material PaintedBackdrop(string kind) => Lit("LA_Backdrop_" + kind, Color.white, .08f, GeneratedTexture($"LA_Backdrop_{kind}.png", 512, false, (u, v) =>
        {
            float wear = Fbm(u * 9 + 3, v * 9 + 7), chip = Mathf.PerlinNoise(u * 60, v * 60);
            Color c;
            float Star(float cx, float cy, float rOut, float rIn) { float dx = u - cx, dy = v - cy, a = Mathf.Atan2(dy, dx) + Mathf.PI * .5f, k = Mathf.Pow(Mathf.Cos(a * 2.5f) * .5f + .5f, 4); return Mathf.Sqrt(dx * dx + dy * dy) - Mathf.Lerp(rIn, rOut, k); }
            if (kind == "Moon")
            {
                c = Color.Lerp(new Color(.05f, .07f, .19f), new Color(.14f, .17f, .36f), v);
                float d1 = new Vector2(u - .52f, v - .55f).magnitude, d2 = new Vector2(u - .61f, v - .6f).magnitude;
                if (d1 < .3f && d2 > .27f) c = new Color(.93f, .84f, .52f) * (.85f + .15f * Fbm(u * 20, v * 20));
                for (int i = 0; i < 14; i++) { float sx = Mathf.Repeat(i * .618f + .1f, 1), sy = Mathf.Repeat(i * .414f + .2f, 1); if (Star(sx, sy, .035f, .014f) < 0 && new Vector2(sx - .52f, sy - .55f).magnitude > .33f) c = new Color(.95f, .87f, .45f); }
            }
            else if (kind == "Clown")
            {
                c = Mathf.Repeat(u * 8, 1) < .5f ? new Color(.55f, .1f, .09f) : new Color(.82f, .75f, .6f);
                float face = new Vector2((u - .5f) * 1.05f, v - .5f).magnitude;
                if (face < .32f) c = new Color(.92f, .9f, .85f);
                foreach (float ex in new[] { .39f, .61f }) { float dx = Mathf.Abs(u - ex), dy = Mathf.Abs(v - .6f); if (dx / .05f + dy / .09f < 1) c = new Color(.15f, .3f, .7f); if (new Vector2(u - ex, v - .6f).magnitude < .022f) c = new Color(.05f, .05f, .06f); }
                if (new Vector2(u - .5f, v - .49f).magnitude < .06f) c = new Color(.8f, .08f, .07f);
                float m = new Vector2(u - .5f, v - .47f).magnitude, ma = Mathf.Atan2(v - .47f, u - .5f) * Mathf.Rad2Deg;
                if (m > .13f && m < .17f && ma < -25 && ma > -155) c = new Color(.75f, .08f, .08f);
                foreach (float hx in new[] { .18f, .82f }) if (new Vector2(u - hx, (v - .58f) * .8f).magnitude < .1f + .03f * Mathf.Sin(v * 40)) c = new Color(.86f, .4f, .08f);
                if (v > .8f && Mathf.Abs(u - .5f) < (.97f - v) * .9f) c = new Color(.2f, .5f, .3f);
            }
            else
            {
                c = Mathf.Repeat((u + v) * 6, 1) < .5f ? new Color(.45f, .07f, .07f) : new Color(.36f, .05f, .06f);
                foreach (var (sx, sy, s) in new[] { (.5f, .55f, .3f), (.2f, .25f, .14f), (.8f, .22f, .12f), (.83f, .8f, .1f), (.17f, .82f, .09f) })
                    if (Star(sx, sy, s, s * .42f) < 0) c = new Color(.85f, .66f, .25f) * (.85f + .15f * Fbm(u * 25, v * 25));
            }
            // Old paint: fading, cracks and chips down to the wooden board.
            c = Color.Lerp(c, c * .75f + new Color(.06f, .05f, .04f), wear * .6f);
            if (chip > .78f) c = Color.Lerp(c, new Color(.36f, .26f, .17f), (chip - .78f) * 4);
            if (u < .025f || u > .975f || v < .025f || v > .975f) c = new Color(.32f, .24f, .15f);
            return new Color(c.r, c.g, c.b);
        }, TextureWrapMode.Clamp));

        // Painted flat on two braced stage jacks with casters; stored a little out of square.
        static Transform BackdropFlat(Transform parent, Vector3 at, float yaw, string kind)
        {
            var f = Group(parent, "BackdropFlat_" + kind, at); f.rotation = Quaternion.Euler(0, yaw + UnityEngine.Random.Range(-3f, 3f), 0);
            const float W = 3.2f, H = 3.6f, lift = .18f; Material wood = Mat("V4_Wood_Old"), dark = Mat("V4_Iron_Dark");
            LBox(f, "FlatBoard", new Vector3(0, lift + H * .5f, -.03f), new Vector3(W, H, .05f), wood, true);
            var face = LShape(f, PrimitiveType.Quad, "PaintedFace", new Vector3(0, lift + H * .5f, -.0f), new Vector3(W - .04f, H - .04f, 1), PaintedBackdrop(kind), new Vector3(0, 180, 0));
            // Unity's quad shows its face toward -Z; turn it to face the room (+Z).
            face.localRotation = Quaternion.Euler(0, 180, 0); face.localPosition = new Vector3(0, lift + H * .5f, .002f);
            // Stiles and rails on the back, and the jacks.
            foreach (float y in new[] { .5f, 1.9f, 3.3f }) LBox(f, "BackRail", new Vector3(0, lift + y, -.08f), new Vector3(W - .1f, .08f, .06f), wood);
            foreach (float x in new[] { -1.1f, 1.1f })
            {
                LBox(f, "JackUpright", new Vector3(x, lift + 1.6f, -.1f), new Vector3(.08f, 3.2f, .06f), wood);
                LBox(f, "JackFoot", new Vector3(x, lift - .05f, -.45f), new Vector3(.1f, .08f, .9f), wood);
                LRod(f, "JackBrace", new Vector3(x, lift, -.85f), new Vector3(x, lift + 2.2f, -.12f), .03f, wood);
                foreach (float z in new[] { -.05f, -.85f }) { LShape(f, PrimitiveType.Cylinder, "Caster", new Vector3(x, .07f, z), new Vector3(.14f, .03f, .14f), dark, new Vector3(0, 0, 90)); }
            }
            LBox(f, "StencilPlate", new Vector3(W * .5f - .35f, lift + .25f, -.11f), new Vector3(.4f, .12f, .01f), Mat("LA_Paper"));
            return f;
        }

        // Velvet curtain wound on a hand-cranked drum between two stands, its tail pooled on the floor, rope to an overhead block.
        static Transform CurtainWinch(Transform parent, Vector3 at, float yaw)
        {
            var w = Group(parent, "CurtainWinch", at); w.rotation = Quaternion.Euler(0, yaw, 0);
            Material steel = Mat("LA_SteelPlate"), dark = Mat("V4_Iron_Dark"), velvet = StageVelvet();
            const float span = 2.6f, drumY = 1.0f;
            foreach (float x in new[] { -span * .5f, span * .5f })
            {
                LRod(w, "StandLeg", new Vector3(x, 0, -.5f), new Vector3(x, drumY + .1f, 0), .04f, steel);
                LRod(w, "StandLeg", new Vector3(x, 0, .5f), new Vector3(x, drumY + .1f, 0), .04f, steel);
                LBox(w, "StandFoot", new Vector3(x, .03f, 0), new Vector3(.08f, .06f, 1.1f), dark, true);
            }
            LShape(w, PrimitiveType.Cylinder, "Drum", new Vector3(0, drumY, 0), new Vector3(.32f, span * .5f, .32f), steel, new Vector3(0, 0, 90));
            LShape(w, PrimitiveType.Cylinder, "WoundVelvet", new Vector3(0, drumY, 0), new Vector3(.62f, span * .46f, .62f), velvet, new Vector3(0, 0, 90));
            var crank = MeshObject(w, "CrankWheel", w.TransformPoint(new Vector3(span * .5f + .15f, drumY, 0)), SaveMesh(TorusMesh(.28f, .025f, 24, 5)), dark);
            crank.transform.rotation = w.rotation * Quaternion.Euler(0, 90, 0);
            LRod(w, "CrankHandle", new Vector3(span * .5f + .18f, drumY + .28f, 0), new Vector3(span * .5f + .38f, drumY + .28f, 0), .02f, Mat("V4_Wood_Old"));
            LShape(w, PrimitiveType.Cylinder, "RatchetPawl", new Vector3(-span * .5f - .1f, drumY, 0), new Vector3(.2f, .04f, .2f), Mat("LA_Bronze"), new Vector3(0, 0, 90));
            // Velvet tail: folds lying across the floor in front of the drum.
            for (int i = 0; i < 5; i++)
            {
                var fold = LShape(w, PrimitiveType.Capsule, "VelvetFold", new Vector3(-.9f + i * .45f, .1f, .55f + (i % 2) * .08f), new Vector3(.22f, .5f, .16f), velvet, new Vector3(90, 0, 0));
                fold.localRotation = Quaternion.Euler(90, i * 7 - 14, 0);
            }
            LBox(w, "VelvetSheet", new Vector3(0, drumY * .5f, .32f), new Vector3(span * .86f, drumY - .1f, .03f), velvet).localRotation = Quaternion.Euler(-28, 0, 0);
            // Overhead block on a short gantry and the lift rope down to the drum.
            LRod(w, "GantryPost", new Vector3(-span * .5f - .25f, 0, -.65f), new Vector3(-span * .5f - .25f, 3.3f, -.65f), .045f, steel);
            LRod(w, "GantryPost", new Vector3(span * .5f + .25f, 0, -.65f), new Vector3(span * .5f + .25f, 3.3f, -.65f), .045f, steel);
            LBox(w, "GantryBeam", new Vector3(0, 3.3f, -.65f), new Vector3(span + .6f, .12f, .12f), dark);
            LShape(w, PrimitiveType.Cylinder, "PulleyBlock", new Vector3(0, 3.1f, -.65f), new Vector3(.24f, .05f, .24f), Mat("LA_Bronze"), new Vector3(90, 0, 0));
            LRod(w, "LiftRope", new Vector3(0, 3.0f, -.65f), new Vector3(0, drumY + .3f, -.1f), .012f, Mat("V6_Rope"));
            return w;
        }

        // Steel rack: fresnel lanterns with barn doors on two shelves, cable drums and counterweights below.
        static Transform LightingRack(Transform parent, Vector3 at, float yaw)
        {
            var r = Group(parent, "StageLightingRack", at); r.rotation = Quaternion.Euler(0, yaw, 0);
            Material steel = Mat("LA_SteelPlate"), dark = Mat("V4_Iron_Dark"), black = Mat("V4_Black");
            const float W = 2.4f, D = .75f;
            foreach (float x in new[] { -W * .5f, W * .5f }) foreach (float z in new[] { -D * .5f, D * .5f }) LBox(r, "Upright", new Vector3(x, 1.1f, z), new Vector3(.06f, 2.2f, .06f), dark, true);
            foreach (float y in new[] { .12f, 1.05f, 2.0f }) LBox(r, "Shelf", new Vector3(0, y, 0), new Vector3(W, .04f, D), steel);
            var lens = Emissive("LA_FresnelLens", new Color(.3f, .3f, .28f), new Color(.05f, .05f, .04f));
            for (int s = 0; s < 2; s++)
                for (int i = 0; i < 3; i++)
                {
                    var c = new Vector3(-.75f + i * .75f, (s == 0 ? 1.05f : 2.0f) + .3f, 0);
                    var lamp = Group(r, "FresnelLantern", r.TransformPoint(c)); lamp.localRotation = Quaternion.Euler(-8 + i * 6, 0, 0);
                    LShape(lamp, PrimitiveType.Cylinder, "Body", Vector3.zero, new Vector3(.32f, .22f, .32f), black, new Vector3(90, 0, 0));
                    LShape(lamp, PrimitiveType.Cylinder, "Lens", new Vector3(0, 0, .22f), new Vector3(.27f, .01f, .27f), lens, new Vector3(90, 0, 0));
                    foreach (var (dx, dy, rx, ry) in new[] { (0f, .17f, -35f, 0f), (0f, -.17f, 35f, 0f), (.17f, 0f, 0f, 35f), (-.17f, 0f, 0f, -35f) })
                        LBox(lamp, "BarnDoor", new Vector3(dx, dy, .3f), new Vector3(dx == 0 ? .3f : .01f, dx == 0 ? .01f : .3f, .16f), black).localRotation = Quaternion.Euler(rx, ry, 0);
                    LRod(lamp, "Yoke", new Vector3(-.2f, 0, 0), new Vector3(-.2f, -.25f, 0), .015f, dark); LRod(lamp, "Yoke", new Vector3(.2f, 0, 0), new Vector3(.2f, -.25f, 0), .015f, dark);
                    LRod(lamp, "Yoke", new Vector3(-.2f, -.25f, 0), new Vector3(.2f, -.25f, 0), .015f, dark);
                }
            foreach (float x in new[] { -.7f, .05f })
            {
                var drum = Group(r, "CableDrum", r.TransformPoint(new Vector3(x, .45f, 0)));
                foreach (float z in new[] { -.14f, .14f }) LShape(drum, PrimitiveType.Cylinder, "DrumFlange", new Vector3(0, 0, z), new Vector3(.62f, .015f, .62f), Mat("V4_Wood_Old"), new Vector3(90, 0, 0));
                LShape(drum, PrimitiveType.Cylinder, "CableWinding", Vector3.zero, new Vector3(.5f, .13f, .5f), black, new Vector3(90, 0, 0));
            }
            for (int i = 0; i < 6; i++) LBox(r, "Counterweight", new Vector3(.82f, .2f + i * .09f, (i % 2) * .04f), new Vector3(.42f, .08f, .2f), dark, true);
            return r;
        }

        // Tightrope stands broken down for storage: two A-frames upright, the tension reel and a coil of wire rope.
        static Transform TightropeRigStored(Transform parent, Vector3 at, float yaw)
        {
            var t = Group(parent, "TightropeRig_Stored", at); t.rotation = Quaternion.Euler(0, yaw, 0);
            Material steel = Mat("LA_SteelPlate"), dark = Mat("V4_Iron_Dark"), wood = Mat("V4_Wood_Old");
            foreach (float x in new[] { -1.1f, .3f })
            {
                LRod(t, "AFrameLeg", new Vector3(x - .35f, 0, -.35f), new Vector3(x, 2.4f, -.35f), .04f, steel);
                LRod(t, "AFrameLeg", new Vector3(x + .35f, 0, -.35f), new Vector3(x, 2.4f, -.35f), .04f, steel);
                LBox(t, "AFrameCross", new Vector3(x, 1.0f, -.35f), new Vector3(.55f, .05f, .05f), dark);
                LBox(t, "StandPlatform", new Vector3(x, 2.42f, -.35f), new Vector3(.5f, .05f, .45f), wood, true);
            }
            var reel = Group(t, "TensionReel", t.TransformPoint(new Vector3(1.25f, .35f, .1f)));
            foreach (float z in new[] { -.16f, .16f }) LShape(reel, PrimitiveType.Cylinder, "ReelPlate", new Vector3(0, 0, z), new Vector3(.5f, .02f, .5f), steel, new Vector3(90, 0, 0));
            LShape(reel, PrimitiveType.Cylinder, "ReelCore", Vector3.zero, new Vector3(.28f, .16f, .28f), dark, new Vector3(90, 0, 0));
            LBox(reel, "ReelBase", new Vector3(0, -.3f, 0), new Vector3(.6f, .06f, .5f), dark, true);
            LRod(reel, "RatchetLever", new Vector3(.1f, .05f, .2f), new Vector3(.45f, .35f, .2f), .02f, Mat("LA_HazardPaint"));
            for (int i = 0; i < 6; i++) { var coil = MeshObject(t, "WireRopeCoil", t.TransformPoint(new Vector3(1.25f, .03f + i * .028f, .75f)), SaveMesh(TorusMesh(.36f - i * .015f, .014f, 24, 4)), Mat("V4_Iron_Dark")); coil.transform.rotation = t.rotation * Quaternion.Euler(90, i * 20, 0); }
            return t;
        }

        // Free-standing aerial frame with a taped hoop and two red silks pooling on the floor.
        static Transform AerialRig(Transform parent, Vector3 at, float yaw)
        {
            var a = Group(parent, "AerialHoopRig", at); a.rotation = Quaternion.Euler(0, yaw, 0);
            Material steel = Mat("LA_SteelPlate"), dark = Mat("V4_Iron_Dark");
            const float top = 4.4f;
            foreach (float x in new[] { -1.05f, 1.05f })
            {
                LRod(a, "FrameLeg", new Vector3(x, 0, -.8f), new Vector3(x * .9f, top, 0), .05f, steel);
                LRod(a, "FrameLeg", new Vector3(x, 0, .8f), new Vector3(x * .9f, top, 0), .05f, steel);
                LBox(a, "FrameFoot", new Vector3(x, .03f, 0), new Vector3(.1f, .06f, 1.7f), dark, true);
            }
            LRod(a, "TopBar", new Vector3(-.95f, top, 0), new Vector3(.95f, top, 0), .05f, dark);
            // Hoop on a short rope and swivel.
            LRod(a, "HoopRope", new Vector3(-.45f, top, 0), new Vector3(-.45f, 2.55f, 0), .012f, Mat("V6_Rope"));
            var hoop = MeshObject(a, "AerialHoop", a.TransformPoint(new Vector3(-.45f, 2.0f, 0)), SaveMesh(TorusMesh(.48f, .028f, 32, 6)), Mat("LA_TentCream"));
            hoop.transform.rotation = a.rotation * Quaternion.Euler(0, 75, 0);
            var silk = DoubleSided(Lit("LA_AerialSilk", new Color(.55f, .06f, .07f), .35f));
            foreach (float x in new[] { .35f, .5f })
            {
                var strip = MeshObject(a, "AerialSilk", a.TransformPoint(new Vector3(x, 0, 0)), SaveMesh(SilkStrip(top - .05f, .28f, x > .4f ? 1 : -1)), silk);
                strip.transform.rotation = a.rotation;
            }
            return a;
        }

        // Hanging silk: a twisting strip from the bar to the floor, then a pooled tail.
        static Mesh SilkStrip(float height, float width, int side)
        {
            var v = new List<Vector3>(); var t = new List<int>(); const int rows = 28;
            for (int j = 0; j <= rows; j++)
            {
                float s = (float)j / rows; Vector3 c; Vector3 across;
                if (s < .8f) { float y = height * (1 - s / .8f) + .02f; c = new Vector3(Mathf.Sin(s * 9) * .03f, y, Mathf.Sin(s * 5) * .05f); float tw = s * 2.2f; across = new Vector3(Mathf.Cos(tw), 0, Mathf.Sin(tw)) * width * (1 - s * .5f) * .5f; }
                else { float k = (s - .8f) / .2f; c = new Vector3(side * k * .5f, .015f + .02f * Mathf.Sin(k * 12), k * .55f); across = new Vector3(Mathf.Cos(1.76f), 0, Mathf.Sin(1.76f)) * width * .6f; }
                v.Add(c - across); v.Add(c + across);
                if (j > 0) { int i = j * 2; t.AddRange(new[] { i - 2, i, i - 1, i - 1, i, i + 1 }); }
            }
            var m = new Mesh { name = "LA_AerialSilk" + (side > 0 ? "R" : "L") }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }

        // Storage frame with two trapeze bars hanging from their ropes.
        static Transform TrapezeFrame(Transform parent, Vector3 at, float yaw)
        {
            var t = Group(parent, "TrapezeStorageFrame", at); t.rotation = Quaternion.Euler(0, yaw, 0);
            Material steel = Mat("LA_SteelPlate"), dark = Mat("V4_Iron_Dark"), rope = Mat("V6_Rope");
            const float top = 3.2f;
            foreach (float x in new[] { -1.2f, 1.2f }) { LRod(t, "FramePost", new Vector3(x, 0, -.3f), new Vector3(x, top, -.3f), .045f, steel); LBox(t, "FrameFoot", new Vector3(x, .03f, -.1f), new Vector3(.1f, .06f, .9f), dark, true); }
            LRod(t, "FrameBeam", new Vector3(-1.2f, top, -.3f), new Vector3(1.2f, top, -.3f), .05f, dark);
            foreach (var (x, drop, swing) in new[] { (-.55f, 1.5f, 6f), (.55f, 1.25f, -9f) })
            {
                var bar = Group(t, "Trapeze", t.TransformPoint(new Vector3(x, top, -.3f))); bar.localRotation = Quaternion.Euler(swing, 0, 0);
                LRod(bar, "TrapezeRope", new Vector3(-.4f, 0, 0), new Vector3(-.4f, -drop, 0), .012f, rope);
                LRod(bar, "TrapezeRope", new Vector3(.4f, 0, 0), new Vector3(.4f, -drop, 0), .012f, rope);
                LRod(bar, "TrapezeBar", new Vector3(-.42f, -drop, 0), new Vector3(.42f, -drop, 0), .022f, Mat("LA_TentCream"));
                foreach (float s in new[] { -.42f, .42f }) LShape(bar, PrimitiveType.Sphere, "BarEnd", new Vector3(s, -drop, 0), Vector3.one * .055f, Mat("LA_Bronze"));
            }
            return t;
        }

        // Portable show platform: a star-painted drum on casters with a three-step box stair.
        static Transform ShowPlatform(Transform parent, Vector3 at, float yaw)
        {
            var p = Group(parent, "PortableShowPlatform", at); p.rotation = Quaternion.Euler(0, yaw, 0);
            const float r = .95f, h = .62f;
            var c = p.TransformPoint(new Vector3(0, 0, .25f));
            LShape(p, PrimitiveType.Cylinder, "PlatformSkirt", new Vector3(0, h * .5f + .08f, .25f), new Vector3(r * 2, h * .5f, r * 2), Mat("V4_Harlequin"));
            var topDisc = LShape(p, PrimitiveType.Cylinder, "PlatformTop", new Vector3(0, h + .09f, .25f), new Vector3(r * 2.04f, .02f, r * 2.04f), Mat("LA_RingFloor"));
            topDisc.gameObject.AddComponent<MeshCollider>();
            LShape(p, PrimitiveType.Cylinder, "GoldBand", new Vector3(0, h + .07f, .25f), new Vector3(r * 2.06f, .03f, r * 2.06f), Mat("LA_Bronze"));
            for (int i = 0; i < 6; i++) { float a = i * Mathf.PI / 3; LShape(p, PrimitiveType.Sphere, "Caster", new Vector3(Mathf.Cos(a) * r * .8f, .05f, .25f + Mathf.Sin(a) * r * .8f), Vector3.one * .1f, Mat("V4_Black")); }
            for (int s = 0; s < 3; s++) LBox(p, "BoxStep", new Vector3(0, (s + 1) * .1f + .04f, -.85f + s * .25f), new Vector3(.8f, (s + 1) * .2f + .02f, .26f), Mat("V4_Wood_Old"), true).localPosition = new Vector3(0, ((s + 1) * .21f) * .5f, -.95f + s * .24f);
            return p;
        }
    }
}
