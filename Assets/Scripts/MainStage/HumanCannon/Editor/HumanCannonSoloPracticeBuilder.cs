using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.Network.Session;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.HumanCannon.EditorTools
{
    /// <summary>원본 코스는 보존하고 옆에 편집 가능한 연습 템플릿을 만든다. 갱신 대상은 생성한 루트뿐이다.</summary>
    public static class HumanCannonSoloPracticeBuilder
    {
        const string RootName = "HumanCannonSoloPractice";
        const string FontPath = "Assets/Scenes/Tests/MainStage/HumanCannon/Labels/distance-font.asset";
        static readonly string[] SourceNames =
        {
            "HumanCannonTest", "Ground", "Cannon", "DistanceMarkers", "Trampoline", "LandingCushion",
            "PoleGateCourse", "SplitRouteCourse", "FinalCannonCourse"
        };

        sealed class RootPose
        {
            public GameObject Root;
            public int Sibling;
            public Vector3 Position, Scale;
            public Quaternion Rotation;
        }

        [MenuItem("Tools/CurtainCall/인간 대포/043 옆 코스 1인 연습 구역 추가")]
        public static void AddToOpenScene() => Build(false);

        [MenuItem("Tools/CurtainCall/인간 대포/043 옆 코스 1인 연습 구역 갱신")]
        public static void RefreshOpenScene() => Build(true);

        static void Build(bool refresh)
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty
                || scene.path != HumanCannonTestSceneBuilder.ScenePath)
                throw new InvalidOperationException("Play 종료 후 저장된 HumanCannon 전용 씬을 여세요.");
            var roots = scene.GetRootGameObjects();
            var previous = roots.SingleOrDefault(root => root.name == RootName);
            if (previous != null && previous.GetComponent<HumanCannonSoloPractice>() == null)
                throw new InvalidOperationException("같은 이름의 사용자 오브젝트가 있어 변경하지 않았습니다.");
            if (previous != null && !refresh)
                throw new InvalidOperationException("연습 구역이 이미 있습니다. 원본을 반영하려면 명시적인 갱신 메뉴를 사용하세요.");
            if (previous == null && refresh)
                throw new InvalidOperationException("갱신할 연습 구역이 없습니다. 먼저 추가 메뉴를 사용하세요.");
            var originals = SourceNames.Select(name => roots.SingleOrDefault(root => root.name == name)).ToArray();
            if (originals.Any(root => root == null)) throw new InvalidOperationException("원본 043 코스의 필수 루트가 없습니다.");
            var systems = originals.SelectMany(root => root.GetComponentsInChildren<HumanCannonSystem>(true)).ToArray();
            if (systems.Length != 1 || systems[0].StationCount != 10)
                throw new InvalidOperationException("원본 대포 6대와 최종 대포 4대가 연결된 코스여야 합니다.");
            var originalObjects = SceneObjects(originals);
            for (int i = 0; i < 10; i++)
            {
                var station = systems[0].Station(i);
                if (station == null || !originalObjects.Contains(station)
                    || i >= 6 && station.name != "FinalCannon" + (i - 5))
                    throw new InvalidOperationException("원본 대포 목록이 예상 코스 밖을 참조합니다.");
            }
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null || font.material == null) throw new InvalidOperationException("기존 거리 라벨 폰트가 없습니다.");

            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(refresh ? "043 1인 연습 구역 갱신" : "043 1인 연습 구역 추가");
            try
            {
                var generated = new GameObject(RootName);
                generated.SetActive(false);
                Undo.RegisterCreatedObjectUndo(generated, "생성된 연습 루트");
                var controller = generated.AddComponent<HumanCannonSoloPractice>();
                var template = CloneTogether(originals, generated.transform);
                foreach (var ui in template.GetComponentsInChildren<NetworkSessionTestUI>(true)) Object.DestroyImmediate(ui);
                foreach (var hud in template.GetComponentsInChildren<HumanCannonTestHud>(true)) hud.enabled = false;
                ValidateReferences(template, originalObjects);
                AddLabels(template.transform, font);
                var data = new SerializedObject(controller);
                var originalList = data.FindProperty("originalRoots");
                var templateField = data.FindProperty("practiceTemplate");
                if (originalList == null || templateField == null)
                    throw new InvalidOperationException("연습 컨트롤러의 originalRoots/practiceTemplate 연결 필드가 없습니다.");
                originalList.arraySize = originals.Length;
                for (int i = 0; i < originals.Length; i++) originalList.GetArrayElementAtIndex(i).objectReferenceValue = originals[i];
                templateField.objectReferenceValue = template;
                data.ApplyModifiedPropertiesWithoutUndo();
                template.SetActive(true); // 편집 화면에서는 실물 배치를 보여 주고, Play 진입의 Awake가 먼저 끈다.
                generated.SetActive(true);
                if (previous != null) Undo.DestroyObjectImmediate(previous);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("1인 연습 구역 저장 실패");
                Undo.CollapseUndoOperations(undo);
                Debug.Log("[HumanCannonPractice] 원본 코스를 보존하고 x+50 연습 템플릿을 저장했습니다.");
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        static GameObject CloneTogether(GameObject[] originals, Transform destination)
        {
            var poses = originals.Select(root => new RootPose
            {
                Root = root, Sibling = root.transform.GetSiblingIndex(), Position = root.transform.localPosition,
                Rotation = root.transform.localRotation, Scale = root.transform.localScale
            }).OrderBy(pose => pose.Sibling).ToArray();
            var combined = new GameObject("SoloPracticeCloneSource");
            combined.SetActive(false);
            try
            {
                // 한 번의 Instantiate가 서로 다른 루트 사이의 대포/봉/관문 참조까지 복사본으로 연결한다.
                foreach (var pose in poses) pose.Root.transform.SetParent(combined.transform, false);
                var clone = Object.Instantiate(combined, destination, false);
                clone.name = "PracticeTemplate";
                clone.transform.localPosition = Vector3.right * 50f;
                return clone;
            }
            finally
            {
                foreach (var pose in poses)
                {
                    var transform = pose.Root.transform;
                    transform.SetParent(null, false);
                    transform.localPosition = pose.Position;
                    transform.localRotation = pose.Rotation;
                    transform.localScale = pose.Scale;
                    transform.SetSiblingIndex(pose.Sibling);
                }
                Object.DestroyImmediate(combined);
            }
        }

        static HashSet<Object> SceneObjects(IEnumerable<GameObject> roots)
        {
            var result = new HashSet<Object>();
            foreach (var root in roots)
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    result.Add(transform.gameObject);
                    foreach (var component in transform.GetComponents<Component>()) if (component != null) result.Add(component);
                }
            return result;
        }

        static void ValidateReferences(GameObject template, HashSet<Object> originals)
        {
            foreach (var component in template.GetComponentsInChildren<Component>(true))
            {
                if (component == null) throw new InvalidOperationException("연습 복사본에 누락된 스크립트가 있습니다.");
                using var data = new SerializedObject(component);
                var property = data.GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference && originals.Contains(property.objectReferenceValue))
                        throw new InvalidOperationException($"복사본이 원본을 참조합니다: {component.name}.{property.propertyPath}");
            }
        }

        static void AddLabels(Transform template, TMP_FontAsset font)
        {
            var labels = new GameObject("PracticeEntryLabels").transform;
            labels.SetParent(template, false);
            Label(labels, font, 1, "DEPARTURE", new Vector3(10, SurfaceTop(template, "Ground"), -3));
            Label(labels, font, 2, "CARRY AND SAW", new Vector3(10, SurfaceTop(template, "SplitRouteCourse/FirstCourseFloor"), 32));
            Label(labels, font, 3, "POLE AND SAW", new Vector3(10, SurfaceTop(template, "SplitRouteCourse/FirstCourseFloor"), 29));
            Label(labels, font, 4, "DEPARTURE LIFT", new Vector3(-10, SurfaceTop(template, "Ground"), 0));
            Label(labels, font, 5, "SHELTER LIFT", new Vector3(-9, SurfaceTop(template, "SplitRouteCourse/FirstCourseFloor"), 36));
            Label(labels, font, 6, "SHELTER DEPARTURE", new Vector3(8, SurfaceTop(template, "SplitRouteCourse/ShelterDeck"), 49));
            Label(labels, font, 7, "CARRY AND SAW 2", new Vector3(10, SurfaceTop(template, "SplitRouteCourse/SecondCourseFloor"), 78));
            Label(labels, font, 8, "POLE AND SWING 2", new Vector3(10, SurfaceTop(template, "SplitRouteCourse/SecondCourseFloor"), 73));
            Label(labels, font, 9, "CUSHION DROP", new Vector3(0, SurfaceTop(template, "LandingCushion"), 84));
            Label(labels, font, 10, "FINAL CANNONS", new Vector3(0, SurfaceTop(template, "FinalCannonCourse/PreparationDeck"), 87.5f));
        }

        static float SurfaceTop(Transform template, string path)
        {
            var surface = template.Find(path);
            if (surface == null) throw new InvalidOperationException("연습 라벨의 바닥이 없습니다: " + path);
            var colliders = surface.GetComponentsInChildren<BoxCollider>(true).Where(c => c.enabled && !c.isTrigger).ToArray();
            if (colliders.Length == 0) throw new InvalidOperationException("연습 라벨의 바닥 Collider가 없습니다: " + path);
            // 비활성 복사본의 Collider.bounds는 비어 있으므로 저장된 BoxCollider 모서리로 윗면을 계산한다.
            return colliders.Max(c => template.InverseTransformPoint(c.transform.TransformPoint(c.center + Vector3.up * c.size.y * 0.5f)).y) + 0.025f;
        }

        static void Label(Transform parent, TMP_FontAsset font, int number, string title, Vector3 position)
        {
            var go = new GameObject("Entry" + number);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var label = go.AddComponent<TextMeshPro>();
            label.font = font;
            label.fontSharedMaterial = font.material;
            label.fontSize = 7;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(8, 2);
            label.text = "ENTRY " + number + "\n" + title;
            label.color = Color.white;
            var renderer = label.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
