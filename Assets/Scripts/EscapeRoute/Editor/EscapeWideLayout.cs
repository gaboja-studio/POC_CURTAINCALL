using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        public const float HorizontalExpansion = 1.25f;

        [MenuItem("Tools/CurtainCall/탈출로/통로 제거와 수평 25퍼센트 확장")]
        public static void ApplyWideLayout()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("escape_route 편집 모드가 필요합니다.");
            var background = scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName)?.transform;
            if (background == null) throw new InvalidOperationException("탈출로 배경이 없습니다.");
            var expandedScale = new Vector3(HorizontalExpansion, 1f, HorizontalExpansion);
            bool expanded = (background.localScale - expandedScale).sqrMagnitude < .000001f;
            if (!expanded && (background.localScale - Vector3.one).sqrMagnitude > .000001f)
                throw new InvalidOperationException("배경에 별도 배율이 설정되어 있습니다. 중복 확대하지 않습니다.");

            var reservation = background.Find("09_MaintenanceReservation");
            if (reservation != null) Undo.DestroyObjectImmediate(reservation.gameObject);
            if (!expanded)
            {
                // One parent-space expansion preserves the Y axis, even on rotated wall modules.
                // Props and labels are compensated afterwards; rail spans and cables stay connected.
                var fixedSize = background.GetComponentsInChildren<Transform>(true)
                    .Where(KeepAuthoredSize).OrderBy(TransformDepth)
                    .Select(t => new { transform = t, lengths = WorldAxisLengths(t) }).ToArray();
                Undo.RecordObject(background, "Widen escape background");
                background.localScale = expandedScale;
                foreach (var item in fixedSize)
                {
                    var after = WorldAxisLengths(item.transform);
                    Undo.RecordObject(item.transform, "Keep escape prop scale");
                    item.transform.localScale = Vector3.Scale(item.transform.localScale,
                        new Vector3(item.lengths.x / after.x, item.lengths.y / after.y, item.lengths.z / after.z));
                    PrefabUtility.RecordPrefabInstancePropertyModifications(item.transform);
                }
                var player = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "PlaySetup")?.transform.Find("Player");
                if (player != null)
                {
                    Undo.RecordObject(player, "Keep spawn on expanded route");
                    var local = background.InverseTransformPoint(player.position);
                    // InverseTransformPoint already contains the new scale; restore old local XZ first.
                    local.x *= HorizontalExpansion; local.z *= HorizontalExpansion;
                    player.position = background.TransformPoint(local);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(player);
                }
            }
            string[,] names = {
                { "01_WaitingUnderstage_24x18", "01_WaitingUnderstage_30x22.5" },
                { "02_ConcreteApproach_12x48", "02_ConcreteApproach_15x60" },
                { "03_CannonRopeVoid_40x20", "03_CannonRopeVoid_50x25" },
                { "04_BendAndShutters_16x36", "04_BendAndShutters_20x45" },
                { "05_SwingVoid_40x30", "05_SwingVoid_50x37.5" },
                { "06_Regroup_20x34", "06_Regroup_25x42.5" },
                { "07B_ExitHall_28x30", "07B_ExitHall_35x37.5" },
                { "08_Reeds_24x30", "08_Reeds_30x37.5" }
            };
            for (int i = 0; i < names.GetLength(0); i++)
            {
                var child = background.Find(names[i, 0]);
                if (child == null) continue;
                Undo.RecordObject(child.gameObject, "Update escape dimensions");
                child.name = names[i, 1];
            }
            foreach (var label in background.GetComponentsInChildren<TextMeshPro>(true))
            {
                if (!label.text.Contains("20m 단절")) continue;
                Undo.RecordObject(label, "Update escape gap label");
                label.text = label.text.Replace("20m 단절", "25m 단절");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static bool KeepAuthoredSize(Transform t)
        {
            if (t.GetComponent<TextMeshPro>() != null || t.name == "ScaleReference_1.8m" || t.name == "작업등 전구") return true;
            if (PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) &&
                (t.name.StartsWith("SM_Prop_", StringComparison.Ordinal) || t.name.StartsWith("SM_Grass", StringComparison.Ordinal))) return true;
            return t.name == "대포 설치 자리" || t.name == "트램폴린 설치 자리" || t.name == "진행 레버 위치" ||
                t.name == "버튼 문 위치" || t.name == "출구 잠금 장치";
        }

        static Vector3 WorldAxisLengths(Transform t) => new Vector3(t.TransformVector(Vector3.right).magnitude,
            t.TransformVector(Vector3.up).magnitude, t.TransformVector(Vector3.forward).magnitude);
        static int TransformDepth(Transform t) { int depth = 0; while (t.parent != null) { t = t.parent; depth++; } return depth; }
    }
}
