using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.Fonts.EditorTools
{
    /// <summary>
    /// 한글 TMP 폰트를 Static으로 굽는다. Dynamic 폰트는 Play 중 화면에 나온 글자를 아틀라스에 더하며 .asset을 바꿔
    /// 아무것도 안 고쳐도 git에 잡힌다(함정 tmp-dynamic-font-churn). 코드(Assets/Scripts)에 나오는 글자 + ASCII + 기호를 미리 굽는다.
    /// 화면에 새 글자를 쓰는 코드를 추가했는데 Console에 "character ... was not found" 경고가 나오면 이 메뉴를 다시 실행한다.
    /// </summary>
    public static class StaticFontBaker
    {
        public static readonly string[] Fonts =
        {
            "Assets/Resources/Fonts/Pretendard-Medium/Pretendard-Medium SDF.asset",
            "Assets/Scenes/Tests/Systems/DummySpaces/Display/dummy-korean.asset",
        };

        const string Symbols = "→←↑↓·▶◀▲▼■□●○◆◇★☆✕✓※…~「」『』《》〈〉【】“”‘’";

        [MenuItem("Tools/CurtainCall/Bake Static Fonts")]
        public static void BakeAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Play를 종료하세요.");
            string characters = CodeCharacters();
            foreach (string path in Fonts)
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font == null) { Debug.LogWarning($"[Fonts] 폰트 없음: {path}"); continue; }
                Bake(font, characters);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>코드 전체에 나오는 글자 + ASCII 출력 문자 + 기호(중복 제거).</summary>
        public static string CodeCharacters()
        {
            string code = string.Concat(Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
            string ascii = new string(Enumerable.Range(32, 95).Select(c => (char)c).ToArray());
            return new string((ascii + Symbols + code).Distinct().Where(c => !char.IsControl(c) && !char.IsSurrogate(c)).OrderBy(c => c).ToArray());
        }

        /// <summary>아틀라스를 비우고 주어진 글자만 다시 채운 뒤 Static으로 고정한다(GUID·재질 참조는 유지).</summary>
        public static void Bake(TMP_FontAsset font, string characters)
        {
            var serialized = new SerializedObject(font);
            serialized.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            font.ClearFontAssetData(false);
            font.TryAddCharacters(characters, out string missing);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssetIfDirty(font);
            string skipped = string.IsNullOrEmpty(missing) ? "" : $", 원본 폰트에 없는 글자 {missing.Length}개";
            Debug.Log($"[Fonts] {font.name}: Static {font.characterTable.Count}자, 아틀라스 {font.atlasTextures.Length}장{skipped}");
        }
    }
}
