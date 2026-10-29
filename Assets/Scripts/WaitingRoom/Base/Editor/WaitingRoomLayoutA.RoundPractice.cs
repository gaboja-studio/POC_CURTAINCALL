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
        // Preserve the 52m round hall, 18m roof and small stand. Restore the original 16m ring and canopy,
        // including its 0.6m deck, lanes, access steps and ladder collision repairs, inside this hall.
        static readonly Vector2 PracC = new(-20f, 128.5f);
        const float PracR = 26f, PracWallH = 12f, PracTopH = 18f, RingR = 13f, RingDeckH = .6f;

        public static string BuildRoundPracticeRoom()
        {
            var (scene, target) = Begin(); var original = Snapshot(source); int undo = StartUndo("Round practice hall");
            try
            {
                var room = target.Find("S09_PracticeRoom");
                MuteFacility(room.Find("Arena")); MuteFacility(room.Find("Corridor"));
                Remove(room, "RoundArena"); var g = Group(room, "RoundArena", new Vector3(PracC.x, 0, PracC.y));
                var entry = LobbyPassages().Single(p => p.Name == "Practice");
                BuildRoundHall(g, "LA_PracticeHall", PracC, PracR, PracWallH, PracTopH, new List<Passage> { entry }, 20, true);
                RestoreLegacyPracticeRing(room);
                SmallStand(Group(g, "Stand", g.position));
                PracticeLights(Group(g, "Lights", g.position));
                Physics.SyncTransforms(); AssertUnchanged(original);
                Undo.CollapseUndoOperations(undo); EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
                return $"Round practice hall {PracR * 2} m, original ring {PracRingRadius * 2} m restored";
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        static Vector3 PW(float x, float z, float y = 0) => new(PracC.x + x, y, PracC.y + z);

        // Ring deck as a short cylinder (walkable top, solid skirt), a padded curb, and three-step stairs at four points.
        static void RaisedRing(Transform g)
        {
            const int seg = 96; var mb = new MeshBuilder("LA_PracticeRingDeck", 2);
            Vector3 P(float r, float a, float y) => PW(Mathf.Cos(a) * r, Mathf.Sin(a) * r, y);
            for (int k = 0; k < 4; k++)
                for (int i = 0; i < seg; i++)
                {
                    float a0 = i * Mathf.PI * 2 / seg, a1 = (i + 1) * Mathf.PI * 2 / seg, r0 = RingR * k / 4, r1 = RingR * (k + 1) / 4;
                    mb.Quad(0, P(r0, a0, RingDeckH), P(r1, a0, RingDeckH), P(r1, a1, RingDeckH), P(r0, a1, RingDeckH), Vector3.up, RingR * 2);
                }
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2 / seg, a1 = (i + 1) * Mathf.PI * 2 / seg; var n = new Vector3(Mathf.Cos((a0 + a1) * .5f), 0, Mathf.Sin((a0 + a1) * .5f));
                mb.Quad(1, P(RingR, a0, 0), P(RingR, a0, RingDeckH), P(RingR, a1, RingDeckH), P(RingR, a1, 0), n, 2f);
            }
            var deck = MeshObject(g, "RingDeck", Vector3.zero, SaveMesh(mb.Build()), Mat("LA_RingFloor"), Mat("LA_TentRed"));
            deck.AddComponent<MeshCollider>().sharedMesh = deck.GetComponent<MeshFilter>().sharedMesh;
            for (int i = 0; i < 48; i++)
            {
                float a = (i + .5f) * Mathf.PI * 2 / 48; if (i % 12 == 0 || i % 12 == 11) continue; // gaps where the stairs land
                var c = P(RingR - .2f, a, RingDeckH + .15f);
                var curb = Box(g, "RingCurb", c, new Vector3(.3f, .3f, RingR * Mathf.PI * 2 / 48 + .02f), Mat(i % 2 == 0 ? "LA_TentRed" : "LA_TentCream"), true);
                curb.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)));
            }
            foreach (var a in new[] { 0f, 90f, 180f, 270f })
            {
                var dir = Quaternion.Euler(0, -a, 0) * Vector3.right;
                for (int s = 0; s < 3; s++)
                {
                    float h = RingDeckH * (s + 1) / 3; var c = PW(0, 0) + dir * (RingR + .9f - s * .4f) + Vector3.up * h * .5f;
                    Box(g, "RingStep", c, new Vector3(2.4f, h, .42f), Mat("V4_Wood_Old"), true).rotation = Quaternion.LookRotation(dir);
                }
            }
        }

        // Tower with deck, rail and a fixed ladder; the deck is walkable.
        static Transform PracticeTower(Transform g, Vector3 foot, float deckH, Vector3 ladderDir)
        {
            var t = Group(g, "Tower", foot);
            foreach (var (dx, dz) in new[] { (-.7f, -.7f), (.7f, -.7f), (-.7f, .7f), (.7f, .7f) })
                Box(t, "TowerLeg", foot + new Vector3(dx, deckH * .5f, dz), new Vector3(.14f, deckH, .14f), Mat("LA_SteelPlate"), true);
            Box(t, "TowerDeck", foot + Vector3.up * (deckH - .05f), new Vector3(1.7f, .1f, 1.7f), Mat("V4_Wood_Old"), true);
            foreach (var y in new[] { deckH * .35f, deckH * .7f })
                foreach (var (dx, dz, sx, sz) in new[] { (0f, -.7f, 1.4f, .05f), (0f, .7f, 1.4f, .05f), (-.7f, 0f, .05f, 1.4f), (.7f, 0f, .05f, 1.4f) })
                    Box(t, "TowerBrace", foot + new Vector3(dx, y, dz), new Vector3(sx, .05f, sz), Mat("V4_Iron_Dark"));
            foreach (var (dx, dz, sx, sz) in new[] { (0f, -.8f, 1.6f, .05f), (0f, .8f, 1.6f, .05f) })
                Box(t, "TowerRail", foot + new Vector3(dx, deckH + .9f, dz), new Vector3(sx, .05f, sz), Mat("V4_Iron_Dark"));
            foreach (var (dx, dz) in new[] { (-.8f, -.8f), (.8f, -.8f), (-.8f, .8f), (.8f, .8f) })
                Box(t, "TowerRailPost", foot + new Vector3(dx, deckH + .45f, dz), new Vector3(.05f, .9f, .05f), Mat("V4_Iron_Dark"));
            var lad = foot + ladderDir * .95f;
            foreach (var side in new[] { -.25f, .25f })
                Box(t, "LadderRail", lad + Vector3.Cross(ladderDir, Vector3.up) * side + Vector3.up * deckH * .5f, new Vector3(.05f, deckH, .05f), Mat("V4_Iron_Dark"));
            for (float y = .3f; y < deckH; y += .3f)
                Box(t, "LadderRung", lad + Vector3.up * y, new Vector3(.5f, .035f, .035f), Mat("V4_Iron_Dark")).rotation = Quaternion.LookRotation(ladderDir);
            return t;
        }

        static void Tightrope(Transform g)
        {
            // North chord of the ring: towers stand on the hall floor just outside the ring, so the rope spans
            // about 23.5 m between deck edges, 3.5 m above the ring deck, with cushions along the line below.
            float z = 6.4f, x = 11.75f, h = RingDeckH + RopeHeight;
            PracticeTower(g, PW(-x - .85f, z), h, Vector3.left);
            PracticeTower(g, PW(x + .85f, z), h, Vector3.right);
            var a = PW(-x, z, h + .03f); var b = PW(x, z, h + .03f);
            Prim(g, PrimitiveType.Cylinder, "WalkingRope", (a + b) * .5f, new Vector3(.05f, (b - a).magnitude * .5f, .05f), Mat("V4_Black")).rotation = Quaternion.FromToRotation(Vector3.up, b - a);
            for (float px = -9.6f; px <= 9.7f; px += 2.1f)
                PlaceKit("PerformanceArtKit/LandingCushion", g, PW(px, z, RingDeckH), 0, .5f);
            Sign(g, "외줄타기", PW(-9.8f, z - 1.4f, RingDeckH + 2.2f), 0);
        }

        static void CannonLane(Transform g)
        {
            // South chord: cannon fires east along z = -6.5 onto a catch trampoline with cushions behind it.
            float z = -6.8f;
            PlaceKit("PerformanceArtKit/HumanCannon", g, PW(-8.6f, z, RingDeckH), 90, 1f);
            PlaceKit("HumanCannonArtKit/RoundCarryTrampolineVisual", g, PW(6.2f, z, RingDeckH), 0, .9f);
            foreach (var dx in new[] { 9.6f }) PlaceKit("PerformanceArtKit/LandingCushion", g, PW(dx, z, RingDeckH), 0, .55f);
            for (float px = -5.8f; px < 4f; px += .9f) Box(g, "LaneStripe", PW(px, z, RingDeckH + .006f), new Vector3(.5f, .01f, .12f), Mat("LA_HazardPaint"));
            Sign(g, "인간 대포", PW(-8.6f, z - 2.3f, RingDeckH + 2f), 0);
        }

        static void FreeTrampoline(Transform g)
        {
            PlaceKit("PerformanceArtKit/CarryTrampoline", g, PW(-7.4f, .3f, RingDeckH), 0, .75f);
            Sign(g, "트램펄린", PW(-7.4f, -1.6f, RingDeckH + 1.6f), 0);
        }

        static void TrapezeRig(Transform g)
        {
            // A-frame rig east of centre: posts to 7 m, a cross bar, the trapeze hung from it, a take-off platform and cushions.
            float x = 6.2f, z = .4f, top = RingDeckH + 7f;
            foreach (var dz in new[] { -2.4f, 2.4f })
            {
                Box(g, "RigPost", PW(x, z + dz, RingDeckH + 3.5f), new Vector3(.2f, 7f, .2f), Mat("LA_SteelPlate"), true);
                foreach (var dx in new[] { -1.2f, 1.2f })
                {
                    var foot = PW(x + dx, z + dz, RingDeckH); var head = PW(x, z + dz, top - .6f);
                    Prim(g, PrimitiveType.Cylinder, "RigStay", (foot + head) * .5f, new Vector3(.08f, (head - foot).magnitude * .5f, .08f), Mat("V4_Iron_Dark")).rotation = Quaternion.FromToRotation(Vector3.up, head - foot);
                }
            }
            Box(g, "RigBar", PW(x, z, top), new Vector3(.2f, .2f, 5f), Mat("LA_SteelPlate"));
            var trapeze = PlaceKit("PerformanceArtKit/Trapeze", g, PW(x, z, top - 2.6f), 90, 1f);
            if (trapeze != null) { var b = Bounds(trapeze); trapeze.position += Vector3.up * (top - .1f - b.max.y); }
            PracticeTower(g, PW(x - 3.6f, z, RingDeckH), 2.6f, Vector3.left);
            foreach (var dz in new[] { -1f, 1f }) PlaceKit("PerformanceArtKit/LandingCushion", g, PW(x, z + dz, RingDeckH), 0, .5f);
            Sign(g, "인간 그네", PW(x, z - 2.7f, RingDeckH + 2.4f), 0);
        }

        static void SmallStand(Transform g)
        {
            // Two low tiers on the north-east arc only (the ring is the point of the room, not the audience).
            for (int i = 0; i < 14; i++)
            {
                float a = (30f + i * 6f) * Mathf.Deg2Rad;
                foreach (var (r, h) in new[] { (16.2f, .45f), (17.4f, .9f) })
                {
                    var c = PW(Mathf.Cos(a) * r, Mathf.Sin(a) * r, h * .5f); var along = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a));
                    Box(g, "StandTier", c, new Vector3(1.2f, h, r * 6f * Mathf.Deg2Rad + .02f), Mat("V4_Wood_Old"), true).rotation = Quaternion.LookRotation(along);
                    Box(g, "StandBench", c + Vector3.up * (h * .5f + .03f), new Vector3(.5f, .06f, r * 6f * Mathf.Deg2Rad - .05f), Mat("LA_TentRed")).rotation = Quaternion.LookRotation(along);
                }
            }
        }

        static void PracticeLights(Transform g)
        {
            foreach (var (x, z) in new[] { (0f, 6.4f), (-1f, -6.8f), (6.2f, .4f), (-7.4f, .3f) })
                Spot(g, "StationSpot", PW(x * .6f, z * .6f, PracTopH - 3f), PW(x, z, RingDeckH), WhiteLight, 9f, 22f, 38f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3; var p = PW(Mathf.Cos(a) * 19f, Mathf.Sin(a) * 19f, 8f);
                Point(g, "HallFill", p, WhiteLight, 2.2f, 16f);
            }
        }

        static Transform PlaceKit(string path, Transform g, Vector3 at, float yaw, float scale)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Kit + path + ".prefab"); if (asset == null) return null;
            var t = ((GameObject)PrefabUtility.InstantiatePrefab(asset, g)).transform; t.localScale = Vector3.one * scale; t.rotation = Quaternion.Euler(0, yaw, 0);
            var b = Bounds(t); t.position += new Vector3(at.x - b.center.x, at.y - b.min.y, at.z - b.center.z);
            return t;
        }

        static void Sign(Transform g, string text, Vector3 at, float yaw)
        {
            Box(g, "SignPost", new Vector3(at.x, (at.y + RingDeckH) * .5f - .1f, at.z), new Vector3(.08f, at.y - RingDeckH, .08f), Mat("V4_Iron_Dark"));
            Box(g, "SignBoard", at + Vector3.up * .05f, new Vector3(1.4f, .42f, .05f), Mat("V4_Wood_Old"));
            FacilityLabel(g, text, at + new Vector3(0, .05f, -.04f), Quaternion.Euler(0, yaw, 0), .045f);
        }
    }
}
