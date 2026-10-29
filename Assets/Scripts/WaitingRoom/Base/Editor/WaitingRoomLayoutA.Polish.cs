using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.Editor
{
    // Dressing passes that leave room shells, doorways and service objects untouched:
    // freight-lift look for the expert lift, white flickering work lamps in the practice hall,
    // mirrored stage-entrance pipes, drains and rust in the machine room, and the lobby's shabby "Christmas tree" tent.
    public static partial class WaitingRoomLayoutA
    {
        static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");

        static string RunPass(string label, Func<Transform, string> body)
        {
            var (scene, target) = Begin(true); int undo = StartUndo(label);
            try
            {
                var result = body(target);
                Physics.SyncTransforms();
                Undo.CollapseUndoOperations(undo); EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
                return result;
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        // Box in a parent's local frame (parent may be rotated); returns the transform.
        static Transform LBox(Transform p, string n, Vector3 at, Vector3 size, Material m, bool solid = false, Vector3? euler = null)
        {
            var t = Prim(p, PrimitiveType.Cube, n, p.TransformPoint(at), size, m, solid);
            t.localRotation = Quaternion.Euler(euler ?? Vector3.zero); t.localPosition = at; t.localScale = size;
            return t;
        }

        static Transform LShape(Transform p, PrimitiveType type, string n, Vector3 at, Vector3 size, Material m, Vector3? euler = null)
        {
            var t = Prim(p, type, n, p.TransformPoint(at), size, m);
            t.localRotation = Quaternion.Euler(euler ?? Vector3.zero); t.localPosition = at; t.localScale = size;
            return t;
        }

        static Transform LRod(Transform p, string n, Vector3 a, Vector3 b, float radius, Material m)
        {
            var t = LShape(p, PrimitiveType.Cylinder, n, (a + b) * .5f, new Vector3(radius * 2, (b - a).magnitude * .5f, radius * 2), m);
            t.localRotation = Quaternion.FromToRotation(Vector3.up, b - a); return t;
        }

        // World-space rod between two points.
        static Transform Rod(Transform p, string n, Vector3 a, Vector3 b, float radius, Material m)
        {
            var t = Prim(p, PrimitiveType.Cylinder, n, (a + b) * .5f, new Vector3(radius * 2, (b - a).magnitude * .5f, radius * 2), m);
            t.rotation = Quaternion.FromToRotation(Vector3.up, b - a); return t;
        }

        static void Tile(Renderer r, float u, float v)
        {
            var block = new MaterialPropertyBlock(); r.GetPropertyBlock(block);
            block.SetVector(BaseMapStId, new Vector4(u, v, 0, 0)); r.SetPropertyBlock(block);
        }

        static Material Emissive(string name, Color baseColor, Color emission)
        {
            var m = Lit(name, baseColor, .5f);
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(m); return m;
        }

        // ================= 1. Freight lift =================

        static Material HazardChevron() => Lit("LA_HazardChevron", Color.white, .25f, GeneratedTexture("LA_HazardChevron.png", 256, false, (u, v) =>
        {
            bool yellow = Mathf.Repeat(u + v, .5f) < .25f;
            float wear = Mathf.PerlinNoise(u * 18, v * 18) * .6f + Mathf.PerlinNoise(u * 70, v * 70) * .4f;
            var c = yellow ? new Color(.78f, .6f, .1f) : new Color(.06f, .055f, .05f);
            if (wear > .62f) c = Color.Lerp(c, new Color(.32f, .22f, .14f), (wear - .62f) * 2.5f); // paint worn to rusty steel
            return c;
        }));

        public static string DressFreightLift() => RunPass("Freight lift dressing", target =>
        {
            var lift = target.Find("S08_ElevatorHall/FreightLift") ?? throw new InvalidOperationException("Freight lift missing");
            Remove(lift, "FreightDressing");
            var g = Group(lift, "FreightDressing", lift.position); g.localRotation = Quaternion.identity;
            Material steel = Mat("LA_SteelPlate"), dark = Mat("V4_Iron_Dark"), rust = Mat("V4_RustyMetal"), chevron = HazardChevron(), wood = Mat("V4_Wood_Old"), black = Mat("V4_Black");

            // Riveted I-beam portal standing proud of the lobby wall, hazard-striped on its faces.
            foreach (float z in new[] { -2f, 2f })
            {
                LBox(g, "PortalFlange", new Vector3(-.25f, 2.75f, z - .15f), new Vector3(.34f, 5.5f, .05f), steel, true);
                LBox(g, "PortalFlange", new Vector3(-.25f, 2.75f, z + .15f), new Vector3(.34f, 5.5f, .05f), steel, true);
                LBox(g, "PortalWeb", new Vector3(-.25f, 2.75f, z), new Vector3(.06f, 5.5f, .26f), dark);
                var face = LBox(g, "PortalChevron", new Vector3(-.43f, 1.6f, z), new Vector3(.02f, 3.2f, .34f), chevron);
                Tile(face.GetComponent<Renderer>(), 1, 3.2f / .34f);
                for (float y = .3f; y < 5.4f; y += .45f)
                    foreach (float dz in new[] { -.12f, .12f })
                        LShape(g, PrimitiveType.Cylinder, "Rivet", new Vector3(-.43f, y, z + dz + Mathf.Sign(dz) * .03f), new Vector3(.04f, .012f, .04f), dark, new Vector3(0, 0, 90));
                LBox(g, "RubberBumper", new Vector3(-.5f, .25f, z), new Vector3(.18f, .5f, .4f), black, true);
                // Yellow bollard guarding each column.
                var post = LShape(g, PrimitiveType.Cylinder, "Bollard", new Vector3(-1f, .55f, z * 1.12f), new Vector3(.2f, .55f, .2f), Mat("LA_HazardPaint"));
                post.gameObject.AddComponent<CapsuleCollider>();
                foreach (float y in new[] { .35f, .75f }) LShape(g, PrimitiveType.Cylinder, "BollardBand", new Vector3(-1f, y, z * 1.12f), new Vector3(.21f, .05f, .21f), black);
            }
            LBox(g, "PortalHeader", new Vector3(-.25f, 5.35f, 0), new Vector3(.34f, .4f, 4.34f), steel, true);
            var headerStripe = LBox(g, "HeaderChevron", new Vector3(-.43f, 5.35f, 0), new Vector3(.02f, .3f, 4.2f), chevron);
            Tile(headerStripe.GetComponent<Renderer>(), 1, 4.2f / .3f);
            for (float z = -1.95f; z <= 1.96f; z += .3f) LShape(g, PrimitiveType.Cylinder, "Rivet", new Vector3(-.43f, 5.55f, z), new Vector3(.04f, .012f, .04f), dark, new Vector3(0, 0, 90));
            // Load plate and caged amber beacon.
            LBox(g, "LoadPlate", new Vector3(-.44f, 3.9f, 2f), new Vector3(.02f, .5f, .3f), Mat("LA_HazardPaint"));
            var plateText = Group(g, "LoadPlateText", g.TransformPoint(new Vector3(-.46f, 3.9f, 2f))); plateText.localRotation = Quaternion.Euler(0, 90, 0);
            var tm = plateText.gameObject.AddComponent<TextMesh>(); DepthText(tm); tm.text = "MAX\n2000kg"; tm.fontSize = 48; tm.characterSize = .018f; tm.anchor = TextAnchor.MiddleCenter; tm.color = new Color(.08f, .07f, .06f);
            var beacon = Group(g, "AmberBeacon", g.TransformPoint(new Vector3(-.35f, 5.85f, -1.7f)));
            LShape(beacon, PrimitiveType.Cylinder, "BeaconBase", new Vector3(0, -.2f, 0), new Vector3(.22f, .06f, .22f), dark);
            var dome = LShape(beacon, PrimitiveType.Sphere, "BeaconDome", Vector3.zero, new Vector3(.2f, .26f, .2f), Emissive("LA_AmberBeacon", new Color(.8f, .45f, .05f), new Color(2.4f, 1.1f, .1f)));
            for (int i = 0; i < 4; i++) { var a = i * Mathf.PI * .5f; LRod(beacon, "BeaconCage", new Vector3(Mathf.Cos(a) * .13f, -.17f, Mathf.Sin(a) * .13f), new Vector3(Mathf.Cos(a) * .06f, .16f, Mathf.Sin(a) * .06f), .008f, dark); }
            var amber = new GameObject("BeaconLight").AddComponent<Light>(); amber.transform.SetParent(beacon, false);
            amber.type = LightType.Point; amber.color = new Color(1, .6f, .15f); amber.intensity = 1.6f; amber.range = 5; amber.shadows = LightShadows.None;
            var amberFlicker = amber.gameObject.AddComponent<WaitingRoomFlickerLight>(); amberFlicker.bulbs = new[] { dome.GetComponent<Renderer>() }; amberFlicker.averageGap = 9;

            // Shaft above the car: girts, cross bracing and the hoist sheave on top.
            LBox(g, "ShaftFacePlate", new Vector3(-.05f, 7.85f, 0), new Vector3(.05f, 4.6f, 3.7f), dark);
            foreach (float y in new[] { 5.75f, 7.3f, 8.85f, 10.2f }) LBox(g, "ShaftGirt", new Vector3(-.14f, y, 0), new Vector3(.14f, .16f, 4.0f), rust);
            foreach (var (y0, y1) in new[] { (5.75f, 7.3f), (7.3f, 8.85f), (8.85f, 10.2f) })
            { LRod(g, "ShaftBrace", new Vector3(-.16f, y0, -1.9f), new Vector3(-.16f, y1, 1.9f), .035f, rust); LRod(g, "ShaftBrace", new Vector3(-.16f, y0, 1.9f), new Vector3(-.16f, y1, -1.9f), .035f, rust); }
            LBox(g, "HoistMotor", new Vector3(1.0f, 10.65f, -.9f), new Vector3(.9f, .6f, .8f), dark);
            LShape(g, PrimitiveType.Cylinder, "HoistSheave", new Vector3(1.45f, 11.0f, .35f), new Vector3(1.3f, .08f, 1.3f), steel, new Vector3(90, 0, 0));
            LShape(g, PrimitiveType.Cylinder, "SheaveHub", new Vector3(1.45f, 11.0f, .35f), new Vector3(.3f, .14f, .3f), rust, new Vector3(90, 0, 0));
            foreach (float dx in new[] { -.15f, -.05f, .05f, .15f }) LRod(g, "HoistCable", new Vector3(1.45f + dx - .6f, 11.0f, .35f), new Vector3(1.45f + dx - .6f, 10.3f, .35f), .012f, black);

            // Car interior: diamond plate, bumper rails, plate seams, folded accordion gate, caged lamp and controls.
            LBox(g, "DiamondPlateFloor", new Vector3(1.4f, .012f, 0), new Vector3(2.66f, .02f, 3.3f), DiamondPlateMaterial());
            foreach (float y in new[] { .35f, .95f })
            {
                LBox(g, "BumperRail", new Vector3(2.6f, y, 0), new Vector3(.05f, .16f, 3.2f), wood, true);
                foreach (float z in new[] { -1.6f, 1.6f }) LBox(g, "BumperRail", new Vector3(1.45f, y, z), new Vector3(2.4f, .16f, .05f), wood, true);
            }
            for (float z = -1.2f; z <= 1.21f; z += .8f) LBox(g, "PlateSeam", new Vector3(2.63f, 1.8f, z), new Vector3(.02f, 3.5f, .04f), dark);
            for (float x = .5f; x < 2.5f; x += .8f) foreach (float z in new[] { -1.63f, 1.63f }) LBox(g, "PlateSeam", new Vector3(x, 1.8f, z), new Vector3(.04f, 3.5f, .02f), dark);
            foreach (float z in new[] { -1.6f, 1.6f }) LBox(g, "CornerGuard", new Vector3(2.58f, 1.2f, z - Mathf.Sign(z) * .02f), new Vector3(.08f, 2.4f, .08f), Mat("LA_HazardPaint"));
            // Gate folded back against the right jamb so the doorway stays open.
            var gate = Group(g, "FoldedAccordionGate", g.TransformPoint(new Vector3(.12f, 0, 1.42f)));
            for (int i = 0; i < 8; i++) LBox(gate, "GateBar", new Vector3(0, 1.75f, -.14f + i * .04f), new Vector3(.03f, 3.4f, .025f), dark);
            for (float y = .2f; y < 3.4f; y += .42f) { LRod(gate, "GateLattice", new Vector3(0, y, -.15f), new Vector3(0, y + .4f, .15f), .008f, rust); LRod(gate, "GateLattice", new Vector3(0, y, .15f), new Vector3(0, y + .4f, -.15f), .008f, rust); }
            LBox(gate, "GateStackCollider", new Vector3(0, 1.75f, 0), new Vector3(.1f, 3.4f, .34f), dark, true).GetComponent<Renderer>().enabled = false;
            LBox(g, "GateTopTrack", new Vector3(.12f, 3.5f, 0), new Vector3(.06f, .05f, 3.3f), dark);
            LBox(g, "GateFloorTrack", new Vector3(.12f, .025f, 0), new Vector3(.06f, .02f, 3.3f), dark);
            // Controls on the left wall near the door.
            var panel = Group(g, "CarControlPanel", g.TransformPoint(new Vector3(.55f, 1.35f, -1.6f)));
            LBox(panel, "PanelBox", Vector3.zero, new Vector3(.36f, .6f, .07f), dark);
            LRod(panel, "Lever", new Vector3(-.08f, -.05f, .05f), new Vector3(-.03f, .2f, .12f), .015f, steel);
            LShape(panel, PrimitiveType.Sphere, "LeverKnob", new Vector3(-.03f, .2f, .12f), Vector3.one * .06f, Emissive("LA_RedKnob", new Color(.6f, .05f, .04f), new Color(.15f, 0, 0)));
            for (int i = 0; i < 3; i++) LShape(panel, PrimitiveType.Cylinder, "Button", new Vector3(.1f, .18f - i * .12f, .045f), new Vector3(.05f, .01f, .05f), Mat("LA_Bulb"), new Vector3(90, 0, 0));
            LShape(panel, PrimitiveType.Cylinder, "StopButton", new Vector3(.1f, -.2f, .05f), new Vector3(.08f, .02f, .08f), Emissive("LA_StopRed", new Color(.7f, .05f, .03f), new Color(.4f, .02f, 0)), new Vector3(90, 0, 0));
            // Caged ceiling lamp and escape hatch.
            for (int i = 0; i < 4; i++) { var a = i * Mathf.PI * .5f + .4f; LRod(g, "LampCage", new Vector3(2.4f + Mathf.Cos(a) * .1f, 3.6f, Mathf.Sin(a) * .1f), new Vector3(2.4f + Mathf.Cos(a) * .09f, 3.3f, Mathf.Sin(a) * .09f), .007f, dark); }
            LBox(g, "EscapeHatch", new Vector3(1.4f, 3.585f, -.6f), new Vector3(.7f, .02f, .7f), rust);
            // Scuffs and a strapped crate in the back corner (the centre stays clear for the service stand).
            foreach (var (x, z, s) in new[] { (1.0f, .4f, 1.2f), (2.2f, -.9f, .9f), (.4f, -1.1f, .8f) })
            { var q = LShape(g, PrimitiveType.Quad, "FloorScuff", new Vector3(x, .025f, z), new Vector3(s, s * .7f, 1), Mat("LA_RustDecal"), new Vector3(90, x * 60, 0)); }
            LBox(g, "StrappedCrate", new Vector3(2.25f, .35f, -1.25f), new Vector3(.6f, .7f, .55f), wood, true);
            LBox(g, "CrateStrap", new Vector3(2.25f, .35f, -1.25f), new Vector3(.62f, .72f, .06f), Mat("LA_HazardPaint"));

            // Door leaves: hazard chevron kick band, corrugation ribs and a striped meeting edge. Parented to the
            // moving leaves so they slide with them; nothing on the leaves themselves changes.
            int doorParts = 0;
            foreach (var leaf in lift.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("ServiceDoor")).ToArray())
            {
                var old = leaf.Find("FreightDoorDressing"); if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
                var r = leaf.GetComponent<Renderer>().bounds;
                var holder = new GameObject("FreightDoorDressing").transform; Undo.RegisterCreatedObjectUndo(holder.gameObject, "Door dressing");
                holder.SetParent(g, false); holder.position = r.center; holder.rotation = Quaternion.identity;
                float front = -r.extents.x - .012f, half = r.extents.z, meeting = Mathf.Sign(lift.position.z - r.center.z);
                var kick = LBox(holder, "KickChevron", new Vector3(front, -r.extents.y + .35f, 0), new Vector3(.015f, .6f, half * 2), chevron);
                Tile(kick.GetComponent<Renderer>(), 1, half * 2 / .6f);
                for (float z = -half + .2f; z < half - .1f; z += .26f) LBox(holder, "DoorRib", new Vector3(front, .25f, z), new Vector3(.03f, r.size.y - 1.2f, .05f), steel);
                var edge = LBox(holder, "MeetingEdge", new Vector3(front, 0, meeting * (half - .04f)), new Vector3(.016f, r.size.y, .08f), chevron);
                Tile(edge.GetComponent<Renderer>(), 1, r.size.y / .08f);
                holder.SetParent(leaf, true); doorParts += holder.childCount;
            }
            return $"Freight lift: {g.GetComponentsInChildren<Renderer>().Length} renderers, {doorParts} door parts";
        });

        // ================= 2. Practice hall white flicker lamps =================

        public static string AddPracticeFlickerLamps() => RunPass("Practice white flicker lamps", target =>
        {
            var room = target.Find("S09_PracticeRoom"); var arena = room.Find("RoundArena");
            var c = room.Find("LegacyRing").position;
            Remove(arena, "WhiteFlickerLamps"); var g = Group(arena, "WhiteFlickerLamps", c);
            var bulbMat = Emissive("LA_WhiteWorkBulb", new Color(.95f, .97f, 1f), new Color(3.2f, 3.3f, 3.5f));
            Material dark = Mat("V4_Iron_Dark"), shade = Mat("LA_SteelPlate");
            int n = 0;
            foreach (float deg in new[] { 22.5f, 67.5f, 112.5f, 157.5f, -157.5f, -112.5f, -67.5f, -22.5f })
            {
                var wall = RingPoint(c, 25.85f, deg, 9.6f); var aim = RingPoint(c, 11f, deg, .6f);
                var lamp = Group(g, "WorkLamp", wall); lamp.rotation = Quaternion.LookRotation(aim - wall);
                LBox(lamp, "WallPlate", new Vector3(0, 0, -.05f), new Vector3(.35f, .45f, .04f), dark);
                LRod(lamp, "Arm", new Vector3(0, 0, -.03f), new Vector3(0, 0, .55f), .025f, dark);
                LShape(lamp, PrimitiveType.Cylinder, "Shade", new Vector3(0, 0, .7f), new Vector3(.6f, .18f, .6f), shade, new Vector3(90, 0, 0));
                var bulb = LShape(lamp, PrimitiveType.Sphere, "Bulb", new Vector3(0, 0, .84f), Vector3.one * .22f, bulbMat);
                for (int i = 0; i < 4; i++) { var a = i * Mathf.PI * .5f; LRod(lamp, "Cage", new Vector3(Mathf.Cos(a) * .19f, Mathf.Sin(a) * .19f, .76f), new Vector3(Mathf.Cos(a) * .06f, Mathf.Sin(a) * .06f, 1.0f), .006f, dark); }
                var l = new GameObject("WhiteFlickerSpot").AddComponent<Light>(); l.transform.SetParent(lamp, false); l.transform.localPosition = new Vector3(0, 0, .85f);
                l.type = LightType.Spot; l.color = new Color(.93f, .96f, 1f); l.intensity = 110f; l.range = 26f; l.spotAngle = 75; l.innerSpotAngle = 35; l.shadows = LightShadows.None;
                // Glow on the wall around the fitting; it flickers with the spot (same burst timing is not needed).
                var halo = new GameObject("LampHalo").AddComponent<Light>(); halo.transform.SetParent(lamp, false); halo.transform.localPosition = new Vector3(0, 0, .5f);
                halo.type = LightType.Point; halo.color = l.color; halo.intensity = 2.5f; halo.range = 3.5f; halo.shadows = LightShadows.None;
                var haloFlicker = halo.gameObject.AddComponent<WaitingRoomFlickerLight>(); haloFlicker.averageGap = n % 4 == 1 ? 2.2f : 5f; haloFlicker.dipLevel = new Vector2(0f, .3f);
                var flicker = l.gameObject.AddComponent<WaitingRoomFlickerLight>();
                flicker.bulbs = new[] { bulb.GetComponent<Renderer>() };
                // Tired tubes: frequent bursts, deep dips, and two lamps that misbehave much more than the rest.
                flicker.averageGap = n % 4 == 1 ? 2.2f : 5f; flicker.dipsPerBurst = new Vector2Int(2, n % 4 == 1 ? 7 : 4); flicker.dipLevel = new Vector2(0f, .3f); flicker.hum = .08f;
                n++;
            }
            return $"{n} white flickering work lamps on the practice hall wall";
        });

        // ================= 3. Stage entrance pipes =================

        public static string BalanceStageEntrancePipes() => RunPass("Mirror and enlarge stage entrance pipes", target =>
        {
            var dressing = target.Find("S06_StageCorridor/StageProcessionalRoute/GreyboxComposition/Tunnel/WallDressing") ?? throw new InvalidOperationException("Tunnel dressing missing");
            if (dressing.Cast<Transform>().Any(t => t.name.EndsWith("_Mirror"))) throw new InvalidOperationException("Pipes already balanced");
            // Corridor centre line from the two inner wall faces.
            Physics.Raycast(new Vector3(-6, 4, -8), Vector3.left, out var lh, 15, ~0, QueryTriggerInteraction.Ignore);
            Physics.Raycast(new Vector3(-6, 4, -8), Vector3.right, out var rh, 15, ~0, QueryTriggerInteraction.Ignore);
            float cx = (lh.point.x + rh.point.x) * .5f; const float grow = 1.2f;
            Bounds B(Transform t) { var rs = t.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b; }
            bool IsPipe(Transform t) => t.name.StartsWith("SM_BigPipe") || t.name.StartsWith("SM_Pipe_Medium") || t.name.StartsWith("SM_Smallpipe") || t.name == "DropPipe" || t.name.StartsWith("SM_Pipe_Small_Valve");
            var items = dressing.Cast<Transform>().Where(t => t.gameObject.activeSelf && t.GetComponentsInChildren<Renderer>().Length > 0).ToArray();
            var pipes = items.Where(IsPipe).ToArray();
            int mirrored = 0, retired = 0;
            // The thin right-hand pipe is replaced by mirrors of the left one so both walls carry the same set.
            foreach (var t in pipes.Where(t => t.name.StartsWith("SM_Smallpipe"))) { Undo.RecordObject(t.gameObject, "Replace thin pipe"); t.gameObject.SetActive(false); retired++; }
            // Dust strips left floating where the low right pipe was cut for curtain bays.
            var live = pipes.Where(t => t.gameObject.activeSelf).ToArray();
            foreach (var d in items.Where(t => t.name == "PipeDust"))
            {
                var db = B(d);
                bool supported = live.Any(p => { var pb = B(p); return Mathf.Abs(pb.center.x - db.center.x) < .6f && pb.min.z < db.center.z && pb.max.z > db.center.z && Mathf.Abs(pb.max.y - db.center.y) < .7f; });
                if (!supported) { Undo.RecordObject(d.gameObject, "Remove floating dust"); d.gameObject.SetActive(false); retired++; }
            }
            // Enlarge every runner pipe's diameter (not its length), then mirror the set across the corridor.
            foreach (var p in live.Where(t => t.name != "DropPipe" && !t.name.StartsWith("SM_Pipe_Small_Valve")))
            {
                var before = B(p);
                Undo.RecordObject(p, "Enlarge pipe");
                var s = p.localScale;
                for (int axis = 0; axis < 3; axis++)
                {
                    var dir = p.TransformDirection(axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward).normalized;
                    if (Mathf.Abs(dir.z) > .9f) continue;
                    s[axis] *= grow;
                }
                p.localScale = s;
                // Keep it against its wall: the outer surface stays, the pipe grows into the corridor.
                var after = B(p); float wallSide = Mathf.Sign(before.center.x - cx);
                float shift = wallSide > 0 ? before.max.x - after.max.x : before.min.x - after.min.x;
                p.position += new Vector3(shift, before.center.y - after.center.y, 0);
                // Dust riding on top follows the new crown.
                foreach (var d in items.Where(t => t.name == "PipeDust" && t.gameObject.activeSelf))
                {
                    var db = B(d);
                    if (Mathf.Abs(db.center.x - before.center.x) < .6f && db.center.z > before.min.z && db.center.z < before.max.z && db.center.y > before.center.y)
                    { Undo.RecordObject(d, "Dust on enlarged pipe"); d.position += Vector3.up * (B(p).max.y - before.max.y) + Vector3.right * shift; }
                }
            }
            var toMirror = dressing.Cast<Transform>().Where(t => t.gameObject.activeSelf && (IsPipe(t) || t.name == "PipeDust")).ToArray();
            foreach (var t in toMirror)
            {
                var copy = Object.Instantiate(t.gameObject, dressing).transform; copy.name = t.name + "_Mirror";
                Undo.RegisterCreatedObjectUndo(copy.gameObject, "Mirror pipe");
                copy.localRotation = Quaternion.Euler(0, 180, 0) * t.localRotation; copy.localScale = t.localScale;
                var b = B(t); var cb = B(copy);
                copy.position += new Vector3(2 * cx - b.center.x - cb.center.x, b.center.y - cb.center.y, b.center.z - cb.center.z);
                mirrored++;
            }
            return $"Stage pipes: centre x {cx:F2}, {mirrored} mirrored copies, {retired} thin pipes/floating dust retired, diameter x{grow}";
        });

        // ================= 4. Machine room drains and rust =================

        static Material RustPatina() => DecalMaterial("LA_RustPatina", "LA_RustPatina.png", () =>
        {
            const int size = 512; var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size, v = (float)y / size;
                // Domain-warped fbm gives ragged, organic edges instead of a soft disc.
                float wx = Fbm(u * 3 + 11, v * 3 + 2) * .35f, wy = Fbm(u * 3 + 5, v * 3 + 17) * .35f;
                float r = new Vector2(u - .5f + wx - .17f, v - .5f + wy - .17f).magnitude * 2;
                float body = Fbm(u * 6 + wx * 4, v * 6 + wy * 4);
                float a = Mathf.Clamp01((body * 1.15f - r * .85f) * 3.2f);
                float speck = Mathf.PerlinNoise(u * 120, v * 120);
                a = Mathf.Clamp01(a + (speck > .72f && r < 1 ? (speck - .72f) * 2f : 0));
                float tone = Fbm(u * 14 + 3, v * 14 + 9);
                var c = Color.Lerp(new Color(.3f, .13f, .045f), new Color(.78f, .36f, .1f), Mathf.SmoothStep(.3f, .75f, tone));
                c = Color.Lerp(c, new Color(.2f, .09f, .04f), Mathf.Clamp01(1 - r) * .35f); // darker, older core
                px[y * size + x] = new Color(c.r, c.g, c.b, Mathf.Clamp01(a * 1.1f));
            }
            tex.SetPixels(px); tex.Apply(); return tex;
        }, .12f, 3005);

        static Material RustStreak() => DecalMaterial("LA_RustStreak", "LA_RustStreak.png", () =>
        {
            const int size = 256; var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size, v = (float)y / size;
                float width = .18f + .12f * Mathf.PerlinNoise(v * 6, 4) ;
                float across = Mathf.Abs(u - .5f - (Mathf.PerlinNoise(v * 3, 9) - .5f) * .25f) / width;
                float a = Mathf.Clamp01(1 - across) * Mathf.Pow(v, .6f) * Mathf.Lerp(.6f, 1f, Mathf.PerlinNoise(u * 40, v * 8));
                var c = Color.Lerp(new Color(.7f, .32f, .09f), new Color(.34f, .15f, .05f), v);
                px[y * size + x] = new Color(c.r, c.g, c.b, a * .75f);
            }
            tex.SetPixels(px); tex.Apply(); return tex;
        }, .1f, 3004);

        static float Fbm(float x, float y) { float s = 0, a = .5f, f = 1; for (int i = 0; i < 4; i++) { s += Mathf.PerlinNoise(x * f, y * f) * a; f *= 2.03f; a *= .5f; } return s / .9375f; }

        public static string DetailMachineRoomFloor() => RunPass("Machine room drains and rust", target =>
        {
            var room = target.Find("B1_MachineRoom") ?? throw new InvalidOperationException("Machine room missing");
            Remove(room, "FloorDrainage");
            var shell = room.Find("B1_MachineRoom_LowerShell");
            var rs = shell.GetComponentsInChildren<Renderer>(); var sb = rs[0].bounds; foreach (var r in rs) sb.Encapsulate(r.bounds);
            // Floor level = the most common lowest hit across the room (plant tops and catwalks are higher, rarer).
            var levels = new List<float>();
            for (float x = sb.min.x + 1; x < sb.max.x - 1; x += 2)
            for (float z = sb.min.z + 1; z < sb.max.z - 1; z += 2)
                if (Physics.Raycast(new Vector3(x, sb.max.y - .3f, z), Vector3.down, out var lh, 12, ~0, QueryTriggerInteraction.Ignore)) levels.Add(Mathf.Round(lh.point.y * 20) / 20);
            if (levels.Count == 0) throw new InvalidOperationException("No machine room floor");
            float floorY = levels.GroupBy(y => y).Where(k => k.Key < sb.min.y + 1.5f).OrderByDescending(k => k.Count()).First().Key;
            var g = Group(room, "FloorDrainage", new Vector3(sb.center.x, floorY, sb.center.z));
            var rng = new System.Random(64);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            // Classify a 1 m grid: free floor, or blocked by plant standing on it.
            var free = new List<Vector3>(); var blocked = new List<Vector3>();
            for (float x = sb.min.x + .8f; x < sb.max.x - .8f; x += 1f)
            for (float z = sb.min.z + .8f; z < sb.max.z - .8f; z += 1f)
            {
                var p = new Vector3(x, floorY, z);
                if (!Physics.Raycast(p + Vector3.up * 4.5f, Vector3.down, out var h, 6, ~0, QueryTriggerInteraction.Ignore)) continue;
                bool onFloor = Mathf.Abs(h.point.y - floorY) < .06f;
                bool clear = Physics.OverlapBox(p + Vector3.up * .7f, new Vector3(.45f, .6f, .45f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore).Length == 0;
                (onFloor && clear ? free : blocked).Add(p);
            }
            // Drains sit on free floor next to plant, at least 4.5 m apart.
            var drains = new List<Vector3>();
            foreach (var p in free.OrderBy(_ => rng.Next()))
            {
                if (drains.Count >= 10) break;
                bool nearPlant = blocked.Any(b => (b - p).sqrMagnitude < 2.3f);
                if (!nearPlant || drains.Any(d => (d - p).sqrMagnitude < 4.5f * 4.5f)) continue;
                drains.Add(p);
            }
            Material frame = Mat("V4_Iron_Dark"), bars = Mat("V4_RustyMetal"), pit = Mat("V4_Black");
            float y0 = floorY;
            foreach (var d in drains)
            {
                var drain = Group(g, "FloorDrain", d); drain.rotation = Quaternion.Euler(0, rng.Next(4) * 90, 0);
                LBox(drain, "DrainPit", new Vector3(0, .004f, 0), new Vector3(.56f, .004f, .56f), pit);
                foreach (var (x, z, sx, sz) in new[] { (0f, -.3f, .66f, .06f), (0f, .3f, .66f, .06f), (-.3f, 0f, .06f, .66f), (.3f, 0f, .06f, .66f) })
                    LBox(drain, "DrainFrame", new Vector3(x, .012f, z), new Vector3(sx, .024f, sz), frame);
                for (float x = -.22f; x <= .23f; x += .074f) LBox(drain, "GrateBar", new Vector3(x, .014f, 0), new Vector3(.025f, .02f, .54f), bars);
                LBox(drain, "GrateCrossbar", new Vector3(0, .016f, 0), new Vector3(.54f, .016f, .03f), bars);
                // Wet halo, rust bloom and streaks running in from the surrounding floor.
                FloorDecal(g, "DrainWetHalo", d, R(1.3f, 1.9f), R(1.1f, 1.6f), R(0, 360), Mat("LA_PuddleDecal"), .011f);
                FloorDecal(g, "DrainRust", d + new Vector3(R(-.2f, .2f), 0, R(-.2f, .2f)), R(1.4f, 2.4f), R(1.2f, 2.1f), R(0, 360), RustPatina(), .013f);
                int streaks = rng.Next(2, 5);
                for (int i = 0; i < streaks; i++)
                {
                    float a = R(0, 360), len = R(.9f, 2.2f); var dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
                    FloorDecal(g, "RustRunoff", d + dir * (len * .5f + .25f), R(.25f, .5f), len, a, RustStreak(), .014f);
                }
            }
            // Trench drain along the longest straight free run (x or z), grating flush with the slab.
            var trench = LongestRun(free);
            if (trench.length >= 5f)
            {
                var t = Group(g, "TrenchDrain", trench.centre); t.rotation = Quaternion.LookRotation(trench.dir);
                LBox(t, "TrenchPit", new Vector3(0, .004f, 0), new Vector3(.3f, .004f, trench.length), pit);
                foreach (float x in new[] { -.19f, .19f }) LBox(t, "TrenchEdge", new Vector3(x, .012f, 0), new Vector3(.06f, .024f, trench.length), frame);
                for (float z = -trench.length * .5f + .05f; z < trench.length * .5f; z += .09f) LBox(t, "TrenchSlat", new Vector3(0, .014f, z), new Vector3(.32f, .018f, .025f), bars);
                for (float z = -trench.length * .5f; z < trench.length * .5f; z += R(1.2f, 2.4f))
                    FloorDecal(g, "TrenchRust", t.TransformPoint(new Vector3(R(-.3f, .3f), 0, z)), R(.8f, 1.6f), R(.7f, 1.4f), R(0, 360), RustPatina(), .013f);
            }
            // Rust bleeding out from under plant footprints, and sparse old patches across open floor.
            int plantStains = 0;
            foreach (var p in free.Where(f => blocked.Any(b => (b - f).sqrMagnitude < 1.1f)).OrderBy(_ => rng.Next()).Take(28))
            {
                var b = blocked.OrderBy(q => (q - p).sqrMagnitude).First();
                FloorDecal(g, "PlantRustBleed", Vector3.Lerp(p, b, .45f), R(1.2f, 2.6f), R(.9f, 1.8f), R(0, 360), RustPatina(), .012f); plantStains++;
            }
            foreach (var p in free.OrderBy(_ => rng.Next()).Take(14))
                FloorDecal(g, "OldRustPatch", p + new Vector3(R(-.4f, .4f), 0, R(-.4f, .4f)), R(1.6f, 3.2f), R(1.2f, 2.6f), R(0, 360), Mat(rng.Next(3) == 0 ? "LA_RustBloom" : "LA_RustDecal"), .01f);
            return $"Machine room floor y={floorY:F2}: {drains.Count} drains, trench {trench.length:F1} m, {plantStains} plant rust bleeds, {free.Count} free cells";
        });

        static void FloorDecal(Transform g, string name, Vector3 at, float sx, float sz, float yaw, Material m, float lift)
        {
            var q = Prim(g, PrimitiveType.Quad, name, new Vector3(at.x, at.y + lift, at.z), new Vector3(sx, sz, 1), m);
            q.rotation = Quaternion.Euler(90, yaw, 0);
            var r = q.GetComponent<Renderer>(); r.shadowCastingMode = ShadowCastingMode.Off;
        }

        static (Vector3 centre, Vector3 dir, float length) LongestRun(List<Vector3> free)
        {
            var set = new HashSet<(int, int)>(free.Select(p => (Mathf.RoundToInt(p.x * 2), Mathf.RoundToInt(p.z * 2))));
            float y = free.Count > 0 ? free[0].y : 0;
            (Vector3, Vector3, float) best = (Vector3.zero, Vector3.forward, 0);
            foreach (var p in free)
                foreach (var dir in new[] { Vector3.right, Vector3.forward })
                {
                    var prev = p - dir; if (set.Contains((Mathf.RoundToInt(prev.x * 2), Mathf.RoundToInt(prev.z * 2)))) continue; // only from a run's start
                    int n = 0; var q = p;
                    while (set.Contains((Mathf.RoundToInt(q.x * 2), Mathf.RoundToInt(q.z * 2)))) { n++; q += dir; }
                    float len = Mathf.Min(n - 1, 10);
                    if (len > best.Item3) best = (p + dir * (len * .5f) + Vector3.up * (y - p.y), dir, len);
                }
            return best;
        }

        // ================= 5. Lobby: shabby Christmas-tree tent =================

        public static string DressLobbyChristmasTent() => RunPass("Lobby shabby Christmas-tree tent", target =>
        {
            var lobby = target.Find("S01_Lobby");
            var floor = lobby.Find("Frame8Lobby/RoundFloor").GetComponent<Renderer>().bounds;
            var hub = new Vector3(floor.center.x, floor.max.y, floor.center.z);
            var at = hub + new Vector3(.5f, 0, 4.4f);
            Remove(lobby, "ShabbyChristmasTent");
            var g = Group(lobby, "ShabbyChristmasTent", at); g.rotation = Quaternion.Euler(0, 18, 0);
            var rng = new System.Random(1225);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            // User: twice the first one-person size (2.3 m → 4.6 m across). Props keep their own size; only the ring they sit on grows.
            const float K = 2f, baseR = 1.15f * K, height = 2.75f * K;
            var canvas = WornCanvasTexture();
            Material red = DoubleSided(Lit("LA_XmasTentRed", Hex("7A2421") * .95f, .06f, canvas)), cream = DoubleSided(Lit("LA_XmasTentCream", Hex("B9A889") * .95f, .05f, canvas));
            var tent = MeshObject(g, "SlumpedConeTent", at, SaveMesh(SlumpedCone(baseR, height)), red, cream);
            tent.transform.rotation = g.rotation;
            tent.AddComponent<MeshCollider>().sharedMesh = tent.GetComponent<MeshFilter>().sharedMesh;
            // Lean of the apex, so pole, topper and lights follow the slump.
            var apex = g.TransformPoint(new Vector3(.28f * K, height - .08f * K, .12f * K));
            var gold = Mat("LA_Bronze");
            Rod(g, "TentPole", g.TransformPoint(new Vector3(.12f * K, height - .5f * K, .05f * K)), apex + (apex - g.TransformPoint(new Vector3(0, height - .6f * K, 0))).normalized * .35f * K, .025f * K, Mat("V4_Wood_Old"));
            var star = MeshObject(g, "CrookedStarTopper", apex + Vector3.up * .48f * K, SaveMesh(StarMesh(.36f, .15f, .08f)), gold);
            star.transform.rotation = Quaternion.LookRotation(g.right, Vector3.up) * Quaternion.Euler(0, 0, -23);
            // Scalloped hem and four short guy ropes to stakes.
            for (int i = 0; i < 28; i++)
            {
                float a = i * Mathf.PI * 2 / 28; var p = g.TransformPoint(new Vector3(Mathf.Cos(a) * (baseR + .03f), .5f, Mathf.Sin(a) * (baseR + .03f)));
                if (rng.Next(5) == 0) continue; // torn away
                bool hanging = rng.Next(4) == 0;
                var flap = Prim(g, PrimitiveType.Cube, "HemScallop", p + (hanging ? Vector3.down * .18f : Vector3.zero), new Vector3(.56f, .32f, .02f), i % 2 == 0 ? cream : red);
                flap.rotation = Quaternion.LookRotation(p - new Vector3(at.x, p.y, at.z)) * Quaternion.Euler(R(-8, 14) + (hanging ? 25 : 0), 0, R(-6, 6) + (hanging ? R(-35, 35) : 0));
            }
            // Frayed strips hanging from the hem and the holes, and patches sewn on with coarse stitches.
            Vector3 Surface(float deg, float h, float lift)
            {
                float a = deg * Mathf.Deg2Rad, r = ConeRadius(baseR, height, h) * (1 - .12f * Mathf.Sin(h / height * Mathf.PI) * .8f) + lift;
                return g.TransformPoint(new Vector3(Mathf.Cos(a) * r, h, Mathf.Sin(a) * r) + new Vector3(.28f, 0, .12f) * K * Mathf.Pow(h / height, 2.2f));
            }
            for (int i = 0; i < 22; i++)
            {
                float deg = R(0, 360), h = i < 16 ? R(.3f, .55f) : R(1.2f, 3.2f), len = R(.18f, .55f);
                var top = Surface(deg, h, .03f); var strip = Prim(g, PrimitiveType.Cube, "FrayedStrip", top + Vector3.down * len * .5f, new Vector3(R(.04f, .11f), len, .008f), rng.Next(2) == 0 ? red : cream);
                strip.rotation = Quaternion.LookRotation(top - new Vector3(at.x, top.y, at.z)) * Quaternion.Euler(R(-12, 25), 0, R(-14, 14));
            }
            var patchMat = Lit("LA_TentPatch", new Color(.42f, .36f, .26f), .05f, GeneratedTexture("LA_TentPatch.png", 128, false, (u, v) => { bool line = Mathf.Repeat(u * 6, 1) < .15f || Mathf.Repeat(v * 6, 1) < .15f; float n = .85f + .15f * Mathf.PerlinNoise(u * 30, v * 30); return line ? new Color(.45f * n, .2f * n, .14f * n) : new Color(n, n * .95f, n * .85f); }));
            for (int i = 0; i < 7; i++)
            {
                float deg = R(0, 360), h = R(.7f, height * .65f), size = R(.22f, .42f);
                var c = Surface(deg, h, .015f); var outward = (c - new Vector3(at.x, c.y, at.z)).normalized;
                var patch = Prim(g, PrimitiveType.Cube, "SewnPatch", c, new Vector3(size, size * R(.7f, 1.1f), .01f), i % 3 == 0 ? patchMat : i % 2 == 0 ? cream : red);
                patch.rotation = Quaternion.LookRotation(outward) * Quaternion.Euler(-14, 0, R(-20, 20));
                for (int k = 0; k < 10; k++)
                {
                    float e = (k / 10f) * 4; int side = (int)e; float f = e - side;
                    var local = side switch { 0 => new Vector3(Mathf.Lerp(-.5f, .5f, f), .5f, 0), 1 => new Vector3(.5f, Mathf.Lerp(.5f, -.5f, f), 0), 2 => new Vector3(Mathf.Lerp(.5f, -.5f, f), -.5f, 0), _ => new Vector3(-.5f, Mathf.Lerp(-.5f, .5f, f), 0) };
                    var st = Prim(g, PrimitiveType.Cube, "Stitch", patch.TransformPoint(local * .92f) + outward * .008f, new Vector3(.006f, .035f, .004f), Mat("V4_Black")); st.rotation = patch.rotation * Quaternion.Euler(0, 0, side % 2 == 0 ? 0 : 90);
                }
            }
            foreach (float deg in new[] { 30f, 120f, 210f, 300f })
            {
                var a = deg * Mathf.Deg2Rad; var top = g.TransformPoint(new Vector3(Mathf.Cos(a) * .55f * K, 1.6f * K, Mathf.Sin(a) * .55f * K));
                var stake = g.TransformPoint(new Vector3(Mathf.Cos(a) * (baseR + 1.6f), .04f, Mathf.Sin(a) * (baseR + 1.6f)));
                Rod(g, "GuyRope", top, stake, .009f, Mat("V6_Rope"));
                Prim(g, PrimitiveType.Cylinder, "Stake", stake + Vector3.up * .06f, new Vector3(.04f, .08f, .04f), Mat("V4_Wood_Old"));
            }
            // Fairy lights spiralling up the cone; a few bulbs are dead.
            var warm = Emissive("LA_XmasBulbWarm", new Color(1, .82f, .55f), new Color(2.6f, 1.7f, .7f));
            var amber = Emissive("LA_XmasBulbAmber", new Color(1, .55f, .2f), new Color(2.2f, .9f, .2f));
            var dead = Lit("LA_XmasBulbDead", new Color(.25f, .22f, .18f), .6f);
            var wire = Mat("V4_Black");
            Vector3 prev = default; int bulbs = 0; var liveBulbs = new List<Renderer>();
            for (int i = 0; i <= 240; i++)
            {
                float t = i / 240f, h = .38f * K + t * (height - .62f * K), turns = 3.6f;
                float r = ConeRadius(baseR, height, h) + .045f, a = t * turns * Mathf.PI * 2;
                var lean = new Vector3(.28f, 0, .12f) * K * Mathf.Pow(h / height, 2.2f);
                var p = g.TransformPoint(new Vector3(Mathf.Cos(a) * r, h - .06f * K * Mathf.Sin(t * 37), Mathf.Sin(a) * r) + lean);
                if (i > 0) Rod(g, "LightWire", prev, p, .006f, wire);
                if (i % 3 == 0)
                {
                    var m = rng.Next(9) == 0 ? dead : rng.Next(3) == 0 ? amber : warm;
                    var b = Prim(g, PrimitiveType.Sphere, "FairyBulb", p, new Vector3(.055f, .07f, .055f), m); bulbs++;
                    b.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                    if (m != dead) liveBulbs.Add(b.GetComponent<Renderer>());
                }
                prev = p;
            }
            // One warm pool under the "tree" (it's the most saturated, brightest point of the hall), with a tired flicker.
            var glow = new GameObject("TreeGlow").AddComponent<Light>(); glow.transform.SetParent(g, false); glow.transform.position = at + Vector3.up * 2.4f * K + g.forward * 1.2f * K;
            glow.type = LightType.Point; glow.color = new Color(1, .78f, .5f); glow.intensity = 9f; glow.range = 12f; glow.shadows = LightShadows.None;
            var fl = glow.gameObject.AddComponent<WaitingRoomFlickerLight>(); fl.bulbs = liveBulbs.Take(8).ToArray(); fl.averageGap = 11; fl.dipLevel = new Vector2(.45f, .8f);
            // Concept art: bulb strings fan out from the tent's tip to the top of the hall wall. A string that would
            // cross the stairs, deck or any fitting is skipped.
            var tip = apex + Vector3.up * .3f * K; int strings = 0;
            float wallR = new Vector2(floor.extents.x, floor.extents.z).magnitude / Mathf.Sqrt(2) - .3f;
            for (int k = 0; k < 8; k++)
            {
                float deg = 22.5f + k * 45f; var a = deg * Mathf.Deg2Rad;
                var end = new Vector3(hub.x + Mathf.Cos(a) * wallR, hub.y + 6.3f, hub.z + Mathf.Sin(a) * wallR);
                Vector3 S(float t) => Vector3.Lerp(tip, end, t) + Vector3.down * 1.1f * 4 * t * (1 - t);
                bool clear = true; Vector3 last = S(.04f);
                for (int i = 2; i <= 25 && clear; i++) { var q = S(i / 25f * .9f + .02f); /* the last stretch ends on the wall hook itself */ if (Physics.Linecast(last, q, ~0, QueryTriggerInteraction.Ignore)) clear = false; last = q; }
                if (!clear) continue;
                var strand = Group(g, "BulbStrand", tip); int count = Mathf.CeilToInt((end - tip).magnitude / .85f);
                Vector3 wp = tip;
                for (int i = 1; i <= count; i++)
                {
                    var q = S((float)i / count); Rod(strand, "StrandWire", wp, q, .006f, wire);
                    if (i < count) { var bm = rng.Next(10) == 0 ? dead : warm; Prim(strand, PrimitiveType.Sphere, "StrandBulb", q + Vector3.down * .07f, new Vector3(.07f, .09f, .07f), bm).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off; }
                    wp = q;
                }
                Prim(strand, PrimitiveType.Cube, "WallHook", end, new Vector3(.12f, .12f, .12f), Mat("V4_Iron_Dark"));
                strings++;
            }
            var rugMesh = Prim(g, PrimitiveType.Cylinder, "WornRoundRug", at + Vector3.up * .006f, new Vector3(8.4f, .004f, 7.7f), Mat("LA_CarpetWorn"));
            Object.DestroyImmediate(rugMesh.GetComponent<Collider>());

            // "Presents": crates, gift-wrapped boxes and circus tools tumbled around the hem.
            int props = 0;
            Vector3 Around(float ring, float deg) { ring += baseR - 1.15f; var a = deg * Mathf.Deg2Rad; return g.TransformPoint(new Vector3(Mathf.Cos(a) * ring, 0, Mathf.Sin(a) * ring)); }
            foreach (var (deg, ring, w, h, d, ribbon) in new[] { (-20f, 1.55f, .55f, .42f, .5f, 0), (15f, 1.45f, .38f, .3f, .38f, 1), (95f, 1.6f, .7f, .5f, .55f, 2), (150f, 1.5f, .42f, .36f, .42f, 0), (200f, 1.65f, .5f, .28f, .6f, 1), (255f, 1.5f, .34f, .34f, .34f, 2), (320f, 1.6f, .6f, .45f, .45f, 1) })
            {
                var p = Around(ring, deg); var box = Group(g, "GiftBox", p + Vector3.up * h * .5f); box.rotation = Quaternion.Euler(R(-4, 4), R(0, 360), R(-6, 9));
                var body = new[] { "LA_Cardboard", "LA_TentCream", "LA_TentRed" }[ribbon];
                LBox(box, "Box", Vector3.zero, new Vector3(w, h, d), Mat(body), true);
                var tie = ribbon == 2 ? Mat("LA_TentCream") : Mat("LA_TentRed");
                LBox(box, "Ribbon", Vector3.zero, new Vector3(w + .01f, h + .01f, .06f), tie); LBox(box, "Ribbon", Vector3.zero, new Vector3(.06f, h + .01f, d + .01f), tie);
                if (rng.Next(2) == 0) { LShape(box, PrimitiveType.Sphere, "Bow", new Vector3(-.06f, h * .5f + .03f, 0), new Vector3(.14f, .05f, .08f), tie, new Vector3(0, 0, 25)); LShape(box, PrimitiveType.Sphere, "Bow", new Vector3(.06f, h * .5f + .03f, 0), new Vector3(.14f, .05f, .08f), tie, new Vector3(0, 0, -25)); }
                props++;
            }
            // Juggling clubs, striped balls, a hoop, a little drum, a top hat and a bear.
            for (int i = 0; i < 4; i++)
            {
                var p = Around(R(1.3f, 2.1f), 40 + i * 23 + R(-6, 6)) + Vector3.up * .06f; var club = Group(g, "JugglingClub", p); club.rotation = Quaternion.Euler(R(80, 95), R(0, 360), 0);
                LShape(club, PrimitiveType.Capsule, "ClubBody", new Vector3(0, .12f, 0), new Vector3(.11f, .17f, .11f), i % 2 == 0 ? Mat("LA_TentCream") : Mat("LA_TentRed"));
                LShape(club, PrimitiveType.Cylinder, "ClubNeck", new Vector3(0, -.12f, 0), new Vector3(.035f, .12f, .035f), Mat("LA_TentCream"));
                LShape(club, PrimitiveType.Sphere, "ClubKnob", new Vector3(0, -.25f, 0), Vector3.one * .05f, Mat("LA_TentRed")); props++;
            }
            foreach (var (deg, size) in new[] { (175f, .32f), (232f, .24f), (288f, .28f) })
            {
                var p = Around(R(1.35f, 1.9f), deg); var ball = Group(g, "CircusBall", p + Vector3.up * size * .5f); ball.rotation = Quaternion.Euler(R(0, 360), R(0, 360), 0);
                LShape(ball, PrimitiveType.Sphere, "BallShell", Vector3.zero, Vector3.one * size, Mat("LA_TentRed"));
                LShape(ball, PrimitiveType.Cylinder, "BallStripe", Vector3.zero, new Vector3(size * 1.01f, size * .09f, size * 1.01f), Mat("LA_TentCream"));
                LShape(ball, PrimitiveType.Cylinder, "BallStripe", Vector3.zero, new Vector3(size * 1.01f, size * .09f, size * 1.01f), gold, new Vector3(90, 0, 0)); props++;
            }
            var hoopAt = Around(1.25f, 60); var hoop = MeshObject(g, "LeaningHoop", hoopAt + Vector3.up * .52f, SaveMesh(TorusMesh(.5f, .022f, 32, 6)), Mat("LA_HazardPaint"));
            hoop.transform.rotation = Quaternion.LookRotation(hoopAt - at) * Quaternion.Euler(-14, 0, 0); props++;
            var drumAt = Around(1.9f, 132); var drum = Group(g, "SmallDrum", drumAt + Vector3.up * .2f); drum.rotation = Quaternion.Euler(0, 0, 8);
            LShape(drum, PrimitiveType.Cylinder, "DrumShell", Vector3.zero, new Vector3(.45f, .2f, .45f), Mat("LA_TentRed"));
            foreach (float y in new[] { -.19f, .19f }) LShape(drum, PrimitiveType.Cylinder, "DrumRim", new Vector3(0, y, 0), new Vector3(.47f, .02f, .47f), gold);
            LShape(drum, PrimitiveType.Cylinder, "DrumHead", new Vector3(0, .205f, 0), new Vector3(.43f, .005f, .43f), Mat("LA_TentCream")); props++;
            var hatAt = Around(1.6f, 95) + Vector3.up * .5f; var hat = Group(g, "TopHat", hatAt + Vector3.up * .01f); hat.rotation = Quaternion.Euler(R(-12, -4), R(0, 360), R(4, 12));
            LShape(hat, PrimitiveType.Cylinder, "HatBrim", Vector3.zero, new Vector3(.34f, .008f, .34f), Mat("V4_Black"));
            LShape(hat, PrimitiveType.Cylinder, "HatCrown", new Vector3(0, .1f, 0), new Vector3(.2f, .1f, .2f), Mat("V4_Black"));
            LShape(hat, PrimitiveType.Cylinder, "HatBand", new Vector3(0, .035f, 0), new Vector3(.205f, .02f, .205f), Mat("LA_TentRed")); props++;
            foreach (var (path, deg, ring, scale) in new[] { ("Assets/_ThirdParty/Synty/PolygonApocalypse/Prefabs/Props/SM_Prop_Toy_Bear_01", 345f, 1.5f, .9f), ("Assets/_ThirdParty/Synty/PolygonApocalypse/Prefabs/Props/SM_Prop_Luggage_Retro_01", 225f, 2.05f, 1f), ("Assets/_ThirdParty/Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Crate_01", 75f, 2.15f, .7f) })
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path + ".prefab"); if (asset == null) continue;
                var t = ((GameObject)PrefabUtility.InstantiatePrefab(asset, g)).transform; Undo.RegisterCreatedObjectUndo(t.gameObject, "Tent prop");
                t.localScale = Vector3.one * scale; t.rotation = Quaternion.Euler(0, R(0, 360), 0);
                var b = Frame8VisibleBounds(t); var p = Around(ring, deg); t.position += new Vector3(p.x - b.center.x, at.y - b.min.y, p.z - b.center.z); props++;
            }
            return $"Christmas-tree tent at {at:F1}: {bulbs} fairy bulbs, {props} props underneath, {strings} bulb strings to the wall";
        });

        // Faded canvas: dirt rising from the ground, water streaks running down, mildew spots and sun-bleached patches.
        static Texture2D WornCanvasTexture() => GeneratedTexture("LA_TentCanvasWorn.png", 512, false, (u, v) =>
        {
            float weave = .94f + .06f * (Mathf.Sin(u * 900) * Mathf.Sin(v * 900));
            float dirt = Mathf.SmoothStep(.32f, 0f, v + (Fbm(u * 8, 2) - .5f) * .12f);
            float streak = Mathf.Pow(Mathf.PerlinNoise(u * 40, 3.3f), 6) * Mathf.SmoothStep(.95f, .3f, v) * 1.6f;
            float mildew = Mathf.SmoothStep(.72f, .8f, Fbm(u * 14 + 5, v * 14 + 2)) * .5f;
            float bleach = Mathf.SmoothStep(.55f, .75f, Fbm(u * 3 + 7, v * 3 + 1)) * .18f;
            float k = weave * (1 - dirt * .55f - streak * .25f - mildew * .35f) + bleach;
            return new Color(k * (1 - dirt * .08f), k * (1 - dirt * .12f), k * (1 - dirt * .2f));
        });

        static float ConeRadius(float baseR, float height, float h) => baseR * Mathf.Pow(Mathf.Clamp01(1 - h / height), .92f);

        static Material DoubleSided(Material m) { m.SetFloat("_Cull", 0); m.doubleSidedGI = true; EditorUtility.SetDirty(m); return m; }

        // 14 alternating gores, dented and slumped: random inward dents, a sagging middle and a crooked tip.
        static Mesh SlumpedCone(float baseR, float height)
        {
            const int gores = 14, cols = 4, rows = 12; var rng = new System.Random(7);
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tris = new[] { new List<int>(), new List<int>() };
            var dents = Enumerable.Range(0, 5).Select(_ => (a: (float)rng.NextDouble() * Mathf.PI * 2, h: .25f + (float)rng.NextDouble() * .5f, depth: .08f + (float)rng.NextDouble() * .1f)).ToArray();
            float sc = height / 2.75f;
            Vector3 P(float a, float t)
            {
                float h = t * height, r = ConeRadius(baseR, height, h);
                r *= 1 - .12f * Mathf.Sin(t * Mathf.PI) * (.6f + .4f * Mathf.Sin(a * 3 + 1)); // sag between ribs
                foreach (var d in dents) { float da = Mathf.DeltaAngle(a * Mathf.Rad2Deg, d.a * Mathf.Rad2Deg) / 40f, dh = (t - d.h) / .14f; r -= d.depth * sc * Mathf.Exp(-(da * da + dh * dh)); }
                var lean = new Vector3(.28f, 0, .12f) * sc * Mathf.Pow(t, 2.2f); float droop = -.12f * sc * Mathf.Pow(t, 4);
                return new Vector3(Mathf.Cos(a) * Mathf.Max(r, .01f), h + droop, Mathf.Sin(a) * Mathf.Max(r, .01f)) + lean;
            }
            // Worn canvas: a ragged, uneven hem, several holes of different shapes, and gores sagging apart at seams.
            var holes = new Dictionary<int, Func<int, int, bool>>
            {
                [4] = (i, j) => j < 4 && i > 0 && i < 3,
                [9] = (i, j) => j >= 5 && j <= 7 && (i == 1 || (i == 2 && j == 6)),
                [12] = (i, j) => j >= 2 && j <= 3 && i >= 2,
                [6] = (i, j) => j == 8 && i == 1,
            };
            for (int s = 0; s < gores; s++)
            {
                int start = v.Count;
                for (int j = 0; j <= rows; j++)
                for (int i = 0; i <= cols; i++)
                {
                    float a = (s + (float)i / cols) * Mathf.PI * 2 / gores, t = (float)j / rows;
                    var p = P(a, t);
                    if (j == 0) p.y -= (float)rng.NextDouble() * .09f * sc; // uneven, stretched hem
                    v.Add(p); uv.Add(new Vector2((s * cols + i) / (float)cols * .5f, t));
                }
                holes.TryGetValue(s, out var hole);
                for (int j = 0; j < rows; j++)
                for (int i = 0; i < cols; i++)
                {
                    if (hole != null && hole(i, j)) continue;
                    if (j == 0 && rng.NextDouble() < .3) continue; // tattered hem
                    int a0 = start + j * (cols + 1) + i, b0 = a0 + 1, c0 = a0 + cols + 1, d0 = c0 + 1;
                    tris[s % 2].AddRange(new[] { a0, c0, b0, b0, c0, d0 });
                }
            }
            var mesh = new Mesh { name = "LA_XmasTentCone" }; mesh.SetVertices(v); mesh.SetUVs(0, uv);
            mesh.subMeshCount = 2; mesh.SetTriangles(tris[0], 0); mesh.SetTriangles(tris[1], 1);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds(); return mesh;
        }

        static Mesh StarMesh(float outer, float inner, float depth)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            for (int side = 0; side < 2; side++)
            {
                float z = side == 0 ? depth : -depth; int c = v.Count; v.Add(new Vector3(0, 0, z * 1.6f));
                for (int i = 0; i < 10; i++) { float a = Mathf.PI * .5f + i * Mathf.PI / 5, r = i % 2 == 0 ? outer * (1 - .15f * (i % 4 == 0 ? 1 : 0)) : inner; v.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0)); }
                for (int i = 0; i < 10; i++) { int a = c + 1 + i, b = c + 1 + (i + 1) % 10; if (side == 0) t.AddRange(new[] { c, b, a }); else t.AddRange(new[] { c, a, b }); }
            }
            var m = new Mesh { name = "LA_CrookedStar" }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }

        static Mesh TorusMesh(float radius, float tube, int seg, int sides)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i <= seg; i++)
            for (int j = 0; j <= sides; j++)
            {
                float a = i * Mathf.PI * 2 / seg, b = j * Mathf.PI * 2 / sides; var c = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * radius;
                v.Add(c + (new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b)) * tube);
            }
            for (int i = 0; i < seg; i++)
            for (int j = 0; j < sides; j++)
            { int a = i * (sides + 1) + j, b = a + sides + 1; t.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 }); }
            var m = new Mesh { name = "LA_Hoop" }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }
    }
}
