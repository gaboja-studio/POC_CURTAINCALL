using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.HumanCannon.EditorTools
{
    public static class HumanCannonOpenCourseBuilder
    {
        [MenuItem("Tools/CurtainCall/인간 대포/측벽과 강풍 제거")]
        public static void ApplyToOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty
                || scene.path != HumanCannonTestSceneBuilder.ScenePath)
                throw new InvalidOperationException("Play 종료 후 저장된 HumanCannon 전용 씬에서 실행하세요.");
            var roots = scene.GetRootGameObjects();
            var courses = roots.SelectMany(r => r.GetComponentsInChildren<HumanCannonHazardCourse>(true)).ToArray();
            if (courses.Length != 1) throw new InvalidOperationException("인간 대포 함정 코스 하나가 필요합니다.");
            var fences = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Where(t => t.name == "SideFences").ToArray();
            var winds = roots.SelectMany(r => r.GetComponentsInChildren<HumanCannonWindZone>(true)).ToArray();
            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("인간 대포 측벽과 강풍 제거");
            Undo.RecordObject(courses[0], "강풍 참조 제거");
            try
            {
                using (var data = new SerializedObject(courses[0]))
                {
                    data.FindProperty("windZones").ClearArray();
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                foreach (var wind in winds) Undo.DestroyObjectImmediate(wind.gameObject);
                foreach (var fence in fences) Undo.DestroyObjectImmediate(fence.gameObject);
                Physics.SyncTransforms();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("측벽/강풍 제거 씬 저장 실패");
                Undo.CollapseUndoOperations(undo);
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }
    }
}
