using System;
using System.Collections.Generic;
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
        const string MidEncounterGroup = "27_MidRouteEncounters_A";
        static Material midWarning;

        [MenuItem("Tools/CurtainCall/탈출로/A 확장 구간 회피 기믹 연결")]
        public static void BuildMidRouteEncounters()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("현재 A 편집 씬이 필요합니다.");
            var background = GameObject.Find(RootName).transform;
            if (background.Find(DepthGroup) == null || background.Find(MidEncounterGroup) != null)
                throw new InvalidOperationException("확장 구조가 필요하며 기존 기믹은 직접 편집하세요.");
            var course = UnityEngine.Object.FindAnyObjectByType<HumanCannonHazardCourse>();
            if (course == null || course.HazardCount != 13) throw new InvalidOperationException("원본 마지막 질주 장치13개를 먼저 확인하세요.");
            bConcrete = RunnerMaterial("brutal-concrete"); bRecess = RunnerMaterial("brutal-recess");
            bSteel = RunnerMaterial("brutal-steel"); bBulb = RunnerMaterial("concrete-service-bulb");
            wornSteel = RunnerMaterial("working-steel"); maintenancePaint = RunnerMaterial("working-faded-ochre");
            midWarning = RunnerMaterial("solo-danger-red");
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Meaningful mid-route encounters");
            var root = VerticalChild(background, MidEncounterGroup);
            root.localScale = new Vector3(1 / background.lossyScale.x, 1 / background.lossyScale.y, 1 / background.lossyScale.z);
            var hazards = new List<HumanCannonHazard>();
            var west = VerticalChild(root, "01_CrossSaw_StopAndGo");
            foreach (var spec in new[] { new Vector2(-17, 0), new Vector2(-34, 5.6f) })
            {
                bool first = spec.x == -17;
                Vector3 from = new Vector3(first ? 63 : 73, -20.9f, spec.x), to = new Vector3(first ? 73 : 63, -20.9f, spec.x);
                hazards.Add(MidHazard(west, first ? "01_WestSaw" : "02_EastSaw", "01_LeftSaw", from, to,
                    new Vector3(2.2f, 2.2f, .28f), new Vector3(68, -21.96f, spec.x), new Vector3(6, .025f, 2.4f), 2, 1.6f, 4.4f, spec.y));
                foreach (float x in new[] { 63f, 73f })
                {
                    BMass(west, "톱날 회수 설비함", new Vector3(x, -20.7f, spec.x), new Vector3(1.2f, 2.6f, 3), bRecess);
                    BMass(west, "톱날 설비 하부 브래킷", new Vector3(x, -23.1f, spec.x), new Vector3(3, 2.2f, 3));
                }
                VerticalCable(west, "횡단 설비 상부 레일", new Vector3(63, -18.4f, spec.x), new Vector3(73, -18.4f, spec.x), .23f, wornSteel);
                BLight(west, new Vector3(72, -17.5f, spec.x + 2), new Vector3(68, -22, spec.x), 230, 15);
            }
            BuildMidWaitingPocket(west, background);
            MidSafe(west, "BeforeSaw", new Vector3(68, -22, -12));
            MidSafe(west, "BetweenSaws", new Vector3(63, -22, -24));
            MidSafe(west, "AfterSaws", new Vector3(68, -22, -40));

            var press = VerticalChild(root, "02_DropPress_ReadAndSprint");
            for (int i = 0; i < 2; i++)
            {
                float x = 83 + i * 15;
                hazards.Add(MidHazard(press, "0" + (i + 1) + "_FallingPress", "05_DropLeft",
                    new Vector3(x, -10, -58), new Vector3(x, -20.5f, -58), new Vector3(3.2f, 3, 6.2f),
                    new Vector3(x, -21.96f, -58), new Vector3(3.2f, .025f, 6), 2.2f, 1.4f, 6.4f, i * 6.5f));
                foreach (float z in new[] { -62.5f, -53.5f })
                    BMass(press, "압착 설비 수직 가이드", new Vector3(x, -14, z), new Vector3(.7f, 18, 1), wornSteel);
                BMass(press, "압착 설비 상부 유압 보", new Vector3(x, -5.5f, -58), new Vector3(4, 2, 10), bRecess);
                BLight(press, new Vector3(x - 3, -15, -64), new Vector3(x, -21, -58), 300, 18);
            }
            MidSafe(press, "BeforePress", new Vector3(77, -22, -58));
            MidSafe(press, "BetweenPresses", new Vector3(90.5f, -22, -58));
            MidSafe(press, "AfterPress", new Vector3(104, -22, -58));

            BuildMidLowDuct(VerticalChild(root, "03_LowDuct_Crouch"));
            var vents = VerticalChild(root, "04_NorthVents_AlternateLanes");
            for (int i = 0; i < 4; i++)
            {
                bool left = i % 2 == 0; float z = new[] { 65f, 79f, 93f, 105f }[i];
                float x = left ? 121.35f : 123.65f;
                var at = new Vector3(x, -6.8f, z);
                hazards.Add(MidHazard(vents, "0" + (i + 1) + (left ? "_LeftVent" : "_RightVent"), "07_FireLeft",
                    at, at, new Vector3(2.7f, 2.4f, 3.6f), new Vector3(x, -7.96f, z), new Vector3(2.7f, .025f, 3.6f), 1.6f, 3, 3.4f, i * 2));
                float nozzleX = left ? 119.7f : 125.3f;
                BMass(vents, "열 배출구 하우징", new Vector3(nozzleX, -7, z), new Vector3(.45f, 1.8f, 2.7f), wornSteel);
                BMass(vents, "배출구 내부", new Vector3(nozzleX + (left ? .25f : -.25f), -7, z), new Vector3(.08f, 1.3f, 2.1f), bRecess, false);
                VerticalCable(vents, "벽체에서 배출구로 이어진 관", new Vector3(left ? 115.6f : 126, -6.8f, z), new Vector3(nozzleX, -6.8f, z), .55f, wornSteel);
                MidSafe(vents, "Dodge_" + (i + 1), new Vector3(left ? 124 : 121, -8, z));
                BLight(vents, new Vector3(left ? 116 : 129, -2, z - 2), new Vector3(x, -8, z), 230, 16);
            }
            var data = new SerializedObject(course); var list = data.FindProperty("hazards"); int original = list.arraySize;
            list.arraySize = original + hazards.Count;
            for (int i = 0; i < hazards.Count; i++) list.GetArrayElementAtIndex(original + i).objectReferenceValue = hazards[i];
            data.ApplyModifiedProperties();
            FinishMidRouteVisuals();
            Physics.SyncTransforms(); EditorSceneManager.MarkSceneDirty(scene); Undo.CollapseUndoOperations(undo);
        }

        static HumanCannonHazard MidHazard(Transform parent, string name, string templateName, Vector3 from, Vector3 to,
            Vector3 size, Vector3 floor, Vector3 footprint, float warning, float active, float rest, float phase)
        {
            var template = GameObject.Find(RootName + "/25_FinaleHazards_A/" + templateName).GetComponentInChildren<HumanCannonHazard>();
            var originalCue = template.GetComponent<HumanCannonHazardCue>();
            var group = VerticalChild(parent, name);
            var visual = UnityEngine.Object.Instantiate(originalCue.visual.gameObject, group).transform;
            Undo.RegisterCreatedObjectUndo(visual.gameObject, "Existing obstacle art copy");
            visual.name = "MovingVisual"; visual.SetPositionAndRotation(from, template.transform.rotation);
            Vector3 sourceSize = new SerializedObject(template).FindProperty("boxSize").vector3Value;
            if (template.KillsOnContact) visual.localScale = Vector3.Scale(visual.localScale, new Vector3(size.x / sourceSize.x, size.y / sourceSize.y, size.z / sourceSize.z));
            visual.gameObject.SetActive(true);
            var mechanism = ChildAt(group, "OriginalMechanism", from, template.transform.rotation);
            var cue = Undo.AddComponent<HumanCannonHazardCue>(mechanism.gameObject);
            cue.origin = from; cue.destination = to; cue.visual = visual;
            cue.warningSeconds = warning; cue.activeSeconds = active; cue.restSeconds = rest; cue.phase = phase;
            cue.falling = template.Kind == CannonHazardKind.FallingLoad; cue.showWhileWarning = cue.falling;
            cue.warning = MidWarning(group, floor, footprint);
            var hazard = Undo.AddComponent<HumanCannonHazard>(mechanism.gameObject);
            EditorUtility.CopySerialized(template, hazard);
            var data = new SerializedObject(hazard);
            data.FindProperty("cue").objectReferenceValue = cue; data.FindProperty("visual").objectReferenceValue = visual;
            data.FindProperty("boxSize").vector3Value = size; data.ApplyModifiedProperties();
            return hazard;
        }

        static Transform MidWarning(Transform p, Vector3 at, Vector3 size)
        {
            var warning = VerticalChild(p, "Warning_Footprint");
            MidWarningLines(warning, at, size);
            foreach (float sign in new[] { -1f, 1f })
            {
                BMass(p, "상시 위험 경계 도색", at + Vector3.right * sign * (size.x * .5f + .06f), new Vector3(.08f, .012f, size.z), maintenancePaint, false);
                BMass(p, "상시 위험 진입선", at + Vector3.forward * sign * (size.z * .5f + .06f), new Vector3(size.x, .012f, .08f), maintenancePaint, false);
            }
            return warning;
        }

        static void MidSafe(Transform p, string name, Vector3 at)
        {
            ChildAt(p, "Safe_" + name, at, Quaternion.identity);
            FusionFloorMarks(p, at + Vector3.up * .02f, Vector3.forward);
        }
    }
}
