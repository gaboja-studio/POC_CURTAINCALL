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
        const string Apo = "Assets/_ThirdParty/Synty/PolygonApocalypse/Prefabs/", Gen = "Assets/_ThirdParty/Synty/PolygonGeneric/Prefabs/Props/",
            Kit = "Assets/Resources/Prefabs/Objects/ETCs/";
        static readonly List<string> CircusLog = new();

        // Places a prefab so its visible bounds stand on `baseY` centred on `xz`, searching nearby for a clear spot.
        // Refuses spots that overlap existing props/walls or the room's keep-out lanes, so nothing floats or clips.
        sealed class Placer
        {
            public Transform Group; public float Floor; public Rect Room; public readonly List<Rect> KeepOut = new(); public readonly List<Bounds> Taken = new();
            // Plan offset for a room moved after its layout was written (the prop room moved east for the dome).
            public Vector2 Offset; public void Keep(Rect r) => KeepOut.Add(new Rect(r.position + Offset, r.size));
            public Transform Put(string prefab, float x, float z, float yaw, float scale = 1, float? baseY = null, bool collide = true, float search = 1.2f, float tilt = 0)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefab + ".prefab");
                if (asset == null) { CircusLog.Add("missing " + prefab); return null; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, Group);
                var t = go.transform; t.localScale = Vector3.one * scale; t.rotation = Quaternion.Euler(tilt, yaw, 0);
                if (!collide) foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                float y = baseY ?? Floor; x += Offset.x; z += Offset.y;
                for (float r = 0; r <= search; r += .3f)
                for (int k = 0; k < (r == 0 ? 1 : 8); k++)
                {
                    var off = r == 0 ? Vector2.zero : new Vector2(Mathf.Cos(k * Mathf.PI / 4), Mathf.Sin(k * Mathf.PI / 4)) * r;
                    var b = Bounds(t); t.position += new Vector3(x + off.x - b.center.x, y - b.min.y, z + off.y - b.center.z);
                    b = Bounds(t);
                    var foot = new Rect(b.min.x, b.min.z, b.size.x, b.size.z);
                    if (!Inside(foot)) continue;
                    if (baseY == null && KeepOut.Any(o => o.Overlaps(foot))) continue;
                    var test = b; test.min += new Vector3(.03f, .04f, .03f); test.max -= new Vector3(.03f, 0, .03f);
                    if (Taken.Any(o => o.Intersects(test))) continue;
                    Taken.Add(b); Undo.RegisterCreatedObjectUndo(go, "Place circus prop");
                    foreach (var r2 in go.GetComponentsInChildren<Renderer>()) if (r2.bounds.size.magnitude < .5f) r2.shadowCastingMode = ShadowCastingMode.Off;
                    return t;
                }
                CircusLog.Add($"no room for {System.IO.Path.GetFileName(prefab)} near ({x:F1},{z:F1})");
                Object.DestroyImmediate(go); return null;
            }
            // Tilted boards, hoops and poles: slide toward the wall until the top edge rests on it, so nothing leans on air.
            public Transform Lean(string prefab, float x, float z, float yaw, float scale, float tilt, Vector3 toWall)
            {
                var t = Put(prefab, x, z, yaw, scale, null, false, 1f, tilt); if (t == null) return null;
                var b = Bounds(t); var from = new Vector3(b.center.x, b.max.y - .08f, b.center.z);
                float extent = Vector3.Dot(b.extents, new Vector3(Mathf.Abs(toWall.x), 0, Mathf.Abs(toWall.z)));
                var hits = Physics.RaycastAll(from, toWall, 3f, ~0, QueryTriggerInteraction.Ignore).Where(h => !h.collider.transform.IsChildOf(t)).OrderBy(h => h.distance).ToArray();
                if (hits.Length == 0) { CircusLog.Add($"no wall to lean {t.name} on; laid flat"); t.rotation = Quaternion.Euler(90, yaw, 0); var fb = Bounds(t); t.position += Vector3.up * (Floor - fb.min.y); return t; }
                t.position += toWall * (hits[0].distance - extent - .005f);
                Taken.RemoveAt(Taken.Count - 1); Taken.Add(Bounds(t)); return t;
            }
            bool Inside(Rect f) => f.xMin >= Room.xMin && f.xMax <= Room.xMax && f.yMin >= Room.yMin && f.yMax <= Room.yMax;
            // Stack on top of a placed prop.
            public Transform On(Transform under, string prefab, float yaw, float scale = 1, float dx = 0, float dz = 0)
            {
                if (under == null) return null; var b = Bounds(under);
                return Put(prefab, b.center.x + dx - Offset.x, b.center.z + dz - Offset.y, yaw, scale, b.max.y, false, .2f); // world point: undo the plan offset
            }
        }

        static Bounds Bounds(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>().Where(r => !(r is ParticleSystemRenderer)).ToArray();
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
        }

        // Renderer.bounds is empty on inactive objects; work from the mesh instead.
        static Bounds MeshBounds(Transform t)
        {
            var m = t.GetComponent<MeshFilter>().sharedMesh.bounds;
            return GeometryUtility.CalculateBounds(new[] { m.min, m.max, new Vector3(m.min.x, m.min.y, m.max.z), new Vector3(m.min.x, m.max.y, m.min.z),
                new Vector3(m.max.x, m.min.y, m.min.z), new Vector3(m.max.x, m.max.y, m.min.z), new Vector3(m.max.x, m.min.y, m.max.z), new Vector3(m.min.x, m.max.y, m.max.z) }, t.localToWorldMatrix);
        }

        // Everything already standing in the room (props, wall and column colliders) that new props must not intrude on.
        static Placer NewPlacer(Transform target, Transform group, Rect room, float floor, params Transform[] scope)
        {
            var p = new Placer { Group = group, Floor = floor, Room = room };
            var area = new Bounds(new Vector3(room.center.x, floor + 2, room.center.y), new Vector3(room.width + 1, 4, room.height + 1));
            foreach (var c in target.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger && c.bounds.Intersects(area)))
            {
                var b = c.bounds; if (b.size.y < .3f && b.min.y < floor + .05f) continue; // floors
                if (c.name == "RackCollider") continue; // one box over a whole pallet rack; its parts are checked as renderers
                if (b.min.y > floor + 3.2f) continue; p.Taken.Add(b);
            }
            foreach (var root in scope)
            foreach (var r in root.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !(r is ParticleSystemRenderer)))
            {
                var b = r.bounds; if (!b.Intersects(area) || b.size.y < .06f || b.min.y > floor + 3.2f || b.size.magnitude > 14f) continue;
                if (r.transform.IsChildOf(group)) continue; p.Taken.Add(b);
            }
            return p;
        }

        public static string FurnishCircusSpaces()
        {
            var (scene, target) = Begin(); var original = Snapshot(source); int undo = StartUndo("Circus props for 05, 04, 11, 12");
            try
            {
                CircusLog.Clear();
                Remove(target, "CircusProps"); var g = Group(target, "CircusProps", Origin);
                SettleDraperies(target);
                JunkStorageProps(target, Group(g, "S05_JunkStorage", Origin));
                PropRoomProps(target, Group(g, "S04_PropRoom", Origin));
                StageGearProps(target, Group(g, "S11_StageGear", Origin));
                CargoBayProps(target, Group(g, "S12_CargoBay", Origin));
                Physics.SyncTransforms(); AssertUnchanged(original);
                Undo.CollapseUndoOperations(undo); EditorSceneManager.MarkSceneDirty(scene);
                System.IO.File.WriteAllLines(".harness/local/task075-env/circus-props-log.txt", CircusLog);
                return $"circus props={g.GetComponentsInChildren<Transform>().Count(t => PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject))}; skipped={CircusLog.Count}";
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        // The canvas sets copied from the source kept their wall clothesline but stood in the open, so the cloth hung in mid air.
        // Drop the free-standing line and lay the canvas over the crate it was named for (or on the floor when none is near).
        static void SettleDraperies(Transform target)
        {
            foreach (var set in target.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("CanvasOnCrate")).ToArray())
            {
                MuteFacility(set.Find("WallClothesline"));
                var cloth = set.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith("SM_MET_TangledCloth")); if (cloth == null) continue;
                var cb = Bounds(cloth);
                var crate = set.parent.GetComponentsInChildren<Renderer>().Where(r => r.name.Contains("Crate") && !r.transform.IsChildOf(set))
                    .OrderBy(r => Vector2.Distance(new Vector2(r.bounds.center.x, r.bounds.center.z), new Vector2(cb.center.x, cb.center.z))).FirstOrDefault();
                Vector3 to;
                if (crate != null && Vector2.Distance(new Vector2(crate.bounds.center.x, crate.bounds.center.z), new Vector2(cb.center.x, cb.center.z)) < 2.5f)
                {
                    var b = crate.bounds; foreach (var r in crate.transform.parent.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                    to = new Vector3(b.center.x, b.max.y - .08f, b.center.z); // sag a little over the lid
                }
                else to = new Vector3(cb.center.x, 0, cb.center.z);
                MoveFacility(cloth, cloth.position + new Vector3(to.x - cb.center.x, to.y - cb.min.y, to.z - cb.center.z));
                CircusLog.Add($"settled {AnimationUtility.CalculateTransformPath(set, target)} on {(crate != null ? crate.name : "floor")}");
            }
        }

        static void MuteNamed(Transform root, params string[] names)
        {
            if (root == null) return;
            foreach (var t in root.GetComponentsInChildren<Transform>().Where(t => names.Any(n => t.name == n || t.name.StartsWith(n + "_"))).ToArray()) MuteFacility(t);
        }

        // ---------- 05 junk storage: the previous generation's discarded acts ----------

        static void JunkStorageProps(Transform target, Transform g)
        {
            var room = target.Find("S05_JunkStorage"); if (room == null) return;
            var sheets = room.Find("UnderSheets");
            // Primitive stand-ins with a real asset now: the sheeted cannon, costume trunks and luggage bundles.
            MuteNamed(sheets, "CannonBarrel", "CannonMuzzle", "CannonCarriage", "CarriageWheel", "DustSheet", "DustSheetFold", "DustSheetHem");
            MuteNamed(room.Find("Archive"), "CostumeTrunk", "EastCrateLuggage");
            var p = NewPlacer(target, g, Rect.MinMaxRect(27.5f, 84.3f, 42.1f, 103.9f), 0, room);
            p.KeepOut.Add(Rect.MinMaxRect(27f, 87f, 31f, 91.6f)); p.KeepOut.Add(Rect.MinMaxRect(27f, 96.8f, 31f, 101.2f)); // both doors from the pipe zone
            p.KeepOut.Add(Rect.MinMaxRect(30.5f, 88.4f, 41.5f, 90.2f)); // cross aisle to the east bays

            p.Put(Kit + "PerformanceArtKit/HumanCannon", 35.4f, 85.6f, 80, .8f);
            p.Put(Apo + "Props/SM_Prop_TrashPile_01", 37.8f, 102.6f, 90, .7f, null, true, 3f);
            foreach (var (x, z, yaw) in new[] { (28.7f, 94f, 90f), (29f, 95.4f, 100f) }) p.Put(Gen + "SM_Gen_Prop_Chest_01", x, z, yaw);
            var stack = p.Put(Apo + "Props/SM_Prop_Luggage_Retro_01", 41.3f, 95f, 90); p.On(stack, Apo + "Props/SM_Prop_Luggage_Retro_01", 75);
            p.Put(Apo + "Props/SM_Prop_Luggage_Retro_01", 41.2f, 96.3f, 100);
            // Torn trampoline dumped by the east wall, a trapeze bar dropped on the floor with its rope.
            p.Put(Kit + "PerformanceArtKit/CarryTrampoline", 40.3f, 92.6f, 90, .6f, null, true, 3f);
            p.Put(Kit + "PerformanceArtKit/Trapeze", 38.3f, 103f, 0, .9f, null, true, 3f, 90);
            p.Put(Gen + "SM_Gen_Prop_Rope_02", 39.6f, 103.6f, 0, 1, null, false);
            // Broken mannequins and a heap of retired costume sacks.
            p.Put(Apo + "Props/SM_Prop_Mannequin_Torso_01", 33.8f, 94.3f, 30); p.Put(Apo + "Props/SM_Prop_Mannequin_Head_01", 34.5f, 94.9f, 120, 1, null, false);
            p.Put(Apo + "Props/SM_Prop_Mannequin_Female_03", 32.4f, 97.6f, 160);
            foreach (var (x, z) in new[] { (36.6f, 94.2f), (37.2f, 94.8f), (36.4f, 95.3f) }) p.Put(Gen + "SM_Gen_Prop_Sack_01", x, z, x * 37, 1, null, false, .6f);
            p.Put(Apo + "Props/SM_Prop_Sleeping_Bag_Rolled_01", 37.6f, 93.9f, 20, 1, null, false);
            // Retired sign boards and a cage panel leaning on the south wall; broken furniture and junk in the corners.
            p.Lean(Apo + "Props/SM_Prop_Billboard_Sign_03", 33.6f, 84.9f, 0, .55f, -12, Vector3.back);
            p.Put(Apo + "Props/SM_Prop_Cupboard_Broken_01", 28.4f, 103f, 0);
            p.Put(Apo + "Props/SM_Prop_Chair_Soft_Damaged_01", 30.1f, 103f, 200);
            p.Put(Apo + "Props/SM_Prop_JunkPile_01", 41f, 85.5f, 90, .8f);
            p.Put(Apo + "Props/SM_Prop_Tire_Pile_01", 38.4f, 84.9f, 0, .7f);
            foreach (var (x, z) in new[] { (32.2f, 91.4f), (40.2f, 91.1f), (35.8f, 100.6f) }) p.Put(Apo + "Props/SM_Prop_TrashBag_0" + (1 + (int)x % 4), x, z, x * 23, 1, null, false);
        }

        // ---------- 04 prop room: the troupe's working kit ----------

        static void PropRoomProps(Transform target, Transform g)
        {
            var room = target.Find("S04_PropRoom"); if (room == null) return;
            var off = new Vector2(room.position.x - PropRoot0.x, room.position.z - PropRoot0.z);
            var p = NewPlacer(target, g, new Rect(Rect.MinMaxRect(6.6f, 56f, 17.9f, 65.4f).position + off, new Vector2(11.3f, 9.4f)), 0, room); p.Offset = off;
            p.Keep(Rect.MinMaxRect(6.4f, 59.1f, 9.6f, 63.5f));   // inside the doorway (west wall, 04)
            p.Keep(Rect.MinMaxRect(11.6f, 62.4f, 15.8f, 65.6f)); // north opening
            p.Keep(Rect.MinMaxRect(9f, 59.2f, 16f, 61.2f));     // the walk between the shelves and the mirrors
            // Costume mannequins and a rack of balance poles by the east wall.
            foreach (var (x, z, n, yaw) in new[] { (16.6f, 63.2f, "Female_02", 250f), (17.1f, 64.4f, "Male_02", 235f), (15.4f, 64.9f, "Female_05", 200f) })
                p.Put(Apo + "Props/SM_Prop_Mannequin_" + n, x, z, yaw, 1, null, true, .8f);
            foreach (var (x, tilt) in new[] { (7.0f, -6f), (7.25f, -9f), (7.5f, -5f) })
                p.Lean(Apo + "Props/SM_Prop_Wood_Pole_01", x, 64.8f, 0, 1, -tilt, Vector3.forward);
            // Juggling kit and magic trunk on the floor between the benches.
            p.Put(Gen + "SM_Gen_Prop_Chest_01", 9.6f, 56.6f, 0);
            p.Put(Apo + "Props/SM_Prop_Crate_Open_01", 7.3f, 58.4f, 15, .8f);
            p.Put(Apo + "Props/SM_Prop_Bike_Broken_01", 11.5f, 57.1f, 0, .75f);
            var bags = p.Put(Apo + "Props/SM_Prop_Luggage_Retro_01", 7.3f, 57f, 0); p.On(bags, Apo + "Props/SM_Prop_Luggage_Retro_01", 15);
            foreach (var (x, z) in new[] { (12.6f, 58.6f), (10.2f, 63.9f) }) p.Put(Apo + "Props/SM_Prop_Toy_Bear_01", x, z, x * 40, 1.3f, null, false, .8f);
        }

        // ---------- 11 stage gear: dismantled acts stored in rows ----------

        static void StageGearProps(Transform target, Transform g)
        {
            var hall = target.Find("S11_StageGear"); if (hall == null) return;
            var zone = hall.Find("Zone11");
            // Tripod lamps were primitives; stand real floodlights where they were.
            var lampSpots = zone.Cast<Transform>().Where(t => t.name == "LampPost").Select(t => t.position).ToArray();
            MuteNamed(zone, "LampLeg", "LampPost", "StageLampBody");
            var p = NewPlacer(target, g, Rect.MinMaxRect(-21.6f, 22.4f, 1.2f, 55.2f), 0, hall, target.Find("S12_CargoBay"), target.Find("FacilityConnections"));
            // Keep the performance-object spawn rings, the lobby doorways and the long cross aisle clear.
            foreach (var c in zone.Cast<Transform>().Where(t => t.name.EndsWith("_Mark")))
            { var b = c.GetComponent<Renderer>().bounds; p.KeepOut.Add(Rect.MinMaxRect(b.min.x - 1f, b.min.z - 1f, b.max.x + 1f, b.max.z + 1f)); }
            p.KeepOut.Add(Rect.MinMaxRect(-21.6f, 37.2f, 1.2f, 39.4f));   // east-west aisle
            p.KeepOut.Add(Rect.MinMaxRect(-7.8f, 51.5f, -1.2f, 55.4f));   // lobby door 11
            p.KeepOut.Add(Rect.MinMaxRect(-17.2f, 51.5f, -12.6f, 55.4f)); // west corridor door
            foreach (var at in lampSpots) p.Put(Apo + "Props/SM_Prop_Floodlights_01", at.x, at.z, 180, .8f, null, true, 1.5f);

            // North row: cannon under a tarp, mobile stage stairs and stacked floor sections.
            p.Put(Kit + "PerformanceArtKit/HumanCannon", -18.4f, 50.8f, 90, 1, null, true, 4f);
            p.Put(Kit + "PerformanceArtKit/StairModule", -9.8f, 52.8f, 180, .9f);
            var deck = p.Put(Kit + "PerformanceArtKit/FloorModule", -20f, 46.4f, 0, .6f);
            for (int i = 0; i < 3 && deck != null; i++) deck = p.On(deck, Kit + "PerformanceArtKit/FloorModule", i * 4, .6f);
            p.On(p.Put(Gen + "SM_Gen_Prop_Plinth_01", -14.2f, 47.5f, 0, 1.2f), Gen + "SM_Gen_Prop_Plinth_01", 20, .9f); // stacked performer pedestals
            // South row: trampolines and landing cushions stacked, a folded safety net on pallets.
            var tramp = p.Put(Kit + "PerformanceArtKit/CarryTrampoline", -18.2f, 26.4f, 0, .75f);
            p.On(tramp, Kit + "PerformanceArtKit/CarryTrampoline", 12, .7f);
            var cushion = p.Put(Kit + "PerformanceArtKit/LandingCushion", -12.3f, 25.6f, 0, .55f);
            for (int i = 0; i < 2 && cushion != null; i++) cushion = p.On(cushion, Kit + "PerformanceArtKit/LandingCushion", (i + 1) * 6, .55f);
            var pallet = p.Put(Apo + "Props/SM_Prop_Pallet_01", -6.8f, 24.6f, 0);
            p.On(pallet, Kit + "PerformanceArtKit/SafetyNetModule", 90, .3f);
            // Middle islands: trapeze bars by the columns; search lights, cable drums and counterweights.
            // Trapeze bars are stored laid down (standing up, their ropes would end in mid air).
            foreach (var (x, z) in new[] { (-17.8f, 41.6f), (-17.8f, 43.6f) }) p.Put(Kit + "PerformanceArtKit/Trapeze", x, z, 90, 1, null, true, 1.2f, 90);
            foreach (var (x, z) in new[] { (-2.6f, 34.4f), (-1.4f, 35.8f) }) p.Put(Apo + "Props/SM_Prop_SearchLight_01", x, z, 200, .9f);
            foreach (var (x, z) in new[] { (-3.2f, 42.6f), (-1.6f, 43.4f) }) p.Put(Apo + "Props/SM_Prop_Spool_01", x, z, x * 30, .7f);
            foreach (var (x, z) in new[] { (-6.2f, 33.2f), (-5.6f, 33.4f), (-5.9f, 32.8f), (-6.4f, 33.8f) }) p.Put(Apo + "Props/SM_Prop_Sandbag_01", x, z, x * 50, 1, null, false, .5f);
            foreach (var (x, z, n) in new[] { (-9.2f, 23f, "Crate_Large_01"), (-8.2f, 23f, "Crate_01"), (-2.4f, 23.2f, "Crate_Large_02"), (-1.2f, 50.6f, "Crate_02") })
                p.Put(Apo + "Props/SM_Prop_" + n, x, z, 90);
            p.Put(Gen + "SM_Gen_Prop_Crate_Preset_01", -4.8f, 49.3f, 0);
        }

        // ---------- 12 cargo bay: racks stocked with the troupe's freight ----------

        // Each rack level only had two front/back beams, so loads looked suspended. Give every bay a plank deck on
        // step beams, then fill every slot of every level with a pallet and freight up to the level above.
        static int StockRack(Transform rack, Transform g)
        {
            MuteNamed(rack, "Pallet", "WrappedLoad", "CartonLoad", "CrateLoad");
            var uprights = rack.Cast<Transform>().Where(t => t.name == "Upright").Select(t => t.GetComponent<Renderer>().bounds).ToArray();
            var beams = rack.Cast<Transform>().Where(t => t.name == "Beam").Select(t => t.GetComponent<Renderer>().bounds).ToArray();
            if (uprights.Length < 4 || beams.Length < 2) return 0;
            bool alongZ = uprights.Max(b => b.center.z) - uprights.Min(b => b.center.z) > uprights.Max(b => b.center.x) - uprights.Min(b => b.center.x);
            float A(Vector3 v) => alongZ ? v.z : v.x; float C(Vector3 v) => alongZ ? v.x : v.z;
            Vector3 P(float a, float c, float y) => alongZ ? new Vector3(c, y, a) : new Vector3(a, y, c);
            var stations = uprights.Select(b => A(b.center)).Distinct(new Near()).OrderBy(v => v).ToArray();
            float c0 = uprights.Min(b => C(b.max)), c1 = uprights.Max(b => C(b.min)); // inner faces front/back
            float bc0 = beams.Min(b => C(b.min)), bc1 = beams.Max(b => C(b.max));     // the deck rests across both beams
            var levels = beams.Select(b => b.max.y).Distinct(new Near()).OrderBy(v => v).ToArray();
            float top = uprights.Max(b => b.max.y) - .35f; float post = uprights.Max(b => A(b.size)) * .5f;
            var rng = new System.Random(Mathf.RoundToInt(A(uprights[0].center) * 10 + C(uprights[0].center) * 3));
            int slots = 0;
            for (int l = 0; l < levels.Length; l++)
            {
                float y = levels[l], ceiling = l + 1 < levels.Length ? levels[l + 1] - .2f : top;
                for (int s = 0; s < stations.Length - 1; s++)
                {
                    float a0 = stations[s] + post, a1 = stations[s + 1] - post, am = (a0 + a1) * .5f, len = a1 - a0;
                    var deck = Box(g, "RackDeck", P(am, (bc0 + bc1) * .5f, y + .02f), Size3(len, .04f, bc1 - bc0, alongZ), Mat("V4_Wood_Old"));
                    deck.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                    foreach (var f in new[] { .25f, .75f }) Box(g, "StepBeam", P(a0 + len * f, (c0 + c1) * .5f, y - .03f), Size3(.05f, .05f, c1 - c0, alongZ), Mat("LA_RackOrange"));
                    // Two pallets across the depth of the rack.
                    float half = (c1 - c0) * .5f;
                    for (int k = 0; k < 2; k++)
                    {
                        float cm = c0 + half * (k + .5f); float deckTop = y + .04f;
                        var pallet = FitPrefab(Apo + "Props/SM_Prop_Pallet_01", g, P(am, cm, deckTop), alongZ ? 90 : 0, new Vector2(len - .15f, half - .08f), .16f, alongZ);
                        float baseY = pallet != null ? Bounds(pallet).max.y : deckTop;
                        StockLoad(g, rng, P(am, cm, baseY), new Vector2(len - .2f, half - .12f), ceiling - baseY, alongZ);
                        slots++;
                    }
                }
            }
            return slots;
        }

        sealed class Near : IEqualityComparer<float> { public bool Equals(float a, float b) => Mathf.Abs(a - b) < .05f; public int GetHashCode(float v) => 0; }
        static Vector3 Size3(float along, float height, float across, bool alongZ) => alongZ ? new Vector3(across, height, along) : new Vector3(along, height, across);

        // Instantiate a prefab, scale it uniformly into a footprint (along x across) and height, and stand it on `at`.
        static Transform FitPrefab(string prefab, Transform g, Vector3 at, float yaw, Vector2 foot, float maxH, bool alongZ, bool fill = false)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefab + ".prefab"); if (asset == null) { CircusLog.Add("missing " + prefab); return null; }
            var t = ((GameObject)PrefabUtility.InstantiatePrefab(asset, g)).transform; Undo.RegisterCreatedObjectUndo(t.gameObject, "Rack freight");
            foreach (var c in t.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            t.rotation = Quaternion.Euler(0, yaw, 0); var b = Bounds(t);
            float along = alongZ ? b.size.z : b.size.x, across = alongZ ? b.size.x : b.size.z;
            float k = Mathf.Min(foot.x / along, foot.y / across, maxH / b.size.y); if (!fill) k = Mathf.Min(k, 1.3f);
            t.localScale *= k; b = Bounds(t);
            t.position += new Vector3(at.x - b.center.x, at.y - b.min.y, at.z - b.center.z);
            foreach (var r in t.GetComponentsInChildren<Renderer>()) if (r.bounds.size.magnitude < .5f) r.shadowCastingMode = ShadowCastingMode.Off;
            return t;
        }

        // One slot's freight, filled close to the level above: carton stacks, stretch-wrapped loads, crates, trunks or sacks.
        static void StockLoad(Transform g, System.Random rng, Vector3 at, Vector2 foot, float height, bool alongZ)
        {
            if (height < .3f) return;
            Vector3 Off(float a, float c) => alongZ ? new Vector3(c, 0, a) : new Vector3(a, 0, c);
            switch (rng.Next(5))
            {
                case 0: // carton stack: a 2 x 2 grid of taped boxes, columns of different heights
                    for (int i = 0; i < 2; i++)
                    for (int j = 0; j < 2; j++)
                    {
                        float w = foot.x * .5f - .03f, d = foot.y * .5f - .03f; float y = at.y; int layers = 2 + rng.Next(3);
                        float layerH = Mathf.Min(.55f, height / layers);
                        for (int n = 0; n < layers && y + layerH <= at.y + height + .01f; n++)
                        {
                            var c = at + Off((i - .5f) * foot.x * .5f, (j - .5f) * foot.y * .5f); c.y = y + layerH * .5f;
                            float shrink = .92f + (float)rng.NextDouble() * .08f;
                            Box(g, "Carton", c, Size3(w * shrink, layerH - .01f, d * shrink, alongZ), Mat("LA_Cardboard"));
                            Box(g, "CartonTape", c + Vector3.up * (layerH * .5f - .004f), Size3(w * shrink * .98f, .01f, .06f, alongZ), Mat("LA_Paper"));
                            y += layerH;
                        }
                    }
                    break;
                case 1: // stretch-wrapped load with two straps
                {
                    float h = height * (.75f + (float)rng.NextDouble() * .2f);
                    Box(g, "WrappedLoad", at + Vector3.up * h * .5f, Size3(foot.x * .96f, h, foot.y * .96f, alongZ), Mat("LA_Wrap"));
                    foreach (var f in new[] { -.25f, .25f }) Box(g, "LoadStrap", at + Off(foot.x * f, 0) + Vector3.up * h * .5f, Size3(.04f, h + .01f, foot.y * .97f, alongZ), Mat("V4_Black"));
                    break;
                }
                case 2: // two crates side by side, a trunk on top where it fits
                {
                    float y = at.y;
                    for (int i = 0; i < 2; i++)
                    {
                        var crate = FitPrefab(Apo + "Props/SM_Prop_Crate_0" + (1 + rng.Next(4)), g, at + Off((i - .5f) * foot.x * .5f, 0) + Vector3.up * 0, rng.Next(2) * 90, new Vector2(foot.x * .5f - .04f, foot.y), height * .55f, alongZ, true);
                        if (crate != null) y = Mathf.Max(y, Bounds(crate).max.y);
                    }
                    if (at.y + height - y > .35f) FitPrefab(Gen + "SM_Gen_Prop_Chest_01", g, new Vector3(at.x, y, at.z), alongZ ? 90 : 0, foot * .9f, at.y + height - y, alongZ, true);
                    break;
                }
                case 3: // sack stacks
                {
                    var sacks = FitPrefab(Gen + "SM_Gen_Prop_Sack_Stack_0" + (1 + rng.Next(2)), g, at, rng.Next(4) * 90, foot, height * .9f, alongZ, true);
                    if (sacks != null && at.y + height - Bounds(sacks).max.y > .4f) FitPrefab(Apo + "Props/SM_Prop_Luggage_Retro_01", g, new Vector3(at.x, Bounds(sacks).max.y, at.z), alongZ ? 90 : 0, foot * .8f, .4f, alongZ);
                    break;
                }
                default: // large crate filling the slot
                    FitPrefab(Apo + "Props/SM_Prop_Crate_Large_0" + (1 + rng.Next(2)), g, at, alongZ ? 0 : 90, foot, height * .92f, alongZ, true);
                    break;
            }
        }

        static void CargoBayProps(Transform target, Transform g)
        {
            var bay = target.Find("S12_CargoBay"); if (bay == null) return;
            var p = NewPlacer(target, g, Rect.MinMaxRect(1.5f, 22.4f, 27.6f, 55.2f), 0, bay, target.Find("S11_StageGear"), target.Find("FacilityConnections"));
            int filled = 0;
            foreach (var rack in bay.GetComponentsInChildren<Transform>().Where(t => t.name == "PalletRack").ToArray()) filled += StockRack(rack, Group(g, "RackStock", Origin));
            CircusLog.Add("rack slots stocked " + filled);
            // Floor staging pallets: swap the cube pallets and loads for real pallets carrying crates, trunks, cartons and sacks.
            string[] loads = { Apo + "Props/SM_Prop_Crate_01", Gen + "SM_Gen_Prop_Chest_01", Apo + "Props/SM_Prop_CardboardBox_01", Gen + "SM_Gen_Prop_Sack_Stack_01", Apo + "Props/SM_Prop_Crate_Large_01", Apo + "Props/SM_Prop_Luggage_Retro_01" };
            int n = 0;
            // Include pallets muted by an earlier run so re-running rebuilds the same loads.
            var pairs = bay.GetComponentsInChildren<Transform>(true).Where(t => t.name == "StagedPallet" && t.parent.gameObject.activeInHierarchy).ToArray();
            foreach (var pal in pairs)
            {
                var parent = pal.parent; int i = pal.GetSiblingIndex();
                var load = i + 1 < parent.childCount ? parent.GetChild(i + 1) : null;
                if (load == null || !load.name.EndsWith("Load")) continue;
                var pb = MeshBounds(pal); var lb = MeshBounds(load);
                MuteFacility(pal); MuteFacility(load);
                foreach (var b in new[] { pb, lb }) p.Taken.RemoveAll(o => o.center == b.center && o.size == b.size);
                var newPallet = p.Put(Apo + "Props/SM_Prop_Pallet_01", pb.center.x, pb.center.z, 0, Mathf.Min(pb.size.x / 1.46f, pb.size.z / 1.42f), pb.min.y, false, .1f);
                if (newPallet == null) continue;
                var prefab = loads[n++ % loads.Length]; var top = Bounds(newPallet).max.y;
                // Fit the load to the old load volume, at least two pieces where it is wide enough.
                var probe = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefab + ".prefab")); var sz = Bounds(probe.transform).size; Object.DestroyImmediate(probe);
                int across = lb.size.x > sz.x * 1.9f || lb.size.z > sz.z * 1.9f ? 2 : 1;
                float scale = Mathf.Min(1.2f, lb.size.y / sz.y, (lb.size.x / across) / sz.x, lb.size.z / sz.z) * .95f;
                for (int k = 0; k < across; k++)
                {
                    float off = across == 1 ? 0 : (k - .5f) * lb.size.x * .5f;
                    p.Put(prefab, lb.center.x + off, lb.center.z, (n * 37 + k * 90) % 360 < 180 ? 0 : 90, scale, top, false, .1f);
                }
            }
            CircusLog.Add("staged loads replaced " + n);
            // Floor freight between the racks and the containers: wrapped barrels, carts, cartons, rope and the odd stray trunk.
            foreach (var (x, z, yaw) in new[] { (24.2f, 40.1f, 0f), (24.2f, 30.8f, 0f) }) p.Put(Apo + "Props/SM_Prop_BarrelStack_01_Tarp", x, z, 90 + yaw);
            p.Put(Apo + "Props/SM_Prop_Cart_01", 22.6f, 46.4f, 20);
            p.Put(Apo + "Props/SM_Prop_Wheelbarrow_01", 4.2f, 51.8f, 140);
            foreach (var (x, z) in new[] { (2.6f, 43.8f), (2.6f, 33.2f) })
            {
                var pal = p.Put(Apo + "Props/SM_Prop_Pallet_01", x, z, 0);
                var c1 = p.On(pal, Apo + "Props/SM_Prop_CardboardBox_01", 0, 1, -.3f); p.On(pal, Apo + "Props/SM_Prop_CardboardBox_03", 90, 1, .35f);
                p.On(c1, Apo + "Props/SM_Prop_CardboardBox_01", 10);
            }
            p.Put(Gen + "SM_Gen_Prop_Crate_Preset_01", 19.4f, 52.6f, 180);
            foreach (var (x, z) in new[] { (11.8f, 52.6f), (12.6f, 53.2f) }) p.Put(Apo + "Props/SM_Prop_Luggage_" + (x > 12 ? "Retro_Open_01" : "01"), x, z, 20 + x * 10, 1, null, false, .6f);
            p.Put(Apo + "Props/SM_Prop_Spool_01", 25.8f, 49.2f, 0, .6f);
            foreach (var (x, z) in new[] { (5.6f, 27.6f), (25.6f, 26.9f) }) p.Put(Gen + "SM_Gen_Prop_Barrel_Wood_0" + (x > 10 ? 2 : 1), x, z, x * 17);
        }
    }
}
