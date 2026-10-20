using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.WaitingRoom.EditorTools
{
    /// <summary>
    /// 비활성 대기실 프로토타입(`대기실` 루트)의 톤앤매너를 현재 그레이박스(`WaitingRoomGreybox`)에 옮긴다.
    /// 1) 그레이박스 도형의 GB_* 재질을 프로토타입 V4~V6 재질로 바꾼다(글자·판정 구역·스크립트가 색을 입히는 도형은 유지).
    /// 2) 천장·조명·안개·소품을 프로토타입에서 복제해 `WaitingRoomGreybox/Mood` 아래에 배치한다.
    /// 기존 그레이박스 오브젝트의 구조·콜라이더·기능 컴포넌트는 건드리지 않는다. 다시 실행하면 Mood를 지우고 새로 만든다.
    /// </summary>
    public static class WaitingRoomMoodDresser
    {
        const string GreyboxName = "WaitingRoomGreybox";
        const string SourceName = "대기실";
        const string MoodName = "Mood";
        const string ProtoMaterialFolder = "Assets/_ThirdParty/Materials/WaitingArea";
        const string MeshAssetPath = "Assets/Scenes/Tests/WaitingRoom/Base/Prefabs/WaitingRoomMoodMeshes.asset";
        const float CeilingY = WaitingRoomGreyboxBuilder.WallHeight;
        const float CeilingLift = CeilingY - 5f; // 처음 배치 기준(천장 5m)에서 천장에 매단 소품을 올리는 양
        const float TilesPerMeter = 0.5f;

        // 그레이박스 로컬 좌표(로비 중심 = 원점)의 방 범위: x0, z0, x1, z1 — 넓힌 레이아웃(WaitingRoomLayoutFitter) 기준
        static readonly Rect Lobby = Rect.MinMaxRect(-10f, -9f, 10f, 9f);
        static readonly Rect Dormitory = Rect.MinMaxRect(-10f, 9f, 10f, 21f); // 숙소 + 화장실
        static readonly Rect PropRoom = WaitingRoomLayoutFitter.PropRoom;
        static readonly Rect JunkStorage = WaitingRoomLayoutFitter.JunkStorage;
        static readonly Rect Corridor = WaitingRoomLayoutFitter.Corridor;
        static readonly Rect ExpertRoom = WaitingRoomLayoutFitter.ExpertRoom;

        static readonly HashSet<string> KeepMaterials = new() { "GB_Text", "GB_Zone", "GB_Marker" };

        static readonly Dictionary<string, Material> materials = new();
        static readonly Dictionary<string, Mesh> meshes = new();
        static Transform greybox, source;

        [MenuItem("Tools/CurtainCall/대기실/프로토타입 분위기 입히기")]
        static void ApplyMenu()
        {
            var scene = SceneManager.GetActiveScene();
            var mood = Apply(scene);
            if (mood == null) return;
            Selection.activeGameObject = mood;
            EditorSceneManager.MarkSceneDirty(scene);
        }

        public static GameObject Apply(Scene scene)
        {
            greybox = Root(scene, GreyboxName);
            source = Root(scene, SourceName);
            if (greybox == null || source == null)
            {
                Debug.LogError($"[WaitingRoomMood] '{GreyboxName}' 또는 '{SourceName}' 루트가 씬에 없습니다.");
                return null;
            }

            materials.Clear();
            LoadMeshes();

            var old = greybox.Find(MoodName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            WaitingRoomTunnelLayout.Apply(greybox);   // 기획서에 없는 터널 옆방 정리·집결 구역 옮기기(이미 했으면 그대로)
            WaitingRoomLayoutFitter.Fit(greybox);     // 카메라 구역 다시
            WaitingRoomRoomArranger.Arrange(greybox); // 가구를 벽 쪽으로·표지 맞춤·설명 글자 끄기(절대 위치라 반복 실행 안전)
            ShowStandInTargets(true);

            int remapped = RemapGreyboxMaterials();
            RefreshTiledMeshes();

            var mood = new GameObject(MoodName).transform;
            Undo.RegisterCreatedObjectUndo(mood.gameObject, "Waiting Room Mood");
            mood.SetParent(greybox, false);

            BuildCeilings(Group(mood, "Ceilings"));
            BuildLights(Group(mood, "Lights"));
            BuildFog(Group(mood, "Fog"));
            BuildDressing(Group(mood, "Dressing"));
            BuildTunnel(Group(mood, "Tunnel"));
            BuildCurtains(Group(mood, "Curtains"));
            BuildCeilingworks(Group(mood, "Ceilingworks"));
            BuildRooms(Group(mood, "Rooms"));
            BuildProtoFills(Group(mood, "ProtoFill"));
            BuildLightingPass(mood);
            FitWiresToCeiling(mood);
            ShowStandInTargets(false);

            AssetDatabase.SaveAssets();
            Selection.activeGameObject = mood.gameObject;
            Debug.Log($"[WaitingRoomMood] 재질 {remapped}개 교체, Mood 오브젝트 {mood.GetComponentsInChildren<Transform>(true).Length - 1}개 배치");
            return mood.gameObject;
        }

        // ── 1. 그레이박스 재질 → 프로토타입 재질 ─────────────────────

        static int RemapGreyboxMaterials()
        {
            int count = 0;
            foreach (var renderer in greybox.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (IsUnderMood(renderer.transform)) continue;
                var current = renderer.sharedMaterial;
                if (current == null || !current.name.StartsWith("GB_") || KeepMaterials.Contains(current.name)) continue;
                if (IsScriptTinted(renderer.transform)) continue;

                var target = ProtoMaterialFor(renderer.transform, current.name);
                if (target == null) continue;
                Undo.RecordObject(renderer, "Waiting Room Mood");
                renderer.sharedMaterial = target;
                ApplyTiledMesh(renderer);
                count++;
            }
            return count;
        }

        /// <summary>이미 교체된 도형의 크기가 바뀌었으면(벽 높이 조정 등) 바둑판 UV 메시를 새 크기로 다시 고른다.</summary>
        static void RefreshTiledMeshes()
        {
            foreach (var renderer in greybox.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (IsUnderMood(renderer.transform)) continue;
                var mf = renderer.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null && mf.sharedMesh.name.StartsWith("MoodBox_")) ApplyTiledMesh(renderer);
            }
        }

        static bool IsUnderMood(Transform t)
        {
            for (var p = t; p != null && p != greybox; p = p.parent)
                if (p.name == MoodName && p.parent == greybox) return true;
            return false;
        }

        /// <summary>런타임에 sharedMaterial을 복제해 색을 입히는 도형(선반 칸·배송구)은 그레이박스 재질을 유지한다.</summary>
        static bool IsScriptTinted(Transform t) =>
            (t.name.StartsWith("Board_") && t.GetComponentInParent<HandoverShelfView>(true) != null)
            || t.GetComponent<ShopDeliverySlot>() != null;

        static Material ProtoMaterialFor(Transform t, string gb)
        {
            string path = PathOf(t);
            bool In(string part) => path.Contains(part);
            string name = t.name;

            string proto = gb switch
            {
                "GB_Floor" => In("S02_") || In("S06_") || In("S08_") ? "V4_Floor_OldPlank" : "V4_Floor_WetConcrete",
                "GB_Wall" => In("W_06_") ? "V5_Vault_Concrete" : In("W_05_") || In("W_08_") ? "V4_Wall_PeeledPlaster" : "V4_Wall_DamagedConcrete",
                "GB_Wood" => "V4_Wood_Old",
                "GB_Metal" => name is "Pillar" or "Upright" ? "V4_Iron_Dark" : "V4_RustyMetal",
                "GB_Cream" => name == "Mattress" ? "V4_Mattress_Stained" : name == "OldDolls" ? "V4_Mask_Porcelain" : "V4_CircusStripes",
                "GB_Door" => In("CircusTent") ? "V4_Velvet_Red" : "V4_Velvet_Red_Cloth",
                "GB_Fabric" => name == "Partition" ? "V4_Partition_RustGreen"
                    : name == "MascotSuit" ? "V4_Mask_BearFur"
                    : name.StartsWith("OldCostume") ? "V4_Harlequin"
                    : "V4_Velvet_Red",
                "GB_Ceramic" => "V4_Ceramic_Dirty",
                "GB_Mirror" => "V4_Mirror_Old",
                "GB_Paper" => "V6_Paper_Old",
                "GB_Gold" => "V4_Gold_Trim",
                "GB_Dark" => "V4_Black",
                _ => null,
            };
            return proto == null ? null : ProtoMaterial(proto);
        }

        // ── 2. 천장 ─────────────────────────────────────────────

        static void BuildCeilings(Transform p)
        {
            Ceiling(p, "Ceiling_Lobby", Lobby, "V4_Ceiling_RustyMetal");
            Ceiling(p, "Ceiling_Dormitory", Dormitory, "V4_Ceiling_Concrete");
            Ceiling(p, "Ceiling_PropRoom", PropRoom, "V4_Ceiling_Concrete");
            Ceiling(p, "Ceiling_JunkStorage", JunkStorage, "V4_Ceiling_RustyMetal");
            Ceiling(p, "Ceiling_Corridor", Corridor, "V4_Ceiling_RustyMetal");
            Ceiling(p, "Ceiling_ExpertRoom", ExpertRoom, "V4_Ceiling_Concrete");
        }

        static void Ceiling(Transform p, string name, Rect r, string material, float height = CeilingY)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(p, false);
            go.transform.localPosition = new Vector3(r.center.x, height + 0.1f, r.center.y);
            go.transform.localScale = new Vector3(r.width - (height < CeilingY ? 0.3f : -0.3f), 0.2f, r.height - (height < CeilingY ? 0.3f : -0.3f)); // 낮은 천장은 벽 안쪽에 맞춘다
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = ProtoMaterial(material);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            ApplyTiledMesh(renderer);
        }

        // ── 3. 조명 (프로토타입 조명 복제) ─────────────────────────

        static void BuildLights(Transform p)
        {
            var hall = Src("대기실_본관/조명");
            var cagedLamp = hall.GetChild(0);
            var lampLight = hall.GetChild(1);

            // 로비: 철망 전구 4개 + 차가운 보조광 + 천창 2개
            foreach (var at in new[] { new Vector2(-6f, -5.5f), new Vector2(6f, -5.5f), new Vector2(-6f, 5.5f), new Vector2(6f, 5.5f) })
                Lamp(p, "Lobby_Lamp", cagedLamp, lampLight, at, 4.1f, null);
            Place(p, "Lobby_FillCold", new[] { hall.Find("FillCold") }, new Vector3(0f, 4.5f, 0f));
            foreach (var x in new[] { -7f, 7f })
                Place(p, "Lobby_Skylight", new[] { hall.Find("Skylight"), hall.Find("SkylightBeam") }, new Vector3(x, CeilingY - 0.03f, 0f), anchor: hall.Find("Skylight"));

            // 숙소: 낮은 주황 전구 2개 / 화장실: 프로토타입 화장실 조명(차가운 빛) 그대로
            var dormColor = new Color32(0xFF, 0x9E, 0x52, 0xFF);
            foreach (var x in new[] { -7.6f, -1.4f })
                Lamp(p, "Dormitory_Lamp", cagedLamp, lampLight, new Vector2(x, 15f), 4.1f, l => { l.color = dormColor; l.intensity = 3.5f; l.range = 6f; });
            Place(p, "Restroom_Lights", Children(Src("화장실/조명")), new Vector3(6.5f, float.NaN, 15f), yOffset: 0.35f);

            // 소품실·분장실 구역: 각 방의 프로토타입 조명 묶음
            Place(p, "PropRoom_Lights", Children(Src("소품실/조명")), new Vector3(-4.5f, float.NaN, -14.75f), yOffset: 0.35f);
            Place(p, "DressingCorner_Lights", Children(Src("분장실/조명")), new Vector3(5.5f, float.NaN, -14.75f), yOffset: 0.35f);

            // 폐기물 보관소: 백스테이지 전구 + 차가운 보관소 채움광
            var backColor = new Color32(0xFF, 0xA3, 0x57, 0xFF);
            foreach (var z in new[] { -5f, 5f })
                Lamp(p, "JunkStorage_Lamp", cagedLamp, lampLight, new Vector2(-16f, z), 4.1f, l => { l.color = backColor; l.intensity = 3.8f; l.range = 8f; });
            Place(p, "JunkStorage_Fill", new[] { Find(Src("백스테이지1"), "StorageFill") }, new Vector3(-16f, 3.5f, 0f));

            // 무대 진입 통로 조명은 BuildTunnel이 둔다

            // 공연 스테이지 입구: 붉은 문틈 빛
            Place(p, "StageEntrance_RedSpill", new[] { hall.Find("DoorRedSpill") }, new Vector3(WaitingRoomTunnelLayout.TunnelEnd - 1.6f, 1.5f, 0f));
        }

        static void Lamp(Transform p, string name, Transform lamp, Transform light, Vector2 at, float y, System.Action<Light> tune)
        {
            var g = Place(p, name, new[] { lamp, light }, new Vector3(at.x, y, at.y), anchor: lamp);
            if (tune != null)
                foreach (var l in g.GetComponentsInChildren<Light>(true)) tune(l);
        }

        // ── 4. 안개 (프로토타입 파티클 복제, 방 크기에 맞춤) ─────────────

        static void BuildFog(Transform p)
        {
            var fog = Src("안개");
            Fog(p, fog.Find("대기실_바닥안개"), Lobby, 0.35f);
            Fog(p, fog.Find("대기실_연무"), Lobby, 2.2f);
            Fog(p, fog.Find("터널_바닥안개"), Corridor, 0.35f);
            Fog(p, fog.Find("터널_연무"), Corridor, 2.4f);
            Fog(p, fog.Find("백스테이지2_바닥안개"), JunkStorage, 0.35f);
            Fog(p, fog.Find("백스테이지2_연무"), JunkStorage, 2.6f);
        }

        static void Fog(Transform p, Transform src, Rect room, float y)
        {
            var copy = Duplicate(new[] { src })[0].transform;
            copy.SetParent(p, true);
            copy.localPosition = new Vector3(room.center.x, y, room.center.y);
            var ps = copy.GetComponent<ParticleSystem>();
            var shape = ps.shape;
            var old = shape.scale;
            var size = new Vector3(room.width - 1f, old.y, room.height - 1f);
            shape.scale = size;
            float ratio = (size.x * size.z) / Mathf.Max(1f, old.x * old.z);
            var main = ps.main;
            main.maxParticles = Mathf.Max(20, Mathf.RoundToInt(main.maxParticles * ratio));
            var emission = ps.emission;
            var rate = emission.rateOverTime;
            rate.constantMax = Mathf.Max(1f, rate.constantMax * ratio);
            rate.constantMin = Mathf.Max(0.5f, rate.constantMin * ratio);
            emission.rateOverTime = rate;
        }

        // ── 5. 소품 (프로토타입 소품 복제) ─────────────────────────

        static void BuildDressing(Transform p)
        {
            BuildTentPile(Group(p, "Lobby_TentPile"));

            // 로비 바닥 연출(물웅덩이·먼지)을 로비 폭에 맞춰 압축
            Place(p, "Lobby_FloorStains", new[] { Src("대기실_본관/바닥_연출") }, new Vector3(0f, float.NaN, 0f), scale: new Vector3(0.55f, 1f, 1.4f));

            // 화장실: 배수구·물웅덩이, 쓰레기통
            var restroom = Src("화장실");
            Place(p, "Restroom_Wet", restroom.Cast<Transform>().Where(c => c.name is "Drain" or "Puddle").ToArray(), new Vector3(6.5f, float.NaN, 14f));
            Place(p, "Restroom_Dustbin", new[] { restroom.Find("SM_CS1_Dustbin_c1_LOD0") }, new Vector3(9.4f, float.NaN, 10.4f));

            var dressing = Src("분장실");

            // 폐기물 보관소: 벽에 걸린 탈 4개, 트렁크와 엉킨 천
            Place(p, "JunkStorage_Masks", dressing.Cast<Transform>().Where(c => c.name.StartsWith("탈_")).ToArray(), new Vector3(-21.6f, float.NaN, 6.8f));
            Place(p, "JunkStorage_Trunk", new[] { dressing.Find("트렁크"), dressing.Find("SM_MET_TangledCloth02_c1_LOD1") }, new Vector3(-13.5f, float.NaN, -5.6f));

            // 숙소 침대: 바랜 담요
            var blankets = Group(p, "Dormitory_Blankets");
            var blanketMats = new[] { "V4_Blanket_DullRed", "V4_Blanket_Navy", "V4_Blanket_Olive", "V4_Blanket_Navy" };
            var beds = greybox.Find("S02_Dormitory/Beds");
            for (int i = 0; beds != null && i < beds.childCount; i++)
            {
                var mattress = beds.GetChild(i).Find("Mattress");
                if (mattress == null) continue;
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Blanket";
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(blankets, false);
                var m = greybox.InverseTransformPoint(mattress.position); // 침대 묶음이 옮겨져도 따라가게 월드 기준
                go.transform.localPosition = new Vector3(m.x, m.y + 0.07f, m.z - 0.3f);
                go.transform.localScale = new Vector3(1.04f, 0.05f, 1.15f);
                go.transform.localRotation = Quaternion.Euler(0f, (i % 2 == 0 ? 4f : -6f), 0f);
                go.GetComponent<MeshRenderer>().sharedMaterial = ProtoMaterial(blanketMats[i % blanketMats.Length]);
            }
        }

        // ── 4단계: 공간별 이식(①②④⑤⑧) ───────────────────────────────
        // 「대기실 에셋 재사용 후보」 공간별 항목에 맞춰 프로토타입 세트를 옮긴다. 그레이박스 기능 오브젝트는 그대로 두고,
        // 출입구·동선·스폰은 비운다. ⑥⑦은 BuildTunnel, ① 천막 더미는 BuildTentPile.

        static void BuildRooms(Transform p)
        {
            var hall = Src("대기실_본관");
            var junk = Src("대기실_본관/잡동사니");
            var debris = Src("백스테이지2/잔해");

            // ① 로비: 깃발줄 2줄(7m, 천막 깃대·쇠사슬 피함) + 트러스에 매단 줄무늬 낡은 천
            var lobby = Group(p, "Lobby");
            foreach (var (z, path) in new[] { (-6.5f, "Props/SM_Prop_FlagLine_01.prefab"), (0.5f, "Props/SM_Prop_FlagLine_02.prefab") })
            {
                var flags = Prefab(lobby, SyntyApocalypse + path, new Vector3(0f, 7f, z), 0f);
                if (flags != null) flags.localScale = new Vector3((Lobby.width - 0.4f) / 27.42f, 1f, 1f);
            }
            var rags = Place(lobby, "TrussRags", new[] { Src("무대진입_터널/낡은천") }, new Vector3(0f, float.NaN, -4.5f));
            HangFrom(rags, TrussBottom);
            CullOutside(rags, Rect.MinMaxRect(Lobby.xMin + 0.5f, Lobby.yMin, Lobby.xMax - 0.5f, Lobby.yMax));

            // ② 숙소: 철창 칸은 쓰지 않는다(사용자 결정 2026-10-20). 서쪽 벽 기존 사물함 옆에 프로토타입 사물함 한 칸(벽을 등지게),
            //    벤치는 침대 발치에 침대와 나란히(로비 문에서 들어오는 길을 막지 않게)
            var dorm = Group(p, "Dormitory");
            Place(dorm, "Lockers", new[] { First(hall.Find("사물함_의상"), "사물함") }, new Vector3(-9.35f, float.NaN, 14.2f), yaw: 90f);
            var lockerSet = hall.Find("사물함_의상");
            Place(dorm, "Bench", lockerSet.Cast<Transform>().Where(t => t.name.StartsWith("Bench")).ToArray(), new Vector3(-4.6f, float.NaN, 17.85f));

            // ② 「낙서」 표지 자리: 북쪽 벽에 얼룩과 찢긴 포스터(표지만 떠 있지 않게)
            WallMark(dorm, "Graffiti", new Vector3(-7.6f, 0.8f, 20.83f), 0f);

            // ④ 소품실은 BuildPropRoom(그레이박스 가구 자리에 프로토타입 실물)

            // ⑤ 폐기물 보관소: 낡은 매트리스 더미(북동 모서리), 빛바랜 현수막 "오늘도 웃어라"(남쪽 벽 위), 통·팔레트(남동 모서리)
            var store = Group(p, "JunkStorage");
            WallMark(store, "Graffiti", new Vector3(-21.83f, 0.8f, 0.5f), 90f);    // 「낙서」 표지 자리(서쪽 벽)
            Place(store, "OldMattresses", debris.Cast<Transform>().Where(t => t.name == "OldMattress").ToArray(), new Vector3(-11.6f, float.NaN, 7.4f), yaw: 90f);
            var banner = debris.Cast<Transform>().Where(t => t.name == "FadedBanner" || t.name.StartsWith("Text_")).ToArray();
            Place(store, "FadedBanner", banner, new Vector3(-16f, float.NaN, -8.78f), anchor: banner.FirstOrDefault(t => t.name == "FadedBanner"));
            CopyAt(store, junk.Find("SM_Barrel"), new Vector3(-10.9f, 0f, -8.2f), 0f);
            CopyAt(store, junk.Find("SM_Barrel_1"), new Vector3(-11.8f, 0f, -8.0f), 35f);
            CopyAt(store, junk.Find("SM_Woodpallet_2"), new Vector3(-13.0f, 0f, -7.9f), 80f);

            // ⑧ 전문가 대기실: 로비 쪽 출입구 위 붉은 밸런스·금색 술(특별한 방 표시), 안쪽 나무 의자·철망 전구
            var curtains = Src("무대진입_터널/커튼");
            Place(p, "ExpertDoorValance", new[] { curtains?.Find("Rail"), curtains?.Find("Valance"), curtains?.Find("Fringe") }, new Vector3(9.78f, float.NaN, 6.5f), scale: new Vector3(1f, 1f, 2.4f / 6f));
            var expert = Group(p, "ExpertRoom");
            CopyAt(expert, junk.Find("SM_WW2_WoodenChair_c1_LOD0"), new Vector3(12.4f, 0f, 11.6f), 150f);
            var lamps = Src("대기실_본관/조명");
            Lamp(expert, "ExpertRoom_Lamp", lamps.GetChild(0), lamps.GetChild(1), new Vector2(14f, 10.2f), 4.1f, l => { l.intensity = 4f; l.range = 7f; });
        }

        // ── 5단계: 조명·안개 (톤앤매너 4.2 라이팅·색감) ─────────────────────────
        // 배경 = 차가운 청록(앰비언트·채움광), 시선 = 따뜻한 전구색 하나로 통일(+텅스텐 보조 1색), 강조 = 기능 지점 스포트라이트.
        // 붉은 문틈 빛·천창 달빛은 의도된 대비색이라 유지. 대기실은 거점이라 안개를 덜고, 폐기물 보관소(뒤편)는 조금 더 짙게.

        static readonly Color WarmBulb = new Color32(0xFF, 0xA6, 0x5A, 0xFF);
        static readonly Color Tungsten = new Color32(0xFF, 0xC2, 0x7A, 0xFF);
        static readonly Color CoolFill = new Color32(0x4E, 0x7F, 0x96, 0xFF);
        static readonly Color SpotWarm = new Color32(0xFF, 0xD9, 0xA8, 0xFF);

        static void BuildLightingPass(Transform mood)
        {
            // 앰비언트: 위는 청록, 바닥은 어두운 갈색 — 전구가 닿지 않는 곳도 형태는 읽히게
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.11f, 0.18f, 0.21f);
            RenderSettings.ambientEquatorColor = new Color(0.10f, 0.12f, 0.14f);
            RenderSettings.ambientGroundColor = new Color(0.06f, 0.05f, 0.045f);

            // 색 통일: 주황 계열 = 전구색, 줄전구·분장 거울 = 텅스텐, 푸른 채움광 = 청록
            foreach (var l in mood.GetComponentsInChildren<Light>(true))
            {
                if (l.name.Contains("RedSpill") || l.name.Contains("Skylight") || l.type == LightType.Directional) continue;
                Color.RGBToHSV(l.color, out float h, out float s, out _);
                if (s > 0.25f && h > 0.02f && h < 0.15f) l.color = l.name.Contains("StringGlow") || l.name.Contains("Vanity") ? Tungsten : WarmBulb;
                else if (h > 0.5f && h < 0.72f && l.intensity < 1.2f) l.color = CoolFill; // 은은한 채움광만(화장실 냉백색 전구 같은 주 조명은 유지)
            }

            // 프로토타입 FlickerLight(매 프레임 무작위 꺼짐·켜짐 → 번쩍이며 깨져 보임)를 잔열 있는 깜빡임으로 교체
            foreach (var old in mood.GetComponentsInChildren<FlickerLight>(true))
            {
                var bulbs = old.bulbs;
                float gap = old.burstsPerSecond > 0.25f ? 5f : 8f; // 터널·옆방은 조금 더 자주
                var go = old.gameObject;
                Object.DestroyImmediate(old);
                var flicker = go.GetComponent<WaitingRoomFlickerLight>() ?? go.AddComponent<WaitingRoomFlickerLight>();
                flicker.bulbs = bulbs;
                flicker.averageGap = gap;
            }

            // 공간별 차가운 채움광(전구가 없는 벽면·구석이 검게 뭉개지지 않게)
            var lights = Group(mood, "LightingPass");
            foreach (var (n, at, range) in new[]
            {
                ("Dormitory", new Vector3(-3.5f, 6f, 15f), 14f), ("PropRoom", new Vector3(0f, 6f, -14.75f), 16f),
            })
                PointLight(lights, "CoolFill_" + n, at, CoolFill, 0.6f, range);

            // 기능 지점 스포트라이트(따뜻한 빛 한 줄기 — 공동 테스트 "기능에 대한 피드백" 항목)
            var gather = WaitingRoomTunnelLayout.Gather;
            foreach (var (n, from, to, intensity, angle) in new[]
            {
                ("Spot_TentPile", new Vector3(0f, 8.3f, -2.5f), WaitingRoomGreyboxBuilder.TentCenter + Vector3.up * 1f, 9f, 38f),   // 링 스포트라이트 모티프
                ("Spot_ShopPhone", new Vector3(6.3f, 4.6f, -4.8f), new Vector3(7.5f, 1.2f, -6.8f), 9f, 30f),
                ("Spot_ScheduleBoard", new Vector3(-7f, 4.6f, 6.2f), new Vector3(-7f, 1.9f, 8.8f), 7f, 38f),
                ("Spot_InfoBoard", new Vector3(6.2f, 4.6f, 3.2f), new Vector3(8.4f, 1.2f, 3.2f), 6f, 34f),
                ("Spot_HandoverShelf", new Vector3(-1f, 4.6f, -17.2f), new Vector3(-1f, 1.2f, -20f), 7f, 40f),
                ("Spot_PerformanceLog", new Vector3(-16.6f, 4.4f, 5.6f), new Vector3(-18.5f, 0.8f, 7f), 6.5f, 30f),
                ("Spot_StageCurtain", new Vector3(gather.xMax - 2.5f, 6.2f, 0f), new Vector3(WaitingRoomTunnelLayout.TunnelEnd - 0.4f, 2f, 0f), 8f, 42f),
            })
            {
                var go = new GameObject(n);
                go.transform.SetParent(lights, false);
                go.transform.localPosition = from;
                go.transform.localRotation = Quaternion.LookRotation(to - from);
                var spot = go.AddComponent<Light>();
                spot.type = LightType.Spot;
                spot.color = SpotWarm;
                spot.intensity = intensity;
                spot.range = Vector3.Distance(from, to) * 1.8f;
                spot.spotAngle = angle;
                spot.innerSpotAngle = angle * 0.55f;
                spot.shadows = LightShadows.None;
            }

            // 안개: 로비는 스폰 화면이 뿌옇지 않게 덜고, 폐기물 보관소는 조금 더
            ScaleFog(mood, "대기실_바닥안개", 0.6f, 1f);
            ScaleFog(mood, "대기실_연무", 0.6f, 0.6f);
            ScaleFog(mood, "백스테이지2_바닥안개", 1.6f, 1f);
        }

        static void PointLight(Transform parent, string name, Vector3 at, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
        }

        static void ScaleFog(Transform mood, string name, float amount, float alpha)
        {
            var ps = mood.GetComponentsInChildren<ParticleSystem>(true).FirstOrDefault(p => p.name == name);
            if (ps == null) return;
            var main = ps.main;
            main.maxParticles = Mathf.Max(10, Mathf.RoundToInt(main.maxParticles * amount));
            var color = main.startColor.color;
            main.startColor = new Color(color.r, color.g, color.b, color.a * alpha);
            var emission = ps.emission;
            var rate = emission.rateOverTime;
            rate.constantMax *= amount;
            rate.constantMin *= amount;
            emission.rateOverTime = rate;
        }

        // ── 4단계 디벨롭: 프로토타입 공간 구성 채우기 ─────────────────────────
        // 프로토타입에서 우리 방에 대응하는 구역을 같은 벽 기준으로 겹쳐 놓고, 그 안의 배치를 통째로 옮긴다.
        // 물건마다 ① 방 밖 ② 그레이박스 오브젝트·이미 놓은 장식과 겹침 ③ 출입구 앞·스폰 줄 ④ 6.5m 위(천장부)이면 뺀다.

        static readonly (Vector2 at, Vector2 size)[] Clearances =
        {
            (new Vector2(-10f, 0f), new Vector2(4.4f, 3.6f)),     // 로비 ↔ ⑤ 문
            (new Vector2(10f, 0f), new Vector2(4.4f, 3.6f)),      // 대기실 문(⑥)
            (new Vector2(10f, 6.5f), new Vector2(4.4f, 2.6f)),    // ⑧ 문
            (new Vector2(-4f, 9f), new Vector2(3.6f, 4.4f)),      // 로비 ↔ ② 문
            (new Vector2(-2f, -9f), new Vector2(3.6f, 4.4f)),     // 로비 ↔ ④ 문
            (new Vector2(3f, 11f), new Vector2(3.0f, 2.2f)),      // 숙소 ↔ 화장실 문
            (new Vector2(0f, -3.5f), new Vector2(9f, 2.6f)),      // 스폰 줄
            (new Vector2(7.2f, -5.6f), new Vector2(3.2f, 2.6f)),  // ③ 상점 수화기·배송구 앞
        };

        static void BuildProtoFills(Transform p)
        {
            var avoid = AvoidBounds();
            int count = 0;
            // ① 로비: 본관 남쪽 벽 줄(사물함·의상걸이·탈·벤치) + 「대기실」 간판 + 흩어진 잡동사니
            var hall = Src("대기실_본관");
            var lobbyOffset = new Vector3(-143.5f, 0f, -11.05f);
            count += Fill(Group(p, "Lobby"), Children(hall.Find("사물함_의상")).Concat(Children(hall.Find("잡동사니"))).Append(hall.Find("Sign_대기실")), lobbyOffset, Lobby, avoid);
            // ② 숙소: 프로토타입 칸 안 생활 소품(2층 침대 제외 — 그레이박스 침대는 이후 에셋 교체 대상)
            var dormProps = new[] { "숙소/숙소_01/가구", "숙소/숙소_02/가구" }.SelectMany(path => Children(hall.Find(path))).Where(t => !t.name.StartsWith("철제2층침대"));
            // 프로토타입 칸 깊이 5.1m > 우리 3.4m → 뒷벽(z 16 → 21) 기준으로 맞추고, 철창·침대와 겹치는 것은 자동으로 빠진다
            count += Fill(Group(p, "Dormitory"), dormProps, new Vector3(-130f, 0f, 5f), Dormitory, avoid);
            // ④ 소품실은 자동 채우기 대신 BuildPropRoom(가구 자리 실물 + 벽을 따라 무더기) — 자동 채우기는 칸막이·밧줄 걸이가 방 가운데 떠서 부자연스러웠다
            BuildPropRoom(Group(p, "PropRoom"));
            // ⑤ 폐기물 보관소: 백스테이지2 남서쪽 모서리(벽을 따라 쌓인 먼지·드럼통·물웅덩이) — 같은 두 벽에 맞춤
            var junkGroup = Group(p, "JunkStorage");
            BuildJunkStandIns(junkGroup);
            avoid = AvoidBounds();
            var debris = Src("백스테이지2/잔해");
            var junkItems = debris.Cast<Transform>().Where(t => t.name != "FadedBanner" && !t.name.StartsWith("Text_") && t.name != "OldMattress" && t.name != "Link");
            count += Fill(junkGroup, junkItems, new Vector3(-101.85f, 0f, -0.65f), JunkStorage, avoid);
            Debug.Log($"[WaitingRoomMood] 프로토타입 공간 구성 채우기: {count}개 배치");
        }

        // ── 실물 대역: 그레이박스 가구 자리에 프로토타입 실물을 세우고 그레이박스 상자는 렌더러만 끈다(충돌체·위치·표지 유지) ──
        // 나중에 실제 에셋을 적용할 때는 이 목록의 렌더러를 다시 켜고 교체한다. 분위기 입히기를 다시 실행하면 먼저 모두 켰다가 다시 끈다.
        static readonly string[] StandInTargets =
        {
            "S04_PropRoom/PropShelf_A", "S04_PropRoom/PropShelf_B", "S04_PropRoom/DressingTable", "S04_PropRoom/DressingMirror",
            "S05_JunkStorage/BrokenProps", "S05_JunkStorage/StorageShelf",
        };

        static void ShowStandInTargets(bool show)
        {
            var targets = StandInTargets.Select(path =>
            {
                var t = greybox.Find(path);
                if (t == null) Debug.LogWarning($"[WaitingRoomMood] 실물 대역 대상 없음: {path}");
                return t;
            }).Concat(CurtainBoxes()).Where(t => t != null);
            foreach (var t in targets)
                foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.GetComponent<TextMesh>() != null) continue;
                    Undo.RecordObject(r, "Waiting Room Mood");
                    r.enabled = show;
                }
        }

        /// <summary>천 커튼으로 대신하는 그레이박스 커튼 도형(⑥ 커튼 방 8곳의 주름·묶음·나무 밸런스, ⑦ 대형 커튼, ⑧ 전문가 대기실 커튼).</summary>
        static IEnumerable<Transform> CurtainBoxes()
        {
            var rooms = greybox.Find("S06_StageCorridor_Temp/CurtainRooms");
            if (rooms != null)
                foreach (Transform room in rooms)
                    foreach (Transform part in room)
                        if (part.name == "Valance" || part.name.StartsWith("Fold_") || part.name.StartsWith("Bunch_")) yield return part;
            var stage = greybox.Find("S07_StageEntrance_Temp");
            if (stage != null)
                foreach (Transform part in stage)
                    if (part.name.StartsWith("CurtainFold_") || part.name == "Valance") yield return part;
            var expert = greybox.Find("S08_ExpertRoom/ExpertRoomCurtain");
            if (expert != null) yield return expert;
        }

        // ── 붉은 커튼: 프로토타입 천 커튼(CurtainController + Cloth 패널, 벨벳 질감·바람에 흔들림·캐릭터가 밀면 젖혀짐) ──

        static void BuildCurtains(Transform p)
        {
            var hallCurtains = Src("대기실_본관/입구커튼");
            var tunnelCurtains = Src("무대진입_터널/커튼");
            if (hallCurtains == null || tunnelCurtains == null) return;
            // 출입구 커튼 한 벌(3.2 × 2.84m): 레일·밸런스·술 + 천 커튼 — 그레이박스 커튼 방 입구(폭 3.2m)와 같다
            var doorway = hallCurtains.Cast<Transform>().Take(3).Append(hallCurtains.Find("소품실_커튼")).ToArray();

            // ⑥ 커튼 방 8곳: 열림 = 양옆으로 묶음, 닫힘 = 내려 둠(통과 막는 충돌체는 그레이박스 것을 그대로 씀)
            // 열린 커튼 뒤 어두운 방 판: V4_Black은 매끄러움 0.5라 주변 반사로 희게 떠 보여 반사 없는 검정으로
            var rooms = greybox.Find("S06_StageCorridor_Temp/CurtainRooms");
            var voidMaterial = VoidMaterial();
            foreach (var dark in rooms.GetComponentsInChildren<Transform>(true).Where(t => t.name == "DarkRoom"))
                if (dark.TryGetComponent(out MeshRenderer darkRenderer)) { Undo.RecordObject(darkRenderer, "Waiting Room Mood"); darkRenderer.sharedMaterial = voidMaterial; }
            foreach (Transform room in rooms)
            {
                var valance = room.Find("Valance");
                if (valance == null) continue;
                var at = greybox.InverseTransformPoint(valance.position);
                int side = at.z > 0f ? 1 : -1;
                var kit = Place(p, "Curtain_" + room.name, doorway, new Vector3(at.x, float.NaN, side * 3.72f), yaw: side > 0 ? 180f : 0f);
                SetCurtain(kit, room.name.StartsWith("CurtainRoom_Open_") ? 0.9f : 0f);
            }

            // ⑦ 공연 스테이지 입구: 프로토타입 무대 커튼(5.1 × 4.38m)을 현재 입구(7.6 × 4.6m)에 맞춰 늘림
            var stage = tunnelCurtains.Find("무대_커튼");
            var stageKit = Place(p, "Curtain_StageEntrance", new[] { stage }, new Vector3(WaitingRoomTunnelLayout.TunnelEnd - 0.4f, 0f, 0f), scale: new Vector3(1f, 4.6f / 4.38f, 7.6f / 5.1f));
            SnapBottom(stageKit, 0f);
            SetCurtain(stageKit, 0f);

            // ⑧ 전문가 대기실 문(폭 2m): 천 커튼만. 기능 스크립트가 미는 그레이박스 조각을 따라 열리고 닫힌다
            var expert = greybox.Find("S08_ExpertRoom/ExpertRoomCurtain");
            if (expert != null)
            {
                var at = greybox.InverseTransformPoint(expert.position);
                var expertKit = Place(p, "Curtain_ExpertRoom", new[] { hallCurtains.Find("분장실_커튼") }, new Vector3(at.x, float.NaN, at.z), yaw: 90f, scale: new Vector3(2f / 3.2f, 3f / 2.84f, 1f));
                SnapBottom(expertKit, 0f);
                SetCurtain(expertKit, 0f);
                var controller = expertKit.GetComponentInChildren<CurtainController>(true);
                if (controller != null)
                {
                    var follow = controller.gameObject.AddComponent<WaitingRoomCurtainFollow>();
                    follow.curtain = controller;
                    follow.watch = expert.Find("Curtain_L");
                }
            }
        }

        const string VoidMaterialPath = "Assets/Scenes/Tests/WaitingRoom/Base/Materials/WR_Void.mat";

        /// <summary>빛·반사를 거의 받지 않는 검정(커튼 뒤 깊이감용). 없으면 만든다.</summary>
        static Material VoidMaterial()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(VoidMaterialPath);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "WR_Void" };
            m.SetColor("_BaseColor", new Color(0.01f, 0.01f, 0.012f));
            m.SetFloat("_Smoothness", 0f);
            m.SetFloat("_EnvironmentReflections", 0f);
            m.SetFloat("_SpecularHighlights", 0f);
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            AssetDatabase.CreateAsset(m, VoidMaterialPath);
            return m;
        }

        /// <summary>커튼이 쉬는 자리(0 = 닫힘, 1 = 활짝)를 정하고 에디터에서도 그 모양으로 보이게 한다.</summary>
        static void SetCurtain(Transform kit, float rest)
        {
            foreach (var c in kit.GetComponentsInChildren<CurtainController>(true))
            {
                c.initialOpen = rest;
                c.openOnStart = false;
                foreach (var panel in c.panels)
                    if (panel != null) panel.SetOpen(rest);
            }
        }

        /// <summary>프로토타입 선반 하나와 그 위에 얹힌 물건(바닥 투영이 선반 안)을 한 묶음으로.</summary>
        static Transform[] ShelfKit(Transform shelf, IEnumerable<Transform> items)
        {
            var sb = Bounds(shelf);
            return new[] { shelf }.Concat(items.Where(i =>
            {
                var c = Bounds(i).center;
                return c.x > sb.min.x - 0.05f && c.x < sb.max.x + 0.05f && c.z > sb.min.z - 0.05f && c.z < sb.max.z + 0.05f;
            })).ToArray();
        }

        static Bounds Bounds(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>
        /// ④ 소품실(컨셉아트: 폐공장 창고를 급조한 서커스 소품·분장실). 문(x -2)에서 남쪽 벽 물려줄 선반까지 가운데 길(x -4~1)은 비운다.
        /// 서쪽 = 소품 창고: 북·서쪽 벽 철제 선반(소품 선반 A·B 자리), 공구 작업대, 남서쪽 구석에 북·큰 공·후프·죽마 무더기.
        /// 동쪽 = 분장 구역: 동쪽 벽 전구 거울 화장대(분장대 자리), 북쪽 벽 의상걸이 2, 병풍·의자, 남쪽 벽 인형 위에 탈.
        /// </summary>
        static void BuildPropRoom(Transform p)
        {
            var props = Src("소품실");
            var shelves = Children(props.Find("가구")).Where(t => t.name == "선반").ToArray();
            var items = Children(props.Find("소품"));
            var wide = shelves.Where(s => Bounds(s).size.x > Bounds(s).size.z).ToArray();  // 2.4 × 0.6
            var deep = shelves.Where(s => Bounds(s).size.x <= Bounds(s).size.z).ToArray(); // 0.6 × 2.4

            // 소품 선반 B(북쪽 벽 x -8.3~-4.3) · A(서쪽 벽 z -14.2~-10.2)
            PlaceKit(p, "PropShelf_B_Kit0", ShelfKit(wide[0], items), new Vector3(-7.5f, 0f, -9.5f), 0f);
            PlaceKit(p, "PropShelf_B_Kit1", ShelfKit(wide[1], items), new Vector3(-5.1f, 0f, -9.5f), 0f);
            PlaceKit(p, "PropShelf_A_Kit0", ShelfKit(deep[0], items), new Vector3(-9.55f, 0f, -11.1f), 0f);
            PlaceKit(p, "PropShelf_A_Kit1", ShelfKit(deep[1 % deep.Length], items), new Vector3(-9.55f, 0f, -13.5f), 0f);

            // 공구 작업대(공구가 얹힌 채)·의자·공구 카트
            var furniture = props.Find("가구");
            var table = First(furniture, "SM_WW2_Table02");
            PlaceKit(p, "Workbench", ShelfKit(table, items), new Vector3(-6.7f, 0f, -13.0f), 90f);
            CopyAt(p, First(furniture, "SM_WW2_WoodenChair"), new Vector3(-5.75f, 0f, -12.4f), -100f);
            CopyAt(p, First(furniture, "SM_ToolCart"), new Vector3(-7.4f, 0f, -15.3f), 15f);

            // 남서쪽 구석(의상 보관함과 물려줄 선반 사이): 큰 서커스 기물 무더기 — 남쪽 벽에 기대 둔다
            var loose = items.Where(i => i.name is "북" or "BigBall" or "Hoop" or "Stilt" or "SM_Woodpallet_1" or "SM_Barrel" || i.name == "트렁크" && Bounds(i).size.x > 0.9f).ToArray();
            CopyAt(p, loose.FirstOrDefault(i => i.name == "SM_Woodpallet_1"), new Vector3(-6.3f, 0f, -19.75f), 0f);
            CopyAt(p, loose.FirstOrDefault(i => i.name == "북"), new Vector3(-6.9f, 0.28f, -19.7f), 20f);   // 팔레트 위
            CopyAt(p, loose.FirstOrDefault(i => i.name == "BigBall"), new Vector3(-5.6f, 0.28f, -19.85f), 0f);
            CopyAt(p, loose.FirstOrDefault(i => i.name == "트렁크"), new Vector3(-4.75f, 0f, -18.6f), -12f);
            CopyAt(p, loose.FirstOrDefault(i => i.name == "SM_Barrel"), new Vector3(-7.7f, 0f, -18.6f), 0f);
            int k = 0;
            foreach (var hoop in loose.Where(i => i.name == "Hoop"))
                CopyAt(p, hoop, new Vector3(-8.0f + 0.12f * k, 0f, -20.15f + 0.05f * k), 90f + 6f * k++);   // 벽에 기댄 후프
            k = 0;
            foreach (var stilt in loose.Where(i => i.name == "Stilt"))
                CopyAt(p, stilt, new Vector3(-4.6f + 0.25f * k++, 0f, -20.15f), 0f);                         // 벽에 세운 죽마

            // 동쪽 벽: 전구 거울 화장대(분장대·거울 자리). 북쪽 벽: 의상걸이 2개(문 동쪽). 병풍으로 옷 갈아입는 구석
            var dressing = Src("분장실");
            var vanity = dressing.Find("화장대");
            var v = Place(p, "Vanity", new[] { vanity }, new Vector3(0f, float.NaN, 0f), yaw: 270f);
            FitInto(v, new Vector2(9.85f, -14.8f), alongZ: true);
            var racks = dressing.Cast<Transform>().Where(t => t.name == "의상걸이").ToArray();
            for (int i = 0; i < racks.Length; i++)
                Place(p, "CostumeRack_" + i, new[] { racks[i] }, new Vector3(2.6f + 4f * i, float.NaN, -9.75f), yaw: 90f);
            Place(p, "FoldingScreen", new[] { dressing.Find("칸막이병풍") }, new Vector3(2.3f, float.NaN, -12.6f), yaw: 75f); // 기준점이 도형 밖이라 묶음 중심으로
            CopyAt(p, dressing.Find("SM_WW2_WoodenChair_c1_LOD0"), new Vector3(3.3f, 0f, -13.4f), 200f);
            var trunk = CopyAt(p, dressing.Find("트렁크"), new Vector3(6.6f, 0f, -19.9f), 0f);
            if (trunk != null) CopyAt(p, dressing.Find("SM_MET_TangledCloth02_c1_LOD1"), new Vector3(6.6f, 0.55f, -19.8f), 30f);

            // 남쪽 벽 낡은 인형·인형탈 위에 탈 4개를 걸어 표지와 맞춘다
            var masks = dressing.Cast<Transform>().Where(t => t.name.StartsWith("탈_")).ToArray();
            for (int i = 0; i < masks.Length; i++)
                CopyAt(p, masks[i], new Vector3(2.6f + 0.95f * i, 2.25f, -20.2f), -90f);

            // 폐공장 흔적: 선반 위 벽에 서커스 포스터, 벽 아래 곰팡이 얼룩, 바닥 물웅덩이
            var traces = Src("무대진입_터널/서커스_흔적");
            Place(p, "Poster_North", new[] { First(traces, "Poster") }, new Vector3(-6.3f, 3.4f, -9.17f), yaw: 0f);
            Place(p, "Poster_West", new[] { First(traces, "PosterTorn") }, new Vector3(-9.83f, 3.1f, -17.6f), yaw: 90f);
            WallMark(p, "Grime_South", new Vector3(-6.4f, 0.75f, -20.33f), 180f, poster: false);
            WallMark(p, "Grime_North", new Vector3(4.6f, 0.75f, -9.17f), 0f, poster: false);
            var floor = Children(Src("대기실_본관/바닥_연출")).Where(t => t.name == "Puddle").ToArray();
            if (floor.Length >= 2)
            {
                Place(p, "Puddle_0", new[] { floor[0] }, new Vector3(-2.6f, float.NaN, -15.5f));
                Place(p, "Puddle_1", new[] { floor[1] }, new Vector3(5.2f, float.NaN, -16.8f));
            }
        }

        /// <summary>벽면 흔적(곰팡이 얼룩 + 찢긴 포스터). at = 벽 안쪽 면 위 중심, yaw 0 = 북쪽 벽(남쪽에서 봄), 90 = 서쪽 벽, 180 = 남쪽 벽, 270 = 동쪽 벽.</summary>
        static void WallMark(Transform parent, string name, Vector3 at, float yaw, bool poster = true)
        {
            var grime = Children(Src("구조물/곰팡이_얼룩")).FirstOrDefault(t => t.name == "Grime" && Bounds(t).size.x > 2f && Bounds(t).size.x < 4f);
            var g = Group(parent, name);
            Place(g, "Grime", new[] { grime }, at, yaw: yaw);
            if (poster) Place(g, "PosterTorn", new[] { First(Src("무대진입_터널/서커스_흔적"), "PosterTorn", 1) }, at + Vector3.up * 0.55f, yaw: yaw);
        }

        /// <summary>⑤ 사용하지 않는 기물 더미(남쪽 벽)·정리된 보관 선반(서쪽 벽) 자리에 실물.</summary>
        static void BuildJunkStandIns(Transform p)
        {
            var junk = Src("대기실_본관/잡동사니");
            var debris = Src("백스테이지2/잔해");
            var items = Children(Src("소품실/소품"));
            // 기물 더미(x -16.65~-14.55, z -8.8~-6.7): 트렁크 위 상자, 북, 통, 기대 세운 후프
            CopyAt(p, First(debris, "트렁크"), new Vector3(-16.0f, 0f, -8.15f), 8f);
            CopyAt(p, First(junk, "SM_MET_WoodenCrate01a"), new Vector3(-16.0f, 0.7f, -8.1f), -14f);
            CopyAt(p, First(junk, "북"), new Vector3(-14.95f, 0f, -7.45f), 30f);
            CopyAt(p, First(debris, "SM_Barrel_1"), new Vector3(-15.1f, 0f, -8.45f), 50f);
            CopyAt(p, First(junk, "SM_PAD_CardboardBox03"), new Vector3(-16.2f, 0f, -7.15f), 25f);
            CopyAt(p, items.FirstOrDefault(i => i.name == "Hoop"), new Vector3(-14.55f, 0f, -8.75f), 0f);
            CopyAt(p, items.FirstOrDefault(i => i.name == "Stilt"), new Vector3(-16.6f, 0f, -8.75f), 0f);

            // 정리된 보관 선반(서쪽 벽 x -21.4, z -5.5~-1.5): 선반 2칸, 벽과 나란히
            var shelves = Children(Src("소품실/가구")).Where(t => t.name == "선반" && Bounds(t).size.x > Bounds(t).size.z).ToArray();
            PlaceKit(p, "StorageShelf_Kit0", ShelfKit(shelves[^1], items), new Vector3(-21.5f, 0f, -2.3f), 90f);
            PlaceKit(p, "StorageShelf_Kit1", ShelfKit(shelves[0], items), new Vector3(-21.5f, 0f, -4.7f), 90f);
        }

        /// <summary>묶음의 바닥 중심(첫 원본 = 받침 기준)을 그레이박스 로컬 (x, y, z)에 두고 yaw만큼 돌린다.</summary>
        static Transform PlaceKit(Transform parent, string name, Transform[] sources, Vector3 target, float yaw)
        {
            var b = Bounds(sources[0]);
            var g = new GameObject(name).transform;
            g.SetParent(parent, false);
            g.position = new Vector3(b.center.x, b.min.y, b.center.z);
            foreach (var copy in Duplicate(sources)) copy.transform.SetParent(g, true);
            g.localPosition = target;
            g.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return g;
        }

        /// <summary>묶음을 벽(x = wall.x)에 등지게 붙이고 벽을 따라 가운데(z = wall.y)에 맞춘다.</summary>
        static void FitInto(Transform group, Vector2 wall, bool alongZ)
        {
            var rs = group.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
            if (rs.Length == 0) return;
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            var min = greybox.InverseTransformPoint(b.min);
            var max = greybox.InverseTransformPoint(b.max);
            var shift = alongZ
                ? new Vector3(wall.x - (wall.x > 0 ? max.x : min.x), 0f, wall.y - (min.z + max.z) / 2f)
                : new Vector3(wall.y - (min.x + max.x) / 2f, 0f, wall.x - (wall.x > 0 ? max.z : min.z));
            group.position += greybox.TransformVector(shift);
        }

        /// <summary>겹치면 안 되는 범위(그레이박스 오브젝트 + 이미 놓은 바닥 장식). 바닥·벽·천장·글씨·천장부 장식은 제외.</summary>
        static List<Bounds> AvoidBounds()
        {
            var list = new List<Bounds>();
            var mood = greybox.Find(MoodName);
            foreach (var r in greybox.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r.GetComponent<TextMesh>() != null) continue;
                var t = r.transform;
                string path = PathOf(t);
                if (path.StartsWith("Walls/") || t.name.StartsWith("Floor") || path.Contains("RoomFloors") || path.Contains("GatherZone")) continue;
                if (path.StartsWith(MoodName + "/Ceilings") || path.StartsWith(MoodName + "/Ceilingworks") || path.StartsWith(MoodName + "/Fog") ||
                    path.StartsWith(MoodName + "/Lights") || path.StartsWith(MoodName + "/Tunnel") || path.StartsWith(MoodName + "/Curtains")) continue;
                if (path.StartsWith(MoodName + "/Dressing/Lobby_FloorStains") || r.bounds.size.y < 0.05f) continue; // 바닥 얼룩은 겹쳐도 됨
                list.Add(LocalBounds(r.bounds));
            }
            return list;
        }

        static Bounds LocalBounds(Bounds world) => new(greybox.InverseTransformPoint(world.center), world.size);

        static int Fill(Transform parent, IEnumerable<Transform> sources, Vector3 offset, Rect room, List<Bounds> avoid)
        {
            int n = 0;
            // 벽 중심선 기준 범위 — 벽 안쪽 면(0.15)에 붙은 물건은 살리고 벽을 넘는 것만 뺀다
            var inner = Rect.MinMaxRect(room.xMin + 0.05f, room.yMin + 0.05f, room.xMax - 0.05f, room.yMax - 0.05f);
            var items = sources.Where(s => s != null && s.name != "조명" && !s.name.StartsWith("Lamp") && !s.name.StartsWith("CagedLamp"))
                .Select(s => (src: s, rs: s.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray()))
                .Where(x => x.rs.Length > 0)
                .Select(x => { var wb = x.rs[0].bounds; foreach (var r in x.rs) wb.Encapsulate(r.bounds); return (x.src, b: new Bounds(wb.center + offset, wb.size)); }) // 프로토타입 월드 + 이동량 = 그레이박스 로컬
                .OrderBy(x => x.b.min.y)                                                                     // 받침이 먼저 판단되게
                .ToArray();
            var dropped = new List<Bounds>();
            foreach (var (src, b) in items)
            {
                bool flat = b.size.y < 0.05f;
                bool keep = !(b.max.y > 6.5f && b.min.y > 4f)                                               // 천장부는 3단계 몫
                    && b.min.x >= inner.xMin && b.max.x <= inner.xMax && b.min.z >= inner.yMin && b.max.z <= inner.yMax
                    && (flat || !Clearances.Any(c => Overlaps(b, c.at, c.size)))
                    && (flat || !avoid.Any(a => a.Intersects(Shrink(b, 0.05f))))
                    // 받침(선반·상자)이 빠졌으면 그 위에 얹힌 것도 뺀다 — 공중에 뜨지 않게
                    && !dropped.Any(d => b.min.y > d.min.y + 0.1f && b.min.x < d.max.x && b.max.x > d.min.x && b.min.z < d.max.z && b.max.z > d.min.z);
                if (!keep) { if (!flat) dropped.Add(b); continue; }
                var copy = Duplicate(new[] { src })[0].transform;
                copy.SetParent(parent, true);
                copy.position = greybox.TransformPoint(src.position + offset);
                n++;
            }
            return n;
        }

        static bool Overlaps(Bounds b, Vector2 at, Vector2 size) =>
            b.min.x < at.x + size.x / 2f && b.max.x > at.x - size.x / 2f && b.min.z < at.y + size.y / 2f && b.max.z > at.y - size.y / 2f;

        static Bounds Shrink(Bounds b, float by) => new(b.center, Vector3.Max(Vector3.zero, b.size - Vector3.one * (2f * by)));

        /// <summary>묶음의 위끝이 주어진 높이(그레이박스 로컬)에 닿게 올리거나 내린다.</summary>
        static void HangFrom(Transform group, float localTop)
        {
            var rs = group.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return;
            float top = rs.Max(r => r.bounds.max.y);
            group.position += Vector3.up * (greybox.TransformPoint(new Vector3(0f, localTop, 0f)).y - top);
        }

        // ── 3단계: 천장부(철골 트러스·배관·쇠사슬·천창·깜빡이는 전구) ─────────────
        // 10m로 높아진 방의 위쪽(6m 이상)을 컨셉아트의 공장 홀처럼 채운다. 3인칭 카메라(최대 ~3.8m)·기능 오브젝트와 겹치지 않는 높이만 쓴다.
        // ⑥ 통로는 터널 볼트가 따로 덮는다.

        const float TrussBottom = 8.4f, TrussDepth = 1.2f;
        const float PipeY = 7.6f, SmallPipeY = 6.9f;

        static void BuildCeilingworks(Transform p)
        {
            var iron = ProtoMaterial("V4_Iron_Dark");

            // 철골 트러스: (시작 x,z) → (끝 x,z). 벽 안쪽 0.2m에서 끊는다
            var trusses = Group(p, "Trusses");
            Truss(trusses, new Vector2(-9.8f, -4.5f), new Vector2(9.8f, -4.5f), iron);   // ① 로비 — 천창(z ±2.3)·천막 깃대 피함
            Truss(trusses, new Vector2(-9.8f, 4.5f), new Vector2(9.8f, 4.5f), iron);
            foreach (var x in new[] { -6f, 0f })                                          // ② 숙소
                Truss(trusses, new Vector2(x, 9.2f), new Vector2(x, 20.8f), iron);
            Truss(trusses, new Vector2(6.5f, 9.2f), new Vector2(6.5f, 20.8f), iron);      // ② 화장실
            foreach (var x in new[] { -6f, 0f, 6f })                                      // ④ 소품실
                Truss(trusses, new Vector2(x, -20.3f), new Vector2(x, -9.2f), iron);
            foreach (var z in new[] { -6.5f, 0f, 6.5f })                                  // ⑤ 폐기물 보관소(전구 전선 z ±5 피함)
                Truss(trusses, new Vector2(-21.8f, z), new Vector2(-10.2f, z), iron);
            Truss(trusses, new Vector2(10.2f, 8.5f), new Vector2(17.8f, 8.5f), iron);     // ⑧ 전문가 대기실

            // 벽을 따라가는 굵은 배관(프로토타입 12m 배관을 벽 길이에 맞춰 나눔) + 아래 가는 배관
            var pipes = Group(p, "Pipes");
            var hallPipes = Src("대기실_본관/배관");
            var big = First(hallPipes, "SM_BigPipe");
            var small = First(hallPipes, "SM_Smallpipe1");
            var riser = First(hallPipes, "SM_Pipe_Big_01");
            var valve = First(hallPipes, "SM_Pipe_Small_Valve_02");
            PipeRun(pipes, big, -10f, 10f, PipeY, -8.4f, false);       // ① 로비 남·북 벽
            PipeRun(pipes, big, -10f, 10f, PipeY, 8.4f, false);
            PipeRun(pipes, small, -10f, 10f, SmallPipeY, 8.65f, false);
            PipeRun(pipes, big, -10f, 10f, PipeY, 20.4f, false);       // ② 숙소·화장실 북벽
            PipeRun(pipes, small, -10f, 3f, SmallPipeY, 20.65f, false);
            PipeRun(pipes, big, -10f, 10f, PipeY, -19.9f, false);      // ④ 소품실 남벽
            PipeRun(pipes, small, -10f, 10f, SmallPipeY, -20.15f, false);
            PipeRun(pipes, big, -9f, 9f, PipeY, -21.4f, true);         // ⑤ 폐기물 보관소 서벽(Z 방향)
            PipeRun(pipes, small, -9f, 9f, SmallPipeY, -21.65f, true);
            // 로비 모서리 세로 배관(바닥 → 가로 배관 높이) + 밸브
            // 로비 서쪽 두 모서리(동쪽은 ⑧ 전문가 대기실 문·상점 기둥이 있어 비움). 원본 기준점이 한쪽으로 치우쳐 x=-8.95면 벽에 붙는다
            foreach (var (x, z) in new[] { (-8.95f, -8.3f), (-8.95f, 8.3f) })
            {
                var r = CopyAt(pipes, riser, new Vector3(x, 0f, z), 0f);
                if (r != null) { r.localScale = Vector3.Scale(r.localScale, new Vector3(1f, PipeY / 6.2f, 1f)); r.localPosition = new Vector3(x, 0f, z); SnapBottom(r, 0f); }
                CopyAt(pipes, valve, new Vector3(x, 1.5f, z + (z < 0 ? 0.7f : -0.7f)), z < 0 ? 0f : 180f);
            }
            // ④ 소품실 북동 모서리(의상걸이와 화장대 사이 빈 구석): 공장 시절 세로 배관
            var propRiser = CopyAt(pipes, riser, new Vector3(8.95f, 0f, -9.6f), 180f);
            if (propRiser != null) { propRiser.localScale = Vector3.Scale(propRiser.localScale, new Vector3(1f, PipeY / 6.2f, 1f)); SnapBottom(propRiser, 0f); }

            // 쇠사슬: 프로토타입 본관 쇠사슬을 천장에 매달아(위끝 = 천장) 방 안만 남긴다
            var chains = Src("대기실_본관/쇠사슬");
            var lobbyInner = Rect.MinMaxRect(Lobby.xMin + 0.4f, Lobby.yMin + 0.4f, Lobby.xMax - 0.4f, Lobby.yMax - 0.4f);
            var junkInner = Rect.MinMaxRect(JunkStorage.xMin + 0.4f, JunkStorage.yMin + 0.4f, JunkStorage.xMax - 0.4f, JunkStorage.yMax - 0.4f);
            CullOutside(HangFromCeiling(Place(p, "Chains_Lobby", new[] { chains }, new Vector3(0f, float.NaN, 6.2f))), lobbyInner);
            CullOutside(HangFromCeiling(Place(p, "Chains_JunkStorage", new[] { chains }, new Vector3(-16f, float.NaN, 0f), yaw: 90f)), junkInner);

            // 천창 추가: 숙소·폐기물 보관소(차가운 빛줄기)
            var hall = Src("대기실_본관/조명");
            Place(p, "Dormitory_Skylight", new[] { hall.Find("Skylight"), hall.Find("SkylightBeam") }, new Vector3(-3f, CeilingY - 0.03f, 15f), anchor: hall.Find("Skylight"));
            Place(p, "JunkStorage_Skylight", new[] { hall.Find("Skylight"), hall.Find("SkylightBeam") }, new Vector3(-16f, CeilingY - 0.03f, -2.5f), anchor: hall.Find("Skylight"));

            // 깜빡이는 전구: 프로토타입 FlickerLight를 일부 철망 전구에
            var lights = p.parent.Find("Lights");
            Flicker(lights, "Lobby_Lamp", 1);
            Flicker(lights, "Lobby_Lamp", 2);
            Flicker(lights, "Dormitory_Lamp", 1);
            Flicker(lights, "JunkStorage_Lamp", 0);
        }

        /// <summary>프랫 트러스: 위·아래 현재 + 수직재 1.5m 간격 + 대각재. 충돌체 없음.</summary>
        static void Truss(Transform parent, Vector2 from, Vector2 to, Material material)
        {
            var t = Group(parent, "Truss");
            var a = new Vector3(from.x, 0f, from.y);
            var b = new Vector3(to.x, 0f, to.y);
            var dir = (b - a).normalized;
            float len = Vector3.Distance(a, b);
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            var mid = (a + b) / 2f;
            const float bar = 0.16f;
            RibPartYaw(t, "TopChord", mid + Vector3.up * (TrussBottom + TrussDepth), new Vector3(bar, bar, len), yaw, 0f, material);
            RibPartYaw(t, "BottomChord", mid + Vector3.up * TrussBottom, new Vector3(bar, bar, len), yaw, 0f, material);
            int bays = Mathf.Max(2, Mathf.RoundToInt(len / 1.5f));
            float bay = len / bays;
            float diagLen = Mathf.Sqrt(bay * bay + TrussDepth * TrussDepth);
            float tilt = Mathf.Atan2(bay, TrussDepth) * Mathf.Rad2Deg;
            for (int i = 0; i <= bays; i++)
            {
                var at = a + dir * (bay * i);
                RibPartYaw(t, "Post", at + Vector3.up * (TrussBottom + TrussDepth / 2f), new Vector3(bar * 0.8f, TrussDepth, bar * 0.8f), yaw, 0f, material);
                if (i == bays) continue;
                var centre = at + dir * (bay / 2f) + Vector3.up * (TrussBottom + TrussDepth / 2f);
                RibPartYaw(t, "Diagonal", centre, new Vector3(bar * 0.7f, diagLen, bar * 0.7f), yaw, i % 2 == 0 ? tilt : -tilt, material);
            }
            // 천장에 매다는 짧은 걸이 두 개
            foreach (var f in new[] { 0.2f, 0.8f })
                RibPartYaw(t, "Hanger", a + dir * (len * f) + Vector3.up * ((TrussBottom + TrussDepth + CeilingY) / 2f), new Vector3(0.06f, CeilingY - TrussBottom - TrussDepth, 0.06f), yaw, 0f, material);
        }

        static void RibPartYaw(Transform parent, string name, Vector3 position, Vector3 size, float yaw, float pitch, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(pitch, 0f, 0f);
            go.transform.localScale = size;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // 천장 철골 그림자가 화면을 어지럽히지 않게
        }

        /// <summary>프로토타입의 X 방향 배관 한 토막을 벽 길이에 맞게 여러 개로 나눠 이어 붙인다(길이 방향만 늘이거나 줄임).</summary>
        static void PipeRun(Transform parent, Transform source, float from, float to, float y, float k, bool alongZ)
        {
            if (source == null) return;
            var rs = source.GetComponentsInChildren<Renderer>(true);
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            float pieceLen = b.size.x;
            float len = Mathf.Abs(to - from);
            int pieces = Mathf.Max(1, Mathf.CeilToInt(len / pieceLen - 0.15f));
            float scale = len / (pieces * pieceLen);
            for (int i = 0; i < pieces; i++)
            {
                float c = Mathf.Min(from, to) + (i + 0.5f) * len / pieces;
                var copy = CopyAt(parent, source, Vector3.zero, alongZ ? 90f : 0f);
                if (copy == null) continue;
                copy.localScale = Vector3.Scale(copy.localScale, new Vector3(scale, 1f, 1f));
                // 묶음 중심이 목표 위치에 오도록 맞춘다
                var cr = copy.GetComponentsInChildren<Renderer>(true);
                var cb = cr[0].bounds; foreach (var r in cr) cb.Encapsulate(r.bounds);
                var target = greybox.TransformPoint(alongZ ? new Vector3(k, y, c) : new Vector3(c, y, k));
                copy.position += target - cb.center;
            }
        }

        static void SnapBottom(Transform t, float localY)
        {
            var rs = t.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return;
            float bottom = rs.Min(r => r.bounds.min.y);
            t.position += Vector3.up * (greybox.TransformPoint(new Vector3(0f, localY, 0f)).y - bottom);
        }

        /// <summary>묶음의 위끝이 천장에 닿게 올린다.</summary>
        static Transform HangFromCeiling(Transform group)
        {
            var rs = group.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return group;
            float top = rs.Max(r => r.bounds.max.y);
            group.position += Vector3.up * (greybox.TransformPoint(new Vector3(0f, CeilingY - 0.05f, 0f)).y - top);
            return group;
        }

        static void Flicker(Transform lights, string groupName, int index)
        {
            if (lights == null) return;
            var groups = lights.Cast<Transform>().Where(t => t.name == groupName).ToArray();
            if (index >= groups.Length) return;
            var g = groups[index];
            var light = g.GetComponentInChildren<Light>(true);
            if (light == null) return;
            var flicker = light.gameObject.GetComponent<WaitingRoomFlickerLight>() ?? light.gameObject.AddComponent<WaitingRoomFlickerLight>();
            flicker.bulbs = g.GetComponentsInChildren<Renderer>(true).Where(r => r.name == "Bulb").ToArray();
            flicker.averageGap = 8f;
        }

        // ── ⑥ 무대 입구 통로 → 프로토타입식 터널 ───────────────────────
        // 기능(커튼 방 열림 3·닫힘 5, 집결 판정 구역, ⑦ 공연 입구, 대기실 문 셔터)은 그대로 두고 외형만 터널로:
        // 콘크리트 반원 볼트, 커튼 방 사이 강철 리브, 벽면 배관, 판자 길, 전구줄·케이블·매단 전구, 깃발·포스터, ⑦ 금색 프로시니엄.
        // 프로토타입 터널(폭 6m, 벽 z 6/12, x 157~198)을 통로(폭 8m, 벽 z -4/4, x 10~52 — WaitingRoomTunnelLayout)에 옮겨 쓴다.

        const float VaultSpring = 3.6f;           // 커튼 방 상단 장식(3.3m)·표지(3.45m) 위에서 아치 시작
        const float VaultRadius = 4f;             // 통로 반폭
        const float VaultApex = VaultSpring + VaultRadius;
        const float ProtoTunnelLift = VaultApex - 6.2f; // 프로토타입 아치 꼭대기(6.2m) 대비
        static readonly (float x0, float x1, int side)[] CurtainSpans = WaitingRoomTunnelLayout.CurtainSpans.ToArray();

        /// <summary>프로토타입처럼 3m 간격 강철 리브. 커튼·출입구 앞(양쪽 벽 어느 쪽이든)은 건너뛴다.</summary>
        static IEnumerable<float> RibXs()
        {
            for (float x = Corridor.xMin + 1f; x < Corridor.xMax - 0.5f; x += 3f)
                if (!CurtainSpans.Any(s => x > s.x0 - 0.2f && x < s.x1 + 0.2f)) yield return x;
        }

        /// <summary>그 위치의 천장 높이(통로 안이면 볼트 곡면, 터널 옆방은 낮은 천장).</summary>
        static float CeilingAt(Vector3 local)
        {
            var at = new Vector2(local.x, local.z);
            if (!Corridor.Contains(at)) return CeilingY;
            float z = Mathf.Clamp(local.z, -VaultRadius, VaultRadius);
            return VaultSpring + Mathf.Sqrt(VaultRadius * VaultRadius - z * z) - 0.05f;
        }

        static void BuildTunnel(Transform p)
        {
            var tunnel = Src("무대진입_터널");
            if (tunnel == null) return;

            // 바닥: 프로토타입 터널처럼 젖은 콘크리트(위에 판자 길)
            var floor = greybox.Find("S06_StageCorridor_Temp/Floor");
            if (floor != null && floor.TryGetComponent(out MeshRenderer floorRenderer))
            {
                Undo.RecordObject(floorRenderer, "Waiting Room Mood");
                floorRenderer.sharedMaterial = ProtoMaterial("V4_Floor_WetConcrete");
                ApplyTiledMesh(floorRenderer);
            }

            // 반원 볼트(안쪽 면)
            var vault = new GameObject("Vault");
            vault.transform.SetParent(p, false);
            vault.transform.localPosition = new Vector3(Corridor.center.x, VaultSpring, 0f);
            vault.AddComponent<MeshFilter>().sharedMesh = VaultMesh(Corridor.width, VaultRadius);
            var vaultRenderer = vault.AddComponent<MeshRenderer>();
            vaultRenderer.sharedMaterial = ProtoMaterial("V5_Vault_Concrete");
            vaultRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;

            // 강철 리브(기둥 + 아치 조각) — 프로토타입 리브와 같은 단면
            var ribs = Group(p, "SteelRibs");
            var rust = ProtoMaterial("V4_RustyMetal");
            foreach (var x in RibXs())
            {
                var rib = Group(ribs, "Rib");
                rib.localPosition = new Vector3(x, 0f, 0f);
                float inner = VaultRadius - 0.13f;
                foreach (var side in new[] { -1f, 1f })
                {
                    RibPart(rib, "Post", new Vector3(0f, VaultSpring / 2f, side * inner), new Vector3(0.24f, VaultSpring, 0.22f), 0f, rust);
                    RibPart(rib, "Foot", new Vector3(0f, 0.06f, side * inner), new Vector3(0.4f, 0.12f, 0.35f), 0f, rust);
                }
                const int arcs = 14;
                float chord = 2f * inner * Mathf.Sin(Mathf.PI / arcs / 2f) + 0.04f;
                for (int i = 0; i < arcs; i++)
                {
                    float a = Mathf.PI * (i + 0.5f) / arcs; // 0 = 남쪽 벽, π = 북쪽 벽
                    var pos = new Vector3(0f, VaultSpring + Mathf.Sin(a) * inner, -Mathf.Cos(a) * inner);
                    RibPart(rib, "Arc", pos, new Vector3(0.24f, 0.22f, chord), a * Mathf.Rad2Deg - 90f, rust);
                }
            }

            // 벽면 배관·포스터·깃발·통 등: 남·북 벽 기준으로 따로 옮기고, 커튼 방 입구를 가리는 것은 뺀다
            var wall = Group(p, "WallDressing");
            CopyTunnelSide(wall, tunnel.Find("배관"));
            CopyTunnelSide(wall, tunnel.Find("서커스_흔적"));

            // 가운데 판자 길·물웅덩이
            CopyTunnelCenter(Group(p, "FloorWalkway"), tunnel.Find("바닥_연출"), 0f);

            // 아치 아래 전구줄·케이블·매단 전구·늘어진 천 — 높아진 아치에 맞춰 올린다
            CopyTunnelCenter(Group(p, "StringLights"), tunnel.Find("전구줄"), ProtoTunnelLift);
            CopyTunnelCenter(Group(p, "Cables"), tunnel.Find("케이블"), ProtoTunnelLift);
            CopyTunnelCenter(Group(p, "Lights"), tunnel.Find("조명"), ProtoTunnelLift);
            CopyTunnelCenter(Group(p, "Rags"), tunnel.Find("낡은천"), ProtoTunnelLift);

            // ⑦ 공연 입구: 금색 프로시니엄(현재 대형 커튼 폭 7.7m에 맞춤) / 대기실 문 쪽: 붉은 밸런스
            var curtains = tunnel.Find("커튼");
            Place(p, "StageProscenium", new[] { curtains?.Find("무대_프로시니엄") }, new Vector3(WaitingRoomTunnelLayout.TunnelEnd - 0.7f, 6.3f * 0.8f / 2f, 0f), scale: new Vector3(1f, 0.8f, 7.7f / 5.4f)); // 높이 6.3m × 0.8, 바닥에 닿게
            Place(p, "LobbyDoorValance", new[] { curtains?.Find("Rail"), curtains?.Find("Valance"), curtains?.Find("Fringe") }, new Vector3(10.45f, float.NaN, 0f), scale: new Vector3(1f, 1f, 3.6f / 6f));
        }

        static void RibPart(Transform parent, string name, Vector3 position, Vector3 size, float tiltX, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(tiltX, 0f, 0f);
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        /// <summary>프로토타입 터널 좌표 → 통로 로컬 좌표. 남쪽 벽(z 6)→-4, 북쪽 벽(z 12)→4, 가운데(9)→0. x는 입구(157)→10.</summary>
        static Vector3 TunnelToCorridor(Vector3 protoWorld, bool keepWallDistance)
        {
            var local = greybox.InverseTransformPoint(protoWorld);
            var tunnelOrigin = greybox.InverseTransformPoint(new Vector3(157f, 0f, 9f)); // 프로토타입 터널 입구 가운데(월드)
            float x = local.x - tunnelOrigin.x + 10f;
            float z = local.z - tunnelOrigin.z; // 프로토타입 가운데 기준
            if (keepWallDistance) z = z < -1f ? z - 1f : z > 1f ? z + 1f : z; // 벽 쪽은 벽에서의 거리 유지(폭 6 → 8)
            return new Vector3(x, local.y, z);
        }

        static void CopyTunnelCenter(Transform parent, Transform group, float lift)
        {
            if (group == null) return;
            foreach (Transform child in group.Cast<Transform>().ToArray())
            {
                var target = TunnelToCorridor(child.position, keepWallDistance: false) + Vector3.up * lift;
                if (target.x < Corridor.xMin + 0.3f || target.x > Corridor.xMax - 0.3f) continue;
                // 긴 판자처럼 기준점은 안인데 끝이 벽을 넘는 것도 뺀다
                var parts = child.GetComponentsInChildren<Renderer>(true);
                if (parts.Length > 0)
                {
                    float half = parts.Max(r => r.bounds.extents.x);
                    if (target.x - half < Corridor.xMin + 0.15f || target.x + half > Corridor.xMax - 0.15f) continue;
                }
                var copy = Duplicate(new[] { child })[0].transform;
                copy.SetParent(parent, true);
                copy.position = greybox.TransformPoint(target);
            }
        }

        static void CopyTunnelSide(Transform parent, Transform group)
        {
            if (group == null) return;
            foreach (Transform child in group.Cast<Transform>().ToArray())
            {
                var renderers = child.GetComponentsInChildren<Renderer>(true);
                Bounds b = renderers.Length > 0 ? renderers[0].bounds : new Bounds(child.position, Vector3.zero);
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                var offset = TunnelToCorridor(child.position, true) - greybox.InverseTransformPoint(child.position);
                var lo = greybox.InverseTransformPoint(b.min) + offset;
                var hi = greybox.InverseTransformPoint(b.max) + offset;
                if (lo.x < Corridor.xMin + 0.2f || hi.x > Corridor.xMax - 0.2f) continue;
                bool nearWall = Mathf.Max(Mathf.Abs(lo.z), Mathf.Abs(hi.z)) > VaultRadius - 1.2f;
                float lift = 0f;
                // 벽을 따라 길게 이어지는 배관은 커튼 방 상단 장식·표지(~3.6m) 위로 올려 입구 위를 지나가게 한다
                if (nearWall && hi.x - lo.x > 4f && lo.y < 3.8f) lift = 3.8f - lo.y;
                // 그 밖의 높이가 있는 벽쪽 물건이 커튼 방 입구 높이에서 입구 폭과 겹치면 뺀다
                else if (nearWall && lo.y < 3.4f && hi.y - lo.y > 0.05f &&
                         CurtainSpans.Any(s => lo.x < s.x1 && hi.x > s.x0 && Mathf.Sign(lo.z + hi.z) == s.side)) continue;
                // 아치 위로 올라간 부분은 볼트 아래로 내린다
                float top = hi.y + lift, roof = CeilingAt(new Vector3((lo.x + hi.x) / 2f, 0f, (lo.z + hi.z) / 2f));
                if (top > roof) lift -= top - roof;
                var copy = Duplicate(new[] { child })[0].transform;
                copy.SetParent(parent, true);
                copy.position = greybox.TransformPoint(TunnelToCorridor(child.position, true) + Vector3.up * lift);
            }
        }

        /// <summary>X축 방향 반원 볼트 안쪽 면. 기준점 = 아치 시작 높이의 통로 가운데.</summary>
        static Mesh VaultMesh(float length, float radius)
        {
            string key = $"MoodVault_{length:0.##}x{radius:0.##}";
            if (meshes.TryGetValue(key, out var cached)) return cached;
            const int segments = 24;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            float arc = Mathf.PI * radius;
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.PI * i / segments;
                var ring = new Vector3(0f, Mathf.Sin(a) * radius, -Mathf.Cos(a) * radius);
                verts.Add(ring + Vector3.left * (length / 2f)); uvs.Add(new Vector2(0f, arc * i / segments * TilesPerMeter));
                verts.Add(ring + Vector3.right * (length / 2f)); uvs.Add(new Vector2(length * TilesPerMeter, arc * i / segments * TilesPerMeter));
            }
            for (int i = 0; i < segments; i++)
            {
                int b = i * 2;
                tris.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
            }
            // 안쪽(가운데 축)을 보는 면이 앞면이 되게 감기 방향을 맞춘다
            var n = Vector3.Cross(verts[tris[1]] - verts[tris[0]], verts[tris[2]] - verts[tris[0]]);
            var toAxis = -(verts[tris[0]] - new Vector3(verts[tris[0]].x, 0f, 0f));
            if (Vector3.Dot(n, toAxis) < 0f)
                for (int i = 0; i < tris.Count; i += 3) (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
            var mesh = new Mesh { name = key };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            AssetDatabase.AddObjectToAsset(mesh, MeshAsset());
            meshes[key] = mesh;
            return mesh;
        }

        // ── 로비 천막 아래 소품 더미 ─────────────────────────────────
        // 「대기실 에셋 재사용 후보」① 로비: 나무 상자·서커스 기물·신문·컵을 천막 아래 어수선하게 — 트리 아래 선물 더미 인상.
        // 스폰 줄(z=-3.5)을 향한 앞쪽 호에 몰아 두고, 스폰과 1.5m 이상 띄운다. 시드 고정이라 다시 실행해도 같은 배치.

        const string SyntyApocalypse = "Assets/_ThirdParty/Synty/PolygonApocalypse/Prefabs/";
        const string SyntyGeneric = "Assets/_ThirdParty/Synty/PolygonGeneric/Prefabs/";

        static void BuildTentPile(Transform p)
        {
            var rng = new System.Random(19);
            float Jitter(float range) => ((float)rng.NextDouble() * 2f - 1f) * range;
            var center = WaitingRoomGreyboxBuilder.TentCenter;
            Vector3 At(float deg, float dist, float y = 0f) =>
                new(center.x + Mathf.Sin(deg * Mathf.Deg2Rad) * dist, y, center.z + Mathf.Cos(deg * Mathf.Deg2Rad) * dist);
            float Facing(float deg) => deg + 180f; // 천막 쪽을 향하는 방향

            // 나무 상자: 바닥 줄 + 2단
            string crate = SyntyApocalypse + "Props/SM_Prop_Crate_01.prefab";
            string open = SyntyApocalypse + "Props/SM_Prop_Crate_Open_01.prefab";
            string large = SyntyApocalypse + "Props/SM_Prop_Crate_Large_01.prefab";
            var crates = Group(p, "Crates");
            Prefab(crates, large, At(110f, 2.55f), Facing(110f) + 90f + Jitter(8f));
            Prefab(crates, crate, At(138f, 2.5f), Facing(138f) + Jitter(20f));
            Prefab(crates, crate, At(138f, 2.5f, 0.82f), Facing(138f) + 25f + Jitter(10f));
            Prefab(crates, open, At(190f, 2.5f), Facing(190f) + Jitter(15f));
            Prefab(crates, crate, At(214f, 2.5f), Facing(214f) + Jitter(20f));
            Prefab(crates, open, At(214f, 2.5f, 0.82f), Facing(214f) - 18f + Jitter(10f));
            Prefab(crates, large, At(246f, 2.55f), Facing(246f) + 90f + Jitter(8f));
            Prefab(crates, crate, At(246f, 2.45f, 0.79f), Facing(246f) + 40f);
            Prefab(crates, open, At(78f, 2.45f), Facing(78f) + Jitter(20f));
            Prefab(crates, crate, At(282f, 2.45f), Facing(282f) + Jitter(20f));

            // 서커스 기물: 프로토타입 소품실·공연 준비실에서 복제
            var props = Src("소품실/소품");
            var circus = Group(p, "CircusProps");
            CopyAt(circus, First(props, "북"), At(163f, 2.75f), Jitter(30f));
            CopyAt(circus, First(props, "BigBall"), At(176f, 3.5f), 0f);
            CopyAt(circus, First(props, "HarlequinBall"), At(138f, 2.5f, 1.64f), 0f);
            CopyAt(circus, First(props, "HarlequinBall", 1), At(232f, 3.3f), 0f);
            CopyAt(circus, First(props, "HarlequinBall", 2), At(122f, 3.2f), 0f);
            CopyAt(circus, First(props, "JugglingPin"), At(152f, 3.3f), Jitter(180f));
            CopyAt(circus, First(props, "JugglingPin", 1), At(204f, 3.25f), Jitter(180f));
            CopyAt(circus, First(props, "JugglingPin", 2), At(228f, 3.0f), Jitter(180f));
            var hoop = CopyAt(circus, First(props, "Hoop"), At(196f, 2.95f), Facing(196f) - 90f);
            if (hoop != null) hoop.localRotation *= Quaternion.Euler(0f, 0f, 14f); // 상자에 기대 세운다
            CopyAt(circus, First(props, "트렁크"), At(96f, 3.0f), Facing(96f) + Jitter(25f));
            CopyAt(circus, First(Find(Src("백스테이지1"), "공연준비실"), "ClownShoe"), At(170f, 3.05f), Jitter(60f));

            // 신문: 바닥과 상자 위에 흩어진 낡은 종이(신문 지면·모델은 미확정 — 종이 재질 임시)
            var papers = Group(p, "Newspapers");
            var paper = ProtoMaterial("V6_Paper_Old");
            foreach (var (deg, dist, y) in new[] { (128f, 3.45f, 0f), (158f, 3.7f, 0f), (186f, 3.25f, 0f), (212f, 3.55f, 0f), (243f, 3.3f, 0f), (190f, 2.5f, 0.825f) })
            {
                var sheet = GameObject.CreatePrimitive(PrimitiveType.Cube);
                sheet.name = "Newspaper";
                Object.DestroyImmediate(sheet.GetComponent<Collider>());
                sheet.transform.SetParent(papers, false);
                sheet.transform.localPosition = At(deg, dist, y + 0.004f);
                sheet.transform.localRotation = Quaternion.Euler(Jitter(3f), (float)rng.NextDouble() * 360f, Jitter(3f));
                sheet.transform.localScale = new Vector3(0.42f, 0.006f, 0.58f);
                sheet.GetComponent<MeshRenderer>().sharedMaterial = paper;
            }

            // 컵: 바닥에 세우거나 쓰러뜨려 마구잡이로
            var cups = Group(p, "Cups");
            string beer = SyntyApocalypse + "Item/SM_Item_BeerCup_01.prefab";
            string beer2 = SyntyApocalypse + "Item/SM_Item_BeerCup_02.prefab";
            string mug = SyntyGeneric + "Props/SM_Gen_Prop_Mug_01.prefab";
            Prefab(cups, beer, At(145f, 3.55f, 0.095f), Jitter(180f));
            Prefab(cups, beer2, At(222f, 3.65f, 0.095f), Jitter(180f));
            Prefab(cups, mug, At(174f, 3.75f), Jitter(180f));
            Prefab(cups, mug, At(252f, 3.05f), Jitter(180f));
            Prefab(cups, beer, At(214f, 2.5f, 1.64f + 0.095f), Jitter(180f));
            var tipped = Prefab(cups, beer2, At(198f, 3.75f, 0.075f), Jitter(180f));
            if (tipped != null) tipped.localRotation *= Quaternion.Euler(90f, 0f, 0f);
        }

        static Transform Prefab(Transform parent, string path, Vector3 localPosition, float yaw)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) { Debug.LogWarning($"[WaitingRoomMood] 프리팹 없음: {path}"); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return go.transform;
        }

        /// <summary>프로토타입 오브젝트를 복제해 그레이박스 로컬 위치에 두고, 원래 기울기는 유지한 채 Y축만 더 돌린다.</summary>
        static Transform CopyAt(Transform parent, Transform source, Vector3 localPosition, float yaw)
        {
            if (source == null) return null;
            var copy = Duplicate(new[] { source })[0].transform;
            var rotation = source.rotation;
            copy.SetParent(parent, false);
            copy.localPosition = localPosition;
            copy.localRotation = Quaternion.Euler(0f, yaw, 0f) * (Quaternion.Inverse(greybox.rotation) * rotation);
            copy.localScale = source.lossyScale;
            // 기준점이 바닥이 아닌 소품도 있으니, 아랫면이 localPosition.y(바닥 또는 상자 윗면)에 닿게 맞춘다
            var renderers = copy.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                float bottom = renderers.Min(r => r.bounds.min.y);
                float target = greybox.TransformPoint(localPosition).y;
                copy.position += Vector3.up * (target - bottom);
            }
            return copy;
        }

        static Transform First(Transform root, string prefix, int skip = 0)
        {
            if (root == null) return null;
            foreach (Transform c in root)
                if (c.name.StartsWith(prefix) && skip-- == 0) return c;
            Debug.LogWarning($"[WaitingRoomMood] '{root.name}'에 '{prefix}' 없음");
            return null;
        }

        // ── 복제·배치 도우미 ───────────────────────────────────────

        /// <summary>
        /// 원본들을 프리팹 연결·오버라이드째 복제해 그룹으로 묶고, 그룹 기준점(anchor 위치 또는 묶음 중심)을 그레이박스 로컬 좌표 <paramref name="target"/>에 둔다.
        /// target.y가 NaN이면 원본 높이에 <paramref name="yOffset"/>만 더한다.
        /// </summary>
        static Transform Place(Transform parent, string name, Transform[] sources, Vector3 target, float yOffset = 0f, Transform anchor = null, Vector3? scale = null, float yaw = 0f)
        {
            sources = sources.Where(s => s != null).ToArray();
            if (sources.Length == 0)
            {
                Debug.LogWarning($"[WaitingRoomMood] '{name}': 프로토타입 원본을 찾지 못해 건너뜀");
                return Group(parent, name);
            }

            var pivot = anchor != null ? anchor.position : Center(sources);
            var g = new GameObject(name).transform;
            g.SetParent(parent, false);
            g.position = pivot;
            foreach (var copy in Duplicate(sources)) copy.transform.SetParent(g, true);

            var local = greybox.InverseTransformPoint(pivot);
            g.localPosition = new Vector3(target.x, float.IsNaN(target.y) ? local.y + yOffset : target.y, target.z);
            g.localRotation = Quaternion.Euler(0f, yaw, 0f);
            if (scale.HasValue) g.localScale = scale.Value;
            return g;
        }

        /// <summary>Edit > Duplicate와 같은 방식으로 복제한다(중첩 프리팹 연결 유지).</summary>
        static GameObject[] Duplicate(Transform[] sources)
        {
            // 복제 직후 Selection이 가끔 원본을 그대로 돌려준다 — 그대로 쓰면 프로토타입 원본이 Mood로 옮겨지고(다음 실행 때 지워짐)
            // 복제본 "이름 (1)"만 프로토타입에 남는다(2026-10-20 발견). 그래서 원본의 부모에 새로 생긴 형제를 복제본으로 삼는다.
            var parents = sources.Select(s => s.parent).Distinct().ToArray();
            var before = new HashSet<Transform>(parents.SelectMany(p => p.Cast<Transform>()));
            Selection.objects = sources.Select(s => (Object)s.gameObject).ToArray();
            Unsupported.DuplicateGameObjectsUsingPasteboard();
            var originals = new HashSet<GameObject>(sources.Select(s => s.gameObject));
            var copies = Selection.gameObjects.Where(g => !originals.Contains(g)).ToArray();
            if (copies.Length != sources.Length)
                copies = parents.SelectMany(p => p.Cast<Transform>()).Where(t => !before.Contains(t)).Select(t => t.gameObject).ToArray();
            if (copies.Length != sources.Length)
                throw new System.InvalidOperationException($"[WaitingRoomMood] 복제 실패: 원본 {sources.Length}개, 복제본 {copies.Length}개 ({sources[0].name})");
            foreach (var c in copies)
            {
                c.name = System.Text.RegularExpressions.Regex.Replace(c.name, @" \(\d+\)$", "");
                c.SetActive(true);
            }
            return copies;
        }

        static void CullOutside(Transform group, Rect localRect)
        {
            var doomed = new List<GameObject>();
            foreach (Transform root in group)
                foreach (Transform child in root.childCount > 0 ? root.Cast<Transform>().ToArray() : new[] { root })
                {
                    var lp = greybox.InverseTransformPoint(child.position);
                    if (!localRect.Contains(new Vector2(lp.x, lp.z))) doomed.Add(child.gameObject);
                }
            foreach (var d in doomed) Object.DestroyImmediate(d);
        }

        /// <summary>복제한 철망 전구의 전선을 천장까지 늘이거나 줄인다.</summary>
        static void FitWiresToCeiling(Transform mood)
        {
            foreach (var t in mood.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "Wire" || t.GetComponent<MeshFilter>() == null) continue;
                float ceiling = greybox.TransformPoint(new Vector3(0f, CeilingAt(greybox.InverseTransformPoint(t.position)), 0f)).y;
                float parentScale = t.parent.lossyScale.y;
                float half = t.localScale.y * parentScale;
                float bottom = t.position.y - half;
                if (bottom >= ceiling) continue;
                float newHalf = (ceiling - bottom) / 2f;
                var s = t.localScale;
                t.localScale = new Vector3(s.x, newHalf / parentScale, s.z);
                t.position = new Vector3(t.position.x, bottom + newHalf, t.position.z);
            }
        }

        static Vector3 Center(Transform[] sources)
        {
            Bounds? b = null;
            foreach (var s in sources)
                foreach (var r in s.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is ParticleSystemRenderer) continue;
                    if (b == null) b = r.bounds;
                    else { var bb = b.Value; bb.Encapsulate(r.bounds); b = bb; }
                }
            if (b != null) return b.Value.center;
            var sum = Vector3.zero;
            foreach (var s in sources) sum += s.position;
            return sum / sources.Length;
        }

        // ── 바둑판 UV 상자 메시 (늘어난 큐브에 텍스처가 늘어지지 않게) ─────────

        static void ApplyTiledMesh(MeshRenderer renderer)
        {
            var mf = renderer.GetComponent<MeshFilter>();
            var mat = renderer.sharedMaterial;
            if (mf == null || mf.sharedMesh == null || mat == null) return;
            if (mf.sharedMesh.name != "Cube" && !mf.sharedMesh.name.StartsWith("MoodBox_")) return;
            if (!mat.HasProperty("_BaseMap") || mat.GetTexture("_BaseMap") == null) return;

            var s = renderer.transform.lossyScale;
            var size = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            string key = $"MoodBox_{size.x:0.##}x{size.y:0.##}x{size.z:0.##}";
            if (!meshes.TryGetValue(key, out var mesh))
            {
                mesh = BoxMesh(size * TilesPerMeter);
                mesh.name = key;
                AssetDatabase.AddObjectToAsset(mesh, MeshAsset());
                meshes[key] = mesh;
            }
            Undo.RecordObject(mf, "Waiting Room Mood");
            mf.sharedMesh = mesh;
        }

        static Object meshAsset;

        static Object MeshAsset()
        {
            if (meshAsset != null) return meshAsset;
            meshAsset = AssetDatabase.LoadMainAssetAtPath(MeshAssetPath);
            if (meshAsset == null)
            {
                var holder = new Mesh { name = "WaitingRoomMoodMeshes" };
                AssetDatabase.CreateAsset(holder, MeshAssetPath);
                meshAsset = holder;
            }
            return meshAsset;
        }

        static void LoadMeshes()
        {
            meshes.Clear();
            meshAsset = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(MeshAssetPath))
                if (o is Mesh m && m.name.StartsWith("MoodBox_")) meshes[m.name] = m;
        }

        /// <summary>단위 큐브(±0.5). 면마다 UV = 면 크기 × 타일 밀도.</summary>
        static Mesh BoxMesh(Vector3 tiles)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            void Face(Vector3 n, Vector3 u, Vector3 v, float tu, float tv)
            {
                int i = verts.Count;
                var c = n * 0.5f;
                verts.Add(c - u * 0.5f - v * 0.5f); uvs.Add(new Vector2(0f, 0f));
                verts.Add(c - u * 0.5f + v * 0.5f); uvs.Add(new Vector2(0f, tv));
                verts.Add(c + u * 0.5f + v * 0.5f); uvs.Add(new Vector2(tu, tv));
                verts.Add(c + u * 0.5f - v * 0.5f); uvs.Add(new Vector2(tu, 0f));
                // 앞면 = Cross(b-a, c-a)가 n과 같은 방향
                bool front = Vector3.Dot(Vector3.Cross(verts[i + 1] - verts[i], verts[i + 2] - verts[i]), n) > 0f;
                if (front) tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
                else tris.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            }
            Face(Vector3.right, Vector3.back, Vector3.up, tiles.z, tiles.y);
            Face(Vector3.left, Vector3.forward, Vector3.up, tiles.z, tiles.y);
            Face(Vector3.up, Vector3.right, Vector3.forward, tiles.x, tiles.z);
            Face(Vector3.down, Vector3.right, Vector3.back, tiles.x, tiles.z);
            Face(Vector3.forward, Vector3.right, Vector3.up, tiles.x, tiles.y);
            Face(Vector3.back, Vector3.left, Vector3.up, tiles.x, tiles.y);

            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ── 공통 ───────────────────────────────────────────────

        static Material ProtoMaterial(string name)
        {
            if (materials.TryGetValue(name, out var m)) return m;
            m = AssetDatabase.LoadAssetAtPath<Material>($"{ProtoMaterialFolder}/{name}.mat");
            if (m == null) Debug.LogWarning($"[WaitingRoomMood] 재질 없음: {name}");
            materials[name] = m;
            return m;
        }

        static Transform Root(Scene scene, string name) =>
            scene.GetRootGameObjects().FirstOrDefault(g => g.name == name)?.transform;

        static Transform Src(string path)
        {
            var t = source.Find(path);
            if (t == null) Debug.LogWarning($"[WaitingRoomMood] 프로토타입 경로 없음: {SourceName}/{path}");
            return t;
        }

        static Transform Find(Transform root, string name) =>
            root == null ? null : root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        static Transform[] Children(Transform t) => t == null ? new Transform[0] : t.Cast<Transform>().ToArray();

        static Transform Group(Transform parent, string name)
        {
            var g = new GameObject(name).transform;
            g.SetParent(parent, false);
            return g;
        }

        static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (var p = t; p != null && p != greybox; p = p.parent) parts.Add(p.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
