using System;
using System.Reflection;
using CurtainCall.Reward;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.Settings.EditorTools
{
    internal abstract class KoreanRewardSettingsEditor : Editor
    {
        internal static GUIContent LabelFor(SerializedProperty property, Type settingsType)
        {
            var field = settingsType.GetField(property.name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var label = field?.GetCustomAttribute<InspectorNameAttribute>();
            return new GUIContent(label?.displayName ?? property.displayName, property.tooltip);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                bool script = property.name == "m_Script";
                using (new EditorGUI.DisabledScope(script))
                    EditorGUILayout.PropertyField(property, LabelFor(property, target.GetType()), true);
            }
            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(AudienceReactionSettings)), CanEditMultipleObjects]
    internal sealed class AudienceReactionSettingsEditor : KoreanRewardSettingsEditor { }

    [CustomEditor(typeof(TemporaryRewardTable)), CanEditMultipleObjects]
    internal sealed class TemporaryRewardTableEditor : KoreanRewardSettingsEditor { }
}
