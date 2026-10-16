using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.HumanCannon.EditorTools
{
    /// <summary>현재 코스의 봉 외형을 공유 반동 각도에 연결한다. 저장은 일괄 작업 호출자가 담당한다.</summary>
    public static class HumanCannonPoleSwingVisualBuilder
    {
        public static void ApplyToOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != HumanCannonTestSceneBuilder.ScenePath)
                throw new InvalidOperationException("Play 종료 후 HumanCannon 전용 씬에서 실행하세요.");
            var roots = scene.GetRootGameObjects();
            var extension = roots.Single(root => root.name == "ExtendedCourse044").transform;
            var system = roots.SelectMany(root => root.GetComponentsInChildren<HumanCannonPoleSystem>(true)).Single();
            using var systemData = new SerializedObject(system);
            var list = systemData.FindProperty("poles");
            if (list == null || list.arraySize != 6) throw new InvalidOperationException("현재 코스의 봉 6개 연결이 필요합니다.");
            var poles = Enumerable.Range(0, list.arraySize)
                .Select(index => list.GetArrayElementAtIndex(index).objectReferenceValue as HumanCannonPole).ToArray();
            if (poles.Any(pole => pole == null)) throw new InvalidOperationException("비어 있는 봉 연결이 있습니다.");
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("봉 반동과 케이블 외형 연결");
            try
            {
                for (int index = 0; index < poles.Length; index++)
                {
                    var pole = poles[index];
                    var ceiling = Require(extension, index < 4 ? "ExistingCeiling" + index : pole.name + "Ceiling");
                    Undo.RegisterFullObjectHierarchyUndo(pole.gameObject, "봉 외형 연결");
                    Undo.RegisterFullObjectHierarchyUndo(ceiling.gameObject, "케이블 연결");
                    var visual = pole.transform.Find("SwingVisual");
                    if (visual == null)
                    {
                        var bar = Require(pole.transform, "Bar");
                        visual = new GameObject("SwingVisual").transform;
                        visual.SetParent(pole.transform, false);
                        visual.SetPositionAndRotation(pole.Grip.position, pole.Grip.rotation);
                        Undo.RegisterCreatedObjectUndo(visual.gameObject, "움직이는 봉 외형");
                        // 연결봉 검정 띠도 봉과 함께 움직여 원래 색 구분을 유지한다.
                        var bands = pole.transform.Cast<Transform>().Where(child => child.name.StartsWith("BlackBand", StringComparison.Ordinal)).ToArray();
                        Undo.SetTransformParent(bar, visual, "봉 외형 묶기");
                        foreach (var band in bands) Undo.SetTransformParent(band, visual, "봉 띠 묶기");
                    }
                    using var data = new SerializedObject(pole);
                    Set(data, "visualBar", visual);
                    Set(data, "leftCable", Require(ceiling, "LeftCable"));
                    Set(data, "rightCable", Require(ceiling, "RightCable"));
                    Set(data, "leftAnchor", Require(ceiling, "LeftAnchor"));
                    Set(data, "rightAnchor", Require(ceiling, "RightAnchor"));
                    data.ApplyModifiedPropertiesWithoutUndo();
                    pole.PresentSwing(0f);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(undo);
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        static Transform Require(Transform parent, string name) => parent.Find(name)
            ?? throw new InvalidOperationException("봉 외형 연결이 없습니다: " + parent.name + "/" + name);
        static void Set(SerializedObject data, string field, Transform value) => data.FindProperty(field).objectReferenceValue = value;
    }
}
