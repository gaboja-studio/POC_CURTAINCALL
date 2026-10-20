using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.EditorTools
{
    /// <summary>프로토타입은 읽기 전용으로 두고, 그레이박스 가구 외형과 방별 소품만 복제한다.</summary>
    public static class WaitingRoomFurnisher
    {
        const string ScenePath = "Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity";
        const string BundlePath = "Assets/Scenes/Tests/WaitingRoom/Base/Prefabs/WaitingRoomFurnishingMeshes.asset";
        const string GeneratedName = "Furnishings";
        static Transform greybox, prototype, kitPrototype, output;
        static int placed, hidden;
        public static string LastReport { get; private set; }

        [MenuItem("Tools/CurtainCall/대기실/프로토타입 가구 교체·공간 채우기")]
        public static void ApplyCurrent() => Apply(SceneManager.GetActiveScene());

        public static GameObject Apply(Scene scene)
        {
            if (EditorApplication.isPlaying || scene.path != ScenePath)
                throw new InvalidOperationException("저장된 WaitingRoomBase 편집 씬에서만 실행합니다.");
            greybox = scene.GetRootGameObjects().Single(g => g.name == "WaitingRoomGreybox").transform;
            if (greybox.Find("Mood/InteriorArt") != null)
                throw new InvalidOperationException("실내 고도화가 적용된 씬입니다. 기존 가구 전체 재생성을 중지합니다.");
            prototype = scene.GetRootGameObjects().Single(g => g.name == "대기실").transform;
            kitPrototype = scene.GetRootGameObjects().Single(g => g.name == "WaitingRoom").transform;
            // 필수 원본이 없으면 기존 배치를 지우기 전에 중단한다.
            foreach (var path in new[] { "대기실_본관/숙소/숙소_01/가구/철제2층침대", "화장실", "소품실/가구/선반", "분장실/FullMirror", "분장실/FullMirrorFrame" })
                Source(path);
            string before = PrototypeSignature();
            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("대기실 가구·공간 고도화");
            placed = hidden = 0;
            try
            {
                var mood = greybox.Find("Mood");
                if (mood == null) throw new InvalidOperationException("먼저 분위기 이식을 적용해야 합니다.");
                var previous = mood.Find(GeneratedName);
                if (previous != null) Undo.DestroyObjectImmediate(previous.gameObject);
                output = Group(mood, GeneratedName);
                Dormitory();
                Restroom();
                PropRoom();
                JunkStorage();
                ExpertRoom();
                LobbyAndCorridor();
                if (before != PrototypeSignature()) throw new InvalidOperationException("프로토타입 원본 변경 감지 — 작업을 되돌립니다.");
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(undo);
                LastReport = $"가구·소품 묶음 {placed}, 숨긴 대역 렌더러 {hidden}, 원본 2개 보존, 생성 루트 {output.name}";
                Debug.Log("[WaitingRoomFurnisher] " + LastReport);
                return output.gameObject;
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        static void Dormitory()
        {
            var p = Group(output, "Dormitory");
            Hide("S02_Dormitory/Beds");
            Mute("Mood/Dressing/Dormitory_Blankets");
            var beds = greybox.Find("S02_Dormitory/Beds");
            foreach (Transform bed in beds)
            {
                var b = BoundsOf(bed);
                Put(p, bed.name + "_Bunk", Source("대기실_본관/숙소/숙소_01/가구/철제2층침대"), LocalBottom(b), 90, true);
            }
            Hide("S02_Dormitory/Lockers");
            Mute("Mood/Rooms/Dormitory/Lockers");
            Put(p, "Lockers_West", Source("대기실_본관/사물함_의상/사물함"), new Vector3(-9.35f, 0, 11.2f), 90, true);
            Hide("S02_Dormitory/Wardrobe");
            Put(p, "Wardrobe_Lockers", Source("대기실_본관/사물함_의상/사물함"), new Vector3(1.45f, 0, 20.3f), 180, true, .8f);
            var personal = Source("대기실_본관/숙소/숙소_01/가구");
            Put(p, "BedsideTrunk_1", Child(personal, "트렁크"), new Vector3(-8.4f, 0, 16.8f), 5, true);
            Put(p, "BedsideTrunk_2", Child(personal, "트렁크"), new Vector3(-.4f, 0, 16.6f), -7, true);
            Put(p, "BedsideBear", Kit("R02_Dormitory/Props/SM_Prop_Toy_Bear_01"), new Vector3(-6.55f, .52f, 19.7f), 0);
            Put(p, "PersonalDesk", Source("소품실/가구/SM_WW2_Table02_c1_LOD0"), new Vector3(-8.7f, 0, 14.1f), 90, true, .85f);
            Put(p, "DeskChair", Source("대기실_본관/잡동사니/SM_WW2_WoodenChair_c1_LOD0"), new Vector3(-7.65f, 0, 14.1f), -90, true);
            Put(p, "DeskTrunk", Child(personal, "SM_PAD_CardboardBox01_c1_LOD0"), new Vector3(-8.7f, .74f, 14.4f), 10, false, .65f);
            Put(p, "SharedRestTable", Kit("R07_Lounge/Props/SM_Prop_Table_01"), new Vector3(-5.8f, 0, 12.6f), 0, true);
            Put(p, "RestChair_North", Kit("R07_Lounge/Props/SM_Prop_Chair_Folding_01"), new Vector3(-5.8f, 0, 13.75f), 0, true);
            Put(p, "RestChair_East", Kit("R07_Lounge/Props/SM_Prop_Chair_Folding_01"), new Vector3(-4.65f, 0, 12.6f), 90, true);
            Put(p, "TablePersonalBox", Child(personal, "SM_PAD_CardboardBox01_c1_LOD0"), new Vector3(-5.95f, .74f, 12.7f), 15, false, .65f);
        }

        static void Restroom()
        {
            var p = Group(output, "Restroom");
            var bathroom = Source("화장실");
            Hide("S02_Dormitory/Restroom/Toilet", true);
            Hide("S02_Dormitory/Restroom/Sinks");
            Hide("S02_Dormitory/Restroom/Mirror");
            for (int i = 0; i < 3; i++)
                Put(p, "Toilet_" + (i + 1), Kit("R03_Restroom/Props/SM_Prop_Toilet_01"), new Vector3(5.2f + i * 1.8f, 0, 20.35f), 0, true);
            Put(p, "Sink_Left", Kit("R03_Restroom/Props/SM_Prop_Sink_01"), new Vector3(5.65f, 0, 9.65f), 0, true);
            Put(p, "Sink_Right", Kit("R03_Restroom/Props/SM_Prop_Sink_01"), new Vector3(8, 0, 9.65f), 0, true);
            Put(p, "SinkMirrors", Children(bathroom, "Mirror"), new Vector3(6.85f, 1.35f, 9.22f), 270);
            var partitions = greybox.Find("S02_Dormitory/Restroom").Cast<Transform>().Where(t => t.name == "Partition").ToArray();
            foreach (var t in partitions)
            {
                var b = BoundsOf(t);
                HideTransform(t);
                Put(p, "StallPartition", Child(bathroom, "Partition"), LocalBottom(b), 0, true);
            }
            for (int i = 0; i < 3; i++)
                Put(p, "StallDoor_Open_" + i, Child(bathroom, "StallDoor_Open"), new Vector3(4.65f + i * 1.8f, .15f, 18.95f), 180, true);
            Put(p, "Drain_Stalls", Child(bathroom, "Drain"), new Vector3(6.5f, .012f, 18.05f), 0);
        }

        static void PropRoom()
        {
            var p = Group(output, "PropRoom");
            Hide("S04_PropRoom/CostumeStorage");
            Hide("S04_PropRoom/DressingWardrobe");
            Hide("S04_PropRoom/OldDolls");
            Hide("S04_PropRoom/MascotSuit");
            // 이미 이식된 일반 선반·전구 화장대는 유지하고, 남아 있는 도형 대역을 교체한다.
            Put(p, "CostumeRack_West", Source("분장실/의상걸이"), new Vector3(-8.85f, 0, -17.5f), 0, true);
            Put(p, "DressingCupboard", Kit("R08_JunkStorage_Hidden/Props/SM_Prop_Cupboard_Broken_01"), new Vector3(8.8f, 0, -19.5f), 180, true);
            Put(p, "FullLengthMirror", new[] { Source("분장실/FullMirrorFrame"), Source("분장실/FullMirror") }, new Vector3(7.3f, .05f, -20.15f), 270);
            Put(p, "OldDoll_1", Kit("R02_Dormitory/Props/SM_Prop_Toy_Bear_01"), new Vector3(2.5f, 0, -19.65f), 10, false, 1.3f);
            Put(p, "OldDoll_2", Kit("R02_Dormitory/Props/SM_Prop_Toy_Bear_01"), new Vector3(3.1f, 0, -19.8f), -25);
            Put(p, "MascotBody", Kit("R04_DressingRoom/Props/SM_Prop_Mannequin_Female_02"), new Vector3(5, 0, -19.7f), 0, true);
            Put(p, "CostumeDisplay", Kit("R04_DressingRoom/Props/SM_Prop_Mannequin_Male_02"), new Vector3(6.05f, 0, -19.7f), 15, true);
            HandoverShelf(p);
            // 기능 선반 네 칸은 비워 두고 일반 선반과 작업대 주변에 소품을 모은다.
            Put(p, "RepairShelf", Kit("R06_PropRoom/Props/SM_Prop_WorkShelf_01"), new Vector3(-5.8f, 0, -18.75f), 180, true);
            Put(p, "WorkMannequin", Kit("R02_Dormitory/Props/SM_Prop_Mannequin_Male_01"), new Vector3(-5.25f, 0, -14.6f), -20, true);
            Put(p, "WorkCrate", Kit("R02_Dormitory/Props/SM_Prop_Crate_01"), new Vector3(-6.5f, 0, -16.6f), 5, true, .85f);
        }

        static void HandoverShelf(Transform p)
        {
            var shelf = greybox.Find("S04_PropRoom/HandoverShelf_4Tier/Shelf");
            var boards = shelf.Cast<Transform>().Where(t => t.name.StartsWith("Board_")).OrderBy(t => t.name).ToArray();
            if (boards.Length != 4) throw new InvalidOperationException("물려줄 선반은 4칸이어야 합니다.");
            var b = BoundsOf(shelf);
            HideTransform(shelf);
            var clone = Put(p, "HandoverShelf_Visual", Source("소품실/가구/선반"), LocalBottom(b), 0);
            var cb = BoundsOf(clone);
            clone.localScale = Vector3.Scale(clone.localScale, new Vector3(4f / cb.size.x, 2.5f / cb.size.y, .6f / cb.size.z));
            cb = BoundsOf(clone);
            clone.position += greybox.TransformPoint(new Vector3(-1, 0, -20.05f)) - new Vector3(cb.center.x, cb.min.y, cb.center.z);
            var visualBoards = clone.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Board").OrderBy(t => t.position.y).ToArray();
            if (visualBoards.Length != 4) throw new InvalidOperationException("원본 선반 판자 수가 바뀌었습니다.");
            for (int i = 0; i < boards.Length; i++)
            {
                var at = visualBoards[i].position;
                at.y = boards[i].position.y;
                visualBoards[i].position = at;
            }
            AddCollider(clone);
        }

        static void JunkStorage()
        {
            var p = Group(output, "JunkStorage");
            Hide("S05_JunkStorage/PerformanceLogDesk");
            Hide("S05_JunkStorage/OldCostume_1");
            Hide("S05_JunkStorage/OldCostume_2");
            Hide("S05_JunkStorage/OldCostume_3");
            Hide("S05_JunkStorage/CircusRecords");
            Put(p, "ArchiveDesk", Source("소품실/가구/SM_WW2_Table02_c1_LOD0"), new Vector3(-18.5f, 0, 7), 0, true, .92f);
            Put(p, "ArchiveChair", Source("소품실/가구/SM_WW2_WoodenChair_c1_LOD0"), new Vector3(-18.45f, 0, 5.8f), 180, true);
            for (int i = 0; i < 3; i++)
            {
                var target = greybox.Find("S05_JunkStorage/OldCostume_" + (i + 1));
                var b = BoundsOf(target);
                Put(p, "PreviousPerformer_" + i, Kit(i == 1 ? "R04_DressingRoom/Props/SM_Prop_Mannequin_Female_02" : "R04_DressingRoom/Props/SM_Prop_Mannequin_Male_02"), LocalBottom(b), 90 + i * 8, true);
            }
            Put(p, "BrokenWardrobe", Kit("R08_JunkStorage_Hidden/Props/SM_Prop_Cupboard_Broken_01"), new Vector3(-12.1f, 0, 4.4f), 90, true);
            Put(p, "RetiredTorso", Kit("R08_JunkStorage_Hidden/Props/SM_Prop_Mannequin_Torso_01"), new Vector3(-18.75f, 0, -4.7f), -20);
            Put(p, "OpenArchiveCrate", Kit("R08_JunkStorage_Hidden/Props/SM_Prop_Crate_Open_01"), new Vector3(-17.4f, 0, -5.3f), 10, true);
            Put(p, "DamagedBooks", Kit("R08_JunkStorage_Hidden/Props/SM_Prop_BurnPile_Books_01"), new Vector3(-19.6f, 0, -5.6f), 5, false, .55f);
            Put(p, "ArchiveBookshelf", Kit("R08_JunkStorage_Hidden/Props/SM_Prop_Bookshelf_01"), new Vector3(-21.35f, 0, 5.8f), 90, true, 1.4f);
            Put(p, "StoredTrunk", Source("대기실_본관/잡동사니/트렁크"), new Vector3(-13.4f, 0, -6.1f), 10, true);
            Put(p, "DiscardedPropsPile", Kit("R08_JunkStorage_Hidden/Props/SM_Prop_TrashPile_05"), new Vector3(-17.8f, 0, -2.8f), 8, true, .55f);
        }

        static void ExpertRoom()
        {
            var p = Group(output, "ExpertRoom");
            Hide("S08_ExpertRoom/Sofa");
            Hide("S08_ExpertRoom/Table");
            Put(p, "WaitingSofa", Kit("R07_Lounge/Props/SM_Prop_Couch_01"), new Vector3(17.1f, 0, 6.5f), -90, true);
            Put(p, "CoffeeTable", Kit("R07_Lounge/Props/SM_Prop_CoffeeTable_02"), new Vector3(15.5f, 0, 6.5f), -90, true);
            Put(p, "ServiceShelf", Kit("R05_Shop/Props/SM_Prop_Medical_Shelf_01"), new Vector3(16.85f, 0, 10.75f), 180, true);
            Put(p, "ServiceDesk", Source("소품실/가구/SM_WW2_Table02_c1_LOD0"), new Vector3(13.7f, 0, 11.2f), 0, true, .8f);
            Put(p, "ServiceStorage", Source("대기실_본관/잡동사니/트렁크"), new Vector3(16.8f, 0, 9.3f), 0, true);
            Put(p, "ServiceJar", Source("상점/가구/Jar"), new Vector3(13.65f, .7f, 11.2f), 0);
        }

        static void LobbyAndCorridor()
        {
            var p = Group(output, "Lobby");
            // 중앙 스폰과 문 앞은 비워 두고, 생활·수선 소품을 기존 가구 가장자리에 배치한다.
            Put(p, "PackedTrunk", Source("대기실_본관/잡동사니/트렁크"), new Vector3(-8.3f, 0, -5.8f), 12, true);
            Put(p, "WaitingChair", Source("대기실_본관/잡동사니/SM_WW2_WoodenChair_c1_LOD0"), new Vector3(-7.0f, 0, -5.8f), 15, true);
            Put(p, "CostumeStand", Kit("R04_DressingRoom/Props/SM_Prop_Mannequin_Female_02"), new Vector3(8.6f, 0, -7.8f), -20, true);
            var corridor = Group(output, "Corridor");
            Put(corridor, "ReadyPropsCrate", Kit("R02_Dormitory/Props/SM_Prop_Crate_01"), new Vector3(35.5f, 0, -1.9f), 8, true, .8f);
            Put(corridor, "ReadyPropsTrunk", Source("대기실_본관/잡동사니/트렁크"), new Vector3(37.2f, 0, -1.9f), -8, true);
        }

        static Transform Source(string path) => prototype.Find(path) ?? throw new InvalidOperationException("원본 없음: 대기실/" + path);
        static Transform Kit(string path) => kitPrototype.Find(path) ?? throw new InvalidOperationException("원본 없음: WaitingRoom/" + path);
        static Transform Child(Transform p, string name) => p.Cast<Transform>().First(t => t.name == name);
        static Transform[] Children(Transform p, params string[] names) => p.Cast<Transform>().Where(t => names.Contains(t.name)).ToArray();
        static Vector3 LocalBottom(Bounds b) => greybox.InverseTransformPoint(new Vector3(b.center.x, b.min.y, b.center.z));

        static Transform Group(Transform p, string name)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "대기실 가구 생성");
            go.transform.SetParent(p, false);
            return go.transform;
        }

        static Transform Put(Transform p, string name, Transform src, Vector3 at, float yaw = 0, bool solid = false, float scale = 1) => Put(p, name, new[] { src }, at, yaw, solid, scale);
        static Transform Put(Transform p, string name, Transform[] sources, Vector3 at, float yaw = 0, bool solid = false, float scale = 1)
        {
            if (sources.Length == 0 || sources.Any(s => s == null || s.IsChildOf(greybox)))
                throw new InvalidOperationException("프로토타입 원본만 복제할 수 있습니다: " + name);
            var group = Group(p, name);
            group.position = Vector3.zero;
            foreach (var src in sources)
            {
                var copy = Object.Instantiate(src.gameObject, group, false);
                copy.name = src.name;
                Undo.RegisterCreatedObjectUndo(copy, "프로토타입 가구 복제");
                copy.transform.SetPositionAndRotation(src.position, src.rotation);
                copy.transform.localScale = src.lossyScale;
                copy.SetActive(true);
                foreach (var c in copy.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                foreach (var b in copy.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(b);
                foreach (var l in copy.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l);
                foreach (var text in copy.GetComponentsInChildren<TextMesh>(true)) Object.DestroyImmediate(text.gameObject);
                foreach (var mf in copy.GetComponentsInChildren<MeshFilter>(true))
                    mf.sharedMesh = Persistent(mf.sharedMesh);
                foreach (var renderer in copy.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(Persistent).ToArray();
            }
            var bounds = BoundsOf(group);
            var offset = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            foreach (Transform child in group) child.position -= offset;
            group.localRotation = Quaternion.Euler(0, yaw, 0);
            group.localScale = Vector3.one * scale;
            group.position = greybox.TransformPoint(at);
            if (solid) AddCollider(group);
            placed++;
            return group;
        }

        static T Persistent<T>(T value) where T : Object
        {
            if (value == null || EditorUtility.IsPersistent(value)) return value;
            string key = value.name + "_" + Hash128.Compute(EditorJsonUtility.ToJson(value));
            var existing = AssetDatabase.LoadAllAssetsAtPath(BundlePath).OfType<T>().FirstOrDefault(v => v.name == key);
            if (existing != null) return existing;
            if (AssetDatabase.LoadMainAssetAtPath(BundlePath) == null)
                AssetDatabase.CreateAsset(new Mesh { name = "WaitingRoomFurnishingMeshes" }, BundlePath);
            var copy = Object.Instantiate(value);
            copy.name = key;
            AssetDatabase.AddObjectToAsset(copy, BundlePath);
            return copy;
        }

        static Bounds BoundsOf(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer) && r.GetComponent<TextMesh>() == null).ToArray();
            if (rs.Length == 0) throw new InvalidOperationException("외형 없는 가구: " + t.name);
            var b = rs[0].bounds;
            foreach (var r in rs.Skip(1)) b.Encapsulate(r.bounds);
            return b;
        }

        static void AddCollider(Transform t)
        {
            var corners = new List<Vector3>();
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r.GetComponent<TextMesh>() != null) continue;
                var b = r.bounds;
                for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                    corners.Add(t.InverseTransformPoint(b.center + Vector3.Scale(b.extents, new Vector3(x, y, z))));
            }
            var bounds = new Bounds(corners[0], Vector3.zero);
            foreach (var v in corners) bounds.Encapsulate(v);
            var collider = Undo.AddComponent<BoxCollider>(t.gameObject);
            collider.center = bounds.center;
            collider.size = bounds.size;
        }

        static void Hide(string path, bool allNames = false)
        {
            if (!allNames) { HideTransform(greybox.Find(path)); return; }
            int slash = path.LastIndexOf('/');
            var p = greybox.Find(path.Substring(0, slash));
            foreach (Transform t in p) if (t.name == path.Substring(slash + 1)) HideTransform(t);
        }
        static void HideTransform(Transform t)
        {
            if (t == null) throw new InvalidOperationException("그레이박스 대역 없음");
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (r.GetComponent<TextMesh>() != null) continue;
                Undo.RecordObject(r, "그레이박스 외형 교체");
                r.enabled = false;
                hidden++;
            }
            foreach (var c in t.GetComponentsInChildren<Collider>(true))
            {
                if (c.isTrigger) continue;
                Undo.RecordObject(c, "가구 충돌체 교체");
                c.enabled = false;
            }
        }
        static void Mute(string path)
        {
            var t = greybox.Find(path);
            if (t == null) return;
            Undo.RecordObject(t.gameObject, "겹치는 장식 숨기기");
            t.gameObject.SetActive(false);
        }

        public static string PrototypeSignature()
        {
            var text = new StringBuilder();
            foreach (var root in new[] { prototype, kitPrototype })
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    text.Append(t.GetInstanceID()).Append(t.name).Append(EditorJsonUtility.ToJson(t)).Append(t.gameObject.activeSelf);
                    foreach (var c in t.GetComponents<Component>())
                        if (c != null && !(c is Transform)) text.Append(EditorJsonUtility.ToJson(c));
                }
            return Hash128.Compute(text.ToString()).ToString();
        }
    }
}
