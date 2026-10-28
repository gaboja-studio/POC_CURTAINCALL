using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.WaitingRoom.Editor
{
    public static partial class WaitingRoomLayoutA
    {
        static readonly Color CorridorTungsten = new(1f, .97f, .93f), TubeWhite = new(.9f, .95f, 1f), WhiteLight = new(1f, .97f, .93f);

        // Service corridors and the basement after the reference photos: concrete below, bare block above, diamond-plate runners,
        // small pipes on brackets with drops and valves, rusty posts, caged lamps; the plant gets steam,
        // overhead mains and an old boiler under a fluorescent tube; the sealed exit gets rivets, a padlock and a dead LED clock.
        public static string DetailCorridorsAndBasement()
        {
            var (scene, target) = Begin(); var original = Snapshot(source); int undo = StartUndo("Detail service corridors and basement");
            try
            {
                Remove(target, "EnvironmentDetail"); var env = Group(target, "EnvironmentDetail", Origin);
                int walls = CinderWalls(target);
                // (side, centre along wall, width): W/E walls run along z, S/N walls along x.
                UtilityCorridor(Group(env, "OfficeLink", Origin), -27.8f, -25.8f, 66.5f, 70.4f, -5f, 4.6f, new[] { ('W', 67.5f, 2f), ('N', -26.8f, 2f) }, 'E', true, 3f, .25f);
                UtilityCorridor(Group(env, "EscapeCorridor", Origin), -18.2f, -16f, 65.9f, 70.9f, -10f, 4.8f, new[] { ('S', -17.1f, 2f), ('N', -17.1f, 2f) }, 'W', true, 5f, .22f); // the hidden way out stays dim
                UtilityCorridor(Group(env, "EscapeEntrance", Origin), -23.6f, -15f, 60.9f, 65.4f, -10f, 4.8f, new[] { ('N', -17.1f, 2f), ('S', -19.3f, 3.2f) }, 'N', true, 4f, .25f);
                StairwellRisers(Group(env, "Stairwell", Origin));
                BasementRooms(Group(env, "BasementRooms", Origin));
                PlantOverhead(Group(env, "PlantOverhead", Origin));
                OldBoiler(Group(env, "OldBoiler", Origin), new Vector3(-20f, -10f, 96.7f));
                SealedExitDetail(Group(env, "SealedExit", Origin), target);
                // Service water and drain lines in the reference are grey galvanised steel; only the plant's steam work stays rusty.
                var galv = new[] { "WallPipe", "PipeCap", "RiserDrop", "ValveBody", "ShaftRiser", "LandingBranch", "CeilingMain", "FeedPipe" };
                foreach (var r in env.GetComponentsInChildren<MeshRenderer>().Where(r => galv.Contains(r.name))) r.sharedMaterial = GalvanisedMaterial();
                foreach (var r in env.GetComponentsInChildren<MeshRenderer>()) if (r.bounds.size.magnitude < .6f) r.shadowCastingMode = ShadowCastingMode.Off;
                Physics.SyncTransforms(); AssertUnchanged(original);
                Undo.CollapseUndoOperations(undo); EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
                return $"Corridors and basement detailed; cinder walls={walls}; objects={env.GetComponentsInChildren<Transform>().Length}; lights={env.GetComponentsInChildren<Light>().Length}";
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        // Lights are scaled from a stored baseline so the pass can be re-run without compounding.
        // The stage route gets an extra lift so the walk lobby -> 06 -> 07 brightens as the tone guide asks.
        public static string LiftLighting(float all = 2f, float stageRoute = 1.6f, float ambient = 2.4f)
        {
            var (scene, target) = Begin(); int undo = StartUndo("Lift layout A lighting");
            string path = $"{AssetDir}/LA_LightingBaseline.json";
            var baseline = File.Exists(path) ? Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, float>>(File.ReadAllText(path)) : new Dictionary<string, float>();
            var lights = target.GetComponentsInChildren<Light>(true);
            foreach (var l in lights)
            {
                string id = GlobalObjectId.GetGlobalObjectIdSlow(l).ToString();
                if (!baseline.TryGetValue(id, out var b)) baseline[id] = b = l.intensity;
                bool route = new[] { "FacilityConnections/StageProcessionalRoute", "S06_StageCorridor", "S07_StageEntrance" }.Any(p => target.Find(p) is Transform r && l.transform.IsChildOf(r));
                Undo.RecordObject(l, "Lift light"); l.intensity = b * all * (route ? stageRoute : 1f);
                // White working light everywhere; only strongly coloured signal lights (red spill, LED, beacons) keep their colour.
                Color.RGBToHSV(l.color, out _, out var sat, out _);
                if (sat < .75f) l.color = WhiteLight;
            }
            // Warm-glass bulbs on the copied lamps read orange next to white light; give them the (now white) layout bulb.
            var bulb = Mat("LA_Bulb");
            foreach (var r in target.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials; bool changed = false;
                for (int i = 0; i < mats.Length; i++) if (mats[i] != null && mats[i].name is "V4_Bulb_Warm" or "Bulb_Emissive") { mats[i] = bulb; changed = true; }
                if (changed) { Undo.RecordObject(r, "White bulb"); r.sharedMaterials = mats; }
            }
            if (!baseline.ContainsKey("ambient.sky"))
            {
                baseline["ambient.sky"] = 0; // marker: ambient colours below are the originals
                foreach (var (k, c) in new[] { ("sky", RenderSettings.ambientSkyColor), ("equator", RenderSettings.ambientEquatorColor), ("ground", RenderSettings.ambientGroundColor) })
                { baseline[$"ambient.{k}.r"] = c.r; baseline[$"ambient.{k}.g"] = c.g; baseline[$"ambient.{k}.b"] = c.b; }
            }
            Color Amb(string k) => new Color(baseline[$"ambient.{k}.r"], baseline[$"ambient.{k}.g"], baseline[$"ambient.{k}.b"]) * ambient;
            RenderSettings.ambientSkyColor = Amb("sky"); RenderSettings.ambientEquatorColor = Amb("equator"); RenderSettings.ambientGroundColor = Amb("ground");
            File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(baseline, Newtonsoft.Json.Formatting.Indented));
            AssetDatabase.ImportAsset(path);
            Undo.CollapseUndoOperations(undo); EditorSceneManager.MarkSceneDirty(scene);
            return $"Lights lifted x{all} (stage route x{all * stageRoute}), ambient x{ambient}; lights={lights.Length}";
        }

        // ---------- corridors ----------

        static int CinderWalls(Transform target)
        {
            int n = 0;
            foreach (var room in new[] { "WC_WestCorridor", "S09_PracticeRoom/Corridor", "B1_OfficeLink", "B1_EscapeCorridor", "B1_EscapeEntrance", "ST_Stairwell" })
            {
                var r = target.Find(room); if (r == null) continue;
                foreach (var surface in r.GetComponentsInChildren<MeshRenderer>().Where(m => m.name == "WallSurfaces"))
                {
                    var mats = surface.sharedMaterials.Select(m => m == null ? m : m.name is "LA_CinderPainted" ? Mat("LA_WallConcrete") : m.name is "LA_WallRust" or "LA_CinderBare" ? CinderMaterial(false) : m).ToArray();
                    Undo.RecordObject(surface, "Cinder block walls"); surface.sharedMaterials = mats; n++;
                }
            }
            return n;
        }

        static void UtilityCorridor(Transform g, float x0, float x1, float z0, float z1, float y, float h, (char side, float at, float width)[] doors, char pipeSide, bool lowPipes, float lampEvery, float lamp)
        {
            bool alongX = x1 - x0 > z1 - z0;
            float a0 = alongX ? x0 : z0, a1 = alongX ? x1 : z1, mid = alongX ? (z0 + z1) * .5f : (x0 + x1) * .5f, span = alongX ? z1 - z0 : x1 - x0;
            Vector3 P(float along, float across, float up) => alongX ? new Vector3(along, y + up, across) : new Vector3(across, y + up, along);
            // Keep hand-height fittings out of every doorway's approach (the validator's 1.5 m clearance box), whichever wall it is on.
            bool NearDoor(float along) => doors.Any(d =>
            {
                bool end = alongX ? d.side is 'W' or 'E' : d.side is 'S' or 'N';
                float endAt = d.side switch { 'W' => x0, 'E' => x1, 'S' => z0, _ => z1 };
                return end ? Mathf.Abs(along - endAt) < 1.9f : Mathf.Abs(along - d.at) < d.width * .5f + 1f;
            });
            // Diamond-plate runner down the middle, stopping short of the end walls.
            float runner = Mathf.Min(1.3f, span - .9f);
            var plate = new MeshBuilder("LA_Runner_" + g.name, 1);
            Vector3 c0 = P(a0 + .3f, mid - runner * .5f, .012f), c1 = P(a1 - .3f, mid + runner * .5f, .012f);
            plate.Quad(0, new Vector3(c0.x, c0.y, c0.z), new Vector3(c0.x, c0.y, c1.z), new Vector3(c1.x, c0.y, c1.z), new Vector3(c1.x, c0.y, c0.z), Vector3.up, 1f);
            var runnerGo = MeshObject(g, "DiamondPlateRunner", Vector3.zero, SaveMesh(plate.Build()), DiamondPlateMaterial());
            runnerGo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            foreach (var edge in new[] { -1f, 1f })
                Box(g, "RunnerEdge", P((a0 + a1) * .5f, mid + edge * runner * .5f, .015f), alongX ? new Vector3(a1 - a0 - .6f, .03f, .04f) : new Vector3(.04f, .03f, a1 - a0 - .6f), Mat("V4_Iron_Dark"));

            // Pipe wall: wall plane and inward direction.
            float wallAcross = pipeSide switch { 'S' => z0, 'N' => z1, 'W' => x0, _ => x1 };
            float inward = pipeSide is 'S' or 'W' ? 1f : -1f;
            var gaps = doors.Where(d => d.side == pipeSide).Select(d => (d.at - d.width * .5f - .25f, d.at + d.width * .5f + .25f)).ToList();
            var runs = Segments(a0 + .15f, a1 - .15f, gaps);
            var heights = (lowPipes ? new[] { (.38f, .045f), (.72f, .06f) } : new (float, float)[0]).Concat(new[] { (h - .55f, .09f), (h - .8f, .05f) }).ToArray();
            foreach (var (s0, s1) in runs)
            {
                if (s1 - s0 < .4f) continue;
                foreach (var (py, pr) in heights)
                {
                    float off = .1f + pr;
                    Pipe(g, P((s0 + s1) * .5f, wallAcross + inward * off, py), alongX ? Vector3.right : Vector3.forward, s1 - s0, pr).name = "WallPipe";
                    foreach (var end in new[] { s0, s1 }) Pipe(g, P(end, wallAcross + inward * off, py), alongX ? Vector3.right : Vector3.forward, .06f, pr * 1.35f).name = "PipeCap";
                }
                for (float b = s0 + .5f; b < s1 - .2f; b += 1.6f)
                    foreach (var (py, pr) in heights)
                        Box(g, "PipeBracket", P(b, wallAcross + inward * .06f, py), alongX ? new Vector3(.05f, .05f, .14f + pr * 2) : new Vector3(.14f + pr * 2, .05f, .05f), Mat("V4_Iron_Dark"));
                // Drops from the high pipe to a valve at hand height, like the reference's branching risers.
                for (float d = s0 + 1.4f; d < s1 - .8f; d += 4.3f)
                {
                    if (NearDoor(d)) continue;
                    float off = .1f + .05f, top = h - .55f, bottom = lowPipes ? .72f : 1.1f;
                    Pipe(g, P(d, wallAcross + inward * (off + .12f), (top + bottom) * .5f), Vector3.up, top - bottom, .045f).name = "RiserDrop";
                    var wheel = Prim(g, PrimitiveType.Cylinder, "ValveWheel", P(d, wallAcross + inward * (off + .27f), 1.35f), new Vector3(.24f, .015f, .24f), Mat("LA_HazardPaint"));
                    wheel.rotation = Quaternion.FromToRotation(Vector3.up, alongX ? new Vector3(0, 0, inward) : new Vector3(inward, 0, 0));
                    Prim(g, PrimitiveType.Cylinder, "ValveBody", P(d, wallAcross + inward * (off + .12f), 1.35f), new Vector3(.12f, .08f, .12f), Mat("V4_RustyMetal"));
                }
            }
            // Rusty posts on the opposite wall, away from doors.
            char opposite = pipeSide switch { 'S' => 'N', 'N' => 'S', 'W' => 'E', _ => 'W' };
            float oppAcross = opposite switch { 'S' => z0, 'N' => z1, 'W' => x0, _ => x1 }, oppIn = opposite is 'S' or 'W' ? 1f : -1f;
            var oppGaps = doors.Where(d => d.side == opposite).Select(d => (d.at - d.width * .5f - .5f, d.at + d.width * .5f + .5f)).ToList();
            for (float p = a0 + 1.2f; p < a1 - .6f; p += 3.6f)
            {
                if (oppGaps.Any(gp => p > gp.Item1 && p < gp.Item2)) continue;
                Box(g, "RustyPost", P(p, oppAcross + oppIn * .1f, h * .5f), new Vector3(.18f, h, .18f), Mat("V4_RustyMetal"));
                Box(g, "PostFoot", P(p, oppAcross + oppIn * .12f, .1f), new Vector3(.3f, .2f, .3f), Mat("V4_Iron_Dark"));
            }
            // Caged tungsten lamps down the centre.
            int count = Mathf.Max(1, Mathf.RoundToInt((a1 - a0) / lampEvery));
            for (int i = 0; i < count; i++)
            {
                float at = Mathf.Lerp(a0, a1, (i + .5f) / count);
                TrimCord(CagedLamp(g, P(at, mid + (alongX ? .2f : -.2f), h - 1.3f), lamp, CorridorTungsten), y + h);
            }
            // Grime: floor stains along the walls, a few leaning boards and a bucket in a corner.
            var rng = new System.Random(Mathf.RoundToInt(a0 * 13 + mid * 7));
            for (float p = a0 + .8f; p < a1 - .5f; p += 2.4f + (float)rng.NextDouble())
            {
                var at = P(p, wallAcross + inward * .45f, .014f);
                var q = Prim(g, PrimitiveType.Quad, "WallFootStain", at, new Vector3(1.3f, .7f, 1), Mat(rng.Next(3) == 0 ? "LA_PuddleDecal" : "LA_RustDecal"));
                q.rotation = Quaternion.Euler(90, alongX ? 0 : 90, 0); q.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            // The corner clutter needs its whole 1 m spread clear of doorways.
            var corner = P(a1 - .45f, oppAcross + oppIn * .4f, 0);
            if (!NearDoor(a1 - .45f) && !NearDoor(a1 - 1.35f))
            {
                for (int i = 0; i < 2; i++)
                {
                    var board = Box(g, "LeaningBoard", corner + new Vector3(0, .9f, 0) + (alongX ? Vector3.left : Vector3.back) * (i * .25f), new Vector3(.18f, 1.8f, .03f), Mat("V4_Wood_Old"));
                    board.rotation = alongX ? Quaternion.Euler(-12 * oppIn, 90, 0) : Quaternion.Euler(12 * oppIn, 0, 0);
                }
                Prim(g, PrimitiveType.Cylinder, "Bucket", corner + (alongX ? Vector3.left : Vector3.back) * .9f + Vector3.up * .17f, new Vector3(.3f, .17f, .3f), Mat("LA_SteelPlate"));
            }
        }

        // The copied lobby lamp has a 5.6 m cord for the 13 m hall; in lower rooms it pierced the ceiling into the space above.
        static Transform TrimCord(Transform lamp, float ceiling)
        {
            if (Physics.Raycast(lamp.position + Vector3.up * .3f, Vector3.up, out var hit, 12f, ~0, QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(lamp))
                ceiling = Mathf.Min(ceiling, hit.point.y);
            foreach (var wire in lamp.GetComponentsInChildren<Renderer>().Where(r => r.name == "Wire"))
            {
                var b = wire.bounds; if (b.max.y <= ceiling + .01f) continue;
                float keep = Mathf.Max(.05f, ceiling - b.min.y), k = keep / b.size.y; var t = wire.transform;
                t.localScale = new Vector3(t.localScale.x, t.localScale.y * k, t.localScale.z);
                t.position += Vector3.up * (b.min.y + keep * .5f - wire.bounds.center.y);
            }
            return lamp;
        }

        static List<(float, float)> Segments(float from, float to, List<(float, float)> gaps)
        {
            var result = new List<(float, float)>(); float cursor = from;
            foreach (var (g0, g1) in gaps.OrderBy(x => x.Item1)) { if (g0 > cursor) result.Add((cursor, Mathf.Min(g0, to))); cursor = Mathf.Max(cursor, g1); }
            if (cursor < to) result.Add((cursor, to));
            return result;
        }

        static void StairwellRisers(Transform g)
        {
            // A riser bundle runs the full shaft in the north-west corner, branching at each landing.
            float x = -32.75f, z = 87f;
            for (int i = 0; i < 3; i++)
            {
                var p = new Vector3(x + i * .22f, 0f, z - .05f - (i % 2) * .08f);
                Pipe(g, p, Vector3.up, 10f, .06f + i * .015f).name = "ShaftRiser";
                for (float yy = -4.6f; yy < 4.8f; yy += 1.8f) Box(g, "RiserClamp", new Vector3(x + i * .22f, yy, z - .02f), new Vector3(.18f, .05f, .12f), Mat("V4_Iron_Dark"));
            }
            foreach (var yy in new[] { -.4f, 4.4f }) Pipe(g, new Vector3(x + .9f, yy, z - .12f), Vector3.right, 1.6f, .06f).name = "LandingBranch";
            TrimCord(CagedLamp(g, new Vector3(-30.4f, -.9f, 76f), .7f, CorridorTungsten), 5f); // shaft ceiling is at 5 m
            TrimCord(CagedLamp(g, new Vector3(-30.4f, 3.7f, 84f), .7f, CorridorTungsten), 5f);
        }

        static void BasementRooms(Transform g)
        {
            // Office (-35..-27.8 x, 63.7..69.6 z) and changing room (-33.6..-24.4, 55.9..63.2): overhead service runs and a tube light.
            foreach (var (x0, x1, z, top) in new[] { (-34.8f, -28f, 69.1f, -1f), (-33.4f, -24.6f, 56.4f, -.9f) })
            {
                Pipe(g, new Vector3((x0 + x1) * .5f, top, z), Vector3.right, x1 - x0, .11f).name = "CeilingMain";
                Pipe(g, new Vector3((x0 + x1) * .5f, top - .05f, z + (z > 60 ? -.32f : .32f)), Vector3.right, x1 - x0, .06f).name = "CeilingMain";
                for (float x = x0 + .6f; x < x1; x += 1.8f) SupportLine(g, new Vector3(x, top, z), new Vector3(x, top + .6f, z), "PipeHanger", .012f, Mat("V4_Iron_Dark"));
            }
            Tube(g, new Vector3(-31.4f, -1.25f, 66.6f), 90, 4f, -.4f);
            Tube(g, new Vector3(-29f, -1.25f, 59.6f), 0, 4f, -.4f);
        }

        // ---------- plant ----------

        static void PlantOverhead(Transform g)
        {
            // Two big mains cross the plant above the catwalk, hung from the slab, with ring flanges at the joints.
            foreach (var z in new[] { 78.7f, 80.6f })
            {
                for (float x = -27.75f + 4.04f; x <= 9.9f; x += 4.04f)
                {
                    PlaceProp(LeartesProps + "SM_Pipe_Big_01", g, new Vector3(x, -1.35f, z), 0, false);
                    PlaceProp(LeartesProps + "SM_Pipe_Big_Ring_01", g, new Vector3(x, -1.35f, z), 0, false);
                    SupportLine(g, new Vector3(x - 2f, -.72f, z), new Vector3(x - 2f, -.2f, z), "MainHanger", .03f, Mat("V4_Iron_Dark"));
                }
            }
            // Drops feeding the pit machinery: one is cracked and leaks steam.
            foreach (var (x, z) in new[] { (-9f, 79.6f), (2.5f, 79.6f) })
                Pipe(g, new Vector3(x, -5.6f, z), Vector3.up, 8.8f, .2f).name = "PlantDrop";
            // Every plume leaves a visible source: a weeping flange on each drop, a joint ring on the main,
            // and two low vent stubs beside the machines (no steam straight out of bare concrete).
            foreach (var (x, y, dir) in new[] { (-9f, -6.2f, Vector3.forward), (2.5f, -7.4f, Vector3.left) })
            {
                Prim(g, PrimitiveType.Cylinder, "LeakingFlange", new Vector3(x, y, 79.6f), new Vector3(.58f, .05f, .58f), Mat("V4_RustyMetal"));
                Steam(g, new Vector3(x, y, 79.6f) + dir * .3f, .8f, dir + Vector3.up * .35f);
            }
            Steam(g, new Vector3(-11.5f, -2f, 78.7f), .9f, Vector3.down + Vector3.back * .4f);
            foreach (var (x, z) in new[] { (-12.5f, 81.2f), (5.5f, 73.8f) })
            {
                Box(g, "VentGrate", new Vector3(x, -9.985f, z), new Vector3(.7f, .03f, .7f), Mat("V4_Iron_Dark"));
                Pipe(g, new Vector3(x, -9.6f, z), Vector3.up, .8f, .12f).name = "VentStub";
                Prim(g, PrimitiveType.Cylinder, "VentLip", new Vector3(x, -9.2f, z), new Vector3(.32f, .03f, .32f), Mat("V4_RustyMetal"));
                Steam(g, new Vector3(x, -9.15f, z), 1.2f, Vector3.up);
            }
            // Cold tube lights along the catwalk edge, in addition to the warm maintenance pools.
            foreach (var x in new[] { -24f, -18.5f, -13f, -5f, 3f })
                Tube(g, new Vector3(x, -1.05f, 77.4f), 0, 3.2f, -.2f);
        }

        static void Steam(Transform g, Vector3 at, float scale, Vector3 direction)
        {
            // A thin rising plume of soft puffs (the pack's fog prefabs are room-wide ground fog, far too heavy here).
            var go = new GameObject("SteamVent"); go.transform.SetParent(g, true); go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(direction)); // the cone emits along +Z
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main; main.duration = 4; main.loop = true; main.prewarm = true; main.maxParticles = 60;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f); main.startSpeed = new ParticleSystem.MinMaxCurve(.9f * scale, 1.6f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(.35f * scale, .7f * scale); main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.startColor = new Color(.86f, .87f, .88f, 1); main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = -.02f;
            var emission = ps.emission; emission.rateOverTime = 10;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 9; shape.radius = .08f * scale;
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .6f, 1, 3f));
            var color = ps.colorOverLifetime; color.enabled = true;
            var fade = new Gradient(); fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.22f, .2f), new GradientAlphaKey(.1f, .7f), new GradientAlphaKey(0, 1) });
            color.color = fade;
            var drift = ps.velocityOverLifetime; drift.enabled = true; drift.space = ParticleSystemSimulationSpace.World;
            drift.x = new ParticleSystem.MinMaxCurve(-.15f, .15f); drift.y = new ParticleSystem.MinMaxCurve(0, 0); drift.z = new ParticleSystem.MinMaxCurve(-.15f, .15f);
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = Mat("V4_FogParticle"); r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        }

        static void Tube(Transform g, Vector3 at, float yaw, float intensity, float ceiling)
        {
            var t = PlaceProp(SyntyProps + "SM_Prop_Light_Flurecent_01", g, at, yaw, false); t.name = "TubeLight";
            // The prefab's pivot sits at one end of the fitting; shift so the fixture centres on `at`.
            t.position += t.rotation * new Vector3(1.17f, 0, 0);
            foreach (var x in new[] { -.8f, .8f })
            {
                var top = at + Quaternion.Euler(0, yaw, 0) * new Vector3(x, 0, 0);
                SupportLine(g, top, new Vector3(top.x, ceiling, top.z), "TubeChain", .01f, Mat("V4_Iron_Dark"));
            }
            Point(g, "TubeGlow", at + Vector3.down * .5f, TubeWhite, intensity, 8.5f);
        }

        // Old riveted boiler after the reference: rounded shoulders, two upper fire doors, a black arch,
        // a rust-orange lower plate, flue to the slab, a pigeonhole rack and a grey cabinet beside it.
        static void OldBoiler(Transform g, Vector3 c)
        {
            var body = Mat("LA_SteelPlate"); var dark = Mat("V4_Iron_Dark");
            Box(g, "BoilerPlinth", c + new Vector3(0, .2f, 0), new Vector3(2.7f, .4f, 2.1f), dark, true);
            Box(g, "BoilerBody", c + new Vector3(0, 1.35f, 0), new Vector3(2.4f, 1.9f, 1.9f), body, true);
            Prim(g, PrimitiveType.Cylinder, "BoilerCrown", c + new Vector3(0, 2.3f, 0), new Vector3(1.9f, 1.2f, 1.9f), body).rotation = Quaternion.Euler(0, 0, 90);
            foreach (var y in new[] { .55f, 1.3f, 2.05f }) Box(g, "RivetBand", c + new Vector3(0, y, 0), new Vector3(2.44f, .05f, 1.94f), dark);
            float front = c.z - .97f;
            foreach (var x in new[] { -.6f, .6f })
            {
                Box(g, "FireDoor", new Vector3(c.x + x, c.y + 2.35f, front), new Vector3(.85f, .62f, .06f), dark);
                Box(g, "DoorLabel", new Vector3(c.x + x, c.y + 2.45f, front - .035f), new Vector3(.5f, .07f, .01f), Mat("LA_Bronze"));
                Box(g, "DoorLatch", new Vector3(c.x + x + (x < 0 ? .36f : -.36f), c.y + 2.3f, front - .05f), new Vector3(.04f, .14f, .04f), Mat("V4_RustyMetal"));
            }
            Box(g, "FireboxMouth", new Vector3(c.x, c.y + 1.62f, front + .02f), new Vector3(1.05f, .58f, .04f), Mat("V4_Black"));
            Box(g, "FireboxEmbers", new Vector3(c.x, c.y + 1.42f, front + .01f), new Vector3(.8f, .1f, .02f), Mat("V4_Glow_Red"));
            Box(g, "AshPlate", new Vector3(c.x, c.y + .82f, front - .02f), new Vector3(1.15f, 1.15f, .04f), Mat("LA_RackOrange"));
            var flue = new Vector3(c.x + .4f, c.y + 2.9f, c.z + .3f);
            Prim(g, PrimitiveType.Cylinder, "FlueCollar", flue, new Vector3(.75f, .12f, .75f), dark);
            Pipe(g, new Vector3(flue.x, (flue.y + -.2f) * .5f, flue.z), Vector3.up, -.2f - flue.y, .3f).name = "Flue";
            Pipe(g, new Vector3(c.x - .9f, c.y + 2.2f, c.z - .2f), Vector3.up, 1.4f, .06f).name = "FeedPipe";
            Pipe(g, new Vector3(c.x - 1.05f, c.y + 2.9f, c.z), Vector3.right, .5f, .06f).name = "FeedPipe";
            Steam(g, new Vector3(c.x - 1.32f, c.y + 2.95f, c.z), .45f, Vector3.up); // relief valve wisp
            PlaceProp(LeartesProps + "SM_Pipe_Small_Meter", g, new Vector3(c.x - .9f, c.y + 1.9f, c.z - .3f), 180, false);
            // Pigeonhole rack (left) and grey cabinet with stacked cartons (right).
            var rack = new Vector3(c.x - 2.05f, c.y, c.z - .2f);
            Box(g, "RackBack", rack + new Vector3(0, .95f, .2f), new Vector3(1f, 1.9f, .03f), dark, true);
            for (int i = 0; i <= 5; i++) Box(g, "RackStile", rack + new Vector3(-.5f + i * .2f, .95f, 0), new Vector3(.02f, 1.9f, .4f), dark);
            for (int j = 0; j <= 8; j++) Box(g, "RackShelf", rack + new Vector3(0, .05f + j * .23f, 0), new Vector3(1f, .02f, .4f), dark);
            var cab = new Vector3(c.x + 2.15f, c.y, c.z - .3f);
            Box(g, "GreyCabinet", cab + new Vector3(0, .75f, 0), new Vector3(1.3f, 1.5f, .8f), Mat("LA_SteelPlate"), true);
            for (int i = 0; i < 3; i++) Box(g, "Carton", cab + new Vector3(-.2f + i * .08f, 1.62f + i * .2f, -.05f), new Vector3(.7f, .2f, .5f), Mat("LA_Paper"));
            Tube(g, new Vector3(c.x, c.y + 4.2f, c.z - 1.1f), 0, 5f, -.2f);
            var q = Prim(g, PrimitiveType.Quad, "SootStain", new Vector3(c.x, c.y + .015f, c.z - 1.5f), new Vector3(2.6f, 1.4f, 1), Mat("LA_RustDecal"));
            q.rotation = Quaternion.Euler(90, 0, 0); q.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // ---------- sealed exit ----------

        static void SealedExitDetail(Transform g, Transform target)
        {
            var door = target.Find("B1_EscapeEntrance/SealedDoor/EscapeDoor"); if (door == null) return;
            var b = door.GetComponent<Renderer>().bounds; float face = b.max.z + .006f;
            var rivet = Mat("V4_Iron_Dark");
            for (float x = b.min.x + .1f; x <= b.max.x - .09f; x += .2f)
                foreach (var y in new[] { b.min.y + .1f, b.max.y - .1f }) Prim(g, PrimitiveType.Sphere, "Rivet", new Vector3(x, y, face), Vector3.one * .035f, rivet);
            for (float y = b.min.y + .3f; y <= b.max.y - .29f; y += .2f)
                foreach (var x in new[] { b.min.x + .1f, b.max.x - .1f }) Prim(g, PrimitiveType.Sphere, "Rivet", new Vector3(x, y, face), Vector3.one * .035f, rivet);
            var window = new Vector3(b.center.x - .55f, b.max.y - .75f, face);
            Box(g, "PeepFrame", window, new Vector3(.42f, .58f, .03f), Mat("V4_RustyMetal"));
            Box(g, "PeepGlass", window + Vector3.forward * .016f, new Vector3(.32f, .48f, .005f), Mat("V4_Black"));
            // Hasp on the latch edge with a padlock and a short chain to a wall eye.
            var hasp = new Vector3(b.max.x - .14f, b.center.y - .15f, face + .02f);
            Box(g, "HaspPlate", hasp, new Vector3(.08f, .28f, .02f), rivet);
            Box(g, "HaspStaple", hasp + new Vector3(.17f, 0, .01f), new Vector3(.12f, .1f, .03f), rivet);
            Box(g, "PadlockBody", hasp + new Vector3(.17f, -.13f, .03f), new Vector3(.1f, .12f, .045f), Mat("LA_Bronze"));
            foreach (var x in new[] { -.03f, .03f }) Prim(g, PrimitiveType.Cylinder, "Shackle", hasp + new Vector3(.17f + x, -.04f, .03f), new Vector3(.014f, .05f, .014f), Mat("V4_RustyMetal"));
            Vector3 from = hasp + new Vector3(.17f, -.18f, .04f), to = new Vector3(b.max.x + .45f, b.center.y - .9f, 60.95f);
            int links = 9;
            for (int i = 0; i < links; i++)
            {
                var p = Vector3.Lerp(from, to, (i + .5f) / links) + Vector3.down * Mathf.Sin(Mathf.PI * (i + .5f) / links) * .12f;
                var link = Prim(g, PrimitiveType.Cylinder, "ChainLink", p, new Vector3(.05f, .03f, .016f), rivet);
                link.rotation = Quaternion.LookRotation(to - from) * Quaternion.Euler(90, i % 2 == 0 ? 0 : 90, 0);
            }
            Box(g, "WallEye", to, new Vector3(.06f, .06f, .06f), rivet);
            // A dead LED clock over the keypad: dark panel with dim red digits and a faint red spill.
            var clock = new Vector3(b.max.x + .95f, b.max.y - .35f, 60.95f); // on the south wall face (z 60.9)
            Box(g, "LedClockCase", clock, new Vector3(1f, .34f, .1f), Mat("V4_Black"));
            var label = Copy(Required("S01_Lobby/Sign_② 숙소"), g, "LedClockDigits", false);
            label.gameObject.SetActive(true); label.SetPositionAndRotation(clock + Vector3.forward * .055f, Quaternion.Euler(0, 180, 0));
            var text = label.GetComponent<TextMesh>(); text.text = "00:00:00"; text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
            text.fontSize = 64; text.characterSize = .045f; text.color = new Color(1f, .16f, .1f);
            Point(g, "LedSpill", clock + Vector3.forward * .4f, new Color(1f, .2f, .12f), .4f, 1.6f);
        }

        // ---------- materials ----------

        static Material CinderMaterial(bool painted)
        {
            string name = painted ? "LA_CinderPainted" : "LA_CinderBare";
            if (Materials.TryGetValue(name, out var cached) && cached != null) return cached;
            var m = Lit(name, Color.white, .08f, CinderTexture(painted), FactoryTex("Concrete/T_Concrete_N.TGA"));
            Materials[name] = m; return m;
        }

        static Material GalvanisedMaterial()
        {
            const string name = "LA_GalvanisedPipe";
            if (Materials.TryGetValue(name, out var cached) && cached != null) return cached;
            var m = NoSkyReflection(Lit(name, Hex("8E8F8A"), .32f, FactoryTex("Steel/T_Steel_BC.TGA"), FactoryTex("Steel/T_Steel_N.TGA"), metallic: .55f));
            Materials[name] = m; return m;
        }

        static Material DiamondPlateMaterial()
        {
            const string name = "LA_DiamondPlate";
            if (Materials.TryGetValue(name, out var cached) && cached != null) return cached;
            var m = NoSkyReflection(Lit(name, Color.white, .3f, DiamondTexture(), metallic: .45f)); // the skybox would otherwise wash the plate out indoors
            Materials[name] = m; return m;
        }

        // 4 m x 4 m tile (the shell's wall UV scale): 10 blocks of 0.4 m by 20 courses of 0.2 m in running bond.
        static Texture2D CinderTexture(bool painted)
        {
            string path = $"{AssetDir}/{(painted ? "LA_CinderPainted" : "LA_CinderBare")}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path); if (existing != null) return existing;
            const int size = 1024; var rng = new System.Random(painted ? 11 : 12);
            // Grimy, not clean: the reference block reads pale only where the lamp hits it.
            var baseCol = painted ? Hex("8A7F62") : Hex("5C5850"); var mortar = painted ? Hex("5E5541") : Hex("383530");
            var tint = Enumerable.Range(0, 200).Select(_ => .84f + (float)rng.NextDouble() * .2f).ToArray();
            // Drip streaks: one strength per pixel column, fading in and out down the tile (whole periods, so it tiles).
            var column = Enumerable.Range(0, size).Select(_ => (float)rng.NextDouble()).ToArray();
            for (int pass = 0; pass < 3; pass++) column = column.Select((c, i) => (column[(i + size - 1) % size] + c + column[(i + 1) % size]) / 3f).ToArray();
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float v = y / (float)size * 20f; int course = (int)v; float fy = v - course;
                float u = x / (float)size * 10f + (course % 2) * .5f; int block = (int)u; float fx = u - block;
                float edge = Mathf.Min(Mathf.Min(fx, 1 - fx) * 2f, Mathf.Min(fy, 1 - fy));
                var c = baseCol * tint[(course * 10 + (block % 10)) % 200];
                float pore = (float)rng.NextDouble(); if (pore > .94f) c *= .8f + pore * .05f;
                float ty = y / (float)size * Mathf.PI * 2;
                float drip = Mathf.Clamp01((column[x] - .53f) * 9f) * (.55f + .45f * Mathf.Sin(ty + x * .013f));
                c = Color.Lerp(c, c * .55f + Hex("3B2E1C") * .15f, drip * .55f);
                if (painted && (course * 7 + block * 3) % 23 == 0) c = Color.Lerp(c, Hex("5E584C"), .5f); // paint worn off a block
                if (edge < .05f) c = Color.Lerp(mortar, c, edge / .05f * .4f);
                pixels[y * size + x] = new Color(c.r, c.g, c.b);
            }
            return WriteTexture(path, size, pixels);
        }

        // 1 m tile of herringbone diamond lugs on worn steel with rust blooms.
        static Texture2D DiamondTexture()
        {
            string path = $"{AssetDir}/LA_DiamondPlate.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path); if (existing != null) return existing;
            const int size = 1024, cells = 16; var steel = Hex("4B4945"); var rust = Hex("4F3320");
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size * cells, v = y / (float)size * cells; int cx = (int)u, cy = (int)v;
                float fx = u - cx - .5f, fy = v - cy - .5f; float s = (cx + cy) % 2 == 0 ? 1 : -1;
                float a = (fx + s * fy) * .7071f, b = (fx - s * fy) * .7071f;
                float lug = (a * a) / (.36f * .36f) + (b * b) / (.075f * .075f);
                float tx = x / (float)size * Mathf.PI * 2, ty = y / (float)size * Mathf.PI * 2;
                float bloom = .5f + .25f * Mathf.Sin(tx * 3 + Mathf.Sin(ty * 2) * 2) + .25f * Mathf.Sin(ty * 4 + Mathf.Sin(tx * 3));
                var c = Color.Lerp(steel, rust, Mathf.Clamp01((bloom - .62f) * 2.2f));
                // Worn lug tops catch light; their lower flank and the ring around them stay dark.
                if (lug < 1f) c = Color.Lerp(c, Hex("8A877F"), .45f * (1 - lug)) * (b * s > 0 ? 1.08f : .8f);
                else if (lug < 1.4f) c *= .62f;
                pixels[y * size + x] = new Color(c.r, c.g, c.b);
            }
            return WriteTexture(path, size, pixels);
        }

        static Texture2D WriteTexture(string path, int size, Color[] pixels)
        {
            EnsureFolder();
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false); tex.SetPixels(pixels); tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat; importer.anisoLevel = 4; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
