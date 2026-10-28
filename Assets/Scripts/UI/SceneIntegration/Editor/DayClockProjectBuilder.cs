using System;
using System.IO;
using System.Linq;
using CurtainCall.UI.DayClockV2.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace CurtainCall.UI.SceneIntegration.EditorTools
{
    /// <summary>패키지 조립기를 재사용하며 프로젝트 전용 폰트와 런 연결 컴포넌트를 저장한다.</summary>
    public static class DayClockProjectBuilder
    {
        public const string Output = "Assets/Resources/Prefabs/UIs/DayClock";
        public const string Prefab = Output + "/W1_DayClock_v2_7.prefab";
        const string FontRoot = "Assets/Resources/Fonts/DayClock/V24";
        const string Kit = "Assets/_ThirdParty/curtaincall_dayclock_v2";
        const string Characters = "0123456789AM P:—-+ /분일차오전후수면확인중공연휴식일시정지제한없음";
        const string Schedule = Output + "/W1_DayClock_v2_2_Schedule.asset";

        [MenuItem("Tools/Curtain Call/하루 시계/실게임 프리팹 생성")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("임포트·컴파일을 마친 편집 모드에서 실행하세요.");
            if (File.Exists(Prefab) || File.Exists(Prefab + ".meta"))
                throw new InvalidOperationException("기존 프리팹은 덮어쓰지 않습니다: " + Prefab);
            var profile = AssetDatabase.LoadAssetAtPath<CurtainCall.UI.DayClockV2.DayClockScheduleProfile>(Schedule);
            if (profile == null) throw new InvalidOperationException("기존 실게임 일정 프로필 누락: " + Schedule);
            var medium = MakeFont("Medium");
            var bold = MakeFont("Bold");
            var semiBold = MakeFont("SemiBold");
            DayClockPrefabBuilder.BuildProject(Output, medium, bold, semiBold, profile);
            var root = PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                root.AddComponent<DayClockView>();
                var rect = root.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                // C1 원 중심 (144,-156)에 v2.7 DialRoot의 중심 (112,-108)을 맞춘다.
                rect.anchoredPosition = new Vector2(32, -48);
                if (PrefabUtility.SaveAsPrefabAsset(root, Prefab) == null)
                    throw new IOException("하루 시계 프리팹 저장 실패");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static TMP_FontAsset MakeFont(string weight)
        {
            string path = FontRoot + "/Pretendard-" + weight + " SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null)
            {
                if (!existing.HasCharacters(Characters))
                {
                    var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(Kit + "/Source/Fonts/Pretendard-" + weight + ".ttf");
                    if (sourceFont == null) throw new InvalidOperationException("폰트 원본 누락: " + weight);
                    try
                    {
                        existing.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                        var serialized = new SerializedObject(existing);
                        serialized.FindProperty("m_SourceFontFile").objectReferenceValue = sourceFont;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        if (!existing.TryAddCharacters(new string(Characters.Distinct().ToArray()), out string missingCharacters))
                            throw new InvalidOperationException("글리프 추가 실패: " + missingCharacters);
                    }
                    finally { existing.atlasPopulationMode = AtlasPopulationMode.Static; }
                    EditorUtility.SetDirty(existing);
                    AssetDatabase.SaveAssetIfDirty(existing);
                }
                return existing;
            }
            if (File.Exists(path) || File.Exists(path + ".meta")) throw new InvalidOperationException("전용 폰트 경로 충돌: " + path);
            var source = AssetDatabase.LoadAssetAtPath<Font>(Kit + "/Source/Fonts/Pretendard-" + weight + ".ttf");
            if (source == null) throw new InvalidOperationException("폰트 원본 누락: " + weight);
            EnsureFolder(FontRoot);
            var font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            font.name = "Pretendard-" + weight + " DayClock SDF";
            if (!font.TryAddCharacters(new string(Characters.Distinct().ToArray()), out string missing))
                throw new InvalidOperationException("글리프 생성 실패: " + missing);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(font, path);
            font.material.name = font.name + " Material";
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var atlas in font.atlasTextures.Where(x => x != null))
            { atlas.name = font.name + " Atlas"; AssetDatabase.AddObjectToAsset(atlas, font); }
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssetIfDirty(font);
            return font;
        }

        static void EnsureFolder(string path)
        {
            string current = "Assets";
            foreach (string part in path.Split('/').Skip(1))
            {
                string next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, part);
                current = next;
            }
        }
    }
}
