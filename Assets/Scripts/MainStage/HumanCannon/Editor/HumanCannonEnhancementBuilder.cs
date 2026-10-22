using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.HumanCannon.EditorTools
{
    /// <summary>기존 전용 코스에 고도화 배치를 적용한다. 공용 씬·카메라 에셋은 저장하지 않는다.</summary>
    public static class HumanCannonEnhancementBuilder
    {
        const string RootName = "HumanCannonEnhancement";
        const string Materials = "Assets/Scenes/Tests/MainStage/HumanCannon/Materials/";

        [MenuItem("Tools/CurtainCall/인간 대포/넓은 반동과 전체 폭 톱 적용")]
        public static void ApplyWideSwingToOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != HumanCannonTestSceneBuilder.ScenePath)
                throw new InvalidOperationException("Play 종료 후 HumanCannon 전용 씬에서 실행하세요.");
            var roots = scene.GetRootGameObjects();
            var poles = All<HumanCannonPole>(roots);
            var saws = All<HumanCannonPoleSaw>(roots);
            if (poles.Length != 10 || saws.Length != 10 || saws.Select(s => s.Pole).Distinct().Count() != 10)
                throw new InvalidOperationException("고도화 봉10/왕복 톱10 연결을 먼저 확인하세요.");
            var ceilings = poles.Select(p =>
            {
                using var data = new SerializedObject(p);
                var left = data.FindProperty("leftAnchor").objectReferenceValue as Transform;
                var right = data.FindProperty("rightAnchor").objectReferenceValue as Transform;
                if (left == null || right == null || left.parent == null || left.parent != right.parent
                    || p.Grip.IsChildOf(left.parent)) throw new InvalidOperationException("독립 천장 연결이 필요합니다: " + p.name);
                return (Left: left, Right: right, Root: left.parent);
            }).ToArray();
            if (ceilings.Select(c => c.Root).Distinct().Count() != poles.Length)
                throw new InvalidOperationException("봉별 천장이 독립적이어야 합니다.");
            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("봉 전체 반동 및 좌우 끝 왕복 톱");
            foreach (var root in roots) Undo.RegisterFullObjectHierarchyUndo(root, "인간 대포 반동 조정");
            try
            {
                for (int i = 0; i < poles.Length; i++)
                {
                    var pole = poles[i];
                    var ceiling = ceilings[i];
                    // 복제된 2구간 천장도 중립 봉 바로 위에 둬 실제 줄 길이와 중립 위치를 맞춘다.
                    ceiling.Root.position += pole.Grip.position + Vector3.up * 8f
                        - (ceiling.Left.position + ceiling.Right.position) * 0.5f;
                    TuneSwing(pole);
                }
                foreach (var saw in saws)
                {
                    saw.ConfigureAcrossMap(saw.Pole, saw.Hazard, -15f, 15f, 6f);
                    saw.Hazard.Present(0, 1);
                    EditorUtility.SetDirty(saw);
                    EditorUtility.SetDirty(saw.Hazard);
                }
                Physics.SyncTransforms();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("반동 조정 씬 저장 실패");
                Undo.CollapseUndoOperations(undo);
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        [MenuItem("Tools/CurtainCall/인간 대포/고도화 배치 적용")]
        public static void ApplyToOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty
                || scene.path != HumanCannonTestSceneBuilder.ScenePath)
                throw new InvalidOperationException("저장된 HumanCannon 전용 씬에서 Play 종료 후 적용하세요.");
            var roots = scene.GetRootGameObjects();
            // 이미 적용된 씬에는 중복 생성이나 수치 덮어쓰기를 하지 않는다.
            if (roots.Any(root => root.name == RootName)) return;
            var system = All<HumanCannonPoleSystem>(roots).Single();
            var course = All<HumanCannonHazardCourse>(roots).Single();
            using var systemData = new SerializedObject(system);
            var polesField = systemData.FindProperty("poles");
            var poles = Enumerable.Range(0, polesField.arraySize)
                .Select(i => (HumanCannonPole)polesField.GetArrayElementAtIndex(i).objectReferenceValue).ToList();
            if (poles.Count != 6 || course.HazardCount != 16 || poles.Any(p => p == null))
                throw new InvalidOperationException("기존 봉6/일반함정16 코스를 먼저 확인하세요.");
            var shader = Shader.Find("CurtainCall/HumanCannon/Outline");
            if (shader == null) throw new InvalidOperationException("전용 외곽선 셰이더를 먼저 임포트하세요.");
            var outline = Material("human-cannon-outline", shader, new Color(0.02f, 0.035f, 0.055f));
            var sign = Material("section-sign", Shader.Find("Universal Render Pipeline/Unlit"), new Color(0.035f, 0.08f, 0.13f));
            var font = HumanCannonSectionFontBuilder.LoadFont();
            if (font == null) throw new InvalidOperationException("한글 구간 표시 폰트가 없습니다.");
            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("인간 대포 고도화 배치");
            foreach (var root in roots) Undo.RegisterFullObjectHierarchyUndo(root, "인간 대포 고도화");
            try
            {
                var root = new GameObject(RootName).transform;
                Undo.RegisterCreatedObjectUndo(root.gameObject, "고도화 배치");
                var additions = new[] { new Vector3(0, poles[0].Grip.position.y, 22),
                    new Vector3(-3.5f, poles[0].Grip.position.y, 54),
                    new Vector3(0, poles[2].Grip.position.y, 98), new Vector3(3.5f, poles[2].Grip.position.y, 130) };
                for (int i = 0; i < additions.Length; i++)
                    poles.Add(ClonePole(poles[i < 2 ? 0 : 2], root, i, additions[i]));
                polesField.arraySize = poles.Count;
                for (int i = 0; i < poles.Count; i++) polesField.GetArrayElementAtIndex(i).objectReferenceValue = poles[i];
                systemData.ApplyModifiedPropertiesWithoutUndo();
                foreach (var pole in poles)
                {
                    using var data = new SerializedObject(pole);
                    data.FindProperty("holdSeconds").floatValue = 0;
                    data.FindProperty("airControlSpeed").floatValue = 4.5f;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    TuneSwing(pole);
                    HumanCannonOutline.AttachToHierarchy(pole.gameObject, outline);
                }
                using var courseData = new SerializedObject(course);
                var hazards = courseData.FindProperty("hazards");
                var template = All<HumanCannonHazard>(roots).First(h => h.Kind == CannonHazardKind.GroundSaw);
                int originalCount = hazards.arraySize;
                hazards.arraySize += poles.Count;
                for (int i = 0; i < poles.Count; i++)
                {
                    var saw = Object.Instantiate(template, root);
                    saw.name = "PoleSaw" + i;
                    Undo.RegisterCreatedObjectUndo(saw.gameObject, "봉 왕복 톱");
                    using var data = new SerializedObject(saw);
                    var visual = (Transform)data.FindProperty("visual").objectReferenceValue;
                    visual.localScale *= 0.55f / saw.Radius;
                    data.FindProperty("radius").floatValue = 0.55f;
                    data.FindProperty("phase").floatValue = -Mathf.PI * 0.5f + i * 0.6f;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    saw.gameObject.AddComponent<HumanCannonPoleSaw>().ConfigureAcrossMap(poles[i], saw, -15f, 15f, 6f);
                    saw.Present(0, 1);
                    HumanCannonOutline.AttachToHierarchy(saw.gameObject, outline);
                    hazards.GetArrayElementAtIndex(originalCount + i).objectReferenceValue = saw;
                }
                courseData.ApplyModifiedPropertiesWithoutUndo();
                foreach (var station in All<HumanCannonStation>(roots))
                {
                    using var data = new SerializedObject(station);
                    data.FindProperty("airControlSpeed").floatValue = 4.5f;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                foreach (var trampoline in All<HumanCannonTrampoline>(roots))
                {
                    using var data = new SerializedObject(trampoline);
                    data.FindProperty("groundBounceAirControlSpeed").floatValue = 4.5f;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    HumanCannonOutline.AttachToHierarchy(trampoline.gameObject, outline);
                }
                foreach (var hazard in All<HumanCannonHazard>(roots)) HumanCannonOutline.AttachToHierarchy(hazard.gameObject, outline);
                foreach (var gate in All<HumanCannonSawGate>(roots))
                    HumanCannonOutline.AttachToHierarchy(gate.transform.Find("SawVisual").gameObject, outline);
                foreach (var final in All<HumanCannonFinalSaws>(roots))
                {
                    using var data = new SerializedObject(final);
                    var horizontal = data.FindProperty("horizontalViews");
                    for (int i = 0; i < horizontal.arraySize; i++)
                        HumanCannonOutline.AttachToHierarchy(((Transform)horizontal.GetArrayElementAtIndex(i).objectReferenceValue).gameObject, outline);
                    foreach (string field in new[] { "verticalView", "secondaryVerticalView" })
                        if (data.FindProperty(field).objectReferenceValue is Transform view)
                            HumanCannonOutline.AttachToHierarchy(view.gameObject, outline);
                }
                root.gameObject.AddComponent<HumanCannonCourseSections>().Configure(
                    new HumanCannonCourseSections.Boundaries(18, 72, 90, 141.5f, 5), font);
                Sign(root, "Section1", "1구간 · 출발", new Vector3(-12, 3, 18), font, sign);
                Sign(root, "Shelter", "쉼터 · 동료 합류", new Vector3(-12, 9, 72), font, sign);
                Sign(root, "Section2", "2구간 · 반동과 회피", new Vector3(-12, 9, 90), font, sign);
                Sign(root, "Final", "최종 구간 · 도착", new Vector3(-12, 9, 143), font, sign);
                Physics.SyncTransforms();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("고도화 씬 저장 실패");
                Undo.CollapseUndoOperations(undo);
                Debug.Log("[HumanCannon] 고도화 저장: 봉10/몸통 톱10/보정4.5/외곽선/구간 표시");
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        static void TuneSwing(HumanCannonPole pole)
        {
            using var data = new SerializedObject(pole);
            data.FindProperty("swingAcceleration").floatValue = 180f;
            data.FindProperty("maxSwingAngle").floatValue = 50f;
            data.FindProperty("maxSwingSpeed").floatValue = 100f;
            data.FindProperty("swingDamping").floatValue = 0.4f;
            data.FindProperty("releaseUpwardSpeed").floatValue = 6f;
            data.ApplyModifiedPropertiesWithoutUndo();
            pole.PresentSwing(0);
        }

        static HumanCannonPole ClonePole(HumanCannonPole source, Transform parent, int index, Vector3 position)
        {
            using var sourceData = new SerializedObject(source);
            var originalCeiling = ((Transform)sourceData.FindProperty("leftAnchor").objectReferenceValue).parent;
            var pole = Object.Instantiate(source, parent, true);
            pole.name = "AddedPole" + index;
            var delta = position - pole.Grip.position;
            pole.transform.position += delta;
            var ceiling = Object.Instantiate(originalCeiling, parent, true);
            ceiling.name = pole.name + "Ceiling";
            ceiling.position += delta;
            Undo.RegisterCreatedObjectUndo(pole.gameObject, "추가 봉");
            Undo.RegisterCreatedObjectUndo(ceiling.gameObject, "추가 봉 케이블");
            using var data = new SerializedObject(pole);
            foreach (string field in new[] { "leftCable", "rightCable", "leftAnchor", "rightAnchor" })
                data.FindProperty(field).objectReferenceValue = ceiling.Find(char.ToUpperInvariant(field[0]) + field.Substring(1));
            data.ApplyModifiedPropertiesWithoutUndo();
            return pole;
        }

        static void Sign(Transform parent, string name, string text, Vector3 position, TMP_FontAsset font, Material material)
        {
            var sign = new GameObject(name).transform;
            sign.SetParent(parent, false); sign.position = position;
            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "Plate"; plate.transform.SetParent(sign, false);
            plate.transform.localScale = new Vector3(7, 1.7f, 0.1f);
            Object.DestroyImmediate(plate.GetComponent<Collider>());
            plate.GetComponent<Renderer>().sharedMaterial = material;
            var label = new GameObject("Label").AddComponent<TextMeshPro>();
            label.transform.SetParent(sign, false); label.transform.localPosition = Vector3.back * 0.07f;
            label.font = font; label.text = text; label.fontSize = 5; label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(6.7f, 1.5f);
            label.color = new Color(0.4f, 0.95f, 1f);
        }

        static Material Material(string name, Shader shader, Color color)
        {
            string path = Materials + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_OutlineColor")) material.SetColor("_OutlineColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static T[] All<T>(GameObject[] roots) where T : Component => roots.SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    }
}
