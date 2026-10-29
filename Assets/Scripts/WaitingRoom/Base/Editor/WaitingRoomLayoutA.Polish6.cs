using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.Editor
{
    // Sixth round: the B1 office becomes a working security room. A console desk faces a wall of three large CCTV
    // monitors that cycle through cameras mounted round the waiting room, a locked weapon rack and real shelving and
    // filing cabinets replace the greybox boxes, and the chair sits at the desk.
    public static partial class WaitingRoomLayoutA
    {
        // Room interior (inner wall faces) of B1_Office.
        const float SecX0 = -11.0f, SecX1 = -5.1f, SecZ0 = 101.85f, SecZ1 = 109.05f, SecFloor = -5.0f;

        // Waiting-room spots the CCTV watches: label, floor target, direction from the target towards the wall the camera hangs on.
        static readonly (string name, Vector3 target, Vector3 toWall)[][] CctvFeeds =
        {
            new[] { ("로비", new Vector3(0, 0, 70), new Vector3(0, 0, 1)), ("로비 서쪽", new Vector3(0, 0, 70), new Vector3(-1, 0, .3f)), ("무대 통로", new Vector3(-6, 0, 38.2f), new Vector3(0, 0, 1)) },
            new[] { ("연습실", new Vector3(-38.5f, 0, 92.8f), new Vector3(1, 0, 1)), ("화물 적재장", new Vector3(-43, 0, 67), new Vector3(1, 0, 1)), ("무대 장치 보관실", new Vector3(-43, 0, 40), new Vector3(1, 0, -1)) },
            new[] { ("숙소", new Vector3(16, 0, 74), new Vector3(1, 0, 1)), ("폐기물 보관소", new Vector3(28, 0, 38.4f), new Vector3(-1, 0, 1)), ("B1 기계실", new Vector3(3.7f, -5, 89), new Vector3(1, 0, 1)) },
        };

        public static string RefitSecurityOffice() => RunPass("Security office refit", target =>
        {
            var office = target.Find("B1_Office"); var old = office.Find("Office");
            var room = Group(office, "SecurityRoom", new Vector3((SecX0 + SecX1) * .5f, SecFloor, (SecZ0 + SecZ1) * .5f));
            int cleared = 0;
            foreach (Transform t in old.Cast<Transform>().ToArray())
                if (t.name is "Desk" or "DeadMonitor" or "MonitorGlass" or "DeskLamp" or "DeskLampShade" or "FilingCabinet" || PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))
                { Undo.RecordObject(t.gameObject, "Clear greybox"); t.gameObject.SetActive(false); cleared++; }
            // The key board slides south along the east wall to make room for the monitors.
            foreach (Transform t in old) if (t.name is "KeyBoard" or "Key") { Undo.RecordObject(t, "Move key board"); t.position += Vector3.back * .85f; }

            var mounts = BuildCctvCameras(target);
            var wall = MonitorWall(room, mounts);
            var desk = SecurityConsole(room);
            var rack = WeaponRack(room);
            var store = OfficeStorage(room);
            return $"Security office: cleared {cleared} greybox pieces; {wall}; {desk}; {rack}; {store}; {mounts.Sum(m => m.Length)} CCTV cameras";
        });

        // ---------- CCTV cameras round the waiting room ----------
        static Transform[][] BuildCctvCameras(Transform target)
        {
            var g = Group(target, "SecurityCctv", Vector3.zero);
            Material housing = Lit("LA_CctvHousing", new Color(.78f, .78f, .74f), .35f), dark = Mat("V4_Black"), steel = Mat("V4_Iron_Dark");
            var result = new Transform[CctvFeeds.Length][];
            for (int m = 0; m < CctvFeeds.Length; m++)
            {
                result[m] = new Transform[CctvFeeds[m].Length];
                for (int i = 0; i < CctvFeeds[m].Length; i++)
                {
                    var (name, t, toWall) = CctvFeeds[m][i]; var d = toWall.normalized;
                    float floor = Physics.Raycast(t + Vector3.up * 3, Vector3.down, out var fh, 10, ~0, QueryTriggerInteraction.Ignore) ? fh.point.y : t.y;
                    var o = new Vector3(t.x, floor + 3.5f, t.z);
                    var wallHit = Physics.Raycast(o, d, out var wh, 40, ~0, QueryTriggerInteraction.Ignore) ? wh.point : o + d * 15;
                    var at = wallHit - d * .35f;
                    float ceil = Physics.Raycast(at, Vector3.up, out var ch, 20, ~0, QueryTriggerInteraction.Ignore) ? ch.point.y : floor + 6;
                    at.y = Mathf.Min(floor + 5.2f, ceil - .4f);
                    var cam = Group(g, "Cctv_" + name, at); cam.rotation = Quaternion.LookRotation(new Vector3(t.x, floor + .8f, t.z) - at);
                    LBox(cam, "Housing", new Vector3(0, 0, -.08f), new Vector3(.13f, .12f, .3f), housing);
                    LBox(cam, "SunShield", new Vector3(0, .075f, -.05f), new Vector3(.17f, .015f, .38f), housing);
                    LRod(cam, "Lens", new Vector3(0, 0, .06f), new Vector3(0, 0, .09f), .045f, dark);
                    var led = LBox(cam, "Led", new Vector3(.045f, -.04f, .075f), new Vector3(.015f, .015f, .01f), Bulb()); led.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                    Rod(cam, "WallArm", cam.TransformPoint(new Vector3(0, -.06f, -.18f)), new Vector3(wallHit.x, at.y - .12f, wallHit.z), .02f, steel);
                    foreach (var r in cam.GetComponentsInChildren<Renderer>()) if (r.name != "WallArm") r.shadowCastingMode = ShadowCastingMode.Off;
                    var lens = Group(cam, "View", cam.TransformPoint(new Vector3(0, 0, .12f))); lens.rotation = cam.rotation;
                    result[m][i] = lens;
                }
            }
            return result;
        }

        // ---------- Three large monitors on a steel rail along the east wall ----------
        static string MonitorWall(Transform room, Transform[][] mounts)
        {
            var g = Group(room, "MonitorWall", new Vector3(SecX1, SecFloor, 105.9f)); g.rotation = Quaternion.LookRotation(Vector3.left); // +z faces into the room
            Material steel = Mat("V4_Iron_Dark"), bezel = Lit("LA_MonitorBezel", new Color(.05f, .05f, .055f), .3f), scan = CctvScanlineMaterial();
            const float W = 1.55f, H = .97f, gap = .1f, bottom = 1.05f, standOff = .32f;
            // Rails and wall posts (stand off past the steel columns on this wall).
            foreach (float y in new[] { bottom + .2f, bottom + H - .2f }) LBox(g, "Rail", new Vector3(0, y, standOff - .04f), new Vector3(3 * W + 2 * gap + .3f, .05f, .04f), steel);
            foreach (float x in new[] { -2.45f, 2.45f }) LBox(g, "RailPost", new Vector3(x, bottom + H * .5f, standOff * .5f - .02f), new Vector3(.05f, H - .3f, standOff), steel);
            for (int i = 0; i < 3; i++)
            {
                float x = (1 - i) * (W + gap); // channel 1 on the left as seen from the chair
                var mon = Group(g, "CctvMonitor_" + (i + 1), g.TransformPoint(new Vector3(x, bottom + H * .5f, standOff + .05f))); mon.localRotation = Quaternion.identity;
                LBox(mon, "Bezel", Vector3.zero, new Vector3(W, H, .08f), bezel);
                LBox(mon, "Back", new Vector3(0, 0, -.07f), new Vector3(W * .6f, H * .55f, .07f), bezel);
                LBox(mon, "Badge", new Vector3(W * .5f - .12f, -H * .5f + .025f, .041f), new Vector3(.06f, .012f, .004f), Mat("LA_Bronze"));
                var screen = LShape(mon, PrimitiveType.Quad, "Screen", new Vector3(0, .01f, .0415f), new Vector3(W - .09f, H - .1f, 1), CctvScreenMaterial(i, mounts[i][0]), new Vector3(0, 180, 0));
                screen.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                var lines = LShape(mon, PrimitiveType.Quad, "Scanlines", new Vector3(0, .01f, .043f), new Vector3(W - .09f, H - .1f, 1), scan, new Vector3(0, 180, 0));
                lines.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                var tag = Group(mon, "Channel", mon.TransformPoint(new Vector3(-W * .5f + .1f, H * .5f - .1f, .046f))); tag.rotation = mon.rotation * Quaternion.Euler(0, 180, 0);
                var tm = tag.gameObject.AddComponent<TextMesh>(); DepthText(tm);
                tm.fontSize = 48; tm.characterSize = .012f; tm.anchor = TextAnchor.UpperRight; tm.color = new Color(.85f, .95f, .85f);
                // Bracket arms back to the rails.
                foreach (float sx in new[] { -.3f, .3f }) LBox(mon, "Arm", new Vector3(sx, 0, -.16f), new Vector3(.04f, .3f, .2f), steel);
                var feed = Undo.AddComponent<WaitingRoomCctvMonitor>(mon.gameObject);
                feed.screen = screen.GetComponent<Renderer>(); feed.label = tm; feed.mounts = mounts[i];
                feed.mountNames = CctvFeeds[i].Select(f => f.name).ToArray(); feed.firstChannel = i * 3 + 1;
                tm.text = $"CAM {feed.firstChannel:00}  {feed.mountNames[0]}";
            }
            // Cable loom from the monitors down behind the desk, and the screens' cold glow.
            LRod(g, "CableLoom", new Vector3(-2.3f, bottom + .2f, standOff - .08f), new Vector3(-2.3f, .05f, standOff - .08f), .035f, Mat("V4_Black"));
            var glow = new GameObject("ScreenGlow"); Undo.RegisterCreatedObjectUndo(glow, "Screen glow"); glow.transform.SetParent(g, false);
            glow.transform.localPosition = new Vector3(0, bottom + H * .5f, 1.4f);
            var light = glow.AddComponent<Light>(); light.type = LightType.Point; light.color = new Color(.62f, .78f, .74f); light.intensity = .9f; light.range = 4.5f; light.shadows = LightShadows.None;
            return "3 CCTV monitors 1.55 x 0.97 m";
        }

        static Material CctvScreenMaterial(int i, Transform firstMount)
        {
            // A still of the first camera stands in until the live feed runs (and in the editor).
            string png = $"{AssetDir}/LA_CctvStill_{i + 1}.png";
            var go = new GameObject("StillCam"); var cam = go.AddComponent<Camera>(); cam.fieldOfView = 70; cam.nearClipPlane = .1f; cam.farClipPlane = 60;
            go.transform.SetPositionAndRotation(firstMount.position, firstMount.rotation);
            var rt = new RenderTexture(384, 216, 24); cam.targetTexture = rt; cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(384, 216, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 384, 216), 0, 0); tex.Apply();
            RenderTexture.active = prev; cam.targetTexture = null;
            File.WriteAllBytes(png, tex.EncodeToPNG()); Object.DestroyImmediate(tex); Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
            AssetDatabase.ImportAsset(png, ImportAssetOptions.ForceUpdate);
            string path = $"{AssetDir}/LA_CctvScreen_{i + 1}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(png));
            m.SetColor("_BaseColor", new Color(.82f, .95f, .88f)); // the cold cast of old CCTV
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material CctvScanlineMaterial()
        {
            string path = $"{AssetDir}/LA_CctvScanlines.mat";
            // Dark horizontal lines with a little static, as alpha over the picture.
            string png = $"{AssetDir}/LA_CctvScanlines.png";
            var px = new Texture2D(64, 256, TextureFormat.RGBA32, false);
            for (int y = 0; y < 256; y++) for (int x = 0; x < 64; x++)
                px.SetPixel(x, y, new Color(0, 0, 0, (y % 3 == 0 ? .28f : 0f) + (Mathf.PerlinNoise(x * .9f, y * 1.7f) > .8f ? .08f : 0f)));
            px.Apply(); File.WriteAllBytes(png, px.EncodeToPNG()); Object.DestroyImmediate(px);
            AssetDatabase.ImportAsset(png, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(png);
            importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.SaveAndReimport();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(png);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha); m.SetFloat("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---------- Console desk, chair and desk things ----------
        static string SecurityConsole(Transform room)
        {
            var g = Group(room, "Console", room.position);
            var desks = new List<Transform>();
            foreach (float z in new[] { 104.9f, 106.9f })
                desks.Add(PutPrefab(g, Apo + "Props/SM_Prop_Desk_02", new Vector3(-5.97f, SecFloor, z), -90));
            float top = desks.Max(d => VisibleBounds(d).max.y);
            var chair = PutPrefab(g, Apo + "Props/SM_Prop_Study_Chair_01", new Vector3(-7.0f, SecFloor, 105.9f), -90);
            // Desk things, front edge towards the chair.
            Material plastic = Lit("LA_DeskPlastic", new Color(.16f, .16f, .17f), .35f), keycap = Lit("LA_Keycap", new Color(.62f, .6f, .55f), .2f);
            var kb = Group(g, "Keyboard", new Vector3(-6.05f, top, 105.9f)); kb.rotation = Quaternion.LookRotation(Vector3.left);
            LBox(kb, "Base", new Vector3(0, .012f, 0), new Vector3(.46f, .024f, .16f), plastic);
            for (int row = 0; row < 4; row++) LBox(kb, "Keys", new Vector3(0, .028f, -.055f + row * .037f), new Vector3(.42f, .01f, .028f), keycap);
            var joy = Group(g, "PtzControl", new Vector3(-6.0f, top, 104.95f));
            LBox(joy, "Base", new Vector3(0, .03f, 0), new Vector3(.22f, .06f, .3f), plastic);
            LRod(joy, "Stick", new Vector3(0, .06f, .05f), new Vector3(0, .17f, .05f), .012f, Mat("V4_Black"));
            LShape(joy, PrimitiveType.Sphere, "Knob", new Vector3(0, .18f, .05f), Vector3.one * .035f, Mat("V4_Black"));
            for (int b = 0; b < 6; b++) LBox(joy, "Button", new Vector3(-.07f + (b % 3) * .07f, .062f, -.09f + (b / 3) * .05f), new Vector3(.03f, .008f, .03f), b == 0 ? Bulb() : keycap);
            PutPrefab(g, Apo + "Props/SM_Prop_Radio_01", new Vector3(-5.78f, top, 104.15f), -90, 0, .9f);
            PutPrefab(g, Gen + "SM_Gen_Prop_Mug_01", new Vector3(-6.2f, top, 106.75f), 30);
            PutPrefab(g, Apo + "Props/SM_Prop_Flashlight_01", new Vector3(-6.15f, top, 107.35f), 70);
            var log = Group(g, "Logbook", new Vector3(-6.0f, top, 106.55f)); log.rotation = Quaternion.Euler(0, -80, 0);
            LBox(log, "Cover", new Vector3(0, .012f, 0), new Vector3(.32f, .024f, .24f), Lit("LA_LogbookCover", new Color(.16f, .2f, .14f), .2f));
            LBox(log, "Pages", new Vector3(0, .025f, .005f), new Vector3(.3f, .004f, .22f), Mat("LA_Paper"));
            LRod(log, "Pen", new Vector3(-.05f, .03f, .06f), new Vector3(.1f, .03f, .1f), .006f, Mat("V4_Black"));
            // Desk lamp on the north end, bent towards the keyboard.
            var lamp = Group(g, "DeskLamp", new Vector3(-5.8f, top, 107.6f));
            Material lampMetal = Lit("LA_LampEnamel", new Color(.2f, .27f, .22f), .45f);
            LRod(lamp, "Foot", new Vector3(0, 0, 0), new Vector3(0, .02f, 0), .08f, lampMetal);
            LRod(lamp, "ArmLow", new Vector3(0, .02f, 0), new Vector3(-.08f, .32f, 0), .01f, lampMetal);
            LRod(lamp, "ArmHigh", new Vector3(-.08f, .32f, 0), new Vector3(-.3f, .38f, -.05f), .01f, lampMetal);
            var shade = LShape(lamp, PrimitiveType.Cylinder, "Shade", new Vector3(-.33f, .34f, -.06f), new Vector3(.14f, .06f, .14f), lampMetal, new Vector3(-25, 0, 40));
            var bulb = LShape(lamp, PrimitiveType.Sphere, "Bulb", new Vector3(-.35f, .3f, -.06f), Vector3.one * .05f, Bulb());
            var spot = new GameObject("LampLight"); Undo.RegisterCreatedObjectUndo(spot, "Desk lamp"); spot.transform.SetParent(lamp, false);
            spot.transform.localPosition = new Vector3(-.35f, .28f, -.06f); spot.transform.rotation = Quaternion.LookRotation(new Vector3(-.4f, -1, -.4f));
            var l = spot.AddComponent<Light>(); l.type = LightType.Spot; l.spotAngle = 85; l.range = 2.4f; l.intensity = 1.6f; l.color = new Color(1f, .82f, .6f); l.shadows = LightShadows.Soft;
            return $"console of 2 desks (top {top - SecFloor:F2} m), chair facing the monitors";
        }

        // ---------- Weapon rack on the west wall, north of the doorway to the changing room ----------
        static string WeaponRack(Transform room)
        {
            var g = Group(room, "WeaponRack", new Vector3(SecX0, SecFloor, 107.45f)); g.rotation = Quaternion.LookRotation(Vector3.right); // +z faces into the room
            Material olive = Lit("LA_ArmoryOlive", new Color(.22f, .25f, .19f), .38f, null, null, .3f), steel = Mat("V4_Iron_Dark"), felt = Lit("LA_RackFelt", new Color(.23f, .07f, .06f), .05f);
            const float W = 1.9f, D = .45f, baseH = .8f, backH = 1.42f; // the back stops under the wall's dark trim band
            // Lower cabinet with two doors, then the open rack above.
            LBox(g, "Cabinet", new Vector3(0, baseH * .5f, D * .5f), new Vector3(W, baseH, D), olive, true);
            foreach (float x in new[] { -W * .25f, W * .25f })
            {
                LBox(g, "Handle", new Vector3(x + (x < 0 ? .3f : -.3f), baseH * .6f, D + .02f), new Vector3(.02f, .14f, .025f), steel);
                for (int v = 0; v < 4; v++) LBox(g, "Vent", new Vector3(x, .15f + v * .035f, D + .002f), new Vector3(.3f, .012f, .004f), steel);
            }
            LBox(g, "DoorSeam", new Vector3(0, baseH * .5f, D + .002f), new Vector3(.006f, baseH - .08f, .004f), steel);
            LBox(g, "Back", new Vector3(0, baseH + backH * .5f, .02f), new Vector3(W, backH, .04f), olive);
            LBox(g, "ButtRest", new Vector3(0, baseH + .04f, .22f), new Vector3(W - .1f, .08f, .3f), felt);
            LBox(g, "TopClampBar", new Vector3(0, baseH + 1.1f, .2f), new Vector3(1.1f, .07f, .06f), steel);
            for (int k = 0; k < 4; k++) LBox(g, "Notch", new Vector3(-.45f + k * .3f, baseH + 1.1f, .235f), new Vector3(.03f, .09f, .02f), felt);
            // Long guns standing butt-down, muzzles in the clamp bar.
            string[] guns = { "Guns/SM_Wep_Shotgun_01", "Guns/SM_Wep_HuntingRifle_01", "Guns/SM_Wep_Shotgun_01", "Guns/SM_Wep_HuntingRifle_01" };
            float[] lengths = { 1.0f, 1.12f, 1.0f, 1.12f };
            for (int k = 0; k < guns.Length; k++)
            {
                var gun = PutPrefab(g, Apo + "Weapons/" + guns[k], g.TransformPoint(new Vector3(-.45f + k * .3f, baseH + .08f, .2f)), 0, lengths[k]);
                gun.rotation = g.rotation * Quaternion.Euler(-90, 90, 0); var b = VisibleBounds(gun);
                var slot = g.TransformPoint(new Vector3(-.45f + k * .3f, 0, .2f));
                gun.position += new Vector3(slot.x - b.center.x, baseH + SecFloor + .08f - b.min.y, slot.z - b.center.z);
            }
            // Locking chain across the guns.
            var a = new Vector3(-.6f, baseH + .7f, .3f); var c = new Vector3(.6f, baseH + .7f, .3f);
            for (int k = 0; k < 18; k++) { var p = Vector3.Lerp(a, c, k / 17f) + Vector3.down * Mathf.Sin(k / 17f * Mathf.PI) * .05f; LShape(g, PrimitiveType.Cylinder, "ChainLink", p, new Vector3(.03f, .025f, .012f), steel, new Vector3(0, 0, k % 2 == 0 ? 90 : 0)); }
            var lockG = Group(g, "Padlock", g.TransformPoint(c + new Vector3(.03f, -.06f, 0))); lockG.rotation = g.rotation;
            LBox(lockG, "Body", Vector3.zero, new Vector3(.07f, .08f, .03f), Mat("LA_Bronze"));
            LRod(lockG, "Shackle", new Vector3(-.02f, .04f, 0), new Vector3(-.02f, .09f, 0), .006f, steel);
            // Batons hang on pegs at the left end, torches and an ammo box on a small shelf at the right end.
            for (int k = 0; k < 2; k++)
            {
                float x = -.82f + k * .16f;
                var bat = PutPrefab(g, Apo + "Weapons/Melee/SM_Wep_Baton_0" + (k + 1), g.TransformPoint(new Vector3(x, baseH + .5f, .12f)), 0);
                bat.rotation = g.rotation * Quaternion.Euler(90, 90, 0); var b = VisibleBounds(bat);
                bat.position += g.TransformPoint(new Vector3(x, baseH + .85f, .16f)) - b.center;
                LRod(g, "Peg", new Vector3(x, baseH + 1.25f, .04f), new Vector3(x, baseH + 1.25f, .14f), .008f, steel);
            }
            LBox(g, "SideShelf", new Vector3(.74f, baseH + .75f, .16f), new Vector3(.34f, .025f, .26f), steel);
            for (int k = 0; k < 2; k++)
            {
                var torch = PutPrefab(g, Apo + "Props/SM_Prop_Flashlight_0" + (k + 2), g.TransformPoint(new Vector3(.66f + k * .16f, baseH + .763f, .16f)), 0, .3f);
                torch.rotation = g.rotation * Quaternion.Euler(-90, 0, 0); var b = VisibleBounds(torch);
                var spot = g.TransformPoint(new Vector3(.66f + k * .16f, 0, .16f));
                torch.position += new Vector3(spot.x - b.center.x, SecFloor + baseH + .763f - b.min.y, spot.z - b.center.z);
            }
            var ammo = PutPrefab(g, Apo + "Props/SM_Prop_Ammo_Box_01", g.TransformPoint(new Vector3(.72f, baseH + .08f, .2f)), -90, 0, .6f);
            return "weapon rack: 2 shotguns, 2 rifles under a padlocked chain, 2 batons, 2 torches, ammo box";
        }

        // ---------- Shelving and filing cabinets instead of greybox boxes ----------
        static string OfficeStorage(Transform room)
        {
            var g = Group(room, "Storage", room.position);
            // Steel shelving on the west wall south of the doorway.
            var shelf = PutPrefab(g, Gen + "SM_Gen_Prop_Shelf_03", new Vector3(SecX0 + .3f, SecFloor, 103.05f), 90);
            int stocked = StockShelf(g, shelf);
            // Two four-drawer filing cabinets where the greybox ones stood, against the south wall.
            Material body = Lit("LA_FilingSteel", new Color(.42f, .45f, .4f), .35f, null, null, .4f), steel = Mat("V4_Iron_Dark");
            for (int k = 0; k < 2; k++)
            {
                var cab = Group(g, "FilingCabinet", new Vector3(-9.9f + k * .62f, SecFloor, SecZ0)); cab.rotation = Quaternion.identity; // drawers face north
                const float w = .5f, d = .62f, h = 1.32f;
                LBox(cab, "Body", new Vector3(0, h * .5f, d * .5f), new Vector3(w, h, d), body, true);
                for (int dr = 0; dr < 4; dr++)
                {
                    float y = .08f + dr * .31f + .15f; bool open = k == 1 && dr == 2;
                    var drawer = LBox(cab, "Drawer", new Vector3(0, y, d + (open ? .18f : .005f)), new Vector3(w - .04f, .28f, open ? .38f : .012f), body);
                    LBox(cab, "Pull", new Vector3(0, y + .05f, d + (open ? .38f : .02f)), new Vector3(.14f, .025f, .025f), steel);
                    LBox(cab, "LabelSlot", new Vector3(0, y + .1f, d + (open ? .372f : .013f)), new Vector3(.1f, .04f, .004f), Mat("LA_Paper"));
                    if (open) for (int f = 0; f < 7; f++) LBox(cab, "Folder", new Vector3(0, y + .1f, d + .02f + f * .045f), new Vector3(w - .1f, .24f, .012f), f % 2 == 0 ? Lit("LA_FolderManila", new Color(.78f, .66f, .42f), .1f) : Mat("LA_Paper"));
                }
            }
            // The flat greybox gauge panel on the north wall gives way to a work shelf and a tall steel cabinet.
            int panel = 0;
            foreach (Transform t in room.parent.Find("Office")) if (t.name is "ControlWall" or "Gauge") { Undo.RecordObject(t.gameObject, "Clear panel"); t.gameObject.SetActive(false); panel++; }
            var work = PutPrefab(g, Apo + "Props/SM_Prop_WorkShelf_01", new Vector3(-9.05f, SecFloor, SecZ1 - .35f), 180);
            stocked += StockShelf(g, work);
            var tall = Group(g, "SteelCabinet", new Vector3(-7.5f, SecFloor, SecZ1)); tall.rotation = Quaternion.LookRotation(Vector3.back);
            LBox(tall, "Body", new Vector3(0, .95f, .25f), new Vector3(.95f, 1.9f, .5f), body, true);
            LBox(tall, "Seam", new Vector3(0, .95f, .502f), new Vector3(.006f, 1.8f, .004f), steel);
            foreach (float x in new[] { -.06f, .06f }) LBox(tall, "Handle", new Vector3(x, 1.0f, .52f), new Vector3(.02f, .2f, .025f), steel);
            for (int v = 0; v < 5; v++) foreach (float x in new[] { -.24f, .24f }) LBox(tall, "Vent", new Vector3(x, 1.6f + v * .04f, .502f), new Vector3(.22f, .012f, .004f), steel);
            LBox(tall, "KeyLock", new Vector3(.06f, 1.18f, .515f), new Vector3(.03f, .03f, .01f), Mat("LA_Bronze"));
            PutPrefab(g, Apo + "Props/SM_Prop_CardboardBox_01", new Vector3(-7.45f, SecFloor + 1.9f, SecZ1 - .28f), 8, .42f);
            // Wall clock above the shelving, stopped.
            var clock = PutPrefab(g, Gen + "SM_Gen_Prop_Clock_01", new Vector3(-8.1f, SecFloor + 2.75f, SecZ1 - .05f), 180, 0, 1.2f);
            return $"2 shelves ({stocked} items), 2 filing cabinets, steel cabinet, wall clock (removed {panel} gauge-panel pieces)";
        }

        // Puts boxes, binders and spares on each board of a shelf prefab (boards found by probing a temporary collider).
        static int StockShelf(Transform g, Transform shelf)
        {
            var temp = shelf.GetComponentsInChildren<MeshFilter>().Select(f => f.gameObject.AddComponent<MeshCollider>()).ToList();
            Physics.SyncTransforms();
            var b = VisibleBounds(shelf); var across = shelf.right; var inward = shelf.forward;
            var levels = new List<float>();
            for (float y = b.max.y + .05f; ;)
            {
                var ray = new Ray(new Vector3(b.center.x, y, b.center.z), Vector3.down); float best = float.MaxValue;
                foreach (var c in temp) if (c.Raycast(ray, out var h, y - b.min.y) && h.distance < best) best = h.distance;
                if (best == float.MaxValue || y - best < b.min.y + .1f) break;
                levels.Add(y - best); y -= best + .03f;
            }
            foreach (var c in temp) Object.DestroyImmediate(c);
            string[] items = { Apo + "Props/SM_Prop_CardboardBox_01", Apo + "Props/SM_Prop_Ammo_Box_01", Apo + "Props/SM_Prop_Radio_01", Apo + "Props/SM_Prop_Flashlight_04", Apo + "Props/SM_Prop_CardboardBox_01" };
            var rng = new System.Random(14); int n = 0;
            float width = Mathf.Abs(Vector3.Dot(b.size, across)) - .2f;
            foreach (var y in levels)
            {
                float room = levels.Where(o => o > y).DefaultIfEmpty(b.max.y).Min() - y - .05f; if (room < .15f) continue;
                for (float s = -width * .5f + .2f; s < width * .5f - .15f; s += .45f)
                {
                    if (rng.NextDouble() < .25) continue;
                    var at = b.center + across * s; at.y = y;
                    var item = PutPrefab(g, items[rng.Next(items.Length)], at, shelf.eulerAngles.y + (float)rng.NextDouble() * 20 - 10, Mathf.Min(.38f, room * .9f));
                    if (VisibleBounds(item).max.y > y + room) { Object.DestroyImmediate(item.gameObject); continue; }
                    n++;
                }
            }
            return n;
        }
    }
}
