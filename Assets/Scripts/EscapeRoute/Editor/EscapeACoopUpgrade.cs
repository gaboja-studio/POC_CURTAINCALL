using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.HumanCannon;
using CurtainCall.Tightrope;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        /// <summary>Upgrade only the saved A route. No full scene rebuild or external prefab edits.</summary>
        public static string UpgradeACooperativeRoute()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != ScenePath || scene.isDirty)
                throw new InvalidOperationException("Saved A edit scene required");
            var bg = GameObject.Find(RootName).transform;
            if (bg.Find("38_ACooperativeWorks") != null) throw new InvalidOperationException("Already upgraded");
            var old = bg.Find("37_ACooperativeFusion");
            var locks = old.GetComponent<EscapeCooperativeLocks>();
            var source = GameObject.Find("A04B_FloorExitGrip").GetComponent<HumanCannonPole>();
            CompletionMaterials();
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("A cooperative machinery and shared swings");
            foreach (Transform child in old.Cast<Transform>().ToArray()) Undo.DestroyObjectImmediate(child.gameObject);
            var root = VerticalChild(bg, "38_ACooperativeWorks");
            root.localScale = new Vector3(1 / bg.lossyScale.x, 1 / bg.lossyScale.y, 1 / bg.lossyScale.z);
            var data = new SerializedObject(locks);
            var entries = data.FindProperty("checkpoints"); entries.arraySize = 5;
            for (int i = 0; i < entries.arraySize; i++) ResetCoopEntry(entries.GetArrayElementAtIndex(i));
            RefitCoopMachinery(old, source, entries);
            var course = UnityEngine.Object.FindAnyObjectByType<TightropeCourse>();
            var courseData = new SerializedObject(course);
            var existingRopes = courseData.FindProperty("sceneRopes");
            var ropes = new List<BoxCollider>();
            for (int i = 0; i < existingRopes.arraySize; i++) ropes.Add((BoxCollider)existingRopes.GetArrayElementAtIndex(i).objectReferenceValue);
            BuildShoulderDrawbridge(VerticalChild(root, "07A_ShoulderDrawbridge"), bg, entries.GetArrayElementAtIndex(2), ropes);
            BuildCrewSwingBridge(VerticalChild(root, "07B_CrewSwingHoist"), bg, entries.GetArrayElementAtIndex(3));
            BuildBuddyBridge(VerticalChild(root, "07C_BuddyReturnBridge"), bg, source, entries.GetArrayElementAtIndex(4));
            data.ApplyModifiedProperties();
            SetFinaleArray(courseData, "sceneRopes", ropes.ToArray());
            var shape = courseData.FindProperty("sceneShape");
            shape.FindPropertyRelative("laneCount").intValue = ropes.Count;
            var ends = shape.FindPropertyRelative("laneEndDistances"); ends.arraySize = ropes.Count;
            for (int i = 0; i < ropes.Count; i++) ends.GetArrayElementAtIndex(i).floatValue = ropes[i].size.z;
            courseData.ApplyModifiedProperties(); course.Rebuild();
            foreach (var pole in bg.GetComponentsInChildren<HumanCannonPole>().Where(p => p.isActiveAndEnabled))
                ConfigureCrewPole(pole, pole.name.Contains("Helper") ? 1 : 4);
            RegisterAllPoles(); PolishACooperativeWorks(); Physics.SyncTransforms();
            Selection.activeGameObject = root.gameObject;
            EditorSceneManager.MarkSceneDirty(scene); Undo.CollapseUndoOperations(undo);
            return "Removed boxed passages; two cannon interlocks; three service bridges; A swings support up to four riders";
        }

        static void ResetCoopEntry(SerializedProperty entry)
        {
            foreach (string key in new[] { "shoulderControl", "helperPole", "arrival", "indicator", "lockedStation", "span" })
                entry.FindPropertyRelative(key).objectReferenceValue = null;
            entry.FindPropertyRelative("shutters").arraySize = 0;
            entry.FindPropertyRelative("minimumHelpers").intValue = 1;
            entry.FindPropertyRelative("latchWhenCrewReady").boolValue = false;
            entry.FindPropertyRelative("prompt").stringValue = "상부 클러치 해제";
            entry.FindPropertyRelative("instruction").stringValue = "가까이에서 F로 레버를 당겨 길을 엽니다";
        }

        static Transform WorksControl(Transform p, string name, Vector3 at)
        {
            var control = BMass(p, name, at, new Vector3(.4f, .4f, .25f), wornSteel, false).transform;
            AddClutchLever(control);
            // A real overhead service frame carries the suspended lever, clear of the traversing bodies.
            foreach (float z in new[] { at.z - 2.3f, at.z + 2.3f })
                BMass(p, "정비 권상기 지지기둥", new Vector3(at.x, at.y - .25f, z), new Vector3(.18f, 5.4f, .18f), wornSteel);
            RevealAsset(p, "기존 크레인 횡보", Industrial + "SM_Crane_Beam.prefab", at + new Vector3(0, 2.5f, 0), new Vector3(.4f, .5f, 5), wornSteel, Quaternion.Euler(0, 90, 0));
            float baseHeight = at.x < 100 ? -86 : -48;
            foreach (float z in new[] { at.z - 2.3f, at.z + 2.3f })
                BMass(p, "권상틀 하부 교각", new Vector3(at.x, (baseHeight + at.y - 2.95f) * .5f, z),
                    new Vector3(.65f, at.y - 2.95f - baseHeight, .65f), bRecess);
            VerticalCable(p, "상부 레버 권상선", at + Vector3.up * .3f, at + Vector3.up * 2.5f, .055f, wornSteel);
            return control;
        }

        static void RefitCoopMachinery(Transform p, HumanCannonPole source, SerializedProperty entries)
        {
            var fourth = VerticalChild(p, "04B_OpenSawAndCannonInterlock");
            var first = entries.GetArrayElementAtIndex(0);
            AFloor(fourth, "톱 뒤 동료 도착대", 58, 61.5f, -25.5f, -22.5f, -23);
            first.FindPropertyRelative("shoulderControl").objectReferenceValue = WorksControl(fourth, "목마 상부 클러치", new Vector3(50.4f, -20.55f, -24));
            first.FindPropertyRelative("arrival").objectReferenceValue = CoopArrival(fourth, "톱 뒤 도착 감지", new Vector3(60, -22.55f, -24), new Vector3(2.5f, 1.3f, 2.4f));
            first.FindPropertyRelative("instruction").stringValue = "전원 버튼에 F → 복귀 대포 가동 · 톱을 지나 대포로 이동";
            SetStationInterlock(fourth, first, GameObject.Find("04_ReturnCannon").GetComponent<HumanCannonStation>());
            first.FindPropertyRelative("indicator").objectReferenceValue = CoopLamp(fourth, "클러치 상태등", new Vector3(50.4f, -20.1f, -23.7f));
            BLight(fourth, new Vector3(49, -17, -21), new Vector3(53, -23, -24), 200, 15);

            var sixth = VerticalChild(p, "06_OpenCannonRelay");
            var second = entries.GetArrayElementAtIndex(1);
            AFloor(sixth, "유지봉 측면 정비턱", 125, 128.5f, 103, 108, -8);
            var helper = AFusedPole(sixth, source, "A06_HelperAscentClutch", new Vector3(126.5f, -6.5f, 106), Vector3.forward, 1.5f);
            SetPoleFloat(helper, "maxSwingAngle", 8);
            second.FindPropertyRelative("helperPole").objectReferenceValue = helper;
            second.FindPropertyRelative("arrival").objectReferenceValue = CoopArrival(sixth, "상부 외줄 도착 감지", new Vector3(135, .5f, 104.5f), new Vector3(1.2f, 1.5f, 4));
            second.FindPropertyRelative("instruction").stringValue = "동료가 유지봉을 잡으면 대포 가동 · 상부 외줄 착지 후 모두 합류";
            SetStationInterlock(sixth, second, GameObject.Find("A06_CoreEndAscentCannon").GetComponent<HumanCannonStation>());
            second.FindPropertyRelative("indicator").objectReferenceValue = CoopLamp(sixth, "상부 복귀 고정등", new Vector3(134.25f, 1.6f, 104.5f));
            BLight(sixth, new Vector3(127, -2, 108), new Vector3(126, -8, 105), 170, 10);
        }

        static void SetStationInterlock(Transform p, SerializedProperty entry, HumanCannonStation station)
        {
            entry.FindPropertyRelative("lockedStation").objectReferenceValue = station;
            var clamp = VerticalChild(p, station.name + "_SafetyClamps");
            foreach (float sign in new[] { -1f, 1f })
                BMass(clamp, "대포 안전 클램프", station.Muzzle.position + station.transform.right * sign * .65f - Vector3.up * .2f,
                    new Vector3(.16f, 1.3f, .22f), maintenancePaint, false);
            SetCoopShutters(entry, clamp.gameObject);
            RevealAsset(p, "대포 연동 제어반", Industrial + "SM_Control_Panel.prefab", station.transform.position + station.transform.right * 2,
                new Vector3(.65f, 1.5f, .8f), wornSteel);
            CoopLamp(clamp, "대포 잠금 표시", station.transform.position + station.transform.right * 2 + Vector3.up * 1.6f);
        }

        static void BuildShoulderDrawbridge(Transform p, Transform bg, SerializedProperty entry, List<BoxCollider> ropes)
        {
            var oldDeck = bg.Find("19_RunnerEscape/02_SideDischarge_164_226/측면 반출 질주면");
            oldDeck.gameObject.SetActive(false);
            RunnerDeck(p, "협동교 이후 기존 함정 주행면", 184, 226);
            FusionFloorMarks(p, new Vector3(162, .02f, 78), Vector3.right);
            AFusionLandingRope(p, "A07A_ShoulderWorkWire", new Vector3(164, 0, 78), new Vector3(174, 0, 78), ropes);
            entry.FindPropertyRelative("shoulderControl").objectReferenceValue = WorksControl(p, "목마 반출교 레버", new Vector3(171.5f, 2.45f, 78));
            entry.FindPropertyRelative("span").objectReferenceValue = WorksBridge(p, "목마 반출교", 174, 184, 78, 4);
            entry.FindPropertyRelative("prompt").stringValue = "반출교 내리기";
            entry.FindPropertyRelative("instruction").stringValue = "레버에 가까이 가서 F → 반출교 내려옴";
            entry.FindPropertyRelative("indicator").objectReferenceValue = CoopLamp(p, "목마 반출교 표시등", new Vector3(171.5f, 2.9f, 78.3f));
            WorksMachinery(p, 173.5f, 78, 4);
            BLight(p, new Vector3(169, 5, 82), new Vector3(174, 0, 78), 380, 20);
        }

        static void BuildCrewSwingBridge(Transform p, Transform bg, SerializedProperty entry)
        {
            var first = GameObject.Find("Original_MovingSwing_1_279").GetComponent<HumanCannonPole>();
            var second = GameObject.Find("Original_MovingSwing_3_295").GetComponent<HumanCannonPole>();
            HideAFusedPole(bg, second);
            AFloor(p, "공동 탑승 대기턱", 276, 278.4f, 75, 81, 0);
            entry.FindPropertyRelative("helperPole").objectReferenceValue = first;
            entry.FindPropertyRelative("minimumHelpers").intValue = 1;
            entry.FindPropertyRelative("latchWhenCrewReady").boolValue = true;
            entry.FindPropertyRelative("span").objectReferenceValue = WorksBridge(p, "두 사람 하중 권상교", 286, 298, 78, 4.5f);
            entry.FindPropertyRelative("instruction").stringValue = "F로 그네를 잡으면 권상교가 내려옵니다 · 최대 4명이 함께 탈 수 있습니다";
            entry.FindPropertyRelative("indicator").objectReferenceValue = CoopLamp(p, "공동 탑승 하중 표시등", new Vector3(278, 2.2f, 80.8f));
            SetPoleFloat(first, "maxReleaseUpwardSpeed", 5.2f);
            WorksMachinery(p, 286, 78, 4.5f);
            BLight(p, new Vector3(281, 7, 82), new Vector3(285, 0, 78), 500, 25);
        }

        static void BuildBuddyBridge(Transform p, Transform bg, HumanCannonPole source, SerializedProperty entry)
        {
            var last = bg.Find("19_RunnerEscape/05_LastSprint_298_366");
            last.Find("착지 후 18m 회복 바닥").gameObject.SetActive(false);
            last.Find("출구를 향한 마지막 질주면").gameObject.SetActive(false);
            AFloor(p, "동료 역할 교대 정비대", 298, 308, 71, 85, 0);
            RunnerDeck(p, "복귀 고정 후 마지막 질주면", 322, 366);
            var helper = AFusedPole(p, source, "A07C_HelperBridgeClutch", new Vector3(305.8f, 1.5f, 78), Vector3.right, 1.5f);
            SetPoleFloat(helper, "maxSwingAngle", 8); SetPoleFloat(helper, "airControlSpeed", 3);
            entry.FindPropertyRelative("helperPole").objectReferenceValue = helper;
            entry.FindPropertyRelative("span").objectReferenceValue = WorksBridge(p, "동료 유지 반출교", 308, 322, 78, 3.5f);
            entry.FindPropertyRelative("arrival").objectReferenceValue = CoopArrival(p, "맞은편 동료 영구 고정", new Vector3(323.8f, .5f, 78), new Vector3(3.5f, 1.4f, 5));
            entry.FindPropertyRelative("instruction").stringValue = "한 명이 유지봉을 잡고 다른 동료가 반출교를 건넙니다 · 맞은편 착지 후 유지한 동료도 합류";
            entry.FindPropertyRelative("indicator").objectReferenceValue = CoopLamp(p, "동료 도착 고정등", new Vector3(323.8f, 1.5f, 81));
            WorksMachinery(p, 308, 78, 3.5f);
            BLight(p, new Vector3(306, 5, 82), new Vector3(316, 0, 78), 420, 25);
        }

        static EscapeCooperativeSpan WorksBridge(Transform p, string name, float x1, float x2, float z, float width)
        {
            var root = VerticalChild(p, name);
            var left = VerticalChild(root, "서측 접이교"); left.position = new Vector3(x1, 0, z);
            var right = VerticalChild(root, "동측 접이교"); right.position = new Vector3(x2, 0, z);
            float half = (x2 - x1) * .5f;
            foreach (var leaf in new[] { left, right })
            {
                float sign = leaf == left ? 1 : -1;
                var deck = BMass(leaf, "반출교 철판", new Vector3(sign * half * .5f, -.16f, 0), new Vector3(half, .32f, width), wornSteel);
                Ensure<HumanCannonLandingZone>(deck);
                // Authored scaffold modules cover the plate, with railing and structure at human scale.
                for (int i = 0; i < Mathf.CeilToInt(half / 2); i++)
                {
                    float segment = Mathf.Min(2, half - i * 2);
                    RevealAsset(leaf, "기존 비계 발판", Industrial + "SM_Scaffolding_Flooring.prefab", new Vector3(sign * (i * 2 + segment * .5f), -.12f, 0), new Vector3(segment, .12f, width), wornSteel);
                }
                foreach (float edge in new[] { -width * .5f, width * .5f })
                {
                    BMass(leaf, "반출교 측면 트러스", new Vector3(sign * half * .5f, -.5f, edge), new Vector3(half, .6f, .16f), wornSteel, false);
                    BMass(leaf, "난간 손잡이", new Vector3(sign * half * .5f, 1, edge), new Vector3(half, .07f, .07f), maintenancePaint, false);
                    for (float x = .3f; x < half; x += 1.8f)
                        BMass(leaf, "난간 지주", new Vector3(sign * x, .5f, edge), new Vector3(.06f, 1, .06f), wornSteel, false);
                }
            }
            var span = Undo.AddComponent<EscapeCooperativeSpan>(root.gameObject);
            var data = new SerializedObject(span);
            data.FindProperty("leftLeaf").objectReferenceValue = left;
            data.FindProperty("rightLeaf").objectReferenceValue = right;
            SetFinaleArray(data, "deckColliders", root.GetComponentsInChildren<Collider>());
            data.ApplyModifiedProperties(); span.SetDeployed(false);
            return span;
        }

        static void WorksMachinery(Transform p, float x, float z, float width)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                var at = new Vector3(x, -.6f, z + side * (width * .5f + .8f));
                RevealAsset(p, "기존 권상 설비", Industrial + "SM_Machinery_04.prefab", at, new Vector3(1.2f, 2.4f, 1.5f), wornSteel);
                BMass(p, "권상 설비 받침", at + Vector3.down * .25f, new Vector3(1.8f, .5f, 2), bConcrete);
                BMass(p, "권상 설비 하부 교각", new Vector3(at.x, -24.55f, at.z), new Vector3(1.1f, 46.9f, 1.3f), bRecess);
                VerticalCable(p, "권상 동력 케이블", at + Vector3.up * 1.7f, at + new Vector3(0, 1.7f, -side * .8f), .06f, maintenancePaint);
            }
        }

        static void SetPoleFloat(HumanCannonPole pole, string name, float value)
        {
            var data = new SerializedObject(pole); data.FindProperty(name).floatValue = value; data.ApplyModifiedProperties();
        }

        public static string PolishACooperativeWorks()
        {
            if (Application.isPlaying || SceneManager.GetActiveScene().path != ScenePath) throw new InvalidOperationException("A edit scene required");
            CompletionMaterials();
            var bg = GameObject.Find(RootName).transform;
            foreach (var parent in new[] {
                bg.Find("37_ACooperativeFusion/04B_OpenSawAndCannonInterlock"),
                bg.Find("38_ACooperativeWorks/07A_ShoulderDrawbridge") })
            {
                var control = parent.Cast<Transform>().First(t => t.name == "목마 상부 클러치" || t.name == "목마 반출교 레버");
                var at = control.position;
                var beam = parent.Find("기존 크레인 횡보"); if (beam != null) Undo.DestroyObjectImmediate(beam.gameObject);
                RevealAsset(parent, "기존 크레인 횡보", Industrial + "SM_Crane_Beam.prefab", at + new Vector3(0, 2.5f, 0), new Vector3(.4f, .5f, 5), wornSteel, Quaternion.Euler(0, 90, 0));
                if (parent.Find("권상틀 하부 교각") == null)
                {
                    float bottom = at.x < 100 ? -86 : -48;
                    foreach (float z in new[] { at.z - 2.3f, at.z + 2.3f })
                        BMass(parent, "권상틀 하부 교각", new Vector3(at.x, (bottom + at.y - 2.95f) * .5f, z), new Vector3(.65f, at.y - 2.95f - bottom, .65f), bRecess);
                }
            }
            foreach (Transform parent in bg.Find("38_ACooperativeWorks"))
                if (parent.Find("권상 설비 하부 교각") == null)
                    foreach (var footing in parent.Cast<Transform>().Where(t => t.name == "권상 설비 받침").ToArray())
                        BMass(parent, "권상 설비 하부 교각", new Vector3(footing.position.x, -24.55f, footing.position.z), new Vector3(1.1f, 46.9f, 1.3f), bRecess);
            foreach (var cue in UnityEngine.Object.FindObjectsByType<HumanCannonHazardCue>(FindObjectsSortMode.None))
                if (cue.warning != null && (Mathf.Abs(cue.origin.x - 176) < .1f || Mathf.Abs(cue.origin.x - 321) < .1f))
                {
                    Undo.RecordObject(cue.warning, "Match hazard paint to deployed walking width");
                    var scale = cue.warning.localScale; scale.z = cue.origin.x < 200 ? 4 : 3.5f; cue.warning.localScale = scale;
                }
            Physics.SyncTransforms(); EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            return "Structural footings connected to the facility base; crane crossbeams aligned; warning paint fits bridge width";
        }

        static void ConfigureCrewPole(HumanCannonPole pole, int capacity)
        {
            var data = new SerializedObject(pole);
            data.FindProperty("riderCapacity").intValue = capacity; data.FindProperty("riderSpacing").floatValue = .8f;
            if (capacity > 1)
            {
                var bar = data.FindProperty("visualBar").objectReferenceValue as Transform;
                var mesh = bar != null ? bar.GetComponent<MeshFilter>() : null;
                if (mesh != null && mesh.sharedMesh != null)
                {
                    Undo.RecordObject(bar, "Widen shared handhold");
                    var scale = bar.localScale;
                    scale.x = 3.2f / Mathf.Max(.01f, mesh.sharedMesh.bounds.size.x * bar.parent.lossyScale.x);
                    bar.localScale = scale;
                }
                var side = Vector3.Cross(Vector3.up, pole.Forward);
                foreach (var prefix in new[] { "left", "right" })
                {
                    var anchor = data.FindProperty(prefix + "Anchor").objectReferenceValue as Transform;
                    if (anchor == null) continue;
                    Undo.RecordObject(anchor, "Widen shared swing suspension");
                    float height = anchor.position.y;
                    anchor.position = pole.Grip.position + side * (prefix == "left" ? -1.5f : 1.5f);
                    anchor.position = new Vector3(anchor.position.x, height, anchor.position.z);
                }
            }
            data.ApplyModifiedProperties(); pole.PresentSwing(0);
        }
    }
}
