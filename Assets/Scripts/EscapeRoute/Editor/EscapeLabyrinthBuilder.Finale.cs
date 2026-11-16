using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        static void BuildLabyrinthFinale(EscapePursuerRoute route)
        {
            var background = labyrinthFrame.Find("EscapeBackground");
            var exit = background.Find("40_AExitCableSaw");
            var arrival = labyrinthFrame.Find("FacilityContent/FinalArrival");
            if (!exit || !arrival || !exit.GetComponent<EscapeExitSaw>() || route.Points.Count < 74)
                throw new InvalidOperationException("The authored saw workshop, final arrival and final pursuit anchors are required.");
            if (labyrinthRoot.Find("Labyrinth 09 final manufacturing hall"))
                throw new InvalidOperationException("The final manufacturing hall has already been built.");
            LAnchor("Labyrinth 09 final manufacturing hall", new Vector3(350, 0, 78));

            // The reviewed fixed floors are X322..366 and X366..400, both Z71..85 at Y0.
            // Clear only static structural fragments above them. Every saw-system hierarchy,
            // the 28m door, its footings and the separate cinematic at Z578 are excluded.
            LFinaleClearStructure(new Bounds(new Vector3(366, 3.6f, 78), new Vector3(58, 8.8f, 32)), exit);
            LFinaleFloor("South production approach", 338, 366, 65, 71);
            LFinaleFloor("North production approach", 338, 366, 85, 91);
            LFinaleFloor("South cable workshop aisle", 366, 396, 61, 71);
            LFinaleFloor("North cable workshop aisle", 366, 396, 85, 95);
            FitLabyrinthFinalePursuit(route);

            LGuard("Labyrinth finale south approach edge", new Vector3(338, 0, 65), new Vector3(365.8f, 0, 65));
            LGuard("Labyrinth finale north approach edge", new Vector3(338, 0, 91), new Vector3(365.8f, 0, 91));
            LGuard("Labyrinth finale south west return", new Vector3(338, 0, 65), new Vector3(338, 0, 70.8f));
            LGuard("Labyrinth finale north west return", new Vector3(338, 0, 85.2f), new Vector3(338, 0, 91));
            LGuard("Labyrinth workshop south outer edge", new Vector3(366, 0, 61), new Vector3(395.4f, 0, 61));
            LGuard("Labyrinth workshop north outer edge", new Vector3(366, 0, 95), new Vector3(395.4f, 0, 95));
            // Both work aisles turn inward before the existing gate jambs at X396..400.
            // No new floor or alternate passage crosses the locked-door plane at X400.
            LBox("Labyrinth workshop south terminal return", new Vector3(395.5f, 3, 63), new Vector3(1, 6, 4), bConcrete);
            LBox("Labyrinth workshop north terminal return", new Vector3(395.5f, 3, 93), new Vector3(1, 6, 4), bConcrete);

            BuildLabyrinthFinaleMachine();
            foreach (float z in new[] { 68f, 88f })
            {
                string side = z < 78 ? "south" : "north";
                LGuide("09 " + side + " split", new Vector3(339, 0, 78), new Vector3(339, 0, z), 71.05f, 71.12f);
                LGuide("09 " + side + " approach", new Vector3(339, 0, z), new Vector3(363, 0, z), 71.12f, 71.8f);
                LGuide("09 " + side + " shared saw pickup", new Vector3(363, 0, z), new Vector3(368, 0, 78), 71.8f, 72);
                float aisle = z < 78 ? 64 : 92;
                LGuide("09 " + side + " workshop entry", new Vector3(368, 0, 78), new Vector3(372, 0, aisle), 72, 72.2f);
                LGuide("09 " + side + " cable work aisle", new Vector3(372, 0, aisle), new Vector3(389, 0, aisle), 72.2f, 72.65f);
                LGuide("09 " + side + " gate reunion", new Vector3(389, 0, aisle), new Vector3(392, 0, 78), 72.65f, 72.85f);
            }
            LGuide("09 shared locked exit approach", new Vector3(392, 0, 78), new Vector3(398, 0, 78), 72.85f, 73);
            LLamp("Labyrinth south approach worklight", new Vector3(342, 5, 65.2f), new Vector3(347, 0, 68), 260);
            LLamp("Labyrinth north approach worklight", new Vector3(359, 5, 90.8f), new Vector3(356, 0, 88), 260);
            LLamp("Labyrinth south bank worklight", new Vector3(371, 6, 61.2f), new Vector3(380, 0, 65), 320);
            LLamp("Labyrinth north bank worklight", new Vector3(390, 6, 94.8f), new Vector3(383, 0, 92), 320);
            BuildLabyrinthFinaleReveal();
        }

        static void LFinaleFloor(string name, float x1, float x2, float z1, float z2)
            => LFloor("Labyrinth finale " + name, new Vector3((x1 + x2) * .5f, 0, (z1 + z2) * .5f), x2 - x1, z2 - z1);

        internal static void FitLabyrinthFinalePursuit(EscapePursuerRoute route)
        {
            var boss = route.GetComponentInChildren<EscapePursuerBoss>(true);
            if (!boss) throw new InvalidOperationException("The original finale pursuer is required.");
            var data = new SerializedObject(boss);
            var arena = data.FindProperty("arena").objectReferenceValue as BoxCollider;
            if (!arena) throw new InvalidOperationException("The original finale pursuit area is required.");
            // Keep the shared boss behavior and longitudinal fire/door boundary. The new
            // work aisles must remain inside target selection and supported navigation.
            var size = arena.size;
            size.z = 34f / Mathf.Abs(arena.transform.lossyScale.z);
            arena.size = size;
            EditorUtility.SetDirty(arena);
        }

        static void BuildLabyrinthFinaleMachine()
        {
            // The low human floor and live late-saw sweep remain open under a machine too large
            // for the original hall. Side aisles give distinct sightlines around its supports.
            foreach (float x in new[] { 343f, 358f })
                foreach (float z in new[] { 72.8f, 83.2f })
                {
                    // The pier meets the floor directly. An oversized knee-high base
                    // would offer reachable corner perches with no traversal purpose.
                    LBox("Labyrinth human concrete conversion pier", new Vector3(x, 4.8f, z), new Vector3(1.2f, 9.6f, 1.2f), bConcrete);
                    LBox("Labyrinth bolted machine pier collar", new Vector3(x, 8.2f, z), new Vector3(1.6f, 1.25f, 1.6f), wornSteel, false);
                    foreach (float dx in new[] { -.48f, .48f })
                        LBox("Labyrinth coarse retrofit anchor", new Vector3(x + dx, 8.2f, z - .83f), new Vector3(.18f, .7f, .15f), maintenancePaint, false);
                }
            foreach (float x in new[] { 343f, 358f })
                LBox("Labyrinth overhead load crossmember", new Vector3(x, 9.3f, 78), new Vector3(1.4f, .6f, 12), wornSteel, false);
            LBox("Labyrinth oversized suspended process casing", new Vector3(350.5f, 9.2f, 78), new Vector3(18, 3.4f, 8.2f), wornSteel, false);
            LBox("Labyrinth suspended process underside", new Vector3(350.5f, 7.58f, 78), new Vector3(16.4f, .16f, 7), bRecess, false);
            for (int i = 0; i < 7; i++)
            {
                float x = 343.5f + i * 2.3f;
                LBox("Labyrinth unequally fitted casing rib", new Vector3(x, 9.325f, 78), new Vector3(.18f, 3.65f, 8.6f), bSteel, false);
                LBox("Labyrinth obscure process inspection slit", new Vector3(x, 9.15f, 73.86f), new Vector3(1.2f, .18f, .06f), bRecess, false);
            }
            LFinaleSharedMesh("Labyrinth original industrial metering head", "Assets/Scenes/Levels/EscapeFacilityMotion/shaft-metering-housing.asset",
                new Vector3(351, 12.4f, 78), new Vector3(8, 3.3f, 6));
            LBox("Labyrinth upper metering head cradle", new Vector3(351, 10.95f, 78), new Vector3(8.4f, .4f, 6.4f), bSteel, false);
            foreach (float x in new[] { 347f, 355f })
                LBox("Labyrinth oversized pressure riser", new Vector3(x, 13.5f, 81), new Vector3(.65f, 5.2f, .65f), wornSteel, false);
            LBox("Labyrinth coupled pressure return", new Vector3(351, 16, 81), new Vector3(8.6f, .65f, .65f), maintenancePaint, false);
        }

        static void LFinaleSharedMesh(string name, string path, Vector3 center, Vector3 size)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!mesh) throw new InvalidOperationException("Required existing industrial mesh missing: " + path);
            var root = LAnchor(name, center);
            var visual = new GameObject("Read-only existing mesh").transform; visual.SetParent(root, false);
            var bounds = mesh.bounds;
            var scale = new Vector3(size.x / Mathf.Max(.01f, bounds.size.x), size.y / Mathf.Max(.01f, bounds.size.y), size.z / Mathf.Max(.01f, bounds.size.z));
            visual.localScale = scale; visual.localPosition = -Vector3.Scale(bounds.center, scale);
            visual.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            visual.gameObject.AddComponent<MeshRenderer>().sharedMaterials = Enumerable.Repeat(wornSteel, mesh.subMeshCount).ToArray();
        }

        static void BuildLabyrinthFinaleReveal()
        {
            // The retained door is 28m tall and each leaf sweeps almost 12m beyond X400.
            // This exterior frame starts at X415.2, outside that sweep and the arrival box.
            // Reuse the meadow floor; there is no new collision surface beyond the locked gate.
            foreach (float z in new[] { 62f, 94f })
            {
                LBox("Labyrinth exterior reclaimed-concrete pier", new Vector3(416, 9, z), new Vector3(1.6f, 18, 1.6f), bConcrete);
                LBox("Labyrinth exterior bolted duct shoe", new Vector3(416, 17.5f, z), new Vector3(2, 1.1f, 2), bSteel, false);
            }
            LBox("Labyrinth last industrial lintel above meadow", new Vector3(416, 18.5f, 78), new Vector3(1.8f, 1, 34), bConcrete);
            LBox("Labyrinth last pressure return to factory", new Vector3(416, 19.3f, 78), new Vector3(.75f, .65f, 34), wornSteel, false);
        }

        static void LFinaleClearStructure(Bounds opening, Transform protectedExit)
        {
            Physics.SyncTransforms();
            foreach (var box in labyrinthFrame.GetComponentsInChildren<BoxCollider>().Where(c => !c.isTrigger && c.GetComponent<MeshRenderer>()
                && !c.transform.IsChildOf(protectedExit) && !c.GetComponentInParent<EscapeExitSaw>()
                && (c.name == "타설 콘크리트 벽" || c.name.StartsWith("Concrete remainder", StringComparison.Ordinal)
                    || c.name == "남겨진 짧은 콘크리트 흉벽")).ToArray())
            {
                var bounds = new Bounds(labyrinthFrame.InverseTransformPoint(box.bounds.center), box.bounds.size);
                if (!bounds.Intersects(opening)) continue;
                var lo = Vector3.Max(bounds.min, opening.min); var hi = Vector3.Min(bounds.max, opening.max);
                if ((hi - lo).x < .001f || (hi - lo).y < .001f || (hi - lo).z < .001f) continue;
                var material = box.GetComponent<MeshRenderer>().sharedMaterial;
                void Piece(Vector3 min, Vector3 max)
                {
                    var size = max - min;
                    if (size.x > .01f && size.y > .01f && size.z > .01f)
                        LBox("Labyrinth finale fitted structural return", (min + max) * .5f, size, material);
                }
                Piece(bounds.min, new Vector3(lo.x, bounds.max.y, bounds.max.z));
                Piece(new Vector3(hi.x, bounds.min.y, bounds.min.z), bounds.max);
                Piece(new Vector3(lo.x, bounds.min.y, bounds.min.z), new Vector3(hi.x, lo.y, bounds.max.z));
                Piece(new Vector3(lo.x, hi.y, bounds.min.z), new Vector3(hi.x, bounds.max.y, bounds.max.z));
                Piece(new Vector3(lo.x, lo.y, bounds.min.z), new Vector3(hi.x, hi.y, lo.z));
                Piece(new Vector3(lo.x, lo.y, hi.z), new Vector3(hi.x, hi.y, bounds.max.z));
                Object.DestroyImmediate(box.gameObject);
            }
        }
    }
}
