using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Editor
{
    // Eighth round: every red velvet bay along the stage passage becomes a small sealed room. The passage walls get an
    // opening behind each bay, a black box room sits behind it, an invisible barrier keeps people out, and the curtains
    // stay drawn (no proximity opening). Low wall pipes that ran across a bay are cut off short of it, and a dust sheet
    // left floating where a crate used to be goes.
    public static partial class WaitingRoomLayoutA
    {
        const float BayOpenW = 4.7f, BayOpenH = 5.0f, BayRoomDepth = 1.8f, BayShell = .15f;
        // Texel density of the passage walls (MoodBox UVs: 21 across 65 m, 5 up 13 m).
        const float WallUPerM = 21f / 65f, WallVPerM = 5f / 13f;

        public static string SealCurtainBayRooms() => RunPass("Sealed rooms behind the stage passage curtains", target =>
        {
            var corridor = target.Find("S06_StageCorridor"); var route = corridor.Find("StageProcessionalRoute");
            var velvets = route.Find("CorridorVelvetCurtains").Cast<Transform>().Where(t => t.GetComponent("CurtainController") != null).ToArray();
            var comp = route.Find("GreyboxComposition");
            var walls = new[] { comp.Find("W_06_South/Seg0"), comp.Find("W_06_North_08_South/Seg0") };
            var rooms = comp.Find("CurtainRooms");
            var black = rooms.GetComponentsInChildren<Renderer>(true).First(r => r.name == "DarkRoom").sharedMaterial;

            Remove(corridor, "CurtainBayRooms");
            var root = Group(corridor, "CurtainBayRooms", Vector3.zero); root.rotation = Quaternion.identity;

            // Wall panels rebuilt from the original long slabs, leaving a hole behind each bay. UVs come from world
            // position so the concrete runs on across panel joints at the original density.
            string meshPath = AssetDir + "/LA_CurtainBayWalls.asset";
            AssetDatabase.DeleteAsset(meshPath);
            Mesh meshBank = null; int panels = 0;
            foreach (var seg in walls)
            {
                // Seen in its original state on a rerun, so the cut is always made from the full slab.
                var b = SlabBounds(seg); Undo.RecordObject(seg.gameObject, "Hide slab"); seg.gameObject.SetActive(false);
                bool west = b.center.x < -6;
                var bays = velvets.Where(v => (v.position.x < -6) == west).Select(v => v.position.z).OrderBy(z => z).ToList();
                var pieces = new List<(float z0, float z1, float y0, float y1)>();
                float cursor = b.min.z;
                foreach (float z in bays)
                {
                    float o0 = z - BayOpenW * .5f, o1 = z + BayOpenW * .5f;
                    pieces.Add((cursor, o0, b.min.y, b.max.y));
                    pieces.Add((o0, o1, b.min.y + BayOpenH, b.max.y)); // lintel
                    cursor = o1;
                }
                pieces.Add((cursor, b.max.z, b.min.y, b.max.y));
                var wallGroup = Group(root, (west ? "West" : "East") + "Wall", Vector3.zero); wallGroup.rotation = Quaternion.identity;
                foreach (var (z0, z1, y0, y1) in pieces)
                {
                    if (z1 - z0 < .01f) continue;
                    var size = new Vector3(b.size.x, y1 - y0, z1 - z0); var center = new Vector3(b.center.x, (y0 + y1) * .5f, (z0 + z1) * .5f);
                    var mesh = WorldUvBox(center, size); mesh.name = $"{wallGroup.name}_{panels}";
                    if (meshBank == null) { AssetDatabase.CreateAsset(mesh, meshPath); meshBank = mesh; } else AssetDatabase.AddObjectToAsset(mesh, meshBank);
                    var go = new GameObject("WallPanel"); Undo.RegisterCreatedObjectUndo(go, "Wall panel");
                    go.transform.SetParent(wallGroup, false); go.transform.position = center;
                    go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = seg.GetComponent<Renderer>().sharedMaterial;
                    go.AddComponent<BoxCollider>().size = size;
                    go.isStatic = seg.gameObject.isStatic; panels++;
                }
            }

            // A small black room behind each opening, a barrier in the opening, and the curtain kept drawn.
            int built = 0, triggers = 0;
            foreach (var v in velvets)
            {
                bool west = v.position.x < -6; float outward = west ? -1 : 1;
                var wall = SlabBounds(walls[west ? 0 : 1]);
                float inner = west ? wall.max.x : wall.min.x, outer = west ? wall.min.x : wall.max.x, floorY = wall.min.y;
                var g = Group(root, "Room_" + v.name.Replace("Velvet_", ""), new Vector3(outer, floorY, v.position.z)); g.rotation = Quaternion.identity;
                float back = outer + outward * BayRoomDepth, far = back + outward * BayShell, midX = (inner + far) * .5f, depth = Mathf.Abs(far - inner);
                Vector3 W(float x, float y, float z) => new Vector3(x, floorY + y, v.position.z + z);
                Box(g, "BackWall", W(back + outward * BayShell * .5f, BayOpenH * .5f, 0), new Vector3(BayShell, BayOpenH + BayShell * 2, BayOpenW + BayShell * 2), black, true);
                foreach (float s in new[] { -1f, 1f })
                    Box(g, "SideWall", W(midX, BayOpenH * .5f, s * (BayOpenW + BayShell) * .5f), new Vector3(depth, BayOpenH + BayShell * 2, BayShell), black, true);
                Box(g, "Ceiling", W(midX, BayOpenH + BayShell * .5f, 0), new Vector3(depth, BayShell, BayOpenW), black, true);
                Box(g, "Floor", W(midX, -BayShell * .5f + .01f, 0), new Vector3(depth, BayShell, BayOpenW), black, true);
                var bar = new GameObject("NoEntryBarrier"); Undo.RegisterCreatedObjectUndo(bar, "Barrier");
                bar.transform.SetParent(g, false); bar.transform.position = W(inner, BayOpenH * .5f, 0);
                bar.AddComponent<BoxCollider>().size = new Vector3(.1f, BayOpenH, BayOpenW);
                built++;

                // Drawn and staying drawn: rest fully closed, never opened by someone walking up.
                var ctrl = (MonoBehaviour)v.GetComponent("CurtainController"); var so = new SerializedObject(ctrl);
                so.FindProperty("initialOpen").floatValue = 0f; so.FindProperty("openOnStart").boolValue = false; so.ApplyModifiedProperties();
                ctrl.GetType().GetMethod("OnValidate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(ctrl, null);
                foreach (var trig in v.GetComponentsInChildren<Transform>(true).Where(t => t.GetComponent("CurtainProximityTrigger") != null))
                { Undo.RecordObject(trig.gameObject, "No proximity open"); trig.gameObject.SetActive(false); triggers++; }
            }
            // Greybox stand-ins of the open bays (black panel and bunched sides) would show through the drawn velvet.
            int hidden = 0;
            foreach (var r in rooms.GetComponentsInChildren<Transform>(true).Where(t => t.name is "DarkRoom" or "Bunch_L" or "Bunch_R"))
            { Undo.RecordObject(r.gameObject, "Hide stand-in"); r.gameObject.SetActive(false); hidden++; }

            // Dust sheet whose crate is gone.
            int floating = 0;
            foreach (var d in corridor.GetComponentsInChildren<Transform>().Where(t => t.name == "CrateDust").ToArray())
            { Undo.RecordObject(d.gameObject, "Floating dust"); d.gameObject.SetActive(false); floating++; }
            // Pipe end caps left hanging on the wall with no pipe behind them.
            var wallRenderers = corridor.GetComponentsInChildren<Renderer>();
            foreach (var cap in wallRenderers.Where(r => r.name == "PipeCap" && r.transform.parent.name == "WallDressing").ToArray())
            {
                var near = cap.bounds; near.Expand(new Vector3(.1f, .1f, .6f));
                if (wallRenderers.Any(r => r != cap && r.gameObject.activeInHierarchy && r.name.StartsWith("SM_Pipe") && r.bounds.Intersects(near))) continue;
                Undo.RecordObject(cap.gameObject, "Orphan cap"); cap.gameObject.SetActive(false); floating++;
            }

            int cut = CutPipesShortOfBays(corridor, velvets, black);
            AssetDatabase.SaveAssets(); Physics.SyncTransforms();

            // Nobody gets in: probes walking into each bay from the passage stop at the barrier.
            int entered = 0;
            foreach (var v in velvets)
            {
                float outward = v.position.x < -6 ? -1 : 1; var from = new Vector3(v.position.x - outward * 1.2f, .9f, v.position.z);
                if (!Physics.Raycast(from, new Vector3(outward, 0, 0), 3f, ~0, QueryTriggerInteraction.Ignore)) entered++;
            }
            return $"{panels} wall panels, {built} sealed bay rooms, {triggers} proximity triggers off, {hidden} stand-ins hidden, {floating} floating dust removed, {cut} pipes cut; bays reachable {entered}/{velvets.Length}";
        });

        // Box mesh of a given world size whose UVs follow world position at the passage wall's texel density.
        static Mesh WorldUvBox(Vector3 center, Vector3 size)
        {
            var h = size * .5f; var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var normals = new List<Vector3>(); var tris = new List<int>();
            void Face(Vector3 n, Vector3 u, Vector3 v)
            {
                int i = verts.Count; var c = Vector3.Scale(n, h);
                Vector3 du = Vector3.Scale(u, h), dv = Vector3.Scale(v, h);
                foreach (var (a, bb) in new[] { (-1, -1), (-1, 1), (1, 1), (1, -1) })
                {
                    var p = c + du * a + dv * bb; verts.Add(p); normals.Add(n);
                    var w = p + center; // world position drives the UVs
                    float uw = Vector3.Dot(w, u) * (Mathf.Abs(u.y) > .5f ? WallVPerM : WallUPerM), vw = Vector3.Dot(w, v) * (Mathf.Abs(v.y) > .5f ? WallVPerM : WallUPerM);
                    uvs.Add(new Vector2(uw, vw));
                }
                tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }
            Face(Vector3.right, Vector3.forward, Vector3.up); Face(Vector3.left, Vector3.back, Vector3.up);
            Face(Vector3.forward, Vector3.left, Vector3.up); Face(Vector3.back, Vector3.right, Vector3.up);
            Face(Vector3.up, Vector3.forward, Vector3.right); Face(Vector3.down, Vector3.forward, Vector3.left);
            var m = new Mesh(); m.SetVertices(verts); m.SetNormals(normals); m.SetUVs(0, uvs); m.SetTriangles(tris, 0); m.RecalculateBounds(); m.RecalculateTangents();
            return m;
        }

        // Low wall pipes that run across a bay end 0.3 m short of it with a cut, open end.
        static int CutPipesShortOfBays(Transform corridor, Transform[] velvets, Material voidMat)
        {
            Material dark = Mat("V4_Iron_Dark"), rust = Mat("V4_RustyMetal");
            var dressing = corridor.GetComponentsInChildren<Transform>().Where(t => t.name == "WallDressing").ToArray();
            int cut = 0;
            foreach (var wd in dressing)
            foreach (Transform pipe in wd.Cast<Transform>().Where(t => t.name.Contains("Pipe") && t.gameObject.activeInHierarchy).ToArray())
            {
                var rs = pipe.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue;
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                if (b.min.y > 5.2f || b.size.z < b.size.x * 2) continue; // only low pipes running along the passage
                foreach (var v in velvets)
                {
                    if (Mathf.Abs(b.center.x - v.position.x) > 1.3f) continue;
                    float bz0 = v.position.z - 2.5f, bz1 = v.position.z + 2.5f;
                    if (b.max.z <= bz0 || b.min.z >= bz1) continue;
                    // Keep the longer part outside the bay.
                    bool keepLow = (bz0 - b.min.z) >= (b.max.z - bz1);
                    float a = keepLow ? b.min.z : bz1 + .3f, e = keepLow ? bz0 - .3f : b.max.z;
                    if (e - a < .4f) { Undo.RecordObject(pipe.gameObject, "Pipe gone"); pipe.gameObject.SetActive(false); cut++; break; }
                    // Scale along the local axis that lies along world z, then slide so the kept end stays put.
                    int axis = Enumerable.Range(0, 3).OrderByDescending(i => Mathf.Abs(Vector3.Dot(pipe.rotation * Axis(i), Vector3.forward))).First();
                    float k = (e - a) / b.size.z;
                    Undo.RecordObject(pipe, "Cut pipe");
                    var ls = pipe.localScale; ls[axis] *= k; pipe.localScale = ls;
                    var nb = rs[0].bounds; foreach (var r in rs) nb.Encapsulate(r.bounds);
                    pipe.position += Vector3.forward * (keepLow ? a - nb.min.z : e - nb.max.z);
                    // Cut end: dark open bore, a ragged rust lip and two torn tabs.
                    float endZ = keepLow ? e : a, dir = keepLow ? 1 : -1; float rad = Mathf.Min(b.size.x, b.size.y) * .5f;
                    var at = new Vector3(b.center.x, b.center.y, endZ);
                    var g = Group(pipe.parent, pipe.name + "_CutEnd", at); g.rotation = Quaternion.LookRotation(Vector3.forward * dir);
                    LShape(g, PrimitiveType.Cylinder, "OpenBore", new Vector3(0, 0, -.015f), new Vector3(rad * 1.7f, .01f, rad * 1.7f), voidMat, new Vector3(90, 0, 0));
                    LShape(g, PrimitiveType.Cylinder, "RustLip", new Vector3(0, 0, -.03f), new Vector3(rad * 2.02f, .03f, rad * 2.02f), rust, new Vector3(90, 0, 0));
                    LBox(g, "TornTab", new Vector3(rad * .7f, rad * .5f, .05f), new Vector3(.12f, .02f, .12f), rust, false, new Vector3(25, 30, 40));
                    LBox(g, "TornTab", new Vector3(-rad * .6f, -rad * .7f, .04f), new Vector3(.1f, .02f, .09f), dark, false, new Vector3(-20, -15, 60));
                    // Dust lying on the pipe follows it.
                    foreach (Transform dust in wd.Cast<Transform>().Where(t => t.name.StartsWith("PipeDust") && t.gameObject.activeInHierarchy).ToArray())
                    {
                        var db = dust.GetComponentInChildren<Renderer>().bounds;
                        if (Mathf.Abs(db.center.x - b.center.x) > .3f || db.max.z <= b.min.z || db.min.z >= b.max.z || db.center.y < b.center.y) continue;
                        float d0 = Mathf.Max(db.min.z, a + .1f), d1 = Mathf.Min(db.max.z, e - .1f);
                        int dAxis = Enumerable.Range(0, 3).OrderByDescending(i => Mathf.Abs(Vector3.Dot(dust.rotation * Axis(i), Vector3.forward))).First();
                        Undo.RecordObject(dust, "Dust follows pipe");
                        var dls = dust.localScale; dls[dAxis] *= (d1 - d0) / db.size.z; dust.localScale = dls;
                        var p = dust.position; p.z += (d0 + d1) * .5f - db.center.z; dust.position = p;
                    }
                    cut++; break;
                }
            }
            return cut;
        }

        // Restroom mirrors: the single 4 m pane leaned 2.6° out through the iron band and ran across both basins. One framed
        // mirror per sink now, flat on the wall (face x 11.65), between the basin top (1.08) and the band (2.08).
        public static string FitRestroomMirrors() => RunPass("Restroom mirrors one per sink", target =>
        {
            var fixtures = target.Find("S02_Dormitory/Restroom/Fixtures") ?? throw new InvalidOperationException("Restroom fixtures missing");
            var group = fixtures.Find("SinkMirrors"); var old = group.Find("Mirror");
            var glass = old.GetComponent<Renderer>().sharedMaterial; Material frame = Mat("V4_Iron_Dark");
            Undo.RecordObject(old.gameObject, "Retire long mirror"); old.gameObject.SetActive(false);
            foreach (Transform t in group.Cast<Transform>().Where(t => t.name.StartsWith("SinkMirror_")).ToArray()) Undo.DestroyObjectImmediate(t.gameObject);
            const float wallX = 11.65f, y0 = 1.27f, y1 = 1.98f, w = .8f, f = .04f;
            int made = 0;
            foreach (var sink in fixtures.Cast<Transform>().Where(t => t.name.StartsWith("Sink_")))
            {
                float z = sink.GetComponentsInChildren<Renderer>().Select(r => r.bounds.center.z).DefaultIfEmpty(sink.position.z).Average();
                var m = Group(group, "SinkMirror_" + sink.name.Substring(5), new Vector3(wallX, (y0 + y1) * .5f, z)); m.rotation = Quaternion.identity;
                Box(m, "Glass", new Vector3(wallX + .022f, (y0 + y1) * .5f, z), new Vector3(.012f, y1 - y0 - f, w - f), glass);
                Box(m, "Backing", new Vector3(wallX + .008f, (y0 + y1) * .5f, z), new Vector3(.016f, y1 - y0, w), frame);
                foreach (float s in new[] { -1f, 1f })
                {
                    Box(m, "FrameSide", new Vector3(wallX + .02f, (y0 + y1) * .5f, z + s * (w - f) * .5f), new Vector3(.04f, y1 - y0, f), frame);
                    Box(m, "FrameEdge", new Vector3(wallX + .02f, (s > 0 ? y1 - f * .5f : y0 + f * .5f), z), new Vector3(.04f, f, w), frame);
                }
                made++;
            }
            return $"Restroom mirrors: {made} framed mirrors, long pane retired";
        });

        // Z-fighting: visible faces of two boxes lying in the same plane flicker (e.g. both rooms' door sills overlapping in a
        // doorway, both rooms' door reveals at a shared wall). Exact duplicates keep one drawn (colliders stay); partial
        // overlaps lift the smaller box 3 mm off the shared plane. Pairs of the same untextured material are left alone.
        public static string FixCoplanarFaces() => RunPass("Coplanar face flicker", target =>
        {
            static bool AxisAligned(Transform t)
            {
                var e = t.rotation.eulerAngles;
                foreach (float a in new[] { e.x, e.y, e.z }) { float m = Mathf.Repeat(a, 90f); if (m > .5f && m < 89.5f) return false; }
                return true;
            }
            var list = new List<(Renderer r, Bounds b)>();
            foreach (var r in target.GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.enabled) continue; var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                string n = mf.sharedMesh.name; if (!(n == "Cube" || n.StartsWith("MoodBox"))) continue;
                if (!AxisAligned(r.transform) || r.sharedMaterial == null || r.sharedMaterial.renderQueue >= 3000) continue;
                var bb = r.bounds; if (bb.size.magnitude > 200) continue; list.Add((r, bb));
            }
            bool Hidden(Vector3 p, Renderer a, Renderer b)
            {
                foreach (var c in Physics.OverlapSphere(p, .003f, ~0, QueryTriggerInteraction.Ignore))
                    if (c.transform != a.transform && c.transform != b.transform) return true;
                foreach (var o in list) if (o.r != a && o.r != b && o.r.enabled && o.b.Contains(p)) return true;
                return false;
            }
            var touched = new HashSet<Renderer>(); int hiddenDup = 0, lifted = 0;
            for (int axis = 0; axis < 3; axis++)
            for (int side = 0; side < 2; side++)
            {
                var buckets = new Dictionary<long, List<int>>();
                for (int i = 0; i < list.Count; i++)
                {
                    float v = side == 0 ? list[i].b.min[axis] : list[i].b.max[axis];
                    long k = (long)Mathf.Round(v / .003f);
                    if (!buckets.TryGetValue(k, out var l)) buckets[k] = l = new List<int>();
                    l.Add(i);
                }
                foreach (var l in buckets.Values)
                for (int a = 0; a < l.Count; a++)
                for (int c = a + 1; c < l.Count; c++)
                {
                    var A = list[l[a]]; var B = list[l[c]];
                    if (touched.Contains(A.r) || touched.Contains(B.r) || !A.r.enabled || !B.r.enabled) continue;
                    float va = side == 0 ? A.b.min[axis] : A.b.max[axis], vb = side == 0 ? B.b.min[axis] : B.b.max[axis];
                    if (Mathf.Abs(va - vb) > .0015f) continue;
                    int u = (axis + 1) % 3, w = (axis + 2) % 3;
                    float u0 = Mathf.Max(A.b.min[u], B.b.min[u]), u1 = Mathf.Min(A.b.max[u], B.b.max[u]), w0 = Mathf.Max(A.b.min[w], B.b.min[w]), w1 = Mathf.Min(A.b.max[w], B.b.max[w]);
                    if (u1 - u0 < .03f || w1 - w0 < .03f) continue;
                    if (A.r.sharedMaterial == B.r.sharedMaterial && A.r.sharedMaterial.mainTexture == null) continue; // same flat colour: no visible flicker
                    int visible = 0;
                    for (int s = 0; s < 5; s++)
                    {
                        var p = Vector3.zero; float fu = s == 0 ? .5f : s % 2 == 0 ? .2f : .8f, fw = s == 0 ? .5f : s < 3 ? .2f : .8f;
                        p[u] = Mathf.Lerp(u0, u1, fu); p[w] = Mathf.Lerp(w0, w1, fw); p[axis] = va + (side == 0 ? -.01f : .01f);
                        if (!Hidden(p, A.r, B.r)) visible++;
                    }
                    if (visible < 2) continue;
                    bool duplicate = (A.b.min - B.b.min).magnitude < .006f && (A.b.max - B.b.max).magnitude < .006f;
                    if (duplicate)
                    {
                        Undo.RecordObject(B.r, "Hide duplicate face"); B.r.enabled = false; touched.Add(B.r); hiddenDup++;
                        continue;
                    }
                    // Lift the smaller face's box off the plane, outward along that face's normal.
                    float areaA = A.b.size[u] * A.b.size[w], areaB = B.b.size[u] * B.b.size[w];
                    var mover = areaA <= areaB ? A.r : B.r;
                    var dir = Vector3.zero; dir[axis] = side == 0 ? -1 : 1;
                    Undo.RecordObject(mover.transform, "Lift coplanar face"); mover.transform.position += dir * .003f;
                    touched.Add(mover); lifted++;
                }
            }
            return $"Coplanar faces: {hiddenDup} duplicate renderers hidden (colliders kept), {lifted} boxes lifted 3 mm";
        });

        // World bounds of a slab from its mesh, valid while the slab is hidden (renderer bounds are empty then).
        static Bounds SlabBounds(Transform t)
        {
            var mb = t.GetComponent<MeshFilter>().sharedMesh.bounds; var b = new Bounds(t.TransformPoint(mb.center), Vector3.zero);
            for (int i = 0; i < 8; i++) b.Encapsulate(t.TransformPoint(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            return b;
        }

        static Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;
    }
}
