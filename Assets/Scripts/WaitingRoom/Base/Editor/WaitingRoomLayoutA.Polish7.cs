using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Editor
{
    // Seventh round: the lobby gallery had been built over the cargo passage mouth (its wall scan read the passage's own
    // walls as dome wall), so its stair, column and leaning beam stood in the doorway. The gallery turns onto the blank
    // wall between the cargo and stage passages and gets a compact switchback stair. Open steel stairs get side guards
    // low enough to jump over but too high to walk through.
    public static partial class WaitingRoomLayoutA
    {
        // Guard height above the walking surface, below the 0.8 m jump. At 0.5 m the controller still rode over it
        // sideways on a 36° flight; 0.6 m held on every side in a CharacterController sweep test.
        const float StairGuardHeight = .6f;

        // Invisible side guards on a sloped ramp collider (local z along the flight, local y its normal).
        static void AddRampGuards(Transform ramp, float halfWidth)
        {
            foreach (Transform old in ramp.Cast<Transform>().Where(c => c.name == "StairGuard").ToArray()) Undo.DestroyObjectImmediate(old.gameObject);
            var box = ramp.GetComponent<BoxCollider>();
            foreach (float side in new[] { -1f, 1f })
            {
                var g = new GameObject("StairGuard"); Undo.RegisterCreatedObjectUndo(g, "Stair guard");
                g.transform.SetParent(ramp, false);
                g.transform.localPosition = new Vector3(side * halfWidth, box.center.y + box.size.y * .5f + StairGuardHeight * .5f, box.center.z);
                g.AddComponent<BoxCollider>().size = new Vector3(.06f, StairGuardHeight, box.size.z);
            }
        }

        // Invisible guard box with its base on a flat walking surface.
        static Transform FlatGuard(Transform parent, string name, Vector3 localBase, Vector3 alongSize)
        {
            var g = new GameObject(name); Undo.RegisterCreatedObjectUndo(g, "Stair guard");
            g.transform.SetParent(parent, false); g.transform.localPosition = localBase + Vector3.up * StairGuardHeight * .5f;
            g.AddComponent<BoxCollider>().size = new Vector3(alongSize.x, StairGuardHeight, alongSize.z);
            return g.transform;
        }

        public static string ClearLobbyMezzanineDoors() => RunPass("Lobby gallery off the cargo passage", target =>
        {
            var lobby = target.Find("S01_Lobby"); var g = lobby.Find("LobbyMezzanine") ?? throw new InvalidOperationException("LobbyMezzanine missing");
            var floorB = lobby.Find("Frame8Lobby/RoundFloor").GetComponent<Renderer>().bounds; var hub = new Vector3(floorB.center.x, floorB.max.y, floorB.center.z);
            float wallR = floorB.extents.x - .05f; const float W = 2.8f, deckY = 4.6f; float innerR = wallR - W;
            Vector3 Dir(float deg) => Quaternion.Euler(0, -deg, 0) * Vector3.right;
            float AngleOf(Vector3 p) { var v = p - hub; float a = Mathf.Atan2(v.z, v.x) * Mathf.Rad2Deg; return a < 0 ? a + 360 : a; }
            // Blank wall runs 228°–252° between the cargo (≈210°–224°) and stage (≈255°–282°) passage mouths.
            const float A0 = 228f, Step = 8f;

            // Turn the whole gallery (decks, pipes, collapsed steel) so its first segment starts at A0.
            var deck0 = g.Find("Lobby0_Deck_0"); float turn = (A0 + Step * .5f) - AngleOf(deck0.position);
            Undo.RecordObject(g, "Turn gallery"); g.RotateAround(hub, Vector3.up, -turn);

            // The old straight stair would reach 32° along the wall, into a doorway either way; replace it.
            foreach (var n in new[] { "LobbyMezzanineStair", "StairLanding" }) { var t = g.Find(n); if (t != null) Undo.DestroyObjectImmediate(t.gameObject); }

            // Columns at the gallery's ends (start of the first deck, end of the last standing one), clear of the stair.
            var cols = g.Cast<Transform>().Where(t => t.name == "Column").OrderBy(t => AngleOf(t.position)).ToArray();
            var plates = g.Cast<Transform>().Where(t => t.name == "BasePlate").OrderBy(t => AngleOf(t.position)).ToArray();
            float[] colAngles = { A0, A0 + Step * 2 };
            for (int i = 0; i < cols.Length && i < 2; i++)
            {
                var at = hub + Dir(colAngles[i]) * (innerR + .15f);
                Undo.RecordObject(cols[i], "Move column"); cols[i].position = new Vector3(at.x, cols[i].position.y, at.z);
                if (i < plates.Length) { Undo.RecordObject(plates[i], "Move plate"); plates[i].position = new Vector3(at.x, plates[i].position.y, at.z); }
            }

            // Switchback: the lower flight starts under the deck edge and climbs towards the hub to a mid landing, the
            // upper flight climbs back out onto the first (rail-less) deck segment.
            float upperAngle = A0 + Step * .5f, half = .7f, rise = deckY * .5f, run = rise / Mathf.Tan(36 * Mathf.Deg2Rad);
            float stairAngle = upperAngle + half / innerR * Mathf.Rad2Deg; // flights sit side by side, the upper one at upperAngle
            var outward = Dir(stairAngle); var edge = hub + outward * innerR; edge.y = hub.y;
            var s = Group(g, "LobbyMezzanineStair", edge); s.rotation = Quaternion.LookRotation(outward); // local +x = towards smaller angles
            SteelStair(s, "UpperFlight", s.TransformPoint(new Vector3(half, rise, -run)), outward, rise);
            SteelStair(s, "LowerFlight", s.TransformPoint(new Vector3(-half, 0, 0)), -outward, rise);
            Material plate = DiamondPlateMaterial(), steel = Mat("LA_SteelPlate"), dark = Mat("V4_Iron_Dark");
            const float landD = 1.4f; float landW = half * 2 + 1.3f, zN = -run - landD;
            LBox(s, "MidLanding", new Vector3(0, rise - .03f, -run - landD * .5f), new Vector3(landW, .06f, landD), plate, true);
            LBox(s, "MidLandingBeam", new Vector3(0, rise - .16f, zN + .06f), new Vector3(landW, .2f, .1f), dark);
            foreach (float x in new[] { -landW * .5f + .06f, landW * .5f - .06f }) foreach (float z in new[] { zN + .06f, -run - .06f })
                LBox(s, "MidLandingPost", new Vector3(x, (rise - .06f) * .5f, z), new Vector3(.1f, rise - .06f, .1f), dark, true);
            // Rail on the landing's open sides: the hub side and both ends.
            LBox(s, "LandingRail", new Vector3(0, rise + 1f, zN + .03f), new Vector3(landW, .05f, .05f), steel);
            foreach (float x in new[] { -landW * .5f + .03f, landW * .5f - .03f })
            {
                LBox(s, "LandingRail", new Vector3(x, rise + 1f, zN + landD * .5f), new Vector3(.05f, .05f, landD), steel);
                LBox(s, "LandingRailPost", new Vector3(x, rise + .5f, zN + .03f), new Vector3(.05f, 1f, .05f), steel);
                FlatGuard(s, "LandingGuard", new Vector3(x, rise, zN + landD * .5f), new Vector3(.06f, 0, landD));
            }
            FlatGuard(s, "LandingGuard", new Vector3(0, rise, zN + .03f), new Vector3(landW, 0, .06f));

            // Deck segment 0 has no rail; close the gaps either side of where the upper flight arrives.
            var segLen = deck0.Find("DeckPlate").localScale.x;
            foreach (float side in new[] { -1f, 1f })
            {
                float a = .7f, b = segLen * .5f, cx = side * (a + b) * .5f, len = b - a; float z = W * .5f - .05f;
                LBox(deck0, "RailPost", new Vector3(side * b, .55f, z), new Vector3(.05f, 1.1f, .05f), steel, true);
                LBox(deck0, "TopRail", new Vector3(cx, 1.1f, z), new Vector3(len, .05f, .05f), steel);
                LBox(deck0, "MidRail", new Vector3(cx, .55f, z), new Vector3(len, .035f, .035f), steel);
                LBox(deck0, "RailCollider", new Vector3(cx, .55f, z), new Vector3(len, 1.1f, .06f), steel, true).GetComponent<Renderer>().enabled = false;
            }

            // Leaning I-beam: from the second deck's edge down onto the floor below the broken end, away from both doors.
            var beam = g.Find("Lobby_CollapsedSteel/LeaningIBeam");
            if (beam != null)
            {
                var top = hub + Dir(A0 + Step * 2 - .5f) * (innerR + .1f) + Vector3.up * (deckY - .1f);
                var foot = hub + Dir(A0 + Step * 2 + 5f) * (innerR - 1.4f); foot.y = hub.y + .12f;
                Undo.RecordObject(beam, "Move beam");
                beam.SetPositionAndRotation((top + foot) * .5f, Quaternion.LookRotation(foot - top) * Quaternion.Euler(0, 0, 12));
                float bl = (foot - top).magnitude + .4f;
                foreach (Transform c in beam) { Undo.RecordObject(c, "Beam length"); var sc = c.localScale; sc.z = bl; c.localScale = sc; }
            }
            Physics.SyncTransforms();

            // Walk checks: hub to both passage mouths (three parallel lanes each), and nothing standing in either mouth.
            int blocked = 0, samples = 0;
            foreach (float doorAngle in new[] { 217f, 268f })
            {
                var d = Dir(doorAngle); var side = Vector3.Cross(Vector3.up, d);
                foreach (float off in new[] { -1.2f, 0f, 1.2f })
                    for (float r = 5f; r <= wallR + .5f; r += .5f)
                    {
                        var p = hub + d * r + side * off; samples++;
                        if (Physics.CheckCapsule(p + Vector3.up * .35f, p + Vector3.up * 1.4f, .3f, ~0, QueryTriggerInteraction.Ignore)) blocked++;
                    }
            }
            // Climb check: rays down along each flight centreline must land on a ramp, not miss or hit something else.
            int climbMiss = 0;
            foreach (var (x, z0, z1) in new[] { (-half, -.2f, -run + .2f), (half, -run + .2f, -.2f) })
                for (int k = 0; k <= 6; k++)
                {
                    var p = s.TransformPoint(new Vector3(x, 6f, Mathf.Lerp(z0, z1, k / 6f)));
                    if (!Physics.Raycast(p, Vector3.down, out var h, 8f, ~0, QueryTriggerInteraction.Ignore) || h.collider.name != "StairRamp") climbMiss++;
                }
            return $"Gallery turned {turn:F1}° to {A0}°–{A0 + Step * 3}°, switchback stair at {stairAngle:F1}°; doorway lanes blocked {blocked}/{samples}, flight rays off-ramp {climbMiss}/14";
        });

        // ================= Schedule board on an old wooden post board by the stage passage =================

        // Lobby floor just east of the stage passage mouth (x -10.5…-1.5 at z 63.5), clear of the path into it.
        static readonly Vector3 SchedulePostBase = new(-.35f, 0, 65.9f);

        public static string ScheduleOnStagePostBoard() => RunPass("Schedule board on a wooden post board", target =>
        {
            var lobby = target.Find("S01_Lobby"); var gameplay = lobby.Find("Gameplay");
            Transform Find(string n) => target.Find("S02_Dormitory/" + n) ?? gameplay.Find(n) ?? throw new InvalidOperationException(n + " missing");
            Transform root = Find("ScheduleBoard"), frame = Find("ScheduleFrame"), cam = Find("ScheduleCloseUp"); var board = root.Find("Board");
            Material wood = Mat("V4_Wood_Old"), dark = Mat("Wood_Dark"), sign = Mat("V4_Sign_Wood"), iron = Mat("V4_Iron_Dark"), rust = Mat("V4_RustyMetal");

            // Facing people walking from the lobby centre towards the passage.
            var face = new Vector3(-6, 0, 72) - SchedulePostBase; face.y = 0; face.Normalize();
            Remove(lobby, "StageSchedulePostBoard");
            var g = Group(lobby, "StageSchedulePostBoard", SchedulePostBase); g.rotation = Quaternion.LookRotation(face); // local +z = front
            var rng = new System.Random(31);
            float R(float a) => ((float)rng.NextDouble() * 2 - 1) * a;

            // Two weathered posts, leaning a touch, wedged at the foot with broken brick and a scrap plank.
            foreach (float x in new[] { -1.42f, 1.42f })
            {
                LBox(g, "Post", new Vector3(x, 1.6f, -.14f), new Vector3(.14f, 3.2f, .14f), wood, true, new Vector3(R(1.2f), R(4), R(1.5f)));
                LBox(g, "PostFootWedge", new Vector3(x + R(.1f), .08f, -.02f), new Vector3(.3f, .16f, .22f), Mat("V4_RustyMetal"), false, new Vector3(0, R(30), R(8)));
                LBox(g, "PostCapRot", new Vector3(x, 3.22f, -.14f), new Vector3(.16f, .05f, .16f), dark);
            }
            // Backboard of vertical planks with a missing one, a split one and a few uneven ends.
            int planks = 13; float bw = 2.7f, pw = bw / planks;
            for (int i = 0; i < planks; i++)
            {
                if (i == 10) continue; // fallen out
                float x = -bw * .5f + pw * (i + .5f), h = 2.05f + R(.06f), y = 1.92f + R(.03f);
                LBox(g, "BackPlank", new Vector3(x, y, -.1f), new Vector3(pw - .012f, h, .04f), i % 3 == 1 ? dark : wood, false, new Vector3(0, 0, i == 4 ? 1.8f : R(.6f)));
            }
            LBox(g, "FallenPlank", new Vector3(1.75f, .03f, .35f), new Vector3(pw - .012f, .04f, 1.95f), wood, false, new Vector3(0, 28, 2));
            LBox(g, "BackboardCollider", new Vector3(0, 1.92f, -.1f), new Vector3(bw, 2.05f, .06f), wood, true).GetComponent<Renderer>().enabled = false;
            // Rails behind, a cross brace below, a little peaked rain cap with one end sagging.
            foreach (float y in new[] { 1.05f, 2.8f }) LBox(g, "BackRail", new Vector3(0, y, -.14f), new Vector3(2.98f, .1f, .05f), dark);
            LRod(g, "CrossBrace", new Vector3(-1.38f, .25f, -.16f), new Vector3(1.38f, .95f, -.16f), .035f, wood);
            LRod(g, "CrossBrace", new Vector3(-1.38f, .95f, -.16f), new Vector3(1.38f, .25f, -.16f), .035f, wood);
            LBox(g, "RainCap", new Vector3(-.78f, 3.18f, -.02f), new Vector3(1.62f, .04f, .42f), dark, false, new Vector3(-12, 0, 6));
            LBox(g, "RainCap", new Vector3(.78f, 3.16f, -.02f), new Vector3(1.62f, .04f, .42f), dark, false, new Vector3(-12, 0, -9));
            // Header plank behind the "스케줄표" lettering, painted once, now peeling.
            LBox(g, "HeaderPlank", new Vector3(0, 2.92f, -.06f), new Vector3(2.5f, .4f, .04f), sign, false, new Vector3(0, 0, R(1)));
            // Rusty nails and a bent hook with a frayed rope where something used to hang.
            foreach (var p in new[] { new Vector3(-1.2f, 2.65f, -.07f), new Vector3(1.2f, 2.65f, -.07f), new Vector3(-1.2f, 1.2f, -.07f), new Vector3(1.2f, 1.2f, -.07f), new Vector3(-.6f, 2.92f, -.03f), new Vector3(.6f, 2.92f, -.03f) })
                LBox(g, "RustNail", p, new Vector3(.025f, .025f, .03f), rust);
            LRod(g, "BentHook", new Vector3(1.3f, .7f, -.07f), new Vector3(1.32f, .62f, .02f), .012f, iron);
            LRod(g, "FrayedRope", new Vector3(1.32f, .62f, .02f), new Vector3(1.36f, .2f, .05f), .01f, Mat("V4_Wood_Old"));

            // Carry the board, its frame and the close-up camera over as one piece: board centre to the backboard front,
            // board front (the side the close-up looks from) turned to face the path.
            Vector3 oldC = board.position, oldFront = -board.forward; oldFront.y = 0; oldFront.Normalize();
            var newC = g.TransformPoint(new Vector3(0, 1.9f, 0));
            var turn = Quaternion.FromToRotation(oldFront, face);
            foreach (var t in new[] { root, frame, cam })
            {
                Undo.SetTransformParent(t, gameplay, "Schedule to lobby");
                Undo.RecordObject(t, "Move schedule");
                t.SetPositionAndRotation(newC + turn * (t.position - oldC), turn * t.rotation);
            }
            // The bronze frame becomes a dark wood frame to suit the post board.
            var fr = frame.GetComponent<Renderer>(); if (fr != null) { Undo.RecordObject(fr, "Wood frame"); fr.sharedMaterial = dark; }
            // The "스케줄표" lettering was switched off while the board sat behind the dorm wall; it reads on the header plank now.
            var label = root.Cast<Transform>().FirstOrDefault(t => t.GetComponent<TextMesh>() != null);
            if (label != null) { Undo.RecordObject(label.gameObject, "Show label"); label.gameObject.SetActive(true); var tm = label.GetComponent<TextMesh>(); var text = tm.text; tm.text = ""; tm.text = text; }
            Physics.SyncTransforms();

            // Path into the stage passage stays open; the board can be reached from the path side.
            int blocked = 0, samples = 0;
            for (float z = 72f; z >= 62f; z -= .5f) foreach (float x in new[] { -8.5f, -6f, -3.5f })
            { samples++; var p = new Vector3(x, 0, z); if (Physics.CheckCapsule(p + Vector3.up * .35f, p + Vector3.up * 1.4f, .3f, ~0, QueryTriggerInteraction.Ignore)) blocked++; }
            var stand = board.position + (-board.forward) * 1.2f; stand.y = 0;
            bool standFree = !Physics.CheckCapsule(stand + Vector3.up * .35f, stand + Vector3.up * 1.4f, .3f, ~0, QueryTriggerInteraction.Ignore);
            return $"Schedule board at {board.position} facing {(-board.forward)}; close-up at {cam.position}; path blocked {blocked}/{samples}; reading spot free {standFree}";
        });

        // Side guards on every open steel stair players climb: the generated mezzanine flights and the lobby's kept stair.
        public static string JumpableStairGuards() => RunPass("Jumpable stair guards", target =>
        {
            int ramps = 0;
            foreach (var ramp in target.GetComponentsInChildren<Transform>(true).Where(t => t.name == "StairRamp" && t.GetComponent<BoxCollider>() != null).ToArray())
            { AddRampGuards(ramp, ramp.GetComponent<BoxCollider>().size.x * .5f); ramps++; }

            // Kept lobby stair: a guard on one side only, 1.2 m tall. Both sides now, at jump height, on flights and landing.
            var kept = target.Find("S01_Lobby/Frame8RetainedFactory/SteelStair"); int keptGuards = 0;
            if (kept != null)
            {
                foreach (var old in kept.Cast<Transform>().Where(t => t.name is "FlightGuard" or "RailGuard").ToArray()) Undo.DestroyObjectImmediate(old.gameObject);
                foreach (var flight in kept.Cast<Transform>().Where(t => t.name == "FlightRamp").ToArray())
                {
                    var box = flight.GetComponent<BoxCollider>(); float hw = flight.localScale.x * box.size.x * .5f;
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var g = new GameObject("FlightGuard"); Undo.RegisterCreatedObjectUndo(g, "Stair guard");
                        g.transform.SetParent(kept, false); g.transform.localRotation = flight.localRotation;
                        float surface = flight.localScale.y * box.size.y * .5f;
                        g.transform.localPosition = flight.localPosition + flight.localRotation * new Vector3(side * hw, surface + StairGuardHeight * .5f, 0);
                        g.AddComponent<BoxCollider>().size = new Vector3(.06f, StairGuardHeight, flight.localScale.z * box.size.z);
                        keptGuards++;
                    }
                }
                var landing = kept.Find("Landing");
                if (landing != null)
                {
                    float top = landing.localPosition.y + landing.localScale.y * .5f, hw = landing.localScale.x * .5f;
                    foreach (float side in new[] { -1f, 1f }) { FlatGuard(kept, "RailGuard", new Vector3(landing.localPosition.x + side * hw, top, landing.localPosition.z), new Vector3(.06f, 0, landing.localScale.z)); keptGuards++; }
                }
            }
            return $"{ramps} generated flights guarded both sides, kept lobby stair {keptGuards} guards; guard height {StairGuardHeight} m";
        });
    }
}
