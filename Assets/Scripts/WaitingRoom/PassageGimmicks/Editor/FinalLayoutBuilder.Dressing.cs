using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CurtainCall.WaitingRoom.PassageGimmicks
{
    // 7단계: 새 구역 분위기(2026-11-12 사용자: 텅 비고 생활감이 없고 톤이 다르다). 다시 실행할 수 있다.
    // 기존 물체(벽·가구·사용자가 손본 것)는 건드리지 않고 FinalLayout/Dressing 아래에만 더한다. 조명 값만 예외로 고친다.
    // - 구조: 벽 기둥(H빔), 천장을 지나는 배관·전선, 벽 녹 얼룩 → 폐공장 톤을 기존 방과 맞춘다.
    // - 생활 흔적: 물건을 "누가 쓰던 자리" 단위로 모은다(공용 탁자, 소파와 빨랫줄, 침대 옆, 바닥 잠자리, 짐 더미).
    // - 조명: 따뜻한 전구색으로 통일하고 천장 등은 낮춰, 탁자·침대 옆 작은 등이 자리마다 빛 웅덩이를 만들게 한다.
    public static partial class FinalLayoutBuilder
    {
        const string Prop = Apocalypse + "SM_Prop_";
        static readonly Color WarmBulb = new(1f, .74f, .46f);
        // 생활 장소 홀 안쪽 면(FinalLayoutBuilder.Rooms와 같은 값).
        const float HallX0 = -20f, HallX1 = 2f, HallZ0 = 37f, HallZ1 = 45.35f, HallH = 7.5f;

        [MenuItem("CurtainCall/Waiting Room/Final Layout/7. Dress Old Quarters")]
        static void DressQuartersMenu() => Debug.Log(DressQuarters());

        public static string DressQuarters()
        {
            var layout = GameObject.Find(LayoutPath)?.transform;
            var quarters = layout != null ? layout.Find(RootName + "/NewRooms/OldTroupeQuarters") : null;
            if (quarters == null) return "생활 장소를 찾지 못했습니다(3단계를 먼저 실행하세요).";
            int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Final layout: dress old quarters");
            var old = layout.Find(RootName + "/Dressing/Quarters"); if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var root = Child(Child(Child(layout, RootName), "Dressing"), "Quarters");
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Dress quarters");

            // ---- 구조 ----
            var structure = Child(root, "Structure");
            // 벽 기둥: 문·침대·가구 사이 빈 벽면에 세운다. 바닥에서 천장까지.
            foreach (float x in new[] { -16.1f, -12.6f, -8.3f, 1.0f }) Column(structure, x, HallZ0 + .2f);
            foreach (float x in new[] { -16.15f, -12.05f, -7.95f, -3.7f, .5f }) Column(structure, x, HallZ1 - .2f);
            // 천장 배관 두 줄(천장에 붙여 동→서)과 북벽 전선 두 줄. 기계실 입구에서 갈라져 온 설비가 이 방을 지나간다.
            // 홀 서쪽은 컨테이너 마당으로 트여 있으므로 마당 서벽(x -26.7)까지 이어 벽에서 벽으로 닿게 한다(끝이 허공에 뜨지 않게).
            const float YardWestX = -26.7f;
            foreach (float z in new[] { 38.4f, 39.0f }) Run(structure, new Vector3(HallX1, HallH - .25f, z), Vector3.left, HallX1 - YardWestX);
            foreach (float y in new[] { 5.6f, 5.75f }) Wire(structure, new Vector3(HallX1, y, HallZ1 - .05f), Vector3.left, HallX1 - YardWestX);
            // 녹 얼룩: 배관 아래 남벽과 북·동벽.
            var streak = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Tests/WaitingRoom/Base/LayoutA/LA_RustStreak.mat");
            var bloom = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Tests/WaitingRoom/Base/LayoutA/LA_RustBloom.mat");
            foreach (var (x, y, w, h) in new[] { (-18.8f, 5.2f, 1.2f, 4.2f), (-10.5f, 5.6f, 1.0f, 3.4f), (-5.2f, 5.0f, 1.4f, 4.6f), (-.6f, 5.8f, .9f, 3.0f) })
                Stain(structure, streak, new Vector3(x, y, HallZ0 + .012f), 180f, w, h);
            foreach (var (x, y, w, h) in new[] { (-14.2f, 4.9f, 1.1f, 4.8f), (-5.8f, 5.3f, 1.3f, 4.0f), (-18.3f, 1.3f, 2.2f, 2.2f) })
                Stain(structure, x < -18f ? bloom : streak, new Vector3(x, y, HallZ1 - .012f), 0f, w, h);
            Stain(structure, bloom, new Vector3(HallX1 - .012f, 1.5f, 39.4f), 90f, 2.4f, 2.6f);

            // ---- 공용 탁자(기존 깔개 위) ----
            var table = Child(root, "CommonTable");
            Put(table, "Table_02", new Vector3(-10f, 0, 40.6f), 0);
            Put(table, "Chair_Folding_01", new Vector3(-10.6f, 0, 41.45f), 180f);
            Put(table, "Chair_Folding_01", new Vector3(-9.45f, 0, 41.5f), 160f);
            Put(table, "Chair_Folding_01", new Vector3(-10.1f, 0, 39.75f), 15f);
            Put(table, "Camp_Chair_01", new Vector3(-11.45f, 0, 40.5f), 80f);
            Put(table, "Radio_01", new Vector3(-10.6f, .71f, 40.55f), 20f);
            Put(table, "Pan_01", new Vector3(-9.7f, .71f, 40.45f), -30f);
            Put(table, "Papers_02", new Vector3(-10.15f, .715f, 40.8f), 40f);
            Lamp(table, "Lamp_01", new Vector3(-9.3f, .71f, 40.85f), 4.5f, 5.5f);
            Put(table, "Blackboard_01", new Vector3(-12.6f, 0, 42.2f), 120f);

            // ---- 소파와 빨랫줄(남벽, 기둥 두 개 사이) ----
            var lounge = Child(root, "Lounge");
            Put(lounge, "Couch_01", new Vector3(-10.45f, 0, 37.6f), 0);
            Put(lounge, "CoffeeTable_02", new Vector3(-10.4f, 0, 38.75f), 4f);
            Put(lounge, "Papers_03", new Vector3(-10.3f, .535f, 38.75f), -20f);
            var line = Spawn(lounge, Factory + "SM_Ceiling_Line.prefab", "Clothesline");
            line.SetPositionAndRotation(new Vector3(-12.45f, 3f, HallZ0 + .25f), Quaternion.Euler(0, 90f, 0)); line.localScale = new Vector3(1, 1, 4.0f / 4f);
            foreach (float x in new[] { -11.6f, -10.4f, -9.3f })
                Spawn(lounge, Factory + "SM_MVV_HangingCloth05_c1_LOD0.prefab", "Laundry").SetPositionAndRotation(new Vector3(x, 3f, HallZ0 + .25f), Quaternion.Euler(0, 90f, 0));

            // ---- 침대 옆 ----
            var bedside = Child(root, "Bedside");
            Put(bedside, "CardboardBox_01", new Vector3(-16.55f, 0, 43.75f), 12f);
            Lamp(bedside, "Study_Lamp_01", new Vector3(-16.55f, .47f, 43.75f), 2.5f, 3.8f, 200f);
            Put(bedside, "CardboardBox_01", new Vector3(-8.35f, 0, 43.8f), -8f);
            Put(bedside, "Toy_Bear_01", new Vector3(-8.35f, .47f, 43.8f), 170f);
            Put(bedside, "Sleeping_Bag_Rolled_01", new Vector3(-12.95f, .2f, 43.85f), 75f);
            Put(bedside, "Papers_02", new Vector3(-14.6f, .005f, 43.3f), 60f);

            // ---- 바닥 잠자리(북동 구석, 침대가 모자라 바닥에서 잔 자리) ----
            var floorBed = Child(root, "FloorBed");
            Put(floorBed, "BedMatress_01", new Vector3(1.35f, 0, 44.1f), 0);
            Put(floorBed, "Sleeping_Bag_01", new Vector3(.1f, 0, 43.85f), 6f);
            Put(floorBed, "Toy_Bear_01", new Vector3(1.4f, .2f, 44.7f), 200f);
            Put(floorBed, "Papers_03", new Vector3(-.2f, .005f, 42.5f), 30f);

            // ---- 짐 더미(서쪽 남벽)와 쓰레기 봉지(동쪽) ----
            var piles = Child(root, "Piles");
            Put(piles, "CardboardBox_Wall_01", new Vector3(-18.1f, 0, 37.72f), 0);
            Put(piles, "TrashBag_01", new Vector3(.95f, 0, 39.0f), 25f);
            Put(piles, "TrashBag_03", new Vector3(1.25f, .03f, 39.9f), -40f);
            Put(piles, "Papers_01", new Vector3(-4.6f, .005f, 42.0f), 15f);

            // ---- 벽에 붙은 실종 전단(동벽) ----
            var wall = Child(root, "Wall");
            Put(wall, "Sign_MissingPapers_01", new Vector3(HallX1 - .005f, 1.9f, 38.3f), -90f);
            Put(wall, "Sign_MissingPapers_02", new Vector3(HallX1 - .005f, 1.75f, 40.1f), -90f);

            // ---- 조명 톤 ----
            int tuned = 0;
            foreach (var light in quarters.GetComponentsInChildren<Light>(true))
            {
                Undo.RecordObject(light, "Warm light");
                light.color = WarmBulb; if (light.intensity > 9f) light.intensity = 8f; tuned++;
            }
            var power = layout.GetComponentInChildren<NightPower>(true);
            if (power != null)
            {
                var pso = new SerializedObject(power); var circuit = pso.FindProperty("circuit");
                for (int i = circuit.arraySize - 1; i >= 0; i--) if (circuit.GetArrayElementAtIndex(i).objectReferenceValue == null) circuit.DeleteArrayElementAtIndex(i);
                var have = Enumerable.Range(0, circuit.arraySize).Select(i => circuit.GetArrayElementAtIndex(i).objectReferenceValue).ToHashSet();
                foreach (var light in root.GetComponentsInChildren<Light>(true).Where(l => !have.Contains(l)))
                { circuit.InsertArrayElementAtIndex(circuit.arraySize); circuit.GetArrayElementAtIndex(circuit.arraySize - 1).objectReferenceValue = light; }
                pso.ApplyModifiedProperties();
            }

            int count = root.GetComponentsInChildren<Renderer>().Length;
            Undo.CollapseUndoOperations(undo); EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
            return $"생활 장소: 물체 {root.GetComponentsInChildren<Transform>().Length}개(그리는 것 {count}개) 추가, 조명 {tuned}개 톤 조정";
        }

        [MenuItem("CurtainCall/Waiting Room/Final Layout/8. Dress Corridor And Side Rooms")]
        static void DressRestMenu() => Debug.Log(DressCorridorAndSideRooms());

        // 사무실 복도(버려진 사무실)와 생활 장소의 화장실·소품실. 7단계와 같은 원칙: 기존 물체는 그대로, Dressing 아래에만 더한다.
        public static string DressCorridorAndSideRooms()
        {
            var layout = GameObject.Find(LayoutPath)?.transform;
            var rooms = layout != null ? layout.Find(RootName + "/NewRooms") : null;
            if (rooms == null) return "새 공간을 찾지 못했습니다(3단계를 먼저 실행하세요).";
            int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Final layout: dress corridor and side rooms");
            var dressing = Child(Child(layout, RootName), "Dressing");
            foreach (var name in new[] { "Corridor", "Washroom", "PropRoom" }) { var old = dressing.Find(name); if (old != null) Undo.DestroyObjectImmediate(old.gameObject); }
            var streak = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Tests/WaitingRoom/Base/LayoutA/LA_RustStreak.mat");
            var bloom = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Tests/WaitingRoom/Base/LayoutA/LA_RustBloom.mat");

            // ---- 사무실 복도: 벽 쪽에만 놓아 가운데 3m는 비운다. 북벽 안쪽 면 z50.8, 남벽 z45.9 ----
            var cor = Child(dressing, "Corridor"); Undo.RegisterCreatedObjectUndo(cor.gameObject, "Dress corridor");
            const float N = 50.8f, S = 45.9f;
            // 공장 배관 한 줄이 낮은 천장 바로 아래 북벽을 따라 지나간다(문·창 위).
            // 동쪽 끝은 배관 연결 통로로 트여 있어 끝이 허공에 뜬다 → 동쪽 끝 구석에서 바닥으로 내려가는 수직 배관에 잇는다.
            Run(cor, new Vector3(12.9f, 2.74f, N - .26f), Vector3.left, 25.9f);
            Run(cor, new Vector3(12.9f, 0f, N - .26f), Vector3.up, 2.98f);
            // 북벽: 버려 둔 책상과 의자, 서랍장, 대기 의자, 막아 둔 문.
            Put(cor, "Table_02", new Vector3(-7.6f, 0, N - .5f), 0);
            Put(cor, "Papers_02", new Vector3(-7.9f, .715f, N - .45f), 25f);
            Put(cor, "CardboardBox_05", new Vector3(-7.0f, .71f, N - .45f), -15f);
            Put(cor, "Study_Chair_01", new Vector3(-7.3f, 0, N - 1.45f), 205f);
            Put(cor, "Bookshelf_01", new Vector3(-4.6f, 0, N - .17f), 180f);
            Put(cor, "RubbishBin_01", new Vector3(-2.75f, 0, N - .35f), 0);
            Put(cor, "Barricade_Corrugated_01", new Vector3(-1.5f, 0, N - .24f), 0);
            Put(cor, "Sign_KeepOut_01", new Vector3(-1.5f, 1.85f, N - .1f), 180f);
            Put(cor, "Chair_Plastic_01", new Vector3(1.6f, 0, N - .36f), 180f);
            Put(cor, "Chair_Plastic_01", new Vector3(2.35f, 0, N - .38f), 172f);
            Put(cor, "Shelf_01_Drawer_01", new Vector3(7.0f, 0, N - .24f), 180f);
            Put(cor, "TrashPile_07", new Vector3(-12.2f, .1f, N - .7f), 0);
            // 남벽: 부서진 수납장, 상자 더미, 굴러 온 의자, 쓰레기 봉지.
            Put(cor, "Cupboard_Broken_01", new Vector3(7.6f, .03f, S + .3f), 0);
            Put(cor, "CardboardBox_01", new Vector3(-5.6f, 0, S + .4f), 8f);
            Put(cor, "CardboardBox_01", new Vector3(-4.95f, 0, S + .42f), -12f);
            Put(cor, "CardboardBox_05", new Vector3(-5.55f, .47f, S + .4f), 30f);
            Put(cor, "Chair_Metal_01", new Vector3(.3f, .57f, S + .6f), 35f);
            Put(cor, "TrashBag_05", new Vector3(11.6f, 0, S + .42f), 0);
            Put(cor, "TrashBag_01", new Vector3(12.3f, 0, S + .5f), 60f);
            // 바닥에 흩어진 서류.
            var sheets = new (string, float, float, float)[] { ("Papers_01", -9.2f, 48.9f, 20f), ("Papers_03", -6.4f, 47.2f, 75f), ("Papers_02", -3.6f, 49.3f, 130f), ("Papers_01", 1.4f, 47.6f, -40f),
                ("Papers_03", 4.8f, 49.5f, 10f), ("Papers_02", 6.1f, 46.9f, 95f), ("Papers_03", 9.7f, 48.2f, -65f), ("Papers_02", -11.1f, 47.0f, 5f) };
            foreach (var (prop, x, z, yaw) in sheets) Put(cor, prop, new Vector3(x, .005f, z), yaw);
            // 천장에서 늘어진 전선(꺼진 등 옆과 동쪽).
            foreach (var p in new[] { new Vector3(-4.2f, 3f, 48.95f), new Vector3(-3.85f, 3f, 49.05f), new Vector3(6.5f, 3f, 47.2f) }) Wire(cor, p, Vector3.down, p.x > 0 ? .8f : 1.15f);
            // 배관 아래 녹 자국과 걸레받이 얼룩.
            foreach (var (x, w, h) in new[] { (-12.3f, .8f, 1.5f), (-2.9f, .7f, 1.3f), (7.5f, .9f, 1.6f), (11.7f, .7f, 1.2f) }) Stain(cor, streak, new Vector3(x, 2.5f - h * .5f, N - .012f), 0f, w, h);
            foreach (var (x, w, h) in new[] { (-12.1f, 1.6f, 1.4f), (.4f, 1.3f, 1.2f), (11.2f, 1.8f, 1.5f) }) Stain(cor, bloom, new Vector3(x, h * .5f + .05f, S + .012f), 180f, w, h);
            // 형광등: 색을 조금 누렇게, 밝기를 낮춘다(한 개는 원래 꺼져 있다).
            int tuned = 0;
            foreach (var light in rooms.Find("BackCorridor").GetComponentsInChildren<Light>(true))
            { Undo.RecordObject(light, "Tune tube"); light.color = new Color(1f, .9f, .74f); light.intensity = Mathf.Min(light.intensity, 4.5f); tuned++; }

            // ---- 화장실(x-20~-9.3, z30.5~36.5) ----
            var wash = Child(dressing, "Washroom"); Undo.RegisterCreatedObjectUndo(wash.gameObject, "Dress washroom");
            Put(wash, "Rug_03", new Vector3(-14.7f, .02f, 35.3f), 0);
            Put(wash, "Laundry_WasherTop_01", new Vector3(-11.8f, .76f, 35.78f), 180f);
            Put(wash, "TrashBag_05", new Vector3(-12.9f, 0, 36.0f), 0);
            Put(wash, "Tool_Bucket_02", new Vector3(-18.0f, 0, 32.3f), 40f);
            Put(wash, "Chair_Plastic_01", new Vector3(-19.2f, 0, 31.6f), 60f);
            Put(wash, "Sleeping_Bag_Rolled_02", new Vector3(-19.2f, .62f, 31.6f), 20f);
            Stain(wash, streak, new Vector3(-18.2f, 3.1f, 36.488f), 0f, 1.0f, 2.0f);
            Stain(wash, streak, new Vector3(-12.2f, 3.2f, 30.512f), 180f, 1.2f, 1.8f);
            Stain(wash, bloom, new Vector3(-19.4f, 3.1f, 30.512f), 180f, 1.6f, 1.6f);

            // ---- 소품실(x-8.75~2, z30.5~36.5) ----
            var prop2 = Child(dressing, "PropRoom"); Undo.RegisterCreatedObjectUndo(prop2.gameObject, "Dress prop room");
            Put(prop2, "Workbench_01", new Vector3(-6.9f, 0, 35.98f), 180f);
            Put(prop2, "Rug_01", new Vector3(-3.3f, .01f, 33.7f), 90f);
            Put(prop2, "Chair_Soft_Damaged_01", new Vector3(-6.3f, .03f, 33.6f), 80f);
            Put(prop2, "BookPodium_01", new Vector3(-1.9f, 0, 32.6f), 200f);
            Put(prop2, "CardboardBox_01", new Vector3(1.4f, 0, 30.95f), 5f);
            Put(prop2, "CardboardBox_01", new Vector3(.75f, 0, 31.0f), -10f);
            Put(prop2, "CardboardBox_05", new Vector3(1.35f, .47f, 30.95f), 40f);
            Put(prop2, "Papers_03", new Vector3(-3.0f, .11f, 33.9f), 35f);
            Put(prop2, "Papers_02", new Vector3(-5.0f, .005f, 34.9f), -20f);
            Put(prop2, "Tool_Bucket_01", new Vector3(-5.5f, 0, 35.2f), 0);
            Stain(prop2, streak, new Vector3(-1.2f, 3.2f, 30.512f), 180f, 1.1f, 1.8f);
            Stain(prop2, bloom, new Vector3(1.988f, 2.2f, 33.5f), 90f, 1.8f, 2.0f);

            Undo.CollapseUndoOperations(undo); EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
            return $"복도 {cor.childCount}개 / 화장실 {wash.childCount}개 / 소품실 {prop2.childCount}개 추가, 형광등 {tuned}개 조정";
        }

        static Transform Put(Transform parent, string prop, Vector3 position, float yaw)
        {
            var t = Spawn(parent, Prop + prop + ".prefab", prop);
            t.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            return t;
        }

        // 작은 등: 소품 위에 따뜻한 점 조명을 단다.
        static void Lamp(Transform parent, string prop, Vector3 position, float intensity, float range, float yaw = 0)
        {
            var lamp = Put(parent, prop, position, yaw);
            var go = new GameObject("Glow"); go.transform.SetParent(lamp, false); go.transform.localPosition = Vector3.up * .4f;
            var light = go.AddComponent<Light>(); light.type = LightType.Point; light.color = WarmBulb; light.intensity = intensity; light.range = range; light.shadows = LightShadows.None;
        }

        // H빔 기둥: 가늘게(0.4배) 줄여 벽에 붙이고 4m 두 토막으로 천장까지.
        static void Column(Transform parent, float x, float z)
        {
            for (float y = 0; y < HallH - .01f; y += 4f)
            {
                var beam = Spawn(parent, Factory + "SM_Hbeam_4m.prefab", "Column");
                beam.position = new Vector3(x, y, z); beam.localScale = new Vector3(.4f, Mathf.Min(4f, HallH - y) / 4f, .4f);
            }
        }

        // 벽 얼룩: 벽면에 1cm 띄운 판(yaw 0 = 남쪽을 봄, 180 = 북쪽을 봄, 90 = 서쪽을 봄).
        static void Stain(Transform parent, Material material, Vector3 center, float yaw, float width, float height)
        {
            if (material == null) return;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); quad.name = "Stain"; quad.transform.SetParent(parent, false);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetPositionAndRotation(center, Quaternion.Euler(0, yaw, 0)); quad.transform.localScale = new Vector3(width, height, 1);
            var r = quad.GetComponent<Renderer>(); r.sharedMaterial = material; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
