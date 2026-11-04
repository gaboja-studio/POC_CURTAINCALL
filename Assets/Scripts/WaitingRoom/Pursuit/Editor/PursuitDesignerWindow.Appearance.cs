using UnityEditor;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public sealed partial class PursuitDesignerWindow
    {
        static readonly string[] AppearanceLabels = {
            "개체별 다양화", "프리팹 기본", "깨끗함", "때 / 마모", "옅은 혈흔", "중간 혈흔", "짙은 혈흔"
        };
        static string SurfaceLabel(PursuitSurfaceState state) => state switch {
            PursuitSurfaceState.Clean => "깨끗함", PursuitSurfaceState.Worn => "때 / 마모",
            PursuitSurfaceState.LightBlood => "옅은 혈흔", PursuitSurfaceState.HeavyBlood => "짙은 혈흔", _ => "중간 혈흔"
        };
        void ApplyAppearanceChanges(SerializedObject so)
        {
            if (!so.ApplyModifiedProperties()) return;
            RefreshSelectedAppearance(); SceneView.RepaintAll();
        }
        void DrawAppearance(SerializedObject placementSO)
        {
            GUILayout.Label("외관 레이어", EditorStyles.boldLabel);
            var appearance = placementSO.FindProperty("Appearance");
            EditorGUI.BeginChangeCheck();
            int choice = EditorGUILayout.Popup("기본 조합", appearance.enumValueIndex, AppearanceLabels);
            if (EditorGUI.EndChangeCheck()) {
                appearance.enumValueIndex = choice;
                placementSO.FindProperty("CustomSurfaceLayers").boolValue = false;
                ApplyAppearanceChanges(placementSO);
            }
            if (selected.CustomSurfaceLayers) {
                GUILayout.Label("레이어 직접 조절 중", EditorStyles.miniBoldLabel);
                if (GUILayout.Button("기본 조합으로 되돌리기")) {
                    placementSO.FindProperty("CustomSurfaceLayers").boolValue = false;
                    ApplyAppearanceChanges(placementSO);
                }
            } else if (selected.Appearance == PursuitAppearanceChoice.Varied)
                EditorGUILayout.LabelField("이 마네킹의 기본 조합", SurfaceLabel(selected.ResolvedSurface));

            GUILayout.Space(6); GUILayout.Label("1 · 피부 / 봉제", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("마네킹 피부 + 관절 실밥 + 꿰맨 천 조각", EditorStyles.wordWrappedMiniLabel);
            EditorGUI.BeginChangeCheck();
            GUILayout.Space(6); GUILayout.Label("2 · 때 / 마모", EditorStyles.boldLabel);
            float dirt = EditorGUILayout.Slider("때 정도", selected.ResolvedDirtAmount, 0, 1);
            float wear = EditorGUILayout.Slider("마모 정도", selected.ResolvedWearAmount, 0, 1);
            int wearPattern = EditorGUILayout.IntSlider("때·마모 무늬", selected.ResolvedWearPattern, 0, 15);
            GUILayout.Space(6); GUILayout.Label("3 · 혈흔", EditorStyles.boldLabel);
            float blood = EditorGUILayout.Slider("혈흔 정도", selected.ResolvedBloodAmount, 0, 1);
            int bloodPattern = EditorGUILayout.IntSlider("혈흔 무늬", selected.AppearancePattern, 0, 15);
            if (EditorGUI.EndChangeCheck()) {
                placementSO.FindProperty("CustomSurfaceLayers").boolValue = true;
                placementSO.FindProperty("DirtAmount").floatValue = dirt;
                placementSO.FindProperty("WearAmount").floatValue = wear;
                placementSO.FindProperty("BloodAmount").floatValue = blood;
                placementSO.FindProperty("WearPattern").intValue = wearPattern;
                placementSO.FindProperty("AppearancePattern").intValue = bloodPattern;
                ApplyAppearanceChanges(placementSO);
            }
            EditorGUILayout.HelpBox("때·마모와 피를 함께 조절할 수 있습니다. 게임의 피 표시를 끄면 혈흔만 숨겨지고, 피부·봉제·때·마모는 유지됩니다.", MessageType.None);
            using (new EditorGUI.DisabledScope(!previews.ContainsKey(selected)))
                if (GUILayout.Button("선택 외관 가까이 보기")) FrameInScene(selected.gameObject, previews[selected]);
            GUILayout.Space(10);
        }
        void RefreshSelectedAppearance()
        {
            if (selected != null && previews.TryGetValue(selected, out var visual) && visual != null)
                selected.ApplyAppearance(visual.GetComponent<PursuitAppearance>());
        }
    }
}
