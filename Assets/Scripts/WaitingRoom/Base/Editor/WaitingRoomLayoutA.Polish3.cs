using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.Editor
{
    // Third polish round: text that no longer shows through walls, powered-off monitors, realistic mascot heads
    // on the retired troupe, and a fallen lamp glowing like a campfire in front of the lobby spawn.
    public static partial class WaitingRoomLayoutA
    {
        const string DepthTextPath = "Assets/Scenes/Tests/WaitingRoom/Base/Materials/GB_Text.mat";

        // TextMesh's default font material ignores depth, so labels show through walls. The greybox text material depth-tests.
        static void DepthText(TextMesh t)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(DepthTextPath);
            if (m != null && t.TryGetComponent(out MeshRenderer r)) r.sharedMaterial = m;
        }

        public static string FixTextThroughWalls() => RunPass("Depth-tested text", target =>
        {
            int fixedCount = 0;
            foreach (var t in target.GetComponentsInChildren<TextMesh>(true))
            {
                var r = t.GetComponent<MeshRenderer>();
                if (r == null || r.sharedMaterial == null || r.sharedMaterial.shader.name != "GUI/Text Shader") continue;
                Undo.RecordObject(r, "Depth-tested text"); DepthText(t); fixedCount++;
            }
            return $"{fixedCount} labels now hidden behind walls";
        });

        // ================= Powered-off monitors in the storage room =================

        public static string PowerDownStorageMonitors() => RunPass("Storage monitors powered off", target =>
        {
            var g = target.Find("S05_JunkStorage/AbandonedResearchStorage") ?? throw new InvalidOperationException("Storage dressing missing");
            int rebuilt = 0;
            // The stencilled heading above the screens goes with the screen text.
            foreach (var label in g.Cast<Transform>().Where(t => t.name.StartsWith("Label_ARCHIVE")).ToArray()) { Undo.DestroyObjectImmediate(label.gameObject); rebuilt++; }
            var wall = g.Find("LargeMonitoringScreen");
            if (wall != null)
            {
                var lp = wall.localPosition; Undo.DestroyObjectImmediate(wall.gameObject);
                AbMonitor(g, lp, 5.2f, 2.35f, true); rebuilt++;
            }
            foreach (var pc in g.GetComponentsInChildren<Transform>(true).Where(t => t.name == "DormantComputer" || t.name == "DeadTerminal").ToArray())
            {
                var parent = pc.parent; var lp = pc.localPosition; bool broken = pc.name == "DeadTerminal";
                Undo.DestroyObjectImmediate(pc.gameObject); AbComputer(parent, lp, broken); rebuilt++;
            }
            return $"{rebuilt} screens rebuilt switched off; no screen text left: {g.GetComponentsInChildren<TextMesh>().All(t => !t.text.Contains("SIGNAL"))}";
        });

        // ================= Realistic mascot heads on the retired troupe =================

        static Material Felt(string name, Color tint)
        {
            var m = Lit(name, tint, .08f, FeltTexture(), FeltNormal());
            m.SetFloat("_BumpScale", .7f); EditorUtility.SetDirty(m); return m;
        }

        static Texture2D FeltTexture() => GeneratedTexture("LA_MascotFelt.png", 256, false, (u, v) =>
        {
            float fibre = Mathf.PerlinNoise(u * 180, v * 60) * .5f + Mathf.PerlinNoise(u * 60, v * 180) * .5f;
            float pilling = Mathf.PerlinNoise(u * 40 + 3, v * 40 + 9) > .7f ? .9f : 1f;
            float grime = 1 - Mathf.SmoothStep(.55f, .8f, Fbm(u * 4 + 2, v * 4 + 5)) * .35f;
            float k = (.82f + .18f * fibre) * pilling * grime;
            return new Color(k, k, k);
        });

        static Texture2D FeltNormal()
        {
            float H(float u, float v) => Mathf.PerlinNoise(u * 180, v * 60) * .5f + Mathf.PerlinNoise(u * 60, v * 180) * .5f;
            return GeneratedTexture("LA_MascotFelt_N.png", 256, true, (u, v) =>
            {
                const float e = 1f / 256; var n = new Vector3(-(H(u + e, v) - H(u - e, v)) * 3, -(H(u, v + e) - H(u, v - e)) * 3, 1).normalized;
                return new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f);
            });
        }

        static Material Glossy(string name, Color c, float smooth = .85f)
        {
            var m = Lit(name, c, smooth); m.SetFloat("_SpecularHighlights", 1); m.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.SetFloat("_EnvironmentReflections", 0); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF"); EditorUtility.SetDirty(m); return m;
        }

        // Lumpy ellipsoid: stuffing settles unevenly, so the surface sags and bulges a little.
        static Mesh LumpyEllipsoid(string name, Vector3 radii, int seed, float lump = .04f, Func<Vector3, Vector3> shape = null)
        {
            const int lon = 28, lat = 20; var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int j = 0; j <= lat; j++)
            for (int i = 0; i <= lon; i++)
            {
                float th = j * Mathf.PI / lat, ph = i * Mathf.PI * 2 / lon;
                var d = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                float n = 1 + lump * (Mathf.PerlinNoise(d.x * 2.2f + seed, d.y * 2.2f + d.z * 1.7f) - .5f) * 2;
                var p = Vector3.Scale(d, radii) * n; if (shape != null) p = shape(p);
                v.Add(p); uv.Add(new Vector2((float)i / lon * 2, (float)j / lat));
                if (i < lon && j < lat) { int a = j * (lon + 1) + i, b = a + lon + 1; t.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b }); }
            }
            var m = new Mesh { name = name }; m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds(); return m;
        }

        static GameObject Part(Transform head, string name, Mesh mesh, Vector3 at, Quaternion rot, Material m)
        {
            var go = MeshObject(head, name, head.TransformPoint(at), SaveMesh(mesh), m); go.transform.localRotation = rot;
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On; return go;
        }

        // Seam: a row of short stitches following a curve over the head.
        static void Seam(Transform head, Func<float, Vector3> curve, int count, Material thread)
        {
            for (int i = 0; i < count; i++)
            {
                float s = (i + .5f) / count; var p = curve(s); var dir = (curve(Mathf.Min(1, s + .01f)) - curve(Mathf.Max(0, s - .01f))).normalized;
                var st = LShape(head, PrimitiveType.Cube, "Stitch", p, new Vector3(.006f, .022f, .004f), thread);
                st.localRotation = Quaternion.LookRotation(p.normalized, Vector3.Cross(dir, p.normalized));
            }
        }

        static Transform MascotHead(Transform parent, string kind)
        {
            var h = new GameObject("RealMascotHead_" + kind).transform; Undo.RegisterCreatedObjectUndo(h.gameObject, "Mascot head");
            h.SetParent(parent, false);
            Material thread = Mat("V4_Black"), eyeGloss = Glossy("LA_MascotEyeGloss", new Color(.02f, .02f, .025f), .92f);
            Material stuffing = Felt("LA_MascotStuffing", new Color(.86f, .82f, .72f));
            switch (kind)
            {
                case "Bear":
                {
                    Material fur = Felt("LA_MascotGreen", new Color(.3f, .4f, .2f)), muzzle = Felt("LA_MascotMuzzle", new Color(.76f, .69f, .52f)), patchM = Felt("LA_MascotPatchPlaid", new Color(.5f, .25f, .2f));
                    Part(h, "BearHead", LumpyEllipsoid("LA_BearHead", new Vector3(.3f, .27f, .28f), 3, .05f, p => p + new Vector3(0, 0, p.y < -.05f ? .03f : 0)), Vector3.zero, Quaternion.identity, fur);
                    Part(h, "BearMuzzle", LumpyEllipsoid("LA_BearMuzzle", new Vector3(.15f, .1f, .12f), 8, .06f), new Vector3(0, -.08f, .22f), Quaternion.identity, muzzle);
                    Part(h, "BearNose", LumpyEllipsoid("LA_BearNose", new Vector3(.055f, .035f, .04f), 2, .02f), new Vector3(0, -.03f, .33f), Quaternion.Euler(-10, 0, 0), eyeGloss);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Part(h, "BearEar", LumpyEllipsoid("LA_BearEar", new Vector3(.1f, .1f, .04f), 5, .05f), new Vector3(s * .21f, .22f, -.02f), Quaternion.Euler(-10, s * 20, s * -25), fur);
                        Part(h, "BearEarInner", LumpyEllipsoid("LA_BearEarIn", new Vector3(.065f, .065f, .02f), 6, .03f), new Vector3(s * .215f, .215f, .02f), Quaternion.Euler(-10, s * 20, s * -25), muzzle);
                    }
                    // One button eye; the other side mended with a plaid patch sewn over it.
                    var button = LShape(h, PrimitiveType.Cylinder, "ButtonEye", new Vector3(-.1f, .05f, .27f), new Vector3(.07f, .012f, .07f), eyeGloss, new Vector3(80, 0, 0));
                    for (int k = 0; k < 4; k++) LShape(h, PrimitiveType.Cylinder, "ButtonHole", new Vector3(-.1f + (k % 2 - .5f) * .022f, .05f + (k / 2 - .5f) * .022f, .282f), new Vector3(.008f, .004f, .008f), Mat("LA_Paper"), new Vector3(80, 0, 0));
                    var patch = LBox(h, "MendedEyePatch", new Vector3(.11f, .06f, .262f), new Vector3(.12f, .1f, .012f), patchM); patch.localRotation = Quaternion.Euler(-8, 22, 9);
                    for (int k = 0; k < 4; k++) LShape(h, PrimitiveType.Cube, "CrossStitch", new Vector3(.11f + (k % 2 == 0 ? -.03f : .03f), .06f + (k < 2 ? .02f : -.02f), .275f), new Vector3(.004f, .03f, .003f), thread, new Vector3(-8, 22, k % 2 == 0 ? 45 : -45));
                    Seam(h, s => new Vector3(Mathf.Lerp(-.04f, .07f, s), Mathf.Lerp(-.12f, -.13f, s) - .015f * Mathf.Sin(s * Mathf.PI), .335f - .01f * Mathf.Sin(s * Mathf.PI)), 6, thread);
                    Seam(h, s => { float a = Mathf.Lerp(.2f, 2.9f, s); return new Vector3(0, Mathf.Sin(a) * .275f, Mathf.Cos(a) * .285f); }, 18, thread);
                    // Little top hat, knocked askew.
                    var hat = Group(h, "SmallTopHat", h.TransformPoint(new Vector3(.06f, .27f, -.02f))); hat.localRotation = Quaternion.Euler(-6, 10, -14);
                    LShape(hat, PrimitiveType.Cylinder, "Brim", Vector3.zero, new Vector3(.22f, .006f, .22f), Mat("V4_Black"));
                    LShape(hat, PrimitiveType.Cylinder, "Crown", new Vector3(0, .07f, 0), new Vector3(.14f, .07f, .14f), Mat("V4_Black"));
                    LShape(hat, PrimitiveType.Cylinder, "Band", new Vector3(0, .02f, 0), new Vector3(.142f, .016f, .142f), Mat("LA_TentRed"));
                    // Split cheek with stuffing pushing out.
                    LBox(h, "CheekTear", new Vector3(-.2f, -.08f, .17f), new Vector3(.06f, .04f, .01f), Mat("V4_Black")).localRotation = Quaternion.Euler(0, -48, 20);
                    for (int k = 0; k < 3; k++) Part(h, "Stuffing", LumpyEllipsoid("LA_Stuffing" + k, Vector3.one * (.025f + k * .006f), 11 + k, .3f), new Vector3(-.21f - k * .012f, -.08f + k * .012f, .18f), Quaternion.identity, stuffing);
                    break;
                }
                case "Rabbit":
                {
                    Material fur = Felt("LA_MascotYellow", new Color(.78f, .66f, .27f)), inner = Felt("LA_MascotPink", new Color(.72f, .46f, .45f)), muzzle = Felt("LA_MascotMuzzle", new Color(.76f, .69f, .52f)), patchM = Felt("LA_MascotPatchBlue", new Color(.22f, .3f, .45f));
                    // Angular, hand-stitched face: front flattened into planes.
                    Part(h, "RabbitHead", LumpyEllipsoid("LA_RabbitHead", new Vector3(.25f, .3f, .26f), 4, .04f, p => new Vector3(p.x * (p.z > .12f ? .9f : 1), p.y, p.z > .16f ? .16f + (p.z - .16f) * .45f : p.z)), Vector3.zero, Quaternion.identity, fur);
                    Part(h, "RabbitMuzzle", LumpyEllipsoid("LA_RabbitMuzzle", new Vector3(.12f, .08f, .07f), 9, .05f), new Vector3(0, -.09f, .2f), Quaternion.identity, muzzle);
                    Part(h, "RabbitNose", LumpyEllipsoid("LA_RabbitNose", new Vector3(.035f, .025f, .02f), 3, .02f), new Vector3(0, -.05f, .265f), Quaternion.identity, Glossy("LA_MascotNosePink", new Color(.6f, .3f, .32f), .55f));
                    foreach (float s in new[] { -.025f, .025f }) LBox(h, "BuckTooth", new Vector3(s, -.15f, .245f), new Vector3(.044f, .055f, .015f), Glossy("LA_MascotTooth", new Color(.86f, .84f, .74f), .5f)).localRotation = Quaternion.Euler(8, 0, 0);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Part(h, "RabbitEye", LumpyEllipsoid("LA_RabbitEye", new Vector3(.045f, .055f, .025f), 1, .0f), new Vector3(s * .09f, .05f, .245f), Quaternion.Euler(0, s * 18, 0), eyeGloss);
                        for (int k = 0; k < 3; k++) LRod(h, "Whisker", new Vector3(s * .08f, -.08f + k * .015f, .25f), new Vector3(s * .27f, -.06f + k * .03f - .03f, .21f), .0025f, thread);
                    }
                    // Long ears: the left upright, the right broken and drooping over the face.
                    var earMesh = LumpyEllipsoid("LA_RabbitEar", new Vector3(.065f, .2f, .03f), 7, .05f);
                    var innerMesh = LumpyEllipsoid("LA_RabbitEarIn", new Vector3(.04f, .16f, .012f), 8, .04f);
                    Part(h, "EarLeft", earMesh, new Vector3(-.09f, .43f, -.02f), Quaternion.Euler(-8, 0, 12), fur);
                    Part(h, "EarLeftInner", innerMesh, new Vector3(-.088f, .43f, .01f), Quaternion.Euler(-8, 0, 12), inner);
                    var root = Group(h, "EarRightBend", h.TransformPoint(new Vector3(.1f, .26f, 0))); root.localRotation = Quaternion.Euler(0, 0, -18);
                    Part(root, "EarRightBase", LumpyEllipsoid("LA_RabbitEarBase", new Vector3(.065f, .09f, .03f), 2, .05f), new Vector3(0, .07f, 0), Quaternion.identity, fur);
                    var tip = Group(root, "EarRightTip", root.TransformPoint(new Vector3(0, .15f, 0))); tip.localRotation = Quaternion.Euler(-60, 0, -95);
                    Part(tip, "EarRightDroop", LumpyEllipsoid("LA_RabbitEarTip", new Vector3(.06f, .14f, .028f), 4, .06f), new Vector3(0, .12f, 0), Quaternion.identity, fur);
                    Part(tip, "EarRightDroopInner", LumpyEllipsoid("LA_RabbitEarTipIn", new Vector3(.035f, .11f, .01f), 5, .04f), new Vector3(0, .12f, .022f), Quaternion.identity, inner);
                    LBox(h, "ForeheadPatch", new Vector3(-.06f, .18f, .2f), new Vector3(.1f, .09f, .012f), patchM).localRotation = Quaternion.Euler(-40, -15, -12);
                    Seam(h, s => new Vector3(0, Mathf.Lerp(.16f, -.12f, s), .165f + .08f * Mathf.Sin(s * Mathf.PI) * .4f), 12, thread);
                    Seam(h, s => { float a = Mathf.Lerp(-1.2f, 1.2f, s); return new Vector3(Mathf.Sin(a) * .245f, .01f, Mathf.Cos(a) * .2f); }, 14, thread);
                    break;
                }
                default:
                {
                    Material blueFelt = Felt("LA_MascotBlue", new Color(.22f, .32f, .55f)), face = Glossy("LA_MascotGreasepaint", new Color(.84f, .81f, .74f), .35f), hair = Felt("LA_MascotBlueHair", new Color(.18f, .3f, .62f)), ruff = Felt("LA_MascotRuff", new Color(.78f, .74f, .66f));
                    Part(h, "ClownHead", LumpyEllipsoid("LA_ClownHead", new Vector3(.27f, .29f, .27f), 6, .03f), Vector3.zero, Quaternion.identity, blueFelt);
                    Part(h, "ClownFace", LumpyEllipsoid("LA_ClownFace", new Vector3(.23f, .25f, .14f), 2, .02f), new Vector3(0, -.03f, .15f), Quaternion.identity, face);
                    Part(h, "ClownNose", LumpyEllipsoid("LA_ClownNoseBall", Vector3.one * .05f, 1, .0f), new Vector3(0, -.04f, .3f), Quaternion.identity, Glossy("LA_ClownNoseRed", new Color(.62f, .05f, .05f), .7f));
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Part(h, "ClownEyeWhite", LumpyEllipsoid("LA_ClownEyeWhite", new Vector3(.04f, .035f, .015f), 3, 0), new Vector3(s * .08f, .05f, .275f), Quaternion.Euler(0, s * 15, s * 12), Glossy("LA_MascotEyeWhite", new Color(.85f, .84f, .8f), .7f));
                        Part(h, "ClownPupil", LumpyEllipsoid("LA_ClownPupil", new Vector3(.018f, .02f, .01f), 1, 0), new Vector3(s * .075f, .045f, .29f), Quaternion.identity, eyeGloss);
                        LBox(h, "PaintedTear", new Vector3(s * .095f, -.03f, .275f), new Vector3(.025f, .04f, .004f), Glossy("LA_ClownTearBlue", new Color(.15f, .28f, .6f), .5f)).localRotation = Quaternion.Euler(-15, s * 18, 45);
                        // Matted blue hair tufts on either side.
                        for (int k = 0; k < 4; k++) Part(h, "HairTuft", LumpyEllipsoid("LA_ClownTuft" + k, new Vector3(.09f, .07f, .08f), 20 + k, .25f), new Vector3(s * (.25f + k * .015f), .06f + k * .06f - .05f, -.03f - k * .03f), Quaternion.Euler(k * 20, 0, s * 20), hair);
                    }
                    // Drooping painted mouth.
                    for (int k = 0; k < 9; k++)
                    {
                        float a = Mathf.Lerp(-1f, 1f, k / 8f); var p = new Vector3(a * .09f, -.14f + Mathf.Pow(Mathf.Abs(a), 1.6f) * -.035f + .01f, .265f - Mathf.Abs(a) * .03f);
                        LShape(h, PrimitiveType.Sphere, "PaintedMouth", p, new Vector3(.03f, .018f, .01f), Glossy("LA_ClownMouthRed", new Color(.55f, .07f, .07f), .45f));
                    }
                    // Greasepaint peeled off in patches.
                    foreach (var (x, y, rz) in new[] { (.12f, -.1f, 30f), (-.06f, .14f, -15f), (.03f, -.19f, 70f) })
                        LBox(h, "PeeledPaint", new Vector3(x, y, .265f - Mathf.Abs(x) * .3f), new Vector3(.05f, .03f, .004f), Felt("LA_MascotBlue", new Color(.22f, .32f, .55f))).localRotation = Quaternion.Euler(0, x * 120, rz);
                    // Frayed ruff collar.
                    for (int k = 0; k < 16; k++)
                    {
                        float a = k * Mathf.PI * 2 / 16; var p = new Vector3(Mathf.Cos(a) * .24f, -.27f, Mathf.Sin(a) * .22f);
                        var frill = LBox(h, "RuffFrill", p, new Vector3(.12f, .07f, .02f), ruff); frill.localRotation = Quaternion.LookRotation(new Vector3(p.x, 0, p.z)) * Quaternion.Euler(-35 + (k % 2) * 25, 0, 0);
                        if (k % 4 == 1) LBox(h, "RuffThread", p + new Vector3(0, -.07f, 0), new Vector3(.005f, .1f, .003f), ruff).localRotation = Quaternion.Euler(0, 0, (k - 8) * 4);
                    }
                    break;
                }
            }
            return h;
        }

        public static string RealisticMascotHeads() => RunPass("Realistic mascot heads", target =>
        {
            var display = target.Find("S05_JunkStorage").GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "RetiredTroupeDisplay") ?? throw new InvalidOperationException("Exhibit missing");
            var made = new List<string>();
            foreach (var figure in display.Cast<Transform>().Where(t => t.name.Contains("_Missing")))
            {
                string kind = figure.name.Contains("Bear") ? "Bear" : figure.name.Contains("Rabbit") ? "Rabbit" : figure.name.Contains("Clown") ? "Clown" : null;
                if (kind == null) continue;
                var old = figure.Cast<Transform>().FirstOrDefault(t => t.name == "WornMascotHead" && t.gameObject.activeSelf);
                var previous = figure.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith("RealMascotHead_"));
                Transform reference = previous != null ? previous : old;
                if (reference == null) continue;
                var b = VisibleBounds(reference); var rot = reference.rotation;
                if (previous != null) Undo.DestroyObjectImmediate(previous.gameObject);
                if (old != null) { Undo.RecordObject(old.gameObject, "Retire primitive head"); old.gameObject.SetActive(false); }
                var head = MascotHead(figure, kind);
                // The prone rabbit's old head had its crown toward the torso; turn about the face axis so the ears lead.
                head.rotation = kind == "Rabbit" ? rot * Quaternion.Euler(0, 0, 180) : rot;
                var nb = VisibleBounds(head); head.localScale *= .62f / Mathf.Max(nb.size.x, Mathf.Max(nb.size.y, nb.size.z));
                nb = VisibleBounds(head); head.position += b.center - nb.center;
                made.Add($"{kind}({head.GetComponentsInChildren<Renderer>().Length} parts)");
            }
            return "Mascot heads: " + string.Join(", ", made);
        });

        // ================= Fallen lamp in front of the lobby spawn =================

        public static string FallenSpawnLamp() => RunPass("Fallen lamp glowing at the spawn", target =>
        {
            var lobby = target.Find("S01_Lobby"); var spawns = lobby.Find("Gameplay/SpawnPoints").Cast<Transform>().Where(t => t.name.StartsWith("Spawn_")).ToArray();
            var centre = spawns.Aggregate(Vector3.zero, (a, t) => a + t.position) / spawns.Length; var facing = spawns[0].forward; facing.y = 0; facing.Normalize();
            Remove(lobby, "FallenSpawnLamp");
            var at = new Vector3(centre.x, FloorAt(centre, 1), centre.z) + facing * 2.4f;
            var g = Group(lobby, "FallenSpawnLamp", at); g.rotation = Quaternion.LookRotation(-facing) * Quaternion.Euler(0, 28, 0);
            Material dark = Mat("V4_Iron_Dark"), rust = Mat("V4_RustyMetal"), black = Mat("V4_Black");
            var glass = Emissive("LA_FallenLampGlow", new Color(1, .7f, .4f), new Color(3.2f, 1.6f, .55f));
            // A caged work lamp knocked over, lying on its side with the lit bulb still burning.
            var lamp = Group(g, "ToppledWorkLamp", g.TransformPoint(new Vector3(0, .17f, 0))); lamp.localRotation = Quaternion.Euler(0, 0, 84);
            LShape(lamp, PrimitiveType.Cylinder, "LampShade", new Vector3(0, .02f, 0), new Vector3(.34f, .1f, .34f), rust);
            var bulb = LShape(lamp, PrimitiveType.Sphere, "GlowingBulb", new Vector3(0, -.1f, 0), new Vector3(.13f, .16f, .13f), glass);
            for (int i = 0; i < 6; i++) { float a = i * Mathf.PI / 3; LRod(lamp, "LampCage", new Vector3(Mathf.Cos(a) * .16f, -.06f, Mathf.Sin(a) * .16f), new Vector3(Mathf.Cos(a) * .05f, -.24f, Mathf.Sin(a) * .05f), .006f, dark); }
            LRod(lamp, "LampHandle", new Vector3(-.12f, .08f, 0), new Vector3(.12f, .08f, 0), .012f, dark);
            // Broken stand beside it and the cable snaking back toward the wall.
            LRod(g, "FallenStandPole", new Vector3(.25f, .03f, .1f), new Vector3(1.45f, .03f, .45f), .022f, dark);
            foreach (float d in new[] { -.15f, .15f }) LRod(g, "StandLeg", new Vector3(1.45f, .03f, .45f), new Vector3(1.7f + d, .03f, .45f + d * 2.5f), .015f, dark);
            Vector3 prev = new Vector3(-.05f, .02f, .05f);
            for (int i = 1; i <= 10; i++) { var p = new Vector3(-.05f - i * .22f, .015f, .05f + Mathf.Sin(i * .9f) * .18f); LRod(g, "LampCable", prev, p, .01f, black); prev = p; }
            // Broken glass from the impact.
            var rng = new System.Random(9);
            for (int i = 0; i < 9; i++) { var shard = LBox(g, "GlassShard", new Vector3((float)rng.NextDouble() * .6f - .1f, .005f, (float)rng.NextDouble() * .5f - .25f), new Vector3(.04f, .003f, .025f), Glossy("LA_GlassShard", new Color(.7f, .72f, .7f), .9f)); shard.localRotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360, 0); }
            // Campfire-like glow: low warm light with a soft, restless flicker and a warm pool on the floor.
            var light = new GameObject("CampfireGlow").AddComponent<Light>(); light.transform.SetParent(g, false); light.transform.localPosition = new Vector3(0, .22f, .1f);
            light.type = LightType.Point; light.color = new Color(1, .56f, .26f); light.intensity = 3.2f; light.range = 6.5f; light.shadows = LightShadows.Soft;
            var flicker = light.gameObject.AddComponent<WaitingRoomFlickerLight>(); flicker.bulbs = new[] { bulb.GetComponent<Renderer>() };
            flicker.hum = .2f; flicker.averageGap = 5f; flicker.dipsPerBurst = new Vector2Int(1, 3); flicker.dipLevel = new Vector2(.55f, .85f);
            var pool = Prim(g, PrimitiveType.Quad, "WarmFloorPool", at + Vector3.up * .012f, new Vector3(3.2f, 2.6f, 1), StageMaterial("LA_FloorGlowWarm", "Universal Render Pipeline/Particles/Unlit", GeneratedTexture("LA_FloorGlow.png", 128, false, (u, v) => { float r = new Vector2(u - .5f, v - .5f).magnitude * 2; float k = Mathf.Pow(Mathf.Clamp01(1 - r), 2.2f) * .5f; return new Color(k, k, k); }, TextureWrapMode.Clamp), true, new Color(1f, .5f, .2f)));
            pool.rotation = Quaternion.Euler(90, g.eulerAngles.y, 0); pool.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return $"Fallen lamp at {at:F1}, {Vector3.Distance(at, centre):F1} m in front of the spawn line";
        });
    }
}
