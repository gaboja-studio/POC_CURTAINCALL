using System;
using System.Linq;
using System.Text;
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
        [MenuItem("Tools/CurtainCall/탈출로/A 모든 기존 묘기 연결")]
        public static void InstallAllOriginalDevices()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("현재 A 편집 씬이 필요합니다.");
            var course = UnityEngine.Object.FindAnyObjectByType<TightropeCourse>();
            if (course == null || !course.UsesSceneGeometry) throw new InvalidOperationException("기존 A03 원본 연결이 필요합니다.");
            var root = GameObject.Find(RootName).transform;
            var a05 = root.Find("24_TrickFusion_A/05_RopeDropTrampolineSwing");
            var a07 = root.Find("19_RunnerEscape/04_SwingChasm_276_298");
            if (a05 == null || a07 == null) throw new InvalidOperationException("A05/A07 배치를 찾지 못했습니다.");
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Connect all A original mechanics");
            var source = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity");
            try
            {
                var sourcePole = source.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<HumanCannonPole>(true)).First();
                var sourceTrampoline = source.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<HumanCannonTrampoline>(true)).First();
                var trampolineRoot = a05.Find("03_ReboundVisual_LogicPending/Trampoline_VisualOnly");
                var trampoline = Ensure<HumanCannonTrampoline>(trampolineRoot.gameObject);
                Undo.RecordObject(trampoline, "Original trampoline parameters");
                EditorUtility.CopySerialized(sourceTrampoline, trampoline);
                var data = new SerializedObject(trampoline);
                data.FindProperty("handle").objectReferenceValue = trampolineRoot.Find("Handle/Grip");
                data.ApplyModifiedProperties(); trampoline.enabled = true;
                PrefabUtility.RecordPrefabInstancePropertyModifications(trampoline);

                var poles = new HumanCannonPole[6];
                for (int i = 0; i < 3; i++)
                {
                    poles[i] = ConnectPole(a05.Find("04_DescendingSwing_LogicPending/MovingSwing_" + (i + 1)),
                        new Vector3(101 + i * 6, 1 - i, 28), 19.5f, 1f, sourcePole);
                    poles[i + 3] = ConnectPole(a07.Find("MovingSwing_" + (i + 1)),
                        new Vector3(i == 0 ? 279 : 281 + i * 7, i == 1 ? 4.2f : 3.5f, 78), 20f, .8f, sourcePole);
                }
                Undo.RecordObject(a07.Find("Anchor_SwingGrip_1"), "Reachable first swing");
                a07.Find("Anchor_SwingGrip_1").position = poles[3].Grip.position;
                var system = Ensure<HumanCannonPoleSystem>(GameObject.Find("EscapeGameplay_A"));
                var poleData = new SerializedObject(system);
                var list = poleData.FindProperty("poles"); list.arraySize = poles.Length;
                for (int i = 0; i < poles.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = poles[i];
                poleData.FindProperty("allowPlatformJumpGrab").boolValue = true;
                poleData.ApplyModifiedProperties(); system.enabled = true;
            }
            finally { EditorSceneManager.ClosePreviewScene(source); }

            var ropeObject = course.transform.Find("RopeSupport_1");
            if (ropeObject == null) ropeObject = ChildAt(course.transform, "RopeSupport_1", new Vector3(89, 0, 28), Quaternion.Euler(0, 90, 0));
            var support = Ensure<BoxCollider>(ropeObject.gameObject);
            Undo.RecordObject(support, "A05 original-width rope");
            support.center = new Vector3(0, -course.Shape.RopeThickness * .5f, 3.6f);
            support.size = new Vector3(course.Shape.RopeWalkWidth, course.Shape.RopeThickness, 7.2f);
            var courseData = new SerializedObject(course);
            courseData.FindProperty("independentSceneRopes").boolValue = true;
            var ropes = courseData.FindProperty("sceneRopes"); ropes.arraySize = 2;
            ropes.GetArrayElementAtIndex(0).objectReferenceValue = course.transform.Find("RopeSupport_0").GetComponent<BoxCollider>();
            ropes.GetArrayElementAtIndex(1).objectReferenceValue = support;
            var shape = courseData.FindProperty("sceneShape");
            shape.FindPropertyRelative("laneCount").intValue = 2;
            var ends = shape.FindPropertyRelative("laneEndDistances"); ends.arraySize = 2;
            ends.GetArrayElementAtIndex(0).floatValue = Mathf.Sqrt(194);
            ends.GetArrayElementAtIndex(1).floatValue = 7.2f;
            shape.FindPropertyRelative("laneChangeZones").arraySize = 0;
            courseData.ApplyModifiedProperties(); course.Rebuild();
            Physics.SyncTransforms();
            foreach (var feet in new[] { new Vector3(119,-8,28), new Vector3(300,0,78), new Vector3(98,-4.6f,25) })
            {
                var floor = Physics.RaycastAll(feet + Vector3.up * .3f, Vector3.down, .8f)
                    .Where(h => h.collider is BoxCollider && h.normal.y > .99f).OrderBy(h => h.distance).FirstOrDefault();
                if (floor.collider == null) throw new InvalidOperationException("안전 착지 바닥 없음: " + feet);
                Ensure<HumanCannonLandingZone>(floor.collider.gameObject);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(group);
        }

        static T Ensure<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(target);
        }
        static Transform ChildAt(Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            var child = new GameObject(name).transform;
            Undo.RegisterCreatedObjectUndo(child.gameObject, "Original mechanic anchor");
            child.SetParent(parent, true); child.SetPositionAndRotation(position, rotation); return child;
        }
        static HumanCannonPole ConnectPole(Transform root, Vector3 gripPosition, float ceiling, float halfWidth, HumanCannonPole source)
        {
            if (root == null) throw new InvalidOperationException("그네 외형 누락");
            var existing = root.GetComponent<HumanCannonPole>();
            if (existing != null)
            {
                Undo.RecordObject(root, "Reachable original swing");
                root.position += gripPosition - existing.Grip.position;
                existing.PresentSwing(0); return existing;
            }
            var originalParts = root.Cast<Transform>().ToArray();
            var material = originalParts.Select(t => t.GetComponent<Renderer>()).First(r => r != null).sharedMaterial;
            var grip = ChildAt(root, "NeutralGrip", gripPosition, Quaternion.Euler(0, 90, 0));
            var bar = ChildAt(root, "OriginalSwingBar", gripPosition, grip.rotation);
            foreach (var part in originalParts)
            {
                if (part.name.Contains("손잡이")) Undo.SetTransformParent(part, bar, "Animate original swing bar");
                else if (part.name.Contains("현수선") || part.name.Contains("케이블"))
                { Undo.RecordObject(part.gameObject, "Replace baked-length cable"); part.gameObject.SetActive(false); }
            }
            Transform[] cables = new Transform[2], anchors = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                anchors[i] = ChildAt(root, "CeilingAnchor_" + i, new Vector3(gripPosition.x, ceiling, gripPosition.z + (i == 0 ? -halfWidth : halfWidth)), Quaternion.identity);
                var cable = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Undo.RegisterCreatedObjectUndo(cable, "Original moving cable"); cable.name = "OriginalCable_" + i;
                cable.transform.SetParent(root, false); cable.transform.localScale = new Vector3(.065f, 1, .065f);
                UnityEngine.Object.DestroyImmediate(cable.GetComponent<Collider>());
                cable.GetComponent<Renderer>().sharedMaterial = material; cables[i] = cable.transform;
            }
            var pole = Undo.AddComponent<HumanCannonPole>(root.gameObject);
            EditorUtility.CopySerialized(source, pole);
            var data = new SerializedObject(pole);
            data.FindProperty("grip").objectReferenceValue = grip;
            data.FindProperty("linkedGate").objectReferenceValue = null;
            data.FindProperty("visualBar").objectReferenceValue = bar;
            data.FindProperty("leftCable").objectReferenceValue = cables[0]; data.FindProperty("rightCable").objectReferenceValue = cables[1];
            data.FindProperty("leftAnchor").objectReferenceValue = anchors[0]; data.FindProperty("rightAnchor").objectReferenceValue = anchors[1];
            data.ApplyModifiedPropertiesWithoutUndo(); pole.enabled = true; pole.PresentSwing(0);
            return pole;
        }

        public static string ValidateAllOriginalDevices()
        {
            var output = new StringBuilder(); int passed = 0, failed = 0;
            void Check(string name, bool valid) { if (valid) passed++; else { failed++; output.AppendLine("FAIL " + name); } }
            var course = UnityEngine.Object.FindAnyObjectByType<TightropeCourse>();
            Check("two independent ropes", course != null && course.HasIndependentRopes && course.LaneCount == 2);
            if (course != null)
            {
                Check("A05 rope origin", Vector3.Distance(course.GetLanePosition(1,0),new Vector3(89,0,28)) < .01f);
                Check("A05 rope end", Vector3.Distance(course.GetLanePosition(1,7.2f),new Vector3(96.2f,0,28)) < .01f);
                Check("A03 rope end", Vector3.Distance(course.GetLanePosition(0,Mathf.Sqrt(194)),new Vector3(64,-6,82)) < .01f);
                Check("A05 rope height", Mathf.Abs(course.RopeHeightAt(new Vector3(92,0,28))) < .01f);
                Check("no jump between independent ropes", course.FindLandingLane(new Vector3(92,0,28),-1) == TightropeCourse.NoLane);
            }
            var systems = UnityEngine.Object.FindObjectsByType<HumanCannonPoleSystem>(FindObjectsSortMode.None);
            bool completed = GameObject.Find(RootName).transform.Find(CompletionGroup) != null;
            int expectedPoles = completed ? 12 : 6;
            Check("one pole system registered slots", systems.Length == 1 && systems[0].PoleCount == expectedPoles);
            var poles = UnityEngine.Object.FindObjectsByType<HumanCannonPole>(FindObjectsSortMode.None);
            Check("enabled original poles", poles.Length == expectedPoles && poles.All(p => p.enabled));
            foreach (var pole in poles)
            {
                var data = new SerializedObject(pole);
                foreach (var key in new[] { "grip", "visualBar", "leftCable", "rightCable", "leftAnchor", "rightAnchor" })
                    Check(pole.name + " " + key, data.FindProperty(key).objectReferenceValue != null);
                Check(pole.name + " neutral bar", Vector3.Distance(pole.BarPosition(0), pole.Grip.position) < .01f);
                Check(pole.name + " forward", Vector3.Dot(pole.Forward,pole.name.StartsWith("Descent_") ? Vector3.back : Vector3.right) > .99f);
                Check(pole.name + " long ceiling cables", pole.SwingLength > (pole.name.StartsWith("Descent_") ? 12f : 15f));
                Check(pole.name + " unlimited original hold", pole.HoldSeconds == 0);
            }
            var trampoline = UnityEngine.Object.FindObjectsByType<HumanCannonTrampoline>(FindObjectsSortMode.None);
            Check("one active original trampoline", trampoline.Length == 1 && trampoline[0].enabled);
            if (trampoline.Length == 1)
            {
                Check("trampoline handle", trampoline[0].Handle.name == "Grip");
                Check("trampoline surface", Vector3.Distance(trampoline[0].Surface,new Vector3(98,-4.1f,28)) < .01f);
                Check("original bounce speed", trampoline[0].UpwardSpeed >= 12);
                Check("A05 first swing reachable vertically", trampoline[0].Surface.y + trampoline[0].UpwardSpeed * trampoline[0].UpwardSpeed / (2 * 9.81f) + 1f > 1f);
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(HumanCannonTrampoline).GetMethod("ResetPreviousPose",flags).Invoke(trampoline[0],null);
                object[] contact = { new Vector3(98,0,28), new Vector3(98,-4.4f,28), .3f, 1.73f, Vector3.zero };
                Check("A05 falling body touches original trampoline", (bool)typeof(HumanCannonTrampoline).GetMethod("TryContact",flags).Invoke(trampoline[0],contact));
            }
            var first = poles.FirstOrDefault(p => p.Grip.position.x > 270 && p.Grip.position.x < 285);
            if (first != null)
            {
                var character = CurtainCall.Settings.GameSettings.Base.Character;
                float gravity = character.Gravity, speed = character.FreeMoveSpeed;
                float vertical = Mathf.Sqrt(2 * gravity * CurtainCall.Settings.GameSettings.HumanCannon.JumpHeight);
                float closest = float.PositiveInfinity;
                for (float t = .01f; t < vertical * 2 / gravity; t += .01f)
                {
                    var center = new Vector3(275.8f + speed*t, vertical*t-.5f*gravity*t*t+character.CapsuleHeight*.5f,78);
                    closest = Mathf.Min(closest,Vector3.Distance(center,first.Grip.position));
                }
                float reach = new SerializedObject(first).FindProperty("grabDistance").floatValue * 1.25f;
                Check("A07 first swing within original jump reach",closest < reach - .2f);
                output.AppendLine($"A07 static jump nearest={closest:F2}m, grab radius={reach:F2}m; no player input used.");
            }
            foreach (var feet in new[] { new Vector3(119,-8,28), new Vector3(300,0,78) })
                Check("safe landing " + feet, UnityEngine.Object.FindObjectsByType<HumanCannonLandingZone>(FindObjectsSortMode.None).Any(z => z.SupportsLanding(feet)));
            output.AppendLine($"Passed={passed}; Failed={failed}. Static references/geometry only; manual traversal not automated.");
            return output.ToString();
        }
    }
}
