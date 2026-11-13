using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace CurtainCall.WaitingRoom.PassageGimmicks
{
    // 12단계: 화면·안내 표시(2026-11-13 사용자 플레이 피드백). 5·11단계 뒤에 실행한다. 다시 실행할 수 있다. 결과물은 FinalLayout/Screens.
    // - 관리실: 책상 위 모니터 3대 대신 폐기물 보관소와 같은 벽면 대형 모니터(6칸 중 3칸만 지금 CCTV 화면, 나머지는 꺼짐). 의자는 옆으로 뺀다.
    // - 폐기물 보관소의 기계실 모니터: 판을 띄우지 않고 모니터 화면을 직접 들여다본다.
    // - 바닥 붉은 화살표: 무대 입구 통로(로비) 가는 길. 길을 잃기 쉬운 갈림목에만 칠한다.
    // - 살펴보기 정면: 종이·지도·노트가 뒤집혀 보이지 않게 정면 면을 지정한다.
    // - 인간대포 세트: 트램펄린을 무대 장치 보관실의 대포 옆 원래 자리로 되돌리고 겹쳐 있던 두 번째 트램펄린은 뺀다.
    public static partial class FinalLayoutBuilder
    {
        const string ArrowFolder = "Assets/Resources/Textures/WaitingRoom/Signs";
        const int MaxArrows = 12, ArrowsPerStart = 2;
        const float ArrowSpacing = 9f;
        // 화살표가 가리키는 곳: 로비에서 무대 입구 통로로 들어가는 문 앞.
        static readonly Vector3 StageGoal = new(-9.7f, 0f, 94f);
        // 길을 잃기 쉬운 먼 곳들. 여기서 무대 입구 통로까지 걸어가는 길의 갈림목마다 화살표를 칠한다.
        static readonly Vector3[] ArrowStarts =
        {
            new(28f, 0f, 40f),    // 폐기물 보관소
            new(-9f, 0f, 38f),    // 이전 세대 생활 장소
            new(-43f, 0f, 45f),   // 무대 장치 보관실
            new(12f, 0f, 48.5f),  // 긴 복도 동쪽 끝
            new(10f, -10f, 80f),  // 기계실
            new(-8f, -5f, 105f),  // 관리실
        };

        [MenuItem("CurtainCall/Waiting Room/Final Layout/12. Build Screens And Signs")]
        static void ScreensMenu() => Debug.Log(BuildScreensAndSigns());

        public static string BuildScreensAndSigns()
        {
            var layout = GameObject.Find(LayoutPath)?.transform;
            if (layout == null) return "WaitingRoom 씬을 여세요.";
            int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Final layout: screens and signs");
            var old = layout.Find(RootName + "/Screens"); if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var root = Child(Child(layout, RootName), "Screens");
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Screens");
            Physics.SyncTransforms();
            var notes = new List<string>
            {
                OfficeMonitorWall(layout, root), JunkMonitorScreen(layout, root), FloorArrows(layout, root), ClueFaces(layout), CannonSet(layout),
                OverlappingSills(layout), PaleQuarters(layout),
            };
            Physics.SyncTransforms();
            Undo.CollapseUndoOperations(undo); EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
            return string.Join(" / ", notes.Where(n => !string.IsNullOrEmpty(n)));
        }

        // ---- 관리실 벽면 대형 모니터 ----
        static string OfficeMonitorWall(Transform layout, Transform root)
        {
            var source = layout.Find("S05_JunkStorage/AbandonedResearchStorage/LargeMonitoringScreen");
            var oldWall = layout.Find("B1_Office/SecurityRoom/MonitorWall");
            var wallCollider = layout.Find("B1_Office/Shell/WallStructure")?.GetComponentsInChildren<Collider>()
                .Where(c => c.bounds.size.z > 5f).OrderByDescending(c => c.bounds.center.x).FirstOrDefault(); // 동쪽 벽
            if (source == null || oldWall == null || wallCollider == null) return "관리실 모니터: 원본을 찾지 못했습니다";
            float wallX = wallCollider.bounds.min.x, floorY = layout.Find("B1_Office/FloorCollider").GetComponent<Collider>().bounds.max.y;

            var wall = Object.Instantiate(source.gameObject, root).transform; wall.name = "OfficeMonitorWall";
            // 원본은 +x를 본다. 관리실 동쪽 벽에 걸어 -x(방 안쪽)를 보게 반 바퀴 돌린다.
            wall.rotation = Quaternion.Euler(0, 180f, 0) * source.rotation;
            var b = Bounds(wall.Find("SteelHousing")); // 아래로 늘어진 전원선은 빼고 철제 틀 기준
            // 책상 위 물건 위쪽(바닥에서 1.5m)부터. 뒤판이 벽면에 붙는다.
            wall.position += new Vector3(wallX - b.max.x, floorY + 1.5f - b.min.y, wallCollider.bounds.center.z - b.center.z);
            foreach (var t in wall.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            // 전원선이 바닥 아래로 내려가지 않게 바닥까지만.
            if (wall.Find("ScreenPowerCable") is Transform power)
            {
                var pb = Bounds(power);
                if (pb.min.y < floorY) { float keep = (pb.max.y - floorY) / pb.size.y; var ps = power.localScale; ps.y *= keep; power.localScale = ps; power.position += Vector3.up * ((floorY + pb.max.y) * .5f - pb.center.y); }
            }

            // 켜진 화면 3칸: 지금 책상 위 CCTV 화면(화면·주사선·채널 글자)을 그대로 옮겨 붙인다. 나머지 3칸은 꺼진 채.
            var dead = wall.Cast<Transform>().Where(t => t.name == "PoweredOffScreen").OrderByDescending(t => t.position.y).ThenBy(t => t.position.z).ToArray();
            var monitors = oldWall.Cast<Transform>().Where(t => t.name.StartsWith("CctvMonitor")).OrderBy(t => t.name).ToArray();
            int[] lit = { 0, 2, 4 }; // 윗줄 양 끝, 아랫줄 가운데
            int moved = 0;
            for (int n = 0; n < lit.Length && n < monitors.Length && lit[n] < dead.Length; n++)
            {
                var slot = dead[lit[n]]; var sb = slot.GetComponent<Renderer>().bounds;
                var screen = monitors[n].Find("Screen"); if (screen == null) continue;
                var from = MeshBounds(screen.GetComponent<Renderer>()); // 꺼 둔 물체는 Renderer.bounds가 비어 있어 메시로 잰다
                var holder = Child(root, "LiveScreen_" + (n + 1)); holder.position = from.center;
                foreach (var partName in new[] { "Screen", "Scanlines", "Channel" })
                    foreach (var part in monitors[n].Cast<Transform>().Where(t => t.name == partName))
                    {
                        var copy = Object.Instantiate(part.gameObject, holder, true); copy.name = partName; copy.SetActive(true);
                        GameObjectUtility.SetStaticEditorFlags(copy, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
                    }
                // 화면 크기에 맞춰 늘리고(두께 방향은 그대로) 꺼진 화면 바로 앞에 놓는다.
                holder.localScale = new Vector3(1f, sb.size.y / from.size.y, sb.size.z / from.size.z);
                holder.position = new Vector3(sb.min.x - .006f, sb.center.y, sb.center.z);
                moved++;
            }
            // 옛 모니터·선반 레일은 끈다(화면 불빛은 남긴다).
            foreach (Transform child in oldWall)
                if (child.name != "ScreenGlow" && child.gameObject.activeSelf) { Undo.RecordObject(child.gameObject, "Hide desk monitors"); child.gameObject.SetActive(false); }

            // 의자가 컴퓨터(자판)를 가려 F가 잘 닿지 않았다. 책상 북쪽 끝 옆으로 빼 둔다.
            string chairNote = "";
            if (layout.Find("B1_Office/SecurityRoom/Console/SM_Prop_Study_Chair_01") is Transform chair)
            {
                Undo.RecordObject(chair, "Move chair");
                var keyboard = layout.Find("B1_Office/SecurityRoom/Console/Keyboard");
                var spots = new[] { new Vector3(-7.25f, floorY, 107.45f), new Vector3(-7.25f, floorY, 104.2f), new Vector3(-7.6f, floorY, 107.0f) };
                var keep = chair.GetComponentsInChildren<Collider>();
                foreach (var spot in spots)
                {
                    bool free = !Physics.OverlapBox(spot + Vector3.up * .6f, new Vector3(.36f, .5f, .36f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore).Any(c => !keep.Contains(c));
                    if (!free) continue;
                    chair.SetPositionAndRotation(spot, Quaternion.Euler(0, 300f, 0)); PrefabUtility.RecordPrefabInstancePropertyModifications(chair);
                    chairNote = $", 의자 {spot:F1}(자판에서 {Vector3.Distance(spot, keyboard.position):F1}m)"; break;
                }
                if (chairNote == "") chairNote = ", 의자 옮길 빈 자리 없음";
            }
            var nb = Bounds(wall);
            return $"관리실 대형 모니터 z {nb.min.z:F1}~{nb.max.z:F1} y {nb.min.y - floorY:F1}~{nb.max.y - floorY:F1}m, 켜진 화면 {moved}칸{chairNote}";
        }

        static Bounds MeshBounds(Renderer r)
        {
            var lb = r.localBounds; var t = r.transform; var b = new Bounds(t.TransformPoint(lb.center), Vector3.zero);
            for (int bit = 0; bit < 8; bit++)
                b.Encapsulate(t.TransformPoint(lb.center + Vector3.Scale(lb.extents, new Vector3((bit & 1) != 0 ? 1 : -1, (bit & 2) != 0 ? 1 : -1, (bit & 4) != 0 ? 1 : -1))));
            return b;
        }

        // ---- 폐기물 보관소: 기계실 모니터를 직접 들여다보기 ----
        static string JunkMonitorScreen(Transform layout, Transform root)
        {
            var gate = layout.Find("S05_JunkStorage/Exploration_A/monitor")?.GetComponent<ExplorationGate>();
            var plate = gate != null ? gate.transform.Find("Visual")?.GetComponent<Renderer>() : null;
            if (plate == null) return "기계실 모니터: 대상을 찾지 못했습니다";
            var b = plate.bounds;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.name = "MachineRoomFeedScreen"; quad.transform.SetParent(root, false);
            // 화면은 +x(방 안쪽)를 본다. Quad는 -Z 면이 보이므로 앞 방향을 -x로 둔다.
            quad.transform.SetPositionAndRotation(new Vector3(b.max.x + .004f, b.center.y, b.center.z), Quaternion.LookRotation(Vector3.left, Vector3.up));
            quad.transform.localScale = new Vector3(b.size.z, b.size.y, 1f);
            var qr = quad.GetComponent<Renderer>(); qr.sharedMaterial = plate.sharedMaterial; qr.enabled = false; // 들여다볼 때만 영상이 켜진다
            qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var so = new SerializedObject(gate); so.FindProperty("screenSurface").objectReferenceValue = qr; so.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(gate);
            return "기계실 모니터 직접 보기";
        }

        // ---- 바닥 붉은 화살표 ----
        static string FloorArrows(Transform layout, Transform root)
        {
            var material = ArrowMaterial();
            var arrows = Child(root, "FloorArrows");
            var placed = new List<Vector3>(); var report = new List<string>(); int failed = 0;
            foreach (var start in ArrowStarts)
            {
                if (!NavMesh.SamplePosition(start, out var s, 4f, NavMesh.AllAreas) || !NavMesh.SamplePosition(StageGoal, out var g, 4f, NavMesh.AllAreas)) { failed++; continue; }
                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(s.position, g.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) { failed++; report.Add($"[길 없음 {start}]"); continue; }
                var corners = path.corners; int mine = 0;
                for (int i = 0; i < corners.Length - 1 && placed.Count < MaxArrows && mine < ArrowsPerStart; i++)
                {
                    Vector3 leg = corners[i + 1] - corners[i]; leg.y = 0; float length = leg.magnitude;
                    if (length < 2.5f) continue; // 짧은 꺾임은 건너뛴다
                    Vector3 dir = leg / length, at = corners[i] + dir * Mathf.Min(1.6f, length * .4f);
                    // 목적지 가까이(로비 안)와 이미 칠한 곳 근처에는 더 칠하지 않는다.
                    bool inLobby = Mathf.Abs(at.y) < 1f && new Vector2(at.x - DomeC.x, at.z - DomeC.y).magnitude < LobbyR + 1f;
                    if (inLobby || Vector3.Distance(at, g.position) < 14f) continue;
                    if (placed.Any(p => Vector3.Distance(p, at) < ArrowSpacing)) { mine++; continue; } // 이미 칠한 화살표가 이 길도 안내한다
                    // 길 모서리(벽)에 붙지 않게 통로 안쪽으로 민다.
                    if (NavMesh.FindClosestEdge(at, out var edge, NavMesh.AllAreas) && edge.distance < .8f) at += (at - edge.position).normalized * (.8f - edge.distance);
                    if (!Physics.Raycast(at + Vector3.up * .8f, Vector3.down, out var floor, 2f, ~0, QueryTriggerInteraction.Ignore) || floor.normal.y < .95f) continue;
                    // 화살표 네 귀퉁이가 모두 같은 바닥 위에 있어야 한다(허공·턱에 걸치지 않게).
                    Vector3 side = Vector3.Cross(Vector3.up, dir); bool flat = true;
                    foreach (var corner in new[] { dir * .55f + side * .3f, dir * .55f - side * .3f, -dir * .55f + side * .3f, -dir * .55f - side * .3f })
                        flat &= Physics.Raycast(floor.point + corner + Vector3.up * .5f, Vector3.down, out var under, 1f, ~0, QueryTriggerInteraction.Ignore) && Mathf.Abs(under.point.y - floor.point.y) < .02f;
                    if (!flat) continue;
                    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(quad.GetComponent<Collider>());
                    quad.name = "RedArrow_" + (placed.Count + 1); quad.transform.SetParent(arrows, false);
                    // 눕힌 Quad: 그림의 위쪽(+Y)이 가는 방향. 바닥에서 1cm 띄워 겹쳐 깜빡이지 않게 한다.
                    quad.transform.SetPositionAndRotation(floor.point + Vector3.up * .012f, Quaternion.LookRotation(Vector3.down, dir));
                    quad.transform.localScale = new Vector3(.62f, 1.15f, 1f);
                    var qr = quad.GetComponent<Renderer>(); qr.sharedMaterial = material; qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    GameObjectUtility.SetStaticEditorFlags(quad, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
                    placed.Add(at); mine++; report.Add($"({at.x:F0},{at.y:F0},{at.z:F0})");
                }
            }
            return $"붉은 화살표 {placed.Count}개 {string.Join(" ", report)}" + (failed > 0 ? $", 길을 못 찾은 출발점 {failed}곳" : "");
        }

        // 붉은 페인트 화살표 그림(닳은 가장자리)과 재질. 한 번 만들어 두고 다시 쓴다.
        static Material ArrowMaterial()
        {
            string texturePath = ArrowFolder + "/RedFloorArrow.png", materialPath = ArrowFolder + "/RedFloorArrow.mat";
            if (!AssetDatabase.IsValidFolder(ArrowFolder)) AssetDatabase.CreateFolder("Assets/Resources/Textures/WaitingRoom", "Signs");
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath) == null)
            {
                const int w = 256, h = 512; var tex = new Texture2D(w, h, TextureFormat.RGBA32, false); var rng = new System.Random(20261113);
                // 닳은 자국용 거친 무늬(작은 격자 난수를 부드럽게 섞는다).
                var noise = new float[33, 65]; for (int x = 0; x < 33; x++) for (int y = 0; y < 65; y++) noise[x, y] = (float)rng.NextDouble();
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                {
                    float u = (x + .5f) / w - .5f, v = (y + .5f) / h; // u -0.5~0.5, v 0(꼬리)~1(머리)
                    // 몸통(폭 0.28) + 머리(삼각형, v 0.62~0.97).
                    bool shaft = v > .05f && v <= .64f && Mathf.Abs(u) < .14f;
                    bool head = v > .62f && v < .97f && Mathf.Abs(u) < (.97f - v) / .35f * .44f;
                    float gx = x / 8f, gy = y / 8f; int ix = (int)gx, iy = (int)gy; float fx = gx - ix, fy = gy - iy;
                    float n = Mathf.Lerp(Mathf.Lerp(noise[ix, iy], noise[ix + 1, iy], fx), Mathf.Lerp(noise[ix, iy + 1], noise[ix + 1, iy + 1], fx), fy);
                    float fine = (float)rng.NextDouble();
                    bool paint = (shaft || head) && n * .75f + fine * .25f > .2f; // 군데군데 벗겨짐
                    float shade = .82f + n * .18f;
                    tex.SetPixel(x, y, paint ? new Color(.8f * shade, .09f * shade, .07f * shade, 1f) : new Color(.8f, .09f, .07f, 0f));
                }
                tex.Apply(); System.IO.File.WriteAllBytes(texturePath, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(texturePath);
                if (AssetImporter.GetAtPath(texturePath) is TextureImporter importer) { importer.alphaIsTransparency = true; importer.wrapMode = TextureWrapMode.Clamp; importer.SaveAndReimport(); }
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "RedFloorArrow" };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath)); material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_AlphaClip", 1f); material.SetFloat("_Cutoff", .5f); material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Smoothness", .15f); material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---- 살펴보기 정면 ----
        // 종이·지도·노트는 눕혀 두거나 벽에 붙여 둬서, 놓인 모습 그대로 띄우면 뒤집히거나 옆모습이 보였다.
        // 그림이 그려진 면의 바깥 방향과 그림의 위쪽을 메시에서 읽어 "정면" 기준(ViewFace)을 만들어 준다.
        static readonly (string id, string surface)[] FlatClues =
        {
            ("structure-a:newspaper", "NewspaperPage"), ("structure-a:map", "MapPage"), ("structure-a:old-notebook", "Pages"),
            ("final:torn-note-1", "Page"), ("final:torn-note-2", "Page"), ("final:torn-note-3", "Page"), ("final:torn-note-4", "Page"),
            ("structure-a:confiscated-notebook", "Document"),
        };

        static string ClueFaces(Transform layout)
        {
            int done = 0; var missing = new List<string>();
            foreach (var clue in layout.GetComponentsInChildren<ClueInspectable>(true))
            {
                var entry = FlatClues.FirstOrDefault(f => f.id == clue.TargetId); if (entry.id == null) continue;
                var so = new SerializedObject(clue);
                var body = so.FindProperty("body").objectReferenceValue as Transform ?? clue.transform;
                var surface = body.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(m => m.name == entry.surface && m.sharedMesh != null);
                if (surface == null || !PageFrame(surface, out var outward, out var up)) { missing.Add(entry.id); continue; }
                var face = body.Find("ViewFace") ?? new GameObject("ViewFace").transform;
                face.SetParent(body, false);
                face.SetPositionAndRotation(surface.GetComponent<Renderer>().bounds.center, Quaternion.LookRotation(-outward, up));
                so.FindProperty("viewFace").objectReferenceValue = face; so.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(clue); done++;
            }
            return $"살펴보기 정면 {done}개" + (missing.Count > 0 ? " (못 찾음: " + string.Join(", ", missing) + ")" : "");
        }

        // 그림 면의 바깥 방향과 그림 위쪽(UV의 v가 커지는 방향). 여러 면이 있으면 가장 넓게 한쪽을 보는 면들을 쓴다.
        static bool PageFrame(MeshFilter filter, out Vector3 outward, out Vector3 up)
        {
            outward = up = Vector3.zero;
            var mesh = filter.sharedMesh; var v = mesh.vertices; var uv = mesh.uv; var tri = mesh.triangles; var t = filter.transform;
            if (uv == null || uv.Length != v.Length) return false;
            // 1) 면 방향 후보(±축 6개) 중 넓이가 가장 큰 방향. 같으면 위를 보는 쪽.
            var axes = new[] { t.right, -t.right, t.up, -t.up, t.forward, -t.forward }; var area = new float[6];
            var normals = new Vector3[tri.Length / 3]; var areas = new float[tri.Length / 3];
            for (int i = 0; i < tri.Length; i += 3)
            {
                Vector3 a = t.TransformPoint(v[tri[i]]), b = t.TransformPoint(v[tri[i + 1]]), c = t.TransformPoint(v[tri[i + 2]]);
                var cross = Vector3.Cross(b - a, c - a); float size = cross.magnitude * .5f; if (size < 1e-9f) continue;
                normals[i / 3] = cross.normalized; areas[i / 3] = size;
                for (int k = 0; k < 6; k++) if (Vector3.Dot(normals[i / 3], axes[k]) > .7f) area[k] += size;
            }
            int best = 0; for (int k = 1; k < 6; k++) if (area[k] > area[best] * 1.02f || Mathf.Abs(area[k] - area[best]) <= area[best] * .02f && axes[k].y > axes[best].y) best = k;
            if (area[best] <= 0f) return false;
            // 2) 그 면들에서 그림 위쪽을 넓이로 가중 평균.
            for (int i = 0; i < tri.Length; i += 3)
            {
                if (areas[i / 3] <= 0f || Vector3.Dot(normals[i / 3], axes[best]) <= .7f) continue;
                Vector3 a = t.TransformPoint(v[tri[i]]), e1 = t.TransformPoint(v[tri[i + 1]]) - a, e2 = t.TransformPoint(v[tri[i + 2]]) - a;
                Vector2 d1 = uv[tri[i + 1]] - uv[tri[i]], d2 = uv[tri[i + 2]] - uv[tri[i]];
                float det = d1.x * d2.y - d2.x * d1.y; if (Mathf.Abs(det) < 1e-9f) continue;
                up += ((e2 * d1.x - e1 * d2.x) / det).normalized * areas[i / 3]; outward += normals[i / 3] * areas[i / 3];
            }
            if (up.sqrMagnitude < 1e-9f) return false;
            outward.Normalize(); up = Vector3.ProjectOnPlane(up, outward).normalized;
            return up.sqrMagnitude > .5f;
        }

        // ---- 겹친 문턱 ----
        // 두 방이 맞닿은 문에는 방마다 문턱이 하나씩 있어 3mm 차이로 겹쳐 있었다. 멀리서 보면 바닥이 깜빡인다
        // (2026-11-13 사용자: 폐기물 보관소·배관 구역 가는 길). 겹친 둘 중 아래쪽 문턱의 그림만 끈다(충돌은 그대로).
        static string OverlappingSills(Transform layout)
        {
            var sills = layout.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled && r.name.Contains("DoorSill")).ToArray(); int hidden = 0;
            var off = new HashSet<MeshRenderer>();
            for (int i = 0; i < sills.Length; i++)
                for (int j = i + 1; j < sills.Length; j++)
                {
                    if (off.Contains(sills[i]) || off.Contains(sills[j])) continue;
                    Bounds a = sills[i].bounds, b = sills[j].bounds;
                    if (Mathf.Abs(a.max.y - b.max.y) > .006f) continue;
                    float ox = Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x), oz = Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z);
                    if (ox <= 0 || oz <= 0 || ox * oz < .6f * Mathf.Min(a.size.x * a.size.z, b.size.x * b.size.z)) continue;
                    var lower = a.max.y <= b.max.y ? sills[i] : sills[j];
                    Undo.RecordObject(lower, "Hide overlapped sill"); lower.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(lower);
                    off.Add(lower); hidden++;
                }
            // 숙소 화장실 바닥 얼룩 둘도 1mm 차이로 겹쳐 있다. 물웅덩이를 조금 올린다.
            if (layout.Find("S02_Dormitory/Restroom/WetPuddle") is Transform puddle && layout.Find("S02_Dormitory/Restroom/Crack") is Transform crack
                && Mathf.Abs(puddle.position.y - crack.position.y) < .004f)
            { Undo.RecordObject(puddle, "Lift puddle"); puddle.position += Vector3.up * .006f; PrefabUtility.RecordPrefabInstancePropertyModifications(puddle); }
            return $"겹친 문턱 {hidden}곳 정리";
        }

        // ---- 이전 세대 생활 장소 조명 ----
        static string PaleQuarters(Transform layout)
        {
            int tuned = 0;
            foreach (var path in new[] { RootName + "/NewRooms/OldTroupeQuarters", RootName + "/Dressing/Quarters" })
                if (layout.Find(path) is Transform room)
                    foreach (var light in room.GetComponentsInChildren<Light>(true))
                    {
                        Undo.RecordObject(light, "Pale light");
                        light.color = PaleFactory; if (light.intensity > PaleFactoryMax) light.intensity = PaleFactoryMax; tuned++;
                    }
            return $"생활 장소 조명 {tuned}개 창백한 색";
        }

        // ---- 인간대포 세트 ----
        static string CannonSet(Transform layout)
        {
            var gear = layout.Find("S11_StageGear/S11_StageGear");
            var discovery = layout.Find("StructureAExploration/HumanCannonDiscovery")?.GetComponent<PerformancePropDiscovery>();
            if (gear == null || discovery == null) return "인간대포 세트: 대상을 찾지 못했습니다";
            var trampolines = gear.Cast<Transform>().Where(t => t.name == "CarryTrampoline").OrderBy(t => t.GetSiblingIndex()).ToArray();
            if (trampolines.Length == 0) return "인간대포 세트: 트램펄린 없음";
            // 첫 번째만 남겨 방 프리팹의 원래 자리(대포 옆 바닥)로 되돌린다.
            var keep = trampolines[0]; var source = PrefabUtility.GetCorrespondingObjectFromSource(keep);
            Undo.RecordObject(keep, "Restore trampoline");
            if (source != null) { keep.localPosition = source.localPosition; keep.localRotation = source.localRotation; }
            if (!keep.gameObject.activeSelf) keep.gameObject.SetActive(true);
            PrefabUtility.RecordPrefabInstancePropertyModifications(keep);
            foreach (var extra in trampolines.Skip(1)) if (extra.gameObject.activeSelf) { Undo.RecordObject(extra.gameObject, "Remove stacked trampoline"); extra.gameObject.SetActive(false); }
            // 해금 연출이 두 번째 트램펄린을 다시 켜지 않게 세트 목록에서 뺀다.
            var so = new SerializedObject(discovery); var props = so.FindProperty("mapProps");
            for (int i = props.arraySize - 1; i >= 0; i--)
                if (props.GetArrayElementAtIndex(i).objectReferenceValue is GameObject prop && trampolines.Skip(1).Any(t => t.gameObject == prop))
                { props.GetArrayElementAtIndex(i).objectReferenceValue = null; props.DeleteArrayElementAtIndex(i); }
            so.ApplyModifiedProperties(); PrefabUtility.RecordPrefabInstancePropertyModifications(discovery);
            return $"트램펄린 1개 {keep.position:F1}(대포 {gear.Find("HumanCannon")?.position:F1})";
        }
    }
}
