using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.WaitingRoom.PassageGimmicks
{
    // 새 공간의 세부: 사무실 복도, 샤워실 겸 화장실, 2층 컨테이너 창고(2026-11-12 사용자 지시).
    public static partial class FinalLayoutBuilder
    {
        // 창고: 이전 세대 생활 장소 홀 서쪽 끝(무대 장치 보관실 동벽에 붙음)에 서 있는 2층 컨테이너 사무실/창고.
        // 아래 단은 닫힌 컨테이너, 위 단(바닥 2.9m)이 들어갈 수 있는 창고다. 창문을 넘으면 보관실 안 바퀴 받침(1m) 위로 내려선다.
        const float YardH = 7.5f, DeckY = 2.9f;
        const float BoxX0 = -26.62f, BoxX1 = -24.02f, BoxZ0 = 37.2f, BoxZ1 = 44.6f, BoxTop = 5.5f;
        // 무대 장치 보관실로 넘어가는 창문(컨테이너 서쪽 면과 보관실 동벽을 함께 뚫는다).
        const float WinZ0 = 40.2f, WinZ1 = 41.8f, WinY0 = 3.3f, WinY1 = 5.2f;
        // 바깥 계단: 컨테이너 동쪽 긴 면을 따라 남쪽 바닥에서 북쪽 발판으로 오른다. 발판 옆 긴 면에 문.
        const float StairX0 = BoxX1, StairX1 = BoxX1 + 1.2f, StairBottomZ = 39.6f, StairTopZ = 43f;
        const float DoorZ0 = 43.2f, DoorZ1 = 44.2f, DoorTop = DeckY + 2.05f;

        static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>(AssetFolder + name + ".mat");

        // ---- 사무실 복도: 낡은 사무동 복도. 허리 높이 초록 칠, 불투명 유리창, 닫힌 사무실 문, 형광등 ----
        static void BuildOfficeCorridor(Transform cor)
        {
            Material green = Mat("LA_AbandonedInstitutionGreen"), beige = Mat("LA_OldPlasticBeige"), glass = Mat("LA_DeadScreenGlass"),
                tube = Mat("LA_WhiteWorkBulb"), paper = Mat("LA_Paper");
            float x0 = CorX0, x1 = CorX1;
            // 낮은 천장 판(3m)은 두지 않는다: 위의 진짜 천장(5.2m)과 두 겹으로 보이고, 배관 연결 통로 쪽에서 중간을 막는 판처럼 보였다(2026-11-13 사용자).
            const float top = CorH;
            // 벽 칠: 실제 문(생활 장소 문 x -3~-1)은 비운다.
            foreach (var (z, inward, gaps) in new[] { (CorZ1, -1f, new (float, float)[0]), (CorZ0, 1f, new[] { (-3.1f, -.9f) }) })
            {
                float cursor = x0;
                foreach (var (g0, g1) in gaps.Append((x1, x1)))
                {
                    if (g0 > cursor)
                    {
                        float a = Mathf.Min(z, z + inward * .03f), b = Mathf.Max(z, z + inward * .03f);
                        Deco(cor, "WallPaintLow", green, new(cursor, 0, a), new(g0, 1.05f, b));
                        Deco(cor, "WallPaintHigh", beige, new(cursor, 1.05f, a), new(g0, top, b));
                        Deco(cor, "ChairRail", mSteel, new(cursor, 1.02f, Mathf.Min(z, z + inward * .05f)), new(g0, 1.08f, Mathf.Max(z, z + inward * .05f)));
                    }
                    cursor = g1;
                }
            }
            // 닫힌 사무실 문(구조도의 회색 문): 북벽 2, 남벽 1, 서쪽 끝 1. 열리지 않는다.
            OfficeDoor(cor, new Vector3(-1.5f, 0, CorZ1 - .05f), 0f, green, glass, paper);
            OfficeDoor(cor, new Vector3(9.5f, 0, CorZ1 - .05f), 0f, green, glass, paper);
            OfficeDoor(cor, new Vector3(9.5f, 0, CorZ0 + .05f), 180f, green, glass, paper);
            OfficeDoor(cor, new Vector3(x0 + .05f, 0, (CorZ0 + CorZ1) * .5f), -90f, green, glass, paper);
            // 안이 비치지 않는 유리창.
            foreach (float x in new[] { -10.6f, -7.6f, -4.6f, 2f, 5f }) OfficeWindow(cor, new Vector3(x, 1.25f, CorZ1 - .05f), 0f, glass);
            foreach (float x in new[] { -10.6f, -7.6f, 2.5f, 5.5f }) OfficeWindow(cor, new Vector3(x, 1.25f, CorZ0 + .05f), 180f, glass);
            // 형광등: 하나는 꺼져 있다.
            foreach (var (x, lit) in new[] { (-10f, true), (-3.5f, false), (3f, true), (9.5f, true) })
            {
                Deco(cor, "TubeHousing", mSteel, new(x - .7f, top - .08f, 48.2f), new(x + .7f, top, 48.5f));
                Deco(cor, "Tube", lit ? tube : beige, new(x - .62f, top - .12f, 48.3f), new(x + .62f, top - .08f, 48.4f));
                if (!lit) continue;
                var l = new GameObject("TubeLight").AddComponent<Light>(); l.transform.SetParent(cor, false); l.transform.position = new Vector3(x, top - .5f, 48.35f);
                l.type = LightType.Point; l.color = new Color(.86f, .93f, 1f); l.intensity = 6f; l.range = 9f; l.shadows = LightShadows.None;
            }
        }

        // 벽에 붙은 닫힌 사무실 문. position은 문 아래 가운데, yaw 0이면 문 면이 -z를 본다.
        static void OfficeDoor(Transform parent, Vector3 position, float yaw, Material leaf, Material glass, Material paper)
        {
            var g = new GameObject("ClosedOfficeDoor").transform; g.SetParent(parent, false); g.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            Part(g, "Leaf", leaf, new(-.5f, 0, -.05f), new(.5f, 2.1f, 0));
            Part(g, "Frame", mSteel, new(-.6f, 0, -.07f), new(-.5f, 2.2f, .01f));
            Part(g, "Frame", mSteel, new(.5f, 0, -.07f), new(.6f, 2.2f, .01f));
            Part(g, "Frame", mSteel, new(-.6f, 2.1f, -.07f), new(.6f, 2.2f, .01f));
            Part(g, "FrostedPane", glass, new(-.3f, 1.2f, -.06f), new(.3f, 1.85f, -.05f));
            Part(g, "Knob", mRustPatina, new(.33f, .98f, -.11f), new(.42f, 1.07f, -.05f));
            Part(g, "NamePlate", paper, new(-.2f, 1.9f, -.06f), new(.2f, 2.0f, -.05f));
            Part(g, "KickPlate", mSteel, new(-.48f, .02f, -.06f), new(.48f, .28f, -.05f));
        }

        static void OfficeWindow(Transform parent, Vector3 sillCentre, float yaw, Material glass)
        {
            var g = new GameObject("FrostedWindow").transform; g.SetParent(parent, false); g.SetPositionAndRotation(sillCentre, Quaternion.Euler(0, yaw, 0));
            Part(g, "Pane", glass, new(-.9f, 0, -.04f), new(.9f, 1.1f, -.02f));
            Part(g, "Frame", mSteel, new(-.96f, -.06f, -.06f), new(.96f, 0, 0));
            Part(g, "Frame", mSteel, new(-.96f, 1.1f, -.06f), new(.96f, 1.16f, 0));
            Part(g, "Frame", mSteel, new(-.96f, 0, -.06f), new(-.9f, 1.1f, 0));
            Part(g, "Frame", mSteel, new(.9f, 0, -.06f), new(.96f, 1.1f, 0));
            Part(g, "Mullion", mSteel, new(-.02f, 0, -.06f), new(.02f, 1.1f, 0));
        }

        // 묶음(g)의 로컬 좌표로 상자 부품을 단다.
        static void Part(Transform g, string name, Material material, Vector3 lo, Vector3 hi)
        {
            var go = Deco(g, name, material, lo, hi);
            go.transform.localPosition = (lo + hi) * .5f; go.transform.localRotation = Quaternion.identity;
        }

        // ---- 샤워실 겸 화장실: 변기·세면대는 벽에 붙이고 남벽에 샤워 칸 ----
        static int BuildWashroom(Transform live, Transform layout)
        {
            Material tile = Mat("LA_TealTile"), knob = Mat("LA_RedKnob"), puddle = Mat("LA_PuddleDecal");
            var fx = layout.Find("S02_Dormitory/Restroom/Fixtures"); int count = 0;
            float west = LiveX0, east = SubSplitX0, south = SubZ0;
            Deco(live, "WashroomTile", mTile, new(west, 0, south), new(east, .012f, SubZ1));
            // 벽 타일(허리 위까지).
            Deco(live, "WallTile", tile, new(west, 0, south), new(west + .02f, 2.1f, SubZ1));
            Deco(live, "WallTile", tile, new(west, 0, south), new(east, 2.1f, south + .02f));
            Deco(live, "WallTile", tile, new(east - .02f, 0, south), new(east, 2.1f, SubZ1));
            // 변기 3개: 서벽에 등을 붙인다. 칸막이도 벽에서 시작한다.
            var toilets = fx.Cast<Transform>().Where(t => t.name.StartsWith("Toilet_")).ToArray();
            var partition = fx.Cast<Transform>().FirstOrDefault(t => t.name == "StallPartition");
            float z = 33.3f;
            foreach (var toilet in toilets)
            {
                var t = Put(toilet, live, new Vector2(west + .6f, z), 180f); if (t == null) continue;
                Snap(t, minX: west + .03f); count++;
                if (partition != null) { var p = Put(partition, live, new Vector2(west + .9f, z + .6f), 180f); Snap(p, minX: west + .03f); count++; }
                z += 1.2f;
            }
            // 세면대와 거울: 동벽.
            float sz = 34.9f;
            foreach (var name in new[] { "Sink_Left", "Sink_Right" })
            {
                var s = Put(fx.Find(name), live, new Vector2(east - .5f, sz), 180f); if (s != null) { Snap(s, maxX: east - .03f); count++; }
                sz -= 1.7f;
            }
            var mirrors = Put(fx.Find("SinkMirrors"), live, new Vector2(east - .1f, 34.05f), 180f); if (mirrors != null) { Snap(mirrors, maxX: east - .025f); count++; }
            // 샤워 칸 3개: 남벽. 배관·샤워기·밸브·배수구·낮은 칸막이.
            var drain = fx.Find("Drain");
            foreach (float x in new[] { -16.4f, -13.8f, -11.2f })
            {
                var g = new GameObject("ShowerBay").transform; g.SetParent(live, false); g.position = new Vector3(x, 0, south);
                Part(g, "Riser", mSteel, new(-.025f, 0, .05f), new(.025f, 2.15f, .1f));
                Part(g, "Arm", mSteel, new(-.02f, 2.1f, .05f), new(.02f, 2.14f, .42f));
                Part(g, "Head", mRustPatina, new(-.09f, 2.02f, .34f), new(.09f, 2.1f, .52f));
                Part(g, "Valve", knob, new(-.06f, 1.08f, .1f), new(.06f, 1.2f, .16f));
                Part(g, "SoapShelf", mSteel, new(.25f, 1.1f, .03f), new(.55f, 1.13f, .16f));
                Part(g, "Partition", tile, new(-1.34f, 0, 0), new(-1.26f, 1.9f, 1.25f));
                if (drain != null) { var placed = Put(drain, live, new Vector2(x, south + .75f), 0f); if (placed != null) placed.transform.position += Vector3.up * .012f; count++; } // 타일(1.2cm)·고인 물 위로 올린다(윗면이 겹쳐 깜빡였음)
            }
            Deco(live, "ShowerEndPartition", tile, new(-9.94f, 0, south), new(-9.86f, 1.9f, south + 1.25f));
            if (puddle != null) Deco(live, "WetFloor", puddle, new(-15.6f, .016f, south + .3f), new(-12.2f, .02f, south + 2.2f));
            return count;
        }

        // ---- 홀 안 2층 컨테이너 창고: 바깥 계단 → 발판 → 옆문 → 안쪽 창문 → 무대 장치 보관실 캣워크 ----
        static string BuildContainerStorage(Transform root, Transform layout)
        {
            Material plate = Mat("LA_DiamondPlate"), blue = Mat("LA_RackBlue"), orange = Mat("LA_RackOrange"), glass = Mat("LA_DeadScreenGlass");
            var yard = Child(root, "StorageYard");
            // 홀 서쪽 끝 바닥·천장·벽(생활 장소와 한 공간).
            Slab(yard, "Floor", mFloor, new(StoreX0, -.2f, StoreZ0), new(LiveX0, 0, StoreZ1));
            Slab(yard, "Ceiling", mCeil, new(StoreX0, YardH, StoreZ0 - T), new(StoreX1, YardH + .2f, StoreZ1 + T));
            Wall(yard, "NorthWall", mWall, new(StoreX0, 0, StoreZ1), new(StoreX1, YardH, StoreZ1 + T));
            Wall(yard, "SouthWall", mWall, new(StoreX0, 0, StoreZ0 - T), new(StoreX1, YardH, StoreZ0));

            Container(yard, "Container_Low", orange, glass, 0f, DeckY - .08f, false);
            var box = Container(yard, "Container_Storage", blue, glass, DeckY - .08f, BoxTop + .08f, true);
            Bulb(box, new Vector3((BoxX0 + BoxX1) * .5f, BoxTop - .85f, 40.2f), 6f, 6f, new Color(1f, .78f, .52f));

            // 바깥 계단과 발판.
            var walk = Child(yard, "OuterStair");
            Slab(walk, "Landing", plate, new(StairX0, DeckY - .08f, StairTopZ), new(StairX1, DeckY, BoxZ1));
            foreach (float z in new[] { StairTopZ + .08f, BoxZ1 - .08f }) Deco(walk, "Post", mSteel, new(StairX1 - .12f, 0, z - .06f), new(StairX1, DeckY - .08f, z + .06f));
            Rail(walk, new Vector3(StairX1 - .03f, DeckY, StairTopZ), new Vector3(StairX1 - .03f, DeckY, BoxZ1));
            Rail(walk, new Vector3(StairX0, DeckY, BoxZ1 - .03f), new Vector3(StairX1, DeckY, BoxZ1 - .03f));
            float run = StairTopZ - StairBottomZ, length = Mathf.Sqrt(run * run + DeckY * DeckY), angle = Mathf.Atan2(DeckY, run) * Mathf.Rad2Deg;
            var mid = new Vector3((StairX0 + StairX1) * .5f, DeckY * .5f, (StairTopZ + StairBottomZ) * .5f);
            var slope = Quaternion.Euler(-angle, 0, 0); // +z(북쪽)로 갈수록 올라간다
            var ramp = new GameObject("StairRamp"); ramp.transform.SetParent(walk, false); ramp.transform.SetPositionAndRotation(mid - slope * Vector3.up * .06f, slope);
            ramp.AddComponent<BoxCollider>().size = new Vector3(StairX1 - StairX0, .12f, length);
            int steps = Mathf.RoundToInt(DeckY / .255f); float rise = DeckY / steps, go = run / steps;
            for (int i = 0; i < steps; i++)
                Deco(walk, "Tread", plate, new(StairX0, (i + 1) * rise - .04f, StairBottomZ + i * go), new(StairX1, (i + 1) * rise, StairBottomZ + (i + 1) * go));
            // 계단 바깥쪽(동쪽): 옆판, 손잡이, 난간살, 떨어짐 방지 충돌체.
            float sideX = StairX1 - .03f;
            foreach (var (name, lift, height) in new[] { ("Stringer", -.14f, .26f), ("Handrail", 1.02f, .06f) })
            {
                var bar = GameObject.CreatePrimitive(PrimitiveType.Cube); bar.name = name; bar.transform.SetParent(walk, false);
                bar.transform.SetPositionAndRotation(new Vector3(sideX, mid.y + lift, mid.z), slope); bar.transform.localScale = new Vector3(.05f, height, length);
                bar.GetComponent<Renderer>().sharedMaterial = mSteel; Object.DestroyImmediate(bar.GetComponent<Collider>());
            }
            for (int i = 1; i < steps; i += 3) { float y = (i + .5f) * rise, z = StairBottomZ + (i + .5f) * go; Deco(walk, "Baluster", mSteel, new(sideX - .02f, y, z - .02f), new(sideX + .02f, y + 1.02f, z + .02f)); }
            var guard = new GameObject("StairGuard"); guard.transform.SetParent(walk, false); guard.transform.SetPositionAndRotation(new Vector3(sideX, mid.y + .55f, mid.z), slope);
            guard.AddComponent<BoxCollider>().size = new Vector3(.06f, 1.1f, length);

            // 홀 바닥의 잡동사니와 높은 조명.
            int props = 0;
            var barrel = layout.Find("S01_Lobby/Frame8Lobby/AbandonedFactory/SM_Barrel_1");
            foreach (var p in new[] { new Vector2(-23.4f, 44.5f), new Vector2(-23.5f, 43.6f), new Vector2(-21.4f, 35.8f) }) props += Clone(barrel, yard, p, p.x * 37f);
            props += Clone(layout.Find("S04_PropRoom/ShelfZone/SouthGeneralPropShelf"), box, new Vector2((BoxX0 + BoxX1) * .5f, 37.75f), 180f, floorY: DeckY);
            props += Clone(barrel, box, new Vector2(BoxX0 + .6f, 39.3f), 20f, floorY: DeckY);
            Bulb(yard, new Vector3(-22f, YardH - .9f, 40.5f), 11f, 14f, new Color(1f, .78f, .52f));

            // 무대 장치 보관실 동벽: 이전 구멍은 되돌리고, 위 단 높이에 창문을 낸다.
            var hole = new Bounds(new Vector3(-26.85f, (WinY0 + WinY1) * .5f, (WinZ0 + WinZ1) * .5f), new Vector3(1.5f, WinY1 - WinY0, WinZ1 - WinZ0));
            var dock = layout.Find("FacilityConnections/CargoDockConnection");
            foreach (var t in dock.Cast<Transform>().ToArray())
            {
                if (!t.name.StartsWith("CargoOuterReturn")) continue;
                if (t.name.EndsWith("_VentCut")) Undo.DestroyObjectImmediate(t.gameObject);
                else if (t.name.EndsWith("_VentOriginal")) { Undo.RecordObject(t.gameObject, "Restore wall"); t.name = "CargoOuterReturn"; t.gameObject.SetActive(true); }
            }
            var wall = dock.Cast<Transform>().FirstOrDefault(t => t.name == "CargoOuterReturn" && t.gameObject.activeSelf && t.GetComponent<Renderer>().bounds.Intersects(hole));
            if (wall == null) return $"창고 가구 {props}개 / 보관실 동벽을 찾지 못해 창문을 내지 못했습니다";
            DormVentBuilder.Split(wall, null, wall.GetComponent<Renderer>().bounds, hole);
            var frame = Child(yard, "WindowFrame");
            // 창틀은 뚫린 면보다 1cm 안쪽으로 나오게(면이 같으면 깜빡인다).
            Deco(frame, "Sill", mSteel, new(-27.02f, WinY0 - .05f, WinZ0 - .05f), new(BoxX0 + .09f, WinY0 + .01f, WinZ1 + .05f));
            Deco(frame, "Head", mSteel, new(-27.02f, WinY1 - .01f, WinZ0 - .05f), new(BoxX0 + .09f, WinY1 + .05f, WinZ1 + .05f));
            Deco(frame, "Jamb", mSteel, new(-27.02f, WinY0 + .01f, WinZ0 - .05f), new(BoxX0 + .09f, WinY1 - .01f, WinZ0 + .01f));
            Deco(frame, "Jamb", mSteel, new(-27.02f, WinY0 + .01f, WinZ1 - .01f), new(BoxX0 + .09f, WinY1 - .01f, WinZ1 + .05f));
            return $"창고 가구 {props}개 / 보관실 동벽 창문(z {WinZ0}~{WinZ1}, y {WinY0}~{WinY1})";
        }

        // 난간 한 구간: 윗대·중간대·기둥 + 충돌체. a, b는 바닥 높이의 양 끝.
        static void Rail(Transform parent, Vector3 a, Vector3 b)
        {
            Vector3 lo = Vector3.Min(a, b), hi = Vector3.Max(a, b), pad = new(.025f, 0, .025f);
            Deco(parent, "RailTop", mSteel, lo - pad + Vector3.up * 1.02f, hi + pad + Vector3.up * 1.08f);
            Deco(parent, "RailMid", mSteel, lo - pad + Vector3.up * .5f, hi + pad + Vector3.up * .54f);
            float span = Vector3.Distance(a, b); int n = Mathf.Max(1, Mathf.RoundToInt(span / .9f));
            for (int i = 0; i <= n; i++) { var p = Vector3.Lerp(a, b, i / (float)n); Deco(parent, "RailPost", mSteel, p - pad, p + pad + Vector3.up * 1.05f); }
            var guard = new GameObject("RailGuard"); guard.transform.SetParent(parent, false); guard.transform.position = (lo + hi) * .5f + Vector3.up * .55f;
            guard.AddComponent<BoxCollider>().size = hi - lo + new Vector3(.06f, 1.1f, .06f);
        }

        // 컨테이너 한 단. open이면 안이 빈 창고(동쪽 긴 면에 문, 서쪽 면에 넘어가는 창문), 아니면 닫힌 상자.
        static Transform Container(Transform parent, string name, Material paint, Material glass, float y0, float y1, bool open)
        {
            var g = Child(parent, name); const float t = .08f;
            if (!open) Slab(g, "Body", paint, new(BoxX0, y0, BoxZ0), new(BoxX1, y1, BoxZ1));
            else
            {
                Slab(g, "Floor", paint, new(BoxX0, y0, BoxZ0), new(BoxX1, y0 + t, BoxZ1));
                Slab(g, "Roof", paint, new(BoxX0, y1 - t, BoxZ0), new(BoxX1, y1, BoxZ1));
                // 끝판은 옆판 사이에 끼운다(모서리에서 바깥 면이 겹치면 깜빡인다).
                Slab(g, "NorthEnd", paint, new(BoxX0 + t, y0 + t, BoxZ1 - t), new(BoxX1 - t, y1 - t, BoxZ1 - .005f));
                Slab(g, "SouthEnd", paint, new(BoxX0 + t, y0 + t, BoxZ0 + .005f), new(BoxX1 - t, y1 - t, BoxZ0 + t));
                // 동쪽 면: 발판 옆 문 둘레.
                Slab(g, "EastSide", paint, new(BoxX1 - t, y0 + t, BoxZ0), new(BoxX1, y1 - t, DoorZ0));
                Slab(g, "EastSide", paint, new(BoxX1 - t, y0 + t, DoorZ1), new(BoxX1, y1 - t, BoxZ1));
                Slab(g, "EastSide", paint, new(BoxX1 - t, DoorTop, DoorZ0), new(BoxX1, y1 - t, DoorZ1));
                Deco(g, "DoorFrame", mSteel, new(BoxX1 - t - .01f, y0 + t, DoorZ0 - .05f), new(BoxX1 + .02f, DoorTop - .01f, DoorZ0 + .01f));
                Deco(g, "DoorFrame", mSteel, new(BoxX1 - t - .01f, y0 + t, DoorZ1 - .01f), new(BoxX1 + .02f, DoorTop - .01f, DoorZ1 + .05f));
                Deco(g, "DoorFrame", mSteel, new(BoxX1 - t - .01f, DoorTop - .01f, DoorZ0 - .05f), new(BoxX1 + .02f, DoorTop + .05f, DoorZ1 + .05f));
                // 안으로 열어 둔 문짝.
                Deco(g, "DoorLeaf", mSteel, new(BoxX1 - t - .95f, y0 + t, DoorZ1 - .05f), new(BoxX1 - t, DoorTop, DoorZ1));
                // 서쪽 면: 창문 둘레 네 조각.
                Slab(g, "WestSide", paint, new(BoxX0, y0 + t, BoxZ0), new(BoxX0 + t, y1 - t, WinZ0));
                Slab(g, "WestSide", paint, new(BoxX0, y0 + t, WinZ1), new(BoxX0 + t, y1 - t, BoxZ1));
                Slab(g, "WestSide", paint, new(BoxX0, y0 + t, WinZ0), new(BoxX0 + t, WinY0, WinZ1));
                Slab(g, "WestSide", paint, new(BoxX0, WinY1, WinZ0), new(BoxX0 + t, y1 - t, WinZ1));
            }
            // 동쪽 긴 면의 틀 창(안이 비치지 않는 장식)과 골판 주름.
            var panes = open ? new[] { 38.2f, 40.6f } : new[] { 38.2f, 40.6f, 43f };
            foreach (float z in panes)
            {
                float sill = y0 + 1.05f;
                Deco(g, "WindowPane", glass, new(BoxX1 + .01f, sill, z), new(BoxX1 + .03f, sill + .95f, z + 1.3f));
                Deco(g, "WindowFrame", mSteel, new(BoxX1, sill - .05f, z - .05f), new(BoxX1 + .05f, sill, z + 1.35f));
                Deco(g, "WindowFrame", mSteel, new(BoxX1, sill + .95f, z - .05f), new(BoxX1 + .05f, sill + 1f, z + 1.35f));
                Deco(g, "WindowFrame", mSteel, new(BoxX1, sill, z - .05f), new(BoxX1 + .05f, sill + .95f, z));
                Deco(g, "WindowFrame", mSteel, new(BoxX1, sill, z + 1.3f), new(BoxX1 + .05f, sill + .95f, z + 1.35f));
                Deco(g, "WindowFrame", mSteel, new(BoxX1, sill, z + .63f), new(BoxX1 + .05f, sill + .95f, z + .67f));
            }
            bool Clear(float z) => panes.All(p => z < p - .1f || z > p + 1.4f) && (!open || z < DoorZ0 - .1f || z > DoorZ1 + .1f);
            for (float z = BoxZ0 + .25f; z < BoxZ1; z += .4f) if (Clear(z)) Deco(g, "Rib", paint, new(BoxX1, y0 + .1f, z - .05f), new(BoxX1 + .035f, y1 - .1f, z + .05f));
            for (float x = BoxX0 + .25f; x < BoxX1; x += .4f) Deco(g, "Rib", paint, new(x - .05f, y0 + .1f, BoxZ0 - .035f), new(x + .05f, y1 - .1f, BoxZ0));
            foreach (var c in new[] { new Vector2(BoxX1, BoxZ0), new Vector2(BoxX1, BoxZ1), new Vector2(BoxX0, BoxZ0) })
                Deco(g, "CornerPost", mRustPatina, new(c.x - .06f, y0, c.y - .06f), new(c.x + .06f, y1, c.y + .06f));
            return g;
        }

        // 기존 물건 복제본을 돌려 놓고 Transform을 돌려준다(게임 스크립트·조명 제거).
        static Transform Put(Transform source, Transform parent, Vector2 centre, float yaw)
        {
            if (source == null) return null;
            int before = parent.childCount;
            if (Clone(source, parent, centre, yaw) == 0) return null;
            return parent.GetChild(before);
        }

        // 복제본의 겉 범위 한쪽 면을 벽에 맞춘다.
        static void Snap(Transform t, float minX = float.NaN, float maxX = float.NaN)
        {
            if (t == null) return; var b = Bounds(t);
            if (!float.IsNaN(minX)) t.position += Vector3.right * (minX - b.min.x);
            if (!float.IsNaN(maxX)) t.position += Vector3.right * (maxX - b.max.x);
        }
    }
}
