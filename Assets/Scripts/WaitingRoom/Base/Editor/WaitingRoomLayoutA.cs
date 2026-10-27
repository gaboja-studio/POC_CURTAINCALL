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
            new("09", "연습실", 360, 75, 665, 295),
            new("10", "작업실", 150, 160, 360, 340),
            new("ST", "계단", 55, 235, 150, 430),
            new("11", "무대 장치 보관실", 350, 805, 675, 1010),
            new("12", "화물 적재 장소", 680, 805, 1000, 1010),
            new("13", "기계실", 130, 395, 385, 530, "B1"),
            new("14", "관리실", 20, 555, 150, 660, "B1"),
            new("CH", "탈의 방", 45, 655, 210, 800, "B1"),
            new("EX", "탈출로 입구", 225, 630, 380, 710, "B1"),
            new("LG", "휴게실 (로비 안)", 600, 660, 700, 760),
        };

        public static Vector2 ToLocal(float px, float py) => new((px - LobbyCenterPx.x) / PxPerMeter, -(py - LobbyCenterPx.y) / PxPerMeter);
        static Vector3 W(float x, float z, float y = 0) => Origin + new Vector3(x, y, z);

        // Lobby hall: 29.4 x 28 m with the prop room cut into the south-east corner, as on the plan.
        const float LobbyW = 14.7f, LobbyN = 14f, LobbyS = -14f, NotchX = 6.4f, NotchZ = -4f;
        const float WallHeight = 13f, DoorHeight = 3.6f, LedgerY = 4.6f, TrussY = 11.6f;
        static readonly Vector2 TentCenter = new(.55f, 4.3f);
        const float TentRadius = 7.6f, TentEdgeY = 4.8f, TentApexY = 8.4f;
        static readonly Vector2 ShopPillar = new(-10.8f, -6f);

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
                BuildTent(Group(lobby, "CircusTent", W(TentCenter.x, TentCenter.y)));
                BuildFloorWear(Group(lobby, "FloorWear", Origin));
                BuildDressing(Group(lobby, "Dressing", Origin));
                BuildLighting(Group(lobby, "Lighting", Origin));
                BuildLobbyGameplay(lobby);
                Group(lobby, "LoungeReservation", W(0, -9.3f)); // Stage 5 builds the lounge here.
                if (target.Find("S04_PropRoom") != null) OpenPortal(target, "04"); // rooms already built keep their doorway open
                if (target.Find("S02_Dormitory") != null) OpenPortal(target, "02");
                if (target.Find("S06_StageCorridor") != null) OpenPortal(target, "06");
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
                PlaceCopy("Mood/InteriorArt/StorageTrunk_0", extras, W(ShopPillar.x - 1.2f, ShopPillar.y + 1.8f), 80f);
                PlaceFactory("SM_Woodpallet_1", extras, W(ShopPillar.x - 1.3f, ShopPillar.y - 2.2f), 8);
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
        const float PropX0 = 6.9f, PropX1 = 18.1f, PropZ0 = -14.1f, PropZ1 = -4.5f, PropHeight = 5.6f, PropDoorZ = -8.7f, PropDoorWidth = 2.6f;

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
        const float DormX0 = 3.6f, DormX1 = 12f, RestX0 = 12.2f, RestX1 = 17.8f, DormZ0 = 14.5f, DormZ1 = 22.5f, DormHeight = 4.6f;
        const float DormDoorX = 7.8f, DormDoorWidth = 3f, RestDoorZ = 16.1f, RestDoorWidth = 1.4f;

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
                    new[] { (0, RestDoorZ, RestDoorWidth, .1f) }, westThickness: .1f);
                // One collider under both rooms and the lobby doorway, so the wall thickness has floor.
                var floorCollider = new GameObject("FloorCollider"); floorCollider.transform.SetParent(root, false);
                floorCollider.transform.position = W((DormX0 + RestX1) * .5f, (LobbyN - .1f + DormZ1) * .5f, -.1f);
                floorCollider.AddComponent<BoxCollider>().size = new Vector3(RestX1 - DormX0 + .2f, .2f, DormZ1 - LobbyN + .1f);

                // Dormitory: original items keep their own shape; positions compress along X and shift north by +6.6 m.
                const string art = "Mood/InteriorArt/Stage01_Dormitory/";
                float DX(float oldX) => 7.7f + (oldX + 3.7f) * .72f;
                const float dz = 6.6f - 70f; // world z old -> local z new
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
                float RZ(float oldZ) => DormZ0 + (oldZ - 79.15f) * .952f;
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
        const float StageX0 = 36.5f, StageX1 = 42.8f, StageZ0 = -4.5f, StageZ1 = 5.5f;
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
                    if (chunk == StageChunk && group == "SteelRibs") return; // ribs span the 8 m tunnel, not the wider entrance
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
                var vaultSource = Required("Mood/Tunnel/Vault");
                var vault = Copy(vaultSource, art);
                vault.localScale = Vector3.Scale(vault.localScale, new Vector3((CorX1 - CorX0) / 42f, 1, 1));
                vault.position = new Vector3(W((CorX0 + CorX1) * .5f, 0).x, vaultSource.position.y, vaultSource.position.z + RouteDz);
                PipeRun(art, new(CorX0 + .3f, CorZ0 + .35f), new(CorX1 - .3f, CorZ0 + .35f), 4.1f, .3f);

                // 07: the stage curtain fills the east wall; faded drapes close the gaps either side of the proscenium, a bulb row marks it.
                foreach (var (z, width) in new[] { (StageZ0 + .6f, 1.2f), (StageZ1 - .55f, 1.1f) })
                {
                    Box(stageArt, "FadedDrape", W(StageX1 - .12f, z, 2.8f), new Vector3(.08f, 5.6f, width), Mat("LA_TentRed"));
                    Box(stageArt, "DrapeRod", W(StageX1 - .2f, z, 5.65f), new Vector3(.06f, .06f, width + .2f), Mat("LA_Bronze"));
                }
                for (int i = 0; i <= 13; i++) Bulb(stageArt, W(StageX1 - .9f, Mathf.Lerp(-3.2f, 4.3f, i / 13f), 5.3f));

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
            (new(-LobbyW, LobbyN), new(LobbyW, LobbyN), new(0, -1), new[] { new Door("09", "09 연습실", 6.9f, 3f), new Door("02", "02 숙소 / 화장실", 22.5f, 3f) }, new[] { 2.4f, 11.2f, 17.8f, 27f }),
            (new(LobbyW, LobbyN), new(LobbyW, NotchZ), new(-1, 0), new[] { new Door("08", "08 전문가 대기실", 4.7f, 2.8f), new Door("06", "06 무대 입구 통로", 13.5f, 5f, true) }, new[] { 8.6f }),
            (new(LobbyW, NotchZ), new(NotchX, NotchZ), new(0, 1), Array.Empty<Door>(), new[] { 4.2f }),
            (new(NotchX, NotchZ), new(NotchX, LobbyS), new(-1, 0), new[] { new Door("04", "04 소품실", 4.7f, 2.6f) }, Array.Empty<float>()),
            (new(NotchX, LobbyS), new(-LobbyW, LobbyS), new(0, 1), new[] { new Door("12", "12 화물 적재 장소", 1.9f, 3f), new Door("11", "11 무대 장치 보관실", 13.6f, 3.6f) }, new[] { 7.4f, 18.2f }),
            (new(-LobbyW, LobbyS), new(-LobbyW, LobbyN), new(1, 0), new[] { new Door("13", "지하 연결 (기계실·탈출로)", 12.1f, 3f), new Door("10", "10 작업실 · 계단", 25f, 3f) }, new[] { 3.6f, 8.9f, 17.5f }),
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

            // High clerestory windows on the north and east walls; the only cold light in the hall comes through these.
            foreach (var x in new[] { -9.5f, -1.5f, 9f })
                Box(structure, "ClerestoryWindow", W(x, LobbyN - .05f, 9.6f), new Vector3(3.2f, 1.8f, .08f), Mat("V4_Skylight"));
            Box(structure, "ClerestoryWindow", W(LobbyW - .05f, 4.5f, 9.6f), new Vector3(.08f, 1.8f, 3.2f), Mat("V4_Skylight"));

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
            // North-west catwalk (only on this side) with railing, hanging brackets and a ladder down to the floor.
            const float deckY = 5.2f, x0 = -LobbyW + .3f, x1 = -2.8f, depth = 1.8f;
            var deckZ = LobbyN - .3f - depth * .5f;
            for (float x = x0; x < x1 - .1f; x += 2f)
            {
                float w = Mathf.Min(2f, x1 - x);
                Box(structure, "CatwalkGrate", W(x + w * .5f, deckZ, deckY), new Vector3(w - .04f, .08f, depth), Mat("LA_WallRust"));
                Box(structure, "CatwalkBracket", W(x + .1f, deckZ, deckY - .45f), new Vector3(.08f, .9f, depth), Mat("V4_Iron_Dark"));
            }
            foreach (var rx in Enumerable.Range(0, 6).Select(i => x0 + 1f + i * 2f).Where(rx => rx < x1 - .5f))
                PlaceFactory("SM_Railing", structure, W(rx + 1f, deckZ - depth * .5f, deckY + .04f), 0);
            Box(structure, "CatwalkFascia", W((x0 + x1) * .5f, deckZ - depth * .5f, deckY - .1f), new Vector3(x1 - x0, .3f, .06f), Mat("V4_Iron_Dark"));
            PlaceFactory("SM_Ladder1", structure, W(-4.4f, LobbyN - .55f), 180).localScale = new Vector3(1, deckY / 6.32f, 1);
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
            PlaceFactory("SM_Barrel", art, W(13.6f, 10.8f), 30); PlaceFactory("SM_Barrel_1", art, W(13.1f, 10.1f), 0);
            PlaceFactory("SM_Grid", art, W(14.1f, 13.4f), 0);
            // West wall: crimson runner, chair and trunk; crate pile just west of the tent.
            Box(art, "WestRunner", W(-13.2f, 4.6f, .02f), new Vector3(1.8f, .01f, 8.4f), Mat("LA_CarpetWorn"));
            PlaceCopy("Mood/Furnishings/Lobby/WaitingChair", art, W(-13.4f, 6.6f), 95f);
            PlaceCopy("Mood/Furnishings/Lobby/PackedTrunk", art, W(-13.3f, 3.0f), 80f);
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
        }

        // ---------- lighting: dark baseline, warm pools at functional points ----------

        static void BuildLighting(Transform lighting)
        {
            var c = W(TentCenter.x, TentCenter.y);
            Point(lighting, "TentKey", c + Vector3.up * 7.2f, new Color(1f, .71f, .4f), 5f, 15f, LightShadows.Soft);
            Point(lighting, "UnderTentPool", c + Vector3.up * 3.6f, new Color(1f, .7f, .42f), 2.2f, 7.5f);
            foreach (var (x, z) in new[] { (-7f, 8.6f), (8.2f, 8f), (-7f, -1.8f), (8.5f, 2f) })
                Point(lighting, "StringGlow", W(x, z, 8.2f), new Color(1f, .68f, .36f), 1.1f, 8f);
            Spot(lighting, "SpawnSpot", W(.5f, -9.5f, 7f), W(.5f, -5.6f), new Color(1f, .78f, .52f), 2.4f, 11f, 50f);
            Spot(lighting, "ScheduleSpot", W(0, LobbyN - 3.2f, 5.4f), W(0, LobbyN - .1f, 1.9f), new Color(1f, .84f, .62f), 3f, 7f, 45f);
            // Warm grazing light on two wall stretches so the concrete and rusted plate read, leaving the corners dark.
            Spot(lighting, "WallWash_North", W(5f, 7f, 7.5f), W(7.8f, LobbyN, 3.5f), new Color(1f, .64f, .38f), 2.6f, 14f, 50f);
            Spot(lighting, "WallWash_West", W(-6f, 2f, 7.5f), W(-LobbyW, 4.5f, 3f), new Color(1f, .64f, .38f), 2.2f, 14f, 50f);
            Point(lighting, "MarqueeGlow", W(LobbyW - 1.4f, .5f, 3.8f), new Color(1f, .6f, .3f), 3f, 7f);
            // Cold moonlight through two clerestory windows; this and the ambient are the only cold tones.
            Spot(lighting, "WindowShaft_N", W(-9.5f, LobbyN - .4f, 10f), W(-7.5f, 6f), new Color(.62f, .76f, .82f), 2.2f, 18f, 32f);
            Spot(lighting, "WindowShaft_E", W(LobbyW - .4f, 4.5f, 10f), W(7f, 1.5f), new Color(.62f, .76f, .82f), 1.6f, 18f, 30f);
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
            EditorUtility.CopySerialized(mesh, existing);
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
