using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.Sound.Editor
{
    public sealed partial class SoundSettingsWindow
    {
        Vector2 situationScroll;

        void DrawSituationsTab()
        {
            EditorGUILayout.HelpBox(
                "게임 코드가 소리를 내는 지점(상황)마다 어떤 사운드(시트 ID)를 낼지 고릅니다. 바꾼 값은 같은 상황의 모든 재생 지점에 바로 적용됩니다.\n" +
                "새 상황은 '+ 상황 추가' 후 코드에 표시된 한 줄(SoundManager.Play)을 넣으면 연결됩니다. 상황을 지우면 그 코드 지점은 소리를 내지 않습니다.",
                MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("코드 위치 새로고침", "스크립트를 다시 읽어 각 상황을 재생하는 코드 줄을 찾습니다."), GUILayout.Width(140))) SoundCodeUsage.Refresh();
            }

            situationScroll = EditorGUILayout.BeginScrollView(situationScroll);
            var list = serialized.FindProperty(nameof(SoundCatalog.situations));
            int remove = -1;
            for (int i = 0; i < list.arraySize; i++)
            {
                var item = list.GetArrayElementAtIndex(i);
                if (!Matches(item.FindPropertyRelative(nameof(SoundSituation.key)).stringValue,
                        item.FindPropertyRelative(nameof(SoundSituation.label)).stringValue,
                        item.FindPropertyRelative(nameof(SoundSituation.cueId)).stringValue)) continue;
                if (DrawSituation(item, i)) remove = i;
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ 상황 추가", GUILayout.Height(24))) AddSituation(list);
                var missing = SoundSituations.Defaults.Where(d => catalog.FindSituation(d.key) == null).ToArray();
                if (missing.Length > 0 && GUILayout.Button(new GUIContent($"+ 코드 기본 상황 {missing.Length}개 복원", string.Join("\n", missing.Select(m => m.key))), GUILayout.Height(24)))
                {
                    serialized.ApplyModifiedProperties();
                    Undo.RecordObject(catalog, "사운드 상황 복원");
                    AddMissingSituations(catalog);
                    EditorUtility.SetDirty(catalog);
                    serialized.Update();
                }
            }
            if (remove >= 0) list.DeleteArrayElementAtIndex(remove);
        }

        /// <returns>삭제 요청이면 true.</returns>
        bool DrawSituation(SerializedProperty item, int index)
        {
            var key = item.FindPropertyRelative(nameof(SoundSituation.key));
            var label = item.FindPropertyRelative(nameof(SoundSituation.label));
            var cueId = item.FindPropertyRelative(nameof(SoundSituation.cueId));
            var enabled = item.FindPropertyRelative(nameof(SoundSituation.enabled));
            var scale = item.FindPropertyRelative(nameof(SoundSituation.volumeScale));
            string foldKey = "situation:" + (string.IsNullOrEmpty(key.stringValue) ? "#" + index : key.stringValue);
            bool open = expanded.Contains(foldKey);
            var cue = catalog.Find(cueId.stringValue);
            var usages = SoundCodeUsage.For(key.stringValue);
            bool delete = false;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool next = EditorGUILayout.Foldout(open, GUIContent.none, true);
                    if (next != open) { if (next) expanded.Add(foldKey); else expanded.Remove(foldKey); open = next; }
                    EditorGUILayout.PropertyField(enabled, GUIContent.none, GUILayout.Width(16));
                    GUILayout.Label(new GUIContent(label.stringValue, key.stringValue), EditorStyles.boldLabel, GUILayout.MinWidth(120));
                    GUILayout.FlexibleSpace();
                    DrawCuePopup(cueId, GUILayout.Width(220));
                    GUILayout.Label(new GUIContent(usages.Count > 0 ? $"코드 {usages.Count}곳" : "코드 없음",
                        usages.Count > 0 ? string.Join("\n", usages.Select(u => u.Label)) : "이 상황 키를 재생하는 코드가 없습니다."), EditorStyles.miniLabel, GUILayout.Width(58));
                    using (new EditorGUI.DisabledScope(cue == null || !cue.HasClip))
                        if (GUILayout.Button(new GUIContent("▶", "이 상황에서 날 소리 미리듣기(음량 배율 포함)"), GUILayout.Width(28)))
                        {
                            serialized.ApplyModifiedProperties();
                            SoundPreviewPlayer.Play(catalog, cue, scale.floatValue, key.stringValue);
                        }
                    if (GUILayout.Button("■", GUILayout.Width(28))) SoundPreviewPlayer.Stop();
                    if (Application.isPlaying && GUILayout.Button(new GUIContent("게임", "플레이 중인 게임에서 이 상황 키로 재생(코드와 같은 경로)"), GUILayout.Width(40)))
                    {
                        serialized.ApplyModifiedProperties();
                        SoundManager.Play(key.stringValue);
                    }
                }
                ProgressBar(key.stringValue);
                if (!open) return false;

                EditorGUILayout.Space(2);
                EditorGUILayout.PropertyField(label, new GUIContent("상황 이름"));
                EditorGUILayout.PropertyField(key, new GUIContent("상황 키", "코드가 부르는 값. 바꾸면 코드도 같이 바꿔야 합니다."));
                EditorGUILayout.PropertyField(item.FindPropertyRelative(nameof(SoundSituation.description)), new GUIContent("설명"));
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel("사운드 (시트 ID)");
                    DrawCuePopup(cueId);
                    if (cue != null && GUILayout.Button(new GUIContent("편집", "사운드 탭에서 이 시트 ID를 엽니다."), GUILayout.Width(44)))
                    {
                        tab = 0;
                        search = cue.id;
                        expanded.Add("cue:" + cue.id);
                    }
                }
                EditorGUILayout.PropertyField(scale, new GUIContent("음량 배율", "이 상황에서만 사운드 음량에 곱합니다."));

                EditorGUILayout.LabelField("코드 위치", EditorStyles.boldLabel);
                if (usages.Count == 0)
                    EditorGUILayout.LabelField("없음 — 아래 한 줄을 소리가 나야 할 코드에 넣으세요.", EditorStyles.wordWrappedMiniLabel);
                foreach (var usage in usages)
                    if (GUILayout.Button(new GUIContent($"{usage.Label}   {usage.Text}", "눌러서 스크립트 열기"), EditorStyles.linkLabel))
                        SoundCodeUsage.Open(usage.Path, usage.Line);
                using (new EditorGUILayout.HorizontalScope())
                {
                    string snippet = $"SoundManager.Play(\"{key.stringValue}\");";
                    EditorGUILayout.SelectableLabel(snippet, EditorStyles.textField, GUILayout.Height(18));
                    if (GUILayout.Button("복사", GUILayout.Width(44))) EditorGUIUtility.systemCopyBuffer = snippet;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("삭제", GUILayout.Width(60))
                        && EditorUtility.DisplayDialog("상황 삭제",
                            $"'{label.stringValue}' 상황을 삭제할까요?" + (usages.Count > 0 ? $"\n코드 {usages.Count}곳에서 이 상황을 재생 중이며, 삭제하면 그 지점은 소리가 나지 않습니다(경고 로그)." : ""),
                            "삭제", "취소"))
                        delete = true;
                }
            }
            return delete;
        }

        void DrawCuePopup(SerializedProperty cueId, params GUILayoutOption[] options)
        {
            var ids = catalog.cues.Where(c => c != null && !string.IsNullOrEmpty(c.id)).Select(c => c.id).Distinct().ToList();
            var names = new[] { "(소리 없음)" }.Concat(ids.Select(id => $"{id}  {catalog.Find(id)?.situation}")).ToList();
            int current = string.IsNullOrEmpty(cueId.stringValue) ? 0 : ids.IndexOf(cueId.stringValue) + 1;
            if (current == 0 && !string.IsNullOrEmpty(cueId.stringValue))
            {
                names.Add($"{cueId.stringValue}  (사운드 없음!)");
                current = names.Count - 1;
            }
            int picked = EditorGUILayout.Popup(current, names.ToArray(), options);
            if (picked == current) return;
            cueId.stringValue = picked == 0 ? "" : picked <= ids.Count ? ids[picked - 1] : cueId.stringValue;
        }

        void AddSituation(SerializedProperty list)
        {
            list.arraySize++;
            var item = list.GetArrayElementAtIndex(list.arraySize - 1);
            string key = "custom.new-" + list.arraySize;
            item.FindPropertyRelative(nameof(SoundSituation.key)).stringValue = key;
            item.FindPropertyRelative(nameof(SoundSituation.label)).stringValue = "새 상황";
            item.FindPropertyRelative(nameof(SoundSituation.description)).stringValue = "";
            item.FindPropertyRelative(nameof(SoundSituation.cueId)).stringValue = "";
            item.FindPropertyRelative(nameof(SoundSituation.enabled)).boolValue = true;
            item.FindPropertyRelative(nameof(SoundSituation.volumeScale)).floatValue = 1f;
            expanded.Add("situation:" + key);
        }
    }
}
