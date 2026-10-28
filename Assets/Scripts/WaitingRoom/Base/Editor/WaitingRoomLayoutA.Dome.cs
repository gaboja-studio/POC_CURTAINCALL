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
        // Round domed lobby (user decision 2026-10-28): 42 m across, 10 m drum, 16 m crown. Its east edge meets the
        // fixed stage approach and its south edge the logistics hall openings; the shop, expert lift, dormitory and
        // restroom sit inside it. Every way out is a short corridor so rooms can later be re-placed freely.
        static readonly Vector2 DomeC = new(-6f, 77.4f);
        const float DomeR = 21f, DomeWallH = 10f, DomeTopH = 16f, DomeWallT = .5f, LiftAngle = 15f;
        static readonly Vector3 DormRoot0 = new(10.7f, 0, 87.85f), DormShift = new(-12.75f, 0, -.1f);
        static readonly Vector3 PropRoot0 = new(12.5f, 0, 60.7f), PropShift = new(6f, 0, 0);
        static readonly Vector3 WorkshopRoot0 = new(-19.18f, 0, 86.88f), WorkshopShift = new(-24f, 0, -17.5f);
        static readonly Vector3 LoungeRoot0 = new(-1.53f, 0, 59.4f), LoungeShift = new(-15.5f, 0, 8.7f);
        static readonly Vector3 LiftRoot0 = new(14.82f, 0, 79.3f);

        // A straight corridor along O + u t (side offset s across it). Each end is either a circle wall or a fixed t.
        sealed class Passage
        {
            public string Name, Label; public Vector2 O, U; public float W, H;
            public Vector2? StartC; public float StartR, StartT; // leaves the start circle (exit side) or starts at StartT
            public Vector2? EndC; public float EndR, EndT;       // enters the end circle (near side) or ends at EndT
            public bool Lamp = true;
            public Vector2 N => new(-U.y, U.x);
            public float T0(float s) => StartC is Vector2 c ? Root(c, StartR, s, true) : StartT;
            public float T1(float s) => EndC is Vector2 c ? Root(c, EndR, s, false) : EndT;
            float Root(Vector2 c, float r, float s, bool larger)
            {
                var p = O + N * s - c; float b = Vector2.Dot(U, p), k = p.sqrMagnitude - r * r, d = b * b - k;
                d = Mathf.Sqrt(Mathf.Max(0, d)); return larger ? -b + d : -b - d;
            }
            public Vector2 At(float s, float t) => O + N * s + U * t;
            // A point of a circular wall lies in this opening when it is inside the band and on the passage's side.
            public bool Cuts(Vector2 c, Vector2 p, float pad = 0)
            {
                bool start = StartC is Vector2 sc && (sc - c).sqrMagnitude < .01f, end = EndC is Vector2 ec && (ec - c).sqrMagnitude < .01f;
                bool through = StartC == null && EndC == null;
                if (!start && !end && !through) return false;
                if (Mathf.Abs(Vector2.Dot(p - O, N)) > W * .5f + pad) return false;
                float side = Vector2.Dot(p - c, U);
                return start ? side > 0 : end ? side < 0 : Vector2.Dot(p - O, U) > -.5f && Vector2.Dot(p - O, U) < EndT + .5f;
            }
        }

        static List<Passage> LobbyPassages() => new()
        {
            new() { Name = "Stage", Label = "", O = new(-6f, 70.5f), U = Vector2.right, W = 9f, H = 9.2f, StartC = DomeC, StartR = DomeR, EndT = 20.75f, Lamp = false },
            new() { Name = "Logistics11", Label = "11 무대 장치", O = new(-8.65f, 77.4f), U = Vector2.down, W = 9.1f, H = 9f, StartC = DomeC, StartR = DomeR, EndT = 21.55f, Lamp = false },
            // Narrower than the hall opening (x -1.5..6.4) so its east wall clears the prop room passage; the rest is infilled.
            new() { Name = "Logistics12", Label = "12 화물 적재", O = new(1.8f, 77.4f), U = Vector2.down, W = 6.6f, H = 9f, StartC = DomeC, StartR = DomeR, EndT = 21.55f, Lamp = false },
            new() { Name = "Stairs", Label = "계단 · B1", O = new(-6f, 79.95f), U = Vector2.left, W = 3.6f, H = 4.2f, StartC = DomeC, StartR = DomeR, EndT = 21.8f },
            new() { Name = "Workshop", Label = "10 작업실", O = new(-6f, 71f), U = Vector2.left, W = 4.2f, H = 4.2f, StartC = DomeC, StartR = DomeR, EndT = 28.55f },
            new() { Name = "PropRoom", Label = "04 소품실", O = new(-6f, 61.3f), U = Vector2.right, W = 3.6f, H = 4.2f, StartC = DomeC, StartR = DomeR, EndT = 18.45f },
            new() { Name = "Lift", Label = "", O = DomeC, U = new(Mathf.Cos(LiftAngle * Mathf.Deg2Rad), Mathf.Sin(LiftAngle * Mathf.Deg2Rad)), W = 3.8f, H = 4.2f, StartC = DomeC, StartR = DomeR, EndT = DomeR + .14f, Lamp = false },
            new() { Name = "Practice", Label = "09 연습실", O = new(-20f, 77.4f), U = Vector2.up, W = 4.4f, H = 5f, StartC = DomeC, StartR = DomeR, EndC = PracC, EndR = PracR },
            // Behind the restroom's breakable wall: a low pipe crawl through the drum to the pipe zone.
            new() { Name = "HiddenPipeCrawl", Label = "", O = new(5.35f, 88.85f), U = Vector2.right, W = 1.9f, H = 2.4f, StartT = 0, EndT = 12.75f, Lamp = false },
        };

        public static string BuildDomeLobby()
        {
            var (scene, target) = Begin(); var original = Snapshot(source); int undo = StartUndo("Round domed lobby");
            try
            {
                var lobby = target.Find("S01_Lobby");
                RelayoutAroundDome(target);
                foreach (var n in new[] { "Shell", "Overhead", "Structure", "FutureConnections" }) MuteFacility(lobby.Find(n));
                var frontage = target.Find("FacilityConnections/LogisticsFrontages");
                foreach (var t in frontage.Cast<Transform>().Where(t => t.name is "LobbyEastInfill" or "ExpertOriginalHeader" or "LobbySouthInfill").ToArray()) MuteFacility(t);
                Remove(lobby, "Dome"); var g = Group(lobby, "Dome", W(0, 0));
                var passages = LobbyPassages();
                BuildRoundHall(g, "LA_Dome", DomeC, DomeR, DomeWallH, DomeTopH, passages, 16, true);
                foreach (var p in passages) BuildPassage(g, p);
                Box(g, "HallOpeningInfill", new Vector3(5.75f, 4.5f, 55.82f), new Vector3(1.3f, 9f, .3f), Mat("LA_WallConcrete"), true);
                // Lobby leftovers outside the drum, inside the dorm block or across a corridor mouth go.
                var dorm = Rect.MinMaxRect(-17.6f, 83.4f, 5.6f, 95.2f);
                foreach (var group in new[] { "Dressing", "FloorWear", "Lighting" })
                {
                    var gr = lobby.Find(group); if (gr == null) continue;
                    foreach (Transform t in gr.Cast<Transform>().ToArray())
                    {
                        var rs = t.GetComponentsInChildren<Renderer>(); Vector3 c = rs.Length > 0 ? Bounds(t).center : t.position;
                        var xz = new Vector2(c.x, c.z);
                        if ((xz - DomeC).magnitude > DomeR - .8f || dorm.Contains(xz) || passages.Any(p => InPassageMouth(p, xz))) MuteFacility(t);
                    }
                }
                // Room shells were only ever seen from inside (single-sided walls, ceiling facing down). Inside the dome the
                // dorm and restroom are seen from the plaza and from a high camera, so give them outer faces and a roof.
                foreach (var path in new[] { "S02_Dormitory/Dormitory", "S02_Dormitory/Restroom" }) SkinRoomExterior(target.Find(path), Group(g, "Skin_" + path.Split('/')[1], g.position));
                // The dorm door faces the plaza; its sign replaces the old lobby portal sign. The schedule board already hangs on that wall.
                FacilityLabel(g, "02 숙소 · 화장실", new Vector3(DormDoorX + DormShift.x, 4.75f, W(0, DormZ0).z + DormShift.z - .56f), Quaternion.identity, .06f);
                // Lighting: a ring of caged lamps under the ribs plus a key light over the plaza.
                for (int i = 0; i < 6; i++)
                {
                    float a = (i * 60f + 30f) * Mathf.Deg2Rad; var p = DomeC + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 11f;
                    if (dorm.Contains(p)) p = DomeC + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 5f;
                    TrimCord(CagedLamp(g, new Vector3(p.x, 6.8f, p.y), 1.6f, WhiteLight), DomeHeightAt((p - DomeC).magnitude, DomeR, DomeWallH, DomeTopH));
                }
                Point(g, "PlazaKey", new Vector3(DomeC.x, 11f, DomeC.y - 6f), WhiteLight, 3.5f, 26f, LightShadows.Soft);
                var area = target.Find("ExplorationTimer/WaitingRoomArea");
                if (area != null)
                {
                    FitBox(area.Find("Area_Lobby"), DomeC.x - DomeR, DomeC.x + DomeR, DomeC.y - DomeR, DomeC.y + DomeR, -1, 7);
                    MuteFacility(area.Find("Area_LobbyEast"));
                    var prop = target.Find("S04_PropRoom/Shell/WallSurfaces"); if (prop != null) { var b = prop.GetComponent<Renderer>().bounds; FitBox(area.Find("Area_PropRoom"), b.min.x, b.max.x, b.min.z, b.max.z, -1, 7); }
                }
                Physics.SyncTransforms(); AssertUnchanged(original);
                Undo.CollapseUndoOperations(undo); EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
                return $"Dome lobby: {DomeR * 2} m, passages={passages.Count}";
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        // Visible boxes over each wall collider, pulled 2 cm off the interior face so they never fight the inner wall
        // surface, plus a concrete roof slab above the ceiling.
        static void SkinRoomExterior(Transform room, Transform g)
        {
            if (room == null || !room.gameObject.activeInHierarchy) return;
            var inner = room.Find("Shell/WallSurfaces").GetComponent<Renderer>().bounds;
            foreach (var c in room.Find("Shell").GetComponentsInChildren<Collider>().Where(c => c.name is "WallCollider" or "DoorHeaderCollider"))
            {
                var b = c.bounds; var min = b.min; var max = b.max;
                if (Mathf.Abs(max.x - inner.min.x) < .05f) max.x -= .02f; if (Mathf.Abs(min.x - inner.max.x) < .05f) min.x += .02f;
                if (Mathf.Abs(max.z - inner.min.z) < .05f) max.z -= .02f; if (Mathf.Abs(min.z - inner.max.z) < .05f) min.z += .02f;
                var size = max - min; var centre = (min + max) * .5f;
                if (size.x < .01f || size.z < .01f) continue;
                Box(g, "OuterWall", centre, size, Mat("LA_WallConcrete"));
            }
            Box(g, "RoofSlab", new Vector3(inner.center.x, inner.max.y + .35f, inner.center.z), new Vector3(inner.size.x + 1f, .3f, inner.size.z + 1f), Mat("LA_WallConcrete"));
        }

        static bool InPassageMouth(Passage p, Vector2 xz)
        {
            var d = xz - p.O; float s = Vector2.Dot(d, p.N), t = Vector2.Dot(d, p.U);
            return Mathf.Abs(s) < p.W * .5f + .8f && t > p.T0(0) - 3f && t < p.T1(0) + .5f;
        }

        // Rooms that the drum now covers move out (prop room, workshop) or in (dorm block, lounge); the lift turns to face the plaza.
        static void RelayoutAroundDome(Transform target)
        {
            void Place(string path, Vector3 to) { var t = target.Find(path); if (t != null && (t.position - to).sqrMagnitude > 1e-6f) MoveFacility(t, to); }
            Place("S02_Dormitory", DormRoot0 + DormShift);
            Place("S04_PropRoom", PropRoot0 + PropShift);
            Place("S10_Workshop", WorkshopRoot0 + WorkshopShift);
            Place("LG_Lounge", LoungeRoot0 + LoungeShift);
            var lift = target.Find("S08_ElevatorHall/FreightLift");
            if (lift != null)
            {
                float a = LiftAngle * Mathf.Deg2Rad; var p = DomeC + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (DomeR + .12f);
                Undo.RecordObject(lift, "Turn lift to the plaza"); lift.SetPositionAndRotation(new Vector3(p.x, 0, p.y), Quaternion.Euler(0, -LiftAngle, 0));
            }
            // The rectangular lobby's own corridors are replaced by the dome's short passages.
            MuteFacility(target.Find("WC_WestCorridor"));
            MuteFacility(target.Find("S09_PracticeRoom/Corridor"));
            MuteFacility(target.Find("S09_PracticeRoom/Arena"));
            // The workshop's old door onto the west corridor now opens onto nothing: close it.
            var ws = target.Find("S10_Workshop"); Remove(ws, "DomeClosures"); var close = Group(ws, "DomeClosures", ws.position);
            var south = ws.GetComponentsInChildren<Collider>().Where(c => c.name == "DoorReveal" && c.bounds.center.z < ws.position.z - 3f).Select(c => c.bounds).ToArray();
            if (south.Length == 2) { var b = south[0]; b.Encapsulate(south[1]); Box(close, "SealedDoorway", b.center, new Vector3(b.size.x, b.size.y, .3f), Mat("LA_WallConcrete"), true); }
            // Only the schedule board stays on the old north wall line, which is now the dorm's south wall face.
        }

        static float DomeHeightAt(float r, float R, float wallH, float topH)
        {
            float h = topH - wallH, rs = (R * R + h * h) / (2 * h), y0 = topH - rs;
            return r >= R ? wallH : y0 + Mathf.Sqrt(rs * rs - r * r);
        }

        // Floor disc, drum wall with openings cut for the passages, spherical cap, steel ring beam, ribs and pilasters.
        static Transform BuildRoundHall(Transform g, string prefix, Vector2 c, float R, float wallH, float topH, List<Passage> passages, int ribs, bool ledger)
        {
            var floorMat = Mat("LA_FloorConcrete"); var lower = Mat("LA_WallConcrete"); var upper = CinderMaterial(false); var steel = Mat("LA_SteelPlate"); var dark = Mat("V4_Iron_Dark");
            const int seg = 160;
            // Floor: concentric rings (world-scale UVs come from MeshBuilder).
            var fb = new MeshBuilder(prefix + "Floor", 1);
            int rings = Mathf.CeilToInt(R / 3f);
            for (int k = 0; k < rings; k++)
            {
                float r0 = R * k / rings, r1 = R * (k + 1) / rings + (k == rings - 1 ? DomeWallT : 0);
                for (int i = 0; i < seg; i++)
                {
                    float a0 = i * Mathf.PI * 2 / seg, a1 = (i + 1) * Mathf.PI * 2 / seg;
                    Vector3 P(float r, float a) => new(c.x + Mathf.Cos(a) * r, 0, c.y + Mathf.Sin(a) * r);
                    fb.Quad(0, P(r0, a0), P(r1, a0), P(r1, a1), P(r0, a1), Vector3.up, 3.5f);
                }
            }
            var floor = MeshObject(g, "RoundFloor", Vector3.zero, SaveMesh(fb.Build()), floorMat);
            floor.AddComponent<MeshCollider>().sharedMesh = floor.GetComponent<MeshFilter>().sharedMesh;
            // Drum wall.
            var wb = new MeshBuilder(prefix + "Wall", 2); var cols = Group(g, "WallColliders", g.position);
            float ledgerY = Mathf.Min(4.6f, wallH * .45f);
            Vector2 On(float a, float r) => c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            void Piece(Vector2 a, Vector2 b, float y0, float y1)
            {
                if (y1 - y0 < .01f || (a - b).sqrMagnitude < 1e-6f) return;
                Vector2 ia = a, ib = b, oa = c + (a - c).normalized * (R + DomeWallT), ob = c + (b - c).normalized * (R + DomeWallT);
                var inN = -(new Vector3((a + b).x * .5f - c.x, 0, (a + b).y * .5f - c.y)).normalized;
                foreach (var (lo, hi, sub) in new[] { (y0, Mathf.Min(y1, ledgerY), 0), (Mathf.Max(y0, ledgerY), y1, 1) })
                {
                    if (hi - lo < .01f) continue;
                    wb.Quad(sub, new Vector3(ia.x, lo, ia.y), new Vector3(ia.x, hi, ia.y), new Vector3(ib.x, hi, ib.y), new Vector3(ib.x, lo, ib.y), inN, 4f);
                    wb.Quad(sub, new Vector3(oa.x, lo, oa.y), new Vector3(oa.x, hi, oa.y), new Vector3(ob.x, hi, ob.y), new Vector3(ob.x, lo, ob.y), -inN, 4f);
                }
                wb.Quad(1, new Vector3(ia.x, y1, ia.y), new Vector3(oa.x, y1, oa.y), new Vector3(ob.x, y1, ob.y), new Vector3(ib.x, y1, ib.y), Vector3.up, 4f);
                if (y0 > .01f) wb.Quad(1, new Vector3(ia.x, y0, ia.y), new Vector3(oa.x, y0, oa.y), new Vector3(ob.x, y0, ob.y), new Vector3(ib.x, y0, ib.y), Vector3.down, 4f);
                var mid = (a + b) * .5f + (mid0(a, b) - c).normalized * DomeWallT * .5f;
                var col = new GameObject("WallCollider"); col.transform.SetParent(cols, false);
                col.transform.SetPositionAndRotation(new Vector3(mid.x, (y0 + y1) * .5f, mid.y), Quaternion.LookRotation(new Vector3(b.x - a.x, 0, b.y - a.y)));
                col.AddComponent<BoxCollider>().size = new Vector3(DomeWallT, y1 - y0, (a - b).magnitude + .05f);
            }
            static Vector2 mid0(Vector2 a, Vector2 b) => (a + b) * .5f;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2 / seg, a1 = (i + 1) * Mathf.PI * 2 / seg;
                Vector2 A = On(a0, R), B = On(a1, R);
                var hit = passages.FirstOrDefault(p => p.Cuts(c, A) || p.Cuts(c, B) || p.Cuts(c, (A + B) * .5f));
                if (hit == null) { Piece(A, B, 0, wallH); continue; }
                bool ia = hit.Cuts(c, A), ib = hit.Cuts(c, B);
                // Split the chord where it crosses the opening's edge line so the wall stops exactly at the corridor side.
                float SA(Vector2 p) => Vector2.Dot(p - hit.O, hit.N);
                Vector2 Cross(Vector2 p, Vector2 q) { float sp = SA(p), sq = SA(q), e = Mathf.Abs(sp) > Mathf.Abs(sq) ? Mathf.Sign(sp) * hit.W * .5f : Mathf.Sign(sq) * hit.W * .5f; float t = Mathf.Clamp01((e - sp) / (sq - sp)); return Vector2.Lerp(p, q, t); }
                if (ia && ib) { Piece(A, B, hit.H, wallH); continue; }
                var X = Cross(A, B);
                if (ia) { Piece(A, X, hit.H, wallH); Piece(X, B, 0, wallH); } else { Piece(A, X, 0, wallH); Piece(X, B, hit.H, wallH); }
            }
            var wall = MeshObject(g, "DrumWall", Vector3.zero, SaveMesh(wb.Build()), lower, upper);
            // Cap: spherical shell from the drum top to the crown, panelled steel seen from below.
            float h = topH - wallH, rs = (R * R + h * h) / (2 * h), cy = topH - rs, phi0 = Mathf.Asin(R / rs);
            var cb = new MeshBuilder(prefix + "Cap", 1); const int capRings = 10;
            Vector3 S(float phi, float a) => new(c.x + Mathf.Cos(a) * rs * Mathf.Sin(phi), cy + rs * Mathf.Cos(phi), c.y + Mathf.Sin(a) * rs * Mathf.Sin(phi));
            for (int k = 0; k < capRings; k++)
            {
                float p0 = phi0 * (capRings - k) / capRings, p1 = phi0 * (capRings - k - 1) / capRings;
                for (int i = 0; i < seg / 2; i++)
                {
                    float a0 = i * Mathf.PI * 4 / seg, a1 = (i + 1) * Mathf.PI * 4 / seg;
                    var n = -(S((p0 + p1) * .5f, (a0 + a1) * .5f) - new Vector3(c.x, cy, c.y)).normalized;
                    cb.Quad(0, S(p0, a0), S(p1, a0), S(p1, a1), S(p0, a1), n, 4f);
                }
            }
            var cap = MeshObject(g, "DomeCap", Vector3.zero, SaveMesh(cb.Build()), steel);
            cap.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
            // Ring beam on the drum, a ledger band, radial ribs up to a crown ring, and pilasters between the openings.
            for (int i = 0; i < 64; i++)
            {
                float a0 = i * Mathf.PI * 2 / 64, a1 = (i + 1) * Mathf.PI * 2 / 64; Vector2 A = On(a0, R - .2f), B = On(a1, R - .2f);
                var beam = Box(g, "RingBeam", new Vector3((A.x + B.x) * .5f, wallH - .2f, (A.y + B.y) * .5f), new Vector3(.3f, .4f, (A - B).magnitude + .02f), steel);
                beam.rotation = Quaternion.LookRotation(new Vector3(B.x - A.x, 0, B.y - A.y));
                if (!ledger) continue;
                Vector2 la = On(a0, R - .08f), lb = On(a1, R - .08f);
                if (passages.Any(p => p.Cuts(c, On((a0 + a1) * .5f, R), .3f) && p.H > ledgerY - .2f)) continue;
                var band = Box(g, "LedgerBand", new Vector3((la.x + lb.x) * .5f, ledgerY, (la.y + lb.y) * .5f), new Vector3(.16f, .24f, (la - lb).magnitude + .02f), dark);
                band.rotation = Quaternion.LookRotation(new Vector3(lb.x - la.x, 0, lb.y - la.y));
            }
            for (int r = 0; r < ribs; r++)
            {
                float a = r * Mathf.PI * 2 / ribs;
                for (int k = 0; k < capRings - 1; k++)
                {
                    float p0 = phi0 * (capRings - k) / capRings, p1 = phi0 * (capRings - k - 1) / capRings;
                    var A = S(p0, a); var B = S(p1, a); var inward = (new Vector3(c.x, cy, c.y) - (A + B) * .5f).normalized;
                    var rib = Box(g, "DomeRib", (A + B) * .5f + inward * .2f, new Vector3(.22f, .32f, (A - B).magnitude + .04f), steel);
                    rib.rotation = Quaternion.LookRotation(B - A, inward);
                }
                if (passages.Any(p => p.Cuts(c, On(a, R), .6f))) continue;
                var foot = On(a, R - .14f);
                var pil = Box(g, "DrumPilaster", new Vector3(foot.x, wallH * .5f, foot.y), new Vector3(.28f, wallH, .26f), steel);
                pil.rotation = Quaternion.LookRotation(new Vector3(c.x - foot.x, 0, c.y - foot.y));
            }
            var crown = S(phi0 / capRings, 0); float crownR = new Vector2(crown.x - c.x, crown.z - c.y).magnitude;
            for (int i = 0; i < 24; i++)
            {
                float a0 = i * Mathf.PI * 2 / 24, a1 = (i + 1) * Mathf.PI * 2 / 24; Vector2 A = On(a0, crownR), B = On(a1, crownR);
                Box(g, "CrownRing", new Vector3((A.x + B.x) * .5f, crown.y - .25f, (A.y + B.y) * .5f), new Vector3(.3f, .3f, (A - B).magnitude + .02f), steel).rotation = Quaternion.LookRotation(new Vector3(B.x - A.x, 0, B.y - A.y));
            }
            return g;
        }

        // Corridor: floor, both side walls, ceiling slab, colliders and (when long enough) a lamp and a sign at the plaza end.
        static void BuildPassage(Transform g, Passage p)
        {
            var pg = Group(g, "Passage_" + p.Name, g.position);
            float hw = p.W * .5f; float t0l = p.T0(-hw), t0r = p.T0(hw), t1l = p.T1(-hw), t1r = p.T1(hw);
            float tMin = Mathf.Min(t0l, t0r), tMax = Mathf.Max(t1l, t1r);
            if (tMax - tMin < .05f) return;
            var u3 = new Vector3(p.U.x, 0, p.U.y); var n3 = new Vector3(p.N.x, 0, p.N.y);
            Vector3 P(float s, float t, float y) { var v = p.At(s, t); return new Vector3(v.x, y, v.y); }
            var fb = new MeshBuilder("LA_Passage_" + p.Name, 1);
            fb.Quad(0, P(-hw, t0l, .006f), P(hw, t0r, .006f), P(hw, t1r, .006f), P(-hw, t1l, .006f), Vector3.up, 3.5f);
            fb.Quad(0, P(-hw, t0l, p.H), P(-hw, t1l, p.H), P(hw, t1r, p.H), P(hw, t0r, p.H), Vector3.down, 3.5f);
            MeshObject(pg, "PassageFloorCeiling", Vector3.zero, SaveMesh(fb.Build()), Mat("LA_FloorConcrete"));
            var floorCol = new GameObject("FloorCollider"); floorCol.transform.SetParent(pg, false);
            var mid = p.At(0, (tMin + tMax) * .5f); floorCol.transform.SetPositionAndRotation(new Vector3(mid.x, -.1f, mid.y), Quaternion.LookRotation(u3));
            floorCol.AddComponent<BoxCollider>().size = new Vector3(p.W + .4f, .2f, tMax - tMin + .6f);
            foreach (var (s, a, b) in new[] { (-hw - .1f, t0l, t1l), (hw + .1f, t0r, t1r) })
            {
                if (b - a < .05f) continue;
                var c = P(s, (a + b) * .5f, p.H * .5f);
                Box(pg, "PassageWall", c, new Vector3(.2f, p.H, b - a + .04f), Mat("LA_WallConcrete"), true).rotation = Quaternion.LookRotation(u3);
            }
            var top = P(0, (tMin + tMax) * .5f, p.H + .15f);
            Box(pg, "PassageRoof", top, new Vector3(p.W + .4f, .3f, tMax - tMin + .2f), Mat("LA_WallConcrete")).rotation = Quaternion.LookRotation(u3);
            if (p.Lamp && tMax - tMin > 2.5f) TrimCord(CagedLamp(pg, P(0, (tMin + tMax) * .5f, Mathf.Max(2.8f, p.H - 1.3f)), .5f, WhiteLight), p.H);
            if (!string.IsNullOrEmpty(p.Label))
            {
                var at = P(0, tMin + .02f, Mathf.Min(p.H + .45f, DomeWallH - .5f)) - u3 * .05f;
                FacilityLabel(pg, p.Label, at, Quaternion.LookRotation(u3), .06f);
            }
        }
    }
}
