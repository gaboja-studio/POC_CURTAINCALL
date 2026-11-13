using System.Collections.Generic;
using CurtainCall.Cameras;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CurtainCall.WaitingRoom.PassageGimmicks
{
    /// <summary>
    /// 숙소 환풍구(통로 기믹 CC-261, 2026-11-11 사용자). WaitingRoom 씬에 다시 실행해도 같은 결과가 나오는 편집 도구.
    /// - 숙소 동벽, 침대 C·D 사이(책상 끝 위, 바닥 2.3m) 환풍구 → 벽 바로 밖 분기 칸(서서 착지).
    ///   환풍구를 보고 오른쪽(남) → 건물 바깥 덕트 → 화장실 동벽 덮개(2.3m). 왼쪽(북) → 짧은 덕트 → 설비실.
    /// - 설비실(기계실 동벽 바로 바깥, 새 방): 기계 설비 상단을 1.7m 안팎씩 내려가 바닥(-5m). 동력 소켓에 배터리를 넣으면
    ///   기계실 동벽 셔터가 열리고 기계실 새 캣워크(-5m)로 나간다. 새 캣워크는 기계실 동벽·북벽을 따라 기존 북쪽 캣워크와 잇는다.
    /// - 숙소 덮개는 방 쪽에서, 화장실 덮개는 덕트 안에서 부순다(맨몸 3대·천막 말뚝 뽑개 1대).
    /// 다른 방의 기존 물체는 건드리지 않는다. 환풍구·셔터가 지나는 벽만 이 씬에서 구멍을 낸다
    /// (벽 메시 사본 *_Vent.asset, 벽 충돌체·벽판은 원본을 끄고(_VentOriginal) 구멍 둘레 조각(_VentCut)으로 대신).
    /// </summary>
    public static class DormVentBuilder
    {
        const string LayoutPath = "WaitingRoom_LayoutA";
        const string RootName = "DormVentRoute";
        const string MachineRootName = "VentCatwalk";
        const string CutSuffix = "_VentCut", OriginalSuffix = "_VentOriginal";
        const string AssetFolder = "Assets/Scenes/Tests/WaitingRoom/Base/LayoutA/";
        const string MeshAssetPath = AssetFolder + "LA_DormVent.asset";
        const float T = .04f, Tile = 1.25f, Ring = .035f, RingDepth = .07f;
        const float H = 1.2f, VentH = 1.7f, Floor = 2.3f, Catwalk = -5f;
        // 숙소 동벽 환풍구(침대 C z74.46~·D ~z69.5 사이, 책상 끝 위). 벽 안쪽 면 x21.85.
        const float DormWallX = 21.85f, VentZ0 = 70.6f, VentZ1 = 71.8f;
        // 분기 칸(벽 바로 밖)과 바깥 덕트 줄(숙소 동벽 바깥 면 x22.35에 붙음).
        const float LaneX0 = 22.4f, LaneX1 = 23.6f, JunctionX1 = LaneX1;
        // 화장실 갈래.
        const float RestroomWallX = 18.35f, RestZ0 = 62.95f, RestZ1 = 64.15f;
        // 설비실(기계실 동벽 x23.3~23.8 바로 바깥).
        const float RoomX0 = 23.85f, RoomX1 = 27.85f, RoomZ0 = 71.7f, RoomZ1 = 78.5f, RoomTop = 4.5f, MachineWallX = 23.3f;
        const float ToRoomZ0 = VentZ1, ToRoomZ1 = VentZ1 + H;
        const float ShutterZ0 = 77.2f, ShutterZ1 = 78.4f, ShutterTop = Catwalk + 1.9f;

        enum Face { XMin, XMax, ZMin, ZMax }

        sealed class Cell
        {
            public string Name; public Vector3 Min, Max;
            public readonly Dictionary<Face, Rect?> Openings = new();
            public Cell(string name, Vector3 min, Vector3 max) { Name = name; Min = min; Max = max; }
            /// <summary>hole: x=면의 가로 시작, width=가로 길이, y=높이 시작, height=높이(월드). null이면 면 전체가 뚫림.</summary>
            public Cell Open(Face face, Rect? hole = null) { Openings[face] = hole; return this; }
            public bool FullyOpen(Face face) => Openings.TryGetValue(face, out var hole) && hole == null;
            public bool Closed(Face face) => !Openings.ContainsKey(face);
        }

        static Material sheet, steel, iron, rusty, concrete, bulb;
        static readonly List<Mesh> meshes = new();

        [MenuItem("CurtainCall/Waiting Room/Build Dorm Vent Route")]
        static void Menu() => Debug.Log(Build());

        public static string Build()
        {
            var layout = GameObject.Find(LayoutPath)?.transform;
            var dorm = layout != null ? layout.Find("S02_Dormitory") : null;
            var machine = layout != null ? layout.Find("B1_MachineRoom") : null;
            if (dorm == null || machine == null) return "숙소·기계실을 찾지 못했습니다(WaitingRoom 씬을 여세요).";
            sheet = AssetDatabase.LoadAssetAtPath<Material>(AssetFolder + "LA_GalvanisedPipe.mat");
            steel = AssetDatabase.LoadAssetAtPath<Material>(AssetFolder + "LA_SteelPlate.mat");
            bulb = AssetDatabase.LoadAssetAtPath<Material>(AssetFolder + "LA_Bulb.mat");
            iron = machine.Find("Catwalk/Grating")?.GetComponent<Renderer>()?.sharedMaterial;
            rusty = machine.Find("Catwalk/GratingEdge")?.GetComponent<Renderer>()?.sharedMaterial;
            var upperWall = FindWallSurface(machine);
            concrete = upperWall != null ? upperWall.GetComponent<Renderer>().sharedMaterial : null;
            if (sheet == null || steel == null || iron == null || rusty == null || concrete == null) return "재질을 찾지 못했습니다.";

            var notes = new List<string>();
            foreach (var old in new[] { dorm.Find(RootName), machine.Find(MachineRootName) })
                if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            notes.Add(RestoreWalls(layout));
            meshes.Clear();

            var root = NewRoot(dorm, RootName);
            var ducts = Group(root, "Duct");
            var lane = new Rect(LaneX0, Floor, LaneX1 - LaneX0, H);
            // 분기 칸: 숙소 벽을 지나 벽 바로 밖까지(서서 착지). 남쪽 면 → 화장실 덕트, 북쪽 면 → 설비실 덕트.
            BuildCell(ducts, new Cell("Junction", new(DormWallX, Floor, VentZ0), new(JunctionX1, Floor + VentH, VentZ1))
                .Open(Face.XMin).Open(Face.ZMin, lane).Open(Face.ZMax, lane));
            BuildCell(ducts, new Cell("ToRestroomLane", new(LaneX0, Floor, RestZ1), new(LaneX1, Floor + H, VentZ0)).Open(Face.ZMax).Open(Face.ZMin));
            BuildCell(ducts, new Cell("ToRestroomBend", new(LaneX0, Floor, RestZ0), new(LaneX1, Floor + H, RestZ1)).Open(Face.ZMax).Open(Face.XMin));
            BuildCell(ducts, new Cell("ToRestroom", new(RestroomWallX, Floor, RestZ0), new(LaneX0, Floor + H, RestZ1)).Open(Face.XMax).Open(Face.XMin));
            BuildCell(ducts, new Cell("ToRoomBend", new(LaneX0, Floor, VentZ1), new(LaneX1, Floor + H, ToRoomZ1)).Open(Face.ZMin).Open(Face.XMax));
            BuildCell(ducts, new Cell("ToRoom", new(LaneX1, Floor, ToRoomZ0), new(RoomX0 + .05f, Floor + H, ToRoomZ1)).Open(Face.XMin).Open(Face.XMax));

            var supports = Group(root, "Supports");
            foreach (float z in new[] { 68.9f, 70.0f }) LaneBracket(supports, z);
            // 화장실 갈래 굽이 밑: 기계실 남벽 위 끝(-0.4m)에 선 기둥.
            Deco(supports, "BendPost", steel, new(22.95f, -.4f, 63.5f), new(23.07f, Floor - T, 63.62f));
            Deco(supports, "BendBearer", steel, new(LaneX0 - .05f, Floor - T - .09f, 63.5f), new(LaneX1 + .1f, Floor - T, 63.62f));
            foreach (float x in new[] { 19.6f, 21.3f }) PocketPost(supports, x);

            var covers = Group(root, "Covers");
            Panel(covers, "DormVentCover", "dorm-vent:dorm-cover", "환풍구 덮개", new(DormWallX - .065f, Floor, (VentZ0 + VentZ1) * .5f), 90f,
                VentZ1 - VentZ0, VentH, latch: new(DormWallX - .35f, Floor + .45f, (VentZ0 + VentZ1) * .5f));
            Panel(covers, "RestroomVentCover", "dorm-vent:restroom-cover", "환풍구 덮개", new(RestroomWallX - .065f, Floor, (RestZ0 + RestZ1) * .5f), 90f,
                RestZ1 - RestZ0, H, latch: new(RestroomWallX + .4f, Floor + .6f, (RestZ0 + RestZ1) * .5f));

            BuildRoom(Group(root, "UtilityRoom"));
            BuildCatwalk(NewRoot(machine, MachineRootName));

            SaveMeshes();
            notes.Add(CutWall(dorm.Find("Dormitory/Shell/WallSurfaces"), new[] { dorm.Find("Dormitory/Shell/WallStructure"), dorm.Find("Skin_Dormitory") },
                new[] { Hole(DormWallX, VentZ0, VentZ1, Floor, Floor + VentH) }, "숙소 동벽"));
            notes.Add(CutWall(dorm.Find("Restroom/Shell/WallSurfaces"), new[] { dorm.Find("Restroom/Shell/WallStructure"), dorm.Find("Skin_Restroom") },
                new[] { Hole(RestroomWallX, RestZ0, RestZ1, Floor, Floor + H) }, "화장실 동벽"));
            notes.Add(CutWall(upperWall, new[] { FindWallStructure(machine) },
                new[] { Hole(MachineWallX, ShutterZ0, ShutterZ1, Catwalk + .01f, ShutterTop) }, "기계실 동벽(셔터)"));
            EditorSceneManager.MarkSceneDirty(dorm.gameObject.scene);
            return $"생성: 판 {meshes.Count}개. {string.Join(" ", notes)} 겹침 검사: {Validate(root, machine.Find(MachineRootName))}";
        }

        static Transform NewRoot(Transform parent, string name)
        {
            var root = new GameObject(name).transform;
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Build dorm vent");
            root.SetParent(parent, false);
            root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.localScale = Vector3.one;
            return root;
        }

        static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform;
        }

        // x가 일정한 벽(동쪽 벽)의 구멍. 벽 두께 0.5m.
        static Bounds Hole(float wallInnerX, float z0, float z1, float y0, float y1) =>
            new(new Vector3(wallInnerX + .25f, (y0 + y1) * .5f, (z0 + z1) * .5f), new Vector3(1.5f, y1 - y0 + 2 * T + .02f, z1 - z0 + 2 * T + .02f));

        // ---- 덕트 판 ----

        static void BuildCell(Transform parent, Cell c)
        {
            var g = Group(parent, c.Name);
            float ex0 = c.Closed(Face.XMin) ? T : 0, ex1 = c.Closed(Face.XMax) ? T : 0, ez0 = c.Closed(Face.ZMin) ? T : 0, ez1 = c.Closed(Face.ZMax) ? T : 0;
            Slab(g, "Floor", sheet, new(c.Min.x - ex0, c.Min.y - T, c.Min.z - ez0), new(c.Max.x + ex1, c.Min.y, c.Max.z + ez1));
            Slab(g, "Top", sheet, new(c.Min.x - ex0, c.Max.y, c.Min.z - ez0), new(c.Max.x + ex1, c.Max.y + T, c.Max.z + ez1));
            float y0 = c.Min.y - T, y1 = c.Max.y + T;
            if (!c.FullyOpen(Face.XMin)) Side(g, "SideXMin", c, Face.XMin, new(c.Min.x - T, y0, c.Min.z - ez0), new(c.Min.x, y1, c.Max.z + ez1));
            if (!c.FullyOpen(Face.XMax)) Side(g, "SideXMax", c, Face.XMax, new(c.Max.x, y0, c.Min.z - ez0), new(c.Max.x + T, y1, c.Max.z + ez1));
            if (!c.FullyOpen(Face.ZMin)) Side(g, "SideZMin", c, Face.ZMin, new(c.Min.x, y0, c.Min.z - T), new(c.Max.x, y1, c.Min.z));
            if (!c.FullyOpen(Face.ZMax)) Side(g, "SideZMax", c, Face.ZMax, new(c.Min.x, y0, c.Max.z), new(c.Max.x, y1, c.Max.z + T));
            bool alongX = c.Max.x - c.Min.x > c.Max.z - c.Min.z;
            float a0 = alongX ? c.Min.x : c.Min.z, a1 = alongX ? c.Max.x : c.Max.z;
            int n = Mathf.Max(1, Mathf.RoundToInt((a1 - a0) / Tile));
            bool skipStart = !c.Closed(alongX ? Face.XMin : Face.ZMin), skipEnd = !c.Closed(alongX ? Face.XMax : Face.ZMax);
            for (int i = 0; i <= n; i++)
                if (!(i == 0 && skipStart) && !(i == n && skipEnd)) RingAt(g, alongX, Mathf.Lerp(a0, a1, i / (float)n), c.Min, c.Max);
        }

        // 다른 덕트와 이어지는 면: 이어지는 사각형만 비우고 둘레를 막는다.
        static void Side(Transform g, string name, Cell c, Face face, Vector3 min, Vector3 max)
        {
            if (!(c.Openings.TryGetValue(face, out var hole) && hole is Rect h)) { Slab(g, name, sheet, min, max); return; }
            bool xFace = face == Face.XMin || face == Face.XMax;
            float a0 = xFace ? min.z : min.x, a1 = xFace ? max.z : max.x;
            Vector3 P(float a, float y, Vector3 basis) => xFace ? new Vector3(basis.x, y, a) : new Vector3(a, y, basis.z);
            Slab(g, name + "Low", sheet, P(a0, min.y, min), P(a1, h.yMin, max));
            Slab(g, name + "High", sheet, P(a0, h.yMax, min), P(a1, max.y, max));
            if (h.xMin > a0 + .001f) Slab(g, name + "A", sheet, P(a0, h.yMin, min), P(h.xMin, h.yMax, max));
            if (h.xMax < a1 - .001f) Slab(g, name + "B", sheet, P(h.xMax, h.yMin, min), P(a1, h.yMax, max));
        }

        static void RingAt(Transform g, bool alongX, float at, Vector3 min, Vector3 max)
        {
            float o = T + Ring, d = RingDepth * .5f, y0 = min.y - o, y1 = max.y + o;
            if (alongX)
            {
                Deco(g, "Ring", steel, new(at - d, y0, min.z - o), new(at + d, min.y - T, max.z + o));
                Deco(g, "Ring", steel, new(at - d, max.y + T, min.z - o), new(at + d, y1, max.z + o));
                Deco(g, "Ring", steel, new(at - d, y0, min.z - o), new(at + d, y1, min.z - T));
                Deco(g, "Ring", steel, new(at - d, y0, max.z + T), new(at + d, y1, max.z + o));
            }
            else
            {
                Deco(g, "Ring", steel, new(min.x - o, y0, at - d), new(max.x + o, min.y - T, at + d));
                Deco(g, "Ring", steel, new(min.x - o, max.y + T, at - d), new(max.x + o, y1, at + d));
                Deco(g, "Ring", steel, new(min.x - o, y0, at - d), new(min.x - T, y1, at + d));
                Deco(g, "Ring", steel, new(max.x + T, y0, at - d), new(max.x + o, y1, at + d));
            }
        }

        // ---- 받침(건물 바깥) ----

        // 화장실 갈래 줄 받침: 숙소 동벽 바깥 면(x22.35, z68.35~)에 판으로 고정한 가로대와 빗대.
        static void LaneBracket(Transform g, float z)
        {
            const float wall = 22.35f;
            float under = Floor - T;
            Deco(g, "WallPlate", steel, new(wall, under - 1.1f, z - .14f), new(wall + .02f, under + .05f, z + .14f));
            Deco(g, "Bearer", steel, new(wall + .02f, under - .09f, z - .05f), new(LaneX1 + .12f, under, z + .05f));
            Strut(g, new Vector3(wall + .04f, under - 1.0f, z), new Vector3(LaneX1 + .06f, under - .08f, z), .06f);
        }

        // 화장실 동쪽 빈 칸 받침: 바닥(0m)에서 덕트 밑을 받치는 기둥과 가로대.
        static void PocketPost(Transform g, float x)
        {
            const float r = .05f;
            float under = Floor - T;
            Deco(g, "PocketPost", steel, new(x - r, 0f, RestZ0 - T - .05f - 2 * r), new(x + r, under, RestZ0 - T - .05f));
            Deco(g, "PocketPost", steel, new(x - r, 0f, RestZ1 + T + .05f), new(x + r, under, RestZ1 + T + .05f + 2 * r));
            Deco(g, "PocketBearer", steel, new(x - .05f, under - .09f, RestZ0 - .2f), new(x + .05f, under, RestZ1 + .2f));
        }

        static void Strut(Transform g, Vector3 from, Vector3 to, float thickness)
        {
            var brace = Deco(g, "Strut", steel, Vector3.zero, Vector3.one);
            brace.transform.position = (from + to) * .5f;
            brace.transform.rotation = Quaternion.LookRotation(to - from);
            brace.transform.localScale = new Vector3(thickness, thickness, Vector3.Distance(from, to));
        }

        // ---- 설비실(새 방) ----

        static void BuildRoom(Transform g)
        {
            var shell = Group(g, "Shell");
            // 서쪽은 기계실 동벽(위 끝 -0.4m)을 쓰고 그 위만 새 벽으로 막는다. 덕트 구멍(ToRoom)은 비운다.
            // 기계실 동벽 위(-0.4m~)는 새 벽, 그 아래는 기계실 벽 바깥 면에 얇은 마감판(바깥 면이 보이지 않아 기계실이 비치므로).
            Wall(shell, "WestWallLow", new(RoomX0 - .25f, -.4f, RoomZ0), new(RoomX0, Floor - T, RoomZ1));
            Wall(shell, "WestWallSouth", new(RoomX0 - .25f, Floor - T, RoomZ0), new(RoomX0, Floor + H + T, ToRoomZ0 - T));
            Wall(shell, "WestWallNorth", new(RoomX0 - .25f, Floor - T, ToRoomZ1 + T), new(RoomX0, Floor + H + T, RoomZ1));
            Wall(shell, "WestWallHigh", new(RoomX0 - .25f, Floor + H + T, RoomZ0), new(RoomX0, RoomTop, RoomZ1));
            Wall(shell, "WestLiner", new(RoomX0 - .05f, Catwalk, RoomZ0), new(RoomX0, -.4f, ShutterZ0 - T));
            Wall(shell, "WestLinerAboveShutter", new(RoomX0 - .05f, ShutterTop + T, ShutterZ0 - T), new(RoomX0, -.4f, ShutterZ1 + T));
            Wall(shell, "WestLinerNorth", new(RoomX0 - .05f, Catwalk, ShutterZ1 + T), new(RoomX0, -.4f, RoomZ1));
            Wall(shell, "EastWall", new(RoomX1, Catwalk - .3f, RoomZ0 - .25f), new(RoomX1 + .25f, RoomTop, RoomZ1 + .25f));
            // 남·북 벽: 기계실 동벽(x23.3~23.8, 위 끝 -0.4m)과 겹치지 않게 그 높이까지는 x23.8부터.
            Wall(shell, "SouthWallLow", new(RoomX0 - .05f, Catwalk - .3f, RoomZ0 - .25f), new(RoomX1, -.4f, RoomZ0));
            Wall(shell, "SouthWall", new(RoomX0 - .25f, -.4f, RoomZ0 - .25f), new(RoomX1, RoomTop, RoomZ0));
            Wall(shell, "NorthWallLow", new(RoomX0 - .05f, Catwalk - .3f, RoomZ1), new(RoomX1, -.4f, RoomZ1 + .25f));
            Wall(shell, "NorthWall", new(RoomX0 - .25f, -.4f, RoomZ1), new(RoomX1, RoomTop, RoomZ1 + .25f));
            Wall(shell, "Floor", new(RoomX0, Catwalk - .3f, RoomZ0), new(RoomX1, Catwalk, RoomZ1));
            Wall(shell, "Roof", new(RoomX0 - .25f, RoomTop, RoomZ0 - .25f), new(RoomX1 + .25f, RoomTop + .2f, RoomZ1 + .25f));
            // 기계 설비: 덕트 출구 아래부터 단을 이루는 설비 상자(위 면을 밟고 내려간다). 0.6 → -1.1 → -2.9 → 바닥 -5.
            var gear = Group(g, "Machinery");
            Machine(gear, "Transformer", new(RoomX0, Catwalk, ToRoomZ0 - .1f), new(RoomX0 + 1.6f, .6f, ToRoomZ1 + .55f));
            Machine(gear, "Compressor", new(RoomX0, Catwalk, ToRoomZ1 + .55f), new(RoomX0 + 1.6f, -1.1f, ToRoomZ1 + 2.15f));
            Machine(gear, "PumpHousing", new(RoomX0, Catwalk, ToRoomZ1 + 2.15f), new(RoomX0 + 1.6f, -2.9f, ToRoomZ1 + 3.75f));
            Machine(gear, "Generator", new(RoomX1 - 1.4f, Catwalk, RoomZ0 + .4f), new(RoomX1 - .2f, -2.6f, RoomZ0 + 2.6f));
            Place(gear, "Assets/_ThirdParty/Synty/PolygonApocalypse/Prefabs/Props/SM_Prop_Pipes_Valves_01.prefab", new Vector3(25.9f, Catwalk, RoomZ1 - .22f), 180f, 1f);
            Pipe(gear, new(RoomX1 - .8f, -2.6f, RoomZ0 + 1.5f), new(RoomX1 - .8f, RoomTop - .3f, RoomZ0 + 1.5f));
            Pipe(gear, new(RoomX1 - .8f, RoomTop - .3f, RoomZ0 + 1.5f), new(RoomX0 + .8f, RoomTop - .3f, RoomZ0 + 1.5f));
            // 동력 소켓(배터리) → 셔터.
            BuildSocketAndShutter(Group(g, "PowerSocket"));
            foreach (var p in new[] { new Vector3(25.9f, RoomTop - .4f, 73.6f), new Vector3(25.9f, RoomTop - .4f, 77f) })
            {
                if (bulb != null) Deco(g, "CageLamp", bulb, p - new Vector3(.08f, .12f, .08f), p + new Vector3(.08f, .12f, .08f));
                Deco(g, "LampWire", steel, new(p.x - .01f, p.y + .12f, p.z - .01f), new(p.x + .01f, RoomTop, p.z + .01f));
                var lightGo = new GameObject("RoomLight"); lightGo.transform.SetParent(g, false); lightGo.transform.position = p - Vector3.up * .2f;
                var light = lightGo.AddComponent<Light>(); light.type = LightType.Point; light.range = 8f; light.intensity = 1.6f; light.color = new Color(1f, .8f, .55f); light.shadows = LightShadows.None;
            }
        }

        static void Wall(Transform g, string name, Vector3 min, Vector3 max) => Slab(g, name, concrete, min, max);

        static void Machine(Transform g, string name, Vector3 min, Vector3 max)
        {
            Slab(g, name, steel, min, max);
            // 냉각 날개·점검 패널·띠(위 면을 밟을 수 있는 설비 상자).
            float h = max.y - min.y;
            for (int i = 0; i < 4; i++)
            {
                float y = min.y + h * (.25f + i * .15f);
                Deco(g, name + "Fin", iron, new(max.x, y, min.z + .2f), new(max.x + .03f, y + .05f, max.z - .2f));
            }
            Deco(g, name + "Band", rusty, new(min.x - .01f, max.y - .12f, min.z - .01f), new(max.x + .01f, max.y - .04f, max.z + .01f));
        }

        static void BuildSocketAndShutter(Transform g)
        {
            float zc = (ShutterZ0 + ShutterZ1) * .5f;
            // 소켓 함: 설비실 동벽, 바닥에서 손이 닿는 높이. 배터리를 들고 F → 넣으면 셔터가 열린다.
            var box = Deco(g, "SocketBox", steel, new(RoomX1 - .25f, Catwalk + .8f, 76.4f), new(RoomX1, Catwalk + 1.5f, 77.0f));
            Deco(g, "SocketSlot", iron, new(RoomX1 - .27f, Catwalk + 1.0f, 76.55f), new(RoomX1 - .24f, Catwalk + 1.3f, 76.85f));
            var socketGo = new GameObject("Socket"); socketGo.transform.SetParent(g, false);
            socketGo.transform.position = new Vector3(RoomX1 - .35f, Catwalk + 1.15f, 76.7f);
            socketGo.SetActive(false);
            var gate = socketGo.AddComponent<ExplorationGate>();
            var so = new SerializedObject(gate);
            so.FindProperty("targetId").stringValue = "dorm-vent:power-socket";
            so.FindProperty("label").stringValue = "동력 소켓";
            so.FindProperty("kind").enumValueIndex = (int)ExplorationGateKind.Open;
            so.FindProperty("toolId").stringValue = ItemUses.PowerBattery;
            so.FindProperty("bareHandHits").intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            socketGo.SetActive(true);
            // 배터리: 소켓 바로 앞 바닥(2026-11-11 사용자). 세대마다 한 번 놓이고 F로 줍는다.
            var spotGo = new GameObject("BatterySpot"); spotGo.transform.SetParent(g, false);
            spotGo.transform.position = new Vector3(RoomX1 - .9f, Catwalk, 76.7f);
            spotGo.SetActive(false);
            var spot = spotGo.AddComponent<FoundItemSpot>();
            var spso = new SerializedObject(spot);
            spso.FindProperty("spotId").stringValue = "utility-room-battery";
            spso.FindProperty("uidOffset").intValue = 51;
            spso.FindProperty("itemId").stringValue = ItemUses.PowerBattery;
            spso.FindProperty("itemName").stringValue = "배터리";
            spso.ApplyModifiedPropertiesWithoutUndo();
            spotGo.SetActive(true);
            // 셔터: 기계실 동벽 구멍을 막고, 소켓이 켜지면 위로 말려 올라간다.
            var shutter = Group(g, "Shutter");
            shutter.position = new Vector3(MachineWallX + .1f, Catwalk, zc);
            for (int i = 0; i < 8; i++)
            {
                var slat = Deco(shutter, "ShutterSlat", steel, new(MachineWallX + .06f, Catwalk + i * .24f, ShutterZ0 - .05f), new(MachineWallX + .14f, Catwalk + i * .24f + .22f, ShutterZ1 + .05f));
            }
            var blocker = new GameObject("ShutterBlocker"); blocker.transform.SetParent(shutter, false);
            var col = blocker.AddComponent<BoxCollider>();
            col.center = shutter.InverseTransformPoint(new Vector3(MachineWallX + .1f, (Catwalk + ShutterTop) * .5f, zc));
            col.size = new Vector3(.1f, ShutterTop - Catwalk, ShutterZ1 - ShutterZ0);
            var slide = shutter.gameObject.AddComponent<ExplorationSlide>();
            var sso = new SerializedObject(slide);
            sso.FindProperty("openedBy").stringValue = "dorm-vent:power-socket";
            sso.FindProperty("openOffset").vector3Value = new Vector3(0, 1.85f, 0);
            sso.ApplyModifiedPropertiesWithoutUndo();
            Deco(g, "ShutterHousing", steel, new(MachineWallX + .02f, ShutterTop, ShutterZ0 - .15f), new(MachineWallX + .3f, ShutterTop + .35f, ShutterZ1 + .15f));
        }

        static void Pipe(Transform g, Vector3 a, Vector3 b)
        {
            const float r = .05f;
            Deco(g, "Pipe", rusty, Vector3.Min(a, b) - Vector3.one * r, Vector3.Max(a, b) + Vector3.one * r);
        }

        static void Place(Transform g, string path, Vector3 position, float yaw, float scale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, g);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            go.transform.localScale = Vector3.one * scale;
        }

        // ---- 기계실 새 캣워크: 설비실 셔터 앞 → 동벽 → 북벽 → 기존 북쪽 캣워크(x11.3). 기존과 같은 재질·높이·난간 ----

        static void BuildCatwalk(Transform g)
        {
            const float ex0 = 21.7f, ex1 = 22.95f, ez0 = 76.8f, nz0 = 99.7f, nz1 = 101.35f, nx0 = 11.3f;
            // 보일러 굴뚝(x22.0~22.6, z93.35~93.95)이 캣워크 자리를 세로로 지난다. 굴뚝은 그대로 두고 발판에 구멍을 내며, 그 앞만 서쪽으로 넓혀 돌아간다.
            const float fx0 = 22.0f, fx1 = 22.6f, fz0 = 93.3f, fz1 = 94.0f, bz0 = 92.8f, bz1 = 94.5f, bx0 = 21.05f;
            Grating(g, new(ex0, ez0), new(ex1, bz0));                         // 동벽 구간(굴뚝 앞까지)
            Grating(g, new(bx0, bz0), new(fx0, bz1));                         // 굴뚝 서쪽 우회
            Grating(g, new(fx0, bz0), new(ex1, fz0));
            Grating(g, new(fx0, fz1), new(ex1, bz1));
            Grating(g, new(fx1, fz0), new(ex1, fz1));
            Grating(g, new(ex0, bz1), new(ex1, nz0));                         // 동벽 구간(굴뚝 뒤)
            Grating(g, new(ex1, ShutterZ0 - .1f), new(MachineWallX, ShutterZ1 + .1f)); // 셔터 앞(벽 기둥 사이)
            Grating(g, new(nx0, nz0), new(ex1, nz1));                         // 북벽 구간
            Railing(g, true, ex0, ez0, bz0);
            Railing(g, false, bz0, bx0, ex0);
            Railing(g, true, bx0, bz0, bz1);
            Railing(g, false, bz1, bx0, ex0);
            Railing(g, true, ex0, bz1, nz0);
            Railing(g, false, ez0, ex0, ex1);
            Railing(g, false, nz0, nx0, ex0);
            foreach (var (x, z) in new[] { (ex0 + .05f, 79.5f), (ex0 + .05f, 82.5f), (ex0 + .05f, 85.5f), (ex0 + .05f, 91.5f), (ex0 + .05f, 94.5f), (ex0 + .05f, 97.5f), (14.3f, nz0 + .05f), (17.3f, nz0 + .05f), (20.3f, nz0 + .05f) })
            {
                var center = new Vector3(x, -7.6f, z);
                if (Physics.CheckBox(center, new Vector3(.1f, 2.45f, .05f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue; // 아래 설비와 겹치면 기둥을 두지 않는다
                Deco(g, "CatwalkPost", iron, new(x - .1f, -10f, z - .05f), new(x + .1f, Catwalk - .1f, z + .05f)).AddComponent<BoxCollider>();
            }
        }

        static void Grating(Transform g, Vector2 min, Vector2 max)
        {
            Deco(g, "Grating", iron, new(min.x, Catwalk - .1f, min.y), new(max.x, Catwalk, max.y)).AddComponent<BoxCollider>();
            Deco(g, "GratingEdge", rusty, new(min.x, Catwalk - .3f, min.y), new(max.x, Catwalk - .1f, max.y));
        }

        // 난간(0.5m·1.0m 가로대 + 1.5m 간격 기둥) + 보이지 않는 막음 충돌체. alongZ면 x=at 선을 따라 a0~a1(z).
        static void Railing(Transform g, bool alongZ, float at, float a0, float a1)
        {
            float top = Catwalk;
            foreach (float y in new[] { top + .5f, top + 1f })
                Deco(g, "Rail", iron, alongZ ? new(at - .05f, y, a0) : new(a0, y, at - .05f), alongZ ? new(at + .05f, y + .1f, a1) : new(a1, y + .1f, at + .05f));
            int n = Mathf.Max(1, Mathf.CeilToInt((a1 - a0) / 1.5f));
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)n);
                Deco(g, "RailPost", iron, alongZ ? new(at - .03f, top, a - .03f) : new(a - .03f, top, at - .03f), alongZ ? new(at + .03f, top + 1.1f, a + .03f) : new(a + .03f, top + 1.1f, at + .03f));
            }
            var guard = new GameObject("RailGuard"); guard.transform.SetParent(g, false);
            var box = guard.AddComponent<BoxCollider>();
            box.center = alongZ ? new Vector3(at, top + .55f, (a0 + a1) * .5f) : new Vector3((a0 + a1) * .5f, top + .55f, at);
            box.size = alongZ ? new Vector3(.1f, 1.1f, a1 - a0) : new Vector3(a1 - a0, 1.1f, .1f);
        }

        // ---- 환풍구 덮개(부수기: 맨몸 3대·천막 말뚝 뽑개 1대) ----

        // hinge: 덮개 아래 가운데(방 쪽 면), yaw 90이면 방 쪽이 -x. latch: 손이 닿는 지점(덮개와 다른 계층이라 반대쪽에서는 덮개에 막힌다).
        static void Panel(Transform parent, string name, string targetId, string label, Vector3 hinge, float yaw, float width, float height, Vector3 latch)
        {
            var root = Group(parent, name);
            var frame = Group(root, "Frame");
            frame.SetPositionAndRotation(hinge, Quaternion.Euler(0, yaw, 0));
            var intact = Group(frame, "Grille"); VentGrille(intact, width, height);
            var knocked = Group(frame, "KnockedGrille"); knocked.localRotation = Quaternion.Euler(-105f, 0, 0); VentGrille(knocked, width, height);
            knocked.gameObject.SetActive(false);
            var blockerGo = new GameObject("Blocker"); blockerGo.transform.SetParent(frame, false);
            var blocker = blockerGo.AddComponent<BoxCollider>();
            blocker.center = new Vector3(0, height * .5f, .02f); blocker.size = new Vector3(width + 2 * T, height + 2 * T, .04f);
            var latchGo = new GameObject("Latch"); latchGo.transform.SetParent(root, false);
            latchGo.transform.position = latch;
            latchGo.SetActive(false);
            var gate = latchGo.AddComponent<ExplorationGate>();
            var so = new SerializedObject(gate);
            so.FindProperty("targetId").stringValue = targetId;
            so.FindProperty("label").stringValue = label;
            so.FindProperty("kind").enumValueIndex = (int)ExplorationGateKind.Breakable;
            so.FindProperty("toolId").stringValue = ItemUses.StakePuller;
            so.FindProperty("requiredHits").intValue = 1;
            so.FindProperty("bareHandHits").intValue = 3;
            so.FindProperty("blocker").objectReferenceValue = blocker;
            var hide = so.FindProperty("hideOnOpen"); hide.arraySize = 1; hide.GetArrayElementAtIndex(0).objectReferenceValue = intact.gameObject;
            var show = so.FindProperty("showOnOpen"); show.arraySize = 1; show.GetArrayElementAtIndex(0).objectReferenceValue = knocked.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
            latchGo.SetActive(true);
        }

        // 환풍구 덮개 모양(로컬: x=가로, y=위, z=-가 방 쪽): 테두리, 비스듬한 날개, 가운데 받침, 모서리 볼트.
        static void VentGrille(Transform g, float width, float height)
        {
            float w = width + 2 * T + .04f, hh = height + 2 * T + .04f, half = w * .5f, rim = .07f, depth = .05f;
            void Bar(string n, Material m, Vector3 center, Vector3 size, Quaternion rotation)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                c.name = n; c.transform.SetParent(g, false);
                Object.DestroyImmediate(c.GetComponent<Collider>());
                c.transform.localPosition = center; c.transform.localRotation = rotation; c.transform.localScale = size;
                c.GetComponent<Renderer>().sharedMaterial = m;
            }
            var none = Quaternion.identity;
            Bar("Rim", steel, new(0, rim * .5f - T, -depth * .5f), new(w, rim, depth), none);
            Bar("Rim", steel, new(0, hh - rim * .5f - T, -depth * .5f), new(w, rim, depth), none);
            Bar("Rim", steel, new(-half + rim * .5f, hh * .5f - T, -depth * .5f), new(rim, hh, depth), none);
            Bar("Rim", steel, new(half - rim * .5f, hh * .5f - T, -depth * .5f), new(rim, hh, depth), none);
            int louvers = Mathf.RoundToInt(hh / .14f); float span = hh - 2 * rim;
            for (int i = 0; i < louvers; i++)
                Bar("Louver", sheet, new(0, rim - T + span * (i + .5f) / louvers, -depth * .45f), new(w - 2 * rim, span / louvers * 1.05f, .012f), Quaternion.Euler(-40f, 0, 0));
            Bar("Brace", steel, new(0, hh * .5f - T, -.004f), new(.03f, hh - 2 * rim, .008f), none);
            foreach (var (bx, by) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
                Bar("Bolt", iron, new(bx * (half - rim * .5f), (by < 0 ? rim * .5f : hh - rim * .5f) - T, -depth - .006f), new(.035f, .035f, .012f), none);
        }

        // ---- 메시 ----

        static GameObject Slab(Transform parent, string name, Material material, Vector3 min, Vector3 max)
        {
            var go = Hex(parent, name, material, min, max);
            go.AddComponent<BoxCollider>();
            go.AddComponent<CameraCollisionSurface>().LocalBounds = go.GetComponent<MeshFilter>().sharedMesh.bounds;
            return go;
        }

        static GameObject Deco(Transform parent, string name, Material material, Vector3 min, Vector3 max) => Hex(parent, name, material, min, max);

        // 상자 메시(월드 좌표, 면마다 1.25m 타일 UV).
        static GameObject Hex(Transform parent, string name, Material material, Vector3 min, Vector3 max)
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
            var mesh = new Mesh { name = "LA_DormVent_" + parent.name + "_" + name + "_" + meshes.Count };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            meshes.Add(mesh);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        static void SaveMeshes()
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(MeshAssetPath) != null) AssetDatabase.DeleteAsset(MeshAssetPath);
            AssetDatabase.CreateAsset(meshes[0], MeshAssetPath);
            for (int i = 1; i < meshes.Count; i++) AssetDatabase.AddObjectToAsset(meshes[i], MeshAssetPath);
            AssetDatabase.SaveAssets();
        }

        // ---- 벽 구멍(이 씬만) ----

        // 이전 실행이 만든 구멍을 되돌린다: _VentCut 조각을 지우고 꺼 둔 원본(_VentOriginal)을 켠다. 벽 메시는 원본으로 되돌린다.
        static string RestoreWalls(Transform layout)
        {
            int restored = 0;
            foreach (var t in layout.GetComponentsInChildren<Transform>(true))
                if (t != null && t.name.EndsWith(CutSuffix)) { Undo.DestroyObjectImmediate(t.gameObject); restored++; }
            foreach (var t in layout.GetComponentsInChildren<Transform>(true))
                if (t.name.EndsWith(OriginalSuffix))
                {
                    Undo.RecordObject(t.gameObject, "Restore vent wall");
                    t.name = t.name.Substring(0, t.name.Length - OriginalSuffix.Length);
                    t.gameObject.SetActive(true);
                }
            foreach (var mf in layout.GetComponentsInChildren<MeshFilter>(true))
            {
                string path = AssetDatabase.GetAssetPath(mf.sharedMesh);
                if (!path.EndsWith("_Vent.asset")) continue;
                var source = AssetDatabase.LoadAssetAtPath<Mesh>(path.Replace("_Vent.asset", ".asset"));
                if (source == null) continue;
                Undo.RecordObject(mf, "Restore vent wall"); mf.sharedMesh = source; restored++;
            }
            return restored > 0 ? $"이전 벽 구멍 되돌림 {restored}곳." : "";
        }

        // 동쪽(x가 일정한) 벽 하나에 구멍을 낸다: 벽 면 메시 사본 + 벽 충돌체·벽판 조각. 구멍과 겹치는 벽 부품만 다룬다.
        internal static string CutWall(Transform surfaceT, Transform[] solidParents, Bounds[] holes, string label)
        {
            var surface = surfaceT != null ? surfaceT.GetComponent<MeshFilter>() : null;
            if (surface == null) return $"{label}: 벽 면을 찾지 못해 구멍을 내지 않았습니다.";
            string source = AssetDatabase.GetAssetPath(surface.sharedMesh);
            string copyPath = source.Replace(".asset", "_Vent.asset");
            var copy = Object.Instantiate(surface.sharedMesh); copy.name = surface.sharedMesh.name + "_Vent";
            foreach (var hole in holes)
                if (!CutQuad(copy, surface.transform, hole)) return $"{label}: 벽 면에서 구멍 자리를 찾지 못했습니다.";
            if (AssetDatabase.LoadAssetAtPath<Object>(copyPath) != null) AssetDatabase.DeleteAsset(copyPath);
            AssetDatabase.CreateAsset(copy, copyPath);
            Undo.RecordObject(surface, "Vent wall"); surface.sharedMesh = copy;
            int solids = 0;
            foreach (var hole in holes)
                foreach (var parent in solidParents)
                {
                    if (parent == null) continue;
                    foreach (var t in parent.GetComponentsInChildren<Transform>())
                    {
                        if (t == null || t == parent) continue;
                        var renderer = t.GetComponent<MeshRenderer>(); var filter = t.GetComponent<MeshFilter>(); var box = t.GetComponent<BoxCollider>();
                        bool cube = renderer != null && filter != null && filter.sharedMesh != null && filter.sharedMesh.name == "Cube";
                        if (!cube && (box == null || renderer != null)) continue;
                        var bb = cube ? renderer.bounds : box.bounds;
                        if (!bb.Intersects(hole)) continue;
                        if (Split(t, cube ? null : box, bb, hole)) solids++;
                    }
                }
            return $"{label} 구멍(벽 면·벽 부품 {solids}).";
        }

        // 벽 면 사각형(두 삼각형) 중 구멍을 품은 것을 구멍 둘레 사각형으로 바꾼다. 바닥에서 시작하는 구멍(셔터)은 아래 조각 없이 자른다.
        static bool CutQuad(Mesh mesh, Transform owner, Bounds hole)
        {
            var v = new List<Vector3>(mesh.vertices); var uv = new List<Vector2>(mesh.uv); var nrm = new List<Vector3>(mesh.normals);
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var tris = new List<int>(mesh.GetTriangles(s));
                for (int t = 0; t + 5 < tris.Count; t += 3)
                {
                    var idx = new HashSet<int> { tris[t], tris[t + 1], tris[t + 2], tris[t + 3], tris[t + 4], tris[t + 5] };
                    if (idx.Count != 4) continue;
                    var ids = new List<int>(idx); var w = ids.ConvertAll(i => owner.TransformPoint(v[i]));
                    float plane = w[0].x;
                    if (!w.TrueForAll(p => Mathf.Abs(p.x - plane) < .01f) || plane < hole.min.x || plane > hole.max.x) continue;
                    float a0 = Mathf.Min(w[0].z, w[1].z, w[2].z, w[3].z), a1 = Mathf.Max(w[0].z, w[1].z, w[2].z, w[3].z);
                    float y0 = Mathf.Min(w[0].y, w[1].y, w[2].y, w[3].y), y1 = Mathf.Max(w[0].y, w[1].y, w[2].y, w[3].y);
                    float hy0 = hole.min.y <= y0 + .05f ? y0 : hole.min.y;
                    if (hole.min.z <= a0 || hole.max.z >= a1 || hole.min.y < y0 - .1f || hole.max.y >= y1) continue;
                    bool At(int i, float a, float y) { var p = owner.TransformPoint(v[i]); return Mathf.Abs(p.z - a) < .001f && Mathf.Abs(p.y - y) < .001f; }
                    int iMin = ids.FindIndex(i => At(i, a0, y0)), iMax = ids.FindIndex(i => At(i, a1, y1));
                    if (iMin < 0 || iMax < 0) continue;
                    Vector2 uvA = uv[ids[iMin]], uvB = uv[ids[iMax]]; var normal = nrm[ids[0]];
                    bool flip = Vector3.Dot(Vector3.Cross(v[tris[t + 1]] - v[tris[t]], v[tris[t + 2]] - v[tris[t]]), normal) < 0;
                    tris.RemoveRange(t, 6);
                    foreach (var (p0, p1, b0, b1) in new[] { (a0, hole.min.z, y0, y1), (hole.max.z, a1, y0, y1), (hole.min.z, hole.max.z, y0, hy0), (hole.min.z, hole.max.z, hole.max.y, y1) })
                    {
                        if (b1 - b0 < .001f) continue;
                        int start = v.Count;
                        foreach (var (pa, py) in new[] { (p0, b0), (p0, b1), (p1, b1), (p1, b0) })
                        {
                            v.Add(owner.InverseTransformPoint(new Vector3(plane, py, pa))); nrm.Add(normal);
                            uv.Add(new Vector2(Mathf.Lerp(uvA.x, uvB.x, (pa - a0) / (a1 - a0)), Mathf.Lerp(uvA.y, uvB.y, (py - y0) / (y1 - y0))));
                        }
                        var quad = new[] { start, start + 1, start + 2, start, start + 2, start + 3 };
                        if (Vector3.Dot(Vector3.Cross(v[start + 1] - v[start], v[start + 2] - v[start]), normal) < 0 != flip) System.Array.Reverse(quad);
                        tris.AddRange(quad);
                    }
                    mesh.SetVertices(v); mesh.SetNormals(nrm); mesh.SetUVs(0, uv); mesh.SetTriangles(tris, s);
                    mesh.RecalculateBounds(); mesh.RecalculateTangents();
                    return true;
                }
            }
            return false;
        }

        // 벽 부품(큐브 또는 충돌체만 있는 상자)을 구멍 둘레 조각(_VentCut)으로 대신하고 원본은 꺼 둔다(_VentOriginal).
        internal static bool Split(Transform t, BoxCollider box, Bounds b, Bounds hole)
        {
            var pieces = new List<(Vector3, Vector3)>();
            if (hole.min.z > b.min.z) pieces.Add((new(b.min.x, b.min.y, b.min.z), new(b.max.x, b.max.y, hole.min.z)));
            if (hole.max.z < b.max.z) pieces.Add((new(b.min.x, b.min.y, hole.max.z), new(b.max.x, b.max.y, b.max.z)));
            float m0 = Mathf.Max(b.min.z, hole.min.z), m1 = Mathf.Min(b.max.z, hole.max.z);
            if (hole.min.y > b.min.y) pieces.Add((new(b.min.x, b.min.y, m0), new(b.max.x, Mathf.Min(hole.min.y, b.max.y), m1)));
            if (hole.max.y < b.max.y) pieces.Add((new(b.min.x, Mathf.Max(hole.max.y, b.min.y), m0), new(b.max.x, b.max.y, m1)));
            foreach (var (min, max) in pieces)
            {
                if (max.y - min.y < .01f || max.z - min.z < .01f) continue;
                var piece = Object.Instantiate(t.gameObject, t.parent).transform;
                piece.name = t.name + CutSuffix;
                for (int i = piece.childCount - 1; i >= 0; i--) Object.DestroyImmediate(piece.GetChild(i).gameObject);
                Undo.RegisterCreatedObjectUndo(piece.gameObject, "Vent wall");
                Fit(piece, box != null ? piece.GetComponent<BoxCollider>() : null, b, new Bounds((min + max) * .5f, max - min));
            }
            Undo.RecordObject(t.gameObject, "Vent wall");
            t.name += OriginalSuffix;
            t.gameObject.SetActive(false);
            return true;
        }

        static void Fit(Transform t, BoxCollider box, Bounds from, Bounds to)
        {
            if (box != null)
            {
                Vector3 a = t.InverseTransformPoint(to.min), b = t.InverseTransformPoint(to.max);
                box.center = (a + b) * .5f;
                box.size = new Vector3(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y), Mathf.Abs(b.z - a.z));
                return;
            }
            t.position += to.center - from.center;
            var scale = t.localScale;
            for (int axis = 0; axis < 3; axis++)
            {
                var world = Vector3.zero; world[axis] = 1f;
                var local = t.InverseTransformDirection(world);
                int j = Mathf.Abs(local.x) >= Mathf.Abs(local.y) && Mathf.Abs(local.x) >= Mathf.Abs(local.z) ? 0 : Mathf.Abs(local.y) >= Mathf.Abs(local.z) ? 1 : 2;
                scale[j] *= to.size[axis] / Mathf.Max(.0001f, from.size[axis]);
            }
            t.localScale = scale;
        }

        // 기계실 동벽(x=23.3 면)의 벽 면 메시·벽 충돌체 묶음.
        static Transform FindWallSurface(Transform machine)
        {
            foreach (var mf in machine.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.name != "WallSurfaces") continue;
                var b = mf.GetComponent<Renderer>().bounds;
                if (Mathf.Abs(b.max.x - MachineWallX) < .05f && b.min.y <= Catwalk + .05f && b.max.y > ShutterTop) return mf.transform;
            }
            return null;
        }

        static Transform FindWallStructure(Transform machine)
        {
            foreach (var box in machine.GetComponentsInChildren<BoxCollider>())
                if (box.name == "WallCollider" && Mathf.Abs(box.bounds.min.x - MachineWallX) < .05f && box.bounds.min.y <= Catwalk + .05f && box.bounds.max.y > ShutterTop)
                    return box.transform.parent;
            return null;
        }

        // ---- 검사: 새 충돌체·판이 기존 물체와 겹치는지(충돌체와 보이는 물체 모두). 고치지 않고 보고만 한다 ----

        static string Validate(params Transform[] roots)
        {
            Physics.SyncTransforms();
            var issues = new List<string>();
            bool Ours(Transform t) { foreach (var r in roots) if (r != null && t.IsChildOf(r)) return true; return false; }
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            foreach (var root in roots)
            {
                if (root == null) continue;
                foreach (var col in root.GetComponentsInChildren<Collider>())
                {
                    if (col.isTrigger) continue;
                    var b = col.bounds;
                    string group = col.transform.parent != null ? col.transform.parent.name : "";
                    bool throughWall = group is "Junction" or "ToRestroom" or "ToRoom" or "Shutter" or "ShutterSlat" || col.name == "ShutterBlocker";
                    foreach (var other in Physics.OverlapBox(b.center, b.extents * .98f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (Ours(other.transform) || other.isTrigger) continue;
                        if (throughWall && other.name.EndsWith(CutSuffix)) continue;
                        if (col.name == "Grating" && other.name == "Grating") continue;
                        issues.Add("충돌 " + group + "/" + col.name + "↔" + other.transform.parent?.name + "/" + other.name);
                    }
                    var shrunk = new Bounds(b.center, b.size * .98f);
                    foreach (var r in renderers)
                    {
                        if (r == null || !r.enabled || Ours(r.transform) || r.bounds.size.magnitude > 30f || r.name.EndsWith(CutSuffix) || r.name == "WallSurfaces") continue;
                        if (throughWall && (r.name.StartsWith("OuterWall") || r.name.StartsWith("SteelLedger"))) continue;
                        if (r.bounds.Intersects(shrunk)) issues.Add("겹침 " + group + "/" + col.name + "↔" + r.transform.parent?.name + "/" + r.name);
                    }
                }
            }
            return issues.Count == 0 ? "없음" : string.Join(", ", issues.GetRange(0, Mathf.Min(20, issues.Count))) + (issues.Count > 20 ? $" 외 {issues.Count - 20}" : "");
        }
    }
}
