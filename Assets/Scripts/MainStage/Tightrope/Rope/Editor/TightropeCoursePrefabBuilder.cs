using CurtainCall.Network.PlayerSync;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.Tightrope.EditorTools
{
    /// <summary>
    /// 외줄 코스 스테이지 프리팹을 만든다. 메뉴: Tools/CurtainCall/Build Tightrope Course Prefab
    /// 코스 모양은 프리팹의 TightropeCourse 인스펙터 값으로 바꾼다. 다시 실행하면 덮어쓰므로 값을 바꾼 뒤에는 실행하지 않는다.
    /// </summary>
    static class TightropeCoursePrefabBuilder
    {
        const string PrefabPath = "Assets/Resources/Prefabs/Objects/Interactables/Tightrope/TightropeCourse.prefab";
        const int SpawnCount = 4;

        [MenuItem("Tools/CurtainCall/Build Tightrope Course Prefab")]
        static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null &&
                !EditorUtility.DisplayDialog("외줄 코스 프리팹", $"{PrefabPath}가 이미 있습니다. 덮어쓸까요?\n(인스펙터 값을 바꿨다면 사라집니다)", "덮어쓰기", "취소"))
                return;

            var root = new GameObject("TightropeCourse");
            var spawnRoot = new GameObject("SpawnPoints").transform;
            spawnRoot.SetParent(root.transform, false);

            var points = new Transform[SpawnCount];
            for (int slot = 0; slot < SpawnCount; slot++)
            {
                points[slot] = new GameObject($"Slot{slot}").transform;
                points[slot].SetParent(spawnRoot, false);
            }

            var course = root.AddComponent<TightropeCourse>();
            SetArray(new SerializedObject(course), "spawnPoints", points);
            SetArray(new SerializedObject(root.AddComponent<PlayerSpawnPoints>()), "points", points);
            root.AddComponent<CourseControlSwitcher>();
            root.AddComponent<CourseShapeSync>();
            root.AddComponent<TightropeRun>();
            course.Rebuild(); // 출발 위치를 줄에 맞춘다(에디터에서는 메시를 만들지 않음)

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[Tightrope] 코스 프리팹을 만들었습니다: {PrefabPath}");
        }

        static void SetArray(SerializedObject target, string property, Transform[] values)
        {
            var array = target.FindProperty(property);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            target.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
