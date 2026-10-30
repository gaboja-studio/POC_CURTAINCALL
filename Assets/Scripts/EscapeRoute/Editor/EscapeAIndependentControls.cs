using System;
using System.Linq;
using TMPro;
using CurtainCall.HumanCannon;
using CurtainCall.UI.NotificationsV1;
using CurtainCall.UI.SceneIntegration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        public static string ApplyAIndependentControls()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath || scene.isDirty)
                throw new InvalidOperationException("Saved A edit scene required");
            var locks = UnityEngine.Object.FindFirstObjectByType<EscapeCooperativeLocks>();
            var ropes = UnityEngine.Object.FindFirstObjectByType<EscapeCooperativeRopes>();
            var poles = UnityEngine.Object.FindFirstObjectByType<HumanCannonPoleSystem>();
            if (locks == null || locks.Count != 5 || ropes == null || poles == null)
                throw new InvalidOperationException("A cooperative devices required");
            var parent = GameObject.Find(RootName).transform;
            var group = parent.Find("44_AIndependentControls");
            if (group == null)
            {
                var go = new GameObject("44_AIndependentControls"); Undo.RegisterCreatedObjectUndo(go, "Independent controls");
                group = go.transform; group.SetParent(parent, false);
            }
            if (group.GetComponent<EscapeIndependentInteractions>() == null) Undo.AddComponent<EscapeIndependentInteractions>(group.gameObject);
            var poleData = new SerializedObject(poles);
            poleData.FindProperty("allowPlatformJumpGrab").boolValue = true;
            poleData.FindProperty("independentInteraction").boolValue = true; poleData.ApplyModifiedProperties();

            const string lampPath = Materials + "a-mechanism-indicator.mat";
            var lampMaterial = AssetDatabase.LoadAssetAtPath<Material>(lampPath);
            if (lampMaterial == null)
            {
                lampMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "A 장치 상태등" };
                lampMaterial.SetColor("_BaseColor", Color.red); lampMaterial.SetColor("_EmissionColor", Color.red * 2);
                lampMaterial.EnableKeyword("_EMISSION"); AssetDatabase.CreateAsset(lampMaterial, lampPath);
            }
            string[] sources = { "전원 버튼", "상승 유지봉", "반출교 레버", "권상교 그네", "통로 유지봉" };
            string[] effects = { "복귀 대포", "상승 대포", "반출교", "권상교", "합류 다리" };
            string[] hints = {
                "전원 버튼에 F → 복귀 대포 가동 · 톱을 지나 대포로 이동",
                "봉을 잡으면 상승 대포 가동 · 놓으면 정지 · 동료가 상부에 도착하면 가동 고정",
                "레버에 F → 반출교 내려옴 · 내려온 다리로 이동",
                "그네를 잡으면 권상교 내려옴 · 최대 4명 탑승 · 각자 Space로 도약",
                "봉을 잡으면 합류 다리 내려옴 · 놓으면 올라감 · 동료가 건너면 다리 고정"
            };
            Vector3[] destinations = { new(78.8f, -17.15f, -72.9f), new(121.3f, -6.65f, 104),
                new(174, 1.35f, 80.3f), new(286, 1.35f, 80.6f), new(308, 1.35f, 80.3f) };
            var data = new SerializedObject(locks); var entries = data.FindProperty("checkpoints");
            for (int i = 0; i < entries.arraySize; i++)
            {
                var e = entries.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("sourceName").stringValue = sources[i];
                e.FindPropertyRelative("effectName").stringValue = effects[i];
                e.FindPropertyRelative("instruction").stringValue = hints[i];
                e.FindPropertyRelative("prompt").stringValue = sources[i] + " 누르기";
                e.FindPropertyRelative("minimumHelpers").intValue = 1;
                var helper = e.FindPropertyRelative("helperPole").objectReferenceValue as HumanCannonPole;
                var control = locks.Control(i);
                if (control != null) { Undo.RecordObject(control.gameObject, "Independent button name"); control.name = sources[i]; }
                var indicators = e.FindPropertyRelative("linkedIndicators"); indicators.arraySize = helper != null ? 2 : 1;
                indicators.GetArrayElementAtIndex(0).objectReferenceValue = MechanismLamp(group, effects[i] + " 가동등", destinations[i], lampMaterial);
                if (helper != null) indicators.GetArrayElementAtIndex(1).objectReferenceValue =
                    MechanismLamp(group, sources[i] + " 연결등", helper.BarPosition(0) + new Vector3(0, .55f, .35f), lampMaterial);
            }
            data.ApplyModifiedProperties();

            var feedback = group.GetComponent<EscapeMechanismFeedback>() ?? Undo.AddComponent<EscapeMechanismFeedback>(group.gameObject);
            var ui = group.Find("장치 작동 알림");
            if (ui == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/Notifications/SystemNotifications.prefab");
                if (prefab == null) throw new InvalidOperationException("Existing notifications prefab required");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
                Undo.RegisterCreatedObjectUndo(instance, "Device notification UI"); instance.name = "장치 작동 알림"; ui = instance.transform;
            }
            var views = ui.GetComponent<SystemNotificationsView>(); views.ResetForRuntime();
            var label = (TMP_Text)new SerializedObject(views.Toast).FindProperty("messageLabel").objectReferenceValue;
            Undo.RecordObject(label, "A notification glyphs");
            label.font = MechanismFont(); label.fontSharedMaterial = label.font.material;
            var toastRect = (RectTransform)views.Toast.transform;
            Undo.RecordObject(toastRect, "Readable device notice width");
            toastRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 580);
            Undo.RecordObject(label.rectTransform, "Readable device notice text width");
            label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 500);
            PrefabUtility.RecordPrefabInstancePropertyModifications(label);
            PrefabUtility.RecordPrefabInstancePropertyModifications(toastRect);
            PrefabUtility.RecordPrefabInstancePropertyModifications(label.rectTransform);
            if (views.Toast.GetComponent<NotificationTextLayout>() == null) Undo.AddComponent<NotificationTextLayout>(views.Toast.gameObject);
            var fd = new SerializedObject(feedback);
            fd.FindProperty("locks").objectReferenceValue = locks; fd.FindProperty("ropes").objectReferenceValue = ropes;
            fd.FindProperty("toast").objectReferenceValue = views.Toast;
            var ri = fd.FindProperty("returnIndicators"); ri.arraySize = 2;
            ri.GetArrayElementAtIndex(0).objectReferenceValue = MechanismLamp(group, "복귀 버튼 연결등", ropes.ButtonPoint + Vector3.up * .5f, lampMaterial);
            ri.GetArrayElementAtIndex(1).objectReferenceValue = MechanismLamp(group, "복귀 계단 개방등", new Vector3(116.7f, -1.8f, 31.7f), lampMaterial);
            fd.ApplyModifiedProperties();
            Undo.FlushUndoRecordObjects(); Physics.SyncTransforms(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            return "A: all buttons independent; all 15 poles accept direct nearby F; 5 linked devices + return gate have status lamps and synchronized notifications";
        }

        static Renderer MechanismLamp(Transform parent, string name, Vector3 position, Material material)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name = name;
                Undo.RegisterCreatedObjectUndo(go, "Device indicator"); child = go.transform; child.SetParent(parent, false);
                Undo.DestroyObjectImmediate(go.GetComponent<Collider>());
            }
            Undo.RecordObject(child, "Place indicator"); child.position = position; child.localScale = Vector3.one * .32f;
            var renderer = child.GetComponent<Renderer>(); Undo.RecordObject(renderer, "Indicator material"); renderer.sharedMaterial = material;
            return renderer;
        }

        static TMP_FontAsset MechanismFont()
        {
            const string path = Materials + "a-mechanism-notifications.asset";
            const string text = "전원 버튼 복귀 대포 상승 유지봉 반출교 레버 권상교 그네 통로 합류 다리 해제 가동 정지 고정 중 맞은편 도착 계단 열림 → ·";
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font != null) return font;
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/_ThirdParty/curtaincall_c1_v1/Source/Fonts/Pretendard-Medium.ttf");
            if (source == null) throw new InvalidOperationException("Existing Pretendard source required");
            font = TMP_FontAsset.CreateFontAsset(source, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                1024, 1024, AtlasPopulationMode.Dynamic, true);
            font.name = "A mechanism notifications";
            if (!font.TryAddCharacters(new string(text.Distinct().ToArray()), out string missing))
                throw new InvalidOperationException("Missing mechanism notification glyphs: " + missing);
            font.atlasPopulationMode = AtlasPopulationMode.Static; AssetDatabase.CreateAsset(font, path);
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var atlas in font.atlasTextures.Where(t => t != null)) AssetDatabase.AddObjectToAsset(atlas, font);
            EditorUtility.SetDirty(font); AssetDatabase.SaveAssetIfDirty(font);
            return font;
        }
    }
}
