using System.Collections.Generic;
using System.Linq;
using CurtainCall.Cameras;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CurtainCall.WaitingRoom.PassageGimmicks
{
    // 3단계: 새 공간. 배관 연결 통로에서 서쪽으로 가는 긴 복도, 그 남쪽의 이전 세대 공연자 생활 장소(화장실·소품실),
    // 무대 장치 보관실과 맞닿는 창고. 톤앤매너(Confluence 6881292 4.3): 오래 방치된 폐공장·교도소 같은 대기실,
    // 어두운 바탕에 전구색 빛 웅덩이. 가구는 기존 숙소·화장실·소품실 물건을 복제해 낡은 생활 흔적으로 쓴다.
    public static partial class FinalLayoutBuilder
    {
        const string RoomsMeshPath = AssetFolder + "LA_FinalRooms.asset";
        const float T = .5f, Tile = 1.25f;
        const float PipeZoneWestX = 10.3f;
        // 긴 복도: 배관 연결 통로(PipeJunkLink)와 같은 폭·높이로 서쪽으로 잇는다.
        const float CorX0 = -13f, CorX1 = 13.35f, CorZ0 = 45.85f, CorZ1 = 50.85f, CorH = 5.2f;
        // 이전 세대 생활 장소와 그 남쪽 두 방.
        const float LiveX0 = -20f, LiveX1 = 2f, LiveZ0 = 37f, LiveZ1 = 45.35f, RoomH = 4.2f;
        const float SubZ0 = 30.5f, SubZ1 = 36.5f, SubSplitX0 = -9.25f, SubSplitX1 = -8.75f;
        // 창고: 무대 장치 보관실 동벽(x -26.99~-26.71) 바로 바깥.
        const float StoreX0 = -26.71f, StoreX1 = -20.5f, StoreZ0 = 35f, StoreZ1 = 45.35f, StoreH = 4.6f;
        static readonly List<Mesh> roomMeshes = new();
        static Material mFloor, mWall, mRust, mCeil, mTile, mSteel, mBulb, mRustPatina;

        [MenuItem("CurtainCall/Waiting Room/Final Layout/3. Build New Rooms")]
        static void RoomsMenu() => Debug.Log(BuildNewRooms());

        public static string BuildNewRooms()
        {
            var layout = GameObject.Find(LayoutPath)?.transform;
            if (layout == null || layout.Find(SwapMarker) == null) return "1단계(방 이동)를 먼저 실행하세요.";
            var scene = layout.gameObject.scene;
            Material M(string n) => AssetDatabase.LoadAssetAtPath<Material>(AssetFolder + n + ".mat");
            mFloor = M("LA_FloorConcrete"); mWall = M("LA_WallConcrete"); mRust = M("LA_WallCementRust"); mCeil = M("LA_ConcreteRough");
            mTile = M("LA_TealTileDark"); mSteel = M("LA_SteelPlate"); mBulb = M("LA_Bulb"); mRustPatina = M("LA_RustPatina");
            if (new[] { mFloor, mWall, mRust, mCeil, mTile, mSteel, mBulb, mRustPatina }.Any(m => m == null)) return "재질을 찾지 못했습니다.";
            int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Final layout: new rooms");
            var old = layout.Find(RootName + "/NewRooms"); if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var root = Child(Child(layout, RootName), "NewRooms");
            roomMeshes.Clear();
            var notes = new List<string>();

            // ---- 긴 복도 ----
            var cor = Child(root, "BackCorridor");
            Slab(cor, "Floor", mFloor, new(CorX0, -.2f, CorZ0), new(CorX1 + T + .01f, 0, CorZ1));
            Slab(cor, "Ceiling", mCeil, new(CorX0 - T, CorH, CorZ0 - T), new(CorX1, CorH + .2f, CorZ1 + T));
            // 동쪽 x10.3부터는 배관 구역 남벽이 같은 자리에 있어 겹치면 깜빡인다(2026-11-12 플레이에서 확인) → 거기까지만 세운다.
            // 남쪽 전실 끝 벽(x-5.2~-2.1)도 이 벽이 대신한다(FinalLayoutBuilder.Lobby에서 따로 세우지 않는다).
            Wall(cor, "NorthWall", mWall, new(CorX0 - T, 0, CorZ1), new(PipeZoneWestX, CorH, CorZ1 + T));
            Wall(cor, "WestWall", mWall, new(CorX0 - T, 0, CorZ0), new(CorX0, CorH, CorZ1));
            // 남벽은 생활 장소와 함께 쓴다. 생활 장소로 들어가는 문.
            Wall(cor, "SouthWall", mWall, new(CorX0 - T, 0, CorZ0 - T), new(CorX1, CorH, CorZ0), (-3f, -1f, 2.6f));
            // 문턱 바닥: 복도 바닥과 생활 장소 바닥 사이(벽 두께만큼)가 비지 않게 메운다.
            Slab(cor, "QuartersDoorSill", mFloor, new(-3f, -.2f, CorZ0 - T), new(-1f, 0, CorZ0));
            BuildOfficeCorridor(cor);

            // ---- 이전 세대 공연자 생활 장소 ----
            var live = Child(root, "OldTroupeQuarters");
            Slab(live, "Floor", mFloor, new(LiveX0, -.2f, SubZ0), new(LiveX1, 0, LiveZ1));
            // 홀(침상 구역)은 창고 컨테이너가 서는 높은 공간, 남쪽 두 방은 낮은 천장.
            Slab(live, "Ceiling", mCeil, new(LiveX0 - T, YardH, LiveZ0), new(LiveX1 + T, YardH + .2f, LiveZ1 + T));
            Slab(live, "SubCeiling", mCeil, new(LiveX0 - T, RoomH, SubZ0 - T), new(LiveX1 + T, RoomH + .2f, LiveZ0));
            Wall(live, "NorthWall", mRust, new(LiveX0 - T, 0, LiveZ1), new(CorX0 - T, YardH, LiveZ1 + T));
            Wall(live, "NorthWallUpper", mRust, new(CorX0 - T, CorH, LiveZ1), new(LiveX1 + T, YardH, LiveZ1 + T));
            Wall(live, "EastWall", mRust, new(LiveX1, 0, SubZ0 - T), new(LiveX1 + T, YardH, LiveZ1));
            // 서쪽은 창고 컨테이너가 있는 홀 끝과 트여 있다. 화장실 서벽만 남긴다.
            Wall(live, "WestWall", mRust, new(LiveX0 - T, 0, SubZ0 - T), new(LiveX0, YardH, LiveZ0));
            Wall(live, "SouthWall", mRust, new(LiveX0, 0, SubZ0 - T), new(LiveX1, RoomH, SubZ0));
            Wall(live, "Divider", mRust, new(LiveX0, 0, SubZ1), new(LiveX1, YardH, LiveZ0), (-15.5f, -13.9f, 2.4f), (-4f, -2.4f, 2.4f));
            Wall(live, "SubSplit", mRust, new(SubSplitX0, 0, SubZ0), new(SubSplitX1, RoomH, SubZ1));
            int washroom = BuildWashroom(live, layout);
            foreach (float x in new[] { -15f, -9.5f, -4f }) Bulb(live, new Vector3(x, YardH - .9f, 41.2f), 12f, 14f, new Color(1f, .74f, .46f));
            Bulb(live, new Vector3(-14.6f, RoomH - .7f, 33.5f), 5f, 7f, new Color(.72f, .86f, 1f));
            Bulb(live, new Vector3(-3.4f, RoomH - .7f, 33.5f), 6f, 7.5f, new Color(1f, .74f, .46f));

            // ---- 창고: 2층 컨테이너(FinalLayoutBuilder.Details) ----
            notes.Add(BuildContainerStorage(root, layout));

            // ---- 가구(기존 방 물건 복제) ----
            int props = 0;
            var dorm = layout.Find("S02_Dormitory/Dormitory");
            float bx = -18.2f;
            foreach (var bed in new[] { "Bed_A", "Bed_B", "Bed_C", "Bed_D" }) { props += Clone(dorm.Find("Beds/" + bed), live, new Vector2(bx, 43.6f), 0f, alignNorth: LiveZ1 - .15f); bx += 4.1f; }
            props += Clone(dorm.Find("Beds/FadedTwoDoorLockers"), live, new Vector2(LiveX1 - .7f, 42.5f), 0f);
            props += Clone(dorm.Find("Beds/FadedTwoDoorLockers (1)"), live, new Vector2(LiveX1 - .7f, 41.4f), 0f);
            props += Clone(dorm.Find("Beds/DarkWoodWardrobe"), live, new Vector2(-6.5f, 38f), 180f);
            props += Clone(dorm.Find("Beds/FootTrunk_0"), live, new Vector2(-16.5f, 40.6f), 20f);
            props += Clone(dorm.Find("Beds/FootTrunk_2"), live, new Vector2(-8.4f, 40.9f), -35f);
            props += Clone(dorm.Find("Living/WornEntryRug"), live, new Vector2(-10f, 40.6f), 90f);
            props += Clone(dorm.Find("Living/WestPersonalDresser"), live, new Vector2(-1.2f, 37.6f), 180f);
            var prop = layout.Find("S04_PropRoom");
            props += Clone(prop.Find("ShelfZone/NorthShelf_West"), live, new Vector2(1.1f, 32.2f), 180f);
            props += Clone(prop.Find("ShelfZone/NorthShelf_East"), live, new Vector2(1.1f, 34.9f), 180f);
            props += Clone(prop.Find("CostumeZone/WestActiveCostumes_0"), live, new Vector2(-7.9f, 32.3f), -90f);
            props += Clone(prop.Find("CostumeZone/CostumeCupboard"), live, new Vector2(-4.6f, 31.2f), -90f);
            notes.Add($"복제 가구 {props + washroom}개");

            // ---- 기존 벽에 문 내기(이 씬만) ----
            var link = layout.Find("FacilityConnections/PipeJunkLink/Shell");
            notes.Add(OpenLinkEnd(link, cor));
            SaveRoomMeshes();

            // ---- 탐색 구역(타이머가 흐르는 탐색 공간으로 등록) ----
            Area(layout, "Area_N1_BackCorridor", new Vector3((CorX0 + CorX1) * .5f, 2.5f, (CorZ0 + CorZ1) * .5f), new Vector3(CorX1 - CorX0 + 1f, 7f, CorZ1 - CorZ0 + .8f));
            Area(layout, "Area_N2_OldQuarters", new Vector3((LiveX0 + LiveX1) * .5f, 2.5f, (SubZ0 + LiveZ1) * .5f), new Vector3(LiveX1 - LiveX0 + .8f, 7f, LiveZ1 - SubZ0 + .8f));
            Area(layout, "Area_N3_Storage", new Vector3((StoreX0 + StoreX1) * .5f, 4.6f, (StoreZ0 + StoreZ1) * .5f), new Vector3(StoreX1 - StoreX0 + .6f, 10.4f, StoreZ1 - StoreZ0 + .8f));
            var zones = layout.Find("ExplorationTimer").GetComponentInChildren<ExplorationTimerZones>(true);
            if (zones != null)
            {
                var so = new SerializedObject(zones); var list = so.FindProperty("runningZones");
                foreach (var name in new[] { "Area_N1_BackCorridor", "Area_N2_OldQuarters", "Area_N3_Storage" })
                {
                    bool has = false; for (int i = 0; i < list.arraySize; i++) has |= list.GetArrayElementAtIndex(i).stringValue == name;
                    if (!has) { list.InsertArrayElementAtIndex(list.arraySize); list.GetArrayElementAtIndex(list.arraySize - 1).stringValue = name; }
                }
                so.ApplyModifiedProperties(); zones.RebuildZones();
            }
            Physics.SyncTransforms();
            Undo.CollapseUndoOperations(undo); EditorSceneManager.MarkSceneDirty(scene);
            return $"새 공간 판 {roomMeshes.Count}개. " + string.Join(" / ", notes);
        }

        // 배관 연결 통로의 막힌 서쪽 끝(x 13.35~13.85)을 복도 쪽으로 튼다: 벽 면 메시 사본에서 서쪽 면을 떼고,
        // 충돌체·띠 철물은 문 둘레 조각으로 바꾸고, 문 둘레 벽은 새 판으로 채운다.
        static string OpenLinkEnd(Transform link, Transform corridor)
        {
            const float planeX = 13.85f, z0 = 46.35f, z1 = 50.35f, top = 4.2f;
            const string copyPath = AssetFolder + "LA_Frame8PipeLinkWalls_Final.asset";
            var filter = link.Find("WallSurfaces").GetComponent<MeshFilter>();
            var source = AssetDatabase.LoadAssetAtPath<Mesh>(AssetFolder + "LA_Frame8PipeLinkWalls.asset");
            if (source == null) return "배관 연결 통로 벽 메시를 찾지 못했습니다.";
            var copy = Object.Instantiate(source); copy.name = "LA_Frame8PipeLinkWalls_Final";
            var verts = source.vertices; int removed = 0;
            for (int s = 0; s < source.subMeshCount; s++)
            {
                var tris = source.GetTriangles(s); var keep = new List<int>();
                for (int i = 0; i < tris.Length; i += 3)
                {
                    bool west = Enumerable.Range(0, 3).All(k => Mathf.Abs(filter.transform.TransformPoint(verts[tris[i + k]]).x - planeX) < .03f);
                    if (west) { removed++; continue; }
                    keep.Add(tris[i]); keep.Add(tris[i + 1]); keep.Add(tris[i + 2]);
                }
                copy.SetTriangles(keep, s);
            }
            AssetDatabase.DeleteAsset(copyPath); AssetDatabase.CreateAsset(copy, copyPath);
            Undo.RecordObject(filter, "Open link end"); filter.sharedMesh = copy; PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
            var hole = new Bounds(new Vector3(planeX - .2f, top * .5f, (z0 + z1) * .5f), new Vector3(1.6f, top, z1 - z0));
            int solids = 0;
            foreach (var t in link.Find("WallStructure").Cast<Transform>().ToArray())
            {
                if (!t.gameObject.activeSelf || t.name.EndsWith("_VentCut")) continue;
                var box = t.GetComponent<BoxCollider>(); var r = t.GetComponent<Renderer>();
                var b = r != null ? r.bounds : box != null ? box.bounds : default;
                if (b.size == Vector3.zero || b.max.x > planeX + .3f || !b.Intersects(hole)) continue;
                if (DormVentBuilder.Split(t, r != null ? null : box, b, hole)) solids++;
            }
            var wall = filter.GetComponent<Renderer>().sharedMaterial;
            var g = Child(corridor, "LinkDoorway");
            Deco(g, "Jamb", wall, new(planeX - T, 0, CorZ0), new(planeX, CorH, z0));
            Deco(g, "Jamb", wall, new(planeX - T, 0, z1), new(planeX, CorH, CorZ1));
            Deco(g, "Lintel", wall, new(planeX - T, top, z0), new(planeX, CorH, z1));
            return $"배관 연결 통로 서쪽 끝 개방(면 {removed}, 벽 부품 {solids})";
        }

        // 축에 나란한 벽. 문(긴 축 좌표 a0~a1, 높이 top)이 있으면 문 둘레 조각으로 나눈다.
        static void Wall(Transform parent, string name, Material material, Vector3 min, Vector3 max, params (float a0, float a1, float top)[] doors)
        {
            bool alongX = max.x - min.x >= max.z - min.z; float cursor = alongX ? min.x : min.z, end = alongX ? max.x : max.z;
            Vector3 Lo(float a) => alongX ? new Vector3(a, min.y, min.z) : new Vector3(min.x, min.y, a);
            Vector3 Hi(float a, float y) => alongX ? new Vector3(a, y, max.z) : new Vector3(max.x, y, a);
            foreach (var d in doors.OrderBy(d => d.a0))
            {
                if (d.a0 > cursor) Slab(parent, name, material, Lo(cursor), Hi(d.a0, max.y));
                var lintel = Lo(d.a0); lintel.y = d.top; Slab(parent, name + "_Lintel", material, lintel, Hi(d.a1, max.y));
                // 문틀: 양옆 기둥과 위 가로대(녹슨 철).
                foreach (float a in new[] { d.a0 - .06f, d.a1 - .06f }) { var lo = Lo(a) - (alongX ? new Vector3(0, 0, .04f) : new Vector3(.04f, 0, 0)); Deco(parent, "DoorJamb", mSteel, lo, Hi(a + .12f, d.top) + (alongX ? new Vector3(0, 0, .04f) : new Vector3(.04f, 0, 0))); }
                cursor = d.a1;
            }
            if (end > cursor) Slab(parent, name, material, Lo(cursor), Hi(end, max.y));
        }

        // 벽에 붙은 잠긴 철문(장식). position은 문 아래 가운데, yaw 0이면 문 면이 -z(남쪽)를 본다.
        static void SealedDoor(Transform parent, Vector3 position, float yaw)
        {
            var g = Child(parent, "SealedDoor_" + parent.Cast<Transform>().Count(t => t.name.StartsWith("SealedDoor")));
            g.name = "SealedDoor"; g.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            void Part(string n, Material m, Vector3 lo, Vector3 hi) { var go = Deco(g, n, m, lo, hi); go.transform.SetParent(g, false); go.transform.localPosition = (lo + hi) * .5f; go.transform.localRotation = Quaternion.identity; }
            Part("Leaf", mSteel, new(-.9f, 0, -.05f), new(.9f, 2.5f, 0));
            Part("Frame", mRustPatina, new(-1.02f, 0, -.07f), new(-.9f, 2.62f, .01f));
            Part("Frame", mRustPatina, new(.9f, 0, -.07f), new(1.02f, 2.62f, .01f));
            Part("Frame", mRustPatina, new(-1.02f, 2.5f, -.07f), new(1.02f, 2.62f, .01f));
            Part("Bar", mRustPatina, new(-.95f, 1.15f, -.1f), new(.95f, 1.27f, -.05f));
            Part("Chain", mRustPatina, new(-.12f, .95f, -.13f), new(.12f, 1.45f, -.1f));
        }

        static void Bulb(Transform parent, Vector3 at, float intensity, float range, Color color)
        {
            var g = new GameObject("HangingBulb").transform; g.SetParent(parent, false); g.position = at;
            var cord = Deco(g, "Cord", mSteel, at + new Vector3(-.012f, .1f, -.012f), at + new Vector3(.012f, .9f, .012f));
            var glass = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.DestroyImmediate(glass.GetComponent<Collider>());
            glass.name = "Glass"; glass.transform.SetParent(g, false); glass.transform.localScale = Vector3.one * .16f; glass.GetComponent<Renderer>().sharedMaterial = mBulb;
            var light = g.gameObject.AddComponent<Light>(); light.type = LightType.Point; light.color = color; light.intensity = intensity; light.range = range; light.shadows = LightShadows.None;
        }

        // 기존 물건을 복제해 놓는다. 게임 기능 스크립트는 떼고 겉모습·충돌체만 남긴다. center는 바닥 위 x·z 중심.
        static int Clone(Transform source, Transform parent, Vector2 center, float yaw, float alignNorth = float.NaN, float floorY = float.NaN)
        {
            if (source == null) return 0;
            var go = Object.Instantiate(source.gameObject, parent); go.name = source.name + "_Old";
            for (int pass = 0; pass < 3; pass++)
                foreach (var b in go.GetComponentsInChildren<MonoBehaviour>(true)) if (b != null && !(b is CameraCollisionSurface)) Object.DestroyImmediate(b);
            foreach (var l in go.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l);
            go.SetActive(true);
            var t = go.transform; t.SetPositionAndRotation(source.position, Quaternion.Euler(0, yaw, 0) * source.rotation);
            var b0 = Bounds(t); float floor = float.IsNaN(floorY) ? Bounds(source).min.y : floorY;
            float z = float.IsNaN(alignNorth) ? center.y - b0.center.z : alignNorth - b0.max.z;
            t.position += new Vector3(center.x - b0.center.x, floor - b0.min.y, z);
            return 1;
        }

        static void Area(Transform layout, string name, Vector3 center, Vector3 size)
        {
            var areas = layout.Find("ExplorationTimer/Frame8Areas"); var template = areas.Find("Area_P05_PipeZone");
            var t = areas.Find(name);
            if (t == null) { t = Object.Instantiate(template.gameObject, areas).transform; t.name = name; Undo.RegisterCreatedObjectUndo(t.gameObject, "New area"); }
            t.SetPositionAndRotation(center, Quaternion.identity); t.localScale = Vector3.one;
            var box = t.GetComponent<BoxCollider>(); box.center = Vector3.zero; box.size = size;
        }

        // 상자 메시(월드 좌표, 면마다 1.25m 타일 UV) + 충돌체.
        static GameObject Slab(Transform parent, string name, Material material, Vector3 min, Vector3 max)
        {
            var go = Deco(parent, name, material, min, max);
            go.AddComponent<BoxCollider>();
            go.AddComponent<CameraCollisionSurface>().LocalBounds = go.GetComponent<MeshFilter>().sharedMesh.bounds;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return go;
        }

        static GameObject Deco(Transform parent, string name, Material material, Vector3 min, Vector3 max)
        {
            var c = new Vector3[8];
            for (int i = 0; i < 8; i++) c[i] = new Vector3((i & 1) != 0 ? max.x : min.x, (i & 2) != 0 ? max.y : min.y, (i & 4) != 0 ? max.z : min.z);
            var center = (min + max) * .5f;
            int[][] faces = { new[] { 0, 2, 6, 4 }, new[] { 1, 5, 7, 3 }, new[] { 0, 4, 5, 1 }, new[] { 2, 3, 7, 6 }, new[] { 0, 1, 3, 2 }, new[] { 4, 6, 7, 5 } };
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            foreach (var f in faces)
            {
                Vector3 a = c[f[0]], b = c[f[1]], cc = c[f[2]], d = c[f[3]];
                var normal = Vector3.Cross(b - a, cc - a);
                if (Vector3.Dot(normal, (a + b + cc + d) * .25f - center) < 0) { (b, d) = (d, b); normal = -normal; }
                int s = verts.Count;
                foreach (var p in new[] { a, b, cc, d })
                {
                    verts.Add(p - center);
                    float ax = Mathf.Abs(normal.x), ay = Mathf.Abs(normal.y), az = Mathf.Abs(normal.z);
                    uvs.Add(ay >= ax && ay >= az ? new Vector2(p.x, p.z) / Tile : ax >= az ? new Vector2(p.z, p.y) / Tile : new Vector2(p.x, p.y) / Tile);
                }
                tris.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
            }
            var mesh = new Mesh { name = "LA_FinalRooms_" + parent.name + "_" + name + "_" + roomMeshes.Count };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            roomMeshes.Add(mesh);
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = center;
            go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        static void SaveRoomMeshes()
        {
            AssetDatabase.DeleteAsset(RoomsMeshPath);
            if (roomMeshes.Count == 0) return;
            AssetDatabase.CreateAsset(roomMeshes[0], RoomsMeshPath);
            for (int i = 1; i < roomMeshes.Count; i++) AssetDatabase.AddObjectToAsset(roomMeshes[i], RoomsMeshPath);
            AssetDatabase.SaveAssets();
        }
    }
}
