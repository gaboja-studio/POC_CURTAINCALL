using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    /// <summary>Changes only material slots on the recorded pursuit floors and break panels.</summary>
    public static class EscapePursuerEditColors
    {
        const string MenuPath = "Tools/CurtainCall/탈출로/추격자 발판·파괴벽 핑크색";
        const string BackupPath = "Assets/Scenes/Levels/EscapeFactoryArt/pursuer-edit-colors.json";
        const string PinkPath = "Assets/Scenes/Levels/EscapeFactoryArt/pursuer-edit-pink.mat";

        [Serializable] public sealed class Entry
        {
            public string renderer, path;
            public string[] materials;
        }
        [Serializable] public sealed class Backup
        {
            public string scene;
            public Entry[] items;
        }

        static Backup ReadBackup()
        {
            var backup = JsonConvert.DeserializeObject<Backup>(File.ReadAllText(BackupPath));
            if (backup?.items == null || backup.items.Length == 0)
                throw new InvalidOperationException("추격자 원래 재질 기록이 없거나 비어 있습니다.");
            return backup;
        }

        static T Resolve<T>(string value) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(value)) return null;
            if (!GlobalObjectId.TryParse(value, out var id))
                throw new InvalidOperationException("잘못된 재질 복구 ID: " + value);
            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as T;
        }

        static bool IsPink(Material material) => material && AssetDatabase.GetAssetPath(material) == PinkPath;

        public static bool IsEnabled()
        {
            if (!File.Exists(BackupPath)) return false;
            return ReadBackup().items.Any(e => Resolve<Renderer>(e.renderer) is var r && r && r.sharedMaterials.Any(IsPink));
        }

        [MenuItem(MenuPath)]
        public static void Toggle() => SetEnabled(!IsEnabled());

        [MenuItem(MenuPath, true)]
        static bool Validate()
        {
            UnityEditor.Menu.SetChecked(MenuPath, IsEnabled());
            return !EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(BackupPath)
                && SceneManager.GetActiveScene().path == ReadBackup().scene;
        }

        public static object SetEnabled(bool enabled)
        {
            var backup = ReadBackup();
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != backup.scene)
                throw new InvalidOperationException("탈출로 escape_route 씬의 편집 모드에서 사용하세요.");
            var pink = AssetDatabase.LoadAssetAtPath<Material>(PinkPath);
            if (enabled && !pink) throw new InvalidOperationException("핑크색 재질이 없습니다.");

            // Resolve every original asset before changing anything, so a missing asset
            // cannot leave a partially restored scene. Deleted map objects are skipped.
            var changes = backup.items.Select(e => new
            {
                entry = e, renderer = Resolve<Renderer>(e.renderer),
                originals = e.materials.Select(id => Resolve<Material>(id)).ToArray()
            }).ToArray();
            foreach (var change in changes)
                for (int i = 0; i < change.originals.Length; i++)
                    if (!string.IsNullOrEmpty(change.entry.materials[i]) && !change.originals[i])
                        throw new InvalidOperationException("원래 재질 에셋을 찾을 수 없습니다: " + change.entry.path);

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(enabled ? "추격자 발판·파괴벽 핑크색 켜기" : "추격자 원래 재질 복구");
            int changed = 0, deleted = 0, skipped = 0;
            foreach (var change in changes)
            {
                var renderer = change.renderer;
                if (!renderer) { deleted++; continue; }
                var current = renderer.sharedMaterials;
                if (current.Length != change.originals.Length) { skipped++; continue; }
                var next = (Material[])current.Clone();
                for (int i = 0; i < current.Length; i++)
                {
                    // Keep any material the user deliberately assigned while editing.
                    if (enabled && (current[i] == change.originals[i] || IsPink(current[i]))) next[i] = pink;
                    else if (!enabled && IsPink(current[i])) next[i] = change.originals[i];
                }
                if (current.SequenceEqual(next)) continue;
                Undo.RecordObject(renderer, "추격자 임시 색상");
                renderer.sharedMaterials = next;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                EditorUtility.SetDirty(renderer);
                changed++;
            }
            Undo.CollapseUndoOperations(group);
            if (changed > 0) EditorSceneManager.MarkSceneDirty(scene);
            SceneView.RepaintAll();
            Debug.Log($"추격자 핑크색 {(enabled ? "켜짐" : "꺼짐")}: 재질 {changed}개, 삭제된 대상 {deleted}개, 슬롯 변경 제외 {skipped}개");
            return new { enabled, changed, deleted, skipped, recorded = backup.items.Length };
        }
    }
}
