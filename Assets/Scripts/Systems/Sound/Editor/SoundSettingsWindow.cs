using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.Sound.Editor
{
    /// <summary>
    /// 사운드 설정 창(Tools/CurtainCall/사운드 설정).
    /// - 사운드: 시트 ID별 클립·음량·피치·재생 구간·루프·지연·재사용 간격.
    /// - 적용 상황: 게임 코드의 재생 지점(상황 키)마다 낼 시트 ID·켜기/끄기·음량 배율, 코드 위치.
    /// - 재생 기록: 플레이 중 실제로 들어온 재생 요청과 호출 위치·결과.
    /// 데이터는 <see cref="SoundCatalog"/> 에셋에 저장된다.
    /// </summary>
    public sealed partial class SoundSettingsWindow : EditorWindow
    {
        const string MenuPath = "Tools/CurtainCall/사운드 설정";
        static readonly string[] TabNames = { "사운드 (시트 ID)", "적용 상황", "재생 기록" };

        SoundCatalog catalog;
        SerializedObject serialized;
        Vector2 scroll;
        string search = "";
        int tab;
        readonly HashSet<string> expanded = new();

        [MenuItem(MenuPath)]
        public static void Open() => GetWindow<SoundSettingsWindow>("사운드 설정").Show();

        public static void Open(SoundCatalog target)
        {
            var window = GetWindow<SoundSettingsWindow>("사운드 설정");
            window.Bind(target);
            window.Show();
        }

        void OnEnable()
        {
            if (catalog == null) Bind(AssetDatabase.LoadAssetAtPath<SoundCatalog>(SoundCatalog.AssetPath));
            SoundManager.Recorded -= OnRecorded;
            SoundManager.Recorded += OnRecorded;
        }

        void OnDisable()
        {
            SoundPreviewPlayer.Stop();
            SoundManager.Recorded -= OnRecorded;
        }

        void OnRecorded(SoundPlayRecord _) => Repaint();

        void Bind(SoundCatalog target)
        {
            catalog = target;
            serialized = target != null ? new SerializedObject(target) : null;
        }

        void Update()
        {
            if (SoundPreviewPlayer.IsPlaying || Application.isPlaying && tab == 2) Repaint();
        }

        void OnGUI()
        {
            DrawToolbar();
            if (catalog == null)
            {
                EditorGUILayout.HelpBox($"사운드 카탈로그가 없습니다. 게임은 Resources/{SoundCatalog.ResourcePath} 에셋을 읽습니다.", MessageType.Warning);
                if (GUILayout.Button("카탈로그 만들기 (기본 사운드·상황 포함)", GUILayout.Height(28))) Bind(CreateCatalog());
                return;
            }
            if (serialized == null || serialized.targetObject != catalog) Bind(catalog);
            serialized.Update();

            tab = GUILayout.Toolbar(tab, TabNames, GUILayout.Height(26));
            EditorGUILayout.Space(4);
            switch (tab)
            {
                case 0: DrawSoundsTab(); break;
                case 1: DrawSituationsTab(); break;
                default: DrawHistoryTab(); break;
            }
            serialized.ApplyModifiedProperties();
        }

        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var picked = (SoundCatalog)EditorGUILayout.ObjectField(catalog, typeof(SoundCatalog), false, GUILayout.Width(240));
                if (picked != catalog) Bind(picked);
                GUILayout.Space(8);
                search = GUILayout.TextField(search, EditorStyles.toolbarSearchField, GUILayout.MinWidth(120));
                GUILayout.FlexibleSpace();
                if (SoundPreviewPlayer.IsPlaying && GUILayout.Button("■ 미리듣기 정지", EditorStyles.toolbarButton)) SoundPreviewPlayer.Stop();
                if (Application.isPlaying && GUILayout.Button("■ 게임 소리 정지", EditorStyles.toolbarButton)) SoundManager.StopAll();
                if (catalog != null && GUILayout.Button("에셋 선택", EditorStyles.toolbarButton))
                {
                    Selection.activeObject = catalog;
                    EditorGUIUtility.PingObject(catalog);
                }
            }
        }

        bool Matches(params string[] fields)
        {
            if (string.IsNullOrWhiteSpace(search)) return true;
            return fields.Any(f => !string.IsNullOrEmpty(f) && f.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0);
        }

        static void ProgressBar(string id)
        {
            float progress = SoundPreviewPlayer.Progress(id);
            if (progress < 0f) return;
            var bar = GUILayoutUtility.GetRect(1, 3, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(new Rect(bar.x, bar.y, bar.width * progress, bar.height), new Color(.35f, .75f, 1f));
        }

        // ── 사운드 탭 ─────────────────────────────────────

        void DrawSoundsTab()
        {
            DrawGlobal();
            EditorGUILayout.Space(4);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            var cues = serialized.FindProperty(nameof(SoundCatalog.cues));
            int remove = -1;
            for (int i = 0; i < cues.arraySize; i++)
            {
                var cue = cues.GetArrayElementAtIndex(i);
                if (!Matches(cue.FindPropertyRelative(nameof(SoundCue.id)).stringValue, cue.FindPropertyRelative(nameof(SoundCue.situation)).stringValue)) continue;
                if (DrawCue(cue, i)) remove = i;
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ 사운드 추가", GUILayout.Height(24))) AddCue(cues);
                // 적용 상황이 가리키지만 아직 없는 시트 ID.
                foreach (var id in catalog.situations.Where(s => s != null && !string.IsNullOrEmpty(s.cueId) && catalog.Find(s.cueId) == null)
                             .Select(s => s.cueId).Distinct())
                    if (GUILayout.Button($"+ {id} (상황에서 사용 중)", GUILayout.Height(24))) AddCue(cues, id);
            }
            if (remove >= 0) cues.DeleteArrayElementAtIndex(remove);
        }

        void DrawGlobal()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("분류별 음량 (사용자 설정의 효과음·배경음 음량에 곱함)", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serialized.FindProperty(nameof(SoundCatalog.uiVolume)), new GUIContent("UI"));
                EditorGUILayout.PropertyField(serialized.FindProperty(nameof(SoundCatalog.sfxVolume)), new GUIContent("효과음"));
                EditorGUILayout.PropertyField(serialized.FindProperty(nameof(SoundCatalog.bgmVolume)), new GUIContent("배경음"));
                EditorGUILayout.PropertyField(serialized.FindProperty(nameof(SoundCatalog.voices)), new GUIContent("동시 재생 수"));
            }
        }

        /// <returns>삭제 요청이면 true.</returns>
        bool DrawCue(SerializedProperty cue, int index)
        {
            var id = cue.FindPropertyRelative(nameof(SoundCue.id));
            var situation = cue.FindPropertyRelative(nameof(SoundCue.situation));
            var enabled = cue.FindPropertyRelative(nameof(SoundCue.enabled));
            var data = catalog.cues[index];
            string key = "cue:" + (string.IsNullOrEmpty(id.stringValue) ? "#" + index : id.stringValue);
            bool open = expanded.Contains(key);
            bool delete = false;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool next = EditorGUILayout.Foldout(open, GUIContent.none, true);
                    if (next != open) { if (next) expanded.Add(key); else expanded.Remove(key); open = next; }
                    EditorGUILayout.PropertyField(enabled, GUIContent.none, GUILayout.Width(16));
                    GUILayout.Label(id.stringValue, EditorStyles.boldLabel, GUILayout.Width(70));
                    GUILayout.Label(situation.stringValue, GUILayout.MinWidth(80));
                    GUILayout.FlexibleSpace();
                    DrawCueBadges(id.stringValue, data);
                    using (new EditorGUI.DisabledScope(!data.HasClip))
                        if (GUILayout.Button(new GUIContent("▶", "미리듣기(편집 모드 가능, 꺼진 사운드도 재생)"), GUILayout.Width(28)))
                        {
                            serialized.ApplyModifiedProperties();
                            SoundPreviewPlayer.Play(catalog, data);
                        }
                    if (GUILayout.Button("■", GUILayout.Width(28))) SoundPreviewPlayer.Stop();
                    if (Application.isPlaying && GUILayout.Button(new GUIContent("게임", "플레이 중인 게임의 SoundManager로 재생(꺼짐·재사용 간격 적용)"), GUILayout.Width(40)))
                    {
                        serialized.ApplyModifiedProperties();
                        SoundManager.Play(id.stringValue);
                    }
                }
                ProgressBar(id.stringValue);
                if (!open) return false;

                EditorGUILayout.Space(2);
                EditorGUILayout.PropertyField(id, new GUIContent("고유 ID"));
                EditorGUILayout.PropertyField(situation, new GUIContent("나오는 상황"));
                EditorGUILayout.PropertyField(cue.FindPropertyRelative(nameof(SoundCue.description)), new GUIContent("설명"));
                EditorGUILayout.PropertyField(cue.FindPropertyRelative(nameof(SoundCue.clips)), new GUIContent("클립 (여러 개면 무작위)"), true);
                EditorGUILayout.PropertyField(cue.FindPropertyRelative(nameof(SoundCue.category)), new GUIContent("분류"));
                EditorGUILayout.PropertyField(cue.FindPropertyRelative(nameof(SoundCue.volume)), new GUIContent("음량"));
                DrawPitch(cue);
                DrawRange(cue, data);
                EditorGUILayout.PropertyField(cue.FindPropertyRelative(nameof(SoundCue.loop)), new GUIContent("루프", "켜면 재생 구간을 멈출 때까지 반복. 코드가 핸들로 Stop 해야 함."));
                EditorGUILayout.PropertyField(cue.FindPropertyRelative(nameof(SoundCue.fadeOut)), new GUIContent("끝 페이드 (초)"));
                EditorGUILayout.PropertyField(cue.FindPropertyRelative(nameof(SoundCue.delay)), new GUIContent("지연 (초)"));
                EditorGUILayout.PropertyField(cue.FindPropertyRelative(nameof(SoundCue.cooldown)), new GUIContent("재사용 간격 (초)"));
                EditorGUILayout.PropertyField(cue.FindPropertyRelative(nameof(SoundCue.output)), new GUIContent("믹서 그룹"));

                var users = catalog.situations.Where(s => s != null && s.cueId == id.stringValue).ToArray();
                EditorGUILayout.LabelField("쓰는 상황", users.Length == 0 ? "없음 (적용 상황 탭에서 연결)" : string.Join(", ", users.Select(s => s.label)), EditorStyles.wordWrappedMiniLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("삭제", GUILayout.Width(60))
                        && EditorUtility.DisplayDialog("사운드 삭제",
                            $"'{id.stringValue}' 사운드를 삭제할까요?" + (users.Length > 0 ? $"\n이 사운드를 쓰는 상황 {users.Length}개는 소리가 나지 않게 됩니다." : ""), "삭제", "취소"))
                        delete = true;
                }
            }
            return delete;
        }

        void DrawCueBadges(string id, SoundCue data)
        {
            var style = EditorStyles.miniLabel;
            if (data.loop) GUILayout.Label(new GUIContent("루프"), style);
            if (!data.HasClip) GUILayout.Label(new GUIContent("클립 없음", "재생 요청이 와도 소리가 나지 않습니다."), style);
            int users = catalog.situations.Count(s => s != null && s.cueId == id);
            GUILayout.Label(new GUIContent(users > 0 ? $"상황 {users}개" : "상황 없음", "이 사운드를 내는 적용 상황 수."), style);
            if (!string.IsNullOrEmpty(id) && catalog.cues.Count(c => c != null && c.id == id) > 1)
                GUILayout.Label(new GUIContent("ID 중복", "같은 ID가 여러 개면 첫 항목만 재생됩니다."), style);
        }

        static void DrawPitch(SerializedProperty cue)
        {
            var min = cue.FindPropertyRelative(nameof(SoundCue.pitchMin));
            var max = cue.FindPropertyRelative(nameof(SoundCue.pitchMax));
            float a = min.floatValue, b = max.floatValue;
            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(new GUIContent("피치 (최소~최대)", "재생마다 이 범위에서 무작위."));
                a = EditorGUILayout.FloatField(a, GUILayout.Width(48));
                EditorGUILayout.MinMaxSlider(ref a, ref b, 0.1f, 3f);
                b = EditorGUILayout.FloatField(b, GUILayout.Width(48));
            }
            if (!EditorGUI.EndChangeCheck()) return;
            min.floatValue = Mathf.Clamp(Mathf.Min(a, b), 0.1f, 3f);
            max.floatValue = Mathf.Clamp(Mathf.Max(a, b), 0.1f, 3f);
        }

        /// <summary>첫 클립 길이를 기준으로 재생 구간(시작·끝)을 슬라이더로 고른다.</summary>
        static void DrawRange(SerializedProperty cue, SoundCue data)
        {
            var start = cue.FindPropertyRelative(nameof(SoundCue.startOffset));
            var duration = cue.FindPropertyRelative(nameof(SoundCue.maxDuration));
            var clip = data.FirstClip;
            if (clip == null)
            {
                EditorGUILayout.PropertyField(start, new GUIContent("시작 위치 (초)"));
                EditorGUILayout.PropertyField(duration, new GUIContent("재생 길이 (초, 0=끝까지)"));
                return;
            }
            float length = clip.length;
            float from = Mathf.Clamp(start.floatValue, 0f, length);
            float to = duration.floatValue > 0f ? Mathf.Min(length, from + duration.floatValue) : length;
            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(new GUIContent("재생 구간 (초)", $"첫 클립 '{clip.name}' 길이 {length:0.00}초 기준. 끝을 클립 끝에 두면 끝까지 재생."));
                from = EditorGUILayout.FloatField(from, GUILayout.Width(48));
                EditorGUILayout.MinMaxSlider(ref from, ref to, 0f, length);
                to = EditorGUILayout.FloatField(to, GUILayout.Width(48));
            }
            if (EditorGUI.EndChangeCheck())
            {
                from = Mathf.Clamp(from, 0f, length);
                to = Mathf.Clamp(to, from, length);
                start.floatValue = from;
                duration.floatValue = length - to < 0.005f ? 0f : to - from;
            }
            EditorGUILayout.LabelField(" ", $"클립 길이 {length:0.00}초 · 재생 {data.PlayLength(clip, 1f):0.00}초 (피치 1 기준)", EditorStyles.miniLabel);
        }

        void AddCue(SerializedProperty cues, string id = null)
        {
            cues.arraySize++;
            var cue = cues.GetArrayElementAtIndex(cues.arraySize - 1);
            var preset = SoundCueIds.Defaults.FirstOrDefault(d => d.id == id);
            cue.FindPropertyRelative(nameof(SoundCue.id)).stringValue = id ?? "NEW-" + cues.arraySize;
            cue.FindPropertyRelative(nameof(SoundCue.situation)).stringValue = preset.situation ?? "";
            cue.FindPropertyRelative(nameof(SoundCue.description)).stringValue = preset.description ?? "";
            cue.FindPropertyRelative(nameof(SoundCue.enabled)).boolValue = true;
            cue.FindPropertyRelative(nameof(SoundCue.clips)).arraySize = 0;
            cue.FindPropertyRelative(nameof(SoundCue.category)).enumValueIndex = (int)SoundCategory.Ui;
            cue.FindPropertyRelative(nameof(SoundCue.volume)).floatValue = 1f;
            cue.FindPropertyRelative(nameof(SoundCue.pitchMin)).floatValue = 1f;
            cue.FindPropertyRelative(nameof(SoundCue.pitchMax)).floatValue = 1f;
            cue.FindPropertyRelative(nameof(SoundCue.startOffset)).floatValue = 0f;
            cue.FindPropertyRelative(nameof(SoundCue.maxDuration)).floatValue = 0f;
            cue.FindPropertyRelative(nameof(SoundCue.loop)).boolValue = false;
            cue.FindPropertyRelative(nameof(SoundCue.fadeOut)).floatValue = 0.05f;
            cue.FindPropertyRelative(nameof(SoundCue.delay)).floatValue = 0f;
            cue.FindPropertyRelative(nameof(SoundCue.cooldown)).floatValue = 0f;
            cue.FindPropertyRelative(nameof(SoundCue.output)).objectReferenceValue = null;
            expanded.Add("cue:" + cue.FindPropertyRelative(nameof(SoundCue.id)).stringValue);
        }

        /// <summary>Resources 경로에 카탈로그를 만들고 기본 사운드·적용 상황을 넣는다.</summary>
        public static SoundCatalog CreateCatalog()
        {
            var existing = AssetDatabase.LoadAssetAtPath<SoundCatalog>(SoundCatalog.AssetPath);
            if (existing != null) return existing;
            string folder = Path.GetDirectoryName(SoundCatalog.AssetPath).Replace('\\', '/');
            EnsureFolder(folder);
            var created = CreateInstance<SoundCatalog>();
            foreach (var (id, situation, description) in SoundCueIds.Defaults)
                created.cues.Add(new SoundCue { id = id, situation = situation, description = description });
            AddMissingSituations(created);
            AssetDatabase.CreateAsset(created, SoundCatalog.AssetPath);
            AssetDatabase.SaveAssets();
            return created;
        }

        /// <summary>코드에 정의된 기본 상황 중 카탈로그에 없는 것을 추가한다.</summary>
        public static int AddMissingSituations(SoundCatalog target)
        {
            int added = 0;
            foreach (var (key, label, description, cueId) in SoundSituations.Defaults)
            {
                if (target.FindSituation(key) != null) continue;
                target.situations.Add(new SoundSituation { key = key, label = label, description = description, cueId = cueId });
                added++;
            }
            return added;
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }

    [CustomEditor(typeof(SoundCatalog))]
    sealed class SoundCatalogEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("사운드 설정 창 열기", GUILayout.Height(28))) SoundSettingsWindow.Open((SoundCatalog)target);
            EditorGUILayout.Space(4);
            DrawDefaultInspector();
        }
    }
}
