using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.WaitingRoom.EditorTools
{
    /// <summary>
    /// 대기실 1단계 그레이박스(①~⑧)를 활성 씬에 다시 만든다. 기준: 2차 기획서 3.3.1 조감도(약 0.06m/px), 로비 중심 = 원점, 조감도 위쪽 = +Z.
    /// 공간마다 무엇인지 알아볼 정도의 형태만 단순 도형으로 둔다(실제 에셋 적용은 후순위). 스폰 위치·⑥ 집결 판정 컴포넌트도 함께 붙인다.
    /// </summary>
    public static class WaitingRoomGreyboxBuilder
    {
        const string RootName = "WaitingRoomGreybox";
        const string MaterialFolder = "Assets/Scenes/Tests/WaitingRoom/Base/Materials";
        const float WallHeight = 5f;
        const float WallThickness = 0.3f;
        const float DoorHeight = 3f;
        static readonly Vector3 RootPosition = new(0f, 0f, 70f);

        static readonly Dictionary<string, Color> Palette = new()
        {
            { "GB_Floor", new Color(0.35f, 0.35f, 0.35f) },
            { "GB_Wall", new Color(0.6f, 0.6f, 0.6f) },
            { "GB_Marker", new Color(0.9f, 0.55f, 0.2f) },
            { "GB_Door", new Color(0.7f, 0.15f, 0.15f) },
            { "GB_Zone", new Color(0.2f, 0.6f, 1f) },
            { "GB_Wood", new Color(0.45f, 0.3f, 0.18f) },
            { "GB_Metal", new Color(0.25f, 0.28f, 0.32f) },
            { "GB_Cream", new Color(0.9f, 0.85f, 0.7f) },
            { "GB_Fabric", new Color(0.4f, 0.28f, 0.5f) },
            { "GB_Ceramic", new Color(0.85f, 0.9f, 0.95f) },
            { "GB_Gold", new Color(0.85f, 0.65f, 0.2f) },
            { "GB_Paper", new Color(0.95f, 0.93f, 0.85f) },
            { "GB_Mirror", new Color(0.6f, 0.75f, 0.85f) },
            { "GB_Dark", new Color(0.08f, 0.08f, 0.1f) },
        };

        static readonly Dictionary<string, Material> materials = new();

        [MenuItem("Tools/CurtainCall/대기실/그레이박스 다시 만들기")]
        static void Rebuild()
        {
            var scene = SceneManager.GetActiveScene();
            var root = Build(scene);
            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(scene);
        }

        /// <summary>씬의 기존 그레이박스를 지우고 다시 만든다.</summary>
        public static GameObject Build(Scene scene)
        {
            foreach (var old in scene.GetRootGameObjects())
                if (old.name == RootName) Undo.DestroyObjectImmediate(old);

            materials.Clear();
            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = RootPosition;

            var t = root.transform;
            var walls = Group(t, "Walls");
            BuildLobby(Group(t, "S01_Lobby"));
            BuildDormitory(Group(t, "S02_Dormitory"));
            BuildShop(Group(t, "S03_Shop"));
            BuildPropRoom(Group(t, "S04_PropRoom"));
            BuildJunkStorage(Group(t, "S05_JunkStorage"));
            BuildStageCorridor(Group(t, "S06_StageCorridor_Temp"));
            BuildStageEntrance(Group(t, "S07_StageEntrance_Temp"));
            BuildExpertRoom(Group(t, "S08_ExpertRoom"));
            BuildWalls(walls);
            walls.SetAsLastSibling();

            Undo.RegisterCreatedObjectUndo(root, "Build Waiting Room Greybox");
            return root;
        }

        // ── 벽 ─────────────────────────────────────────────

        static void BuildWalls(Transform p)
        {
            var none = new (float, float, float)[0];
            Wall(p, "W_x-10", false, -10f, -15.5f, 16f, new[] { (0f, 3f, DoorHeight) });                       // 로비 ↔ ⑤
            Wall(p, "W_x+10", false, 10f, -15.5f, 16f, new[] { (0f, 3f, DoorHeight), (6.5f, 2f, DoorHeight) }); // 대기실 문(로비 ↔ ⑥), 로비 ↔ ⑧
            Wall(p, "W_z+9", true, 9f, -18f, 10f, new[] { (-4f, 3f, DoorHeight) });                           // 로비 ↔ ②
            Wall(p, "W_z-9", true, -9f, -18f, 10f, new[] { (-2f, 3f, DoorHeight) });                          // 로비 ↔ ④
            Wall(p, "W_02_Partition_x3", false, 3f, 9f, 16f, new[] { (11f, 1.5f, 2.5f) });                   // 숙소 ↔ 화장실
            Wall(p, "W_02_North", true, 16f, -10f, 10f, none);
            Wall(p, "W_04_South", true, -15.5f, -10f, 10f, none);
            Wall(p, "W_05_West", false, -18f, -9f, 9f, none);
            Wall(p, "W_08_North", true, 9f, 10f, 18f, none);
            Wall(p, "W_08_East", false, 18f, 4f, 9f, none);
            Wall(p, "W_06_North_08_South", true, 4f, 10f, 37f, none);
            Wall(p, "W_06_South", true, -4f, 10f, 37f, none);
            Wall(p, "W_06_East", false, 37f, -4f, 4f, none);
        }

        /// <summary>X축(z 고정) 또는 Z축(x 고정) 벽. 구멍 = (중심, 폭, 높이), 높이 위는 상인방으로 막는다.</summary>
        static void Wall(Transform parent, string name, bool alongX, float k, float from, float to, (float c, float w, float h)[] openings)
        {
            var w = Group(parent, name);
            var list = new List<(float c, float w, float h)>(openings);
            list.Sort((a, b) => a.c.CompareTo(b.c));
            float cursor = from;
            int index = 0;
            void Segment(float s0, float s1, float y0, float y1)
            {
                if (s1 - s0 < 0.01f) return;
                float mid = (s0 + s1) / 2f, len = s1 - s0;
                var center = alongX ? new Vector3(mid, (y0 + y1) / 2f, k) : new Vector3(k, (y0 + y1) / 2f, mid);
                var size = alongX ? new Vector3(len, y1 - y0, WallThickness) : new Vector3(WallThickness, y1 - y0, len);
                Box(w, "Seg" + index++, center, size, "GB_Wall");
            }
            foreach (var o in list)
            {
                Segment(cursor, o.c - o.w / 2f, 0f, WallHeight);
                Segment(o.c - o.w / 2f, o.c + o.w / 2f, o.h, WallHeight);
                cursor = o.c + o.w / 2f;
            }
            Segment(cursor, to, 0f, WallHeight);
        }

        // ── ① 대기실 로비 ──────────────────────────────────

        static void BuildLobby(Transform p)
        {
            Floor(p, -10f, 10f, -9f, 9f);
            Label(p, "① 대기실 로비", new Vector3(0f, 0f, -5.3f));

            // 원형 서커스 천막: 빨강/크림 벽판 16장(남쪽 입구 2칸 비움) + 계단식 지붕 + 기둥·무대
            var tent = Group(p, "CircusTent");
            const float radius = 4.5f;
            for (int i = 0; i < 16; i++)
            {
                if (i == 7 || i == 8) continue;
                float deg = (i + 0.5f) * 22.5f, rad = deg * Mathf.Deg2Rad;
                var panel = Box(tent, "Panel_" + i, new Vector3(Mathf.Sin(rad) * radius, 1.5f, Mathf.Cos(rad) * radius),
                    new Vector3(1.8f, 3f, 0.12f), i % 2 == 0 ? "GB_Door" : "GB_Cream");
                panel.transform.localRotation = Quaternion.Euler(0f, deg, 0f);
            }
            Cyl(tent, "Roof", new Vector3(0f, 3.2f, 0f), 4.8f, 0.4f, "GB_Cream", false);

            // 안내판(대기실 문 옆) · 스케줄표(북쪽 벽)
            var info = Group(p, "InfoBoard");
            Box(info, "Board", new Vector3(8.4f, 1.2f, 3.2f), new Vector3(0.1f, 2.4f, 1.4f), "GB_Wood");
            Sign(info, "안내판", new Vector3(8.33f, 2.6f, 3.2f), 90f);

            var schedule = Group(p, "ScheduleBoard");
            Box(schedule, "Board", new Vector3(-7f, 1.9f, 8.8f), new Vector3(2.4f, 1.4f, 0.06f), "GB_Paper", false);
            Sign(schedule, "스케줄표", new Vector3(-7f, 2.9f, 8.76f), 0f);

            // 대기실 문(⑥ 쪽으로 열린 문틀)
            Box(p, "WaitingRoomDoor_Frame", new Vector3(10f, 3.1f, 0f), new Vector3(0.4f, 0.25f, 3.4f), "GB_Gold", false);

            // 로비에서 보이는 출입구 표지
            Sign(p, "② 숙소", new Vector3(-4f, 3.6f, 8.83f), 0f);
            Sign(p, "④ 소품실", new Vector3(-2f, 3.6f, -8.83f), 180f);
            Sign(p, "⑤ 폐기물 보관소", new Vector3(-9.83f, 3.6f, 0f), -90f);
            Sign(p, "대기실 문 → ⑥", new Vector3(9.83f, 3.6f, 0f), 90f);

            // ① 로비 스폰 4곳(천막 쪽을 본다)
            var spawnRoot = Group(p, "SpawnPoints");
            var spawns = new[] { new Vector3(-2.5f, 0f, -6.5f), new Vector3(2.5f, 0f, -6.5f), new Vector3(-6.5f, 0f, -3f), new Vector3(6.5f, 0f, -3f) };
            var points = new Transform[spawns.Length];
            for (int i = 0; i < spawns.Length; i++)
            {
                var s = new GameObject("Spawn_" + (i + 1)).transform;
                s.SetParent(spawnRoot, false);
                s.localPosition = spawns[i];
                points[i] = s;
                Cyl(spawnRoot, "SpawnMark_" + (i + 1), spawns[i] + Vector3.up * 0.01f, 0.35f, 0.02f, "GB_Zone", false);
                Label(spawnRoot, (i + 1).ToString(), spawns[i] + Vector3.up * 0.03f, 0.15f);
            }
            var spawnPoints = spawnRoot.gameObject.AddComponent<PlayerSpawnPoints>();
            var so = new SerializedObject(spawnPoints);
            var array = so.FindProperty("points");
            array.arraySize = points.Length;
            for (int i = 0; i < points.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            spawnRoot.gameObject.AddComponent<WaitingRoomSpawn>();
        }

        // ── ② 숙소(+화장실) ────────────────────────────────

        static void BuildDormitory(Transform p)
        {
            Floor(p, -10f, 10f, 9f, 16f);
            Label(p, "② 숙소", new Vector3(-4f, 0f, 11.8f));
            Label(p, "② 화장실", new Vector3(6.5f, 0f, 12.2f));

            var beds = Group(p, "Beds");
            float[] bedX = { -8.6f, -6.6f, -2.4f, -0.4f };
            for (int i = 0; i < bedX.Length; i++)
            {
                var b = Group(beds, "Bed_" + (i + 1));
                float x = bedX[i];
                Box(b, "Frame", new Vector3(x, 0.25f, 14.75f), new Vector3(1.1f, 0.5f, 2.1f), "GB_Metal");
                Box(b, "Mattress", new Vector3(x, 0.55f, 14.7f), new Vector3(1f, 0.1f, 1.9f), "GB_Cream", false);
            }

            Box(p, "Lockers", new Vector3(-9.55f, 1f, 11f), new Vector3(0.55f, 2f, 2.8f), "GB_Metal");
            Sign(p, "사물함", new Vector3(-9.25f, 2.3f, 11f), 90f);
            Box(p, "Wardrobe", new Vector3(1.6f, 1.1f, 15.45f), new Vector3(2f, 2.2f, 0.8f), "GB_Wood");
            Sign(p, "옷장", new Vector3(1.6f, 2.4f, 15.03f), 0f);

            // 이전 세대가 남긴 벽 낙서
            Sign(p, "낙서", new Vector3(-7.6f, 2.2f, 15.83f), 0f, Color.black, 0.12f);

            // 화장실: 칸막이·변기 3칸 + 세면대·거울
            var restroom = Group(p, "Restroom");
            foreach (float x in new[] { 4.3f, 6.1f, 7.9f })
                Box(restroom, "Partition", new Vector3(x, 1f, 14.97f), new Vector3(0.06f, 2f, 1.75f), "GB_Fabric");
            foreach (float x in new[] { 5.2f, 7f, 8.8f })
                Box(restroom, "Toilet", new Vector3(x, 0.4f, 15.45f), new Vector3(0.5f, 0.8f, 0.7f), "GB_Ceramic");
            Box(restroom, "Sinks", new Vector3(6.9f, 0.425f, 9.45f), new Vector3(4.6f, 0.85f, 0.55f), "GB_Ceramic");
            Box(restroom, "Mirror", new Vector3(6.9f, 1.75f, 9.18f), new Vector3(4f, 0.9f, 0.04f), "GB_Mirror", false);
            Sign(p, "② 화장실", new Vector3(2.83f, 2.7f, 11f), 90f);
        }

        // ── ③ 상점(로비 안 기둥: 수화기·배송구) ─────────────

        static void BuildShop(Transform p)
        {
            Label(p, "③ 상점", new Vector3(6.4f, 0f, -8.1f));
            var pillar = new Vector3(8f, WallHeight / 2f, -6.8f);
            Box(p, "Pillar", pillar, new Vector3(0.8f, WallHeight, 0.8f), "GB_Metal");

            // 수화기: 로비 중앙을 보는 서쪽 면 / 배송구: 북쪽 면
            Box(p, "Phone", new Vector3(7.5f, 1.45f, -6.8f), new Vector3(0.2f, 0.5f, 0.32f), "GB_Gold", false);
            Sign(p, "수화기", new Vector3(7.58f, 2.05f, -6.8f), 90f);
            Box(p, "DeliverySlot", new Vector3(8f, 0.95f, -6.35f), new Vector3(0.6f, 0.5f, 0.12f), "GB_Dark", false);
            Sign(p, "배송구", new Vector3(8f, 1.45f, -6.38f), 180f);

            Sign(p, "③ 상점", new Vector3(7.58f, 3.2f, -6.8f), 90f);
        }

        // ── ④ 소품실(+분장실) ──────────────────────────────

        static void BuildPropRoom(Transform p)
        {
            Floor(p, -10f, 10f, -15.5f, -9f);
            Label(p, "④ 소품실", new Vector3(-1f, 0f, -10.3f));

            // 서쪽: 의상 보관
            Box(p, "CostumeStorage", new Vector3(-8.6f, 1f, -12.5f), new Vector3(1.6f, 2f, 4.4f), "GB_Fabric");
            Sign(p, "의상 보관", new Vector3(-8.6f, 2.3f, -10.27f), 180f);

            // 남쪽 벽 가운데: 물려줄 선반(4칸) — 선반 칸이 보이게 판 4장
            var handover = Group(p, "HandoverShelf_4Tier");
            Shelf(handover, new Vector3(-1f, 0f, -15.05f), 4f, 0.6f, 4, 0.6f, "GB_Gold");
            Sign(handover, "물려줄 선반(4칸)", new Vector3(-1f, 2.75f, -14.7f), 180f);

            // 가운데: 소품 선반 2줄
            Box(p, "PropShelf_A", new Vector3(-3f, 0.9f, -11.6f), new Vector3(4f, 1.8f, 0.7f), "GB_Metal");
            Box(p, "PropShelf_B", new Vector3(2.5f, 0.9f, -11.6f), new Vector3(4f, 1.8f, 0.7f), "GB_Metal");
            Sign(p, "소품 선반", new Vector3(-0.25f, 2.1f, -11.2f), 0f);

            // 낡은 인형·인형탈
            Box(p, "OldDolls", new Vector3(3f, 0.25f, -14.9f), new Vector3(1.6f, 0.5f, 0.6f), "GB_Cream");
            Cyl(p, "MascotSuit", new Vector3(5f, 0.8f, -14.7f), 0.35f, 1.6f, "GB_Fabric");
            Sign(p, "낡은 인형·인형탈", new Vector3(4f, 1.9f, -15.3f), 180f);

            // 동쪽: 분장대·거울, 분장실 옷장
            Box(p, "DressingTable", new Vector3(9.5f, 0.4f, -12.3f), new Vector3(0.6f, 0.8f, 3.6f), "GB_Wood");
            Box(p, "DressingMirror", new Vector3(9.82f, 1.6f, -12.3f), new Vector3(0.04f, 1f, 3f), "GB_Mirror", false);
            Sign(p, "분장대·거울", new Vector3(9.78f, 2.5f, -12.3f), 90f);
            Box(p, "DressingWardrobe", new Vector3(8.7f, 1.1f, -15.0f), new Vector3(1.6f, 2.2f, 0.6f), "GB_Wood");
            Sign(p, "분장실 옷장", new Vector3(8.7f, 2.4f, -14.68f), 180f);
        }

        // ── ⑤ 폐기물 보관소 ────────────────────────────────

        static void BuildJunkStorage(Transform p)
        {
            Floor(p, -18f, -10f, -9f, 9f);
            Label(p, "⑤ 폐기물 보관소", new Vector3(-12.6f, 0f, 3.2f));

            // 북쪽: 공연 기록 노트(책상) + 기록 책장
            Box(p, "PerformanceLogDesk", new Vector3(-14.5f, 0.4f, 7f), new Vector3(2f, 0.8f, 1f), "GB_Wood");
            Box(p, "PerformanceLogNote", new Vector3(-14.5f, 0.83f, 7f), new Vector3(0.6f, 0.05f, 0.4f), "GB_Paper", false);
            Sign(p, "공연 기록 노트", new Vector3(-14.5f, 1.6f, 7.52f), 0f);

            // 서쪽: 이전 세대 코스튬(마네킹 3)
            for (int i = 0; i < 3; i++)
                Cyl(p, "OldCostume_" + (i + 1), new Vector3(-17.2f, 0.9f, 2.2f + i * 1.2f), 0.25f, 1.8f, "GB_Fabric");
            Sign(p, "이전 세대 코스튬", new Vector3(-17.83f, 2.4f, 3.4f), -90f);

            // 가운데: 사용하지 않는(망가진) 서커스 기물
            var broken = Box(p, "BrokenProps", new Vector3(-14f, 0.4f, 0f), new Vector3(1.6f, 0.8f, 1.6f), "GB_Marker");
            broken.transform.localRotation = Quaternion.Euler(0f, 25f, 0f);
            Sign(p, "사용하지 않는 기물", new Vector3(-14f, 1.4f, 0f), -90f);

            // 서쪽 남쪽: 정리된 보관 선반 / 남쪽: 서커스 관련 기록물
            Box(p, "StorageShelf", new Vector3(-17.4f, 1.1f, -3.5f), new Vector3(0.6f, 2.2f, 4f), "GB_Metal");
            Sign(p, "정리된 보관 선반", new Vector3(-17.05f, 2.5f, -3.5f), -90f);
            Box(p, "CircusRecords", new Vector3(-15.2f, 0.7f, -8.5f), new Vector3(3f, 1.4f, 0.6f), "GB_Paper");
            Sign(p, "서커스 관련 기록물", new Vector3(-15.2f, 1.9f, -8.18f), 180f);

            // 탈출 힌트 낱장·이전 세대 벽 낙서
            Box(p, "HintNote", new Vector3(-12.2f, 0.01f, 4.3f), new Vector3(0.25f, 0.01f, 0.32f), "GB_Paper", false);
            Sign(p, "힌트 낱장", new Vector3(-12.2f, 0.3f, 4.3f), -90f, null, 0.08f);
            Sign(p, "낙서", new Vector3(-17.83f, 2.1f, 0.5f), -90f, Color.black, 0.12f);
        }

        // ── ⑥ 무대 입구 통로(테스트 대체) ─────────────────

        static void BuildStageCorridor(Transform p)
        {
            Floor(p, 10f, 37f, -4f, 4f);
            Label(p, "⑥ 무대 입구 통로 (대체)", new Vector3(17f, 0f, 0f));

            // 붉은 커튼 방: 열림 = 드나들 수 있음(어두운 안쪽이 보임), 닫힘 = 드나들 수 없음
            var rooms = Group(p, "CurtainRooms");
            float[] xs = { 14f, 19f, 24f, 29f };
            bool[] northOpen = { true, false, false, true };
            bool[] southOpen = { false, true, false, false };
            for (int i = 0; i < xs.Length; i++)
            {
                CurtainRoom(rooms, "N_" + (i + 1), xs[i], 3.7f, northOpen[i]);
                CurtainRoom(rooms, "S_" + (i + 1), xs[i], -3.7f, southOpen[i]);
            }

            // 집결 판정 영역(전원이 들어오면 공연으로)
            var zone = Box(p, "GatherZone", new Vector3(26f, 1.5f, 0f), new Vector3(14f, 3f, 7f), "GB_Zone");
            zone.GetComponent<BoxCollider>().isTrigger = true;
            zone.GetComponent<MeshRenderer>().enabled = false;
            zone.AddComponent<StageEntranceGather>();
            var border = Group(p, "GatherZone_Border");
            Box(border, "N", new Vector3(26f, 0.01f, 3.45f), new Vector3(14f, 0.02f, 0.1f), "GB_Zone", false);
            Box(border, "S", new Vector3(26f, 0.01f, -3.45f), new Vector3(14f, 0.02f, 0.1f), "GB_Zone", false);
            Box(border, "W", new Vector3(19.05f, 0.01f, 0f), new Vector3(0.1f, 0.02f, 7f), "GB_Zone", false);
            Box(border, "E", new Vector3(32.95f, 0.01f, 0f), new Vector3(0.1f, 0.02f, 7f), "GB_Zone", false);
            Label(border, "집결 판정 영역", new Vector3(26f, 0.02f, -2.6f), 0.18f);
        }

        static void CurtainRoom(Transform parent, string name, float x, float z, bool open)
        {
            var room = Group(parent, (open ? "CurtainRoom_Open_" : "CurtainRoom_Closed_") + name);
            float face = Mathf.Sign(z);
            Box(room, "Valance", new Vector3(x, 3.15f, z), new Vector3(3.2f, 0.3f, 0.3f), "GB_Wood", false);
            if (open)
            {
                Box(room, "DarkRoom", new Vector3(x, 1.5f, z + face * 0.12f), new Vector3(2.4f, 3f, 0.03f), "GB_Dark", false);
                Box(room, "Bunch_L", new Vector3(x - 1.35f, 1.5f, z), new Vector3(0.4f, 3f, 0.25f), "GB_Door");
                Box(room, "Bunch_R", new Vector3(x + 1.35f, 1.5f, z), new Vector3(0.4f, 3f, 0.25f), "GB_Door");
            }
            else
            {
                for (int i = 0; i < 4; i++)
                    Box(room, "Fold_" + i, new Vector3(x - 1.125f + i * 0.75f, 1.5f, z + (i % 2 == 0 ? 0f : face * 0.08f)), new Vector3(0.75f, 3f, 0.18f), "GB_Door");
            }
            Sign(room, open ? "열림" : "닫힘", new Vector3(x, 3.45f, z - face * 0.17f), face > 0 ? 180f : 0f);
        }

        // ── ⑦ 공연 스테이지 입구 ───────────────────────────

        static void BuildStageEntrance(Transform p)
        {
            Label(p, "⑦ 공연 스테이지 입구", new Vector3(33.6f, 0f, 0f));
            for (int i = 0; i < 8; i++)
                Box(p, "CurtainFold_" + i, new Vector3(i % 2 == 0 ? 36.55f : 36.7f, 2.3f, -3.33f + i * 0.95f), new Vector3(0.22f, 4.6f, 0.97f), "GB_Door");
            Box(p, "Valance", new Vector3(36.5f, 4.65f, 0f), new Vector3(0.4f, 0.5f, 7.7f), "GB_Gold", false);
            Sign(p, "⑦ 공연 스테이지 입구", new Vector3(36.27f, 4.1f, 0f), 90f);
        }

        // ── ⑧ 전문가 대기실 ────────────────────────────────

        static void BuildExpertRoom(Transform p)
        {
            Floor(p, 10f, 18f, 4f, 9f);
            Label(p, "⑧ 전문가 대기실", new Vector3(13f, 0f, 5f));

            // 평소 닫힌 붉은 커튼 문(지나갈 수 없음)
            Box(p, "ExpertRoomDoor_Closed", new Vector3(10.2f, DoorHeight / 2f, 6.5f), new Vector3(0.15f, DoorHeight, 2f), "GB_Door");
            Sign(p, "⑧ 전문가 대기실", new Vector3(9.83f, 3.6f, 6.5f), 90f);

            // 낡은 소파·탁자
            Box(p, "Sofa", new Vector3(17.3f, 0.4f, 6.5f), new Vector3(1f, 0.8f, 2.4f), "GB_Fabric");
            Box(p, "Table", new Vector3(15.6f, 0.25f, 6.5f), new Vector3(1f, 0.5f, 1.4f), "GB_Wood");
            Sign(p, "소파·탁자", new Vector3(17.6f, 2.5f, 6.5f), 90f);
        }

        // ── 도형 도우미 ────────────────────────────────────

        static Transform Group(Transform parent, string name)
        {
            var g = new GameObject(name).transform;
            g.SetParent(parent, false);
            return g;
        }

        static void Floor(Transform p, float x0, float x1, float z0, float z1) =>
            Box(p, "Floor", new Vector3((x0 + x1) / 2f, -0.1f, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.2f, z1 - z0), "GB_Floor");

        static GameObject Primitive(PrimitiveType type, Transform parent, string name, Vector3 center, Vector3 scale, string material, bool solid)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = Material(material);
            if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static GameObject Box(Transform p, string name, Vector3 center, Vector3 size, string material, bool solid = true) =>
            Primitive(PrimitiveType.Cube, p, name, center, size, material, solid);

        static GameObject Cyl(Transform p, string name, Vector3 center, float radius, float height, string material, bool solid = true) =>
            Primitive(PrimitiveType.Cylinder, p, name, center, new Vector3(radius * 2f, height / 2f, radius * 2f), material, solid);

        /// <summary>바닥 판자 + 기둥 4개인 선반. <paramref name="width"/> = X, <paramref name="depth"/> = Z.</summary>
        static void Shelf(Transform p, Vector3 baseCenter, float width, float depth, int tiers, float spacing, string material)
        {
            var shelf = Group(p, "Shelf");
            float height = tiers * spacing + 0.1f;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Box(shelf, "Upright", baseCenter + new Vector3(sx * (width / 2f - 0.03f), height / 2f, sz * (depth / 2f - 0.03f)), new Vector3(0.06f, height, 0.06f), "GB_Metal");
            for (int i = 0; i < tiers; i++)
                Box(shelf, "Board_" + i, baseCenter + new Vector3(0f, 0.3f + i * spacing, 0f), new Vector3(width, 0.05f, depth), material, false);
        }

        /// <summary>바닥에 눕힌 큰 글씨(위에서 내려다볼 때).</summary>
        static void Label(Transform p, string text, Vector3 position, float size = 0.25f)
        {
            var g = new GameObject("Label");
            g.transform.SetParent(p, false);
            g.transform.localPosition = position + Vector3.up * 0.02f;
            g.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Text(g, text, size, Color.white);
        }

        /// <summary>벽·기둥에 세운 표지. <paramref name="yaw"/> 0 = 남쪽에서 북쪽을 보며 읽음, 90 = 서쪽에서, 180 = 북쪽에서, -90 = 동쪽에서.</summary>
        static void Sign(Transform p, string text, Vector3 position, float yaw, Color? color = null, float size = 0.12f)
        {
            var g = new GameObject("Sign_" + text);
            g.transform.SetParent(p, false);
            g.transform.localPosition = position;
            g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Text(g, text, size, color ?? Color.white);
        }

        static void Text(GameObject g, string text, float size, Color color)
        {
            var tm = g.AddComponent<TextMesh>();
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            g.GetComponent<MeshRenderer>().sharedMaterial = TextMaterial(tm.font);
            tm.text = text;
            tm.characterSize = size;
            tm.fontSize = 48;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
        }

        /// <summary>표지 글자 재질(깊이 검사로 벽 뒤 글자를 가린다).</summary>
        static Material TextMaterial(Font font)
        {
            const string name = "GB_Text";
            if (materials.TryGetValue(name, out var cached) && cached != null) return cached;
            string path = $"{MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("CurtainCall/GreyboxText"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.mainTexture = font.material.mainTexture;
            materials[name] = mat;
            return mat;
        }

        static Material Material(string name)
        {
            if (materials.TryGetValue(name, out var cached) && cached != null) return cached;
            string path = $"{MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.SetColor("_BaseColor", Palette[name]);
                AssetDatabase.CreateAsset(mat, path);
            }
            materials[name] = mat;
            return mat;
        }
    }
}
