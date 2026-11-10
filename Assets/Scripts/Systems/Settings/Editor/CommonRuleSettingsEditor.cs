using UnityEditor;
using UnityEngine;

namespace CurtainCall.Settings.EditorTools
{
    // 기존 보상 설정과 같은 한글 라벨·Tooltip 표시 경로를 사용한다.
    [CustomEditor(typeof(PoleSettings)), CanEditMultipleObjects]
    internal sealed class PoleSettingsEditor : KoreanRewardSettingsEditor { }

    [CustomEditor(typeof(TrampolineSettings)), CanEditMultipleObjects]
    internal sealed class TrampolineSettingsEditor : KoreanRewardSettingsEditor { }

    [CustomPropertyDrawer(typeof(BaseGameSettings.InteractionSettings))]
    [CustomPropertyDrawer(typeof(BaseGameSettings.BodyDamageSettings))]
    [CustomPropertyDrawer(typeof(TightropeSettings.RopeMovementSettings))]
    [CustomPropertyDrawer(typeof(WaitingRoomSettings.TutorialSettings))]
    internal sealed class CommonRuleSettingsDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (property.isExpanded)
                VisitChildren(property, child => height += EditorGUIUtility.standardVerticalSpacing + EditorGUI.GetPropertyHeight(child, true));
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label, true);
            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                VisitChildren(property, child =>
                {
                    line.y += line.height + EditorGUIUtility.standardVerticalSpacing;
                    line.height = EditorGUI.GetPropertyHeight(child, true);
                    EditorGUI.PropertyField(line, child, KoreanRewardSettingsEditor.LabelFor(child, fieldInfo.FieldType), true);
                });
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }

        static void VisitChildren(SerializedProperty parent, System.Action<SerializedProperty> visit)
        {
            var child = parent.Copy();
            var end = child.GetEndProperty();
            if (!child.NextVisible(true)) return;
            while (!SerializedProperty.EqualContents(child, end))
            {
                visit(child);
                if (!child.NextVisible(false)) break;
            }
        }
    }
}
