using UnityEditor;
using UnityEngine;

namespace CurtainCall.StartLobby.EditorTools
{
    public static class StartMenuPresentationAssets
    {
        public const string SettingsPath = "Assets/Resources/Settings/StartMenu/StartMenuCredits.asset";
        const string RopePath = "Assets/Resources/Prefabs/UIs/StartMenu/TitleSuspensionRopes.prefab";

        [MenuItem("Tools/CurtainCall/Start Menu/Credits Settings")]
        public static void SelectSettings()
        {
            EnsureAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<StartMenuCreditsSettings>(SettingsPath);
        }

        public static void EnsureAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Stop Play before creating assets.");
            Folder("Assets/Resources/Settings/StartMenu");
            Folder("Assets/Resources/Prefabs/UIs/StartMenu");
            if (AssetDatabase.LoadAssetAtPath<StartMenuCreditsSettings>(SettingsPath) == null)
            {
                var settings = ScriptableObject.CreateInstance<StartMenuCreditsSettings>();
                settings.sourceFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/Pretendard-Medium/Pretendard-Medium 1.otf");
                settings.licenses.Add(new StartMenuCreditsSettings.AssetLicense
                {
                    asset = "Pretendard", author = "Kil Hyung-jin", license = "SIL Open Font License 1.1",
                    notice = "Copyright (c) 2021, Kil Hyung-jin. Reserved Font Name: Pretendard.",
                    url = "https://github.com/orioncactus/pretendard", fullLicense = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Fonts/Pretendard-License.txt")
                });
                settings.licenses.Add(new StartMenuCreditsSettings.AssetLicense
                {
                    asset = "Quaternius Standard Models", author = "Quaternius", license = "CC0 1.0 Universal",
                    url = "https://creativecommons.org/publicdomain/zero/1.0/",
                    fullLicense = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_ThirdParty/Task062/QuaterniusStandard/license.txt")
                });
                settings.licenses.Add(new StartMenuCreditsSettings.AssetLicense
                {
                    asset = "DOTween", author = "Daniele Giardini · Demigiant", license = "DOTween License",
                    notice = "Copyright (c) 2014–2026 Daniele Giardini - Demigiant.", url = "https://dotween.demigiant.com/license.php"
                });
                settings.licenses.Add(new StartMenuCreditsSettings.AssetLicense
                {
                    asset = "DOTween Pro", author = "Daniele Giardini · Demigiant", license = "DOTween Pro License",
                    notice = "Copyright (c) 2014–2026 Daniele Giardini - Demigiant.", url = "https://dotween.demigiant.com/license.php"
                });
                settings.licenses.Add(new StartMenuCreditsSettings.AssetLicense
                {
                    asset = "POLYGON Environment & Props", author = "Synty Studios", license = "Synty / 구매처 EULA",
                    notice = "Synty Studios의 환경 및 소품 에셋.", url = "https://syntystore.com/pages/licences-overview"
                });
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(RopePath) == null)
            {
                var rope = new GameObject("TitleSuspensionRopes", typeof(RectTransform), typeof(TitleRopeGraphic));
                rope.GetComponent<TitleRopeGraphic>().raycastTarget = false;
                ((RectTransform)rope.transform).sizeDelta = new Vector2(1020, 600);
                PrefabUtility.SaveAsPrefabAsset(rope, RopePath);
                Object.DestroyImmediate(rope);
            }
            else if (AssetDatabase.LoadAssetAtPath<GameObject>(RopePath).GetComponent<CanvasRenderer>() == null)
            {
                var rope = PrefabUtility.LoadPrefabContents(RopePath);
                rope.AddComponent<CanvasRenderer>();
                PrefabUtility.SaveAsPrefabAsset(rope, RopePath);
                PrefabUtility.UnloadPrefabContents(rope);
            }
            AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<StartMenuCreditsSettings>(SettingsPath));
        }

        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            Folder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        [SettingsProvider]
        public static SettingsProvider CreditsProvider()
        {
            return new SettingsProvider("Project/Curtain Call/Development and Licenses", SettingsScope.Project)
            {
                label = "Development and Licenses",
                keywords = new System.Collections.Generic.HashSet<string> { "개발", "라이선스", "Credits", "License", "작업자" },
                guiHandler = _ =>
                {
                    var settings = AssetDatabase.LoadAssetAtPath<StartMenuCreditsSettings>(SettingsPath);
                    if (settings == null)
                    {
                        if (GUILayout.Button("Create Credits Settings")) EnsureAssets();
                        return;
                    }
                    EditorGUILayout.HelpBox("개발자와 에셋 목록의 + / − 로 항목을 추가·삭제할 수 있습니다. 순서, 이름, 역할, 라이선스, 원문 및 링크가 게임 화면에 반영됩니다.", MessageType.Info);
                    var serialized = new SerializedObject(settings);
                    serialized.Update();
                    EditorGUILayout.PropertyField(serialized.FindProperty("sourceFont"));
                    EditorGUILayout.PropertyField(serialized.FindProperty("contributors"), true);
                    EditorGUILayout.PropertyField(serialized.FindProperty("licenses"), true);
                    if (serialized.ApplyModifiedProperties()) AssetDatabase.SaveAssetIfDirty(settings);
                }
            };
        }
    }
}
