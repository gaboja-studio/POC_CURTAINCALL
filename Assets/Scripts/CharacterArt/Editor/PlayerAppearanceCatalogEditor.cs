using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace CurtainCall.CharacterArt.Editor
{
    [CustomEditor(typeof(PlayerAppearanceCatalog))]
    public sealed class PlayerAppearanceCatalogEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new HelpBox("플레이어 1~4의 시작 외형입니다. 변경값은 다음 참가/생성부터 적용됩니다. 개인 외형은 로비의 모델/의상 변경으로 선택합니다.", HelpBoxMessageType.Info));
            foreach (var name in new[] { "models", "outfits", "outfitColors", "outfitPatterns" })
                root.Add(new PropertyField(serializedObject.FindProperty(name)));
            var slots = serializedObject.FindProperty("defaults");
            for (int i = 0; i < slots.arraySize; i++)
            {
                var box = new VisualElement();
                box.style.marginTop = 8;
                box.Add(new Label($"플레이어 {i + 1}"));
                var slot = slots.GetArrayElementAtIndex(i);
                AddChoice(box, "모델", slot.FindPropertyRelative("model"), true);
                AddChoice(box, "의상", slot.FindPropertyRelative("outfit"), false);
                root.Add(box);
            }
            return root;
        }

        void AddChoice(VisualElement root, string label, SerializedProperty property, bool models)
        {
            var catalog = (PlayerAppearanceCatalog)target;
            List<int> Choices() => Enumerable.Range(0, models ? catalog.models.Length : System.Math.Min(4, catalog.outfits.Length)).ToList();
            string Name(int index) => models
                ? (index >= 0 && index < catalog.models.Length ? catalog.models[index].name : $"잘못된 모델 ({index})")
                : (index >= 0 && index < catalog.outfits.Length ? catalog.outfits[index] : $"잘못된 의상 ({index})");
            var choices = Choices();
            if (choices.Count == 0)
            {
                root.Add(new HelpBox($"{label} 목록을 먼저 등록하세요.", HelpBoxMessageType.Warning));
                return;
            }
            var field = new PopupField<int>(label, choices, 0, Name, Name);
            field.SetValueWithoutNotify(property.intValue);
            field.RegisterValueChangedCallback(evt =>
            {
                serializedObject.Update();
                property.intValue = evt.newValue;
                serializedObject.ApplyModifiedProperties();
            });
            field.TrackSerializedObjectValue(serializedObject, _ =>
            {
                field.choices = Choices();
                field.SetValueWithoutNotify(property.intValue);
            });
            root.Add(field);
        }
    }
}
