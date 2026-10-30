using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        public static string RemoveAConveyorRecovery()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath)
                throw new InvalidOperationException("A edit scene required");
            var root = GameObject.Find(RootName).transform.Find("43_AConveyorRecovery");
            if (root == null) throw new InvalidOperationException("Repaired conveyor required");
            int removed = 0;
            void Remove(Transform target)
            {
                if (target == null) return;
                removed += target.GetComponentsInChildren<Transform>(true).Length;
                Undo.DestroyObjectImmediate(target.gameObject);
            }
            Remove(root.Find("추락 후 재탑승 정비로"));
            Remove(root.Find("그네 재탑승 대기대"));
            foreach (var t in root.Cast<Transform>().ToArray())
                if (t.name == "진행 방향 화살표" && Vector3.Distance(t.position, new Vector3(67, -19.18f, -17.5f)) < .1f
                    || t.name.Contains("정비등") && Vector3.Distance(t.position, new Vector3(76, -13, -18)) < .2f
                    || t.name.StartsWith("작업등") && Vector3.Distance(t.position, new Vector3(76, -13, -18)) < .2f) Remove(t);
            var finish = root.Find("명확한 승하차 마감");
            if (finish != null) foreach (var t in finish.Cast<Transform>().ToArray())
                if (t.name.Contains("회수") || t.name.Contains("재탑승") || t.name == "진행 방향 화살표"
                    || t.name.Contains("정비등") || t.name.StartsWith("작업등")) Remove(t);

            // Remove the old visible sump floor too. Distant, non-colliding black enclosure
            // hides the outside sky without providing another landing/recovery surface.
            var envelope = GameObject.Find(RootName).transform.Find("35_AOpenFacility/02_ExpandedPumpEnvelope");
            Remove(envelope != null ? envelope.Find("확장 설비동 심부 집수정") : null);
            if (root.Find("컨베이어 심부 암부 차폐") == null)
            {
                var abyss = VerticalChild(root, "컨베이어 심부 암부 차폐");
                var material = Mat("conveyor-void", new Color(.003f, .004f, .005f));
                BMass(abyss, "먼 심부 암부", new Vector3(83, -400, -37.5f), new Vector3(126, 1, 89), material, false);
                foreach (float x in new[] { 20f, 146f })
                    BMass(abyss, "심부 측벽 차폐", new Vector3(x, -243, -37.5f), new Vector3(3, 314, 89), material, false);
                foreach (float z in new[] { -82f, 7f })
                    BMass(abyss, "심부 끝벽 차폐", new Vector3(83, -243, z), new Vector3(126, 314, 3), material, false);
            }

            var fall = root.Find("컨베이어 낙사 영역");
            if (fall == null)
            {
                var go = new GameObject("컨베이어 낙사 영역");
                Undo.RegisterCreatedObjectUndo(go, "Conveyor fall void"); fall = go.transform; fall.SetParent(root, false);
                Undo.AddComponent<EscapeFallVoid>(go);
            }
            var locks = UnityEngine.Object.FindFirstObjectByType<EscapeCooperativeLocks>();
            var data = new SerializedObject(locks);
            data.FindProperty("checkpoints").GetArrayElementAtIndex(0).FindPropertyRelative("instruction").stringValue =
                "노란 하차장 → 전원 버튼에 F → 톱 통과 → 복귀 대포 탑승 · 아래로 떨어지면 낙사";
            data.ApplyModifiedProperties();
            Undo.FlushUndoRecordObjects(); Physics.SyncTransforms(); EditorSceneManager.MarkSceneDirty(scene);
            return $"Removed {removed} recovery objects; conveyor, intended landing and exit route retained; host fall void at Y -30";
        }
    }
}
