using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

namespace CurtainCall.HumanCannon.EditorTools
{
    public static class HumanCannonSectionFontBuilder
    {
        const string Path = "Assets/Scenes/Tests/MainStage/HumanCannon/Labels/section-korean.asset";
        const string Characters = "현재 구간 · 출발1쉼터동료합류2반동과회피최종도착0123456789Space봉 놓기W/S A/D 앞뒤 이동옆걸음마우스방향전환F길게눌러F로 발사 · F 길게 눌러 내리기";

        public static TMP_FontAsset LoadFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Path);
            if (font != null)
            {
                if (!font.HasCharacters(Characters))
                    CurtainCall.Fonts.EditorTools.StaticFontBaker.Bake(font, Characters);
                if (!font.HasCharacters(Characters))
                    throw new InvalidOperationException("구간·봉·트램폴린·대포 안내 글꼴에 필요한 문자를 굽지 못했습니다.");
                return font;
            }
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/Pretendard-Medium/Pretendard-Medium 1.otf");
            if (source == null) throw new InvalidOperationException("구간 표시 한글 원본 폰트가 없습니다.");
            font = TMP_FontAsset.CreateFontAsset(source, 48, 5, GlyphRenderMode.SDFAA, 512, 512, AtlasPopulationMode.Dynamic);
            font.name = "Human Cannon Section Korean";
            if (!font.TryAddCharacters(Characters, out string missing))
                throw new InvalidOperationException("구간 글꼴에 없는 문자: " + missing);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(font, Path);
            font.material.name = "Human Cannon Section Material";
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var texture in font.atlasTextures)
            {
                texture.name = "Human Cannon Section Atlas";
                AssetDatabase.AddObjectToAsset(texture, font);
            }
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssetIfDirty(font);
            return font;
        }

        public static void RefreshOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty || scene.path != HumanCannonTestSceneBuilder.ScenePath)
                throw new InvalidOperationException("저장된 HumanCannon 씬에서 Play 종료 후 실행하세요.");
            var root = GameObject.Find("HumanCannonEnhancement");
            if (root == null) throw new InvalidOperationException("고도화 배치를 먼저 적용하세요.");
            var font = LoadFont();
            Undo.RegisterFullObjectHierarchyUndo(root, "구간 전용 한글 폰트 연결");
            foreach (var label in root.GetComponentsInChildren<TMP_Text>(true)) label.font = font;
            using var data = new SerializedObject(root.GetComponent<HumanCannonCourseSections>());
            data.FindProperty("font").objectReferenceValue = font;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("구간 폰트 씬 저장 실패");
        }
    }
}
