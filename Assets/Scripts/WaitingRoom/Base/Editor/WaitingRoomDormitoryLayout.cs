using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.EditorTools
{
    /// <summary>구조도 ② 숙소만 조정한다. 원래 존재하던 비대상 오브젝트 전체를 비교하여 다른 공간 변경을 차단한다.</summary>
    public static class WaitingRoomDormitoryLayout
    {
        public enum SuspendedSpanKind { Rope, Rod }
        const string Bundle = "Assets/Scenes/Tests/WaitingRoom/Base/Prefabs/WaitingRoomDormitoryLayout.asset";
        static Transform g, src, kit, output;
        static readonly List<Transform> allowed = new();
        public static string LastReport { get; private set; }

        [MenuItem("Tools/CurtainCall/대기실/공간별 1단계·숙소 구조도 배치")]
        public static void ApplyCurrent()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity")
                throw new InvalidOperationException("WaitingRoomBase 편집 씬에서만 실행합니다.");
            g = scene.GetRootGameObjects().Single(o => o.name == "WaitingRoomGreybox").transform;
            src = scene.GetRootGameObjects().Single(o => o.name == "대기실").transform;
            kit = scene.GetRootGameObjects().Single(o => o.name == "WaitingRoom").transform;
            if (g.Find("Mood/InteriorArt/Stage01_Dormitory")) throw new InvalidOperationException("숙소 단계가 이미 적용됐습니다. 재적용 전 Undo로 이 단계만 되돌려 주세요.");
            allowed.Clear();
            foreach (string path in new[] { "Mood/Furnishings/Dormitory", "Mood/ProtoFill/Dormitory", "Mood/Rooms/Dormitory", "CameraZones/Zone_Dormitory" }) allowed.Add(g.Find(path));
            foreach (Transform t in g.Find("Mood/InteriorArt"))
                if ((t.name.StartsWith("Dorm_") && g.InverseTransformPoint(t.position).x < 3) || t.name == "Dormitory_PatchedCeiling" || t.name.StartsWith("WR04_") || t.name == "WR05_RepairedShortBench") allowed.Add(t);
            foreach (Transform t in g.Find("Mood/VisibilityLighting")) if (t.name.StartsWith("Dormitory_")) allowed.Add(t);
            var protectedObjects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Where(t => !allowed.Any(a => a && (t == a || t.IsChildOf(a)))).ToArray();
            string before = Signature(protectedObjects);
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("공간별 1단계·숙소만 구조도 배치");
            try
            {
                output = Group(g.Find("Mood/InteriorArt"), "Stage01_Dormitory");
                foreach (var a in allowed.Where(t => t && t != g.Find("CameraZones/Zone_Dormitory") && !t.name.StartsWith("Dormitory_Living") && !t.name.StartsWith("Dormitory_Beds")))
                { Undo.RecordObject(a.gameObject, "숙소 기존 배치 보관"); a.gameObject.SetActive(false); }
                BuildRoom();
                BuildBedRow();
                BuildPersonalItems();
                RefineDetails();
                var zone = g.Find("CameraZones/Zone_Dormitory"); Undo.RecordObject(zone, "숙소 구역"); zone.localPosition = new Vector3(-3.5f, 3, 12.35f);
                var box = zone.GetComponent<BoxCollider>(); Undo.RecordObject(box, "숙소 구역"); box.size = new Vector3(12.7f, 6, 6.4f);
                foreach (Transform t in g.Find("Mood/VisibilityLighting")) if (t.name.StartsWith("Dormitory_"))
                {
                    Undo.RecordObject(t, "숙소 보조광"); var p = t.localPosition; p.z = t.name.EndsWith("Beds") ? 14.35f : 11.2f; t.localPosition = p;
                    var light = t.GetComponent<Light>(); Undo.RecordObject(light, "숙소 보조광"); light.intensity = 5.5f;
                }
                if (before != Signature(protectedObjects)) throw new InvalidOperationException("숙소 외 오브젝트 변경 감지: 단계 전체를 되돌립니다.");
                LastReport = $"숙소 13×6.7m, 침대4·사물함2칸·옷장1·러그1·개인 짐, 비대상 {protectedObjects.Length}개 오브젝트 서명 동일(중앙 천막/통로/화장실 포함)";
                AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); Undo.CollapseUndoOperations(undo); Debug.Log("[DormitoryLayout] " + LastReport);
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        static void BuildRoom()
        {
            // 공용 북쪽 외벽과 화장실 바닥은 그대로 두고 숙소 내부에만 낮은 벽·바닥을 덧댄다.
            Box("Dorm_NorthPatchedWall", new Vector3(-3.5f, 2.2f, 15.85f), new Vector3(13, 4.4f, .3f), Material("V4_Wall_PeeledPlaster"), true);
            var tile = Variant("Dorm_GrimyStoneTile", "V4_Tile_Grimy", new Color(.43f, .45f, .43f), new Vector2(13, 6.7f));
            Box("Dorm_StoneFloor", new Vector3(-3.5f, .006f, 12.35f), new Vector3(12.8f, .012f, 6.5f), tile, false);
            for (int i = 0; i < 4; i++)
                Box("CeilingRepairPanel_" + i, new Vector3(-8.3f + i * 3.2f, 4.36f + .04f * (i % 2), 12.35f), new Vector3(3.28f, .12f, 6.8f), Material(i % 2 == 0 ? "V4_Ceiling_RustyMetal" : "V4_Ceiling_Concrete"), false, new Vector3(0, 0, i % 2 == 0 ? 1 : -1));
            for (int i = 0; i < 6; i++)
            {
                float x = -9.7f + i * 2.45f;
                Box("OldWallStud_" + i, new Vector3(x, 2.15f, 15.64f), new Vector3(.065f, 4.2f, .07f), Material("V4_RustyMetal"), false, new Vector3(0, 0, i % 2 == 0 ? .6f : -.8f));
            }
            var rugAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_ThirdParty/Synty/PolygonApocalypse/Prefabs/Props/SM_Prop_Rug_04.prefab");
            var rug = Clone("WornEntryRug", new[] { rugAsset.transform }, new Vector3(-2, .021f, 10.55f), 1.5f);
            var b = Bounds(rug); rug.localScale *= Mathf.Min(4.4f / b.size.x, 2.1f / b.size.z); Bottom(rug, new Vector3(-2, .021f, 10.55f));
            // 낙서와 찢긴 포스터는 새로운 북쪽 벽에 다시 붙인다.
            var graffiti = Clone("PreviousGenerationWallMarks", new[] { g.Find("Mood/Rooms/Dormitory/Graffiti") }, new Vector3(-5, .85f, 15.65f), 0);
            foreach (var c in graffiti.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        }

        static void BuildBedRow()
        {
            var bed = kit.Find("R02_Dormitory/Props/SM_Prop_Bed_Bunk_01");
            float[] xs = { -8.6f, -6.5f, -2.4f, 1.2f };
            string[] blankets = { "V4_Blanket_DullRed", "V4_Blanket_Navy", "V4_Blanket_Olive", "V4_Mattress_Stained" };
            for (int i = 0; i < xs.Length; i++)
            {
                var clone = Clone("Bed_" + (char)('A' + i), new[] { bed }, new Vector3(xs[i], .025f, 14.25f), 270, 1, true);
                Clone("BedsideLamp_" + i, new[] { src.Find("대기실_본관/조명/CagedLamp") }, new Vector3(xs[i] + .65f, 2.2f, 15.45f), -5 + i * 4, .3f);
                Light("BedsideGlow_" + i, new Vector3(xs[i] + .65f, 2.35f, 15.2f), new Color(1, .65f, .34f), 1.5f, 3);
            }
            var lockerSource = src.Find("대기실_본관/사물함_의상/사물함");
            var parts = lockerSource.Cast<Transform>().Where(t => t.localPosition.x < -.2f && t.name != "Base").ToArray();
            var lockers = Clone("FadedTwoDoorLockers", parts, new Vector3(-4.6f, 0, 15.23f), 0);
            var teal = Variant("Dorm_FadedTealLocker", "V4_Partition_RustGreen", new Color(.29f, .38f, .36f), Vector2.one);
            foreach (var r in lockers.GetComponentsInChildren<Renderer>(true)) if (r.name == "Locker" || r.name == "Seam" || r.name == "OpenDoor") r.sharedMaterial = teal;
            ColliderFor(lockers);
            Clone("DarkWoodWardrobe", new[] { kit.Find("R08_JunkStorage_Hidden/Props/SM_Prop_Cupboard_Broken_01") }, new Vector3(-.7f, 0, 15), 180, 1, true);
            Clone("WestPersonalDresser", new[] { kit.Find("R04_DressingRoom/Props/SM_Prop_Dresser_02") }, new Vector3(-9.2f, 0, 11.55f), 90, .7f, true);
        }

        static void BuildPersonalItems()
        {
            float[] xs = { -8.6f, -6.5f, -2.4f, 1.2f };
            for (int i = 0; i < 4; i++)
            {
                Clone("FootTrunk_" + i, new[] { src.Find("대기실_본관/잡동사니/트렁크") }, new Vector3(xs[i] + .1f, 0, 12.6f), i % 2 == 0 ? -7 : 9, .55f, true);
                Clone("PersonalClothes_" + i, new[] { src.Find("분장실/SM_MET_TangledCloth02_c1_LOD1") }, new Vector3(xs[i] + .15f, .47f, 12.65f), i * 27, .38f);
            }
            Clone("BedsideStool_B", new[] { src.Find("대기실_본관/숙소/숙소_01/가구/SM_CS1_Stool_01a_c1_LOD0") }, new Vector3(-5.6f, 0, 14), 13, .9f, true);
            Clone("PersonalBear", new[] { kit.Find("R02_Dormitory/Props/SM_Prop_Toy_Bear_01") }, new Vector3(1.15f, .59f, 14.85f), 180);
            Clone("StoredBedLinen", new[] { src.Find("대기실_본관/숙소/숙소_01/가구/SM_PAD_CardboardBox01_c1_LOD0") }, new Vector3(-9.05f, .72f, 11.45f), -8, .65f);
            FitPersonalClothesline(g);
        }

        /// <summary>천의 위쪽 접힘선을 줄에 맞추고 침대 앞 가구와 분리한다. 원본 프로토타입 없이 재적용 가능.</summary>
        public static void FitPersonalClothesline(Transform greybox, SuspendedSpanKind kind = SuspendedSpanKind.Rope)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("편집 모드에서만 가능합니다.");
            var dorm = greybox.Find("Mood/InteriorArt/Stage01_Dormitory");
            if (!dorm) throw new InvalidOperationException("숙소 배치가 없습니다.");
            if (kind != SuspendedSpanKind.Rope && kind != SuspendedSpanKind.Rod) throw new ArgumentOutOfRangeException(nameof(kind));
            const float startX = -9.78f, endX = 2.78f, z = 11.6f;
            Vector3 Point(float x)
            {
                float t = Mathf.InverseLerp(startX, endX, x);
                float sag = kind == SuspendedSpanKind.Rope ? .05f * Mathf.Sin(t * Mathf.PI) : 0f;
                return greybox.TransformPoint(new Vector3(x, 2.3f - sag, z));
            }
            (Renderer surface, Vector3 point) CeilingAttachment(Vector3 from)
            {
                var ray = new Ray(from, Vector3.up);
                foreach (var filter in dorm.GetComponentsInChildren<MeshFilter>().Where(f => f.name.StartsWith("CeilingRepairPanel_")))
                {
                    var renderer = filter.GetComponent<Renderer>();
                    if (!renderer || !renderer.enabled || !filter.sharedMesh) continue;
                    var meshBounds = filter.sharedMesh.bounds;
                    var bottom = filter.transform.TransformPoint(new Vector3(meshBounds.center.x, meshBounds.min.y, meshBounds.center.z));
                    var plane = new Plane(-filter.transform.up, bottom);
                    if (!plane.Raycast(ray, out float distance) || distance > 6f) continue;
                    var point = ray.GetPoint(distance);
                    var local = filter.transform.InverseTransformPoint(point);
                    if (local.x < meshBounds.min.x || local.x > meshBounds.max.x || local.z < meshBounds.min.z || local.z > meshBounds.max.z) continue;
                    return (renderer, point);
                }
                throw new InvalidOperationException("줄/봉 생성 거부: 양끝을 고정할 실제 천장 지지면이 없습니다. 기존 배치는 유지합니다.");
            }
            // 좌표를 벽으로 간주하지 않는다. 줄과 봉 모두 양끝의 실제 천장 표면을 먼저 확인한다.
            // 지지가 없으면 기존 구조를 지우거나 새 구조를 출력하기 전에 중단한다.
            var west = CeilingAttachment(Point(startX));
            var east = CeilingAttachment(Point(endX));
            var line = dorm.Find("PersonalClothesline");
            if (!line) line = Group(dorm, "PersonalClothesline");
            foreach (Transform child in line.Cast<Transform>().ToArray()) Undo.DestroyObjectImmediate(child.gameObject);
            const string materialPath = "Assets/Scenes/Tests/WaitingRoom/Base/Materials/WR_Clothesline.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "WR_Clothesline", color = new Color(.45f, .36f, .23f) };
                material.SetFloat("_Smoothness", .1f);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            void Rope(string name, Vector3 from, Vector3 to, float diameter)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Undo.RegisterCreatedObjectUndo(go, "숙소 빨랫줄 지지 구조");
                go.name = name;
                go.transform.SetParent(line, true);
                go.transform.SetPositionAndRotation((from + to) * .5f, Quaternion.FromToRotation(Vector3.up, to - from));
                go.transform.localScale = new Vector3(diameter, Vector3.Distance(from, to) * .5f, diameter);
                go.GetComponent<Renderer>().sharedMaterial = material;
                Object.DestroyImmediate(go.GetComponent<Collider>());
            }
            int segments = kind == SuspendedSpanKind.Rope ? 24 : 1;
            for (int i = 0; i < segments; i++)
                Rope((kind == SuspendedSpanKind.Rope ? "Rope_" : "Rod_") + i, Point(Mathf.Lerp(startX, endX, i / (float)segments)), Point(Mathf.Lerp(startX, endX, (i + 1f) / segments)), kind == SuspendedSpanKind.Rope ? .026f : .04f);
            foreach (var support in new[] { (x: startX, hit: west), (x: endX, hit: east) })
            {
                string side = support.x == startX ? "W" : "E";
                var attachment = support.hit.point;
                Rope("CeilingSuspension_" + side, Point(support.x), attachment, .026f);
                Rope("CeilingFixing_" + side, attachment - Vector3.up * .015f, attachment + Vector3.up * .015f, .1f);
            }
            float[] xs = { -8.45f, -6.35f, -2.25f, 1.35f };
            for (int i = 0; i < xs.Length; i++)
            {
                var cloth = dorm.Find("PersonalClothes_" + i);
                if (!cloth) throw new InvalidOperationException("개인 천 없음: " + i);
                var filter = cloth.GetComponentInChildren<MeshFilter>();
                if (!filter || !filter.sharedMesh.isReadable) throw new InvalidOperationException("천의 지지선 메시를 읽을 수 없습니다.");
                Undo.RecordObject(cloth, "천 접힘선을 빨랫줄에 맞춤");
                Vector3[] Points() => filter.sharedMesh.vertices.Select(v => filter.transform.TransformPoint(v)).ToArray();
                var points = Points();
                float top = points.Max(p => p.y);
                var crest = points.Where(p => p.y >= top - .01f).ToArray();
                float cx = crest.Average(p => p.x), cz = crest.Average(p => p.z);
                float xx = crest.Sum(p => (p.x - cx) * (p.x - cx));
                float zz = crest.Sum(p => (p.z - cz) * (p.z - cz));
                float xz = crest.Sum(p => (p.x - cx) * (p.z - cz));
                float angle = .5f * Mathf.Atan2(2f * xz, xx - zz);
                var axis = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                cloth.rotation = Quaternion.FromToRotation(axis, greybox.right) * cloth.rotation;
                points = Points(); top = points.Max(p => p.y);
                crest = points.Where(p => p.y >= top - .01f).ToArray();
                var attachment = new Vector3(crest.Average(p => p.x), top - .008f, crest.Average(p => p.z));
                cloth.position += Point(xs[i]) - attachment;
                foreach (var collider in cloth.GetComponentsInChildren<Collider>(true))
                { Undo.RecordObject(collider, "장식 천 충돌 해제"); collider.enabled = false; }
                if (PrefabUtility.IsPartOfPrefabInstance(cloth)) PrefabUtility.RecordPrefabInstancePropertyModifications(cloth);
            }
            EditorSceneManager.MarkSceneDirty(greybox.gameObject.scene);
        }

        public static void RefineCurrent()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("편집 모드에서만 가능합니다.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity") throw new InvalidOperationException("대상 씬이 아닙니다.");
            g = scene.GetRootGameObjects().Single(o => o.name == "WaitingRoomGreybox").transform;
            src = scene.GetRootGameObjects().Single(o => o.name == "대기실").transform;
            output = g.Find("Mood/InteriorArt/Stage01_Dormitory");
            if (!output) throw new InvalidOperationException("먼저 숙소 단계를 적용하세요.");
            RefineDetails(); AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
        }
        static void RefineDetails()
        {
            var floor = output.Find("Dorm_StoneFloor").GetComponent<Renderer>(); Undo.RecordObject(floor, "숙소 석재 바닥");
            floor.sharedMaterial = Variant("Dorm_WornStoneFloor", "V4_Floor_WetConcrete", new Color(.5f, .52f, .49f), new Vector2(6, 3));
            if (!output.Find("StoneTileGrout"))
            {
                var vertices = new List<Vector3>(); var triangles = new List<int>();
                void Quad(float x0, float z0, float x1, float z1)
                { int n = vertices.Count; vertices.AddRange(new[] { new Vector3(x0, .014f, z0), new Vector3(x0, .014f, z1), new Vector3(x1, .014f, z1), new Vector3(x1, .014f, z0) }); triangles.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3 }); }
                for (float x = -9.6f; x < 2.9f; x += .8f) Quad(x, 9.1f, x + .012f, 15.6f);
                for (float z = 9.2f; z < 15.6f; z += .8f) Quad(-9.9f, z, 2.9f, z + .012f);
                var mesh = new Mesh { name = "Dorm_StoneTileGrout" }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.AddObjectToAsset(mesh, Bundle);
                var grid = Group(output, "StoneTileGrout"); Undo.AddComponent<MeshFilter>(grid.gameObject).sharedMesh = mesh;
                Undo.AddComponent<MeshRenderer>(grid.gameObject).sharedMaterial = Variant("Dorm_DarkGrout", "V4_Black", new Color(.08f, .085f, .08f), Vector2.one);
            }
            for (int i = 0; i < 4; i++)
            {
                var bed = output.Find("Bed_" + (char)('A' + i)); Undo.RecordObject(bed, "침대 미세 어긋남"); bed.localRotation = Quaternion.Euler(0, 270 + new[] { -1.5f, 1.8f, -2f, 2.5f }[i], 0);
            }
            var lockers = output.Find("FadedTwoDoorLockers"); Undo.RecordObject(lockers, "사물함 정면 방향"); lockers.localRotation = Quaternion.Euler(0, 180, 0);
            foreach (var renderer in output.Find("DarkWoodWardrobe").GetComponentsInChildren<Renderer>(true)) { Undo.RecordObject(renderer, "옷장 낡은 나무"); renderer.sharedMaterial = Material("V4_Wood_Old"); }
            var marks = output.Find("PreviousGenerationWallMarks"); Undo.RecordObject(marks, "이전 세대 벽 흔적"); marks.localScale = Vector3.one * .55f; marks.localRotation = Quaternion.Euler(0, 270, 0); Bottom(marks, new Vector3(-9.78f, 1.1f, 10.1f));
            if (!output.Find("SkylightGrille_0"))
            {
                for (int i = 0; i < 5; i++) Box("SkylightGrille_" + i, new Vector3(-5.2f + i * 1.1f, 4.13f, 13.4f), new Vector3(.065f, .07f, 4.6f), Material("V4_RustyMetal"), false);
                for (int i = 0; i < 3; i++) Box("SkylightCrossbar_" + i, new Vector3(-3, 4.13f, 11.1f + i * 2.3f), new Vector3(4.4f, .07f, .065f), Material("V4_RustyMetal"), false);
            }
            var rug = output.Find("WornEntryRug");
            foreach (var filter in rug.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.sharedMesh.name.StartsWith("Authored_"))
                {
                    float bottom = filter.GetComponent<Renderer>().bounds.min.y;
                    var mesh = WaitingRoomRugMesh.Create("Dorm_FadedPatternRug");
                    AssetDatabase.AddObjectToAsset(mesh, Bundle); Undo.RecordObject(filter, "러그 무늬"); filter.sharedMesh = mesh;
                    Undo.RecordObject(filter.transform, "러그 바닥 높이"); filter.transform.position += Vector3.up * (bottom - filter.GetComponent<Renderer>().bounds.min.y);
                }
                var renderer = filter.GetComponent<Renderer>(); Undo.RecordObject(renderer, "바랜 러그"); renderer.sharedMaterial = Variant("Dorm_FadedRug", "V4_Harlequin", new Color(.55f, .5f, .42f), Vector2.one);
            }
        }

        static void Light(string name, Vector3 p, Color c, float intensity, float range)
        { var t = Group(output, name); t.localPosition = p; var l = Undo.AddComponent<Light>(t.gameObject); l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None; }
        static Transform Clone(string name, Transform[] sources, Vector3 at, float yaw = 0, float scale = 1, bool solid = false)
        {
            if (sources.Length == 0 || sources.Any(t => !t)) throw new InvalidOperationException("원본 없음: " + name);
            var root = Group(output, name); root.position = Vector3.zero;
            foreach (var source in sources)
            {
                var copy = Object.Instantiate(source.gameObject, root); Undo.RegisterCreatedObjectUndo(copy, "숙소 에셋 복제"); copy.name = source.name;
                copy.transform.SetPositionAndRotation(source.position, source.rotation); copy.transform.localScale = source.lossyScale; copy.SetActive(true);
                foreach (var c in copy.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                foreach (var b in copy.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(b);
                foreach (var l in copy.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l);
                foreach (var t in copy.GetComponentsInChildren<TextMesh>(true)) Object.DestroyImmediate(t.gameObject);
            }
            var bnd = Bounds(root); var offset = new Vector3(bnd.center.x, bnd.min.y, bnd.center.z);
            foreach (Transform t in root) t.position -= offset;
            root.localRotation = Quaternion.Euler(0, yaw, 0); root.localScale = Vector3.one * scale; root.position = g.TransformPoint(at);
            if (solid) ColliderFor(root); return root;
        }
        static Renderer[] Renderers(Transform t) => t.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer) && !r.GetComponent<TextMesh>()).ToArray();
        static Bounds Bounds(Transform t) { var rs = Renderers(t); var b = rs[0].bounds; foreach (var r in rs.Skip(1)) b.Encapsulate(r.bounds); return b; }
        static void Bottom(Transform t, Vector3 at) { var b = Bounds(t); t.position += g.TransformPoint(at) - new Vector3(b.center.x, b.min.y, b.center.z); }
        static void ColliderFor(Transform t)
        {
            var b = Bounds(t); var local = new Bounds(t.InverseTransformPoint(b.center), Vector3.zero);
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                local.Encapsulate(t.InverseTransformPoint(b.center + Vector3.Scale(b.extents, new Vector3(x, y, z))));
            var c = Undo.AddComponent<BoxCollider>(t.gameObject); c.center = local.center; c.size = local.size;
        }
        static Transform Box(string name, Vector3 at, Vector3 size, Material mat, bool solid, Vector3 rotation = default)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); Undo.RegisterCreatedObjectUndo(go, "숙소 구조"); go.name = name; go.transform.SetParent(output, false);
            go.transform.localPosition = at; go.transform.localScale = size; go.transform.localRotation = Quaternion.Euler(rotation); go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>()); return go.transform;
        }
        static Transform Group(Transform parent, string name) { var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "숙소 단계"); go.transform.SetParent(parent, false); return go.transform; }
        static Material Material(string name) => src.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).First(m => m && m.name == name);
        static Material Variant(string name, string original, Color color, Vector2 tiling)
        {
            var mat = AssetDatabase.LoadAllAssetsAtPath(Bundle).OfType<Material>().FirstOrDefault(m => m.name == name);
            if (mat) return mat;
            if (!AssetDatabase.LoadMainAssetAtPath(Bundle)) AssetDatabase.CreateAsset(new Mesh { name = "DormitoryLayoutAssets" }, Bundle);
            mat = new Material(Material(original)) { name = name, color = color, mainTextureScale = tiling };
            AssetDatabase.AddObjectToAsset(mat, Bundle); return mat;
        }
        static string Signature(IEnumerable<Transform> objects)
        {
            var text = new System.Text.StringBuilder();
            foreach (var t in objects)
            {
                if (!t) { text.Append("DESTROYED"); continue; }
                text.Append(t.GetInstanceID()).Append(t.name).Append(t.parent ? t.parent.GetInstanceID() : 0).Append(t.localPosition.ToString("R")).Append(t.localRotation.ToString("R")).Append(t.localScale.ToString("R")).Append(t.gameObject.activeSelf).Append(t.gameObject.layer);
                foreach (var c in t.GetComponents<Component>()) if (c && !(c is Transform)) text.Append(EditorJsonUtility.ToJson(c));
            }
            return Hash128.Compute(text.ToString()).ToString();
        }
    }
}
