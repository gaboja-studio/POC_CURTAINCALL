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
            FitWiresToCeiling(mood);

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

        static void Ceiling(Transform p, string name, Rect r, string material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(p, false);
            go.transform.localPosition = new Vector3(r.center.x, CeilingY + 0.1f, r.center.y);
            go.transform.localScale = new Vector3(r.width + 0.3f, 0.2f, r.height + 0.3f);
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
            Place(p, "StageEntrance_RedSpill", new[] { hall.Find("DoorRedSpill") }, new Vector3(35.4f, 1.5f, 0f));
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

            // 소품실 동쪽(분장 코너): 의상걸이
            var dressing = Src("분장실");
            Place(p, "DressingCorner_CostumeRack", new[] { dressing.Find("의상걸이") }, new Vector3(6.8f, float.NaN, -14.75f));

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

        // ── ⑥ 무대 입구 통로 → 프로토타입식 터널 ───────────────────────
        // 기능(커튼 방 열림 3·닫힘 5, 집결 판정 구역, ⑦ 공연 입구, 대기실 문 셔터)은 그대로 두고 외형만 터널로:
        // 콘크리트 반원 볼트, 커튼 방 사이 강철 리브, 벽면 배관, 판자 길, 전구줄·케이블·매단 전구, 깃발·포스터, ⑦ 금색 프로시니엄.
        // 프로토타입 터널(폭 6m, 벽 z 6/12, x 157~198)을 통로(폭 8m, 벽 z -4/4, x 10~37)에 옮겨 쓴다.

        const float VaultSpring = 3.6f;           // 커튼 방 상단 장식(3.3m)·표지(3.45m) 위에서 아치 시작
        const float VaultRadius = 4f;             // 통로 반폭
        const float VaultApex = VaultSpring + VaultRadius;
        const float ProtoTunnelLift = VaultApex - 6.2f; // 프로토타입 아치 꼭대기(6.2m) 대비
        static readonly float[] RibXs = { 11f, 16.5f, 21.5f, 26.5f, 31.5f, 36.2f }; // 커튼 방 사이 틈
        static readonly (float x0, float x1)[] CurtainSpans = { (12.4f, 15.6f), (17.4f, 20.6f), (22.4f, 25.6f), (27.4f, 30.6f) };

        /// <summary>그 위치의 천장 높이(통로 안이면 볼트 곡면).</summary>
        static float CeilingAt(Vector3 local)
        {
            if (!Corridor.Contains(new Vector2(local.x, local.z))) return CeilingY;
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
            foreach (var x in RibXs)
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
            Place(p, "StageProscenium", new[] { curtains?.Find("무대_프로시니엄") }, new Vector3(36.3f, 6.3f * 0.8f / 2f, 0f), scale: new Vector3(1f, 0.8f, 7.7f / 5.4f)); // 높이 6.3m × 0.8, 바닥에 닿게
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
                else if (nearWall && lo.y < 3.4f && hi.y - lo.y > 0.05f && CurtainSpans.Any(s => lo.x < s.x1 && hi.x > s.x0)) continue;
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
            Selection.objects = sources.Select(s => (Object)s.gameObject).ToArray();
            Unsupported.DuplicateGameObjectsUsingPasteboard();
            var copies = Selection.gameObjects;
            foreach (var c in copies)
            {
                c.name = c.name.Replace(" (1)", "");
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
