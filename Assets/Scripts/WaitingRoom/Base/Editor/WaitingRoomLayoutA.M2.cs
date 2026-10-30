using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Editor
{
    // M2 (Task-079): night upkeep ends when everyone lies down in a dormitory bed, not from the schedule board.
    public static partial class WaitingRoomLayoutA
    {
        public static string AttachNightBeds() => RunPass("Night beds take the ready input", target =>
        {
            var beds = target.Find("S02_Dormitory/Dormitory/Beds") ?? throw new System.InvalidOperationException("Dormitory beds missing");
            int added = 0, kept = 0;
            foreach (Transform bed in beds.Cast<Transform>().Where(t => t.name.StartsWith("Bed_")))
            {
                if (bed.GetComponent<NightBed>() != null) { kept++; continue; }
                Undo.AddComponent<NightBed>(bed.gameObject); added++;
            }
            return $"Night beds: {added} added, {kept} already set";
        });

        // First inspectable clue for the overview (step 2): the former troupe's open record book in the junk storage.
        // Its wording is still to be written, so only the title is set.
        public static string AttachSampleClue() => RunPass("Sample clue on the troupe record book", target =>
        {
            var book = target.Find("S05_JunkStorage/AbandonedResearchStorage/FormerTroupeJournals/SM_Item_Book_Open_01")
                ?? throw new System.InvalidOperationException("Record book missing");
            var clue = book.GetComponent<ClueInspectable>() ?? Undo.AddComponent<ClueInspectable>(book.gameObject);
            var so = new SerializedObject(clue);
            so.FindProperty("title").stringValue = "이전 단원의 기록책";
            so.ApplyModifiedProperties();
            return $"Clue on {book.name} at {book.position}";
        });

        // ================= Step 3: crowbar breach in the restroom wall =================

        // Restroom-side opening of the old broken wall towards the pipe crawl (measured): x 15.25–16.85, y 0–4.2, wall z 62.05–62.55.
        const float BreachX0 = 15.25f, BreachX1 = 16.85f, BreachZ0 = 62.05f, BreachZ1 = 62.55f, BreachTop = 4.2f, TileTop = 2.2f;

        // A wall filling the opening that a crowbar (stake puller) breaks with six F hits. Blocks carry the restroom's own
        // tile below 2.2 m and concrete above at the restroom wall's texel density, so the patch reads as the same wall.
        public static string BuildRestroomBreach() => RunPass("Restroom breach wall", target =>
        {
            var restroom = target.Find("S02_Dormitory/Restroom") ?? throw new System.InvalidOperationException("Restroom missing");
            Remove(restroom, "BreachWall");
            float cx = (BreachX0 + BreachX1) * .5f, cz = (BreachZ0 + BreachZ1) * .5f, thick = BreachZ1 - BreachZ0 - .04f;
            var root = Group(restroom, "BreachWall", new Vector3(cx, 0, cz)); root.rotation = Quaternion.identity;
            Material concrete = Mat("LA_WallConcrete"), tile = LayoutMat("LA_TealTileDark") ?? Mat("LA_TealTile");

            string meshPath = AssetDir + "/LA_RestroomBreachWall.asset";
            AssetDatabase.DeleteAsset(meshPath);
            Mesh bank = null;
            void Save(Mesh m) { if (bank == null) { AssetDatabase.CreateAsset(m, meshPath); bank = m; } else AssetDatabase.AddObjectToAsset(m, bank); }

            // 4 columns; rows meet the tile line at 2.2 m (5 × 0.44 below, 5 × 0.4 above).
            var rows = Enumerable.Range(0, 5).Select(i => (i * .44f, (i + 1) * .44f)).Concat(Enumerable.Range(0, 5).Select(i => (TileTop + i * .4f, TileTop + (i + 1) * .4f))).ToArray();
            int cols = 4; float colW = (BreachX1 - BreachX0) / cols;
            var pieces = new System.Collections.Generic.List<Transform>();
            var rng = new System.Random(78);
            for (int r = 0; r < rows.Length; r++)
            for (int c = 0; c < cols; c++)
            {
                float x0 = BreachX0 + c * colW, x1 = x0 + colW, y0 = rows[r].Item1, y1 = rows[r].Item2;
                var center = new Vector3((x0 + x1) * .5f, (y0 + y1) * .5f, cz);
                var size = new Vector3(colW, y1 - y0, thick); // no joints: looks exactly like the wall beside it until it starts to give
                var piece = new GameObject($"Block_{r}_{c}").transform; Undo.RegisterCreatedObjectUndo(piece.gameObject, "Breach block");
                piece.SetParent(root, false); piece.position = center;
                var core = WorldUvBox(center, size); core.name = $"BreachCore_{r}_{c}"; Save(core);
                piece.gameObject.AddComponent<MeshFilter>().sharedMesh = core;
                piece.gameObject.AddComponent<MeshRenderer>().sharedMaterial = concrete;
                // Restroom face skin, mapped like the surrounding restroom wall (tile 1.06 u/m · 0.832 v/m; concrete 0.427 · 0.336 from 2.2 m).
                bool isTile = y1 <= TileTop + .001f;
                var skin = RestroomSkin(center, size, isTile); skin.name = $"BreachSkin_{r}_{c}"; Save(skin);
                var face = new GameObject("RestroomFace").transform; Undo.RegisterCreatedObjectUndo(face.gameObject, "Breach skin");
                face.SetParent(piece, false); face.localPosition = Vector3.zero;
                face.gameObject.AddComponent<MeshFilter>().sharedMesh = skin;
                face.gameObject.AddComponent<MeshRenderer>().sharedMaterial = isTile ? tile : concrete;
                // The restroom's iron ledger band (y 2.08–2.32, 0.16 proud of the face) runs on across the patch.
                // The end columns also cover the 0.2 m where the hidden door posts interrupted the band.
                if (Mathf.Approximately(y0, TileTop))
                {
                    float left = c == 0 ? .2f : 0f, right = c == cols - 1 ? .2f : 0f;
                    BoxLocal(piece, "SteelLedger", new Vector3((right - left) * .5f, TileTop - center.y, (BreachZ1 + .08f) - cz), new Vector3(colW + left + right, .24f, .16f), Mat("V4_Iron_Dark"));
                }
                // A touch of rust on the restroom face (sits on each block, so it falls with it): streaks bleeding down from
                // the iron band, damp rust at the foot, faint patina on the concrete.
                Material rust = (r == 3 || r == 4) && c % 2 == 0 ? Faint(RustStreak(), .55f) : r == 0 || (r >= 6 && r <= 8 && (r + c) % 2 == 1) ? Faint(RustPatina(), .35f) : null;
                if (rust != null)
                {
                    bool streak = r == 3 || r == 4;
                    float sx = colW * (streak ? .55f + (float)rng.NextDouble() * .35f : .95f), sy = (y1 - y0) * (streak ? .6f + (float)rng.NextDouble() * .35f : .95f);
                    var stain = Prim(piece, PrimitiveType.Quad, "RustStain", piece.position, Vector3.one, rust);
                    stain.localPosition = new Vector3(((float)rng.NextDouble() - .5f) * (colW - sx) * .8f, r == 0 ? -(y1 - y0 - sy) * .5f : 0, thick * .5f + .006f);
                    stain.localRotation = Quaternion.Euler(0, 180, r == 3 || r == 4 ? 0 : rng.Next(-15, 15));
                    stain.localScale = new Vector3(sx, sy, 1);
                    stain.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                pieces.Add(piece);
            }

            var blocker = root.gameObject.AddComponent<BoxCollider>();
            blocker.center = new Vector3(0, BreachTop * .5f, 0); blocker.size = new Vector3(BreachX1 - BreachX0, BreachTop, BreachZ1 - BreachZ0);

            // Cracks: one per hit (five before it gives). Each is a spider web around the same impact point, larger each hit,
            // so the web grows outward from where the crowbar keeps landing. Both faces.
            AssetDatabase.DeleteAsset(AssetDir + "/LA_WallCrack.png"); // regenerate the web texture
            var crackMat = WallCrackMaterial();
            var cracks = new System.Collections.Generic.List<GameObject>();
            var spots = new[] { (0f, 1.3f, .4f), (.02f, 1.29f, .65f), (-.02f, 1.31f, .9f), (.01f, 1.3f, 1.15f), (0f, 1.3f, 1.5f) }; // largest stays inside the 1.6 m patch
            for (int i = 0; i < spots.Length; i++)
            {
                var (x, y, s) = spots[i];
                var crack = new GameObject($"Crack_{i + 1}").transform; Undo.RegisterCreatedObjectUndo(crack.gameObject, "Crack");
                crack.SetParent(root, false); crack.localPosition = new Vector3(x, y, 0);
                float spin = rng.Next(360);
                foreach (float side in new[] { 1f, -1f })
                {
                    var q = Prim(crack, PrimitiveType.Quad, side > 0 ? "RestroomSide" : "CrawlSide", crack.position, Vector3.one, crackMat);
                    q.localPosition = new Vector3(0, 0, side * ((BreachZ1 - BreachZ0) * .5f + .004f + i * .0006f));
                    q.localRotation = Quaternion.Euler(0, side > 0 ? 180 : 0, spin);
                    q.localScale = new Vector3(s, s, 1);
                    var qr = q.GetComponent<Renderer>(); qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                crack.gameObject.SetActive(false);
                cracks.Add(crack.gameObject);
            }

            // After it gives: extra broken concrete and tile shards on both sides of the doorway.
            var after = new GameObject("Aftermath").transform; Undo.RegisterCreatedObjectUndo(after.gameObject, "Aftermath");
            after.SetParent(root, false);
            for (int i = 0; i < 14; i++)
            {
                float side = i % 2 == 0 ? 1 : -1, s = .08f + (float)rng.NextDouble() * .14f;
                var p = Box(after, "Debris", root.TransformPoint(new Vector3(((float)rng.NextDouble() - .5f) * 1.8f, s * .5f, side * (.45f + (float)rng.NextDouble() * .9f))), new Vector3(s * 1.3f, s, s), i % 3 == 0 ? tile : Mat("LA_FloorConcrete"));
                p.rotation = Quaternion.Euler(rng.Next(30), rng.Next(360), rng.Next(30));
            }
            after.gameObject.SetActive(false);

            var wall = Undo.AddComponent<BreakableWall>(root.gameObject);
            var so = new SerializedObject(wall);
            so.FindProperty("targetId").stringValue = "restroom-breach";
            so.FindProperty("blocker").objectReferenceValue = blocker;
            var pp = so.FindProperty("pieces"); pp.arraySize = pieces.Count; for (int i = 0; i < pieces.Count; i++) pp.GetArrayElementAtIndex(i).objectReferenceValue = pieces[i];
            var cp = so.FindProperty("cracks"); cp.arraySize = cracks.Count; for (int i = 0; i < cracks.Count; i++) cp.GetArrayElementAtIndex(i).objectReferenceValue = cracks[i];
            so.FindProperty("aftermath").objectReferenceValue = after.gameObject;
            // Until it gives, nothing may mark this as a doorway: the old broken edges wait for the collapse, and the door
            // frame posts and lintel stop drawing (their colliders stay).
            var broken = restroom.Find("BrokenWall");
            var reveal = so.FindProperty("revealOnOpen"); reveal.arraySize = broken != null ? 1 : 0;
            if (broken != null) { reveal.GetArrayElementAtIndex(0).objectReferenceValue = broken.gameObject; Undo.RecordObject(broken.gameObject, "Hide broken edges"); broken.gameObject.SetActive(false); }
            int frames = 0;
            foreach (var r in restroom.Find("Shell/WallStructure").GetComponentsInChildren<Renderer>(true).Where(r => r.name is "DoorReveal" or "RevealLintel"))
            {
                var b = r.bounds; if (b.max.x < BreachX0 - .3f || b.min.x > BreachX1 + .3f || Mathf.Abs(b.center.z - cz) > .5f) continue;
                Undo.RecordObject(r, "Hide door frame"); r.enabled = false; frames++;
            }
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            // The crawl passage pieces poked 0.1 m through the restroom face and read as posts beside the patch; end them
            // just inside the face (stretching along z only, far end kept).
            int trimmed = 0;
            var crawl = target.Find("S01_Lobby/Frame8Lobby/Passage_F8_PipeCrawl");
            if (crawl != null)
                foreach (var r in crawl.GetComponentsInChildren<Renderer>())
                {
                    var b = r.bounds; const float end = BreachZ1 - .05f;
                    if (b.max.z <= end + .001f || b.min.z > BreachZ0) continue;
                    var t = r.transform; float k = (end - b.min.z) / b.size.z;
                    int axis = Enumerable.Range(0, 3).OrderByDescending(i => Mathf.Abs(Vector3.Dot(t.rotation * Axis(i), Vector3.forward))).First();
                    Undo.RecordObject(t, "Trim crawl at restroom face");
                    var ls = t.localScale; ls[axis] *= k; t.localScale = ls;
                    Physics.SyncTransforms(); var nb = r.bounds;
                    t.position += Vector3.forward * (b.min.z - nb.min.z);
                    trimmed++;
                }
            Physics.SyncTransforms();
            bool sealedNow = Physics.CheckCapsule(new Vector3(cx, .4f, cz), new Vector3(cx, 1.4f, cz), .3f, ~0, QueryTriggerInteraction.Ignore);
            return $"Breach wall: {pieces.Count} blocks, {cracks.Count} crack stages, door frame pieces hidden {frames}, crawl pieces trimmed {trimmed}, sealed {sealedNow}";
        });

        // A see-through copy of a decal material for light weathering ("살짝" rust), saved beside the generated assets.
        static Material Faint(Material source, float alpha)
        {
            string name = source.name + "Faint", path = $"{AssetDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(source) { name = name }; AssetDatabase.CreateAsset(m, path); }
            var c = m.GetColor("_BaseColor"); c.a = alpha; m.SetColor("_BaseColor", c);
            EditorUtility.SetDirty(m);
            return m;
        }

        // Restroom-facing quad (+z face) in the block's local frame with the restroom wall's UVs.
        static Mesh RestroomSkin(Vector3 center, Vector3 size, bool tile)
        {
            var h = size * .5f; var m = new Mesh();
            var local = new[] { new Vector3(-h.x, -h.y, h.z + .002f), new Vector3(-h.x, h.y, h.z + .002f), new Vector3(h.x, h.y, h.z + .002f), new Vector3(h.x, -h.y, h.z + .002f) };
            var uv = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                var w = local[i] + center;
                uv[i] = tile ? new Vector2(16.42f - (w.x - 16.85f) * 1.06f, w.y * .832f)
                             : new Vector2(6.57f - (w.x - 16.85f) * .427f, .73f + (w.y - TileTop) * .336f);
            }
            m.vertices = local; m.uv = uv; m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateNormals(); m.RecalculateBounds(); m.RecalculateTangents();
            return m;
        }

        // Dark branching cracks with chipped edges on a transparent square, radiating from the middle.
        static Material WallCrackMaterial() => DecalMaterial("LA_WallCrack", "LA_WallCrack.png", () =>
        {
            const int size = 512; var tex = new Texture2D(size, size, TextureFormat.RGBA32, false); var px = new Color32[size * size];
            var rng = new System.Random(5);
            void Dot(float x, float y, float r, float a)
            {
                int x0 = Mathf.FloorToInt(x - r), x1 = Mathf.CeilToInt(x + r), y0 = Mathf.FloorToInt(y - r), y1 = Mathf.CeilToInt(y + r);
                for (int yy = Mathf.Max(0, y0); yy <= Mathf.Min(size - 1, y1); yy++)
                for (int xx = Mathf.Max(0, x0); xx <= Mathf.Min(size - 1, x1); xx++)
                {
                    float d = Mathf.Sqrt((xx - x) * (xx - x) + (yy - y) * (yy - y)); if (d > r) continue;
                    byte alpha = (byte)Mathf.Clamp(a * (1 - d / r) * 255f, 0, 255); int k = yy * size + xx;
                    if (alpha > px[k].a) px[k] = new Color32(28, 24, 22, alpha);
                }
            }
            // Slightly wandering crack line between two points, thinning towards b.
            void Line(Vector2 a, Vector2 b, float w0, float w1)
            {
                float len = Vector2.Distance(a, b); var n = new Vector2(-(b - a).y, (b - a).x).normalized; float drift = 0;
                for (float t = 0; t <= len; t += 1.2f)
                {
                    float f = t / len; drift = Mathf.Clamp(drift + ((float)rng.NextDouble() - .5f) * .9f, -3f, 3f);
                    var p = Vector2.Lerp(a, b, f) + n * drift * Mathf.Sin(f * Mathf.PI);
                    Dot(p.x, p.y, Mathf.Lerp(w0, w1, f), .95f);
                }
            }
            // Spider-web fracture: jagged spokes out of the impact point, joined by sagging rings that thin outward.
            var c = new Vector2(size * .5f, size * .5f);
            const int spokes = 11, rings = 6; float maxR = size * .48f;
            var angles = new float[spokes]; var reach = new float[spokes];
            for (int i = 0; i < spokes; i++) { angles[i] = (i + ((float)rng.NextDouble() - .5f) * .55f) * Mathf.PI * 2 / spokes; reach[i] = maxR * (.72f + (float)rng.NextDouble() * .28f); }
            Vector2 On(int spoke, float radius) => c + new Vector2(Mathf.Cos(angles[spoke]), Mathf.Sin(angles[spoke])) * radius;
            for (int i = 0; i < spokes; i++)
            {
                // Each spoke kinks a little where it crosses a ring.
                var prev = c;
                for (int k = 1; k <= rings; k++)
                {
                    float r = reach[i] * k / rings; var next = On(i, r) + new Vector2((float)rng.NextDouble() - .5f, (float)rng.NextDouble() - .5f) * 6f;
                    Line(prev, next, 3.4f * (1 - (k - 1f) / rings) + .7f, 3.4f * (1 - (float)k / rings) + .6f);
                    prev = next;
                }
            }
            for (int k = 1; k < rings; k++)
            {
                float frac = (float)k / rings, ringW = Mathf.Lerp(2.4f, .7f, frac);
                for (int i = 0; i < spokes; i++)
                {
                    if (k > 2 && rng.NextDouble() < .18) continue; // outer rings break off here and there
                    int j = (i + 1) % spokes;
                    // Ring segments sag inward between spokes, like a real web of glass/plaster fractures.
                    var a = On(i, reach[i] * frac); var b = On(j, reach[j] * frac); var mid = (a + b) * .5f; var sag = c + (mid - c) * .9f;
                    Line(a, sag, ringW, ringW); Line(sag, b, ringW, ringW);
                }
            }
            for (int i = 0; i < 40; i++) Dot(c.x + ((float)rng.NextDouble() - .5f) * 34, c.y + ((float)rng.NextDouble() - .5f) * 34, 2 + (float)rng.NextDouble() * 5, .85f); // crushed impact point
            tex.SetPixels32(px); tex.Apply(); return tex;
        }, .05f, 3002);

        // The three exploration tools as found items on the lobby floor right in front of the spawn line (user request, for
        // trying them straight away). The workshop crowbar spot moves here; the crate and trunk keep their rewards.
        public static string PlaceLobbyToolSpots() => RunPass("Exploration tools in front of the lobby spawn", target =>
        {
            var lobby = target.Find("S01_Lobby");
            var workshop = target.Find("S10_Workshop"); if (workshop != null) Remove(workshop, "CrowbarSpot");
            Remove(lobby, "LobbyToolSpots");
            var group = Group(lobby, "LobbyToolSpots", Vector3.zero); group.rotation = Quaternion.identity;
            var tools = new[] { (ItemUses.StakePuller, "lobby-crowbar", 1, -6f), (ItemUses.MagicianHairpin, "lobby-hairpin", 2, -4f), (ItemUses.UvLamp, "lobby-uv-lamp", 3, -2f) };
            var report = new System.Text.StringBuilder();
            foreach (var (id, spotId, offset, x) in tools)
            {
                var at = FreeFloorNear(lobby, new Vector3(x, 0, 76.2f), .3f);
                var spot = Group(group, "Spot_" + id, at + Vector3.up * .02f); spot.rotation = Quaternion.identity;
                var s = Undo.AddComponent<FoundItemSpot>(spot.gameObject); var so = new SerializedObject(s);
                so.FindProperty("spotId").stringValue = spotId; so.FindProperty("uidOffset").intValue = offset;
                so.FindProperty("itemId").stringValue = id; so.FindProperty("itemName").stringValue = ItemCatalog.Get(id)?.displayName ?? id;
                so.ApplyModifiedProperties();
                Physics.SyncTransforms();
                report.Append($"{id} {at}; ");
            }
            return report.ToString();
        });

        // ================= Trial placements for the exploration tools (positions to be replaced by the layout plan) =================

        // Free floor (no 1 m × 1.7 m obstruction) nearest to `prefer` inside a room's floor collider bounds.
        static Vector3 FreeFloorNear(Transform room, Vector3 prefer, float half = .5f)
        {
            var floor = room.GetComponentsInChildren<Collider>().Where(c => c.name.Contains("Floor")).Select(c => c.bounds).DefaultIfEmpty(VisibleBounds(room)).First();
            Vector3 best = default; float bestD = float.MaxValue;
            for (float x = floor.min.x + .7f; x <= floor.max.x - .7f; x += .25f)
            for (float z = floor.min.z + .7f; z <= floor.max.z - .7f; z += .25f)
            {
                var p = new Vector3(x, floor.max.y, z);
                if (Physics.CheckBox(p + Vector3.up * .9f, new Vector3(half, .85f, half), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (!Physics.Raycast(p + Vector3.up * .5f, Vector3.down, out var hit, 1f, ~0, QueryTriggerInteraction.Ignore)) continue;
                float d = (p - prefer).sqrMagnitude; if (d < bestD) { bestD = d; best = hit.point; }
            }
            if (bestD == float.MaxValue) throw new System.InvalidOperationException("No free floor in " + room.name);
            return best;
        }

        static void SetRef(SerializedObject so, string field, Object value) => so.FindProperty(field).objectReferenceValue = value;
        static void SetRefs(SerializedObject so, string field, params GameObject[] values)
        {
            var p = so.FindProperty(field); p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        public static string PlaceExplorationSamples() => RunPass("Exploration tool trial placements", target =>
        {
            var report = new System.Text.StringBuilder();
            Material wood = Mat("V4_Wood_Old"), iron = Mat("V4_Iron_Dark"), brass = Mat("LA_Bronze");

            // 1) Junk storage: a padlocked footlocker. Hairpin: 50% per try, third try guaranteed. Inside: a found UV lamp.
            var junk = target.Find("S05_JunkStorage"); Remove(junk, "TrialPadlockedTrunk");
            var junkCentre = VisibleBounds(junk).center;
            var at = FreeFloorNear(junk, junkCentre + new Vector3(-2f, 0, -2f));
            var trunk = Group(junk, "TrialPadlockedTrunk", at); trunk.rotation = Quaternion.LookRotation(new Vector3(junkCentre.x - at.x, 0, junkCentre.z - at.z));
            LBox(trunk, "Body", new Vector3(0, .25f, 0), new Vector3(.9f, .5f, .55f), wood, true);
            foreach (float x in new[] { -.38f, .38f }) LBox(trunk, "Strap", new Vector3(x, .25f, 0), new Vector3(.05f, .52f, .57f), iron);
            var lid = Group(trunk, "LidHinge", trunk.TransformPoint(new Vector3(0, .5f, -.275f))); lid.localRotation = Quaternion.identity;
            LBox(lid, "Lid", new Vector3(0, .05f, .275f), new Vector3(.92f, .1f, .57f), wood, true);
            var padlock = Group(trunk, "Padlock", trunk.TransformPoint(new Vector3(0, .42f, .3f))); padlock.localRotation = Quaternion.identity;
            LBox(padlock, "LockBody", Vector3.zero, new Vector3(.09f, .1f, .04f), brass);
            LRod(padlock, "Shackle", new Vector3(-.03f, .05f, 0), new Vector3(-.03f, .11f, 0), .008f, iron);
            LRod(padlock, "Shackle", new Vector3(.03f, .05f, 0), new Vector3(.03f, .11f, 0), .008f, iron);
            LBox(padlock, "Hasp", new Vector3(0, .08f, -.02f), new Vector3(.08f, .02f, .03f), iron);
            var lamp = Group(trunk, "RewardPoint", trunk.TransformPoint(new Vector3(0, 0, .65f))); lamp.localRotation = Quaternion.identity;
            var lockT = Undo.AddComponent<ToolTarget>(trunk.gameObject); var so = new SerializedObject(lockT);
            so.FindProperty("targetId").stringValue = "trial-padlocked-trunk"; so.FindProperty("kind").enumValueIndex = (int)ToolTargetKind.HairpinLock;
            so.FindProperty("label").stringValue = "자물쇠 잠긴 트렁크";
            SetRef(so, "hinge", lid); so.FindProperty("openEuler").vector3Value = new Vector3(-105, 0, 0);
            SetRefs(so, "hideOnOpen", padlock.gameObject);
            so.FindProperty("rewardItemId").stringValue = ItemUses.UvLamp; SetRef(so, "rewardPoint", lamp); so.FindProperty("rewardUidOffset").intValue = 51;
            so.ApplyModifiedProperties();
            report.Append($"padlocked trunk at {at}; ");

            // 2) Workshop: a crate boarded shut with nailed planks. Stake puller (crowbar) tears them off. Inside: a found hairpin.
            var workshop = target.Find("S10_Workshop"); Remove(workshop, "TrialBoardedCrate");
            var wsCentre = VisibleBounds(workshop).center;
            var crowbarSpot = workshop.Find("CrowbarSpot");
            var cat = FreeFloorNear(workshop, (crowbarSpot != null ? crowbarSpot.position : wsCentre) + new Vector3(2.2f, 0, 0));
            var crate = Group(workshop, "TrialBoardedCrate", cat); crate.rotation = Quaternion.LookRotation(new Vector3(wsCentre.x - cat.x, 0, wsCentre.z - cat.z) + Vector3.forward * .001f);
            LBox(crate, "Crate", new Vector3(0, .4f, 0), new Vector3(.8f, .8f, .8f), wood, true);
            var planks = new System.Collections.Generic.List<GameObject>();
            for (int i = 0; i < 3; i++)
            {
                var plank = LBox(crate, "NailedPlank", new Vector3(0, .18f + i * .24f, .42f), new Vector3(.9f, .12f, .03f), Mat("Wood_Dark"));
                plank.localRotation = Quaternion.Euler(0, 0, (i - 1) * 6f);
                foreach (float x in new[] { -.4f, .4f }) LBox(plank, "Nail", new Vector3(x / .9f, 0, .7f), new Vector3(.02f / .9f, .2f, .5f), iron);
                planks.Add(plank.gameObject);
            }
            var torn = new GameObject("TornPlanks").transform; Undo.RegisterCreatedObjectUndo(torn.gameObject, "Torn planks"); torn.SetParent(crate, false);
            for (int i = 0; i < 3; i++) { var p = LBox(torn, "TornPlank", new Vector3(-.2f + i * .25f, .02f + i * .02f, .75f + i * .08f), new Vector3(.9f, .03f, .12f), Mat("Wood_Dark")); p.localRotation = Quaternion.Euler(0, 20 + i * 35, 0); }
            torn.gameObject.SetActive(false);
            var pin = Group(crate, "RewardPoint", crate.TransformPoint(new Vector3(0, 0, .7f))); pin.localRotation = Quaternion.identity;
            var pry = Undo.AddComponent<ToolTarget>(crate.gameObject); so = new SerializedObject(pry);
            so.FindProperty("targetId").stringValue = "trial-boarded-crate"; so.FindProperty("kind").enumValueIndex = (int)ToolTargetKind.Pry;
            so.FindProperty("label").stringValue = "판자로 막힌 상자";
            SetRefs(so, "hideOnOpen", planks.ToArray()); SetRefs(so, "showOnOpen", torn.gameObject);
            so.FindProperty("rewardItemId").stringValue = ItemUses.MagicianHairpin; SetRef(so, "rewardPoint", pin); so.FindProperty("rewardUidOffset").intValue = 52;
            so.ApplyModifiedProperties();
            report.Append($"boarded crate at {cat}; ");

            // 3) Restroom: fluorescent marks only the UV lamp reveals — an arrow towards the breach wall and a cross on it.
            var restroom = target.Find("S02_Dormitory/Restroom"); Remove(restroom, "TrialUvMark");
            var glow = UvGlowMaterial();
            var mark = Group(restroom, "TrialUvMark", new Vector3(14.3f, 1.3f, BreachZ1 + .006f)); mark.rotation = Quaternion.identity;
            var arrow = Prim(mark, PrimitiveType.Quad, "UvArrow", mark.position, Vector3.one, glow);
            arrow.localRotation = Quaternion.Euler(0, 180, 180); // faces the restroom, points +x towards the breach
            // 2026-10-30: 그림은 모두 낙서 자리(얼룩 판 안)에 둔다. X를 부서지는 벽에 두면 벽 안에 묻혀 엉뚱한 곳에서 보였다.
            arrow.localPosition = new Vector3(-.12f, 0, 0); arrow.localScale = new Vector3(.36f, .2f, 1);
            var cross = Prim(mark, PrimitiveType.Quad, "UvCross", mark.TransformPoint(new Vector3(.2f, 0, 0)), Vector3.one, UvGlowMaterial("LA_UvCross", cross: true));
            cross.rotation = Quaternion.Euler(0, 180, 0); cross.localScale = new Vector3(.2f, .2f, 1);
            foreach (var r in new[] { arrow, cross }) r.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var uv = Undo.AddComponent<UvMark>(mark.gameObject); so = new SerializedObject(uv);
            so.FindProperty("targetId").stringValue = "trial-uv-restroom"; so.FindProperty("label").stringValue = "벽의 희미한 얼룩";
            SetRefs(so, "marks", arrow.gameObject, cross.gameObject);
            so.ApplyModifiedProperties();
            arrow.gameObject.SetActive(false); cross.gameObject.SetActive(false);
            report.Append("UV marks by the breach wall");
            return report.ToString();
        });

        // Violet fluorescent paint (unlit-bright via emission) on a transparent decal: an arrow, or a cross.
        static Material UvGlowMaterial(string name = "LA_UvArrow", bool cross = false)
        {
            var m = DecalMaterial(name, name + ".png", () =>
            {
                const int size = 256; var tex = new Texture2D(size, size, TextureFormat.RGBA32, false); var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size, v = (float)y / size; bool on;
                    if (cross) on = (Mathf.Abs(u - v) < .07f || Mathf.Abs(u + v - 1) < .07f) && u > .12f && u < .88f;
                    else on = (Mathf.Abs(v - .5f) < .09f && u > .08f && u < .7f) || (u >= .6f && u < .95f && Mathf.Abs(v - .5f) < (.95f - u) * .9f);
                    float wobble = Mathf.PerlinNoise(u * 18, v * 18);
                    px[y * size + x] = on && wobble > .22f ? new Color32(255, 255, 255, (byte)(200 + wobble * 55)) : new Color32(0, 0, 0, 0);
                }
                tex.SetPixels32(px); tex.Apply(); return tex;
            }, 0f, 3003);
            var violet = new Color(.62f, .25f, 1f);
            m.SetColor("_BaseColor", new Color(violet.r, violet.g, violet.b, 1f));
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", violet * 2.2f); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(m);
            return m;
        }

        // Shared exploration progress lives on a scene-root object so it survives space changes like the shop and carry.
        public static string EnsureExplorationState() => RunPass("Exploration state root", target =>
        {
            var scene = target.gameObject.scene;
            var existing = scene.GetRootGameObjects().FirstOrDefault(g => g.GetComponent<ExplorationState>() != null);
            if (existing != null) return "Exploration state already at " + existing.name;
            var go = new GameObject("ExplorationState"); Undo.RegisterCreatedObjectUndo(go, "Exploration state");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<ExplorationState>();
            return "Exploration state root created";
        });

        // The test crowbar: one stake puller on the workshop floor each generation, on the free floor nearest the room centre.
        public static string PlaceWorkshopCrowbar() => RunPass("Workshop crowbar spot", target =>
        {
            var workshop = target.Find("S10_Workshop") ?? throw new System.InvalidOperationException("Workshop missing");
            Remove(workshop, "CrowbarSpot");
            var floor = workshop.GetComponentsInChildren<Collider>().Where(c => c.name.Contains("Floor")).Select(c => c.bounds).DefaultIfEmpty(VisibleBounds(workshop)).First();
            var centre = new Vector3(floor.center.x, floor.max.y, floor.center.z);
            Vector3 best = default; float bestD = float.MaxValue;
            for (float x = floor.min.x + .8f; x <= floor.max.x - .8f; x += .4f)
            for (float z = floor.min.z + .8f; z <= floor.max.z - .8f; z += .4f)
            {
                var p = new Vector3(x, floor.max.y, z);
                if (Physics.CheckBox(p + Vector3.up * .9f, new Vector3(.5f, .85f, .5f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (!Physics.Raycast(p + Vector3.up * .5f, Vector3.down, out var hit, 1f, ~0, QueryTriggerInteraction.Ignore)) continue;
                float d = (p - centre).sqrMagnitude; if (d < bestD) { bestD = d; best = hit.point; }
            }
            if (bestD == float.MaxValue) throw new System.InvalidOperationException("No free workshop floor for the crowbar");
            var spot = Group(workshop, "CrowbarSpot", best + Vector3.up * .02f); spot.rotation = Quaternion.identity;
            Undo.AddComponent<FoundItemSpot>(spot.gameObject);
            return $"Crowbar spot at {spot.position}";
        });
    }
}
