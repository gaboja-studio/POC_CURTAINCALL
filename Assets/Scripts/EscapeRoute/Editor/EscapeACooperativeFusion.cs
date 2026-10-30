using System;
using System.Linq;
using CurtainCall.HumanCannon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        /// <summary>저장된 A에만 두 협동 연결을 추가한다. 전체 배경 생성기를 호출하지 않는다.</summary>
        public static string AddACooperativeFusion()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != ScenePath || scene.isDirty)
                throw new InvalidOperationException("Saved A edit scene required");
            var bg = GameObject.Find(RootName).transform;
            if (bg.Find("37_ACooperativeFusion") != null) throw new InvalidOperationException("Already applied");
            CompletionMaterials();
            var root = VerticalChild(bg, "37_ACooperativeFusion");
            root.localScale = new Vector3(1 / bg.lossyScale.x, 1 / bg.lossyScale.y, 1 / bg.lossyScale.z);
            var locks = Undo.AddComponent<EscapeCooperativeLocks>(root.gameObject);
            var data = new SerializedObject(locks);
            var list = data.FindProperty("checkpoints"); list.arraySize = 2;
            BuildConveyorCooperativeLock(VerticalChild(root, "04B_ShoulderAndSawPassage"), list.GetArrayElementAtIndex(0));
            BuildAscentCooperativeRelay(VerticalChild(root, "06_PoleCannonTeamRelay"), bg, list.GetArrayElementAtIndex(1));
            data.ApplyModifiedProperties();
            RegisterAllPoles(); Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
            return "A04B roof/side route enclosed; original shoulder clutch and saw-arrival latch; A06 original pole-held ascent shutter and upper-wire latch";
        }

        static BoxCollider CoopArrival(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var t = VerticalChild(parent, name); t.position = center;
            var box = Undo.AddComponent<BoxCollider>(t.gameObject); box.isTrigger = true; box.size = size;
            return box;
        }

        static Renderer CoopLamp(Transform parent, string name, Vector3 at)
        {
            BMass(parent, name + " 전기함", at + Vector3.back * .08f, new Vector3(.5f, .65f, .18f), wornSteel, false);
            return BMass(parent, name, at, new Vector3(.3f, .35f, .08f), maintenancePaint, false).GetComponent<Renderer>();
        }

        static void SetCoopShutters(SerializedProperty entry, params GameObject[] shutters)
        {
            var values = entry.FindPropertyRelative("shutters"); values.arraySize = shutters.Length;
            for (int i = 0; i < shutters.Length; i++) values.GetArrayElementAtIndex(i).objectReferenceValue = shutters[i];
        }

        static void BuildConveyorCooperativeLock(Transform p, SerializedProperty entry)
        {
            // The loop feet are Y-18.5. Its closed roof is entirely below that pose;
            // both alternate disembark paths enter the west mouth before the saws.
            BMass(p, "외줄 정비 통로 상부 덮개", new Vector3(51, -19.1f, -24), new Vector3(14, .6f, 11), bConcrete);
            BuildCooperativeSightWalls(p);
            BMass(p, "복귀 정비실 끝벽", new Vector3(81, -25, -24), new Vector3(.6f, 12, 11), bConcrete);
            foreach (float z in new[] { -28.5f, -19.5f })
                BMass(p, "두 하차 경로 공용 출입 잼", new Vector3(44, -24, z), new Vector3(.6f, 10, 2), bConcrete);
            // Do not add support under the saw wire. Only its existing endpoint gets a recovery treadle.
            AFloor(p, "톱 통과 후 동료 해제 답판", 58, 61.5f, -25.5f, -22.5f, -23);
            var door = BMass(p, "목마 해제 대기 격벽", new Vector3(62.5f, -24.25f, -24), new Vector3(.5f, 10.3f, 10.4f), wornSteel);
            var hatch = BMass(p, "복귀 대포 발사구 잠금 덮개", new Vector3(69.5f, -19.1f, -24), new Vector3(23, .6f, 11), wornSteel);
            var wireEntry = GameObject.Find("A04B_WireExitGrip").GetComponent<HumanCannonPole>();
            var entryData = new SerializedObject(wireEntry);
            entryData.FindProperty("maxReleaseUpwardSpeed").floatValue = 5.2f;
            entryData.ApplyModifiedProperties();
            // The control is independent: the same radial F interaction works from the wire, ground or air.
            var control = BMass(p, "목마 상부 클러치", new Vector3(50.4f, -20.55f, -24), new Vector3(.4f, .4f, .25f), wornSteel, false);
            AddClutchLever(control.transform);
            BMass(p, "클러치 상부 지지대", new Vector3(50.4f, -19.75f, -24), new Vector3(.12f, 1.2f, .12f), wornSteel, false);
            entry.FindPropertyRelative("shoulderControl").objectReferenceValue = control.transform;
            entry.FindPropertyRelative("arrival").objectReferenceValue = CoopArrival(p, "톱 뒤 동료 도착 감지", new Vector3(60, -22.55f, -24), new Vector3(2.5f, 1.3f, 2.4f));
            entry.FindPropertyRelative("indicator").objectReferenceValue = CoopLamp(p, "목마 클러치 상태등", new Vector3(50.4f, -20.2f, -23.7f));
            SetCoopShutters(entry, door, hatch);
            // Industrial cable and threshold marking connect the clutch to the far shutter without wall text.
            VerticalCable(p, "클러치에서 통과문 전선", new Vector3(50.4f, -19.6f, -24), new Vector3(62.5f, -19.6f, -24), .07f, wornSteel);
            FusionFloorMarks(p, new Vector3(60, -22.98f, -24), Vector3.right);
            BLight(p, new Vector3(46, -19.65f, -22), new Vector3(50, -23, -24), 120, 10);
            BLight(p, new Vector3(60, -19.65f, -22), new Vector3(55, -23, -24), 150, 12);
        }

        static void BuildCooperativeSightWalls(Transform p)
        {
            foreach (float z in new[] { -29.5f, -18.5f })
            {
                BMass(p, "정비 통로 전망창 하부", new Vector3(62.5f, -26.75f, z), new Vector3(37, 8.5f, .6f), bConcrete);
                BMass(p, "정비 통로 전망창 상부", new Vector3(62.5f, -20, z), new Vector3(37, 2, .6f), bConcrete);
                // Two visible apertures remain below capsule height, including crouching clearance.
                BMass(p, "전망창 이탈 방지 가로대", new Vector3(62.5f, -21.75f, z), new Vector3(37, .7f, .18f), wornSteel);
                for (float x = 44; x <= 81; x += 3)
                    BMass(p, "전망창 수직 프레임", new Vector3(x, -21.75f, z), new Vector3(.12f, 1.5f, .2f), wornSteel);
            }
        }

        static void AddClutchLever(Transform control)
        {
            if (control.Find("상부 클러치 레버") != null) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_ThirdParty/Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Lever_01.prefab");
            if (prefab == null) throw new InvalidOperationException("Existing lever art is required");
            var lever = (GameObject)PrefabUtility.InstantiatePrefab(prefab, control);
            Undo.RegisterCreatedObjectUndo(lever, "Use existing clutch lever art"); lever.name = "상부 클러치 레버";
            foreach (var collider in lever.GetComponentsInChildren<Collider>(true)) Undo.DestroyObjectImmediate(collider);
            var renderers = lever.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            lever.transform.localScale *= .8f / Mathf.Max(.01f, longest);
            var resized = renderers[0].bounds; foreach (var renderer in renderers) resized.Encapsulate(renderer.bounds);
            lever.transform.position += control.position + Vector3.back * .25f - resized.center;
            foreach (var renderer in renderers)
                renderer.sharedMaterials = Enumerable.Repeat(maintenancePaint, renderer.sharedMaterials.Length).ToArray();
        }

        public static string RefineACooperativePresentation()
        {
            if (Application.isPlaying || SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("A edit scene required");
            CompletionMaterials();
            var p = GameObject.Find("37_ACooperativeFusion").transform.Find("04B_ShoulderAndSawPassage");
            var old = p.Cast<Transform>().Where(t => t.name == "외줄 정비 통로 측벽").ToArray();
            if (old.Length > 0)
            {
                foreach (var t in old) Undo.DestroyObjectImmediate(t.gameObject);
                BuildCooperativeSightWalls(p);
            }
            AddClutchLever(p.Find("목마 상부 클러치"));
            Physics.SyncTransforms(); EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            return "Existing lever art; narrow guarded sight windows retain the immense pump-hall view";
        }

        static void BuildAscentCooperativeRelay(Transform p, Transform bg, SerializedProperty entry)
        {
            // Beside the retained cannon pad, north of the core end; no second ascent cannon or solo flight.
            AFloor(p, "동료 유지봉 측면 정비턱", 125, 128.5f, 103, 108, -8);
            var source = GameObject.Find("A04B_FloorExitGrip").GetComponent<HumanCannonPole>();
            var helper = AFusedPole(p, source, "A06_HelperAscentClutch", new Vector3(126.5f, -6.5f, 106), Vector3.forward);
            var poleData = new SerializedObject(helper);
            poleData.FindProperty("maxSwingAngle").floatValue = 12;
            poleData.ApplyModifiedProperties();
            // A visible bulkhead joins the core to the north envelope. Hold the original pole to raise it.
            var shutter = BMass(p, "동료 유지 상승 통로 셔터", new Vector3(130, 1, 110.25f), new Vector3(.6f, 26, 16.5f), wornSteel);
            BMass(p, "상승 셔터 코어측 문틀", new Vector3(130, 1, 101.7f), new Vector3(1.2f, 27, .6f), bConcrete);
            BMass(p, "상승 셔터 북측 문틀", new Vector3(130, 1, 118.8f), new Vector3(1.2f, 27, .6f), bConcrete);
            BMass(p, "상승 셔터 상부 권상기", new Vector3(130, 14.25f, 110.25f), new Vector3(1.8f, 1.5f, 17.7f), wornSteel);
            var receiver = CoopArrival(p, "상부 외줄 동료 복귀 고정", new Vector3(135, .5f, 104.5f), new Vector3(1.2f, 1.5f, 4));
            entry.FindPropertyRelative("helperPole").objectReferenceValue = helper;
            entry.FindPropertyRelative("arrival").objectReferenceValue = receiver;
            entry.FindPropertyRelative("indicator").objectReferenceValue = CoopLamp(p, "동료 복귀 고정 상태등", new Vector3(134.25f, 1.6f, 104.5f));
            SetCoopShutters(entry, shutter);
            VerticalCable(p, "유지봉 권상 전선", new Vector3(126.5f, -1.5f, 106), new Vector3(129.5f, -1.5f, 106), .1f, wornSteel);
            BLight(p, new Vector3(127, -2, 108), new Vector3(126, -8, 105), 170, 10);
        }
    }
}
