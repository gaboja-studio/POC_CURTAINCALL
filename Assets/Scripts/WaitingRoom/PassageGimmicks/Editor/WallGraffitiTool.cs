using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace CurtainCall.WaitingRoom.PassageGimmicks
{
    /// <summary>
    /// 벽에 적힌 한글 글귀(2026-11-12 사용자: 낙서는 글귀로 된 에셋이 필요). 그림 파일이 아니라 글자 그대로 적는 방식이라
    /// 프리팹의 Text 칸만 바꾸면 어떤 문장이든 벽 글씨가 된다.
    /// - 글꼴 2종(붓으로 휘갈긴 글씨, 닳은 페인트 글씨): 프로젝트의 배민 글꼴에서 만든 TMP 글꼴 에셋. 쓰는 글자만 그때그때 담는다.
    /// - 프리팹 WallGraffiti: 벽면에서 1cm 띄워 붙이는 3D 글씨(조명 영향 없이 어둡게 보이도록 색으로 조절).
    /// </summary>
    public static class WallGraffitiTool
    {
        const string FontFolder = "Assets/Resources/Fonts/Graffiti/";
        public const string PrefabPath = "Assets/Resources/Prefabs/Objects/WaitingRoom/WallGraffiti.prefab";
        static readonly (string name, string source)[] Fonts =
        {
            ("Graffiti_Brush", "Assets/_ThirdParty/BM_Fonts/BMKIRANGHAERANG-TTF.ttf"),
            ("Graffiti_Worn", "Assets/_ThirdParty/BM_Fonts/BMEuljirooraeorae.ttf"),
        };
        // 마른 붉은 페인트 색.
        static readonly Color Paint = new(.66f, .1f, .08f, .95f); // 어두운 방에서도 읽히게 조금 밝게

        [MenuItem("CurtainCall/Waiting Room/Graffiti/Create Korean Wall Graffiti Assets")]
        static void Menu() => Debug.Log(CreateAssets());

        public static string CreateAssets()
        {
            Directory.CreateDirectory(FontFolder);
            TMP_FontAsset first = null; int made = 0;
            foreach (var (name, source) in Fonts)
            {
                string path = FontFolder + name + ".asset";
                var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (asset == null)
                {
                    var font = AssetDatabase.LoadAssetAtPath<Font>(source);
                    if (font == null) return "글꼴 파일을 찾지 못했습니다: " + source;
                    asset = TMP_FontAsset.CreateFontAsset(font, 96, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
                    asset.name = name;
                    AssetDatabase.CreateAsset(asset, path);
                    asset.atlasTextures[0].name = name + " Atlas"; AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
                    asset.material.name = name + " Material"; AssetDatabase.AddObjectToAsset(asset.material, asset);
                    EditorUtility.SetDirty(asset); made++;
                }
                first ??= asset;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                var go = new GameObject("WallGraffiti");
                Configure(go.AddComponent<TextMeshPro>(), first, "글귀를 입력하세요", .3f);
                PrefabUtility.SaveAsPrefabAsset(go, PrefabPath); Object.DestroyImmediate(go); made++;
            }
            AssetDatabase.SaveAssets();
            return $"벽 글씨 에셋 준비(새로 만든 것 {made}개): 글꼴 {FontFolder}, 프리팹 {PrefabPath}";
        }

        /// <summary>벽에 글귀를 붙인다. position은 벽면 위 글씨 중심, yaw는 글씨가 보는 방향(0 = 남쪽, 90 = 서쪽, 180 = 북쪽, 270 = 동쪽).</summary>
        public static TextMeshPro Stamp(Transform parent, string text, Vector3 position, float yaw, float letterHeight = .3f, bool worn = false, float tilt = 0)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) { CreateAssets(); prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath); }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            var tmp = go.GetComponent<TextMeshPro>();
            Configure(tmp, AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontFolder + Fonts[worn ? 1 : 0].name + ".asset"), text, letterHeight);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, tilt));
            tmp.ForceMeshUpdate();
            EditorUtility.SetDirty(tmp.font);
            return tmp;
        }

        static void Configure(TextMeshPro tmp, TMP_FontAsset font, string text, float letterHeight)
        {
            tmp.font = font; tmp.text = text;
            tmp.fontSize = letterHeight * 10f; // 3D 글씨는 글자 크기 10이 약 1m.
            tmp.color = Paint; tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.rectTransform.sizeDelta = new Vector2(letterHeight * 12f, letterHeight * 5f);
            tmp.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
