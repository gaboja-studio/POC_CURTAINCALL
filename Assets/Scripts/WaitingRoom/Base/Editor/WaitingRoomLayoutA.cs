using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CurtainCall.Network.PlayerSync;
using CurtainCall.UI.SceneIntegration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.Editor
{
    // Task-075 Layout A: builds the bird's-eye plan "대기실 공간 구조 A" as an abandoned factory hall that once housed a circus.
    // Tone follows Task-060 Layout/global.md: darkness by default, warm bulb pools only at functional points,
    // dark walls and pipes, rusted broken concrete, a faded tent as the only saturated element.
    // The preserved WaitingRoomGreybox is only read and copied. Only generated LayoutA roots are replaced.
    public static class WaitingRoomLayoutA
    {
        const string ScenePath = "Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity";
        const string AssetDir = "Assets/Scenes/Tests/WaitingRoom/Base/LayoutA";
        const string FactoryPrefabs = "Assets/_ThirdParty/LeartesStudios/AbandonedFactory/HDRP/Art/Prefabs/";
        const string FactoryTextures = "Assets/_ThirdParty/LeartesStudios/AbandonedFactory/HDRP/Art/Textures/";
        const string WaitingMaterials = "Assets/_ThirdParty/Materials/WaitingArea/";
        const string ReportDir = "Tasks/Active/Task-20261027-075-waiting-room-design-rooms";

        // Plan image is 1442x1091 px. 18 px = 1 m (a factory-hall scale), the lobby centre pixel maps to Origin.
        public static readonly Vector3 Origin = new(0, 0, 70);
        const float PxPerMeter = 18f;
        static readonly Vector2 LobbyCenterPx = new(650, 547);

        public readonly struct Room
        {
            public readonly string Id, Name, Level; public readonly Rect Px;
            public Room(string id, string name, float x0, float y0, float x1, float y1, string level = "1F")
            { Id = id; Name = name; Px = Rect.MinMaxRect(x0, y0, x1, y1); Level = level; }
            public Vector2 Min => ToLocal(Px.xMin, Px.yMax);
            public Vector2 Max => ToLocal(Px.xMax, Px.yMin);
        }

        // Footprints traced from the plan image (pixel rectangles). Later stages build each room inside its footprint.
        public static readonly Room[] Rooms =
        {
            new("01", "대기실 로비", 385, 295, 915, 800),
            new("02", "숙소 / 화장실", 705, 165, 970, 285),
            new("03", "상점 (기존 기둥: 수화기 / 배송구)", 430, 630, 480, 680),
            new("04", "소품실", 765, 620, 975, 800),
            new("05", "폐기물 보관소", 1140, 65, 1410, 300),
            new("05P", "배관 구역", 995, 140, 1145, 215),
            new("06", "무대 입구 통로", 925, 460, 1300, 615),
            new("07", "공연 스테이지 입구", 1300, 435, 1420, 630),
            new("08", "전문가 대기실 (엘리베이터)", 925, 310, 1115, 450),
            new("09", "연습실 (2026-10-27 북쪽으로 확장)", 360, -101, 665, 295),
            new("10", "작업실", 150, 160, 360, 340),
            new("ST", "계단", 55, 235, 150, 430),
            new("11", "무대 장치 보관실", 350, 805, 675, 1010),
            new("12", "화물 적재 장소", 680, 805, 1000, 1010),
            new("13", "기계실", 130, 395, 385, 530, "B1"),
            new("14", "관리실", 20, 555, 150, 660, "B1"),
            new("CH", "탈의 방", 45, 655, 210, 800, "B1"),
            new("EX", "탈출로 입구", 225, 630, 380, 710, "B1"),
            new("LG", "휴게실 (로비 안)", 600, 660, 700, 760),
            new("WC", "서쪽 복도 (10 아래, 로비 ↔ 10 · 계단)", 150, 340, 376, 395),
        };

        // Connections read from the plan image (confirmed by the planner 2026-10-27). Rooms do not all hang off the lobby:
        // 1F  lobby - 06 - 07 | lobby - 08 | lobby - 02 - restroom -(breakable wall)- pipe zone - 05
        //     lobby - 09 - 10 | lobby - west corridor - 10, stairs | lobby - 04 | lobby - 11 - 12 - lobby | shop, lounge inside the lobby
        // B1  stairs - 13 - corridor - 14 - changing room | 13 - corridor - escape entrance
        // Doors into rooms not built yet stay closed with a temporary curtain until that room is built.

        public static Vector2 ToLocal(float px, float py) => new((px - LobbyCenterPx.x) / PxPerMeter, -(py - LobbyCenterPx.y) / PxPerMeter);
        static Vector3 W(float x, float z, float y = 0) => Origin + new Vector3(x, y, z);

        // Lobby hall: 29.4 x 28 m with the prop room cut into the south-east corner, as on the plan.
        const float LobbyW = 14.7f, LobbyN = 14f, LobbyS = -14f, NotchX = 6.4f, NotchZ = -4f;
        const float WallHeight = 13f, DoorHeight = 4.2f, LedgerY = 4.6f, TrussY = 11.6f;
        static readonly Vector2 TentCenter = new(.55f, 4.3f);
        const float TentRadius = 7.6f, TentEdgeY = 4.8f, TentApexY = 8.4f;
        static readonly Vector2 ShopPillar = new(-10.8f, -6f);
        // 2026-10-27: the planner removed the central tent for now. Its bulb strings and their glow lights go with it;
        // the hall key light stays so the lobby keeps its base light. Set true to bring the tent back.
        static readonly bool WithTent = false;

        static readonly Dictionary<Object, Object> Copies = new();
        static readonly List<string> Manifest = new();
        static readonly Dictionary<string, Material> Materials = new();
        static Transform source;
        static System.Random random;

        struct Door
        {
            public string Id, Label; public float At, Width; public bool Marquee;
            public Door(string id, string label, float at, float width, bool marquee = false) { Id = id; Label = label; At = at; Width = width; Marquee = marquee; }
        }

        // ---------- entry points ----------

        public static string BuildLobby()
        {
            var (scene, target) = Begin();
            var original = Snapshot(source);
            int undo = StartUndo("Layout A: rebuild lobby");
            try
            {
                Remove(target, "S01_Lobby");
                var lobby = Group(target, "S01_Lobby", Origin);
                BuildShell(lobby);
                if (WithTent) BuildTent(Group(lobby, "CircusTent", W(TentCenter.x, TentCenter.y)));
                BuildFloorWear(Group(lobby, "FloorWear", Origin));
                BuildDressing(Group(lobby, "Dressing", Origin));
                BuildLighting(Group(lobby, "Lighting", Origin));
                BuildLobbyGameplay(lobby);
                Group(lobby, "LoungeReservation", W(0, -9.3f)); // Stage 5 builds the lounge here.
                if (target.Find("S04_PropRoom") != null) OpenPortal(target, "04"); // rooms already built keep their doorway open
                if (target.Find("S02_Dormitory") != null) OpenPortal(target, "02");
                if (target.Find("S06_StageCorridor") != null) OpenPortal(target, "06");
                if (target.Find("S09_PracticeRoom") != null) OpenPortal(target, "09");
                if (target.Find("WC_WestCorridor") != null) OpenPortal(target, "WC");
                if (target.Find("S08_ElevatorHall") != null) OpenPortal(target, "08");
                int remapped = RemapReferences(lobby);
                int outer = RemapExistingReferences(scene, lobby);
                Physics.SyncTransforms();
                AssertUnchanged(original);
                ValidateLobby(lobby);
                Finish(scene, undo, "stage-02-copy-manifest.md", "2단계 로비 재구성 복제 목록", lobby, remapped, outer);
                return $"Lobby rebuilt; objects={lobby.GetComponentsInChildren<Transform>(true).Length}; lights={lobby.GetComponentsInChildren<Light>().Length}; remapped={remapped}; outerRefs={outer}; sourceUnchanged=true";
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        // Stage 3 shop: the original pillar (phone + delivery slot) is copied as-is and moved onto the plan's shop spot.
        public static string BuildShop()
        {
            var (scene, target) = Begin();
            if (target.Find("S01_Lobby") == null) throw new InvalidOperationException("Build the lobby first.");
            var original = Snapshot(source);
            int undo = StartUndo("Layout A: copy shop pillar");
            try
            {
                Remove(target, "S03_Shop");
                var shopSource = Required("S03_Shop");
                var pillarSource = Required("S03_Shop/Pillar");
                var shop = Copy(shopSource, target);
                // Original phone faces -X (west wall of the old lobby). Here the pillar stands west of the hub, so turn it to face +X.
                var turn = Quaternion.Euler(0, 180, 0);
                var pivotOld = new Vector3(pillarSource.position.x, 0, pillarSource.position.z);
                shop.SetPositionAndRotation(W(ShopPillar.x, ShopPillar.y) + turn * (shopSource.position - pivotOld), turn * shopSource.rotation);
                var extras = Group(shop, "LayoutA_Additions", shop.position);
                var phone = shop.GetComponentInChildren<ShopPhone>();
                // Single lamp over the phone (Task-060: "수화기 위 단일 램프").
                Spot(extras, "PhoneLamp", phone.transform.position + Vector3.up * 2.6f + Vector3.right * .9f, phone.transform.position, new Color(1f, .74f, .45f), 3.2f, 6f, 55f);
                var lamp = Copy(Required("Mood/Lights").Cast<Transform>().First(t => t.name == "Lobby_Lamp"), extras, "PhoneCagedLamp");
                lamp.position = phone.transform.position + Vector3.up * 2.3f + Vector3.right * .55f;
                foreach (var r in lamp.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
                foreach (var l in lamp.GetComponentsInChildren<Light>(true)) l.intensity *= .5f;
                PlaceCopy("Mood/InteriorArt/StorageTrunk_0", extras, W(ShopPillar.x - 1.6f, ShopPillar.y - 1.4f), 80f); // south-west of the pillar, clear of the stair foot
                PlaceFactory("SM_Woodpallet_1", extras, W(ShopPillar.x - 1.3f, ShopPillar.y - 2.2f), 8);
                // The pillar runs on up to the hall roof (2026-10-27): the same steel continues from its old 10 m top, banded at the joints.
                var pillarBounds = Footprint(shop.Find("Pillar"));
                var pillarMat = shop.Find("Pillar").GetComponentInChildren<Renderer>().sharedMaterial;
                Box(extras, "PillarToRoof", new Vector3(pillarBounds.center.x, (pillarBounds.max.y + WallHeight) * .5f, pillarBounds.center.z), new Vector3(pillarBounds.size.x, WallHeight - pillarBounds.max.y, pillarBounds.size.z), pillarMat);
                foreach (var y in new[] { pillarBounds.max.y, TrussY - .6f, WallHeight - .25f })
                    Box(extras, "PillarBand", new Vector3(pillarBounds.center.x, y, pillarBounds.center.z), new Vector3(pillarBounds.size.x + .12f, .2f, pillarBounds.size.z + .12f), Mat("LA_Bronze"));
                int remapped = RemapReferences(shop);
                int outer = RemapExistingReferences(scene, shop) + ConnectWorldLabels(scene, shop.GetComponentInChildren<ShopDeliverySlot>());
                Physics.SyncTransforms();
                AssertUnchanged(original);
                ValidateShop(scene, shop);
                Finish(scene, undo, "stage-03-shop-copy-manifest.md", "3단계 상점 복제 목록", shop, remapped, outer);
                return $"Shop pillar copied; objects={shop.GetComponentsInChildren<Transform>(true).Length}; remapped={remapped}; outerRefs={outer}; sourceUnchanged=true";
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        // Stage 3 prop room: the original room's three zones (costumes, 4-tier shelves, dressing) are copied and re-laid
        // into the plan footprint, which is shorter (11.2 x 9.6 m inside) than the original 16 x 8.5 m room.
        const float PropX0 = 6.9f, PropX1 = 18.1f, PropZ0 = -14.1f, PropZ1 = -4.5f, PropHeight = 5.6f, PropDoorZ = -8.7f, PropDoorWidth = 3.4f;

        public static string BuildPropRoom()
        {
            var (scene, target) = Begin();
            if (target.Find("S01_Lobby") == null) throw new InvalidOperationException("Build the lobby first.");
            var original = Snapshot(source);
            int undo = StartUndo("Layout A: build prop room");
            try
            {
                Remove(target, "S04_PropRoom");
                var room = Group(target, "S04_PropRoom", W((PropX0 + PropX1) * .5f, (PropZ0 + PropZ1) * .5f));
                BuildPropShell(room);
                const string art = "Mood/InteriorArt/Stage03_PropRoom/";

                // West/south-west: costumes in use. Racks move from the original west wall onto the south wall.
                var costumes = Group(room, "CostumeZone", room.position);
                PlaceItem(art + "WestActiveCostumes_0", costumes, 8.3f, -13.55f, -90);
                PlaceItem(art + "WestActiveCostumes_1", costumes, 10.6f, -13.55f, -90);
                PlaceItem(art + "CostumeCupboard", costumes, 7.55f, -11.6f, 90);
                PlaceGroup(new[] { art + "CostumeRepairBench", art + "RepairChair" }, costumes, new Vector2(-4.8f, 57.45f), 9.9f, -11.2f, 0);
                for (int i = 0; i < 3; i++) PlaceItem(art + "StoredMascotHead_" + i, costumes, 8.1f + i * 1.15f, -14f, 0);

                // North wall shelves and the functional 4-tier handover shelf across the middle of the room.
                var shelves = Group(room, "ShelfZone", room.position);
                PlaceItem(art + "NorthShelf_West", shelves, 9.1f, -4.85f, 0);
                PlaceItem(art + "NorthShelf_East", shelves, 11.7f, -4.85f, 0);
                PlaceGroup(new[] { art + "SouthGeneralPropShelf", art + "PreviousToy" }, shelves, new Vector2(1.8f, 54.9f), 14.6f, -4.85f, 180);
                PlaceGroup(new[] { "WaitingRoomGameplay/HandoverShelf_4Tier", "Mood/Furnishings/PropRoom/HandoverShelf_Visual", "Mood/LightingPass/Spot_HandoverShelf" },
                    shelves, new Vector2(-1.7f, 54.85f), 13.3f, -8.4f, 0);
                PlaceItem(art + "CoolToolsAccent", shelves, 16.8f, -5.2f, 0);

                // South and east walls: bulb-framed dressing mirrors, the brightest spot in the room.
                var dressing = Group(room, "DressingZone", room.position);
                var south = new List<string>();
                foreach (var i in new[] { "0", "1" })
                    foreach (var n in new[] { "SouthDressingDesk_", "SouthDressingMirror_", "SouthDressingChair_", "SouthBulbs_", "VanityWarm_", "DressingRug_", "WigStand_", "MakeupJar_" })
                        south.Add(art + n + i);
                PlaceGroup(south.ToArray(), dressing, new Vector2(5.5f, 53.5f), 14.7f, -13.25f, 0);
                PlaceGroup(new[] { art + "EastDressingDesk", art + "EastDressingMirror", art + "EastDressingChair", art + "EastBulbs", art + "EastVanityWarm" },
                    dressing, new Vector2(7f, 58.8f), PropX1 - 1.05f, -10.2f, 0);

                // Original hanging lamps, sagging ceiling canvas and dust, fitted to the shorter room.
                var mood = Group(room, "Mood", room.position);
                PlaceItem("Mood/Lights/PropRoom_Lights", mood, 11f, -6.8f, 0);
                PlaceItem("Mood/Lights/DressingCorner_Lights", mood, 14.6f, -11f, 0);
                PlaceItem("Mood/InteriorArt/Dust_Props", mood, 12.5f, -9.3f, 0);
                var canvas = Copy(Required("Mood/InteriorArt/PropRoom_PatchedCeiling"), mood);
                canvas.localScale = new Vector3((PropX1 - PropX0) / 15.8f, 1, (PropZ1 - PropZ0) / 8.3f);
                MoveCenter(canvas, (PropX0 + PropX1) * .5f, (PropZ0 + PropZ1) * .5f);
                foreach (var (x, z, s) in new[] { (8f, -9.5f, 2.2f), (16.5f, -6f, 1.8f), (12f, -12.5f, 1.6f) })
                    Decal(mood, "RustStain", x, z, s, s * .8f, Mat("LA_RustDecal"));
                Decal(mood, "Crack", 10.5f, -6.4f, 2.6f, 2.6f, Mat("LA_CrackDecal"));

                // Room camera framing: the original close-view zone, resized to the new room.
                var zone = Copy(Required("CameraZones/Zone_PropRoom"), room);
                zone.position = W((PropX0 + PropX1) * .5f, (PropZ0 + PropZ1) * .5f, 3f);
                zone.GetComponent<BoxCollider>().size = new Vector3(PropX1 - PropX0 - .2f, 6f, PropZ1 - PropZ0 - .2f);

                OpenPortal(target, "04");
                int remapped = RemapReferences(room);
                int outer = RemapExistingReferences(scene, room) + ConnectWorldLabels(scene, "shelf", room.GetComponentInChildren<HandoverShelfView>());
                Physics.SyncTransforms();
                AssertUnchanged(original);
                ValidatePropRoom(scene, room);
                Finish(scene, undo, "stage-03-proproom-copy-manifest.md", "3단계 소품실 복제 목록", room, remapped, outer);
                return $"Prop room built; objects={room.GetComponentsInChildren<Transform>(true).Length}; lights={room.GetComponentsInChildren<Light>().Length}; remapped={remapped}; outerRefs={outer}; sourceUnchanged=true";
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        // Stage 3 dormitory + restroom (plan 02): one footprint split by a partition. The lobby door opens into the dormitory,
        // the restroom is reached through the partition. Original 20 x 8.7 m content is compressed into 14.2 x 8 m.
        const float DormX0 = 3.6f, DormX1 = 12f, RestX0 = 12.2f, RestX1 = 17.8f, DormZ0 = 14.5f, DormZ1 = 21.2f, DormHeight = 5f;
        const float DormDoorX = 7.8f, DormDoorWidth = 3.8f, RestDoorZ = 16.6f, RestDoorWidth = 2f;
        const float BreachZ = 18.9f, BreachWidth = 1.6f; // restroom east wall, between the stalls and the sinks

        public static string BuildDormitory()
        {
            var (scene, target) = Begin();
            if (target.Find("S01_Lobby") == null) throw new InvalidOperationException("Build the lobby first.");
            var original = Snapshot(source);
            int undo = StartUndo("Layout A: build dormitory and restroom");
            try
            {
                Remove(target, "S02_Dormitory");
                var root = Group(target, "S02_Dormitory", W((DormX0 + RestX1) * .5f, (DormZ0 + DormZ1) * .5f));
                var dorm = Group(root, "Dormitory", W((DormX0 + DormX1) * .5f, (DormZ0 + DormZ1) * .5f));
                var rest = Group(root, "Restroom", W((RestX0 + RestX1) * .5f, (DormZ0 + DormZ1) * .5f));
                RoomShell(dorm, "LA_Dorm", DormX0, DormX1, DormZ0, DormZ1, DormHeight, 2.2f, Mat("LA_FloorConcrete"), Mat("LA_WallConcrete"), Mat("LA_WallRust"),
                    new[] { (2, RestDoorZ, RestDoorWidth, .1f), (3, DormDoorX, DormDoorWidth, .5f) }, eastThickness: .1f);
                RoomShell(rest, "LA_Restroom", RestX0, RestX1, DormZ0, DormZ1, DormHeight, 2.2f, Mat("LA_TealTile"), Mat("LA_TealTileDark"), Mat("LA_WallConcrete"),
                    new[] { (0, RestDoorZ, RestDoorWidth, .1f), (2, BreachZ, BreachWidth, .5f) }, westThickness: .1f);
                // One collider under both rooms and the lobby doorway, so the wall thickness has floor.
                var floorCollider = new GameObject("FloorCollider"); floorCollider.transform.SetParent(root, false);
                floorCollider.transform.position = W((DormX0 + RestX1) * .5f, (LobbyN - .1f + DormZ1) * .5f, -.1f);
                floorCollider.AddComponent<BoxCollider>().size = new Vector3(RestX1 - DormX0 + .2f, .2f, DormZ1 - LobbyN + .1f);

                // Dormitory: original items keep their own shape; positions compress along X and shift north by +6.6 m.
                const string art = "Mood/InteriorArt/Stage01_Dormitory/";
                float DX(float oldX) => 7.7f + (oldX + 3.7f) * .72f;
                const float dz = 5.3f - 70f; // world z old -> local z new (plan depth 6.7 m)
                var beds = Group(dorm, "Beds", dorm.position);
                var bedOldX = new[] { -8.6f, -6.5f, -2.4f, 1.2f }; var bedNewX = new[] { 4.2f, 5.7f, 8.6f, 11.3f }; var letters = new[] { "A", "B", "C", "D" };
                for (int i = 0; i < 4; i++)
                {
                    var paths = new List<string> { "Mood/InteriorArt/Stage01_WaitingRoomBeds/Bed_" + letters[i], art + "BedsideLamp_" + i, art + "BedsideGlow_" + i, art + "FootTrunk_" + i, art + "PersonalClothes_" + i };
                    if (i == 1) paths.Add(art + "BedsideStool_B");
                    if (i == 3) paths.Add(art + "PersonalBear");
                    PlaceGroup(paths.ToArray(), beds, new Vector2(bedOldX[i], 70f), bedNewX[i], 0, 0);
                }
                PlaceGroup(new[] { art + "FadedTwoDoorLockers" }, beds, new Vector2(-4.6f, 70f), 7.1f, 0, 0);
                PlaceGroup(new[] { art + "DarkWoodWardrobe" }, beds, new Vector2(-.7f, 70f), 9.9f, 0, 0);
                // Every copy above was placed with dz = 0; push them all north onto the new wall line.
                foreach (Transform t in beds) t.position += new Vector3(0, 0, dz + 70f);
                var living = Group(dorm, "Living", dorm.position);
                // West-wall items keep their distance from the west wall.
                foreach (var n in new[] { "WestPersonalDresser", "StoredBedLinen", "PreviousGenerationWallMarks" })
                {
                    var o = Required(art + n);
                    var t = Copy(o, living);
                    t.position = new Vector3(W(DormX0 + (o.position.x + 9.85f), 0).x, o.position.y, o.position.z + dz + 70f);
                }
                for (int i = 0; i < 6; i++) { var o = Required(art + "OldWallStud_" + i); var t = Copy(o, living); t.position = new Vector3(W(DX(o.position.x), 0).x, o.position.y, o.position.z + dz + 70f); }
                for (int i = 0; i < 4; i++) { var o = Required(art + "CeilingRepairPanel_" + i); var t = Copy(o, living); t.position = new Vector3(W(DX(o.position.x), 0).x, DormHeight - .15f, o.position.z + dz + 70f); t.localScale = Vector3.Scale(t.localScale, new Vector3(.8f, 1, 1.15f)); }
                var line = Copy(Required(art + "PersonalClothesline"), living);
                line.localScale = Vector3.Scale(line.localScale, new Vector3(.62f, 1, 1));
                MoveCenter(line, (DormX0 + DormX1) * .5f, Required(art + "PersonalClothesline").position.z + dz);
                PlaceItem(art + "WornEntryRug", living, DormDoorX, DormZ0 + 2.1f, 0);
                var sky = Group(dorm, "Skylight", dorm.position);
                PlaceGroup(new[] { "Mood/Ceilingworks/Dormitory_Skylight" }.Concat(Enumerable.Range(0, 5).Select(i => art + "SkylightGrille_" + i)).Concat(Enumerable.Range(0, 3).Select(i => art + "SkylightCrossbar_" + i)).ToArray(),
                    sky, new Vector2(-3f, 83.4f), DormDoorX, 83.4f + dz, 0);
                foreach (Transform t in sky) t.position = new Vector3(t.position.x, Mathf.Min(t.position.y, DormHeight - .3f), t.position.z);
                BoardUp(sky); // the skylight glass becomes steel plate under the grille; no sky shows indoors
                var lamps = Required("Mood/Lights").Cast<Transform>().Where(t => t.name == "Dormitory_Lamp").ToArray();
                for (int i = 0; i < lamps.Length; i++)
                {
                    var t = Copy(lamps[i], dorm);
                    t.position = new Vector3(W(DX(lamps[i].position.x), 0).x, 3.6f, lamps[i].position.z + dz + 70f);
                }
                var dormZone = Copy(Required("CameraZones/Zone_Dormitory"), dorm);
                dormZone.position = W((DormX0 + DormX1) * .5f, (DormZ0 + DormZ1) * .5f, 3f);
                dormZone.GetComponent<BoxCollider>().size = new Vector3(DormX1 - DormX0 - .2f, 6f, DormZ1 - DormZ0 - .2f);

                // Restroom: stalls on the north wall, sinks on the south wall; X compresses around the room centre.
                float RX(float oldX) => 15f + (oldX - 6.5f) * .8f;
                float RZ(float oldZ) => DormZ0 + (oldZ - 79.15f) * (DormZ1 - DormZ0) / 8.4f;
                const string furn = "Mood/Furnishings/Restroom/";
                var fixtures = Group(rest, "Fixtures", rest.position);
                foreach (Transform o in Required("Mood/Furnishings/Restroom"))
                {
                    var t = Copy(o, fixtures);
                    float x = o.name.StartsWith("Sink") ? 15f + (o.position.x - 6.15f) : o.name == "SinkMirrors" ? 15f + (o.position.x - 6.15f) : RX(o.position.x);
                    t.position = new Vector3(W(x, 0).x, o.position.y, W(0, RZ(o.position.z)).z);
                }
                foreach (var path in new[] { "Mood/Dressing/Restroom_Wet", "Mood/Dressing/Restroom_Dustbin", "Mood/Lights/Restroom_Lights" })
                    foreach (Transform o in Required(path))
                    {
                        if (o.name == "Puddle") continue; // V4_Puddle renders as a black slab; replaced by a decal below
                        var t = Copy(o, fixtures);
                        t.position = new Vector3(W(RX(o.position.x), 0).x, o.position.y, W(0, RZ(o.position.z)).z);
                    }
                PlaceItem("Mood/InteriorArt/Restroom_PatchedCeiling", rest, (RestX0 + RestX1) * .5f, (DormZ0 + DormZ1) * .5f, 0)
                    .position += Vector3.down * (4.35f - (DormHeight - .25f));
                Point(rest, "ColdPanel_0", W(14f, 20f, DormHeight - .3f), Hex("CFE8E6"), 1.4f, 6f);
                Point(rest, "ColdPanel_1", W(16f, 16.5f, DormHeight - .3f), Hex("CFE8E6"), 1.2f, 6f);
                foreach (var (x, z) in new[] { (14f, 20f), (16f, 16.5f) })
                    Box(rest, "CeilingPanel", W(x, z, DormHeight - .04f), new Vector3(1.2f, .04f, .6f), Mat("V4_Skylight"));
                var restZone = Copy(Required("CameraZones/Zone_Restroom"), rest);
                restZone.position = W((RestX0 + RestX1) * .5f, (DormZ0 + DormZ1) * .5f, 3f);
                restZone.GetComponent<BoxCollider>().size = new Vector3(RestX1 - RestX0 - .2f, 6f, DormZ1 - DormZ0 - .2f);
                Decal(rest, "Crack", 13.4f, 18.4f, 1.8f, 1.8f, Mat("LA_CrackDecal"));
                Decal(rest, "WetPuddle", 14.6f, 17.6f, 2.2f, 1.6f, Mat("LA_PuddleDecal"));
                Decal(dorm, "RustStain", 5f, 16f, 1.8f, 1.4f, Mat("LA_RustDecal"));

                // Breakable wall into the pipe zone (plan: restroom - breakable wall - pipe zone - 05), left broken open for now
                // (2026-10-27); the breaking itself comes later. Until the pipe zone is built the hole is closed off.
                BrokenWall(rest, new Vector2(RestX1, BreachZ), Vector3.forward, Vector3.left, BreachWidth, Mat("LA_TealTile"));
                if (target.Find("P05_PipeZone") == null)
                    TempClosure(Group(rest, "BreachClosure", rest.position), new Vector2(RestX1, BreachZ), Vector3.forward, Vector3.left, BreachWidth, "배관 구역");
                OpenPortal(target, "02");
                int remapped = RemapReferences(root);
                int outer = RemapExistingReferences(scene, root);
                Physics.SyncTransforms();
                AssertUnchanged(original);
                ValidateDormitory(root);
                Finish(scene, undo, "stage-03-dormitory-copy-manifest.md", "3단계 숙소·화장실 복제 목록", root, remapped, outer);
                return $"Dormitory built; objects={root.GetComponentsInChildren<Transform>(true).Length}; lights={root.GetComponentsInChildren<Light>().Length}; remapped={remapped}; outerRefs={outer}; sourceUnchanged=true";
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        static void ValidateDormitory(Transform root)
        {
            var portal = root.parent.Find("S01_Lobby/FutureConnections/Portal_02");
            if (portal != null && portal.Find("TemporaryClosure") != null) throw new InvalidOperationException("Dormitory portal still closed");
            if (root.GetComponentsInChildren<Component>(true).Any(c => c == null)) throw new InvalidOperationException("Missing script in dormitory");
            if (!Physics.Raycast(W(DormDoorX, LobbyN + .25f, 1f), Vector3.down, out var hit, 2f) || Mathf.Abs(hit.point.y) > .01f)
                throw new InvalidOperationException("No floor under the dormitory doorway");
            foreach (var (x, z, what) in new[] { (DormDoorX, LobbyN + .25f, "dorm doorway"), (DormDoorX, DormZ0 + 1.2f, "dorm entry"), (7.8f, 18.2f, "bed aisle"),
                         (DormX1 - .8f, RestDoorZ, "partition door west"), ((DormX1 + RestX0) * .5f, RestDoorZ, "partition door"), (RestX0 + 1f, RestDoorZ, "restroom entry"), (15f, 18.5f, "restroom middle") })
                CheckStanding(W(x, z, .1f), root.parent, what);
            if (root.GetComponentsInChildren<Light>().Length > 12) throw new InvalidOperationException("Too many dormitory lights");
        }

        // Stage 4 performance route (plan 06 + 07). The original 42 m backstage tunnel is copied in four chunks into the
        // 21 m corridor east of the lobby; its stage-curtain end becomes the wider 07 stage entrance. The night shutter,
        // the all-gathered check and the waiting-room exit watch move with it.
        const float CorX0 = 15.2f, CorX1 = 36.3f, CorZ0 = -3.5f, CorZ1 = 4.5f, RouteHeight = 7.6f;
        const float StageX0 = 36.5f, StageX1 = 42.8f, StageZ0 = -4.6f, StageZ1 = 6.2f; // plan 07: 10.8 m
        const float RouteDoorZ = .5f, RouteDoorWidth = 5f, StageDoorWidth = 7f, RouteDz = .5f; // old tunnel centre (world z 70) -> 06 door centre

        // Kept chunks of the original tunnel (world X from, to, shift). The fourth pair of curtain rooms (x 41-46.5) is left out.
        static readonly (float from, float to, float shift)[] RouteChunks = { (10f, 17.5f, 4.7f), (22.5f, 29.5f, -.3f), (34f, 41f, -4.8f), (46.5f, 52.3f, -9f) };
        const int StageChunk = 3;
        // Single items from the left-out stretches that the shorter route still needs.
        static readonly Dictionary<string, float> RouteOverrides = new() { { "TunnelFill", -4.25f }, { "Sign_무대 →", -8f }, { "Corridor_Far", -8.5f } };

        public static string BuildStageRoute()
        {
            var (scene, target) = Begin();
            if (target.Find("S01_Lobby") == null) throw new InvalidOperationException("Build the lobby first.");
            var original = Snapshot(source);
            int undo = StartUndo("Layout A: build stage corridor and entrance");
            try
            {
                Remove(target, "S06_StageCorridor");
                Remove(target, "S07_StageEntrance");
                var corridor = Group(target, "S06_StageCorridor", W((CorX0 + CorX1) * .5f, RouteDoorZ));
                var stage = Group(target, "S07_StageEntrance", W((StageX0 + StageX1) * .5f, RouteDoorZ));
                RoomShell(corridor, "LA_Corridor", CorX0, CorX1, CorZ0, CorZ1, RouteHeight, 2.2f, Mat("LA_FloorConcrete"), Mat("LA_WallConcrete"), Mat("LA_WallRust"),
                    new[] { (0, RouteDoorZ, RouteDoorWidth, .1f), (2, RouteDoorZ, StageDoorWidth, .1f) }, eastThickness: .1f);
                RoomShell(stage, "LA_StageEntrance", StageX0, StageX1, StageZ0, StageZ1, RouteHeight, 2.6f, Mat("LA_FloorConcrete"), Mat("LA_WallConcrete"), Mat("LA_WallRust"),
                    new[] { (0, RouteDoorZ, StageDoorWidth, .1f) }, westThickness: .1f);
                // One collider from the lobby doorway to the stage curtain, so both wall thicknesses have floor.
                var floorCollider = new GameObject("FloorCollider"); floorCollider.transform.SetParent(corridor, false);
                floorCollider.transform.position = W((LobbyW - .1f + StageX1) * .5f, (StageZ0 + StageZ1) * .5f, -.1f);
                floorCollider.AddComponent<BoxCollider>().size = new Vector3(StageX1 - LobbyW + .3f, .2f, StageZ1 - StageZ0 + .2f);

                // Tunnel art, curtain rooms and lights keep their shape and height; each moves rigidly with its chunk.
                var art = Group(corridor, "TunnelArt", corridor.position);
                var stageArt = Group(stage, "StageArt", stage.position);
                var groups = new Dictionary<(Transform, string), Transform>();
                void Route(Transform o, string group)
                {
                    var bounds = Footprint(o);
                    int chunk = Array.FindIndex(RouteChunks, c => bounds.center.x >= c.from && bounds.center.x < c.to);
                    bool special = RouteOverrides.TryGetValue(o.name, out float shift);
                    if (!special && (chunk < 0 || bounds.size.x > 7f)) return; // long pipe runs are rebuilt to the new length below
                    if (!special) shift = RouteChunks[chunk].shift;
                    if (o.name.StartsWith("SM_CS1_Stool")) return; // stood in the 06 - 07 opening
                    if (chunk == StageChunk && group == "SteelRibs") return; // ribs span the 8 m tunnel, not the wider entrance
                    // Wall pipe pieces cut at the chunk edge leave a broken run in the entrance; it gets whole runs below instead.
                    if (chunk == StageChunk && (o.name.StartsWith("SM_Pipe_Medium") || o.name.StartsWith("SM_Smallpipe") || o.name.StartsWith("PipeDust"))) return;
                    var room = chunk == StageChunk ? stageArt : art;
                    // The entrance is 1 m wider each side, so items standing against the old tunnel walls follow the walls out.
                    float dz = RouteDz + (chunk != StageChunk || group == "Curtains" || group.StartsWith("S07") ? 0
                        : bounds.center.z < 67.5f ? StageZ0 - CorZ0 : bounds.center.z > 72.5f ? StageZ1 - CorZ1 : 0);
                    if (o.name.StartsWith("Puddle")) // V4_Puddle renders as a black slab; a puddle decal takes its place
                    {
                        Decal(room, "WetPuddle", bounds.center.x + shift, bounds.center.z - Origin.z + dz, bounds.size.x, Mathf.Max(bounds.size.z, .6f), Mat("LA_PuddleDecal"));
                        return;
                    }
                    if (!groups.TryGetValue((room, group), out var parent)) groups[(room, group)] = parent = Group(room, group, room.position);
                    Copy(o, parent).position = o.position + new Vector3(shift, 0, dz);
                }
                foreach (var path in new[] { "Mood/Tunnel/SteelRibs", "Mood/Tunnel/WallDressing", "Mood/Tunnel/FloorWalkway", "Mood/Tunnel/StringLights", "Mood/Tunnel/Cables",
                             "Mood/Tunnel/Lights", "Mood/Tunnel/Rags", "Mood/Tunnel/StageProscenium", "S06_StageCorridor_Temp/CurtainRooms", "Mood/Furnishings/Corridor", "S07_StageEntrance_Temp" })
                    foreach (Transform o in Required(path)) Route(o, path.Substring(path.LastIndexOf('/') + 1));
                foreach (Transform o in Required("Mood/Curtains"))
                    if (o.name.StartsWith("Curtain_CurtainRoom") || o.name == "Curtain_StageEntrance") Route(o, "Curtains");
                foreach (var path in new[] { "Mood/VisibilityLighting/Corridor_Near", "Mood/VisibilityLighting/Corridor_Middle", "Mood/VisibilityLighting/Corridor_Far",
                             "Mood/VisibilityLighting/StageEntrance", "Mood/Lights/StageEntrance_RedSpill", "Mood/LightingPass/Spot_StageCurtain", "S06_StageCorridor_Temp/Label" })
                    Route(Required(path), "Lighting");
                FaceStageSign(art.Find("WallDressing/Sign_무대 →"));
                var vaultSource = Required("Mood/Tunnel/Vault");
                var vault = Copy(vaultSource, art);
                vault.localScale = Vector3.Scale(vault.localScale, new Vector3((CorX1 - CorX0) / 42f, 1, 1));
                vault.position = new Vector3(W((CorX0 + CorX1) * .5f, 0).x, vaultSource.position.y, vaultSource.position.z + RouteDz);
                PipeRun(art, new(CorX0 + .3f, CorZ0 + .35f), new(CorX1 - .3f, CorZ0 + .35f), 4.1f, .3f);

                // 07: the stage curtain fills the east wall; faded drapes close the gaps either side of the proscenium, a bulb row marks it.
                // The proscenium spans Z -3.3..4.4; the drapes fill whatever the room leaves on each side.
                foreach (var (z, width) in new[] { ((StageZ0 - 3.3f) * .5f, -3.3f - StageZ0), ((4.4f + StageZ1) * .5f, StageZ1 - 4.4f) })
                {
                    Box(stageArt, "FadedDrape", W(StageX1 - .12f, z, 2.8f), new Vector3(.08f, 5.6f, width), Mat("LA_TentRed"));
                    Box(stageArt, "DrapeRod", W(StageX1 - .2f, z, 5.65f), new Vector3(.06f, .06f, width + .2f), Mat("LA_Bronze"));
                }
                for (int i = 0; i <= 13; i++) Bulb(stageArt, W(StageX1 - .9f, Mathf.Lerp(-3.2f, 4.3f, i / 13f), 5.3f));
                foreach (var z in new[] { StageZ0 + .3f, StageZ1 - .3f })
                    PipeRun(stageArt, new(StageX0 + .2f, z), new(StageX1 - 1.5f, z), 4.1f, .16f);

                // Room camera framing: the original corridor zone, once per room.
                var zone = Copy(Required("CameraZones/Zone_Corridor"), corridor);
                zone.position = W((CorX0 + CorX1) * .5f, RouteDoorZ, 3f);
                zone.GetComponent<BoxCollider>().size = new Vector3(CorX1 - CorX0 - .2f, 6f, CorZ1 - CorZ0 - .2f);
                var stageZone = Copy(Required("CameraZones/Zone_Corridor"), stage, "Zone_StageEntrance");
                stageZone.position = W((StageX0 + StageX1) * .5f, (StageZ0 + StageZ1) * .5f, 3f);
                stageZone.GetComponent<BoxCollider>().size = new Vector3(StageX1 - StageX0 - .2f, 6f, StageZ1 - StageZ0 - .2f);

                // All-gathered check in front of the stage curtain (Task-060: 4 x 7 m before 07) with its floor border.
                var gameplay = Group(stage, "Gameplay", stage.position);
                foreach (var path in new[] { "WaitingRoomGameplay/GatherZone", "S06_StageCorridor_Temp/GatherZone_Border" })
                    Copy(Required(path), gameplay).position = Required(path).position + new Vector3(RouteChunks[StageChunk].shift, 0, RouteDz);

                BuildNightShutter(corridor);
                OpenPortal(target, "06");
                BuildWaitingRoomArea(target);
                int remapped = RemapReferences(corridor) + RemapReferences(stage) + RemapReferences(target.Find("ExplorationTimer"));
                var gather = stage.GetComponentInChildren<StageEntranceGather>();
                gather.ApplyArea(); // lays the floor border strips around the moved box
                int outer = RemapExistingReferences(scene, corridor) + RemapExistingReferences(scene, stage) + Connect<WaitingRoomUiBinding>(scene, "gather", gather);
                Physics.SyncTransforms();
                AssertUnchanged(original);
                ValidateStageRoute(scene, corridor, stage);
                Finish(scene, undo, "stage-04-route-copy-manifest.md", "4단계 무대 통로·스테이지 입구 복제 목록", corridor, remapped, outer);
                return $"Stage route built; corridor={corridor.GetComponentsInChildren<Transform>(true).Length} stage={stage.GetComponentsInChildren<Transform>(true).Length}; " +
                    $"lights={corridor.GetComponentsInChildren<Light>().Length + stage.GetComponentsInChildren<Light>().Length}; remapped={remapped}; outerRefs={outer}; sourceUnchanged=true";
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        // The original "무대 →" sign reads toward the lobby from the south wall; on the north wall the arrow points at the stage.
        public static void FaceStageSign(Transform sign)
        {
            sign.rotation = Quaternion.Euler(0, 180, 0) * sign.rotation;
            float fromWall = sign.position.z - W(0, CorZ0).z;
            sign.position = new Vector3(sign.position.x, sign.position.y, W(0, CorZ1).z - fromWall);
        }

        // Stage 6 elevator hall (plan 08, "전문가 대기실 (엘리베이터)"). The planner turned the expert waiting room into an
        // elevator; on 2026-10-27 they chose to keep the expert service as a curtained booth inside the hall. A freight lift
        // stands in the east wall (shown only: gate shut, no ride). The booth is the original expert room's working parts
        // (curtain, inside and outside points, glow, sound) moved here, so the expert service keeps working unchanged.
        const float HallX0 = 15.2f, HallX1 = 25.8f, HallZ0 = 5f, HallZ1 = 13.2f, HallHeight = 6f, HallDoorZ = 9.3f;
        const float LiftZ = 11.3f, LiftWidth = 3.2f, LiftDepth = 2.7f, LiftHeight = 3.6f;
        const float BoothX0 = 21.6f, BoothZ1 = 9.4f, BoothDoorZ = 7f, BoothDoorWidth = 2f, BoothHeight = 3.8f;

        public static string BuildElevatorHall()
        {
            var (scene, target) = Begin();
            if (target.Find("S01_Lobby") == null) throw new InvalidOperationException("Build the lobby first.");
            var original = Snapshot(source);
            int undo = StartUndo("Layout A: build elevator hall");
            try
            {
                Remove(target, "S08_ElevatorHall");
                var hall = Group(target, "S08_ElevatorHall", W((HallX0 + HallX1) * .5f, (HallZ0 + HallZ1) * .5f));
                RoomShell(hall, "LA_ElevatorHall", HallX0, HallX1, HallZ0, HallZ1, HallHeight, 2.4f, Mat("LA_FloorConcrete"), Mat("LA_WallConcrete"), Mat("LA_WallRust"),
                    new[] { (0, HallDoorZ, 3.6f, .5f), (2, LiftZ, LiftWidth, .1f) });
                var floorCollider = new GameObject("FloorCollider"); floorCollider.transform.SetParent(hall, false);
                floorCollider.transform.position = W((LobbyW - .1f + HallX1) * .5f, (HallZ0 + HallZ1) * .5f, -.1f);
                floorCollider.AddComponent<BoxCollider>().size = new Vector3(HallX1 - LobbyW + .3f, .2f, HallZ1 - HallZ0 + .2f);

                // Freight lift: a closed car behind a scissor gate, a floor dial above, a call panel and hazard stripes in front.
                var lift = Group(hall, "FreightLift", W(HallX1, LiftZ));
                float carX0 = HallX1 + .1f, carX1 = HallX1 + LiftDepth, cz0 = LiftZ - LiftWidth * .5f - .1f, cz1 = LiftZ + LiftWidth * .5f + .1f;
                Box(lift, "CarFloor", W((carX0 + carX1) * .5f, LiftZ, -.05f), new Vector3(LiftDepth, .1f, cz1 - cz0), Mat("V4_Floor_OldPlank"), true);
                Box(lift, "CarBack", W(carX1, LiftZ, LiftHeight * .5f), new Vector3(.1f, LiftHeight, cz1 - cz0), Mat("LA_SteelPlate"), true);
                foreach (var z in new[] { cz0, cz1 })
                    Box(lift, "CarSide", W((carX0 + carX1) * .5f, z, LiftHeight * .5f), new Vector3(LiftDepth, LiftHeight, .1f), Mat("LA_SteelPlate"), true);
                Box(lift, "CarRoof", W((carX0 + carX1) * .5f, LiftZ, LiftHeight + .05f), new Vector3(LiftDepth + .1f, .1f, cz1 - cz0), Mat("V4_RustyMetal"));
                Box(lift, "ShaftAbove", W(HallX1 + .3f, LiftZ, (LiftHeight + DoorHeight) * .5f + .3f), new Vector3(.1f, DoorHeight - LiftHeight + .8f, LiftWidth + .4f), Mat("V4_Iron_Dark"));
                Bulb(lift, W(carX1 - .3f, LiftZ, LiftHeight - .2f));
                Point(lift, "CarBulb", W(carX1 - .6f, LiftZ, LiftHeight - .4f), new Color(1f, .62f, .32f), .8f, 3.5f);
                for (float z = LiftZ - LiftWidth * .5f + .1f; z < LiftZ + LiftWidth * .5f; z += .2f)
                    Box(lift, "GateBar", W(HallX1 - .08f, z, LiftHeight * .5f), new Vector3(.03f, LiftHeight, .03f), Mat("V4_Iron_Dark"));
                for (int i = 0; i < 8; i++)
                    foreach (var s in new[] { 1f, -1f })
                        Box(lift, "GateLattice", W(HallX1 - .1f, LiftZ - LiftWidth * .5f + .2f + i * .4f, LiftHeight * .5f), new Vector3(.02f, LiftHeight * 1.04f, .02f), Mat("V4_RustyMetal"))
                            .rotation = Quaternion.Euler(s * 6.5f, 0, 0);
                var gate = new GameObject("GateCollider"); gate.transform.SetParent(lift, false); gate.transform.position = W(HallX1 - .08f, LiftZ, LiftHeight * .5f);
                gate.AddComponent<BoxCollider>().size = new Vector3(.12f, LiftHeight, LiftWidth);
                Prim(lift, PrimitiveType.Cylinder, "FloorDial", W(HallX1 - .05f, LiftZ, DoorHeight + .55f), new Vector3(.8f, .02f, .8f), Mat("LA_Bronze")).rotation = Quaternion.Euler(0, 0, 90);
                Box(lift, "DialNeedle", W(HallX1 - .08f, LiftZ - .12f, DoorHeight + .62f), new Vector3(.02f, .34f, .03f), Mat("V4_Black")).rotation = Quaternion.Euler(-35, 0, 0);
                Box(lift, "CallPanel", W(HallX1 - .05f, LiftZ - LiftWidth * .5f - .2f, 1.3f), new Vector3(.06f, .5f, .18f), Mat("V4_Iron_Dark"));
                foreach (var y in new[] { 1.4f, 1.2f })
                    Prim(lift, PrimitiveType.Sphere, "CallButton", W(HallX1 - .1f, LiftZ - LiftWidth * .5f - .2f, y), Vector3.one * .07f, Mat("LA_Bulb"));
                for (int i = 0; i < 7; i++)
                    Box(lift, "HazardStripe", W(HallX1 - .55f, LiftZ - LiftWidth * .5f + .25f + i * .45f, .012f), new Vector3(.9f, .004f, .22f), Mat(i % 2 == 0 ? "LA_HazardPaint" : "V4_Black"))
                        .rotation = Quaternion.Euler(0, 35, 0);
                var liftSign = Copy(Required("S01_Lobby/Sign_② 숙소"), lift, "LiftSign", false);
                liftSign.gameObject.SetActive(true);
                liftSign.SetPositionAndRotation(W(HallX1 - .06f, LiftZ, DoorHeight + 1.15f), Quaternion.LookRotation(Vector3.right));
                var text = liftSign.GetComponent<TextMesh>(); text.text = "화물 승강기"; text.anchor = TextAnchor.MiddleCenter; text.fontSize = 64; text.characterSize = .05f; text.color = new Color(.82f, .7f, .5f);

                BuildExpertBooth(Group(hall, "ExpertBooth", W((BoothX0 + HallX1) * .5f, (HallZ0 + BoothZ1) * .5f)));

                // Hall dressing: crimson drape and bulb string on the upper wall, a waiting bench and luggage, the old service desk.
                var dress = Group(hall, "Dressing", hall.position);
                BulbString(dress, W(HallX0 + .3f, HallZ1 - .3f, 4.6f), W(HallX1 - .3f, HallZ1 - .3f, 4.6f), .8f);
                Box(dress, "WallDrape", W(18.6f, HallZ1 - .08f, 3.2f), new Vector3(3.4f, 2.6f, .05f), Mat("V4_Velvet_Red_Cloth"));
                PlaceProp(SyntyProps + "SM_Prop_Plank_Bench_01", dress, W(18.6f, HallZ1 - .6f), 90);
                PlaceCopy("Mood/InteriorArt/StorageTrunk_0", dress, W(20.9f, HallZ1 - .55f), 95f);
                PlaceItem("Mood/Furnishings/ExpertRoom/ServiceDesk", dress, 18.4f, HallZ0 + .6f, 0);
                PlaceItem("Mood/Furnishings/ExpertRoom/ServiceJar", dress, 18f, HallZ0 + .6f, 0).position += Vector3.up * .02f;
                PlaceItem("Mood/Furnishings/ExpertRoom/ServiceShelf", dress, 16.1f, HallZ0 + .5f, 0);
                WallPoster(dress, W(HallX0 + .3f, 6.3f), 90);
                foreach (var (x, z, sz) in new[] { (17f, 11f, 1.8f), (23.6f, 11.8f, 1.6f) })
                    Decal(dress, "RustStain", x, z, sz, sz * .8f, Mat("LA_RustDecal"));
                var lamp = Copy(Required("Mood/Lights").Cast<Transform>().First(t => t.name == "Lobby_Lamp"), dress, "HallLamp");
                lamp.position = W(19.2f, 10.4f, 4.2f);
                foreach (var r in lamp.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
                var zone = Copy(Required("CameraZones/Zone_ExpertRoom"), hall, "Zone_ElevatorHall");
                zone.position = W((HallX0 + HallX1) * .5f, (HallZ0 + HallZ1) * .5f, 3f);
                zone.GetComponent<BoxCollider>().size = new Vector3(HallX1 - HallX0 - .2f, 6f, HallZ1 - HallZ0 - .2f);

                OpenPortal(target, "08");
                int remapped = RemapReferences(hall);
                var view = hall.GetComponentInChildren<ExpertRoomView>();
                int outer = RemapExistingReferences(scene, hall) + Connect<WaitingRoomWorldLabels>(scene, "expert", view);
                Physics.SyncTransforms();
                AssertUnchanged(original);
                ValidateElevatorHall(scene, hall);
                Finish(scene, undo, "stage-06-elevator-copy-manifest.md", "6단계 엘리베이터 홀·전문가 자리 복제 목록", hall, remapped, outer);
                return $"Elevator hall built; objects={hall.GetComponentsInChildren<Transform>(true).Length}; lights={hall.GetComponentsInChildren<Light>().Length}; remapped={remapped}; outerRefs={outer}; sourceUnchanged=true";
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        // The original expert room's working parts moved into a small curtained booth in the hall's south-east corner:
        // the curtain sits in the booth's west partition, 'outside' in the hall and 'inside' in the booth, sofa and table behind.
        static void BuildExpertBooth(Transform booth)
        {
            var shift = new Vector3(BoothX0 - 10.2f, 0, BoothDoorZ - 6.5f); // original curtain at (10.2, 6.5)
            var gameplay = Copy(Required("WaitingRoomGameplay/S08_ExpertRoom"), booth);
            gameplay.position += shift;
            Object.DestroyImmediate(gameplay.Find("Floor").gameObject); // the hall has its own floor
            var curtain = Copy(Required("Mood/Curtains/Curtain_ExpertRoom"), booth);
            curtain.position += shift;
            // Sofa and table: the original room was 8 m deep, the booth 4 m, so they move closer to the curtain.
            foreach (var (name, visual, nx, nz) in new[] { ("Sofa", "WaitingSofa", HallX1 - .7f, 6.6f), ("Table", "CoffeeTable", 23.9f, 8.4f) })
            {
                MoveCenter(gameplay.Find(name), nx, nz);
                MoveCenter(Copy(Required("Mood/Furnishings/ExpertRoom/" + visual), booth), nx, nz);
            }
            // Partitions: dark wood below, crimson velvet above, a low ceiling; the curtain opening stays clear.
            var wall = Group(booth, "Partitions", booth.position);
            float doorLo = BoothDoorZ - BoothDoorWidth * .5f, doorHi = BoothDoorZ + BoothDoorWidth * .5f;
            foreach (var (z0, z1) in new[] { (HallZ0, doorLo), (doorHi, BoothZ1) })
                BoothPanel(wall, W(BoothX0, (z0 + z1) * .5f), new Vector3(.12f, 0, z1 - z0));
            Box(wall, "CurtainHeader", W(BoothX0, BoothDoorZ, (3.1f + BoothHeight) * .5f), new Vector3(.12f, BoothHeight - 3.1f, BoothDoorWidth), Mat("V4_Wood_Old"));
            BoothPanel(wall, W((BoothX0 + HallX1) * .5f, BoothZ1), new Vector3(HallX1 - BoothX0, 0, .12f));
            Box(wall, "BoothCeiling", W((BoothX0 + HallX1) * .5f, (HallZ0 + BoothZ1) * .5f, BoothHeight + .05f), new Vector3(HallX1 - BoothX0, .1f, BoothZ1 - HallZ0), Mat("V4_Wood_Old"));
            for (int i = 0; i <= 10; i++)
                Bulb(wall, W(BoothX0 - .1f, Mathf.Lerp(HallZ0 + .2f, BoothZ1 - .2f, i / 10f), BoothHeight - .15f));
        }

        static void BoothPanel(Transform parent, Vector3 foot, Vector3 size)
        {
            Box(parent, "BoothWainscot", foot + Vector3.up * .7f, new Vector3(Mathf.Max(size.x, .12f), 1.4f, Mathf.Max(size.z, .12f)), Mat("V4_Wood_Old"), true);
            Box(parent, "BoothVelvet", foot + Vector3.up * (1.4f + (BoothHeight - 1.4f) * .5f), new Vector3(Mathf.Max(size.x, .1f), BoothHeight - 1.4f, Mathf.Max(size.z, .1f)), Mat("V4_Velvet_Red"), true);
        }

        static void ValidateElevatorHall(Scene scene, Transform hall)
        {
            var views = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ExpertRoomView>()).Where(v => v.gameObject.activeInHierarchy).ToArray();
            if (views.Length != 1 || !views[0].transform.IsChildOf(hall)) throw new InvalidOperationException("Expected one active expert room in the elevator hall");
            var so = new SerializedObject(views[0]);
            foreach (var field in new[] { "curtainLeft", "curtainRight", "inside", "outside", "glow" })
                if (!(so.FindProperty(field).objectReferenceValue is Component c) || !c.transform.IsChildOf(hall)) throw new InvalidOperationException("Expert room " + field + " still points at the original");
            var inside = ((Component)so.FindProperty("inside").objectReferenceValue).transform.position;
            if (inside.x < W(BoothX0, 0).x + .3f || inside.z > W(0, BoothZ1).z - .3f) throw new InvalidOperationException("Expert inside point is not in the booth");
            foreach (var labels in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<WaitingRoomWorldLabels>(true)))
                if (new SerializedObject(labels).FindProperty("expert").objectReferenceValue != views[0]) throw new InvalidOperationException("World labels do not point at the moved expert room");
            var portal = hall.parent.Find("S01_Lobby/FutureConnections/Portal_08");
            if (portal != null && portal.Find("TemporaryClosure") != null) throw new InvalidOperationException("08 portal still closed");
            if (hall.GetComponentsInChildren<Component>(true).Any(c => c == null)) throw new InvalidOperationException("Missing script in elevator hall");
            foreach (var (p, what) in new[] { (inside, "expert inside"), (((Component)so.FindProperty("outside").objectReferenceValue).transform.position, "expert outside") })
                CheckStanding(new Vector3(p.x, .1f, p.z), hall.parent, what);
            foreach (var (x, z, what) in new[] { (LobbyW + .25f, HallDoorZ, "08 doorway"), (HallX0 + 1.2f, HallDoorZ, "hall entry"), (19.5f, 9.2f, "hall middle"), (HallX1 - 1f, LiftZ, "lift gate front") })
                CheckStanding(W(x, z, .1f), hall.parent, what);
            if (!Physics.Raycast(W(HallX1 - .9f, LiftZ, 1f), Vector3.right, 1.2f, ~0, QueryTriggerInteraction.Ignore)) throw new InvalidOperationException("Lift gate is open");
            if (hall.GetComponentsInChildren<Light>().Length > 6) throw new InvalidOperationException("Too many elevator hall lights");
        }

        // Stage 6 pipe zone (plan 05P) and waste storage (plan 05). Reached only through the restroom's broken wall: a low passage
        // crowded with pipes, then an orderly archive of past generations (records, costumes, retired circus props under sheets),
        // laid out from the original junk storage's own shelves, desks and costumes.
        const float PipeX0 = 18.3f, PipeX1 = 27.2f, PipeZ0 = 17.4f, PipeZ1 = 21.2f, PipeHeight = 4.8f; // above the 4.2 m door head, so the broken wall shows no gap
        const float StoreX0 = 27.2f, StoreX1 = 42.2f, StoreZ0 = 13.7f, StoreZ1 = 26.8f, StoreHeight = 6f, PipeDoorZ = 19.3f, PipeDoorWidth = 2.4f;

        public static string BuildStorageWing()
        {
            var (scene, target) = Begin();
            if (target.Find("S02_Dormitory") == null) throw new InvalidOperationException("Build the dormitory first.");
            var original = Snapshot(source);
            int undo = StartUndo("Layout A: build pipe zone and waste storage");
            try
            {
                Remove(target, "P05_PipeZone");
                Remove(target, "S05_JunkStorage");
                var pipes = Group(target, "P05_PipeZone", W((PipeX0 + PipeX1) * .5f, (PipeZ0 + PipeZ1) * .5f));
                RoomShell(pipes, "LA_PipeZone", PipeX0, PipeX1, PipeZ0, PipeZ1, PipeHeight, 1.6f, Mat("LA_FloorConcrete"), Mat("LA_WallConcrete"), Mat("LA_WallRust"),
                    new[] { (0, BreachZ, BreachWidth, .5f), (2, PipeDoorZ, PipeDoorWidth, .1f) }, eastThickness: .1f);
                var floorCollider = new GameObject("FloorCollider"); floorCollider.transform.SetParent(pipes, false);
                floorCollider.transform.position = W((RestX1 - .1f + StoreX1) * .5f, (StoreZ0 + StoreZ1) * .5f, -.1f);
                floorCollider.AddComponent<BoxCollider>().size = new Vector3(StoreX1 - RestX1 + .3f, .2f, StoreZ1 - StoreZ0 + .2f);
                // Pipes crowd both walls and the ceiling; the middle stays a walkable 1.6 m lane.
                var run = Group(pipes, "Pipes", pipes.position);
                foreach (var (z, y, r) in new[] { (PipeZ0 + .35f, .45f, .32f), (PipeZ0 + .3f, 1.35f, .22f), (PipeZ1 - .3f, .7f, .26f), (PipeZ1 - .3f, 2.4f, .2f), (PipeZ0 + .6f, 3.0f, .18f), (PipeZ1 - .7f, 3.05f, .14f) })
                    PipeRun(run, new(PipeX0 + .1f, z), new(PipeX1 - .1f, z), y, r);
                foreach (var x in new[] { 20.5f, 23.4f, 26f })
                {
                    Pipe(run, W(x, PipeZ1 - .3f, PipeHeight * .5f), Vector3.up, PipeHeight, .12f);
                    var wheel = Prim(run, PrimitiveType.Cylinder, "ValveWheel", W(x, PipeZ1 - .55f, 1.5f), new Vector3(.45f, .03f, .45f), Mat("LA_Bronze"));
                    wheel.rotation = Quaternion.Euler(90, 0, 0);
                }
                foreach (var (x, z, sz) in new[] { (21f, 19.3f, 1.6f), (24.8f, 19f, 1.2f) })
                    Decal(run, "OilPuddle", x, z, sz, sz * .7f, Mat("LA_PuddleDecal"));
                Point(run, "PipeGlow", W(23f, 19.3f, PipeHeight - .4f), new Color(.85f, .72f, .45f), .9f, 6f);
                BrokenWall(pipes, new Vector2(PipeX0, BreachZ), Vector3.forward, Vector3.right, BreachWidth, Mat("LA_WallConcrete"));

                var store = Group(target, "S05_JunkStorage", W((StoreX0 + StoreX1) * .5f, (StoreZ0 + StoreZ1) * .5f));
                RoomShell(store, "LA_JunkStorage", StoreX0, StoreX1, StoreZ0, StoreZ1, StoreHeight, 2.4f, Mat("LA_FloorConcrete"), Mat("LA_WallConcrete"), Mat("LA_WallRust"),
                    new[] { (0, PipeDoorZ, PipeDoorWidth, .1f) }, westThickness: .1f);
                // The original archive turns a quarter so its back wall becomes this room's north wall; spacing along the wall eases.
                var archive = Group(store, "Archive", store.position);
                var skip = new[] { "InnerWestWall", "NorthPatchedWall", "SouthPatchedWall", "LowRoof", "WestRepairStud" };
                foreach (Transform o in Required("Mood/InteriorArt/Stage02_JunkStorage"))
                {
                    if (skip.Any(n => o.name.StartsWith(n))) continue;
                    var c = FootprintCenter(o);
                    var copy = Copy(o, archive);
                    copy.rotation = Quaternion.Euler(0, 90, 0) * o.rotation;
                    MoveCenter(copy, 34.7f + (c.z - 70f) * .85f, StoreZ1 - .1f - (c.x + 18.2f));
                }
                foreach (var l in archive.GetComponentsInChildren<Light>()) l.color = new Color(1f, .66f, .34f);
                // Nothing may stand in front of the only door: anything mapped there moves north along the west wall.
                var doorway = new Bounds(W(StoreX0 + 1f, PipeDoorZ, 1f), new Vector3(2f, 2f, PipeDoorWidth + .6f));
                foreach (Transform item in archive)
                    if (Footprint(item).Intersects(doorway)) MoveCenter(item, FootprintCenter(item).x, PipeDoorZ + PipeDoorWidth * .5f + .4f + Footprint(item).extents.z);
                // Retired circus equipment under dust sheets in the south half, an aisle to the door left open.
                var sheets = Group(store, "UnderSheets", store.position);
                foreach (var (x, z, sx, sy, sz, yaw) in new[] { (31f, 15.6f, 2.2f, 1.6f, 1.6f, 8f), (35.4f, 15.3f, 3f, 2.2f, 1.8f, -4f), (39.8f, 16.2f, 1.8f, 1.2f, 2.4f, 15f) })
                {
                    Box(sheets, "SheetedShape", W(x, z, sy * .5f), new Vector3(sx, sy, sz), Mat("LA_TentCream"), true).rotation = Quaternion.Euler(0, yaw, 0);
                    Box(sheets, "SheetHem", W(x, z, .12f), new Vector3(sx + .25f, .24f, sz + .25f), Mat("LA_TentCream")).rotation = Quaternion.Euler(0, yaw + 3, 0);
                }
                for (int i = 0; i < 3; i++) PlaceCopy("Mood/InteriorArt/Stage03_PropRoom/StoredMascotHead_" + i, sheets, W(33.2f + i * .7f, 18.2f), 160f + i * 25f);
                foreach (var (x, z, sz) in new[] { (30f, 19.5f, 2f), (37.5f, 18.6f, 2.4f), (40.5f, 23f, 1.6f) })
                    Decal(sheets, "RustStain", x, z, sz, sz * .8f, Mat("LA_RustDecal"));
                var lamp = Required("Mood/Lights").Cast<Transform>().First(t => t.name == "Lobby_Lamp");
                foreach (var p in new[] { W(31.5f, 20.5f, 4f), W(38.5f, 18.5f, 4f) })
                {
                    var copy = Copy(lamp, sheets, "StorageLamp");
                    copy.position = p;
                    foreach (var r in copy.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
                    foreach (var l in copy.GetComponentsInChildren<Light>(true)) { l.intensity *= .55f; l.color = new Color(1f, .62f, .3f); }
                }

                // The restroom's broken wall now leads somewhere: drop its temporary closure.
                var closure = target.Find("S02_Dormitory/Restroom/BreachClosure");
                if (closure != null) Undo.DestroyObjectImmediate(closure.gameObject);
                int remapped = RemapReferences(pipes) + RemapReferences(store);
                Physics.SyncTransforms();
                AssertUnchanged(original);
                ValidateStorageWing(pipes, store);
                Finish(scene, undo, "stage-06-storage-copy-manifest.md", "6단계 배관 구역·폐기물 보관소 복제 목록", store, remapped, 0);
                return $"Storage wing built; pipes={pipes.GetComponentsInChildren<Transform>(true).Length} store={store.GetComponentsInChildren<Transform>(true).Length}; lights={pipes.GetComponentsInChildren<Light>().Length + store.GetComponentsInChildren<Light>().Length}; sourceUnchanged=true";
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        static void ValidateStorageWing(Transform pipes, Transform store)
        {
            var root = pipes.parent;
            if (root.Find("S02_Dormitory/Restroom/BreachClosure") != null) throw new InvalidOperationException("Restroom breach still closed");
            if (pipes.GetComponentsInChildren<Component>(true).Concat(store.GetComponentsInChildren<Component>(true)).Any(c => c == null)) throw new InvalidOperationException("Missing script in storage wing");
            foreach (var (x, z, what) in new[] { (RestX1 - .8f, BreachZ, "restroom side of the breach"), (RestX1 + .25f, BreachZ, "in the breach"), (PipeX0 + 1f, BreachZ, "pipe zone entry"),
                         (23f, 19.3f, "pipe zone middle"), (PipeX1 - .8f, PipeDoorZ, "pipe zone end"), (StoreX0 + 1f, PipeDoorZ, "storage entry"), (34.5f, 19.8f, "storage aisle"), (39.6f, 20f, "storage east") })
                CheckStanding(W(x, z, .1f), root, what);
            // Only one way in: the storage room has no door but the pipe zone's.
            foreach (var r in store.GetComponentsInChildren<Renderer>())
                if (r.bounds.min.x < W(StoreX0 - .2f, 0).x || r.bounds.max.x > W(StoreX1 + .1f, 0).x || r.bounds.max.z > W(0, StoreZ1 + .1f).z || r.bounds.min.z < W(0, StoreZ0 - .1f).z)
                    if (!r.transform.IsChildOf(store.Find("Shell"))) throw new InvalidOperationException("Outside the storage room: " + r.name);
            if (pipes.GetComponentsInChildren<Light>().Length + store.GetComponentsInChildren<Light>().Length > 6) throw new InvalidOperationException("Too many storage wing lights");
        }

        // A wall knocked through: jagged chunks along both jambs and the head, rubble and bent rebar on the floor.
        // inward points into the room this side belongs to.
        static void BrokenWall(Transform parent, Vector2 at, Vector3 along, Vector3 inward, float width, Material face)
        {
            var g = Group(parent, "BrokenWall", W(at.x, at.y));
            var rng = new System.Random(Mathf.RoundToInt(at.x * 31 + at.y * 17));
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var p = W(at.x, at.y) + inward * .04f;
            foreach (var side in new[] { -1f, 1f })
                for (int i = 0; i < 6; i++)
                {
                    float y = .3f + i * .65f;
                    Box(g, "Chunk", p + along * side * (width * .5f + R(-.05f, .12f)) + Vector3.up * y, Size(along, R(.18f, .38f), R(.25f, .5f), .1f), face)
                        .rotation = Quaternion.Euler(R(-12, 12), R(-20, 20), R(-25, 25));
                }
            for (int i = 0; i < 5; i++)
                Box(g, "Chunk", p + along * R(-width * .5f, width * .5f) + Vector3.up * (DoorHeight - R(0, .25f)), Size(along, R(.25f, .45f), R(.2f, .35f), .1f), face)
                    .rotation = Quaternion.Euler(R(-15, 15), R(-20, 20), R(-30, 30));
            for (int i = 0; i < 10; i++)
                Box(g, "Rubble", W(at.x, at.y, .04f) + inward * R(.1f, 1f) + along * R(-width * .6f, width * .6f), Vector3.one * R(.06f, .14f), Mat("LA_FloorConcrete"))
                    .rotation = Quaternion.Euler(R(0, 360), R(0, 360), 0);
            for (int i = 0; i < 3; i++)
                Pipe(g, p + along * (width * .5f - .05f) * (i % 2 == 0 ? 1 : -1) + Vector3.up * R(.6f, 3f), inward + Vector3.up * R(-.3f, .3f), R(.3f, .6f), .012f).name = "Rebar";
        }

        // Stage 5 lounge (plan LG, inside the lobby south of the spawn): a half-open rest corner. A faded curtain screen on the
        // west, a crate pile behind the couch on the south and a short curtain on the east; the north stays open to the tent.
        // No gameplay function. Built as its own root so a lobby rebuild keeps it.
        const float LoungeX0 = -2.8f, LoungeX1 = 2.8f, LoungeZ0 = -11.8f, LoungeZ1 = -6.3f;
        const string SyntyProps = "Assets/_ThirdParty/Synty/PolygonApocalypse/Prefabs/Props/";

        public static string BuildLounge()
        {
            var (scene, target) = Begin();
            if (target.Find("S01_Lobby") == null) throw new InvalidOperationException("Build the lobby first.");
            var original = Snapshot(source);
            int undo = StartUndo("Layout A: build lounge");
            try
            {
                Remove(target, "LG_Lounge");
                var lounge = Group(target, "LG_Lounge", W((LoungeX0 + LoungeX1) * .5f, (LoungeZ0 + LoungeZ1) * .5f));
                // Layered worn carpet and a patterned rug mark the corner on the cracked slab.
                Box(lounge, "WornCarpet", W(0, -9.4f, .012f), new Vector3(4.8f, .01f, 3.8f), Mat("LA_CarpetWorn")).rotation = Quaternion.Euler(0, 4, 0);
                PlaceProp(SyntyProps + "SM_Prop_Rug_05", lounge, W(-.1f, -9.5f, .02f), 0, false);

                var seats = Group(lounge, "Seating", lounge.position);
                PlaceProp(SyntyProps + "SM_Prop_Couch_01", seats, W(0, -11.15f), 0);
                PlaceProp(SyntyProps + "SM_Prop_Chair_Soft_01", seats, W(-2.05f, -9.3f), 75);
                PlaceProp(SyntyProps + "SM_Prop_Chair_Soft_01", seats, W(2.1f, -9.7f), -100);
                PlaceProp(SyntyProps + "SM_Prop_CoffeeTable_02", seats, W(0, -9.35f), 6);
                PlaceProp(SyntyProps + "SM_Prop_Radio_01", seats, W(.35f, -9.3f, .53f), -20, false);
                PlaceProp("Assets/_ThirdParty/Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Mug_01", seats, W(-.3f, -9.45f, .53f), 40, false);
                // Side crate with a small lamp: the corner's one warm pool.
                PlaceCopy("Mood/InteriorArt/CircusStorageCrate_0", seats, W(-2.15f, -11.2f), 10f);
                var lamp = PlaceProp(SyntyProps + "SM_Prop_Lamp_01", seats, W(-2.15f, -11.2f), 30, false);
                lamp.position += Vector3.up * (Footprint(seats.GetChild(seats.childCount - 2)).max.y + .01f);
                Point(lounge, "LampPool", lamp.position + Vector3.up * .45f, new Color(1f, .7f, .42f), 1.6f, 5.5f, LightShadows.Soft);

                // Screens: curtain on posts west, crate pile south, a short curtain east.
                var screens = Group(lounge, "Screens", lounge.position);
                CurtainScreen(screens, new Vector2(LoungeX0 - .1f, LoungeZ0 + .2f), new Vector2(LoungeX0 - .1f, -7.6f), 2.5f);
                CurtainScreen(screens, new Vector2(LoungeX1 + .1f, LoungeZ0 + .2f), new Vector2(LoungeX1 + .1f, -10.1f), 2.2f);
                PlaceCopy("Mood/InteriorArt/CircusStorageCrate_1", screens, W(-1.2f, -12.2f), -6f);
                PlaceCopy("Mood/InteriorArt/StorageTrunk_1", screens, W(.9f, -12.25f), 2f);
                PlaceCopy("Mood/InteriorArt/CanvasOnCrate_1", screens, W(-1.2f, -12.2f, .7f), 20f);
                for (int i = 0; i < 3; i++) PlaceFactory("SM_Woodpallet_1", screens, W(-2.3f, -12.5f, i * .29f), 90 + i * 6); // west end, clear of the 12 door

                // A sagging bulb string between the screen posts, lower and dimmer than the hall's strings.
                BulbString(lounge, W(LoungeX0 - .1f, -7.6f, 2.5f), W(LoungeX1 + .1f, -10.1f, 2.2f), .7f);
                Decal(lounge, "RustStain", 2.2f, -7.4f, 1.4f, 1.1f, Mat("LA_RustDecal"));

                int remapped = RemapReferences(lounge);
                Physics.SyncTransforms();
                AssertUnchanged(original);
                ValidateLounge(lounge);
                Finish(scene, undo, "stage-05-lounge-copy-manifest.md", "5단계 휴게실 복제·배치 목록", lounge, remapped, 0);
                return $"Lounge built; objects={lounge.GetComponentsInChildren<Transform>(true).Length}; lights={lounge.GetComponentsInChildren<Light>().Length}; sourceUnchanged=true";
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        // Stage 5 practice room (plan 09, north-west of the lobby), enlarged on 2026-10-27 after the planner's concept art:
        // a high factory bay around a raised circus ring with a radial star floor, harlequin skirt and low railing, under a torn
        // striped canopy and a circular light rig; two small stands, cages, banners and pennants. Big enough to actually practise:
        // a tightrope lane across the ring between two wooden towers, and a human-cannon lane from the ring's south edge
        // to the landing mats in the north. The practice interactions themselves come later (M2); only their lanes are kept clear.
        const float PracX0 = -16.1f, PracX1 = .8f, PracZ0 = 14.5f, PracZ1 = 36f, PracHeight = 12f, PracDoorX = -7.8f, PracDoorWidth = 3.8f;
        static readonly Vector2 PracRing = new(-7.65f, 25.2f), CannonLaunch = new(-7.65f, 21f), CannonLanding = new(-7.65f, 33.9f);
        // Towers stand just off the ring with a walkway behind them, so the ring can be walked round on the floor.
        const float PracRingRadius = 5.6f, PracRingDeck = .18f, PracWestDoorZ = 18.5f, RopeHeight = 3.5f, TowerWestX = -14.25f, TowerEastX = -1.05f;

        // West corridor (plan band under 10): from the lobby west door to the stairs (west end, down to B1),
        // with the workshop (10) door on its north side. Stairs and 10 are later stages, so both doors start closed.
        const float WcX0 = -27.8f, WcX1 = -15.2f, WcZ0 = 8.15f, WcZ1 = 11.75f, WcHeight = 5f, WcDoorZ = 9.95f, WcDoorWidth = 3.2f, WcNorthDoorX = -22f;

        public static string BuildWestCorridor()
        {
            var (scene, target) = Begin();
            if (target.Find("S01_Lobby") == null) throw new InvalidOperationException("Build the lobby first.");
            var original = Snapshot(source);
            int undo = StartUndo("Layout A: build west corridor");
            try
            {
                Remove(target, "WC_WestCorridor");
                var root = Group(target, "WC_WestCorridor", W((WcX0 + WcX1) * .5f, (WcZ0 + WcZ1) * .5f));
                RoomShell(root, "LA_WestCorridor", WcX0, WcX1, WcZ0, WcZ1, WcHeight, 2.2f, Mat("LA_FloorConcrete"), Mat("LA_WallConcrete"), Mat("LA_WallRust"),
                    new[] { (2, WcDoorZ, WcDoorWidth, .5f), (0, WcDoorZ, WcDoorWidth, .1f), (1, WcNorthDoorX, 2.8f, .1f) });
                var floorCollider = new GameObject("FloorCollider"); floorCollider.transform.SetParent(root, false);
                floorCollider.transform.position = W((WcX0 - .1f - LobbyW + .1f) * .5f, (WcZ0 + WcZ1) * .5f, -.1f);
                floorCollider.AddComponent<BoxCollider>().size = new Vector3(-LobbyW + .1f - WcX0 + .1f, .2f, WcZ1 - WcZ0 + .2f);

                var connections = Group(root, "Connections", root.position);
                TempClosure(connections, new Vector2(WcX0, WcDoorZ), Vector3.forward, Vector3.right, WcDoorWidth, "계단 (B1)");
                TempClosure(connections, new Vector2(WcNorthDoorX, WcZ1), Vector3.right, Vector3.back, 2.8f, "10 작업실");

                // A plain service passage: pipes overhead, a few drums and crates against the south wall, two dim caged lamps.
                var dressing = Group(root, "Dressing", root.position);
                PipeRun(dressing, new(WcX0 + .3f, WcZ1 - .3f), new(WcX1 - .3f, WcZ1 - .3f), 3.7f, .2f);
                PipeRun(dressing, new(WcX0 + .3f, WcZ1 - .65f), new(WcX1 - .3f, WcZ1 - .65f), 3.9f, .1f);
                PlaceFactory("SM_Barrel", dressing, W(-25.2f, WcZ0 + .55f), 10);
                PlaceFactory("SM_Barrel_1", dressing, W(-24.4f, WcZ0 + .5f), 0);
                PlaceCopy("Mood/InteriorArt/CircusStorageCrate_0", dressing, W(-18.6f, WcZ0 + .6f), 4f);
                foreach (var (x, z, s) in new[] { (-20f, 9.6f, 1.8f), (-26.2f, 10.6f, 1.4f) })
                    Decal(dressing, "RustStain", x, z, s, s * .7f, Mat("LA_RustDecal"));
                var lamp = Required("Mood/Lights").Cast<Transform>().First(t => t.name == "Lobby_Lamp");
                foreach (var x in new[] { -24.5f, -18.8f })
                {
                    var copy = Copy(lamp, dressing, "CorridorLamp");
                    copy.position = W(x, (WcZ0 + WcZ1) * .5f, 3.3f);
                    foreach (var r in copy.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
                    foreach (var l in copy.GetComponentsInChildren<Light>(true)) l.intensity *= .7f;
                }

                OpenPortal(target, "WC");
                int remapped = RemapReferences(root);
                Physics.SyncTransforms();
                AssertUnchanged(original);
                ValidateWestCorridor(root);
                Finish(scene, undo, "stage-overall-westcorridor-manifest.md", "전체 공간 다듬기 — 서쪽 복도 목록", root, remapped, 0);
                return $"West corridor built; objects={root.GetComponentsInChildren<Transform>(true).Length}; lights={root.GetComponentsInChildren<Light>().Length}; sourceUnchanged=true";
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        static void ValidateWestCorridor(Transform root)
        {
            var portal = root.parent.Find("S01_Lobby/FutureConnections/Portal_WC");
            if (portal == null || portal.Find("TemporaryClosure") != null) throw new InvalidOperationException("Lobby west corridor portal missing or closed");
            if (root.parent.Find("S01_Lobby/FutureConnections/Portal_13") != null || root.parent.Find("S01_Lobby/FutureConnections/Portal_10") != null)
                throw new InvalidOperationException("Old lobby west doors still present; rebuild the lobby");
            if (root.GetComponentsInChildren<Component>(true).Any(c => c == null)) throw new InvalidOperationException("Missing script in west corridor");
            if (!Physics.Raycast(W(-LobbyW - .25f, WcDoorZ, 1f), Vector3.down, out var hit, 2f) || Mathf.Abs(hit.point.y) > .01f)
                throw new InvalidOperationException("No floor under the west corridor doorway");
            foreach (var (x, z, what) in new[] { (-LobbyW - .25f, WcDoorZ, "corridor doorway"), (WcX1 - 1f, WcDoorZ, "corridor east"), (-21.5f, WcDoorZ, "corridor middle"), (WcX0 + 1.2f, WcDoorZ, "corridor west end") })
                CheckStanding(W(x, z, .1f), root.parent, what);
            // Both doors into unbuilt rooms stay shut.
            if (!Physics.Raycast(W(WcX0 + 1.2f, WcDoorZ, 1f), Vector3.left, 2f)) throw new InvalidOperationException("Stairs door is open into nothing");
            if (!Physics.Raycast(W(WcNorthDoorX, WcZ1 - 1f, 1f), Vector3.forward, 2f)) throw new InvalidOperationException("Workshop door is open into nothing");
            if (root.GetComponentsInChildren<Light>().Length > 3) throw new InvalidOperationException("Too many west corridor lights");
        }

        // Temporary closure for a doorway into a room not built yet: drawn curtain behind a half-down shutter, with the room's name.
        // inward points into the room being built; the closure sits in the wall thickness on the far side.
        static void TempClosure(Transform parent, Vector2 at, Vector3 along, Vector3 inward, float width, string label)
        {
            var p = W(at.x, at.y);
            Box(parent, "TemporaryClosure", p + Vector3.up * DoorHeight * .5f - inward * .3f, Size(along, width, DoorHeight, .1f), Mat("V4_Velvet_Red_Cloth"), true);
            Box(parent, "RollerShutter", p + Vector3.up * (DoorHeight - .7f) - inward * .2f, Size(along, width + .1f, 1.4f, .06f), Mat("V4_Ceiling_RustyMetal"));
            var sign = Copy(Required("S01_Lobby/Sign_② 숙소"), parent, "DestinationSign", false);
            sign.gameObject.SetActive(true);
            sign.SetPositionAndRotation(p + inward * .12f + Vector3.up * (DoorHeight + .35f), Quaternion.LookRotation(-inward));
            var text = sign.GetComponent<TextMesh>();
            text.text = label; text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
            text.fontSize = 64; text.characterSize = .05f; text.color = new Color(.82f, .7f, .5f);
        }

        public static string BuildPracticeRoom()
        {
            var (scene, target) = Begin();
            if (target.Find("S01_Lobby") == null) throw new InvalidOperationException("Build the lobby first.");
            var original = Snapshot(source);
            int undo = StartUndo("Layout A: build practice room");
            try
            {
                Remove(target, "S09_PracticeRoom");
                var room = Group(target, "S09_PracticeRoom", W((PracX0 + PracX1) * .5f, (PracZ0 + PracZ1) * .5f));
                RoomShell(room, "LA_Practice", PracX0, PracX1, PracZ0, PracZ1, PracHeight, 2.4f, Mat("LA_FloorConcrete"), Mat("LA_WallConcrete"), Mat("LA_WallRust"),
                    new[] { (3, PracDoorX, PracDoorWidth, .5f), (0, PracWestDoorZ, 2.8f, .5f) });
                // 09 - 10 door in the west wall; closed until the workshop is built.
                var connections = Group(room, "Connections", room.position);
                TempClosure(connections, new Vector2(PracX0, PracWestDoorZ), Vector3.forward, Vector3.right, 2.8f, "10 작업실");
                var floorCollider = new GameObject("FloorCollider"); floorCollider.transform.SetParent(room, false);
                floorCollider.transform.position = W((PracX0 + PracX1) * .5f, (LobbyN - .1f + PracZ1) * .5f, -.1f);
                floorCollider.AddComponent<BoxCollider>().size = new Vector3(PracX1 - PracX0 + .2f, .2f, PracZ1 - LobbyN + .1f);
                var overhead = Group(room, "Overhead", room.position);
                foreach (var z in new[] { 17.5f, 22.4f, 28f, 33.2f })
                    Box(overhead, "RoofBeam", W((PracX0 + PracX1) * .5f, z, PracHeight - .25f), new Vector3(PracX1 - PracX0, .5f, .3f), Mat("V4_Iron_Dark"));
                PipeRun(overhead, new(PracX0 + .4f, PracZ1 - .4f), new(PracX1 - .4f, PracZ1 - .4f), 10.6f, .22f);
                PipeRun(overhead, new(PracX1 - .4f, PracZ0 + .4f), new(PracX1 - .4f, PracZ1 - .4f), 10.2f, .16f);
                BoardedWindow(overhead, W(-4f, PracZ1, 8.4f), Vector3.right, Vector3.back, 3f, 1.4f);
                BoardedWindow(overhead, W(-11.5f, PracZ1, 8.4f), Vector3.right, Vector3.back, 3f, 1.4f);

                BuildPracticeRing(Group(room, "PracticeRing", W(PracRing.x, PracRing.y)));
                BuildPracticeLanes(Group(room, "PracticeLanes", room.position));
                BuildPracticeCanopy(Group(room, "CanopyAndRig", W(PracRing.x, PracRing.y)));
                BuildPracticeSurroundings(Group(room, "Surroundings", room.position));

                OpenPortal(target, "09");
                int remapped = RemapReferences(room);
                Physics.SyncTransforms();
                AssertUnchanged(original);
                ValidatePracticeRoom(room);
                Finish(scene, undo, "stage-05-practice-copy-manifest.md", "5단계 연습실(확장) 복제·배치 목록", room, remapped, 0);
                return $"Practice room built; objects={room.GetComponentsInChildren<Transform>(true).Length}; lights={room.GetComponentsInChildren<Light>().Length}; sourceUnchanged=true";
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        // Raised ring (low enough to step onto): radial star floor, harlequin skirt, wooden rim and a low railing open
        // at the four compass points (S: lobby door and cannon launch, N: landing, E/W: tightrope towers).
        static void BuildPracticeRing(Transform ring)
        {
            var c = W(PracRing.x, PracRing.y);
            var deck = Prim(ring, PrimitiveType.Cylinder, "DeckCollider", c + Vector3.up * PracRingDeck * .5f, new Vector3(PracRingRadius * 2, PracRingDeck * .5f, PracRingRadius * 2), Mat("V4_Wood_Old"));
            Object.DestroyImmediate(deck.GetComponent<MeshRenderer>());
            deck.gameObject.AddComponent<MeshCollider>().sharedMesh = deck.GetComponent<MeshFilter>().sharedMesh;
            MeshObject(ring, "StarFloor", c + Vector3.up * (PracRingDeck + .004f), SaveMesh(DiscMesh("LA_RingDisc", PracRingRadius - .12f, 64)), Mat("LA_RingFloor"));
            MeshObject(ring, "WoodenRim", c + Vector3.up * (PracRingDeck + .008f), SaveMesh(RingMesh("LA_RingRim", PracRingRadius - .16f, PracRingRadius + .04f, 64)), Mat("V4_Wood_Old"));
            for (int i = 0; i < 48; i++)
            {
                float deg = i * 7.5f;
                var p = Ring(c, deg, PracRingRadius + .02f);
                Box(ring, "HarlequinSkirt", p + Vector3.up * PracRingDeck * .5f, new Vector3(2 * Mathf.PI * PracRingRadius / 48f + .02f, PracRingDeck, .04f), Mat("V4_Harlequin"))
                    .rotation = Quaternion.LookRotation(p - c);
            }
            var openings = new[] { 0f, 90f, 180f, 270f };
            bool Open(float deg) => openings.Any(o => Mathf.Abs(Mathf.DeltaAngle(deg, o)) < 13f);
            for (int i = 0; i < 36; i++)
            {
                float deg = i * 10f;
                if (Open(deg)) continue;
                var p = Ring(c, deg, PracRingRadius - .12f);
                Box(ring, "RailPost", p + Vector3.up * (PracRingDeck + .45f), new Vector3(.08f, .9f, .08f), Mat("V4_Wood_Old"), true);
                if (i % 3 == 0) Bulb(ring, p + Vector3.up * (PracRingDeck + .98f));
                if (Open(deg + 10f)) continue;
                var q = Ring(c, deg + 10f, PracRingRadius - .12f);
                Box(ring, "RailTop", (p + q) * .5f + Vector3.up * (PracRingDeck + .85f), new Vector3(Vector3.Distance(p, q) + .06f, .06f, .06f), Mat("V4_Wood_Old"), true)
                    .rotation = Quaternion.LookRotation(Vector3.Cross(q - p, Vector3.up));
                Box(ring, "RailPennant", (p + q) * .5f + Vector3.up * (PracRingDeck + .62f), new Vector3(Vector3.Distance(p, q) - .1f, .36f, .02f), Mat(i % 2 == 0 ? "V4_Harlequin" : "LA_TentRed"))
                    .rotation = Quaternion.LookRotation(Vector3.Cross(q - p, Vector3.up));
            }
        }

        // Lanes kept clear for the practice interactions, marked by floor tape: tightrope across the ring between two
        // wooden towers (deck at the rope height, ladder on the wall side), human cannon from the ring's south edge to the mats.
        static void BuildPracticeLanes(Transform lanes)
        {
            Group(lanes, "TightropeLane", W(PracRing.x, PracRing.y, RopeHeight));
            Group(lanes, "CannonLaunch", W(CannonLaunch.x, CannonLaunch.y, PracRingDeck));
            Group(lanes, "CannonLanding", W(CannonLanding.x, CannonLanding.y));
            foreach (var (x, side) in new[] { (TowerWestX, 1f), (TowerEastX, -1f) })
            {
                var t = W(x, PracRing.y);
                foreach (var dx in new[] { -.8f, .8f }) foreach (var dz in new[] { -.8f, .8f })
                    Box(lanes, "TowerPost", t + new Vector3(dx, (RopeHeight + 1f) * .5f, dz), new Vector3(.14f, RopeHeight + 1f, .14f), Mat("V4_Wood_Old"), true);
                Box(lanes, "TowerDeck", t + Vector3.up * RopeHeight, new Vector3(1.8f, .1f, 1.8f), Mat("V4_Floor_OldPlank"), true);
                foreach (var dz in new[] { -.8f, .8f })
                    Box(lanes, "TowerRail", t + new Vector3(0, RopeHeight + .9f, dz), new Vector3(1.7f, .06f, .06f), Mat("V4_Wood_Old"));
                foreach (var dz in new[] { -.8f, .8f })
                    Box(lanes, "TowerBrace", t + new Vector3(0, RopeHeight * .5f, dz), new Vector3(.06f, RopeHeight * 1.15f, .06f), Mat("V4_Iron_Dark")).rotation = Quaternion.Euler(0, 90, 30);
                PlaceFactory("SM_Ladder1", lanes, t + new Vector3(0, 0, -.95f), 180).localScale = new Vector3(1, (RopeHeight + .1f) / 6.32f, 1);
                Box(lanes, "RopeAnchor", t + new Vector3(side * .85f, RopeHeight + .25f, 0), new Vector3(.12f, .4f, .12f), Mat("V4_RustyMetal"));
            }
            TapeRect(lanes, new Vector2(PracRing.x, PracRing.y), TowerEastX - TowerWestX - 1.8f, 1.2f, PracRingDeck);
            TapeRect(lanes, CannonLaunch, 3f, 2.6f, PracRingDeck);
            // Landing: two stacked mats and a slack net frame against the north wall.
            foreach (var (h, yaw) in new[] { (.3f, 0f), (.6f, 3f) })
                Box(lanes, "LandingMat", W(CannonLanding.x, CannonLanding.y, h - .15f), new Vector3(5.4f, .3f, 3f), Mat("LA_MatWorn"), true).rotation = Quaternion.Euler(0, yaw, 0);
            foreach (var dx in new[] { -3f, 3f })
                Box(lanes, "NetPost", W(CannonLanding.x + dx, PracZ1 - .4f, 2.2f), new Vector3(.12f, 4.4f, .12f), Mat("V4_RustyMetal"), true);
            Box(lanes, "CatchNet", W(CannonLanding.x, PracZ1 - .45f, 2.4f), new Vector3(5.9f, 3.6f, .02f), Mat("V6_Rope"));
            TapeRect(lanes, CannonLanding, 6f, 3.6f, 0f);
        }

        // Torn red-cream canopy high over the ring, a circular truss rig with spot fixtures, and bulb strings to the walls.
        static void BuildPracticeCanopy(Transform top)
        {
            var c = W(PracRing.x, PracRing.y);
            var canopy = MeshObject(top, "TornCanopy", c + Vector3.up * 9.4f, TentMesh("LA_PracticeCanopy", 16, 7.6f, 2.2f), Mat("LA_TentRed"), Mat("LA_TentCream"));
            canopy.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
            Prim(top, PrimitiveType.Cylinder, "CanopyCrown", c + Vector3.up * 11.65f, new Vector3(1f, .1f, 1f), Mat("LA_Bronze"));
            const float rigY = 8.2f, rigR = 2.6f;
            for (int i = 0; i < 24; i++)
            {
                var p = Ring(c, i * 15f, rigR); var q = Ring(c, (i + 1) * 15f, rigR);
                Box(top, "RigTruss", (p + q) * .5f + Vector3.up * rigY, new Vector3(Vector3.Distance(p, q) + .02f, .22f, .22f), Mat("V4_Iron_Dark"))
                    .rotation = Quaternion.LookRotation(Vector3.Cross(q - p, Vector3.up));
            }
            for (int i = 0; i < 4; i++) Rope(top, Ring(c, 45 + i * 90, rigR) + Vector3.up * rigY, Ring(c, 45 + i * 90, rigR * .6f) + Vector3.up * (PracHeight - .4f), .03f);
            for (int i = 0; i < 8; i++)
            {
                var p = Ring(c, i * 45f + 22.5f, rigR) + Vector3.up * (rigY - .3f);
                Prim(top, PrimitiveType.Cylinder, "SpotFixture", p, new Vector3(.32f, .22f, .32f), Mat("V4_Black")).rotation = Quaternion.FromToRotation(Vector3.up, (c - p).normalized);
                Bulb(top, p + (c - p).normalized * .25f);
            }
            var warm = new Color(1f, .78f, .5f);
            Spot(top, "RingSpot_Centre", c + Vector3.up * (rigY - .3f) + Vector3.back * rigR, c, warm, 9f, 16f, 50f);
            Spot(top, "RingSpot_Launch", c + Vector3.up * (rigY - .3f) + Vector3.left * rigR, W(CannonLaunch.x, CannonLaunch.y), warm, 5.5f, 14f, 36f);
            Spot(top, "RingSpot_Landing", c + Vector3.up * (rigY - .3f) + Vector3.forward * rigR, W(CannonLanding.x, CannonLanding.y), warm, 6f, 16f, 36f);
            foreach (var (x, z) in new[] { (PracX0 + .3f, 17f), (PracX0 + .3f, 33f), (PracX1 - .3f, 17f), (PracX1 - .3f, 33f), (-12f, PracZ1 - .3f), (-3.5f, PracZ1 - .3f), (-12f, PracZ0 + .3f), (-3.5f, PracZ0 + .3f) })
            {
                var to = W(x, z, 9.6f);
                BulbString(top, Ring(c, Mathf.Atan2(to.z - c.z, to.x - c.x) * Mathf.Rad2Deg, rigR) + Vector3.up * rigY, to, .9f);
            }
        }

        // Two small stands facing the ring, cages and the old circus props in the corners, banners and pennants on the walls.
        static void BuildPracticeSurroundings(Transform s)
        {
            foreach (var (wallX, dir) in new[] { (PracX0, 1f), (PracX1, -1f) })
            {
                for (int tier = 0; tier < 3; tier++)
                {
                    float h = .45f * (3 - tier), x = wallX + dir * (.35f + tier * .7f);
                    Box(s, "StandTier", W(x, 29.2f, h * .5f), new Vector3(.7f, h, 3.6f), Mat("V4_Wood_Old"), true);
                    Box(s, "StandSeat", W(x, 29.2f, h + .02f), new Vector3(.72f, .05f, 3.62f), Mat("LA_TentRed"));
                }
                Box(s, "StandRail", W(wallX + dir * .1f, 29.2f, 2.3f), new Vector3(.06f, .06f, 3.6f), Mat("V4_Iron_Dark"));
                var lamp = Copy(Required("Mood/Lights").Cast<Transform>().First(t => t.name == "Lobby_Lamp"), s, "StandLamp");
                lamp.position = W(wallX + dir * 1.1f, 31.4f, 3.4f);
                foreach (var r in lamp.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
                foreach (var l in lamp.GetComponentsInChildren<Light>(true)) l.intensity *= .6f;
                Cage(s, W(wallX + dir * .95f, PracZ1 - .95f));
            }
            // Old circus props along the south-west corner, clear of both doors.
            var props = Copy(Required("Mood/Dressing/Lobby_TentPile/CircusProps"), s);
            var spots = new Dictionary<string, Queue<Vector2>>
            {
                ["북"] = new(new Vector2[] { new(-15.2f, 15.4f) }),
                ["BigBall"] = new(new Vector2[] { new(-13.9f, 15.2f) }),
                ["Hoop"] = new(new Vector2[] { new(-12.5f, 15f) }),
                ["트렁크"] = new(new Vector2[] { new(-11.1f, 15.1f) }),
                ["HarlequinBall"] = new(new Vector2[] { new(-13.1f, 16.1f), new(-15.4f, 16.5f), new(-10.4f, 15.8f) }),
                ["JugglingPin"] = new(new Vector2[] { new(-12.1f, 16f), new(-14.6f, 16.4f), new(-11.7f, 15.6f) }),
                ["ClownShoe"] = new(new Vector2[] { new(-13.6f, 16.5f) }),
            };
            foreach (Transform prop in props)
            {
                if (!spots.TryGetValue(prop.name, out var queue) || queue.Count == 0) continue;
                var at = queue.Dequeue();
                MoveCenter(prop, at.x, at.y);
                prop.position += Vector3.up * -Footprint(prop).min.y;
            }
            PlaceFactory("SM_Barrel_1", s, W(PracX1 - .6f, PracZ0 + .7f), 0);
            for (int i = 0; i < 4; i++)
                Pipe(s, W(PracX1 - .6f - i * .06f, 16.2f + i * .35f, 2.2f), new Vector3(-.18f, 1, .02f * i), 4.4f, .025f).name = "BalancePole";
            for (int i = 0; i < 3; i++)
                MeshObject(s, "RopeCoil", W(-2.2f + i * .15f, 15.5f, .02f + i * .05f), SaveMesh(RingMesh("LA_RopeCoil", .22f, .42f, 20)), Mat("LA_TentCream"));
            WallPoster(s, W(-14.2f, PracZ0), 0);
            WallPoster(s, W(-3.4f, PracZ0), 0);
            // Hanging banners and pennant strings.
            foreach (var (x, z, yaw) in new[] { (PracX0, 22f, 90f), (PracX0, 33f, 90f), (PracX1, 22f, -90f), (PracX1, 33f, -90f) })
                Banner(s, W(x, z, 6.6f), Quaternion.Euler(0, yaw, 0) * Vector3.forward);
            foreach (var (a, b) in new[] { (new Vector2(PracX0 + .2f, PracZ0 + .2f), new Vector2(PracX1 - .2f, PracZ0 + .2f)), (new Vector2(PracX0 + .2f, PracZ1 - .2f), new Vector2(PracX1 - .2f, PracZ1 - .2f)),
                         (new Vector2(PracX0 + .2f, PracZ0 + .2f), new Vector2(PracX0 + .2f, PracZ1 - .2f)), (new Vector2(PracX1 - .2f, PracZ0 + .2f), new Vector2(PracX1 - .2f, PracZ1 - .2f)) })
                Pennants(s, W(a.x, a.y, 7.4f), W(b.x, b.y, 7.4f));
            foreach (var (x, z, sz) in new[] { (-4f, 16f, 2.2f), (-14.4f, 24.6f, 1.8f), (-.6f, 34f, 1.6f), (-12.5f, 34.5f, 2f) })
                Decal(s, "RustStain", x, z, sz, sz * .8f, Mat("LA_RustDecal"));
            Point(s, "LandingPool", W(CannonLanding.x, CannonLanding.y, 4.4f), new Color(1f, .7f, .42f), 1.4f, 7f);
        }

        static void Cage(Transform parent, Vector3 at)
        {
            var g = Group(parent, "Cage", at);
            for (int i = 0; i < 14; i++)
            {
                float a = i * Mathf.PI * 2 / 14;
                Box(g, "CageBar", at + new Vector3(Mathf.Cos(a) * .7f, 1f, Mathf.Sin(a) * .7f), new Vector3(.04f, 2f, .04f), Mat("V4_RustyMetal"));
            }
            foreach (var y in new[] { .05f, 1f, 2f })
                MeshObject(g, "CageHoop", at + Vector3.up * y, SaveMesh(RingMesh("LA_CageHoop", .66f, .74f, 28)), Mat("V4_Iron_Dark"));
            Prim(g, PrimitiveType.Sphere, "CageDome", at + Vector3.up * 2f, new Vector3(1.5f, .6f, 1.5f), Mat("V4_RustyMetal"));
            var col = new GameObject("CageCollider"); col.transform.SetParent(g, false); col.transform.position = at + Vector3.up;
            col.AddComponent<BoxCollider>().size = new Vector3(1.5f, 2f, 1.5f);
        }

        static void Banner(Transform parent, Vector3 wallPoint, Vector3 facing)
        {
            var p = wallPoint + facing * .08f;
            var along = Vector3.Cross(Vector3.up, facing);
            Box(parent, "BannerRod", p + Vector3.up * 2f, Size(along, 1.5f, .06f, .06f), Mat("LA_Bronze"));
            Box(parent, "Banner", p, Size(along, 1.2f, 3.8f, .03f), Mat("LA_TentRed"));
            Box(parent, "BannerDiamond", p + facing * .02f + Vector3.up * .3f, new Vector3(.55f, .55f, .02f), Mat("LA_TentCream")).rotation = Quaternion.LookRotation(facing) * Quaternion.Euler(0, 0, 45);
            foreach (var s in new[] { -.3f, .3f })
                Box(parent, "BannerTail", p + Vector3.down * 2.05f + along * s, new Vector3(.5f, .4f, .03f), Mat("LA_TentRed")).rotation = Quaternion.LookRotation(facing) * Quaternion.Euler(0, 0, s > 0 ? 35 : -35);
        }

        static void Pennants(Transform parent, Vector3 a, Vector3 b)
        {
            Rope(parent, a, b, .012f);
            float len = Vector3.Distance(a, b); var dir = (b - a).normalized; var facing = Vector3.Cross(dir, Vector3.up);
            for (float d = .3f; d < len - .2f; d += .6f)
            {
                var p = a + dir * d + Vector3.down * (.12f + Mathf.Sin(d / len * Mathf.PI) * .25f);
                var f = Box(parent, "Pennant", p, new Vector3(.3f, .3f, .01f), Mat((int)(d / .6f) % 2 == 0 ? "LA_TentRed" : "LA_TentCream"));
                f.rotation = Quaternion.LookRotation(facing) * Quaternion.Euler(0, 0, 45);
                f.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        // Disc with UVs spanning 0..1 across its diameter, so a radial texture sits centred on it.
        static Mesh DiscMesh(string name, float radius, int segments)
        {
            var mesh = new Mesh { name = name };
            var v = new List<Vector3> { Vector3.zero }; var uv = new List<Vector2> { new(.5f, .5f) }; var tri = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments;
                v.Add(new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius));
                uv.Add(new Vector2(.5f + Mathf.Cos(a) * .5f, .5f + Mathf.Sin(a) * .5f));
                if (i > 0) tri.AddRange(new[] { 0, i + 1, i });
            }
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0);
            mesh.SetNormals(Enumerable.Repeat(Vector3.up, v.Count).ToList());
            mesh.RecalculateTangents(); mesh.RecalculateBounds();
            return mesh;
        }

        // Worn radial ring floor: 16 red / cream wedges, an eight-point star in the middle, a dark outer band, scuffed paint.
        static Texture2D RingFloorTexture()
        {
            EnsureFolder();
            string path = $"{AssetDir}/LA_RingFloor.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            const int size = 1024;
            Color red = Hex("8E1F1C"), cream = Hex("D9C7A3"), dark = Hex("4A1412"), gold = Hex("C9963B");
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = (x - size * .5f) / (size * .5f), py = (y - size * .5f) / (size * .5f);
                float r = Mathf.Sqrt(px * px + py * py), a = Mathf.Atan2(py, px);
                int wedge = Mathf.FloorToInt((a + Mathf.PI) / (Mathf.PI * 2) * 16);
                var c = wedge % 2 == 0 ? red : cream;
                float star = .34f + .2f * Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 4)), 6f);
                if (r < star) c = (wedge % 2 == 0) ? cream : red;
                if (r < .08f) c = gold;
                if (r > .9f) c = dark;
                if (Mathf.Abs(r - .9f) < .012f || Mathf.Abs(r - .62f) < .008f) c = gold;
                float wear = Mathf.PerlinNoise(x * .012f, y * .012f) * .6f + Mathf.PerlinNoise(x * .05f, y * .05f) * .4f;
                c = Color.Lerp(c, Hex("6A5A48"), Mathf.Clamp01((wear - .55f) * 1.6f) * .7f) * (.82f + wear * .2f);
                pixels[y * size + x] = new Color(c.r, c.g, c.b);
            }
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            tex.SetPixels(pixels); tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Clamp; importer.anisoLevel = 4; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void TapeRect(Transform parent, Vector2 c, float sx, float sz, float floorY = 0f)
        {
            foreach (var s in new[] { -1f, 1f })
            {
                Box(parent, "FloorTape", W(c.x, c.y + s * sz * .5f, floorY + .012f), new Vector3(sx, .004f, .06f), Mat("LA_HazardPaint")).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                Box(parent, "FloorTape", W(c.x + s * sx * .5f, c.y, floorY + .012f), new Vector3(.06f, .004f, sz), Mat("LA_HazardPaint")).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        static void ValidatePracticeRoom(Transform room)
        {
            var portal = room.parent.Find("S01_Lobby/FutureConnections/Portal_09");
            if (portal != null && portal.Find("TemporaryClosure") != null) throw new InvalidOperationException("Practice room portal still closed");
            if (room.GetComponentsInChildren<Component>(true).Any(c => c == null)) throw new InvalidOperationException("Missing script in practice room");
            if (!Physics.Raycast(W(PracDoorX, LobbyN + .25f, 1f), Vector3.down, out var hit, 2f) || Mathf.Abs(hit.point.y) > .01f)
                throw new InvalidOperationException("No floor under the practice room doorway");
            foreach (var (x, z, what) in new[] { (PracDoorX, LobbyN + .25f, "practice doorway"), (PracDoorX, PracZ0 + 1.2f, "practice entry"), (PracRing.x, PracRing.y - PracRingRadius + .4f, "ring south opening"),
                         (PracRing.x, PracRing.y, "ring centre"), (CannonLaunch.x, CannonLaunch.y, "cannon launch"), (PracRing.x, PracRing.y + PracRingRadius + .6f, "landing front"),
                         (PracX0 + .9f, PracWestDoorZ, "in front of the 10 door"), (-15.2f, 21.5f, "west aisle"), (0f, 21.5f, "east aisle"), (-15.65f, PracRing.y, "behind the west tower"), (.35f, PracRing.y, "behind the east tower"), (-12.4f, 34.6f, "north-west aisle"), (-3f, 34.6f, "north-east aisle") })
                CheckStanding(W(x, z, .1f), room.parent, what);
            // Both lanes must be clear of anything solid: the rope line across the ring and the cannon's flight corridor.
            var rope = Physics.OverlapBox(W((TowerWestX + TowerEastX) * .5f, PracRing.y, RopeHeight + .8f), new Vector3((TowerEastX - TowerWestX - 2f) * .5f, 2.2f, .7f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
                .Where(c => c.transform.IsChildOf(room)).ToArray();
            if (rope.Length > 0) throw new InvalidOperationException("Tightrope lane blocked: " + string.Join(",", rope.Select(c => c.name)));
            var flight = Physics.OverlapBox(W(PracRing.x, (CannonLaunch.y + CannonLanding.y) * .5f, 5f), new Vector3(1.5f, 3.6f, (CannonLanding.y - CannonLaunch.y) * .5f - 1.6f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
                .Where(c => c.transform.IsChildOf(room)).ToArray();
            if (flight.Length > 0) throw new InvalidOperationException("Cannon lane blocked: " + string.Join(",", flight.Select(c => c.name)));
            if (TowerEastX - TowerWestX < 12f || CannonLanding.y - CannonLaunch.y < 12f) throw new InvalidOperationException("Practice lanes too short");
            if (room.GetComponentsInChildren<Light>().Length > 8) throw new InvalidOperationException("Too many practice room lights");
            foreach (var r in room.GetComponentsInChildren<Renderer>())
                if (r.bounds.min.x < W(PracX0 - .05f, 0).x || r.bounds.max.x > W(PracX1 + .05f, 0).x || r.bounds.max.z > W(0, PracZ1 + .05f).z || r.bounds.max.y > PracHeight + .3f)
                    if (!r.transform.IsChildOf(room.Find("Shell")) && !r.transform.IsChildOf(room.Find("Overhead")) && !r.transform.IsChildOf(room.Find("Connections")))
                        throw new InvalidOperationException("Outside the practice room: " + r.name);
            if (!Physics.Raycast(W(PracX0 + .9f, PracWestDoorZ, 1f), Vector3.left, 2f)) throw new InvalidOperationException("09 - 10 door is open into nothing");
        }

        // Flat annulus (or disc when inner = 0) in the XZ plane around the local origin.
        static Mesh RingMesh(string name, float inner, float outer, int segments)
        {
            var b = new MeshBuilder(name, 1);
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2 / segments, a1 = (i + 1) * Mathf.PI * 2 / segments;
                Vector3 P(float r, float a) => new(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                b.Quad(0, P(inner, a0), P(outer, a0), P(outer, a1), P(inner, a1), Vector3.up, 1.5f);
            }
            return b.Build();
        }

        static Transform PlaceProp(string prefab, Transform parent, Vector3 position, float yaw, bool collide = true)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefab + ".prefab") ?? throw new InvalidOperationException("Missing prefab " + prefab);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            // Small table-top props and the rug are walked over or never touched; their colliders only snag the player.
            if (!collide) foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            Manifest.Add($"- 에셋 {Path.GetFileName(prefab)} → {AnimationUtility.CalculateTransformPath(go.transform, null)}");
            return go.transform;
        }

        static void CurtainScreen(Transform parent, Vector2 a, Vector2 b, float height)
        {
            foreach (var p in new[] { a, b })
                Box(parent, "ScreenPost", W(p.x, p.y, height * .5f), new Vector3(.08f, height, .08f), Mat("V4_Iron_Dark"), true);
            var mid = (a + b) * .5f; float length = Vector2.Distance(a, b);
            var along = new Vector3(b.x - a.x, 0, b.y - a.y).normalized;
            Box(parent, "ScreenRod", W(mid.x, mid.y, height - .05f), Size(along, length, .04f, .04f), Mat("LA_Bronze"));
            // Hung slightly short of the floor and of one post, so it reads as cloth rather than a wall.
            var cloth = Box(parent, "FadedCurtain", W(mid.x, mid.y, (height - .1f + .35f) * .5f) - along * .25f, Size(along, length - .6f, height - .45f, .05f), Mat("LA_TentRed"), true);
            cloth.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
        }

        static void ValidateLounge(Transform lounge)
        {
            if (lounge.GetComponentsInChildren<Component>(true).Any(c => c == null)) throw new InvalidOperationException("Missing script in lounge");
            var spawn = lounge.parent.Find("S01_Lobby/Gameplay/SpawnPoints");
            foreach (Transform p in spawn) if (p.name.StartsWith("Spawn_")) CheckStanding(p.position, lounge.parent, "spawn " + p.name);
            foreach (var (x, z, what) in new[] { (0f, -7.4f, "lounge front"), (-1.1f, -8.5f, "west chair side"), (1.1f, -8.5f, "east chair side"), (0f, LobbyS + .8f, "behind the corner"), (-4f, -9.3f, "west of the screen"), (4f, -9.3f, "east of the screen") })
                CheckStanding(W(x, z, .1f), lounge.parent, what);
            if (lounge.GetComponentsInChildren<Light>().Length > 2) throw new InvalidOperationException("Too many lounge lights");
        }

        // The night lock (lobby <-> 06) moves onto the wider 06 door. The roller shutter hangs on the corridor side so its housing
        // clears the lobby marquee; the locked sign and warning lamp face the lobby, where players wait at night.
        static void BuildNightShutter(Transform corridor)
        {
            var night = Copy(Required("NightSchedule"), corridor);
            Object.DestroyImmediate(night.Find("ScheduleCloseUp").gameObject); // the lobby already owns the schedule close-up
            var door = night.Find("WaitingRoomShutter");
            var shutter = door.Find("Shutter");
            var widen = new Vector3(1, DoorHeight / 3f, RouteDoorWidth / 3.33f); // old opening 3.33 x 3 m
            door.rotation = Quaternion.Euler(0, 180, 0) * door.rotation;
            door.localScale = Vector3.Scale(door.localScale, widen);
            float plane = CorX0 + .25f;
            door.position += W(plane, RouteDoorZ) - new Vector3(shutter.position.x, 0, shutter.position.z);
            foreach (var t in new[] { door.Find("Sign_잠김"), door.Find("WarningLamp") })
            {
                t.localScale = Vector3.Scale(t.localScale, new Vector3(1 / widen.x, 1 / widen.y, 1 / widen.z));
                t.rotation = Quaternion.Euler(0, 180, 0) * t.rotation;
            }
            var sign = door.Find("Sign_잠김");
            sign.position = new Vector3(2 * W(plane, 0).x - sign.position.x, sign.position.y, sign.position.z); // mirrored to the lobby face of the shutter
            var lamp = door.Find("WarningLamp");
            lamp.position = new Vector3(W(LobbyW - .2f, 0).x, DoorHeight + .75f, lamp.position.z); // above the marquee, on the lobby wall
        }

        // The waiting-room exit watch (start of the exploration timer): the union of the built waiting-room spaces.
        // Walking out through the 06 door counts as going backstage. Rebuild after adding a room that belongs to the waiting room.
        // Undecided (2026-10-27): whether the new rooms from stage 5 on (09 and beyond, outside the lobby) are waiting room or backstage.
        // Until the planner decides, they are not added here; the lounge sits inside the lobby box already.
        static void BuildWaitingRoomArea(Transform target)
        {
            Remove(target, "ExplorationTimer");
            var area = Copy(Required("ExplorationTimer/WaitingRoomArea"), Group(target, "ExplorationTimer", Origin));
            var boxes = area.GetComponentsInChildren<BoxCollider>(true);
            if (boxes.Length != 4) throw new InvalidOperationException("Expected four waiting-room area boxes");
            var rects = new (string name, float x0, float x1, float z0, float z1, bool on)[]
            {
                ("Area_Lobby", -LobbyW, NotchX, LobbyS, LobbyN, true),
                ("Area_LobbyEast", NotchX, LobbyW, NotchZ, LobbyN, true),
                ("Area_Dormitory", DormX0, RestX1, LobbyN, DormZ1, target.Find("S02_Dormitory") != null),
                ("Area_PropRoom", NotchX, PropX1, PropZ0, NotchZ, target.Find("S04_PropRoom") != null),
            };
            for (int i = 0; i < 4; i++)
            {
                var (name, x0, x1, z0, z1, on) = rects[i];
                var box = boxes[i];
                box.name = name;
                box.transform.SetPositionAndRotation(W((x0 + x1) * .5f, (z0 + z1) * .5f, 3f), Quaternion.identity);
                box.transform.localScale = Vector3.one;
                box.center = Vector3.zero; box.size = new Vector3(x1 - x0, 8f, z1 - z0); box.enabled = on;
            }
        }

        static void ValidateStageRoute(Scene scene, Transform corridor, Transform stage)
        {
            T Active<T>(Transform under) where T : Component
            {
                var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>()).Where(c => c.gameObject.activeInHierarchy).ToArray();
                if (all.Length != 1 || !all[0].transform.IsChildOf(under)) throw new InvalidOperationException($"Expected one active {typeof(T).Name} under {under.name}, found {all.Length}");
                return all[0];
            }
            var gather = Active<StageEntranceGather>(stage);
            if (!(new SerializedObject(gather).FindProperty("border").objectReferenceValue is Transform border) || !border.IsChildOf(stage))
                throw new InvalidOperationException("Gather border still points at the original");
            foreach (var binding in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<WaitingRoomUiBinding>(true)))
                if (binding.Gather != gather) throw new InvalidOperationException("UI binding does not point at the copied gather zone");
            if (!gather.GetComponent<BoxCollider>().bounds.Contains(W(40.5f, RouteDoorZ, .6f))) throw new InvalidOperationException("Gather zone not in front of the stage curtain");

            var doorLock = Active<NightDoorLock>(corridor);
            var shutter = (Transform)new SerializedObject(doorLock).FindProperty("shutter").objectReferenceValue;
            if (shutter == null || !shutter.IsChildOf(corridor)) throw new InvalidOperationException("Night lock shutter still points at the original");
            // Close the shutter as the night does, then make sure nobody fits through the 06 door.
            var open = shutter.localScale;
            shutter.localScale = new Vector3(open.x, 1, open.z);
            Physics.SyncTransforms();
            var panel = shutter.GetComponent<Collider>();
            bool sealedDoor = new[] { -1.8f, RouteDoorZ, 2.8f }.All(z =>
                Physics.OverlapCapsule(W(shutter.position.x, z, .4f), W(shutter.position.x, z, 1.5f), .3f, ~0, QueryTriggerInteraction.Ignore).Contains(panel));
            shutter.localScale = open;
            Physics.SyncTransforms();
            if (!sealedDoor) throw new InvalidOperationException("Closed night shutter does not seal the 06 door");

            var watch = Active<WaitingRoomExitWatch>(corridor.parent.Find("ExplorationTimer"));
            var areas = watch.GetComponentsInChildren<BoxCollider>();
            bool Inside(float x, float z) => areas.Any(a => a.enabled && a.bounds.Contains(W(x, z, .6f)));
            foreach (var (x, z, inside, what) in new[] { (.5f, -5.6f, true, "spawn"), (12f, 8f, true, "lobby east"), (LobbyW - .3f, RouteDoorZ, true, "lobby side of 06 door"),
                         (CorX0 + 1f, RouteDoorZ, false, "corridor"), (40f, RouteDoorZ, false, "stage entrance") })
                if (Inside(x, z) != inside) throw new InvalidOperationException($"Exit watch wrong at {what}");
            if (corridor.parent.Find("S02_Dormitory") != null && !Inside(DormDoorX, 18f)) throw new InvalidOperationException("Exit watch misses the dormitory");
            if (corridor.parent.Find("S04_PropRoom") != null && !Inside(13.3f, -7.3f)) throw new InvalidOperationException("Exit watch misses the prop room");

            var portal = corridor.parent.Find("S01_Lobby/FutureConnections/Portal_06");
            if (portal != null && portal.Find("TemporaryClosure") != null) throw new InvalidOperationException("06 portal still closed");
            if (corridor.GetComponentsInChildren<Component>(true).Concat(stage.GetComponentsInChildren<Component>(true)).Any(c => c == null)) throw new InvalidOperationException("Missing script in stage route");
            if (!Physics.Raycast(W(LobbyW + .25f, RouteDoorZ, 1f), Vector3.down, out var hit, 2f) || Mathf.Abs(hit.point.y) > .01f)
                throw new InvalidOperationException("No floor under the 06 doorway");
            foreach (var (x, z, what) in new[] { (LobbyW + .25f, RouteDoorZ, "06 doorway"), (CorX0 + 1.2f, RouteDoorZ, "corridor entry"), (20f, RouteDoorZ, "first curtain rooms"),
                         (26f, RouteDoorZ, "second curtain rooms"), (32f, RouteDoorZ, "third curtain rooms"), ((CorX1 + StageX0) * .5f, RouteDoorZ, "07 opening"),
                         (40f, RouteDoorZ, "gather zone"), (39f, StageZ0 + 1.6f, "07 south side"), (39f, StageZ1 - 1.6f, "07 north side") })
                CheckStanding(W(x, z, .1f), corridor.parent, what);
            if (corridor.GetComponentsInChildren<Light>().Length + stage.GetComponentsInChildren<Light>().Length > 14) throw new InvalidOperationException("Too many stage route lights");
        }

        // Rectangular room shell. doors: (side 0=W 1=N 2=E 3=S, world-axis coordinate along that side, width, blocker depth).
        static void RoomShell(Transform room, string prefix, float x0, float x1, float z0, float z1, float height, float ledger,
            Material floorMat, Material lower, Material upper, (int side, float at, float width, float depth)[] doors, float westThickness = .5f, float eastThickness = .5f)
        {
            var walls = new (Vector2 a, Vector2 b, Vector2 inward, Door[] doors, float[] pilasters)[]
            {
                (new(x0, z0), new(x0, z1), new(1, 0), doors.Where(d => d.side == 0).Select(d => new Door("", "", d.at - z0, d.width)).ToArray(), Array.Empty<float>()),
                (new(x0, z1), new(x1, z1), new(0, -1), doors.Where(d => d.side == 1).Select(d => new Door("", "", d.at - x0, d.width)).ToArray(), Array.Empty<float>()),
                (new(x1, z1), new(x1, z0), new(-1, 0), doors.Where(d => d.side == 2).Select(d => new Door("", "", z1 - d.at, d.width)).ToArray(), Array.Empty<float>()),
                (new(x1, z0), new(x0, z0), new(0, 1), doors.Where(d => d.side == 3).Select(d => new Door("", "", x1 - d.at, d.width)).ToArray(), Array.Empty<float>()),
            };
            var thickness = new[] { westThickness, .5f, eastThickness, .5f };
            var shell = Group(room, "Shell", room.position);
            var b = new MeshBuilder(prefix + "Floor", 1);
            for (float x = x0; x < x1 - .01f; x += 3.5f)
            for (float z = z0; z < z1 - .01f; z += 3.5f)
            {
                float xe = Mathf.Min(x1, x + 3.5f), ze = Mathf.Min(z1, z + 3.5f);
                b.Quad(0, new Vector3(x, 0, z), new Vector3(x, 0, ze), new Vector3(xe, 0, ze), new Vector3(xe, 0, z), Vector3.up, floorMat.name.Contains("Teal") ? 1.2f : 3.5f);
            }
            MeshObject(shell, "Floor", Origin, SaveMesh(b.Build()), floorMat);
            MeshObject(shell, "WallSurfaces", Origin, WallMesh(prefix + "Walls", walls, height, ledger, DoorHeight, lower.name.Contains("Teal") ? 1.2f : 4f), lower, upper);
            var structure = Group(shell, "WallStructure", room.position);
            for (int s = 0; s < 4; s++)
            {
                var wall = walls[s];
                Vector2 u = (wall.b - wall.a).normalized; float length = Vector2.Distance(wall.a, wall.b);
                var inward = new Vector3(wall.inward.x, 0, wall.inward.y); var along = new Vector3(u.x, 0, u.y);
                float depth = thickness[s], cursor = 0, from = 0;
                foreach (var d in wall.doors.Append(new Door("", "", length + 100, 0)))
                {
                    float end = Mathf.Min(length, d.At - d.Width * .5f);
                    if (end - cursor > .05f) ThinBlocker(structure, wall.a + u * ((cursor + end) * .5f), along, inward, end - cursor, 0, height, depth);
                    float to = Mathf.Min(length, d.At - d.Width * .5f - .2f);
                    if (to - from > .05f) { var m = wall.a + u * ((from + to) * .5f); Box(structure, "SteelLedger", W(m.x, m.y, ledger) + inward * .08f, Size(along, to - from, .24f, .16f), Mat("V4_Iron_Dark")); }
                    if (d.At > length) break;
                    var p = wall.a + u * d.At;
                    ThinBlocker(structure, p, along, inward, d.Width, DoorHeight, height, depth);
                    // Floor through the wall thickness (and under a temporary closure beyond it); without it the void shows as a strip.
                    Box(structure, "DoorSill", W(p.x, p.y, .004f) - inward * (depth * .5f), Size(along, d.Width + .4f, .008f, depth + .5f), floorMat);
                    foreach (var side in new[] { -1f, 1f })
                        Box(structure, "DoorReveal", W(p.x, p.y, DoorHeight * .5f) + along * side * (d.Width * .5f + .1f) - inward * depth * .5f, Size(along, .2f, DoorHeight, depth + .12f), Mat("V4_Iron_Dark"), true);
                    Box(structure, "RevealLintel", W(p.x, p.y, DoorHeight + .12f) - inward * depth * .5f, Size(along, d.Width + .4f, .24f, depth + .12f), Mat("V4_RustyMetal"));
                    cursor = d.At + d.Width * .5f; from = d.At + d.Width * .5f + .2f;
                }
            }
            Box(shell, "Ceiling", W((x0 + x1) * .5f, (z0 + z1) * .5f, height + .1f), new Vector3(x1 - x0 + .4f, .2f, z1 - z0 + .4f), Mat("LA_WallRust"));
        }

        static void ThinBlocker(Transform parent, Vector2 mid, Vector3 along, Vector3 inward, float width, float y0, float y1, float depth)
        {
            var go = new GameObject(y0 > 0 ? "DoorHeaderCollider" : "WallCollider");
            go.transform.SetParent(parent, true);
            go.transform.position = W(mid.x, mid.y, (y0 + y1) * .5f) - inward * depth * .5f;
            go.AddComponent<BoxCollider>().size = Size(along, width, y1 - y0, depth);
            go.isStatic = true;
        }

        static readonly (Vector2 a, Vector2 b, Vector2 inward, Door[] doors, float[] pilasters)[] PropWalls =
        {
            (new(PropX0, PropZ0), new(PropX0, PropZ1), new(1, 0), new[] { new Door("04", "", PropDoorZ - PropZ0, PropDoorWidth) }, Array.Empty<float>()),
            (new(PropX0, PropZ1), new(PropX1, PropZ1), new(0, -1), Array.Empty<Door>(), new[] { 6.3f }),
            (new(PropX1, PropZ1), new(PropX1, PropZ0), new(-1, 0), Array.Empty<Door>(), new[] { 6.7f }),
            (new(PropX1, PropZ0), new(PropX0, PropZ0), new(0, 1), Array.Empty<Door>(), new[] { 4.9f }),
        };

        static void BuildPropShell(Transform room)
        {
            var shell = Group(room, "Shell", room.position);
            var b = new MeshBuilder("LA_PropRoomFloor", 1);
            for (float x = PropX0; x < PropX1 - .01f; x += 3.5f)
            for (float z = PropZ0; z < PropZ1 - .01f; z += 3.5f)
            {
                float xe = Mathf.Min(PropX1, x + 3.5f), ze = Mathf.Min(PropZ1, z + 3.5f);
                b.Quad(0, new Vector3(x, 0, z), new Vector3(x, 0, ze), new Vector3(xe, 0, ze), new Vector3(xe, 0, z), Vector3.up, 3.5f);
            }
            var floor = MeshObject(shell, "Floor_BrokenConcrete", Origin, SaveMesh(b.Build()), Mat("LA_FloorConcrete"));
            // Reaches under the doorway to the lobby floor edge (X 6.4), closing the gap inside the wall thickness.
            AddBox(floor, new Vector3((NotchX - .1f + PropX1) * .5f, -.1f, (PropZ0 + PropZ1) * .5f), new Vector3(PropX1 - NotchX + .1f, .2f, PropZ1 - PropZ0 + .2f));
            // Low room: concrete up to the steel ledger at 2.4 m, rusted plate above, under a 5.6 m ceiling.
            MeshObject(shell, "WallSurfaces", Origin, WallMesh("LA_PropRoomWalls", PropWalls, PropHeight, 2.4f, DoorHeight), Mat("LA_WallConcrete"), Mat("LA_WallRust"));
            var structure = Group(shell, "WallStructure", room.position);
            foreach (var wall in PropWalls)
            {
                Vector2 u = (wall.b - wall.a).normalized; float length = Vector2.Distance(wall.a, wall.b);
                var inward = new Vector3(wall.inward.x, 0, wall.inward.y); var along = new Vector3(u.x, 0, u.y);
                float cursor = 0;
                foreach (var d in wall.doors.Append(new Door("", "", length + 100, 0)))
                {
                    float end = Mathf.Min(length, d.At - d.Width * .5f);
                    if (end - cursor > .05f) Blocker(structure, wall.a + u * ((cursor + end) * .5f), along, inward, end - cursor, 0, PropHeight);
                    if (d.At > length) break;
                    var p = wall.a + u * d.At;
                    Blocker(structure, p, along, inward, d.Width, DoorHeight, PropHeight);
                    // Reveal between the lobby face and this room's face.
                    foreach (var side in new[] { -1f, 1f })
                        Box(structure, "DoorReveal", W(p.x, p.y, DoorHeight * .5f) + along * side * (d.Width * .5f + .12f) - inward * .25f, Size(along, .24f, DoorHeight, .62f), Mat("V4_Iron_Dark"), true);
                    Box(structure, "RevealLintel", W(p.x, p.y, DoorHeight + .14f) - inward * .25f, Size(along, d.Width + .5f, .28f, .62f), Mat("V4_RustyMetal"));
                    cursor = d.At + d.Width * .5f;
                }
                // The ledger sits below the door head, so it stops at openings.
                float from = 0;
                foreach (var d in wall.doors.Append(new Door("", "", length + 100, 0)))
                {
                    float to = Mathf.Min(length, d.At - d.Width * .5f - .24f);
                    if (to - from > .05f)
                    {
                        var m = wall.a + u * ((from + to) * .5f);
                        Box(structure, "SteelLedger", W(m.x, m.y, 2.4f) + inward * .1f, Size(along, to - from, .3f, .2f), Mat("V4_Iron_Dark"));
                    }
                    from = d.At + d.Width * .5f + .24f;
                }
                foreach (var at in wall.pilasters)
                {
                    var p = wall.a + u * at;
                    Box(structure, "IBeam", W(p.x, p.y, PropHeight * .5f) + inward * .2f, Size(along, .4f, PropHeight, .4f), Mat("V4_Iron_Dark"), true);
                }
            }
            var overhead = Group(room, "Overhead", room.position);
            Box(overhead, "Ceiling", W((PropX0 + PropX1) * .5f, (PropZ0 + PropZ1) * .5f, PropHeight + .1f), new Vector3(PropX1 - PropX0 + .6f, .2f, PropZ1 - PropZ0 + .6f), Mat("LA_WallRust"));
            foreach (var x in new[] { 9.5f, 13f, 16.4f })
                Box(overhead, "CeilingBeam", W(x, (PropZ0 + PropZ1) * .5f, PropHeight - .2f), new Vector3(.25f, .4f, PropZ1 - PropZ0), Mat("V4_Iron_Dark"));
            PipeRun(overhead, new(PropX0 + .4f, PropZ1 - .4f), new(PropX1 - .4f, PropZ1 - .4f), 4.9f, .16f);
        }

        // Copies one original item and moves it so its footprint centre lands on (x, z), turned by yaw. Height is kept.
        static Transform PlaceItem(string path, Transform parent, float x, float z, float yaw)
        {
            var original = Required(path);
            var c = FootprintCenter(original);
            return PlaceGroupCore(new[] { original }, parent, new Vector2(c.x, c.z), x, z, yaw)[0];
        }

        static Transform[] PlaceGroup(string[] paths, Transform parent, Vector2 pivotWorld, float x, float z, float yaw) =>
            PlaceGroupCore(paths.Select(Required).ToArray(), parent, pivotWorld, x, z, yaw);

        // Rigid move of several originals: positions rotate about the old pivot and land on the new one.
        static Transform[] PlaceGroupCore(Transform[] originals, Transform parent, Vector2 pivotWorld, float x, float z, float yaw)
        {
            var turn = Quaternion.Euler(0, yaw, 0);
            var pivotOld = new Vector3(pivotWorld.x, 0, pivotWorld.y);
            var pivotNew = W(x, z);
            return originals.Select(o =>
            {
                var copy = Copy(o, parent);
                copy.SetPositionAndRotation(pivotNew + turn * (o.position - pivotOld), turn * o.rotation);
                return copy;
            }).ToArray();
        }

        static Vector3 FootprintCenter(Transform t) => Footprint(t).center;

        static Bounds Footprint(Transform t)
        {
            var renderers = t.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(t.position, Vector3.zero);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        static void MoveCenter(Transform t, float x, float z)
        {
            var c = FootprintCenter(t); var target = W(x, z);
            t.position += new Vector3(target.x - c.x, 0, target.z - c.z);
        }

        // A built room opens its lobby portal: the temporary curtain and shutter go, the sign stays.
        static void OpenPortal(Transform target, string id)
        {
            var portal = target.Find("S01_Lobby/FutureConnections/Portal_" + id);
            if (portal == null) return;
            foreach (var n in new[] { "TemporaryClosure", "RollerShutter" })
            {
                var t = portal.Find(n);
                if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
            }
        }

        static void ValidatePropRoom(Scene scene, Transform room)
        {
            var shelves = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<HandoverShelfView>()).Where(s => s.gameObject.activeInHierarchy).ToArray();
            if (shelves.Length != 1 || !shelves[0].transform.IsChildOf(room)) throw new InvalidOperationException("Expected one active handover shelf in the prop room");
            foreach (var labels in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<WaitingRoomWorldLabels>(true)))
                if (new SerializedObject(labels).FindProperty("shelf").objectReferenceValue != shelves[0])
                    throw new InvalidOperationException("World labels do not point at the copied handover shelf");
            var portal = room.parent.Find("S01_Lobby/FutureConnections/Portal_04");
            if (portal != null && portal.Find("TemporaryClosure") != null) throw new InvalidOperationException("Prop room portal still closed");
            if (room.GetComponentsInChildren<Component>(true).Any(c => c == null)) throw new InvalidOperationException("Missing script in prop room");
            if (!Physics.Raycast(W(NotchX + .25f, PropDoorZ, 1f), Vector3.down, out var gapHit, 2f) || Mathf.Abs(gapHit.point.y) > .01f)
                throw new InvalidOperationException("No floor under the prop room doorway");
            foreach (var (x, z, what) in new[] { (PropX0 + 2f, PropDoorZ, "prop room entry"), (NotchX + .25f, PropDoorZ, "prop room doorway"), (13.3f, -7.3f, "shelf north side"), (13.3f, -9.6f, "shelf south side"),
                         (11.5f, -11.6f, "costume aisle"), (16.2f, -8f, "east aisle") })
                CheckStanding(W(x, z, .1f), room.parent, what);
            if (room.GetComponentsInChildren<Light>().Length > 12) throw new InvalidOperationException("Too many prop room lights");
        }

        static int ConnectWorldLabels(Scene scene, ShopDeliverySlot slot) => ConnectWorldLabels(scene, "delivery", slot);
        static int ConnectWorldLabels(Scene scene, string field, Component target) => Connect<WaitingRoomWorldLabels>(scene, field, target);

        // A rebuilt room leaves an outside field pointing at the destroyed copy (null); point it at the new component.
        static int Connect<T>(Scene scene, string field, Component target) where T : MonoBehaviour
        {
            int changed = 0;
            foreach (var owner in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)))
            {
                var so = new SerializedObject(owner); var p = so.FindProperty(field);
                if (p.objectReferenceValue == target) continue;
                if (p.objectReferenceValue is Component c && !c.transform.IsChildOf(source)) continue;
                Undo.RecordObject(owner, "Connect Layout A " + field);
                p.objectReferenceValue = target; so.ApplyModifiedProperties(); changed++;
                Manifest.Add($"- 참조 재연결: {AnimationUtility.CalculateTransformPath(owner.transform, null)} {typeof(T).Name}.{field} → {AnimationUtility.CalculateTransformPath(target.transform, null)}");
            }
            return changed;
        }

        // Nothing may stand in a doorway: a strip 1.5 m deep on each side of every door (and at the foot and head of the lobby
        // stair) must be free of colliders and of anything drawn between knee and head height, door fittings excepted.
        public static List<string> AuditDoorways(Transform target)
        {
            var doors = new List<(string name, Vector2 at, Vector2 along, Vector2 inward, float width, float inside, float outside, float y)>();
            foreach (var wall in Walls)
            {
                var u = (wall.b - wall.a).normalized;
                foreach (var d in wall.doors)
                {
                    var portal = target.Find("S01_Lobby/FutureConnections/Portal_" + d.Id);
                    bool open = portal != null && portal.Find("TemporaryClosure") == null;
                    doors.Add(("lobby " + d.Id, wall.a + u * d.At, u, wall.inward, d.Width, .05f, open ? .55f : -1f, 0f));
                }
            }
            if (target.Find("S02_Dormitory")) doors.Add(("restroom partition", new(DormX1 + .1f, RestDoorZ), new(0, 1), new(-1, 0), RestDoorWidth, .15f, .15f, 0f));
            if (target.Find("S07_StageEntrance")) doors.Add(("06-07 opening", new((CorX1 + StageX0) * .5f, RouteDoorZ), new(0, 1), new(-1, 0), StageDoorWidth, .15f, .15f, 0f));
            if (target.Find("S09_PracticeRoom")) doors.Add(("09-10 door", new(PracX0, PracWestDoorZ), new(0, 1), new(1, 0), 2.8f, .05f, -1f, 0f));
            if (target.Find("WC_WestCorridor"))
            {
                doors.Add(("corridor stairs door", new(WcX0, WcDoorZ), new(0, 1), new(1, 0), WcDoorWidth, .05f, -1f, 0f));
                doors.Add(("corridor 10 door", new(WcNorthDoorX, WcZ1), new(1, 0), new(0, -1), 2.8f, .05f, -1f, 0f));
            }
            if (target.Find("P05_PipeZone"))
            {
                doors.Add(("restroom breach", new(RestX1, BreachZ), new(0, 1), new(-1, 0), BreachWidth, .05f, .55f, 0f));
                doors.Add(("pipe zone - 05", new(StoreX0, PipeDoorZ), new(0, 1), new(1, 0), PipeDoorWidth, .05f, .55f, 0f));
            }
            if (target.Find("S08_ElevatorHall"))
            {
                doors.Add(("expert booth curtain", new(BoothX0, BoothDoorZ), new(0, 1), new(1, 0), BoothDoorWidth, .1f, .1f, 0f));
                doors.Add(("freight lift gate", new(HallX1, LiftZ), new(0, 1), new(-1, 0), LiftWidth, .25f, -1f, 0f)); // the gate itself stands .1 m inside the wall line
            }
            doors.Add(("lobby stair foot", new((StairX0 + StairX1) * .5f, StairZ0), new(1, 0), new(0, -1), StairX1 - StairX0, .05f, -1f, 0f));
            doors.Add(("lobby stair head", new((StairX0 + StairX1) * .5f, MezzSouthZ), new(1, 0), new(0, 1), StairX1 - StairX0, .05f, -1f, MezzY));
            string[] fittings = { "Floor", "WallSurfaces", "Sill", "Threshold", "Reveal", "Jamb", "Lintel", "TemporaryClosure", "RollerShutter", "DestinationSign", "SignPlate", "Bulb", "Marquee",
                                  "Joint", "Tape", "Hazard", "Curtain", "커튼", "Chunk", "Rebar", "Ramp", "Grating", "Landing", "Deck", "Decal", "Rope", "Puddle", "Stain", "Crack", "Bloom", "Paint", "Paper", "Splinter", "Rubble" };
            bool Fitting(Transform t) => fittings.Any(f => t.name.Contains(f)) || t.GetComponentsInParent<Transform>().Any(p => p.name.StartsWith("Portal_") || p.name == "WaitingRoomShutter" || p.name == "Connections");
            var problems = new List<string>();
            foreach (var d in doors)
                foreach (var (side, depthFrom) in new[] { (1f, d.inside), (-1f, d.outside) })
                {
                    if (depthFrom < 0) continue;
                    var n = new Vector3(d.inward.x, 0, d.inward.y) * side; var along = new Vector3(d.along.x, 0, d.along.y);
                    var centre = W(d.at.x, d.at.y, d.y + 1.05f) + n * (depthFrom + .75f); // knee to head height (.2 - 1.9 m)
                    var half = Size(along, (d.width - .6f) * .5f, .85f, .75f);
                    var box = new Bounds(centre, half * 2);
                    var hits = Physics.OverlapBox(centre, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore).Where(c => c.transform.IsChildOf(target) && !Fitting(c.transform)).Select(c => c.name)
                        .Concat(target.GetComponentsInChildren<Renderer>().Where(r => r.bounds.Intersects(box) && r.bounds.size.magnitude < 12f && !Fitting(r.transform)
                            && (r.sharedMaterial == null || r.sharedMaterial.renderQueue < 2500)).Select(r => r.name)).Distinct().ToArray();
                    if (hits.Length > 0) problems.Add($"{d.name} ({(side > 0 ? "near" : "far")} side): {string.Join(", ", hits)}");
                }
            return problems;
        }

        public static string Validate()
        {
            var scene = SceneManager.GetActiveScene();
            source = scene.GetRootGameObjects().Single(g => g.name == "WaitingRoomGreybox").transform;
            var target = scene.GetRootGameObjects().Single(g => g.name == "WaitingRoom_LayoutA").transform;
            Physics.SyncTransforms();
            ValidateLobby(target.Find("S01_Lobby"));
            var shop = target.Find("S03_Shop");
            if (shop != null) ValidateShop(scene, shop);
            var corridor = target.Find("S06_StageCorridor");
            if (corridor != null) ValidateStageRoute(scene, corridor, target.Find("S07_StageEntrance"));
            // No glazing or daylight anywhere indoors (the restroom's ceiling light panels are fittings, not windows).
            var glass = target.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r => r.name != "CeilingPanel" && r.sharedMaterials.Any(m => m != null && m.name.Contains("Skylight")));
            if (glass != null) throw new InvalidOperationException("Window still open to the sky: " + AnimationUtility.CalculateTransformPath(glass.transform, null));
            var daylight = target.GetComponentsInChildren<Light>().FirstOrDefault(l => l.name.Contains("WindowShaft") || l.name.Contains("SkylightBeam"));
            if (daylight != null) throw new InvalidOperationException("Daylight left: " + daylight.name);
            var blocked = AuditDoorways(target);
            if (blocked.Count > 0) throw new InvalidOperationException("Doorways blocked: " + string.Join(" | ", blocked));
            return "Structure PASS; lobby" + (shop != null ? " + shop pillar" : "") + (corridor != null ? " + stage route" : "") + "; legacy inactive";
        }

        public static string WritePlan()
        {
            var lines = new List<string>
            {
                "# 조감도 A 배치 기준 좌표", "",
                "- 원본 이미지: 대기실 공간 구조 A (1442×1091px). 18px = 1m, 로비 중심 픽셀 (650,547) = 월드 (0, 0, 70).",
                "- 2026-10-27 사용자 피드백(공간이 좁고 균일함)에 따라 23px → 18px로 키웠다. 로비 29.4×28m, 천장 13m 공장 홀.",
                "- 월드 X = 오른쪽(동), 월드 Z = 이미지 위쪽(북). 아래 값은 Origin 기준 로컬 좌표(m)다.",
                "- 도구: `WaitingRoomLayoutA.Rooms`. 각 단계는 이 영역 안에 방을 만든다. 층고·계단 길이는 이동 검증 후 확정한다.", "",
                "| ID | 공간 | 층 | X 범위 | Z 범위 | 크기(m) |", "|---|---|---|---|---|---|",
            };
            foreach (var r in Rooms)
                lines.Add($"| {r.Id} | {r.Name} | {r.Level} | {r.Min.x:F1} ~ {r.Max.x:F1} | {r.Min.y:F1} ~ {r.Max.y:F1} | {r.Max.x - r.Min.x:F1} × {r.Max.y - r.Min.y:F1} |");
            lines.Add("");
            lines.Add($"로비는 남동쪽 소품실 영역(X {NotchX}~{LobbyW}, Z {LobbyS}~{NotchZ})을 뺀 L자 평면이다. 상점은 원본 기둥을 그대로 복제해 로비 안 ({ShopPillar.x}, {ShopPillar.y})에 둔다. 휴게실은 로비 안에 놓인다.");
            File.WriteAllLines(Path.Combine(ReportDir, "layout-a-plan.md"), lines);
            return "Plan written";
        }

        // ---------- shell ----------

        static readonly (Vector2 a, Vector2 b, Vector2 inward, Door[] doors, float[] pilasters)[] Walls =
        {
            (new(-LobbyW, LobbyN), new(LobbyW, LobbyN), new(0, -1), new[] { new Door("09", "09 연습실", 6.9f, PracDoorWidth), new Door("02", "02 숙소 / 화장실", 22.5f, DormDoorWidth) }, new[] { 2.4f, 11.2f, 17.8f, 27f }),
            (new(LobbyW, LobbyN), new(LobbyW, NotchZ), new(-1, 0), new[] { new Door("08", "08 전문가 대기실", 4.7f, 3.6f), new Door("06", "06 무대 입구 통로", 13.5f, 5f, true) }, new[] { 8.6f }),
            (new(LobbyW, NotchZ), new(NotchX, NotchZ), new(0, 1), Array.Empty<Door>(), new[] { 4.2f }),
            (new(NotchX, NotchZ), new(NotchX, LobbyS), new(-1, 0), new[] { new Door("04", "04 소품실", 4.7f, PropDoorWidth) }, Array.Empty<float>()),
            (new(NotchX, LobbyS), new(-LobbyW, LobbyS), new(0, 1), new[] { new Door("12", "12 화물 적재 장소", 2.4f, 3.8f), new Door("11", "11 무대 장치 보관실", 13.6f, 4.2f) }, new[] { 7.4f, 18.2f }),
            // West: only the west corridor (plan band under 10). B1 is reached by its stairs, not from the lobby.
            (new(-LobbyW, LobbyS), new(-LobbyW, LobbyN), new(1, 0), new[] { new Door("WC", "서쪽 복도 · 10 작업실 · 계단", WcDoorZ - LobbyS, WcDoorWidth) }, new[] { 3.6f, 8.9f, 17.5f }),
        };

        static void BuildShell(Transform lobby)
        {
            var shell = Group(lobby, "Shell", Origin);
            var floor = MeshObject(shell, "Floor_BrokenConcrete", Origin, FloorMesh(), Mat("LA_FloorConcrete"));
            AddBox(floor, new Vector3((NotchX - LobbyW) * .5f, -.1f, 0), new Vector3(NotchX + LobbyW, .2f, LobbyN - LobbyS));
            AddBox(floor, new Vector3((NotchX + LobbyW) * .5f, -.1f, (LobbyN + NotchZ) * .5f), new Vector3(LobbyW - NotchX, .2f, LobbyN - NotchZ));
            floor.isStatic = true;

            // Wall inner faces are one mesh with world-scale UVs: dark concrete below the steel ledger, rusted plate above.
            var wallSurface = MeshObject(shell, "WallSurfaces", Origin, WallMesh(), Mat("LA_WallConcrete"), Mat("LA_WallRust"));
            wallSurface.isStatic = true;
            var structure = Group(shell, "WallStructure", Origin);
            var connections = Group(lobby, "FutureConnections", Origin);
            foreach (var wall in Walls)
            {
                Vector2 u = (wall.b - wall.a).normalized; float length = Vector2.Distance(wall.a, wall.b);
                var inward = new Vector3(wall.inward.x, 0, wall.inward.y);
                var along = new Vector3(u.x, 0, u.y);
                // Collision: full-height solid blocks between openings plus headers.
                float cursor = 0;
                foreach (var d in wall.doors.Append(new Door("", "", length + 100, 0)))
                {
                    float end = Mathf.Min(length, d.At - d.Width * .5f);
                    if (end - cursor > .05f) Blocker(structure, wall.a + u * ((cursor + end) * .5f), along, inward, end - cursor, 0, WallHeight);
                    if (d.At > length) break;
                    Blocker(structure, wall.a + u * d.At, along, inward, d.Width, DoorHeight, WallHeight);
                    Doorway(structure, connections, wall.a + u * d.At, d, along, inward);
                    cursor = d.At + d.Width * .5f;
                }
                // Steel ledger with rivets along the whole run, plus I-beam pilasters at irregular spacing.
                var mid = wall.a + u * length * .5f;
                var ledger = Box(structure, "SteelLedger", W(mid.x, mid.y, LedgerY) + inward * .12f, Size(along, length, .38f, .24f), Mat("V4_Iron_Dark"));
                ledger.gameObject.isStatic = true;
                for (float r = .6f; r < length; r += 1.2f)
                    Prim(structure, PrimitiveType.Sphere, "Rivet", W(wall.a.x + u.x * r, wall.a.y + u.y * r, LedgerY) + inward * .25f, Vector3.one * .07f, Mat("V4_RustyMetal"));
                Box(structure, "CapBeam", W(mid.x, mid.y, WallHeight - .3f) + inward * .15f, Size(along, length, .5f, .3f), Mat("V4_Iron_Dark"));
                foreach (var at in wall.pilasters)
                {
                    var p = wall.a + u * at;
                    Box(structure, "IBeamWeb", W(p.x, p.y, WallHeight * .5f) + inward * .22f, Size(along, .14f, WallHeight, .44f), Mat("V4_Iron_Dark"), true);
                    Box(structure, "IBeamFlange", W(p.x, p.y, WallHeight * .5f) + inward * .46f, Size(along, .5f, WallHeight, .06f), Mat("V4_RustyMetal"), true);
                    Box(structure, "BasePlate", W(p.x, p.y, .1f) + inward * .3f, Size(along, .8f, .2f, .7f), Mat("V4_RustyMetal"));
                }
            }
            foreach (var c in new Vector2[] { new(-LobbyW, LobbyN), new(LobbyW, LobbyN), new(LobbyW, NotchZ), new(NotchX, NotchZ), new(NotchX, LobbyS), new(-LobbyW, LobbyS) })
                Box(structure, "CornerColumn", W(c.x, c.y, WallHeight * .5f), new Vector3(.9f, WallHeight, .9f), Mat("V4_Iron_Dark"), true);

            // High clerestory windows on the north and east walls, boarded up with riveted steel plate (no view outside).
            foreach (var x in new[] { -9.5f, -1.5f, 9f })
                BoardedWindow(structure, W(x, LobbyN, 9.6f), Vector3.right, Vector3.back, 3.2f, 1.8f);
            BoardedWindow(structure, W(LobbyW, 4.5f, 9.6f), Vector3.forward, Vector3.left, 3.2f, 1.8f);

            BuildRoof(Group(lobby, "Overhead", Origin));
            BuildInteriorStructure(Group(lobby, "Structure", Origin));
        }

        static Vector3 Size(Vector3 along, float width, float height, float depth) =>
            Mathf.Abs(along.x) > .5f ? new Vector3(width, height, depth) : new Vector3(depth, height, width);

        static void Blocker(Transform parent, Vector2 mid, Vector3 along, Vector3 inward, float width, float y0, float y1)
        {
            var go = new GameObject(y0 > 0 ? "DoorHeaderCollider" : "WallCollider");
            go.transform.SetParent(parent, true);
            go.transform.position = W(mid.x, mid.y, (y0 + y1) * .5f) - inward * .25f;
            go.AddComponent<BoxCollider>().size = Size(along, width, y1 - y0, .5f);
            go.isStatic = true;
        }

        static void Doorway(Transform structure, Transform connections, Vector2 at, Door door, Vector3 along, Vector3 inward)
        {
            var p = W(at.x, at.y);
            foreach (var side in new[] { -1f, 1f })
                Box(structure, "DoorJamb", p + along * side * (door.Width * .5f + .12f) + Vector3.up * DoorHeight * .5f, Size(along, .24f, DoorHeight, .6f), Mat("V4_Iron_Dark"), true);
            Box(structure, "DoorLintel", p + Vector3.up * (DoorHeight + .14f), Size(along, door.Width + .5f, .28f, .6f), Mat("V4_RustyMetal"));
            Box(structure, "Threshold", p + Vector3.up * .01f - inward * .15f, Size(along, door.Width, .02f, .7f), Mat("V4_Iron_Dark"));

            var portal = Group(connections, "Portal_" + door.Id, p);
            // Temporary closure until the room behind is built: a rusted roller shutter half down behind a drawn curtain.
            Box(portal, "TemporaryClosure", p + Vector3.up * DoorHeight * .5f - inward * .3f, Size(along, door.Width, DoorHeight, .1f), Mat("V4_Velvet_Red_Cloth"), true);
            Box(portal, "RollerShutter", p + Vector3.up * (DoorHeight - .7f) - inward * .2f, Size(along, door.Width + .1f, 1.4f, .06f), Mat("V4_Ceiling_RustyMetal"));
            var sign = Copy(Required("S01_Lobby/Sign_② 숙소"), portal, "DestinationSign", false);
            sign.gameObject.SetActive(true);
            sign.SetPositionAndRotation(p + inward * .32f + Vector3.up * (DoorHeight + .7f), Quaternion.LookRotation(-inward));
            var text = sign.GetComponent<TextMesh>();
            text.text = door.Label; text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
            text.fontSize = 64; text.characterSize = .075f; text.color = new Color(.82f, .7f, .5f);
            Box(portal, "SignPlate", p + inward * .28f + Vector3.up * (DoorHeight + .7f), Size(along, Mathf.Max(2.4f, door.Label.Length * .22f), .6f, .04f), Mat("V4_Sign_Wood"));
            Group(portal, "Approach", p + inward * 2f + Vector3.up * .1f);
            if (!door.Marquee) return;
            // The performance corridor carries the brightest frame so the route to the stage reads first.
            for (int i = 0; i <= 11; i++)
                Bulb(portal, p + along * Mathf.Lerp(-door.Width * .5f - .35f, door.Width * .5f + .35f, i / 11f) + Vector3.up * (DoorHeight + .4f) + inward * .35f);
            foreach (var side in new[] { -1f, 1f })
                for (int i = 0; i < 7; i++)
                    Bulb(portal, p + along * side * (door.Width * .5f + .35f) + Vector3.up * (.4f + i * .5f) + inward * .35f);
            Box(portal, "MarqueeFrame", p + Vector3.up * (DoorHeight + .4f) + inward * .3f, Size(along, door.Width + .9f, .16f, .05f), Mat("V4_Gold_Trim"));
        }

        static void BuildRoof(Transform overhead)
        {
            Box(overhead, "Roof_Main", W((NotchX - LobbyW) * .5f, 0, WallHeight + .15f), new Vector3(NotchX + LobbyW + .9f, .3f, LobbyN - LobbyS + .9f), Mat("LA_WallRust"));
            Box(overhead, "Roof_East", W((NotchX + LobbyW) * .5f, (LobbyN + NotchZ) * .5f, WallHeight + .15f), new Vector3(LobbyW - NotchX + .9f, .3f, LobbyN - NotchZ + .9f), Mat("LA_WallRust"));
            // Roof trusses: deep steel chords with diagonal webs, spaced unevenly over the hall.
            foreach (var z in new[] { 11.2f, 6.6f, 1.4f, -4.6f, -10.2f })
            {
                float east = z < NotchZ ? NotchX : LobbyW, len = east + LobbyW, cx = (east - LobbyW) * .5f;
                Box(overhead, "TrussTop", W(cx, z, TrussY + .7f), new Vector3(len, .3f, .25f), Mat("V4_Iron_Dark"));
                Box(overhead, "TrussBottom", W(cx, z, TrussY - .6f), new Vector3(len, .22f, .25f), Mat("V4_Iron_Dark"));
                for (float x = -LobbyW + .8f; x < east - .4f; x += 1.6f)
                {
                    var web = Box(overhead, "TrussWeb", W(x, z, TrussY + .05f), new Vector3(.08f, 1.55f, .08f), Mat("V4_RustyMetal"));
                    web.rotation = Quaternion.Euler(0, 0, ((int)((x + 20) / 1.6f) % 2 == 0) ? 32 : -32);
                }
            }
            // Purlins across the trusses.
            foreach (var x in new[] { -11f, -5.5f, 0f, 5f, 10.5f })
            {
                float south = x > NotchX ? NotchZ : LobbyS;
                Box(overhead, "Purlin", W(x, (LobbyN + south) * .5f, TrussY + .95f), new Vector3(.16f, .2f, LobbyN - south), Mat("V4_RustyMetal"));
            }
            // Big service pipes along the upper walls with collars; two vertical drops with bronze valve wheels.
            PipeRun(overhead, new(-LobbyW + .7f, LobbyN - .7f), new(LobbyW - .7f, LobbyN - .7f), 9.2f, .32f);
            PipeRun(overhead, new(-LobbyW + .7f, LobbyN - 1.5f), new(LobbyW - .7f, LobbyN - 1.5f), 8.5f, .14f);
            PipeRun(overhead, new(-LobbyW + .7f, LobbyS + .7f), new(-LobbyW + .7f, LobbyN - .7f), 9.2f, .32f);
            PipeRun(overhead, new(LobbyW - .7f, NotchZ + .7f), new(LobbyW - .7f, LobbyN - .7f), 7.6f, .2f);
            PipeRun(overhead, new(-LobbyW + .7f, LobbyS + .7f), new(NotchX - .7f, LobbyS + .7f), 8.2f, .18f);
            PipeRun(overhead, new(-4f, LobbyN - .7f), new(-4f, -2f), 10.4f, .24f); // a run crossing the hall breaks the symmetry
            foreach (var (c, h) in new[] { (new Vector2(-LobbyW + .7f, LobbyN - .7f), 9.2f), (new Vector2(LobbyW - .7f, LobbyN - .7f), 9.2f), (new Vector2(-LobbyW + .7f, -9f), 9.2f) })
            {
                Pipe(overhead, W(c.x, c.y, h * .5f), Vector3.up, h, .3f);
                var wheel = Prim(overhead, PrimitiveType.Cylinder, "ValveWheel", W(c.x + (c.x < 0 ? .45f : -.45f), c.y, 1.6f), new Vector3(.7f, .03f, .7f), Mat("LA_Bronze"));
                wheel.rotation = Quaternion.Euler(0, 0, 90);
                Pipe(overhead, W(c.x + (c.x < 0 ? .25f : -.25f), c.y, 1.6f), Vector3.right, .5f, .05f);
            }
        }

        static void BuildInteriorStructure(Transform structure)
        {
            // Three free-standing steel columns (one missing on the south-east where the prop room cuts in).
            foreach (var c in new Vector2[] { new(-10.4f, 7.6f), new(10.6f, 7.6f), new(-6.8f, -9.6f) })
            {
                Box(structure, "HColumnWeb", W(c.x, c.y, TrussY * .5f), new Vector3(.18f, TrussY, .6f), Mat("V4_Iron_Dark"), true);
                foreach (var s in new[] { -1f, 1f })
                    Box(structure, "HColumnFlange", W(c.x, c.y + s * .3f, TrussY * .5f), new Vector3(.55f, TrussY, .06f), Mat("V4_RustyMetal"), true);
                Box(structure, "ColumnFooting", W(c.x, c.y, .18f), new Vector3(1.2f, .36f, 1.2f), Mat("LA_WallConcrete"), true);
                Box(structure, "HazardBand", W(c.x, c.y, 1.1f), new Vector3(.62f, .5f, .66f), Mat("LA_HazardPaint"));
            }
            BuildMezzanine(structure);
        }

        // 2026-10-27: the lobby's second level, after the planner's references (factory steel stairs and mezzanines).
        // A steel mezzanine runs along the north and west walls (deck at 5.6 m, above the 09 and west corridor doors), reached by
        // a two-flight steel stair against the west wall. A bridge from the west deck runs out over the hall and stops short in
        // mid-air, its torn end chained off. Every open edge has a railing with a collider so nobody walks off.
        const float MezzY = 5.6f, MezzInnerX = -12.1f, MezzInnerZ = 11.4f, MezzEastX = -3.4f, MezzSouthZ = 4.55f;
        const float StairX0 = -14.15f, StairX1 = -12.95f, StairZ0 = -4.5f, StairTread = .28f, StairRiser = .2f, StairLanding = 1.2f;
        const float BridgeZ = 8.7f, BridgeWidth = 1.4f, BridgeTornX = -5.4f; // clears the hall column at (-10.4, 7.6)

        static void BuildMezzanine(Transform structure)
        {
            var g = Group(structure, "Mezzanine", Origin);
            float west = -LobbyW + .05f;
            MezzDeck(g, "NorthDeck", west, MezzEastX, MezzInnerZ, LobbyN - .05f);
            MezzDeck(g, "WestDeck", west, MezzInnerX, MezzSouthZ, MezzInnerZ);
            MezzDeck(g, "Bridge", MezzInnerX, BridgeTornX, BridgeZ - BridgeWidth * .5f, BridgeZ + BridgeWidth * .5f);

            // Columns down to the floor, kept out of the 09 door and west corridor door approaches.
            foreach (var (x, z) in new[] { (-10.2f, MezzInnerZ + .15f), (-5.4f, MezzInnerZ + .15f), (MezzEastX - .15f, MezzInnerZ + .15f), (MezzEastX - .15f, LobbyN - .3f),
                         (MezzInnerX - .15f, MezzSouthZ + .2f), (MezzInnerX - .15f, 6.9f), (-7.4f, BridgeZ) })
            {
                Box(g, "MezzColumn", W(x, z, (MezzY - .3f) * .5f), new Vector3(.22f, MezzY - .3f, .22f), Mat("V4_Iron_Dark"), true);
                Box(g, "ColumnFoot", W(x, z, .05f), new Vector3(.45f, .1f, .45f), Mat("V4_RustyMetal"));
            }
            XBrace(g, new Vector2(MezzEastX - .15f, MezzInnerZ + .15f), new Vector2(MezzEastX - .15f, LobbyN - .3f), MezzY - .4f);
            XBrace(g, new Vector2(MezzInnerX - .15f, MezzSouthZ + .2f), new Vector2(MezzInnerX - .15f, 6.9f), MezzY - .4f);

            // Railings on every open edge (gaps where the stair arrives and the bridge leaves).
            MezzRail(g, new(MezzInnerX, MezzInnerZ), new(MezzEastX, MezzInnerZ));
            MezzRail(g, new(MezzEastX, MezzInnerZ), new(MezzEastX, LobbyN - .05f));
            MezzRail(g, new(MezzInnerX, MezzSouthZ), new(MezzInnerX, BridgeZ - BridgeWidth * .5f));
            MezzRail(g, new(MezzInnerX, BridgeZ + BridgeWidth * .5f), new(MezzInnerX, MezzInnerZ));
            MezzRail(g, new(StairX1, MezzSouthZ), new(MezzInnerX, MezzSouthZ));
            MezzRail(g, new(MezzInnerX, BridgeZ - BridgeWidth * .5f), new(BridgeTornX, BridgeZ - BridgeWidth * .5f));
            MezzRail(g, new(MezzInnerX, BridgeZ + BridgeWidth * .5f), new(BridgeTornX + .9f, BridgeZ + BridgeWidth * .5f)); // the north rail tore off first

            // The torn end: a plate hanging down off its last bolts, a twisted rail stub, a dangling chain, and a chained barrier.
            Box(g, "TornPlate", W(BridgeTornX + .45f, BridgeZ + .15f, MezzY - .5f), new Vector3(1.1f, .06f, 1.1f), Mat("LA_WallRust")).rotation = Quaternion.Euler(8, 0, -48);
            Box(g, "BentRail", W(BridgeTornX + .5f, BridgeZ + BridgeWidth * .5f, MezzY + .7f), new Vector3(1.2f, .05f, .05f), Mat("V4_Iron_Dark")).rotation = Quaternion.Euler(0, 18, -32);
            Rope(g, W(BridgeTornX + .1f, BridgeZ - .4f, MezzY - .1f), W(BridgeTornX + .2f, BridgeZ - .5f, MezzY - 2.2f), .02f);
            foreach (var dz in new[] { -.6f, .6f })
                Box(g, "BarrierPost", W(BridgeTornX - .25f, BridgeZ + dz, MezzY + .55f), new Vector3(.08f, 1.1f, .08f), Mat("V4_Iron_Dark"));
            for (int i = 0; i < 6; i++)
                Box(g, "BarrierStripe", W(BridgeTornX - .25f, BridgeZ - .55f + i * .22f, MezzY + .8f), new Vector3(.04f, .14f, .2f), Mat(i % 2 == 0 ? "LA_HazardPaint" : "V4_Black"));
            Rope(g, W(BridgeTornX - .25f, BridgeZ - .6f, MezzY + .45f), W(BridgeTornX - .25f, BridgeZ + .6f, MezzY + .45f), .015f);
            var barrier = new GameObject("BarrierCollider"); barrier.transform.SetParent(g, false);
            barrier.transform.position = W(BridgeTornX - .25f, BridgeZ, MezzY + .7f);
            barrier.AddComponent<BoxCollider>().size = new Vector3(.12f, 1.4f, BridgeWidth);

            // Two-flight steel stair against the west wall: open treads on stringers, a walkable ramp collider under each flight.
            var st = Group(g, "SteelStair", W((StairX0 + StairX1) * .5f, StairZ0));
            int steps = Mathf.RoundToInt(MezzY / StairRiser / 2f);
            float run = steps * StairTread, rise = steps * StairRiser, sx = (StairX0 + StairX1) * .5f, sw = StairX1 - StairX0;
            for (int f = 0; f < 2; f++)
            {
                float z0 = StairZ0 + f * (run + StairLanding), y0 = f * rise;
                for (int i = 0; i < steps; i++)
                    Box(st, "Tread", W(sx, z0 + (i + .5f) * StairTread, y0 + (i + 1) * StairRiser - .03f), new Vector3(sw, .05f, StairTread + .02f), Mat("V4_Iron_Dark"));
                float len = Mathf.Sqrt(run * run + rise * rise), angle = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
                var mid = W(sx, z0 + run * .5f, y0 + rise * .5f);
                var ramp = new GameObject("FlightRamp"); ramp.transform.SetParent(st, false);
                ramp.transform.SetPositionAndRotation(mid + Vector3.down * .05f, Quaternion.Euler(-angle, 0, 0));
                ramp.AddComponent<BoxCollider>().size = new Vector3(sw, .1f, len);
                foreach (var x in new[] { StairX0 + .03f, StairX1 - .03f })
                {
                    Box(st, "Stringer", W(x, z0 + run * .5f, y0 + rise * .5f - .1f), new Vector3(.06f, .3f, len), Mat("V4_RustyMetal")).rotation = Quaternion.Euler(-angle, 0, 0);
                    Box(st, "Handrail", W(x, z0 + run * .5f, y0 + rise * .5f + 1f), new Vector3(.05f, .05f, len), Mat("V4_Iron_Dark")).rotation = Quaternion.Euler(-angle, 0, 0);
                    for (int k = 0; k <= 3; k++)
                        Box(st, "Baluster", W(x, z0 + run * k / 3f, y0 + rise * k / 3f + .5f), new Vector3(.04f, 1f, .04f), Mat("V4_Iron_Dark"));
                }
                // Open side: a sloped collider so nobody steps off the flight.
                var guard = new GameObject("FlightGuard"); guard.transform.SetParent(st, false);
                guard.transform.SetPositionAndRotation(W(StairX1 + .05f, z0 + run * .5f, y0 + rise * .5f + .6f), Quaternion.Euler(-angle, 0, 0));
                guard.AddComponent<BoxCollider>().size = new Vector3(.1f, 1.2f, len);
            }
            var landingZ = StairZ0 + run + StairLanding * .5f;
            Box(st, "Landing", W(sx, landingZ, rise - .05f), new Vector3(sw, .1f, StairLanding), Mat("LA_WallRust"), true);
            MezzRail(st, new(StairX1, landingZ - StairLanding * .5f), new(StairX1, landingZ + StairLanding * .5f), rise);
            foreach (var dz in new[] { -StairLanding * .5f + .1f, StairLanding * .5f - .1f })
                Box(st, "LandingPost", W(StairX1 - .1f, landingZ + dz, (rise - .1f) * .5f), new Vector3(.12f, rise - .1f, .12f), Mat("V4_Iron_Dark"), true);

            // Two caged lamps up on the level.
            var lamp = Required("Mood/Lights").Cast<Transform>().First(t => t.name == "Lobby_Lamp");
            foreach (var p in new[] { W(-8.5f, 12.6f, MezzY + 2.5f), W(-13.3f, 8.8f, MezzY + 2.5f) })
            {
                var copy = Copy(lamp, g, "MezzanineLamp");
                copy.position = p;
                foreach (var r in copy.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
                foreach (var l in copy.GetComponentsInChildren<Light>(true)) l.intensity *= .6f;
            }
        }

        // Steel grating deck with an edge beam all round and joists underneath.
        static void MezzDeck(Transform parent, string name, float x0, float x1, float z0, float z1)
        {
            Box(parent, name + "Grating", W((x0 + x1) * .5f, (z0 + z1) * .5f, MezzY - .04f), new Vector3(x1 - x0, .08f, z1 - z0), Mat("LA_WallRust"), true);
            foreach (var z in new[] { z0, z1 })
                Box(parent, name + "Edge", W((x0 + x1) * .5f, z, MezzY - .2f), new Vector3(x1 - x0, .3f, .12f), Mat("V4_Iron_Dark"));
            foreach (var x in new[] { x0, x1 })
                Box(parent, name + "Edge", W(x, (z0 + z1) * .5f, MezzY - .2f), new Vector3(.12f, .3f, z1 - z0), Mat("V4_Iron_Dark"));
            bool alongX = x1 - x0 > z1 - z0;
            for (float t = 1.5f; t < (alongX ? x1 - x0 : z1 - z0) - .2f; t += 1.5f)
                Box(parent, name + "Joist", alongX ? W(x0 + t, (z0 + z1) * .5f, MezzY - .2f) : W((x0 + x1) * .5f, z0 + t, MezzY - .2f),
                    alongX ? new Vector3(.08f, .24f, z1 - z0) : new Vector3(x1 - x0, .24f, .08f), Mat("V4_Iron_Dark"));
        }

        // Railing along a deck edge: posts, top and middle rail, and a collider so the edge can't be walked off.
        static void MezzRail(Transform parent, Vector2 a, Vector2 b, float deckY = MezzY)
        {
            float len = Vector2.Distance(a, b); if (len < .1f) return;
            var along = new Vector3(b.x - a.x, 0, b.y - a.y).normalized; var mid = (a + b) * .5f;
            foreach (var (h, t) in new[] { (1.05f, .05f), (.55f, .04f) })
                Box(parent, "Rail", W(mid.x, mid.y, deckY + h), Size(along, len, t, t), Mat("V4_Iron_Dark"));
            int n = Mathf.Max(1, Mathf.RoundToInt(len / 1.4f));
            for (int i = 0; i <= n; i++)
            {
                var p = Vector2.Lerp(a, b, i / (float)n);
                Box(parent, "RailPost", W(p.x, p.y, deckY + .53f), new Vector3(.05f, 1.06f, .05f), Mat("V4_Iron_Dark"));
            }
            var guard = new GameObject("RailGuard"); guard.transform.SetParent(parent, false);
            guard.transform.position = W(mid.x, mid.y, deckY + .6f);
            guard.AddComponent<BoxCollider>().size = Size(along, len, 1.2f, .1f);
        }

        static void XBrace(Transform parent, Vector2 a, Vector2 b, float height)
        {
            var mid = (a + b) * .5f; float len = Vector2.Distance(a, b);
            float diag = Mathf.Sqrt(len * len + height * height), angle = Mathf.Atan2(height, len) * Mathf.Rad2Deg;
            var yaw = Mathf.Atan2(b.x - a.x, b.y - a.y) * Mathf.Rad2Deg;
            foreach (var s in new[] { 1f, -1f })
                Box(parent, "XBrace", W(mid.x, mid.y, height * .5f), new Vector3(.05f, .05f, diag), Mat("V4_Iron_Dark")).rotation = Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(s * angle, 0, 0);
        }

        static void PipeRun(Transform parent, Vector2 a, Vector2 b, float y, float radius)
        {
            var from = W(a.x, a.y, y); var to = W(b.x, b.y, y); float len = Vector3.Distance(from, to);
            Pipe(parent, (from + to) * .5f, to - from, len, radius);
            for (float d = 1.7f; d < len; d += 3.4f)
                Pipe(parent, Vector3.Lerp(from, to, d / len), to - from, .2f, radius * 1.3f).name = "PipeCollar";
        }

        static Transform Pipe(Transform parent, Vector3 centre, Vector3 axis, float length, float radius)
        {
            var t = Prim(parent, PrimitiveType.Cylinder, "Pipe", centre, new Vector3(radius * 2, length * .5f, radius * 2), Mat("V4_RustyMetal"));
            t.rotation = Quaternion.FromToRotation(Vector3.up, axis.normalized);
            return t;
        }

        // ---------- faded tent ----------

        static void BuildTent(Transform tent)
        {
            var c = tent.position;
            var roof = MeshObject(tent, "SaggingCanopy", c + Vector3.up * TentEdgeY,
                TentMesh("LA_TentCanopy", 16, TentRadius, TentApexY - TentEdgeY), Mat("LA_TentRed"), Mat("LA_TentCream"));
            roof.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
            MeshObject(tent, "ScallopedValance", c + Vector3.up * TentEdgeY, ValanceMesh("LA_TentValance", 32, TentRadius, .7f), Mat("LA_TentRed"));
            for (int i = 0; i < 32; i++)
            {
                float a0 = i * Mathf.PI * 2 / 32, a1 = (i + 1) * Mathf.PI * 2 / 32;
                var p0 = c + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * TentRadius;
                var p1 = c + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * TentRadius;
                var trim = Box(tent, "FadedGoldTrim", (p0 + p1) * .5f + Vector3.up * (TentEdgeY + .03f), new Vector3(Vector3.Distance(p0, p1) + .02f, .1f, .1f), Mat("LA_Bronze"));
                trim.rotation = Quaternion.LookRotation(Vector3.Cross(p1 - p0, Vector3.up));
            }
            Prim(tent, PrimitiveType.Cylinder, "ApexCap", c + Vector3.up * (TentApexY + .05f), new Vector3(1.2f, .12f, 1.2f), Mat("LA_Bronze"));
            Box(tent, "KingPole", c + Vector3.up * TentApexY * .5f, new Vector3(.22f, TentApexY, .22f), Mat("V4_Wood_Old"), true);
            // Eight rim poles off the axes; the N/E/S/W routes under the rim stay open.
            for (int i = 0; i < 8; i++)
            {
                float a = (22.5f + i * 45f) * Mathf.Deg2Rad;
                var p = c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (TentRadius - .2f);
                Box(tent, "RimPole", p + Vector3.up * TentEdgeY * .5f, new Vector3(.16f, TentEdgeY, .16f), Mat("V4_Wood_Old"), true);
                Box(tent, "PoleFoot", p + Vector3.up * .08f, new Vector3(.45f, .16f, .45f), Mat("V4_RustyMetal"));
            }
            // Guy ropes pegged diagonally into the cracked floor.
            for (int i = 0; i < 8; i++)
            {
                float a = (i * 45f + 15f) * Mathf.Deg2Rad;
                var top = c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * TentRadius + Vector3.up * TentEdgeY;
                var stake = c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (TentRadius + 3.2f);
                if (!InsideLobby(stake, .8f)) continue;
                Rope(tent, top, stake + Vector3.up * .3f, .025f);
                Box(tent, "Stake", stake + Vector3.up * .15f, new Vector3(.08f, .35f, .08f), Mat("V4_Wood_Old"));
            }
            // Radial bulb strings from the apex to the tops of the walls: the hall's main light and its sense of scale.
            var strings = Group(tent, "BulbStrings", c);
            foreach (var (x, z) in new[] { (-LobbyW + .6f, 12.8f), (-LobbyW + .6f, -1f), (-6f, LobbyN - .6f), (7f, LobbyN - .6f), (LobbyW - .6f, 9.8f), (LobbyW - .6f, -.5f), (-9f, LobbyS + .6f), (3f, LobbyS + .6f) })
                BulbString(strings, c + Vector3.up * (TentApexY - .2f), W(x, z, 9.6f), 1.15f);
            for (int i = 0; i < 44; i++)
            {
                float a = i * Mathf.PI * 2 / 44;
                Bulb(strings, c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (TentRadius + .05f) + Vector3.up * (TentEdgeY - .1f));
            }
            // Overlapping worn crimson carpet pieces under the tent (Task-060 lobby: "천막 주변에 진홍 카펫 조각").
            foreach (var (dx, dz, sx, sz, yaw) in new[] { (-1.2f, .4f, 5.6f, 3.6f, 12f), (1.6f, -1.1f, 4.2f, 3f, -20f), (.2f, 2.3f, 3.6f, 2.6f, 47f) })
            {
                Box(tent, "CarpetPiece", c + new Vector3(dx, .012f + Mathf.Abs(yaw) * .0002f, dz), new Vector3(sx, .01f, sz), Mat("LA_CarpetWorn")).rotation = Quaternion.Euler(0, yaw, 0);
            }
            // Props kept on the ring, leaving most of the floor under the canopy open.
            var ring = Group(tent, "RingProps", c);
            PlaceCopy("Mood/InteriorArt/CircusStorageCrate_1", ring, Ring(c, 200f, 5.6f), 20f);
            PlaceCopy("Mood/InteriorArt/CanvasOnCrate_1", ring, Ring(c, 200f, 5.6f) + Vector3.up * .7f, 140f);
            PlaceCopy("Mood/InteriorArt/StorageTrunk_1", ring, Ring(c, 330f, 5.8f), 70f);
            Bench(ring, c, 140f, 5.4f);
            PlaceFactory("SM_Barrel_1", ring, Ring(c, 62f, 6.2f), 0);
        }

        static void BulbString(Transform parent, Vector3 from, Vector3 to, float spacing)
        {
            float len = Vector3.Distance(from, to); int n = Mathf.Max(2, Mathf.RoundToInt(len / spacing));
            float sag = len * .07f; Vector3 prev = from;
            for (int i = 1; i <= n; i++)
            {
                float t = i / (float)n;
                var p = Vector3.Lerp(from, to, t) + Vector3.down * sag * 4 * t * (1 - t);
                Rope(parent, prev, p, .012f);
                if (i < n) Bulb(parent, p + Vector3.down * .08f);
                prev = p;
            }
        }

        static void Rope(Transform parent, Vector3 a, Vector3 b, float radius)
        {
            var t = Prim(parent, PrimitiveType.Cylinder, "Rope", (a + b) * .5f, new Vector3(radius * 2, Vector3.Distance(a, b) * .5f, radius * 2), Mat("V4_Black"));
            t.rotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
            t.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        static Vector3 Ring(Vector3 c, float deg, float radius) => c + new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad), 0, Mathf.Sin(deg * Mathf.Deg2Rad)) * radius;

        static bool InsideLobby(Vector3 world, float margin)
        {
            var p = world - Origin;
            bool inRect = p.x > -LobbyW + margin && p.x < LobbyW - margin && p.z > LobbyS + margin && p.z < LobbyN - margin;
            bool inNotch = p.x > NotchX - margin && p.z < NotchZ + margin;
            return inRect && !inNotch;
        }

        static void Bench(Transform parent, Vector3 c, float deg, float radius)
        {
            var p = Ring(c, deg, radius);
            var bench = Group(parent, "Bench", p); bench.rotation = Quaternion.LookRotation(c - p);
            BoxLocal(bench, "Frame", new Vector3(0, .22f, 0), new Vector3(1.9f, .44f, .55f), Mat("V4_Wood_Old"), true);
            BoxLocal(bench, "Cushion", new Vector3(0, .5f, 0), new Vector3(1.8f, .12f, .5f), Mat("LA_CarpetWorn"));
            BoxLocal(bench, "Back", new Vector3(0, .85f, -.24f), new Vector3(1.9f, .7f, .08f), Mat("V4_Wood_Old"), true);
        }

        // ---------- floor wear ----------

        static void BuildFloorWear(Transform wear)
        {
            // Large rust blooms tint whole areas of the slab so the floor never reads as one uniform grey.
            foreach (var (x, z, s) in new[] { (-9f, -6.5f, 7f), (8f, 10.5f, 6.5f), (-6.5f, 10.5f, 5.5f), (3.5f, -1.5f, 6f), (-11.5f, 1.5f, 5f), (11.5f, 2.5f, 4.5f) })
                Decal(wear, "RustBloom", x, z, s, s * .8f, Mat("LA_RustBloom"));
            // Expansion joints between cast slabs, broken in places.
            var joints = Group(wear, "SlabJoints", Origin);
            for (float x = -LobbyW + 5.9f; x < LobbyW - .5f; x += 5.9f)
                JointLine(joints, new Vector2(x, x > NotchX ? NotchZ : LobbyS), new Vector2(x, LobbyN));
            for (float z = LobbyS + 5.6f; z < LobbyN - .5f; z += 5.6f)
                JointLine(joints, new Vector2(-LobbyW, z), new Vector2(z < NotchZ ? NotchX : LobbyW, z));
            // Rust bleeding from pipe drops, columns and drums; cracks fanning out of the damaged slab; oily puddles.
            foreach (var (x, z, s) in new[] { (-13.6f, 12.6f, 3.2f), (13.4f, 12.8f, 2.6f), (-13.5f, -9f, 2.8f), (-10.4f, 7.6f, 2.2f), (10.6f, 7.6f, 2.4f),
                         (-6.8f, -9.6f, 2f), (11.6f, 11.4f, 3.6f), (-12.6f, -12f, 3f), (4.6f, -2.6f, 1.6f), (-3.6f, 9.5f, 1.8f) })
                Decal(wear, "RustStain", x, z, s, s * (.7f + (float)random.NextDouble() * .5f), Mat("LA_RustDecal"));
            foreach (var (x, z, s) in new[] { (9.6f, 6.6f, 5f), (-7.5f, -3.2f, 4.2f), (3.2f, -9.8f, 3.6f), (-11.4f, 3.4f, 3.4f), (6.8f, 12.2f, 3f), (-2.2f, -12.4f, 3.2f) })
                Decal(wear, "Crack", x, z, s, s, Mat("LA_CrackDecal"));
            foreach (var (x, z, s) in new[] { (8.8f, 5.4f, 2.6f), (-12.2f, -11f, 2f), (4.2f, 11.6f, 1.8f), (-8.6f, -1.6f, 1.4f) })
                Decal(wear, "OilPuddle", x, z, s, s * .75f, Mat("LA_PuddleDecal"));
            foreach (var (x, z, s) in new[] { (-5.2f, -1.4f, 1.6f), (6.8f, -1.8f, 1.6f), (-6.4f, 11.6f, 1.4f), (9.4f, 1.8f, 1.5f) })
                Decal(wear, "PaintMark", x, z, s, s, Mat("LA_PaintStar"), (float)Math.Atan2(-z + TentCenter.y, -x + TentCenter.x) * Mathf.Rad2Deg);

            // Broken slab east of the tent: low lifted plates (walkable, no colliders), exposed rebar, a steel plate over the hole.
            const float sx0 = 9.6f, sz0 = -1.6f;
            var slab = Group(wear, "BrokenSlab", W(sx0, sz0));
            Box(slab, "SunkenHole", W(sx0, sz0, .004f), new Vector3(2.4f, .005f, 1.8f), Mat("V4_Black"));
            foreach (var (dx, dz, sx, sz, rx, rz) in new[] { (-1.4f, .2f, 1.2f, 1.6f, 5f, -3f), (1.3f, -.6f, 1.4f, 1.1f, -4f, 6f), (.4f, 1.2f, 1.6f, .9f, 6f, 2f) })
                Box(slab, "LiftedPlate", W(sx0 + dx, sz0 + dz, .06f), new Vector3(sx, .12f, sz), Mat("LA_FloorConcrete")).rotation = Quaternion.Euler(rx, random.Next(0, 90), rz);
            for (int i = 0; i < 5; i++)
                Pipe(slab, W(sx0 - .8f + i * .38f, sz0 - .2f, .05f), new Vector3(1, .12f, .15f * (i - 2)), 1.6f, .018f).name = "ExposedRebar";
            LayPlate(PlaceFactory("SM_MetalPlate1", slab, W(sx0 - 1.1f, sz0 + 1.3f, .02f), 17), sx0 - .5f, sz0 + .3f);
            for (int i = 0; i < 14; i++)
            {
                var p = W(sx0 + (float)(random.NextDouble() * 4 - 2), sz0 + (float)(random.NextDouble() * 3.2 - 1.6), .05f);
                Box(slab, "Rubble", p, Vector3.one * (.08f + (float)random.NextDouble() * .16f), Mat("LA_FloorConcrete")).rotation = Quaternion.Euler(random.Next(0, 360), random.Next(0, 360), 0);
            }
            // Debris drifts around the tent: torn paper and wood splinters.
            var debris = Group(wear, "Debris", W(TentCenter.x, TentCenter.y));
            for (int i = 0; i < 26; i++)
            {
                float a = (float)(random.NextDouble() * Math.PI * 2), r = TentRadius * (.4f + (float)random.NextDouble() * .9f);
                var p = W(TentCenter.x + Mathf.Cos(a) * r, TentCenter.y + Mathf.Sin(a) * r, .02f);
                if (!InsideLobby(p, .6f)) continue;
                var quad = Prim(debris, PrimitiveType.Quad, i % 3 == 0 ? "Splinter" : "Paper", p, i % 3 == 0 ? new Vector3(.08f, .5f, 1) : new Vector3(.24f, .32f, 1), Mat(i % 3 == 0 ? "V4_Wood_Old" : "LA_Paper"));
                quad.rotation = Quaternion.Euler(90, random.Next(0, 360), 0);
                quad.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        // SM_MetalPlate1 is an upright 3.5 x 4 m sheet; laid flat and scaled down it covers the hole as a low step.
        public static void LayPlate(Transform plate, float x, float z)
        {
            plate.SetPositionAndRotation(plate.position, Quaternion.Euler(90, 17, 0));
            plate.localScale = Vector3.one * .6f;
            MoveCenter(plate, x, z);
            plate.position += Vector3.up * (.01f - Footprint(plate).min.y);
        }

        static void JointLine(Transform parent, Vector2 a, Vector2 b)
        {
            float len = Vector2.Distance(a, b), d = 0; var u = (b - a).normalized;
            while (d < len)
            {
                float seg = Mathf.Min(len - d, 2f + (float)random.NextDouble() * 3f);
                var mid = a + u * (d + seg * .5f);
                var j = Box(parent, "Joint", W(mid.x, mid.y, .006f), Mathf.Abs(u.x) > .5f ? new Vector3(seg, .004f, .05f) : new Vector3(.05f, .004f, seg), Mat("LA_JointDark"));
                j.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                d += seg + .3f + (float)random.NextDouble() * 1.2f;
            }
        }

        static void Decal(Transform parent, string name, float x, float z, float sx, float sz, Material material, float? yaw = null)
        {
            var q = Prim(parent, PrimitiveType.Quad, name, W(x, z, .015f + parent.childCount * .0004f), new Vector3(sx, sz, 1), material);
            q.rotation = Quaternion.Euler(90, yaw ?? random.Next(0, 360), 0);
            q.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // ---------- dressing: clustered, asymmetric ----------

        static void BuildDressing(Transform art)
        {
            // North-east heap: pallets, crates, drums and a lattice mast in the corner.
            for (int i = 0; i < 5; i++) PlaceFactory("SM_Woodpallet_1", art, W(13.2f, 12.6f, i * .29f), 90 + i * 5);
            for (int i = 0; i < 3; i++) PlaceFactory("SM_Woodpallet_1", art, W(11.6f, 13.1f, i * .29f), -i * 7);
            PlaceCopy("Mood/InteriorArt/CircusStorageCrate_0", art, W(12.1f, 11.2f), -8f);
            PlaceFactory("SM_Barrel", art, W(10.6f, 12.6f), 30); PlaceFactory("SM_Barrel_1", art, W(10.4f, 13.4f), 0); // in the corner, clear of the 02 and 08 doors
            PlaceFactory("SM_Grid", art, W(14.1f, 13.4f), 0);
            // West wall: crimson runner, chair and trunk; crate pile just west of the tent.
            Box(art, "WestRunner", W(-13.2f, 6.8f, .02f), new Vector3(1.8f, .01f, 4f), Mat("LA_CarpetWorn")); // under the west deck, clear of the stair
            PlaceCopy("Mood/Furnishings/Lobby/WaitingChair", art, W(-13.4f, 6.6f), 95f);
            PlaceCopy("Mood/Furnishings/Lobby/PackedTrunk", art, W(-13.3f, 5.3f), 80f);
            PlaceCopy("Mood/InteriorArt/CircusStorageCrate_1", art, W(-9.2f, 3.0f), 14f);
            PlaceCopy("Mood/InteriorArt/StorageTrunk_1", art, W(-8.6f, 1.9f), 100f);
            PlaceCopy("Mood/InteriorArt/CanvasOnCrate_1", art, W(-9.2f, 3.0f, .7f), 40f);
            PlaceFactory("SM_Cablespool", art, W(-10.2f, 9.8f, .76f), 70);
            // Under the catwalk: desk corner and gas bottles.
            PlaceCopy("Mood/InteriorArt/CircusStorageCrate_0", art, W(-12.2f, 12.9f), 0f);
            PlaceFactory("SM_Gascylender", art, W(-13.9f, 13.3f), 0); PlaceFactory("SM_Gascylender", art, W(-13.5f, 13.4f), 20);
            PlaceFactory("SM_Gascylender", art, W(-13.7f, 12.9f), 50);
            // East wall between 06 and 08: costume rail and stand.
            PlaceCopy("Mood/InteriorArt/WR02_LobbyCostumeRail", art, W(14.0f, 6.4f), 270f);
            PlaceCopy("Mood/Furnishings/Lobby/CostumeStand", art, W(13.7f, 4.8f), 250f);
            // South-west corner behind the shop pillar: red drum and drums.
            PlaceFactory("SM_Barrel", art, W(-13.7f, -13.2f), 0); PlaceFactory("SM_Barrel_1", art, W(-12.9f, -13.4f), 40);
            PlaceFactory("SM_Barrel", art, W(-13.6f, -12.3f), 75);
            // Lone drum and spool on the open south-east floor; most of the hall is left empty on purpose.
            PlaceFactory("SM_Cablespool", art, W(12.8f, -2.9f, .76f), 20);
            PlaceFactory("SM_Barrel_1", art, W(5.4f, -5f), 0);
            // North wall: faded drapes framing the schedule board.
            foreach (var x in new[] { -2.4f, 2.4f })
            {
                Box(art, "FadedDrape", W(x, LobbyN - .14f, 2.6f), new Vector3(1.6f, 4f, .08f), Mat("LA_TentRed"));
                Box(art, "DrapeRod", W(x, LobbyN - .22f, 4.65f), new Vector3(1.8f, .06f, .06f), Mat("LA_Bronze"));
            }
            BuildLobbyRemnants(art);
        }

        // 2026-10-27 mood pass. With the tent gone its folded canvas lies in a heap where it stood, under a chain hoist;
        // a rusted scaffold, old posters and hanging chains fill the hall's height without blocking any route.
        static void BuildLobbyRemnants(Transform art)
        {
            var g = Group(art, "Remnants", Origin);
            var heap = W(1.8f, 5.2f);
            PlaceFactory("SM_Woodpallet_1", g, heap + new Vector3(-.6f, 0, 0), 4);
            PlaceFactory("SM_Woodpallet_1", g, heap + new Vector3(.7f, 0, .2f), -6);
            for (int i = 0; i < 6; i++)
                Box(g, "FoldedCanvas", heap + new Vector3(i % 2 * .08f - .04f, .2f + i * .09f, i % 3 * .05f), new Vector3(2.4f - i * .12f, .09f, 1.5f - i * .05f),
                    Mat(i % 2 == 0 ? "LA_TentRed" : "LA_TentCream")).rotation = Quaternion.Euler(0, 3 + i * 4, 0);
            Box(g, "OldKingPole", heap + new Vector3(.2f, .82f, .1f), new Vector3(.18f, .18f, 4.2f), Mat("V4_Wood_Old")).rotation = Quaternion.Euler(4, 68, 0);
            var heapCollider = new GameObject("HeapCollider"); heapCollider.transform.SetParent(g, false);
            heapCollider.transform.position = heap + Vector3.up * .45f;
            heapCollider.AddComponent<BoxCollider>().size = new Vector3(2.7f, .9f, 1.9f);
            // Chain hoist from the truss over the heap, and two loose chains elsewhere in the hall.
            Rope(g, heap + Vector3.up * (TrussY - .6f), heap + Vector3.up * 3.1f, .03f);
            Box(g, "HoistBlock", heap + Vector3.up * 3f, new Vector3(.3f, .4f, .22f), Mat("V4_Iron_Dark"));
            Box(g, "Hook", heap + Vector3.up * 2.65f, new Vector3(.06f, .3f, .18f), Mat("V4_RustyMetal"));
            foreach (var (x, z, bottom) in new[] { (-4.2f, 9.2f, 6.2f), (7.6f, -1.2f, 5.4f) })
                Rope(g, W(x, z, TrussY - .6f), W(x, z, bottom), .025f);
            // Old circus posters on the walls.
            WallPoster(g, W(-11.5f, LobbyN), 180);
            WallPoster(g, W(-LobbyW, -7.2f), 90);
            WallPoster(g, W(LobbyW, 12.8f), -90);
            WallPoster(g, W(-3.5f, LobbyS), 0);
        }

        // Copies the tunnel's circus poster onto a wall. The original faces +Z; yaw turns it to face away from the wall.
        static void WallPoster(Transform parent, Vector3 wallPoint, float yaw)
        {
            var original = Required("Mood/Tunnel/WallDressing/Poster");
            var poster = Copy(original, parent, "CircusPoster");
            var turn = Quaternion.Euler(0, yaw, 0);
            poster.rotation = turn * original.rotation;
            poster.position = new Vector3(wallPoint.x, original.position.y + .4f, wallPoint.z) + turn * Vector3.forward * .04f;
        }

        // A window opening boarded up from inside: rusted steel plate over the frame, two straps and corner bolts.
        static void BoardedWindow(Transform parent, Vector3 centre, Vector3 along, Vector3 inward, float width, float height)
        {
            var p = centre + inward * .06f;
            Box(parent, "BoardedWindow", p, Size(along, width + .3f, height + .3f, .06f), Mat("LA_SteelPlate"));
            foreach (var s in new[] { -.3f, .3f })
                Box(parent, "WindowStrap", p + inward * .04f + Vector3.up * s * height, Size(along, width + .4f, .12f, .03f), Mat("V4_Iron_Dark"));
            foreach (var sx in new[] { -.5f, .5f })
            foreach (var sy in new[] { -.5f, .5f })
                Prim(parent, PrimitiveType.Sphere, "Bolt", p + inward * .05f + along * sx * (width + .1f) + Vector3.up * sy * (height + .1f), Vector3.one * .08f, Mat("V4_RustyMetal"));
        }

        // Anything glazed under root becomes steel plate and its daylight goes out.
        static void BoardUp(Transform root)
        {
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                if (r.sharedMaterials.Any(m => m != null && m.name.Contains("Skylight")))
                    r.sharedMaterials = r.sharedMaterials.Select(m => m != null && m.name.Contains("Skylight") ? Mat("LA_SteelPlate") : m).ToArray();
            foreach (var l in root.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l.gameObject);
        }

        // ---------- lighting: dark baseline, warm pools at functional points ----------

        static void BuildLighting(Transform lighting)
        {
            var c = W(TentCenter.x, TentCenter.y);
            Point(lighting, WithTent ? "TentKey" : "HallKey", c + Vector3.up * 7.2f, new Color(1f, .71f, .4f), 5f, 15f, LightShadows.Soft);
            if (WithTent)
            {
                Point(lighting, "UnderTentPool", c + Vector3.up * 3.6f, new Color(1f, .7f, .42f), 2.2f, 7.5f);
                foreach (var (x, z) in new[] { (-7f, 8.6f), (8.2f, 8f), (-7f, -1.8f), (8.5f, 2f) })
                    Point(lighting, "StringGlow", W(x, z, 8.2f), new Color(1f, .68f, .36f), 1.1f, 8f);
            }
            Spot(lighting, "SpawnSpot", W(.5f, -9.5f, 7f), W(.5f, -5.6f), new Color(1f, .78f, .52f), 2.4f, 11f, 50f);
            Spot(lighting, "ScheduleSpot", W(0, LobbyN - 3.2f, 5.4f), W(0, LobbyN - .1f, 1.9f), new Color(1f, .84f, .62f), 3f, 7f, 45f);
            // Warm grazing light on two wall stretches so the concrete and rusted plate read, leaving the corners dark.
            Spot(lighting, "WallWash_North", W(5f, 7f, 7.5f), W(7.8f, LobbyN, 3.5f), new Color(1f, .64f, .38f), 2.6f, 14f, 50f);
            Spot(lighting, "WallWash_West", W(-6f, 2f, 7.5f), W(-LobbyW, 4.5f, 3f), new Color(1f, .64f, .38f), 2.2f, 14f, 50f);
            Point(lighting, "MarqueeGlow", W(LobbyW - 1.4f, .5f, 3.8f), new Color(1f, .6f, .3f), 3f, 7f);
            // Two existing caged work lamps over the west runner and the north-east heap.
            var lamps = Required("Mood/Lights").Cast<Transform>().Where(t => t.name == "Lobby_Lamp").Take(2).ToArray();
            var spots = new[] { W(-12.4f, 5f, 4.2f), W(11.8f, 11.6f, 4.2f) };
            for (int i = 0; i < lamps.Length; i++)
            {
                var lamp = Copy(lamps[i], lighting);
                lamp.position = spots[i];
                // The bulb mesh encloses its own light; without this it throws a large square shadow on the floor.
                foreach (var r in lamp.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        static void Point(Transform parent, string name, Vector3 position, Color color, float intensity, float range, LightShadows shadows = LightShadows.None)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, true); go.transform.position = position;
            var l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = color; l.intensity = intensity; l.range = range; l.shadows = shadows;
        }

        static void Spot(Transform parent, string name, Vector3 position, Vector3 target, Color color, float intensity, float range, float angle)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
            var l = go.AddComponent<Light>(); l.type = LightType.Spot; l.color = color; l.intensity = intensity; l.range = range;
            l.spotAngle = angle; l.innerSpotAngle = angle * .55f; l.shadows = LightShadows.Soft;
        }

        // ---------- gameplay copies ----------

        static void BuildLobbyGameplay(Transform lobby)
        {
            var gameplay = Group(lobby, "Gameplay", Origin);
            var spawn = Copy(Required("WaitingRoomGameplay/SpawnPoints"), gameplay);
            var xs = new[] { -.5f, 1.5f, -2.5f, 3.5f };
            for (int i = 0; i < 4; i++)
            {
                var pos = W(xs[i], -5.6f, .1f);
                spawn.Find("Spawn_" + (i + 1)).position = pos;
                spawn.Find("SpawnMark_" + (i + 1)).position = pos + Vector3.up * .01f;
            }
            // Schedule board and its close-up camera move together onto the north wall, between the drapes.
            var board = Copy(Required("WaitingRoomGameplay/ScheduleBoard"), gameplay);
            var closeUp = Copy(Required("NightSchedule/ScheduleCloseUp"), gameplay);
            var delta = W(0, LobbyN - .1f, 1.9f) - Required("WaitingRoomGameplay/ScheduleBoard/Board").position;
            board.position += delta; closeUp.position += delta;
            Box(gameplay, "ScheduleFrame", board.Find("Board").position + Vector3.forward * .04f, new Vector3(2.7f, 1.7f, .05f), Mat("LA_Bronze"));
        }

        // ---------- validation ----------

        static void ValidateLobby(Transform lobby)
        {
            if (lobby == null) throw new InvalidOperationException("Lobby missing");
            if (source.gameObject.activeSelf || !lobby.gameObject.activeInHierarchy) throw new InvalidOperationException("Activation mismatch");
            if (lobby.GetComponentsInChildren<Component>(true).Any(c => c == null)) throw new InvalidOperationException("Missing script in lobby");
            var scene = lobby.gameObject.scene;
            // GetComponentsInChildren on an inactive root still returns its components, so filter by activeInHierarchy.
            var providers = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerSpawnPoints>()).Where(p => p.gameObject.activeInHierarchy).ToArray();
            if (providers.Length != 1 || scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<WaitingRoomSpawn>()).Count(p => p.gameObject.activeInHierarchy) != 1)
                throw new InvalidOperationException("Expected one active spawn provider: " + string.Join(", ", providers.Select(p => AnimationUtility.CalculateTransformPath(p.transform, null))));
            var spawn = lobby.Find("Gameplay/SpawnPoints").GetComponent<PlayerSpawnPoints>();
            var points = new SerializedObject(spawn).FindProperty("points");
            if (points.arraySize != 4) throw new InvalidOperationException("Expected four spawn references");
            for (int i = 0; i < 4; i++)
            {
                if (!(points.GetArrayElementAtIndex(i).objectReferenceValue is Transform p) || !p.IsChildOf(lobby))
                    throw new InvalidOperationException("Spawn still references original");
                CheckStanding(p.position, lobby.parent, "spawn " + p.name);
            }
            foreach (Transform portal in lobby.Find("FutureConnections"))
                CheckStanding(portal.Find("Approach").position, lobby.parent, "approach " + portal.name);
            // Second level: decks, bridge and stair flights are walkable from above; the torn bridge end is barred.
            foreach (var (x, z, what) in new[] { (-8.5f, 12.4f, "north deck"), (-13.1f, 8f, "west deck"), (-8.6f, BridgeZ, "bridge"), ((StairX0 + StairX1) * .5f, StairZ0 + 2f, "stair flight") })
                if (!Physics.Raycast(W(x, z, MezzY + 2f), Vector3.down, out var deckHit, MezzY + 2f, ~0, QueryTriggerInteraction.Ignore) || deckHit.point.y < .5f)
                    throw new InvalidOperationException("Nothing to stand on at the " + what);
            if (!Physics.Raycast(W(BridgeTornX - 1.5f, BridgeZ, MezzY + .7f), Vector3.right, 2f, ~0, QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("Torn bridge end is not barred");
            if (lobby.GetComponentsInChildren<Light>().Length > 16) throw new InvalidOperationException("Too many lobby lights");
        }

        static void ValidateShop(Scene scene, Transform shop)
        {
            var phones = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ShopPhone>()).Where(p => p.gameObject.activeInHierarchy).ToArray();
            var slots = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ShopDeliverySlot>()).Where(p => p.gameObject.activeInHierarchy).ToArray();
            if (phones.Length != 1 || !phones[0].transform.IsChildOf(shop)) throw new InvalidOperationException("Expected one active phone in the copied pillar");
            if (slots.Length != 1 || !slots[0].transform.IsChildOf(shop)) throw new InvalidOperationException("Expected one active delivery slot in the copied pillar");
            foreach (var labels in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<WaitingRoomWorldLabels>(true)))
                if (new SerializedObject(labels).FindProperty("delivery").objectReferenceValue != slots[0])
                    throw new InvalidOperationException("World labels do not point at the copied delivery slot");
            var pillar = shop.Find("Pillar");
            if (pillar == null || pillar.lossyScale != Required("S03_Shop/Pillar").lossyScale) throw new InvalidOperationException("Pillar must stay as the original");
            // Stand in front of the phone face (+X after the turn) and check both interactions are in reach.
            var stand = new Vector3(phones[0].transform.position.x + .75f, .1f, phones[0].transform.position.z);
            CheckStanding(stand, shop.parent, "shop phone front");
            if (Flat(phones[0].transform.position - stand) > 1.8f) throw new InvalidOperationException("Phone out of reach");
            var slotStand = new Vector3(slots[0].transform.position.x + .75f, .1f, slots[0].transform.position.z);
            CheckStanding(slotStand, shop.parent, "delivery slot front");
            if (Flat(slots[0].transform.position - slotStand) > 1.2f) throw new InvalidOperationException("Delivery slot out of reach");
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        static void CheckStanding(Vector3 feet, Transform root, string what)
        {
            if (!Physics.Raycast(feet + Vector3.up, Vector3.down, out var hit, 3f) || hit.point.y > .2f) throw new InvalidOperationException("No floor at " + what);
            var blockers = Physics.OverlapCapsule(feet + Vector3.up * .4f, feet + Vector3.up * 1.5f, .3f, ~0, QueryTriggerInteraction.Ignore)
                .Where(c => c.transform.IsChildOf(root) && !c.name.StartsWith("Floor")).ToArray();
            if (blockers.Length > 0) throw new InvalidOperationException($"Blocked {what}: {string.Join(",", blockers.Select(b => b.name))}");
        }

        // ---------- capture ----------

        public static string Capture(string name, Vector3 position, Vector3 lookAt, bool overhead, float size = 16f)
        {
            var root = GameObject.Find("WaitingRoom_LayoutA").transform;
            var hidden = new List<GameObject>();
            if (overhead)
                foreach (var t in root.GetComponentsInChildren<Transform>())
                    if (t.name == "Overhead" && t.gameObject.activeSelf) { hidden.Add(t.gameObject); t.gameObject.SetActive(false); }
            var cameraObject = new GameObject("LayoutA_TemporaryCapture") { hideFlags = HideFlags.HideAndDontSave };
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .022f, .025f);
            camera.nearClipPlane = .05f; camera.farClipPlane = 200; camera.orthographic = overhead; camera.orthographicSize = size; camera.fieldOfView = 60;
            camera.transform.position = position; camera.transform.rotation = Quaternion.LookRotation(lookAt - position, overhead ? Vector3.forward : Vector3.up);
            var rt = new RenderTexture(1440, 1080, 24); rt.Create();
            var previous = RenderTexture.active; Texture2D texture = null;
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                texture = new Texture2D(1440, 1080, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 1440, 1080), 0, 0); texture.Apply();
                Directory.CreateDirectory("Temp/Task075");
                string file = "Temp/Task075/" + name + ".png"; File.WriteAllBytes(file, texture.EncodeToPNG()); return Path.GetFullPath(file);
            }
            finally
            {
                foreach (var go in hidden) go.SetActive(true);
                RenderTexture.active = previous; camera.targetTexture = null; Object.DestroyImmediate(cameraObject);
                if (texture != null) Object.DestroyImmediate(texture); rt.Release(); Object.DestroyImmediate(rt);
            }
        }

        // ---------- procedural meshes ----------

        static Mesh FloorMesh()
        {
            var b = new MeshBuilder("LA_LobbyFloor", 1);
            void Quad(float x0, float z0, float x1, float z1)
            {
                // Subdivided so per-vertex lighting and shadows stay smooth on the large slab.
                for (float x = x0; x < x1 - .01f; x += 3.5f)
                for (float z = z0; z < z1 - .01f; z += 3.5f)
                {
                    float xe = Mathf.Min(x1, x + 3.5f), ze = Mathf.Min(z1, z + 3.5f);
                    b.Quad(0, new Vector3(x, 0, z), new Vector3(x, 0, ze), new Vector3(xe, 0, ze), new Vector3(xe, 0, z), Vector3.up, 3.5f);
                }
            }
            Quad(-LobbyW, LobbyS, NotchX, LobbyN);
            Quad(NotchX, NotchZ, LobbyW, LobbyN);
            return SaveMesh(b.Build());
        }

        static Mesh WallMesh() => WallMesh("LA_LobbyWalls", Walls, WallHeight, LedgerY, DoorHeight);

        static Mesh WallMesh(string name, (Vector2 a, Vector2 b, Vector2 inward, Door[] doors, float[] pilasters)[] walls, float height, float ledger, float doorHeight, float lowerTile = 4f)
        {
            var b = new MeshBuilder(name, 2);
            foreach (var wall in walls)
            {
                Vector2 u = (wall.b - wall.a).normalized; float length = Vector2.Distance(wall.a, wall.b);
                var normal = new Vector3(wall.inward.x, 0, wall.inward.y);
                Vector3 P(float d, float y) => new Vector3(wall.a.x + u.x * d, y, wall.a.y + u.y * d);
                void Face(float d0, float d1, float y0, float y1, int sub, float tile)
                {
                    if (d1 - d0 < .01f || y1 - y0 < .01f) return;
                    b.Quad(sub, P(d0, y0), P(d0, y1), P(d1, y1), P(d1, y0), normal, tile);
                }
                float cursor = 0;
                foreach (var d in wall.doors.Append(new Door("", "", length + 100, 0)))
                {
                    float end = Mathf.Min(length, d.At - d.Width * .5f);
                    Face(cursor, end, 0, ledger, 0, lowerTile);
                    Face(cursor, end, ledger, height, 1, 3f);
                    if (d.At > length) break;
                    float s = d.At - d.Width * .5f, e = d.At + d.Width * .5f;
                    Face(s, e, doorHeight, ledger, 0, lowerTile);
                    Face(s, e, Mathf.Max(ledger, doorHeight), height, 1, 3f);
                    cursor = e;
                }
            }
            return SaveMesh(b.Build());
        }

        sealed class MeshBuilder
        {
            readonly string name; readonly List<Vector3> v = new(); readonly List<Vector3> n = new(); readonly List<Vector2> uv = new(); readonly List<int>[] tris;
            public MeshBuilder(string name, int submeshes) { this.name = name; tris = Enumerable.Range(0, submeshes).Select(_ => new List<int>()).ToArray(); }
            // UVs are world-scale (metres / tile) so textures keep their size on every face.
            public void Quad(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, float tile)
            {
                int i = v.Count;
                var tangent = (d - a).normalized;
                foreach (var p in new[] { a, b, c, d })
                {
                    v.Add(p); n.Add(normal);
                    uv.Add(Mathf.Abs(normal.y) > .5f ? new Vector2(p.x, p.z) / tile : new Vector2(Vector3.Dot(p, tangent) * Mathf.Sign(tangent.x + tangent.z + 1e-4f), p.y) / tile);
                }
                bool front = Vector3.Dot(Vector3.Cross(b - a, c - a), normal) > 0;
                tris[sub].AddRange(front ? new[] { i, i + 1, i + 2, i, i + 2, i + 3 } : new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            }
            public Mesh Build()
            {
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv);
                mesh.subMeshCount = tris.Length;
                for (int s = 0; s < tris.Length; s++) mesh.SetTriangles(tris[s], s);
                mesh.RecalculateTangents(); mesh.RecalculateBounds();
                return mesh;
            }
        }

        // Faded canopy: 16 alternating panels that sag between the seams; one panel is torn short.
        static Mesh TentMesh(string name, int segments, float radius, float height)
        {
            var mesh = new Mesh { name = name };
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>();
            var tris = new[] { new List<int>(), new List<int>() };
            const int rings = 6, cols = 4;
            for (int s = 0; s < segments; s++)
            {
                bool torn = s == 5;
                foreach (var side in new[] { 1, -1 })
                {
                    int start = v.Count;
                    for (int r = 0; r <= rings; r++)
                    for (int k = 0; k <= cols; k++)
                    {
                        float t = r / (float)rings, f = k / (float)cols;
                        float a = (s + f) * Mathf.PI * 2 / segments;
                        float sag = Mathf.Sin(f * Mathf.PI) * Mathf.Sin(t * Mathf.PI) * .38f + Mathf.Sin(f * Mathf.PI) * t * .18f;
                        var p = new Vector3(Mathf.Cos(a) * radius * t, height * (1 - t) - sag, Mathf.Sin(a) * radius * t);
                        v.Add(p); uv.Add(new Vector2(f, t)); n.Add(Vector3.up * side);
                    }
                    for (int r = 0; r < rings; r++)
                    {
                        if (torn && r >= rings - 2) continue;
                        for (int k = 0; k < cols; k++)
                        {
                            int i0 = start + r * (cols + 1) + k, i1 = i0 + 1, i2 = i0 + cols + 1, i3 = i2 + 1;
                            if (side == 1) tris[s % 2].AddRange(new[] { i0, i2, i1, i1, i2, i3 });
                            else tris[s % 2].AddRange(new[] { i0, i1, i2, i1, i3, i2 });
                        }
                    }
                }
            }
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.subMeshCount = 2;
            mesh.SetTriangles(tris[0], 0); mesh.SetTriangles(tris[1], 1);
            mesh.RecalculateNormals();
            // Keep the underside lit like the top: flip normals of the back copy towards the floor.
            var normals = mesh.normals;
            for (int i = 0; i < normals.Length; i++) if (n[i].y < 0 && normals[i].y > 0) normals[i] = -normals[i];
            mesh.normals = normals;
            mesh.RecalculateTangents(); mesh.RecalculateBounds();
            return SaveMesh(mesh);
        }

        static Mesh ValanceMesh(string name, int segments, float radius, float drop)
        {
            var mesh = new Mesh { name = name };
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int i = 0; i < segments; i++)
            {
                if (i == 10 || i == 11) continue; // torn away
                float a0 = i * Mathf.PI * 2 / segments, a1 = (i + 1) * Mathf.PI * 2 / segments, am = (a0 + a1) * .5f;
                var p0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * radius;
                var p1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * radius;
                var q0 = p0 + Vector3.down * drop * .55f; var q1 = p1 + Vector3.down * drop * .55f;
                var m = new Vector3(Mathf.Cos(am), 0, Mathf.Sin(am)) * radius + Vector3.down * drop * (.8f + .2f * Mathf.Sin(i * 1.7f));
                var outward = new Vector3(Mathf.Cos(am), 0, Mathf.Sin(am));
                foreach (var side in new[] { 1, -1 })
                {
                    int s = v.Count;
                    v.AddRange(new[] { p0, p1, q1, m, q0 });
                    for (int k = 0; k < 5; k++) n.Add(outward * side);
                    uv.AddRange(new[] { new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, .45f), new Vector2(.5f, 0), new Vector2(0, .45f) });
                    int[] fan = { s, s + 1, s + 2, s, s + 2, s + 3, s, s + 3, s + 4 };
                    if (side == -1) for (int k = 0; k < fan.Length; k += 3) (fan[k + 1], fan[k + 2]) = (fan[k + 2], fan[k + 1]);
                    tri.AddRange(fan);
                }
            }
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0);
            mesh.RecalculateTangents(); mesh.RecalculateBounds();
            return SaveMesh(mesh);
        }

        static Mesh SaveMesh(Mesh mesh)
        {
            EnsureFolder();
            string path = $"{AssetDir}/{mesh.name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            // CopySerialized into a mesh already on screen left the renderer drawing the previous shape (rebuilt walls showed
            // the old doors and lost whole panels). Refill the asset through the mesh API so the GPU copy is rebuilt as well.
            existing.Clear();
            existing.indexFormat = mesh.indexFormat;
            existing.SetVertices(mesh.vertices); existing.SetNormals(mesh.normals); existing.SetTangents(mesh.tangents); existing.SetUVs(0, mesh.uv);
            existing.subMeshCount = mesh.subMeshCount;
            for (int s = 0; s < mesh.subMeshCount; s++) existing.SetTriangles(mesh.GetTriangles(s), s);
            existing.RecalculateBounds();
            existing.name = mesh.name;
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        // ---------- materials (palette from Task-060 global.md) ----------

        static Texture2D FactoryTex(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(FactoryTextures + path) ?? throw new InvalidOperationException("Missing texture " + path);

        static Material Mat(string name)
        {
            if (Materials.TryGetValue(name, out var cached) && cached != null) return cached;
            Material m = name switch
            {
                // 바닥 회색 #4A4744 under rusted, broken concrete.
                "LA_FloorConcrete" => Lit(name, Hex("A39686"), .2f, FactoryTex("DamagedConcrete/T_DamagedConcrete_BC.TGA"), FactoryTex("DamagedConcrete/T_DamagedConcrete_N.TGA")),
                // 벽 먹색 #2B2826.
                "LA_WallConcrete" => Lit(name, Hex("7A716A"), .1f, FactoryTex("Concrete/T_Concrete_BC.TGA"), FactoryTex("Concrete/T_Concrete_N.TGA")),
                // 녹슨 철 #5C4A3C.
                "LA_WallRust" => Lit(name, Hex("A08670"), .22f, FactoryTex("TileRust/T_TileRust_BC.TGA"), FactoryTex("TileRust/T_TileRust_N.TGA")),
                // 천막 빨강 #8E1F1C / 크림 #D9C7A3, faded.
                "LA_TentRed" => Lit(name, Hex("7A2421"), .05f),
                "LA_TentCream" => Lit(name, Hex("B9A889"), .04f),
                "LA_CarpetWorn" => Lit(name, Hex("6E1E1B"), .08f),
                // 금장 #C9963B tarnished; valves in bronze.
                "LA_Bronze" => Lit(name, Hex("7E5E2E"), .45f, metallic: .6f),
                "LA_Paper" => Lit(name, Hex("BFB6A2"), .05f),
                "LA_HazardPaint" => Lit(name, Hex("8C7A2E"), .15f),
                // Practice room: sawdust ring floor, worn landing mats, dull mirror glass.
                "LA_Sawdust" => Lit(name, Hex("7A6448"), .04f),
                "LA_MatWorn" => Lit(name, Hex("3F4D52"), .1f),
                // Aged mirror without sky reflection: a skybox reflection reads as a window to the outside.
                "LA_Mirror" => NoSkyReflection(Lit(name, Hex("4E5658"), .7f, metallic: .6f)),
                // Rusted steel plate bolted over every window opening.
                "LA_RingFloor" => Lit(name, Color.white, .12f, RingFloorTexture()),
                "LA_SteelPlate" => Lit(name, Hex("8A7462"), .28f, FactoryTex("TileRust/T_TileRust_BC.TGA"), FactoryTex("TileRust/T_TileRust_N.TGA"), metallic: .3f),
                "LA_JointDark" => Lit(name, Hex("161311"), 0f),
                // 청록 타일 #5E8C86: restroom floor and lower walls, the only cold surface in the map.
                "LA_TealTile" => Lit(name, Color.white, .45f, TileTexture()),
                "LA_TealTileDark" => Lit(name, Hex("8FA9A5"), .4f, TileTexture()),
                "LA_Bulb" => Bulb(),
                // The rust texture is a 2x2 atlas; the two left cells (splatter, speckle) read as floor stains.
                "LA_RustDecal" => Region(DecalMaterial(name, "LA_RustDecal.png", RustTexture, .15f, 3001), 0, .5f),
                "LA_RustBloom" => Region(DecalMaterial(name, "LA_RustDecal.png", RustTexture, .1f, 3000), 0, 0),
                "LA_CrackDecal" => DecalMaterial(name, "LA_CrackDecal.png", () => Combine(null, FactoryTex("Decal/T_Crack_Mask.BMP"), Hex("17130F"), .62f, .82f), .05f, 3002),
                "LA_PuddleDecal" => DecalMaterial(name, "LA_PuddleDecal.png", () => Combine(null, FactoryTex("Decal/T_Puddle_Mask.TGA"), Hex("15120F"), .35f, .75f, .8f), .9f, 3003, true),
                "LA_PaintStar" => DecalMaterial(name, "LA_PaintStar.png", StarTexture, .1f, 3004),
                _ => AssetDatabase.LoadAssetAtPath<Material>(WaitingMaterials + name + ".mat"),
            };
            if (m == null) throw new InvalidOperationException("Missing material " + name);
            return Materials[name] = m;
        }

        static Material NoSkyReflection(Material m)
        {
            m.SetFloat("_EnvironmentReflections", 0); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(m);
            return m;
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.magenta;

        static Material Lit(string name, Color color, float smoothness, Texture2D baseMap = null, Texture2D normal = null, float metallic = 0)
        {
            EnsureFolder();
            string path = $"{AssetDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", color);
            m.SetTexture("_BaseMap", baseMap);
            m.SetTexture("_BumpMap", normal);
            if (normal != null) m.EnableKeyword("_NORMALMAP"); else m.DisableKeyword("_NORMALMAP");
            m.SetFloat("_Smoothness", smoothness); m.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Bulb()
        {
            var m = Lit("LA_Bulb", Hex("FFC98A"), .6f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Hex("FFB45A") * 2.6f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Texture2D RustTexture() => Combine(FactoryTex("Decal/T_RustDecal_BC.TGA"), FactoryTex("Decal/T_RustDecal_Mask.TGA"), Hex("6B3E20"), .05f, .6f);

        static Material Region(Material m, float x, float y)
        {
            m.SetTextureScale("_BaseMap", new Vector2(.5f, .5f)); m.SetTextureOffset("_BaseMap", new Vector2(x, y));
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material DecalMaterial(string name, string file, Func<Texture2D> make, float smoothness, int queue, bool highlights = false)
        {
            string path = $"{AssetDir}/{file}";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                var generated = make();
                File.WriteAllBytes(path, generated.EncodeToPNG());
                Object.DestroyImmediate(generated);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.alphaIsTransparency = true; importer.wrapMode = TextureWrapMode.Clamp; importer.mipmapEnabled = true;
                importer.SaveAndReimport();
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            var m = Lit(name, Color.white, smoothness, tex);
            // URP Lit transparent, alpha blended, no depth write: lies on the floor without z-fighting the slab.
            m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0); m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = queue;
            // Preserve-specular keeps reflections on fully transparent texels, which shows the whole quad as a pale square.
            m.SetFloat("_BlendModePreserveSpecular", 0); m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.SetFloat("_EnvironmentReflections", 0); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            m.SetFloat("_SpecularHighlights", highlights ? 1 : 0);
            if (highlights) m.DisableKeyword("_SPECULARHIGHLIGHTS_OFF"); else m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.SetShaderPassEnabled("DepthOnly", false); m.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(m);
            return m;
        }

        // Reads third-party textures through the GPU so their import settings stay untouched.
        static Color[] Read(Texture2D tex, int size)
        {
            var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            Graphics.Blit(tex, rt); RenderTexture.active = rt;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, size, size), 0, 0); t.Apply();
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            var pixels = t.GetPixels(); Object.DestroyImmediate(t);
            return pixels;
        }

        // Mask values between lo and hi ramp to full alpha; this trims the soft halo baked into the source masks.
        static Texture2D Combine(Texture2D color, Texture2D mask, Color tint, float lo, float hi, float opacity = 1f)
        {
            const int size = 512;
            var m = Read(mask, size); var c = color != null ? Read(color, size) : null;
            var output = new Color[m.Length];
            for (int i = 0; i < m.Length; i++)
            {
                float a = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(lo, hi, m[i].r)) * opacity;
                var rgb = c != null ? Color.Lerp(tint, c[i], .6f) : tint;
                output[i] = new Color(rgb.r, rgb.g, rgb.b, a);
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels(output); tex.Apply();
            return tex;
        }

        // Grimy teal ceramic tiles: 4 x 4 tiles per texture (0.3 m each at 1.2 m per repeat), dirty grout, per-tile tint, stains.
        static Texture2D TileTexture()
        {
            EnsureFolder();
            string path = $"{AssetDir}/LA_TealTile.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            const int size = 512, tiles = 4;
            var teal = Hex("5E8C86"); var grout = Hex("2C2A26");
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            var pixels = new Color[size * size];
            var rng = new System.Random(2);
            var tint = Enumerable.Range(0, tiles * tiles).Select(_ => .85f + (float)rng.NextDouble() * .25f).ToArray();
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size * tiles, v = y / (float)size * tiles;
                int tx = (int)u, ty = (int)v; float fx = u - tx, fy = v - ty;
                float edge = Mathf.Min(Mathf.Min(fx, 1 - fx), Mathf.Min(fy, 1 - fy));
                // Tileable stains: sines with whole periods across the texture.
                float stain = .5f + .25f * Mathf.Sin(u * 1.57f + Mathf.Sin(v * 3.14f) * 2f) + .25f * Mathf.Sin(v * 3.14f + Mathf.Sin(u * 1.57f) * 1.5f);
                var c = teal * tint[ty * tiles + tx];
                c = Color.Lerp(c, c * .6f + Hex("4A3A22") * .25f, Mathf.Clamp01((stain - .6f) * 2f));
                c = Color.Lerp(c, Color.white * .9f, Mathf.Pow(1 - Mathf.Clamp01(fy * 3f), 6f) * .12f); // glaze highlight
                if (edge < .035f) c = Color.Lerp(grout, c, edge / .035f * .3f);
                pixels[y * size + x] = new Color(c.r, c.g, c.b);
            }
            tex.SetPixels(pixels); tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat; importer.anisoLevel = 4; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // Faded white five-point star with paint dropout, like the floor marks on the reference.
        static Texture2D StarTexture()
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = (x - size * .5f) / (size * .5f), py = (y - size * .5f) / (size * .5f);
                float r = Mathf.Sqrt(px * px + py * py), a = Mathf.Atan2(py, px);
                float edge = .42f + .48f * Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 2.5f)), 3f);
                float inside = Mathf.Clamp01((edge - r) * 30f);
                float wear = Mathf.PerlinNoise(x * .06f, y * .06f) * .7f + Mathf.PerlinNoise(x * .21f, y * .21f) * .3f;
                pixels[y * size + x] = new Color(.82f, .8f, .74f, inside * Mathf.Clamp01((wear - .32f) * 2.2f) * .55f);
            }
            tex.SetPixels(pixels); tex.Apply();
            return tex;
        }

        static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(AssetDir)) AssetDatabase.CreateFolder("Assets/Scenes/Tests/WaitingRoom/Base", "LayoutA");
        }

        // ---------- scene helpers ----------

        static (Scene, Transform) Begin()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || EditorApplication.isPlaying) throw new InvalidOperationException("Open WaitingRoomBase in Edit mode.");
            source = scene.GetRootGameObjects().Single(g => g.name == "WaitingRoomGreybox").transform;
            if (source.gameObject.activeSelf) throw new InvalidOperationException("Original Greybox must stay inactive.");
            Copies.Clear(); Manifest.Clear(); Materials.Clear();
            random = new System.Random(75);
            return (scene, scene.GetRootGameObjects().Single(g => g.name == "WaitingRoom_LayoutA").transform);
        }

        static int StartUndo(string label)
        {
            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(label);
            return undo;
        }

        static void Finish(Scene scene, int undo, string manifestFile, string title, Transform root, int remapped, int outer)
        {
            Undo.CollapseUndoOperations(undo);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            File.WriteAllLines(Path.Combine(ReportDir, manifestFile), new[]
            {
                "# " + title, "", "원본: WaitingRoomGreybox(비활성 보존). 생성 도구: WaitingRoomLayoutA.", "",
                $"오브젝트: {root.GetComponentsInChildren<Transform>(true).Length}, 내부 참조 재연결: {remapped}, 바깥 참조 재연결: {outer}", "",
            }.Concat(Manifest.Distinct()));
        }

        static void Remove(Transform target, string name)
        {
            var old = target.Find(name);
            if (old == null) return;
            // Nothing outside a generated room may point into it, otherwise rebuilding would break a link.
            foreach (var root in old.gameObject.scene.GetRootGameObjects())
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || mb.transform.IsChildOf(old)) continue;
                var p = new SerializedObject(mb).GetIterator();
                while (p.Next(true))
                    if (p.propertyType == SerializedPropertyType.ObjectReference && TargetTransform(p.objectReferenceValue) is Transform t && t.IsChildOf(old))
                    {
                        if (mb is WaitingRoomWorldLabels || mb is WaitingRoomUiBinding) continue; // re-pointed when the room is rebuilt
                        throw new InvalidOperationException($"{mb.name}.{p.propertyPath} references {name}; refusing to rebuild");
                    }
            }
            Undo.DestroyObjectImmediate(old.gameObject);
        }

        static Transform Required(string path) => source.Find(path) ?? throw new InvalidOperationException("Missing original: " + path);

        static Transform Group(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create Layout A group");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            return go.transform;
        }

        static Transform Prim(Transform parent, PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(position, Quaternion.identity);
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go.transform;
        }

        static Transform Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool collider = false) =>
            Prim(parent, PrimitiveType.Cube, name, position, scale, material, collider);

        static Transform BoxLocal(Transform parent, string name, Vector3 local, Vector3 scale, Material material, bool collider = false)
        {
            var t = Box(parent, name, Vector3.zero, scale, material, collider);
            t.localPosition = local; t.localRotation = Quaternion.identity;
            return t;
        }

        static void Bulb(Transform parent, Vector3 position)
        {
            var b = Prim(parent, PrimitiveType.Sphere, "Bulb", position, Vector3.one * .11f, Mat("LA_Bulb"));
            b.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        static GameObject MeshObject(Transform parent, string name, Vector3 position, Mesh mesh, params Material[] materials)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, true); go.transform.position = position;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            return go;
        }

        static void AddBox(GameObject go, Vector3 center, Vector3 size)
        {
            var c = go.AddComponent<BoxCollider>(); c.center = center; c.size = size;
        }

        static Transform PlaceFactory(string prefab, Transform parent, Vector3 position, float yaw)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(FactoryPrefabs + prefab + ".prefab") ?? throw new InvalidOperationException("Missing prefab " + prefab);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            Manifest.Add($"- 폐공장 에셋 {prefab} → {AnimationUtility.CalculateTransformPath(go.transform, null)}");
            return go.transform;
        }

        static Transform PlaceCopy(string path, Transform parent, Vector3 position, float yaw)
        {
            var t = Copy(Required(path), parent);
            t.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            return t;
        }

        static Transform Copy(Transform original, Transform parent, string name = null, bool registerMap = true)
        {
            var copy = Object.Instantiate(original.gameObject, parent, true);
            copy.name = name ?? original.name;
            if (registerMap) Map(original, copy.transform);
            Manifest.Add("- " + AnimationUtility.CalculateTransformPath(original, null) + " → " + AnimationUtility.CalculateTransformPath(copy.transform, null));
            return copy.transform;
        }

        static void Map(Transform original, Transform copy)
        {
            Copies[original.gameObject] = copy.gameObject;
            var a = original.GetComponents<Component>(); var b = copy.GetComponents<Component>();
            if (a.Length != b.Length || original.childCount != copy.childCount) throw new InvalidOperationException("Clone mismatch");
            for (int i = 0; i < a.Length; i++) if (a[i] != null) Copies[a[i]] = b[i];
            for (int i = 0; i < original.childCount; i++) Map(original.GetChild(i), copy.GetChild(i));
        }

        static Transform TargetTransform(Object target) => target is GameObject go ? go.transform : (target as Component)?.transform;

        static int RemapReferences(Transform root)
        {
            int changed = 0;
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null) throw new InvalidOperationException("Missing copied script");
                if (component is Transform || component is MeshFilter || component is MeshRenderer) continue;
                var so = new SerializedObject(component); var p = so.GetIterator();
                while (p.Next(true))
                {
                    if (p.propertyType != SerializedPropertyType.ObjectReference || p.objectReferenceValue == null) continue;
                    if (Copies.TryGetValue(p.objectReferenceValue, out var replacement)) { p.objectReferenceValue = replacement; changed++; }
                    else if (TargetTransform(p.objectReferenceValue) is Transform tr && tr.IsChildOf(source))
                        throw new InvalidOperationException($"Uncopied reference: {component.name}.{p.propertyPath} → {tr.name}");
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return changed;
        }

        static int RemapExistingReferences(Scene scene, Transform built)
        {
            int changed = 0;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null || component.transform.IsChildOf(source) || component.transform.IsChildOf(built)) continue;
                var so = new SerializedObject(component); var p = so.GetIterator(); bool dirty = false;
                while (p.Next(true))
                    if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue != null && Copies.TryGetValue(p.objectReferenceValue, out var replacement))
                    {
                        if (!dirty) Undo.RecordObject(component, "Connect Layout A copy");
                        p.objectReferenceValue = replacement; changed++; dirty = true;
                        Manifest.Add($"- 참조 재연결: {AnimationUtility.CalculateTransformPath(component.transform, null)} {component.GetType().Name}.{p.propertyPath} → {AnimationUtility.CalculateTransformPath(TargetTransform(replacement), null)}");
                    }
                if (dirty) so.ApplyModifiedProperties();
            }
            return changed;
        }

        static Dictionary<int, string> Snapshot(Transform root) => root.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.GetInstanceID(), State);
        static string State(Transform t) => t.name + "|" + t.gameObject.activeSelf + "|" + t.position.ToString("R") + "|" +
            string.Join("|", t.GetComponents<Component>().Select(c => c == null ? "MISSING" : EditorJsonUtility.ToJson(c)));
        static void AssertUnchanged(Dictionary<int, string> before)
        {
            var after = Snapshot(source);
            if (before.Count != after.Count || before.Any(p => !after.TryGetValue(p.Key, out var value) || p.Value != value))
                throw new InvalidOperationException("Preserved Greybox content changed");
        }
    }
}
