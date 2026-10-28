using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.WaitingRoom.Editor
{
    public static partial class WaitingRoomLayoutA
    {
        // Rooms that already carry their own structure or must stay as built are left alone.
        static readonly string[] FactoryFinishSkip = { "StageProcessionalRoute", "S06_", "S07_", "Restroom", "S01_Lobby", "LogisticsHall", "S11_", "S12_", "Arena", "GreyboxComposition" };

        // Every waiting-room shell reads as the same factory: cast concrete below the ledger, bare concrete block above,
        // and an exposed steel frame (I-section columns on the walls tied by roof girders across the short span).
        public static string FactoryFinish()
        {
            var (scene, target) = Begin(); var original = Snapshot(source); int undo = StartUndo("Factory finish for waiting-room shells");
            try
            {
                Remove(target, "FactoryFrame"); var g = Group(target, "FactoryFrame", Origin);
                int rooms = 0, columns = 0, girders = 0;
                foreach (var surface in target.GetComponentsInChildren<MeshRenderer>().Where(r => r.name == "WallSurfaces").ToArray())
                {
                    string path = AnimationUtility.CalculateTransformPath(surface.transform, target);
                    if (FactoryFinishSkip.Any(s => path.Contains(s))) continue;
                    // Below ground the block pattern read as brick; the basement gets rough board-cast concrete above the ledger instead.
                    bool basement = path.StartsWith("B1_") || path.Contains("/B1Level/");
                    var upper = basement ? RoughConcrete() : CinderMaterial(false);
                    var mats = surface.sharedMaterials.Select(m => m == null ? m : m.name is "LA_CinderPainted" ? Mat("LA_WallConcrete") : m.name is "LA_WallRust" or "LA_CinderBare" or "LA_ConcreteRough" ? upper : m).ToArray();
                    Undo.RecordObject(surface, "Concrete walls"); surface.sharedMaterials = mats;
                    var room = surface.transform.parent.parent; var b = surface.bounds;
                    if (b.size.y < 2.5f || b.size.x < 1.5f || b.size.z < 1.5f) continue;
                    var rg = Group(g, path.Split('/')[0] + "_" + room.name, room.position);
                    var (c, r) = SteelFrame(target, room, rg, b); columns += c; girders += r; rooms++;
                }
                foreach (var r in g.GetComponentsInChildren<MeshRenderer>()) if (r.bounds.size.magnitude < .6f) r.shadowCastingMode = ShadowCastingMode.Off;
                Physics.SyncTransforms(); AssertUnchanged(original);
                Undo.CollapseUndoOperations(undo); EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
                return $"Factory finish: rooms={rooms} columns={columns} girders={girders}";
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        static Material RoughConcrete()
        {
            const string name = "LA_ConcreteRough";
            if (Materials.TryGetValue(name, out var cached) && cached != null) return cached;
            var m = Lit(name, Hex("8A847C"), .08f, FactoryTex("DamagedConcrete/T_DamagedConcrete_BC.TGA"), FactoryTex("DamagedConcrete/T_DamagedConcrete_N.TGA"));
            Materials[name] = m; return m;
        }

        static (int, int) SteelFrame(Transform target, Transform room, Transform g, Bounds b)
        {
            float y0 = b.min.y, h = b.size.y, x0 = b.min.x, x1 = b.max.x, z0 = b.min.z, z1 = b.max.z;
            // Door reveals of this room and of neighbours that open into it (lobby portals, shared walls).
            var near = b; near.Expand(new Vector3(1f, 0, 1f));
            var doors = target.GetComponentsInChildren<Collider>().Where(c => c.name == "DoorReveal" && c.bounds.Intersects(near)).Select(c => c.bounds).ToList();
            // Pair the two reveals of each opening so the span between them is kept clear too, not only its edges.
            foreach (var (a, c) in doors.SelectMany((a, i) => doors.Skip(i + 1).Select(c => (a, c))).ToArray())
                if (Vector3.Distance(a.center, c.center) < 6.5f && (Mathf.Abs(a.center.x - c.center.x) < .3f || Mathf.Abs(a.center.z - c.center.z) < .3f))
                { var opening = a; opening.Encapsulate(c); doors.Add(opening); }
            // Anything already standing in the room (furniture, pipes, lamps, generated props) blocks a column spot.
            var area = b; area.Expand(.3f);
            var obstacles = new List<Bounds>();
            foreach (var root in new[] { room, target.Find("EnvironmentDetail"), target.Find("CircusProps"), target.Find("SupportFinishing") })
                if (root != null)
                    foreach (var r in root.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !(r is ParticleSystemRenderer)))
                    {
                        var rb = r.bounds; if (!rb.Intersects(area) || rb.size.magnitude > 12f) continue;
                        var p = AnimationUtility.CalculateTransformPath(r.transform, target);
                        if (p.Contains("/Shell/") || r.name.Contains("Decal") || r.name.Contains("Stain") || r.name.Contains("Puddle") || rb.size.y < .03f) continue;
                        obstacles.Add(rb);
                    }
            var steel = Mat("LA_SteelPlate"); var dark = Mat("V4_Iron_Dark");
            const float flange = .24f, depth = .22f, tf = .025f, web = .014f;
            int columns = 0, girders = 0;
            var placed = new List<(float along, bool longWall, float side)>(); var frames = new List<Vector3>();
            bool spanX = x1 - x0 >= z1 - z0; // long walls run along x when the room is wider than deep
            foreach (var (alongX, at, inward) in new[] { (true, z0, 1f), (true, z1, -1f), (false, x0, 1f), (false, x1, -1f) })
            {
                float a0 = alongX ? x0 : z0, a1 = alongX ? x1 : z1, len = a1 - a0;
                int n = Mathf.Max(1, Mathf.RoundToInt(len / 4f));
                for (int i = 0; i <= n; i++)
                {
                    float a = Mathf.Lerp(a0 + .15f + flange * .5f, a1 - .15f - flange * .5f, i / (float)n);
                    Bounds? spot = null; float chosen = a;
                    foreach (var shiftBy in new[] { 0f, .5f, -.5f, .9f, -.9f })
                    {
                        float s = Mathf.Clamp(a + shiftBy, a0 + .15f + flange * .5f, a1 - .15f - flange * .5f);
                        var centre = alongX ? new Vector3(s, y0 + h * .5f, at + inward * depth * .5f) : new Vector3(at + inward * depth * .5f, y0 + h * .5f, s);
                        var size = alongX ? new Vector3(flange, h, depth) : new Vector3(depth, h, flange);
                        var test = new Bounds(centre, size); test.Expand(new Vector3(.02f, -.06f, .02f));
                        var doorTest = test; doorTest.Expand(new Vector3(3.2f, 0, 3.2f)); // keep the 1.5 m doorway approach clear
                        if (doors.Any(d => d.Intersects(doorTest)) || obstacles.Any(o => o.Intersects(test))) continue;
                        if (frames.Any(f => Vector2.Distance(new Vector2(f.x, f.z), new Vector2(centre.x, centre.z)) < .6f)) continue; // corners: one column, not two
                        spot = new Bounds(centre, size); chosen = s; break;
                    }
                    if (spot == null) continue;
                    IColumn(g, spot.Value, alongX, inward, steel, dark, flange, depth, tf, web); frames.Add(spot.Value.center);
                    placed.Add((chosen, alongX == spanX, at)); columns++;
                }
            }
            // Roof girders across the short span, at the long walls' column stations, tight under the ceiling.
            float span = spanX ? z1 - z0 : x1 - x0;
            if (span < 13f)
                foreach (var a in placed.Where(p => p.longWall && Mathf.Approximately(p.side, spanX ? z0 : x0)).Select(p => p.along))
                {
                    if (!placed.Any(p => p.longWall && !Mathf.Approximately(p.side, spanX ? z0 : x0) && Mathf.Abs(p.along - a) < 1.2f)) continue;
                    float gy = y0 + h - .14f;
                    var centre = spanX ? new Vector3(a, gy, (z0 + z1) * .5f) : new Vector3((x0 + x1) * .5f, gy, a);
                    var test = new Bounds(centre, spanX ? new Vector3(flange, .26f, span) : new Vector3(span, .26f, flange));
                    var shrunk = test; shrunk.Expand(new Vector3(-.02f, -.04f, -.5f));
                    if (obstacles.Any(o => o.Intersects(shrunk))) continue;
                    Box(g, "RoofGirderWeb", centre, spanX ? new Vector3(web, .26f, span) : new Vector3(span, .26f, web), steel);
                    foreach (var dy in new[] { -.12f, .12f }) Box(g, "RoofGirderFlange", centre + Vector3.up * dy, spanX ? new Vector3(flange * .8f, tf, span) : new Vector3(span, tf, flange * .8f), steel);
                    girders++;
                }
            return (columns, girders);
        }

        // An I-section column standing against a wall: flanges parallel to the wall, a web between, a base plate and cap.
        static void IColumn(Transform g, Bounds b, bool alongX, float inward, Material steel, Material dark, float flange, float depth, float tf, float web)
        {
            var c = b.center; float h = b.size.y;
            Vector3 In(float d) => alongX ? new Vector3(0, 0, inward * d) : new Vector3(inward * d, 0, 0);
            Vector3 Face(float thick) => alongX ? new Vector3(flange, h, thick) : new Vector3(thick, h, flange);
            Box(g, "ColumnFlange", c + In(-depth * .5f + tf * .5f), Face(tf), steel);
            Box(g, "ColumnFlange", c + In(depth * .5f - tf * .5f), Face(tf), steel);
            Box(g, "ColumnWeb", c, alongX ? new Vector3(web, h, depth - tf * 2) : new Vector3(depth - tf * 2, h, web), steel);
            var foot = new Vector3(c.x, b.min.y + .01f, c.z) + In(.03f);
            Box(g, "ColumnBasePlate", foot, alongX ? new Vector3(flange + .14f, .02f, depth + .08f) : new Vector3(depth + .08f, .02f, flange + .14f), dark);
            foreach (var y in new[] { .9f, h * .55f }) Box(g, "ColumnStiffener", new Vector3(c.x, b.min.y + y, c.z), alongX ? new Vector3(flange - .02f, .012f, depth - .06f) : new Vector3(depth - .06f, .012f, flange - .02f), dark);
        }
    }
}
