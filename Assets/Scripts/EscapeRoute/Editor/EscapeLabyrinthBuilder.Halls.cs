using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        static void BuildLabyrinthHalls(EscapePursuerRoute route)
        {
            if (!route || !labyrinthFrame || !labyrinthRoot)
                throw new InvalidOperationException("Initialize the copied labyrinth before rebuilding its halls.");
            if (labyrinthRoot.Find("Labyrinth 01 intake observation hall"))
                throw new InvalidOperationException("The hall architecture has already been built.");
            BuildLabyrinthIntakeWindow(route);
            BuildLabyrinthConveyorNave();
            BuildLabyrinthReturnQuay();
            BuildLabyrinthConversionRoof();
            BuildLabyrinthThermalManifold();
        }

        static void BuildLabyrinthIntakeWindow(EscapePursuerRoute route)
        {
            LAnchor("Labyrinth 01 intake observation hall", new Vector3(7.2f, 0, 41.5f));
            // Existing infill: X7.18, Y2.65, Z40.994, size .58 x 8.818 x 11.918.
            // The separate transparent safety panels at X9.91 are deliberately untouched.
            // Nothing enters the original pursuer emergence sweep X15/Z-10..13.
            LCutNamedBox("옛 점검창 매립 콘크리트", new Bounds(new Vector3(7.2f, 3.65f, 41.5f), new Vector3(.9f, 5.1f, 10)));
            foreach (float z in new[] { 36.35f, 46.65f })
                LBox("Labyrinth intake reveal vertical steel return", new Vector3(7.4f, 3.65f, z), new Vector3(.35f, 5.4f, .3f), wornSteel);
            foreach (float y in new[] { 1f, 6.3f })
                LBox("Labyrinth intake reveal horizontal steel return", new Vector3(7.4f, y, 41.5f), new Vector3(.35f, .3f, 10.6f), wornSteel);
            foreach (float z in new[] { 37f, 40f, 43f, 46f })
                LBox("Labyrinth intake window retrofit clamps", new Vector3(7.62f, 6.3f, z), new Vector3(.16f, .55f, .28f), maintenancePaint, false);
            // Preserve the effluent behind the old glazed window. The occupied intake is
            // visible in the opposite wall pocket, between the retained hall columns.
            var pocket=new Bounds(new Vector3(27,4,33),new Vector3(9.6f,8,15.4f));
            LOpen(route,pocket.center,pocket.size);
            LCutNamedBox("타설 콘크리트 벽",pocket);
        }

        static void BuildLabyrinthConveyorNave()
        {
            LAnchor("Labyrinth 03 lower conversion nave", new Vector3(7, -20, -47));
            // Keep the complete elevation west of X12.1: the existing service path is X16,
            // and the pursuer exits X23.7/Z-50 before leaping to X38/Z-67. Allow its full
            // 1.4m radius and 4.9m height, not only its centerline. No floor is added.
            // The existing continuous deep foundation has top Y-152 in frame coordinates.
            foreach (float z in new[] { -27f, -67f })
                LHallPier("Labyrinth transfer nave deep pier", 5, z, -2, 2.4f, 2.2f);
            LBox("Labyrinth transfer nave deep concrete backing", new Vector3(3.9f, -23, -47), new Vector3(2, 44, 42), bConcrete);
            LBox("Labyrinth transfer nave upper entablature", new Vector3(5, -2, -47), new Vector3(3, 2, 43), bConcrete);
            foreach (float z in new[] { -34f, -47f, -60f })
            {
                LBox("Labyrinth conversion machine lower wall bracket", new Vector3(7.7f, -22.3f, z), new Vector3(8, .6f, 1.4f), wornSteel, false);
                LBox("Labyrinth conversion machine upper wall bracket", new Vector3(7.7f, -9.7f, z), new Vector3(8, .6f, 1.4f), wornSteel, false);
                LHallMesh("Labyrinth alien scale borrowed metering casing", "shaft-metering-housing.asset",
                    new Vector3(8.3f, -16, z), new Vector3(6, 12, 8), wornSteel);
                LBox("Labyrinth narrow blinded inspection cover", new Vector3(11.35f, -15, z), new Vector3(.1f, 4.4f, 5.5f), bRecess, false);
                foreach (float dz in new[] { -2.3f, 2.3f })
                    LBox("Labyrinth crude inspection cover clamps", new Vector3(11.44f, -15, z + dz), new Vector3(.12f, 4.7f, .16f), maintenancePaint, false);
                LHallPipe("Labyrinth nave coupled pressure feed", new Vector3(10, -10, z), new Vector3(10, -8.7f, z), .24f, wornSteel);
                LHallPipe("Labyrinth nave pressure elbow", new Vector3(10, -8.7f, z), new Vector3(11.7f, -8.7f, z), .24f, wornSteel);
                LBox("Labyrinth nave pressure wall saddle", new Vector3(7.9f, -8.7f, z), new Vector3(7.6f, .2f, .35f), bSteel, false);
            }
            LHallPipe("Labyrinth continuous conversion pressure main", new Vector3(11.7f, -8.7f, -27), new Vector3(11.7f, -8.7f, -67), .38f, wornSteel);
            // The three large machines read as one wall of the transfer hall, not independent
            // obstacles on the moving-pole line. Fixed wall lights reveal their scale from it.
            LHallMountedLamp("Labyrinth transfer nave wall lamp", new Vector3(5, -5.8f, -47), new Vector3(31, -18, -43), 420);
        }

        static void BuildLabyrinthReturnQuay()
        {
            LAnchor("Labyrinth 04 return sorting hall", new Vector3(48, -18.5f, -74));
            // Preserve unloading X32.5..44, switch wire X44..54/Z-74, bridge X54..76,
            // return cannon X80/Z-74, and the existing body procession at Z-81.8.
            // A rear portal frames those devices; it adds neither a parallel floor nor a net.
            // The right pier ends at X64.7, west of the full encounter-3 capsule sweeping
            // from (70,-18.5,-82.3) to (82,-18.5,-71.5), including its rear staging path.
            foreach (float x in new[] { 32f, 64f })
                LHallPier("Labyrinth return quay deep portal pier", x, -81.2f, 1.5f, 1.4f, 1.4f);
            LBox("Labyrinth return processing portal crown", new Vector3(48, 1, -81.2f), new Vector3(34, 1.5f, 2), bConcrete);
            LBox("Labyrinth return overhead sorting duct", new Vector3(48, 2.7f, -81.2f), new Vector3(30, 2, 3), wornSteel, false);
            for (int i = 0; i < 5; i++)
                LBox("Labyrinth return duct external compression collar", new Vector3(34.5f + i * 6.75f, 2.7f, -81.2f),
                    new Vector3(.24f, 2.25f, 3.25f), maintenancePaint, false);
            LHallPipe("Labyrinth sorting main vertical elbow", new Vector3(11.7f, -8.7f, -67), new Vector3(11.7f, 2.7f, -67), .38f, wornSteel);
            LHallPipe("Labyrinth conversion to sorting pressure connection", new Vector3(11.7f, 2.7f, -67), new Vector3(11.7f, 2.7f, -81.2f), .38f, wornSteel);
            LHallPipe("Labyrinth high rear sorting feed", new Vector3(11.7f, 2.7f, -81.2f), new Vector3(33, 2.7f, -81.2f), .38f, wornSteel);
            LBox("Labyrinth sorting pressure elbow wall saddle", new Vector3(8.3f, -2, -67), new Vector3(7, .45f, .8f), bSteel, false);
            LHallMountedLamp("Labyrinth return quay crown task light", new Vector3(52, .15f, -80.4f), new Vector3(49, -18.5f, -74), 450);
        }

        static void BuildLabyrinthConversionRoof()
        {
            LAnchor("Labyrinth 05 conversion hall upper structure", new Vector3(103, 12, 40.7f));
            // Recess the display west of the landing jump and south of the hidden service
            // gallery at Z51. This also stays clear of the timed wires at Z22..28.
            var pocket=new Bounds(new Vector3(85.5f,-1,37),new Vector3(15.2f,8,6.2f));
            LCutNamedBox("Concrete remainder",pocket);
            LCutNamedBox("타설 콘크리트 벽",pocket);
            // Verified upper masses: west X76.5/Z40 begins Y18; east X116/Z40 begins Y10.
            // Span between those masses rather than planting new supports in the rescue islands.
            LBox("Labyrinth conversion hall upper concrete span", new Vector3(95.5f, 20, 40.7f), new Vector3(38, 2, 2), bConcrete);
            foreach (float x in new[] { 77.3f, 113.8f })
            {
                LBox("Labyrinth conversion roof fixed wall shoe", new Vector3(x, 20, 40.7f), new Vector3(1.2f, 3, 3), wornSteel, false);
                foreach (float z in new[] { 39.55f, 41.85f })
                    LBox("Labyrinth conversion roof anchor band", new Vector3(x, 20, z), new Vector3(1.4f, 2.5f, .18f), maintenancePaint, false);
            }
            foreach (float x in new[] { 83f, 104f, 110f })
                LBox("Labyrinth hanging process manifold suspension", new Vector3(x, 18.2f, 40.7f), new Vector3(.22f, 2.6f, .4f), bSteel, false);
            LHallPipe("Labyrinth conversion hall overhead pressure main", new Vector3(81, 17.2f, 40.7f), new Vector3(111, 17.2f, 40.7f), .5f, wornSteel);
            LHallMesh("Labyrinth suspended unknown conversion head", "shaft-metering-housing.asset", new Vector3(104, 12, 40.7f), new Vector3(8, 8, 3), wornSteel);
            LBox("Labyrinth conversion head fixed suspension collar", new Vector3(104, 16.6f, 40.7f), new Vector3(1.6f, 1.5f, 1.2f), maintenancePaint, false);
            LHallMountedLamp("Labyrinth conversion head inspection lamp", new Vector3(103, 8.15f, 39.1f), new Vector3(88, -4, 37), 280);
        }

        static void BuildLabyrinthThermalManifold()
        {
            LAnchor("Labyrinth 06 thermal process hall", new Vector3(116.5f, -3, 72));
            // Existing paired flames stay at X119.7/125.3, Y-7, Z58/62/70/74/82/86.
            // Their shared timing, nozzle clearance and gallery X120..125/Y-8 are untouched.
            // The inspection cannon flight at X114/Y15/Z62 and service route X108/Z64
            // are outside this wall-mounted feed; gate6 at Z50 also remains unobstructed.
            LBox("Labyrinth continuous thermal distribution cabinet", new Vector3(116.5f, 1.5f, 72), new Vector3(1.6f, 6, 32), wornSteel, false);
            foreach (float z in new[] { 58f, 62f, 70f, 74f, 82f, 86f })
            {
                LBox("Labyrinth thermal cabinet wall bracket", new Vector3(115.3f, 1.5f, z), new Vector3(3, .35f, .55f), bSteel, false);
                LBox("Labyrinth thermal feed inspection plate", new Vector3(117.34f, 1.5f, z), new Vector3(.08f, 3, 1.4f), bRecess, false);
                LHallPipe("Labyrinth thermal downfeed to original manifold", new Vector3(116.3f, -1.4f, z), new Vector3(116.3f, -6.8f, z), .18f, wornSteel);
                LBox("Labyrinth thermal feed restrained coupling", new Vector3(116.3f, -3.8f, z), new Vector3(.6f, .24f, .6f), maintenancePaint, false);
            }
            foreach (float z in new[] { 56f, 88f })
            {
                // Entry pier clears the Z50..52 encounter capsules. Its lintel also clears
                // the descending cannon arc at Z56; the original core supports up to Y32.
                float top = z < 60 ? 31 : 8.2f;
                LHallPier("Labyrinth thermal hall wall-side buttress", z<60?116.8f:114.9f, z, top, z<60?1f:1.3f, 1.5f);
                LBox("Labyrinth thermal hall high cross lintel", new Vector3(121.95f, top, z), new Vector3(14.1f, 1.2f, 1.6f), bConcrete);
                LBox("Labyrinth thermal lintel on original core", new Vector3(128.5f, top - .3f, z), new Vector3(1.5f, 2.2f, 2), wornSteel, false);
            }
            LHallMountedLamp("Labyrinth continuous thermal cabinet task light", new Vector3(117.4f, 3, 72), new Vector3(122.5f, -8, 72), 280);
        }

        static void LHallPier(string name, float x, float z, float top, float width, float depth)
        {
            const float foundationTop = -152;
            LBox(name, new Vector3(x, (top + foundationTop) * .5f, z), new Vector3(width, top - foundationTop, depth), bConcrete);
        }

        static Transform LHallMesh(string name, string file, Vector3 center, Vector3 size, Material material)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Scenes/Levels/EscapeFacilityMotion/" + file);
            if (!mesh) throw new InvalidOperationException("Required existing industrial mesh missing: " + file);
            var root = LAnchor(name, center);
            var visual = new GameObject("Read-only industrial housing mesh").transform; visual.SetParent(root, false);
            var bounds = mesh.bounds;
            var scale = new Vector3(size.x / Mathf.Max(.01f, bounds.size.x), size.y / Mathf.Max(.01f, bounds.size.y), size.z / Mathf.Max(.01f, bounds.size.z));
            visual.localScale = scale; visual.localPosition = -Vector3.Scale(bounds.center, scale);
            visual.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            visual.gameObject.AddComponent<MeshRenderer>().sharedMaterials = Enumerable.Repeat(material, mesh.subMeshCount).ToArray();
            return root;
        }

        static void LHallPipe(string name, Vector3 from, Vector3 to, float radius, Material material)
        {
            var pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder); pipe.name = name;
            Object.DestroyImmediate(pipe.GetComponent<Collider>());
            pipe.transform.SetParent(labyrinthRoot, false);
            pipe.transform.SetPositionAndRotation(LW((from + to) * .5f), Quaternion.FromToRotation(Vector3.up, LW(to) - LW(from)));
            pipe.transform.localScale = new Vector3(radius * 2, Vector3.Distance(from, to) * .5f, radius * 2);
            pipe.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        static void LHallMountedLamp(string name, Vector3 position, Vector3 target, float intensity)
        {
            // These positions touch an authored wall bracket/casing: no free-standing prop pole.
            LBox(name + " bolted fixture", position, new Vector3(.55f, .22f, .5f), bSteel, false);
            var light = LAnchor(name, position + Vector3.down * .12f).gameObject.AddComponent<Light>();
            light.transform.rotation = Quaternion.LookRotation(LW(target) - light.transform.position);
            light.type = LightType.Spot; light.color = new Color(.82f, .86f, .78f);
            light.intensity = intensity; light.range = 35; light.spotAngle = 78; light.innerSpotAngle = 52;
            light.shadows = LightShadows.None;
        }
    }
}
