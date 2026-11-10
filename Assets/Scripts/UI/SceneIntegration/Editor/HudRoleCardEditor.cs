using TMPro;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration.Editor
{
    [CustomEditor(typeof(HudRoleCard))]
    public sealed class HudRoleCardEditor : UnityEditor.Editor
    {
        bool showLabels=true;
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if(GUILayout.Button("공통 HUD 표시 설정 · 교체 / 쌓기"))HudRolePrefabBuilder.SelectPresentation();
            var card=(HudRoleCard)target;
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("루트 RectTransform: 화면 앵커·피벗·위치. Rows의 Vertical Layout Group: 안쪽 여백·간격·정렬. 완전 수동 배치는 Rows의 Layout Group을 끄세요. Fit Width/Height를 끈 축은 프리팹 크기를 유지합니다.",MessageType.Info);
            showLabels=EditorGUILayout.Foldout(showLabels,"문구 템플릿 · TMP 원본 편집",true);
            if(!showLabels) return;
            EditorGUILayout.HelpBox("{title} 제목, {content} 본문, {condition} 조건, 버튼은 {choice}. 토큰 없는 문장은 고정입니다. 폰트·색상·정렬은 해당 TMP에서 편집하세요.",MessageType.None);
            using(new EditorGUI.DisabledScope(Application.isPlaying))
            {
                Edit("제목",card.title); Edit("본문",card.body); Edit("조건 / 힌트",card.condition);
                if(card.choices!=null)
                    for(int i=0;i<card.choices.Length;i++)
                        if(card.choices[i]) Edit("선택 "+(i+1),card.choices[i].GetComponentInChildren<TMP_Text>(true));
                using(new EditorGUI.DisabledScope(EditorUtility.IsPersistent(card) || !card.body))
                    if(GUILayout.Button("고정 텍스트 행 추가"))
                    {
                        var label=Instantiate(card.body,card.body.transform.parent,false);
                        label.name="FixedText"; label.text="고정 안내"; label.gameObject.SetActive(true);
                        Undo.RegisterCreatedObjectUndo(label.gameObject,"Add fixed HUD text");
                        Selection.activeGameObject=label.gameObject;
                    }
            }
        }

        static void Edit(string name,TMP_Text label)
        {
            if(!label) return;
            EditorGUILayout.LabelField(name);
            EditorGUI.BeginChangeCheck();
            var value=EditorGUILayout.TextArea(label.text,GUILayout.MinHeight(22));
            if(!EditorGUI.EndChangeCheck()) return;
            Undo.RecordObject(label,"Edit HUD text template");
            label.text=value;
            PrefabUtility.RecordPrefabInstancePropertyModifications(label);
            EditorUtility.SetDirty(label);
        }
    }
}
