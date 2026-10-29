using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Editor
{
    public static partial class WaitingRoomLayoutA
    {
        // The dormitory becomes an interaction space: it grows west into the empty strip beside the practice corridor
        // and north up to the arena wall, and its furniture spreads out so every bed has room around it.
        const float DormWideX0 = -4.3f, DormDeepZ1 = 24.7f; // local; world x -4.3, z 94.7

        public static string ExpandDormitory()
        {
            var (scene, target) = Begin(); var original = Snapshot(source); int undo = StartUndo("Expand dormitory");
            try
            {
                var root = target.Find("S02_Dormitory"); var dorm = root.Find("Dormitory");
                var shell = dorm.Find("Shell"); var old = Bounds(shell);
                // The dome relayout may have moved the whole dorm block; every plan coordinate below follows it.
                var shift = root.position - DormRoot0;
                bool already = old.min.x - shift.x < DormX0 - 1f;
                float ox0 = DormX0 + shift.x, ox1 = DormX1 + shift.x, oz0 = W(0, DormZ0).z + shift.z, oz1 = W(0, DormZ1).z + shift.z;
                float nx0 = DormWideX0 + shift.x, nx1 = DormX1 + shift.x, nz0 = oz0, nz1 = W(0, DormDeepZ1).z + shift.z;
                float restX1 = W(RestX1, 0).x + shift.x, lobbyN = W(0, LobbyN).z + shift.z;
                if (!already) SpreadDormFurniture(dorm, ox0, ox1, oz0, oz1, nx0, nx1, nz0, nz1);
                // The boarded skylight hung 0.8 m under the ceiling and came apart when spread; indoors there is no glazing anyway.
                MuteFacility(dorm.Find("Skylight"));
                MuteNamed(dorm.Find("Living"), "OldWallStud"); // replaced by the steel frame; spread off the wall they stood on

                Undo.DestroyObjectImmediate(shell.gameObject);
                RoomShell(dorm, "LA_Dorm", DormWideX0, DormX1, DormZ0, DormDeepZ1, DormHeight, 2.2f, Mat("LA_FloorConcrete"), Mat("LA_WallConcrete"), Mat("LA_WallRust"),
                    new[] { (2, RestDoorZ, RestDoorWidth, .1f), (3, DormDoorX, DormDoorWidth, .5f) }, eastThickness: .1f);
                dorm.Find("Shell").position += shift;
                var floor = root.Find("FloorCollider"); var box = floor.GetComponent<BoxCollider>(); Undo.RecordObject(floor, "Dorm floor"); Undo.RecordObject(box, "Dorm floor");
                floor.position = new Vector3((nx0 + restX1) * .5f, -.1f, (lobbyN - .1f + nz1) * .5f);
                box.size = new Vector3(restX1 - nx0 + .2f, .2f, nz1 - lobbyN + .1f);
                // Camera zone and the exploration-area box follow the room.
                FitBox(dorm.Find("Zone_Dormitory"), nx0 + .1f, nx1 - .1f, nz0 + .1f, nz1 - .1f, 0, 6);
                var area = target.Find("ExplorationTimer/WaitingRoomArea/Area_Dormitory");
                if (area != null) FitBox(area, nx0, restX1 + .2f, lobbyN, nz1, -1, 7);
                // The practice corridor bench poked 1.2 m through its wall into the strip the dorm now uses.
                var bench = target.Find("S09_PracticeRoom/Corridor/Dressing/SM_Prop_Plank_Bench_01");
                if (bench != null && bench.gameObject.activeInHierarchy) { var bb = Bounds(bench); if (bb.max.x > W(CorrX1, 0).x - .1f) MoveFacility(bench, bench.position + Vector3.left * (bb.max.x - (W(CorrX1, 0).x - .15f))); }
                Physics.SyncTransforms(); AssertUnchanged(original);
                Undo.CollapseUndoOperations(undo); EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
                return $"Dormitory {nx1 - nx0:F1} x {nz1 - nz0:F1} m (was {ox1 - ox0:F1} x {oz1 - oz0:F1}); furniture {(already ? "kept" : "spread")}";
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        static void FitBox(Transform t, float x0, float x1, float z0, float z1, float y0, float y1)
        {
            if (t == null) return; var c = t.GetComponent<BoxCollider>(); Undo.RecordObject(t, "Fit zone"); Undo.RecordObject(c, "Fit zone");
            t.position = new Vector3((x0 + x1) * .5f, (y0 + y1) * .5f, (z0 + z1) * .5f); c.center = Vector3.zero;
            var s = t.lossyScale; c.size = new Vector3((x1 - x0) / s.x, (y1 - y0) / s.y, (z1 - z0) / s.z);
        }

        // Things against a wall keep their gap to that wall; the rest spread in proportion. Each bed carries its own
        // trunk, lamp, stool and bear with it. The clothes line could not span the wider room, so the clothes go on the beds.
        static void SpreadDormFurniture(Transform dorm, float ox0, float ox1, float oz0, float oz1, float nx0, float nx1, float nz0, float nz1)
        {
            float Map(float v, float a0, float a1, float b0, float b1, float lo, float hi)
            {
                if (lo - a0 < .35f) return b0 + (lo - a0) + (v - lo);
                if (a1 - hi < .35f) return b1 - (a1 - hi) + (v - hi);
                return b0 + (v - a0) * (b1 - b0) / (a1 - a0);
            }
            Vector3 Target(Bounds b) => new(Map(b.center.x, ox0, ox1, nx0, nx1, b.min.x, b.max.x), b.center.y, Map(b.center.z, oz0, oz1, nz0, nz1, b.min.z, b.max.z));
            var beds = dorm.Find("Beds");
            var bedFrames = beds.Cast<Transform>().Where(t => t.name.StartsWith("Bed_")).ToArray();
            var shift = new Dictionary<Transform, Vector3>();
            foreach (var bed in bedFrames) { var b = Bounds(bed); shift[bed] = Target(b) - b.center; }
            foreach (Transform t in beds.Cast<Transform>().ToArray())
            {
                if (bedFrames.Contains(t)) { MoveFacility(t, t.position + shift[t]); continue; }
                var b = t.GetComponentsInChildren<Renderer>().Length > 0 ? Bounds(t) : new Bounds(t.position, Vector3.zero);
                var owner = bedFrames.OrderBy(f => Mathf.Abs(Bounds(f).center.x - b.center.x)).First();
                bool belongs = Mathf.Abs(Bounds(owner).center.x - b.center.x) < 1.1f && !t.name.Contains("Locker") && !t.name.Contains("Wardrobe");
                MoveFacility(t, t.position + (belongs ? shift[owner] : Target(b) - b.center));
            }
            foreach (var group in new[] { "Living", "Skylight" })
            {
                var g = dorm.Find(group); if (g == null) continue;
                foreach (Transform t in g.Cast<Transform>().ToArray())
                {
                    if (t.GetComponentsInChildren<Renderer>().Length == 0) { MoveFacility(t, Target(new Bounds(t.position, Vector3.zero)) - Vector3.zero); continue; }
                    var b = Bounds(t); MoveFacility(t, t.position + (Target(b) - b.center));
                }
            }
            foreach (var t in dorm.Cast<Transform>().Where(t => t.name == "RustStain").ToArray()) { var b = Bounds(t); MoveFacility(t, t.position + (Target(b) - b.center)); }
            MuteFacility(dorm.Find("Living/PersonalClothesline"));
            foreach (var clothes in beds.Cast<Transform>().Where(t => t.name.StartsWith("PersonalClothes_")))
            {
                var cb = Bounds(clothes); var bed = bedFrames.OrderBy(f => Vector2.Distance(new Vector2(Bounds(f).center.x, Bounds(f).center.z), new Vector2(cb.center.x, cb.center.z))).First();
                var bb = Bounds(bed);
                // Folded at the foot of the mattress: sink slightly into the bedding so it reads as resting on it.
                var hit = Physics.Raycast(new Vector3(bb.center.x, bb.max.y + 1, bb.min.z + .45f), Vector3.down, out var h, 3, ~0, QueryTriggerInteraction.Ignore) ? h.point.y : bb.min.y + .55f;
                MoveFacility(clothes, clothes.position + new Vector3(bb.center.x - cb.center.x, hit - .02f - cb.min.y, bb.min.z + .45f - cb.center.z));
            }
            // Four ceiling lamps instead of two for the larger floor, cords cut at the 5 m ceiling.
            var lamps = dorm.Cast<Transform>().Where(t => t.name == "Dormitory_Lamp").ToArray();
            var spots = new[] { (nx0 + (nx1 - nx0) * .25f, nz0 + (nz1 - nz0) * .35f), (nx0 + (nx1 - nx0) * .75f, nz0 + (nz1 - nz0) * .35f), (nx0 + (nx1 - nx0) * .25f, nz0 + (nz1 - nz0) * .72f), (nx0 + (nx1 - nx0) * .75f, nz0 + (nz1 - nz0) * .72f) };
            for (int i = 0; i < spots.Length; i++)
            {
                Transform lamp;
                if (i < lamps.Length) lamp = lamps[i];
                else { lamp = Object.Instantiate(lamps[i % lamps.Length].gameObject, dorm).transform; lamp.name = "Dormitory_Lamp"; Undo.RegisterCreatedObjectUndo(lamp.gameObject, "Extra dorm lamp"); }
                MoveFacility(lamp, new Vector3(spots[i].Item1, lamp.position.y, spots[i].Item2));
                TrimCord(lamp, W(0, 0, DormHeight).y);
            }
        }
    }
}
