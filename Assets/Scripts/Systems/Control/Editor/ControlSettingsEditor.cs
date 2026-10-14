using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.Player.EditorTools
{
    /// <summary>
    /// 조작 설정 인스펙터. 프로젝트 안의 조작 규칙 목록에서 기본 이동 규칙을 고른다.
    /// 목록: 규칙 에셋 전부 + 에셋이 아직 없는 규칙 종류("새로 만들기", 고르면 <see cref="SchemeFolder"/>에 에셋을 만든다).
    /// 각 공간이 자기 폴더에 만든 규칙도 자동으로 나온다. 플레이 중 바꾸면 바로 반영된다.
    /// </summary>
    [CustomEditor(typeof(ControlSettings))]
    sealed class ControlSettingsEditor : Editor
    {
        const string SettingsFolder = "Assets/Resources/Settings/Control";
        const string SettingsPath = SettingsFolder + "/ControlSettings.asset";
        const string SchemeFolder = SettingsFolder + "/Schemes";

        [MenuItem("Tools/CurtainCall/조작/기본 이동 규칙 선택")]
        static void SelectSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<ControlSettings>(SettingsPath);
            if (settings == null)
            {
                settings = CreateInstance<ControlSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[ControlSettings] 조작 설정을 만들었습니다: {SettingsPath}");
            }
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var property = serializedObject.FindProperty("defaultScheme");

            EditorGUILayout.HelpBox(
                "조작 규칙을 끼우지 않은 공간에서 쓰는 기본 이동 규칙입니다. 비우면 플랫폼 이동(8방향 이동·점프).\n" +
                "공간 규칙은 각 공간 폴더에 PlayerControlScheme을 상속해 만들면 아래 목록에 자동으로 나옵니다.",
                MessageType.Info);

            var options = BuildOptions(property.objectReferenceValue as PlayerControlScheme, out int selected);
            int picked = EditorGUILayout.Popup("기본 이동 규칙", selected, options.Select(o => o.Label).ToArray());
            if (picked != selected)
            {
                var option = options[picked];
                property.objectReferenceValue = option.Asset != null ? option.Asset
                    : option.Type != null ? CreateSchemeAsset(option.Type) : null;
            }

            EditorGUILayout.PropertyField(property, new GUIContent("규칙 에셋"));
            if (Application.isPlaying)
                EditorGUILayout.HelpBox("플레이 중: 규칙을 끼우지 않은 플레이어에게 바로 반영됩니다.", MessageType.None);

            serializedObject.ApplyModifiedProperties();
        }

        struct Option
        {
            public string Label;
            public PlayerControlScheme Asset;
            public Type Type;
        }

        static List<Option> BuildOptions(PlayerControlScheme current, out int selected)
        {
            var options = new List<Option> { new() { Label = "(비움) 플랫폼 이동 — 기본" } };
            var typesWithAsset = new HashSet<Type>();
            foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(PlayerControlScheme)}"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<PlayerControlScheme>(path);
                if (asset == null) continue;
                typesWithAsset.Add(asset.GetType());
                options.Add(new Option { Label = $"{asset.name}  ({Path.GetDirectoryName(path)?.Replace('\\', '/')})", Asset = asset });
            }
            foreach (var type in TypeCache.GetTypesDerivedFrom<PlayerControlScheme>()
                         .Where(t => !t.IsAbstract && !typesWithAsset.Contains(t))
                         .OrderBy(t => t.Name))
                options.Add(new Option { Label = $"새로 만들기: {type.Name}", Type = type });

            selected = current == null ? 0 : options.FindIndex(o => o.Asset == current);
            if (selected < 0)
            {
                options.Add(new Option { Label = current.name, Asset = current }); // 목록 밖(실행 중 만든 규칙 등)
                selected = options.Count - 1;
            }
            return options;
        }

        static PlayerControlScheme CreateSchemeAsset(Type type)
        {
            if (!AssetDatabase.IsValidFolder(SchemeFolder))
                AssetDatabase.CreateFolder(SettingsFolder, "Schemes");
            var scheme = (PlayerControlScheme)CreateInstance(type);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{SchemeFolder}/{type.Name}.asset");
            AssetDatabase.CreateAsset(scheme, path);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ControlSettings] 조작 규칙 에셋을 만들었습니다: {path}");
            return scheme;
        }
    }
}
