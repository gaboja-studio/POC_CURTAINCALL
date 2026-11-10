using System;
using System.Linq;
using CurtainCall.Cameras;
using CurtainCall.UI.PerformanceHudV1;
using CurtainCall.UI.NotificationsV1;
using CurtainCall.UI.SceneIntegration;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static class EscapePresentationUpgrade
    {
        [MenuItem("Tools/CurtainCall/탈출로/A 공용 관전·안내 연결")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != "Assets/Scenes/Levels/escape_route.unity" || scene.isDirty)
                throw new InvalidOperationException("Saved A edit scene required");
            var displays = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EscapeRopeBalanceDisplay>(true)).ToArray();
            var display = displays.Single();
            var view = (PerformanceHUDView)new SerializedObject(display).FindProperty("view").objectReferenceValue;
            var binding = display.GetComponent<PerformanceSceneBinding>() ?? Undo.AddComponent<PerformanceSceneBinding>(display.gameObject);
            Undo.RecordObject(binding, "Share escape presentation without scoring");
            binding.ConfigurePresentation(view.Balance);
            if (display.GetComponent<HumanCannonUiBinding>() == null) Undo.AddComponent<HumanCannonUiBinding>(display.gameObject);
            var notifications = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EscapeMechanismFeedback>(true)).Single()
                .GetComponentInChildren<SystemNotificationsView>(true);
            var guide = notifications.GetComponent<TightropeControlsPanel>() ?? Undo.AddComponent<TightropeControlsPanel>(notifications.gameObject);
            var message = notifications.Toast.transform.Find("Motion/60_MessageTMP").GetComponent<TMP_Text>();
            Undo.RecordObject(message, "Reuse shared notification typography");
            message.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/Notifications/Pretendard-Medium SDF.asset");
            message.fontSharedMaterial = message.font.material;
            PrefabUtility.RecordPrefabInstancePropertyModifications(message);
            Undo.RecordObject(guide, "Escape rope tutorial"); guide.ConfigureEscape();
            if (notifications.Banner.GetComponent<NotificationTextLayout>() == null)
                Undo.AddComponent<NotificationTextLayout>(notifications.Banner.gameObject);
            var timer = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CurtainCall.UI.SceneIntegration.RestTimerView>(true)).Single();
            foreach (var scaler in new[] { notifications.GetComponent<CanvasScaler>(), timer.GetComponentInParent<CanvasScaler>() })
            {
                Undo.RecordObject(scaler, "Match original UI scaling"); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                PrefabUtility.RecordPrefabInstancePropertyModifications(scaler);
            }
            var countdown = timer.transform.Find("Motion/60_CountdownTMP").GetComponent<TMP_Text>();
            Undo.RecordObject(countdown, "Restore shared timer font range"); countdown.fontSize = countdown.fontSizeMax;
            PrefabUtility.RecordPrefabInstancePropertyModifications(countdown);
            foreach (var camera in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CameraSpectator>(true)))
            {
                Undo.RecordObject(camera, "Enable common spectator controls"); camera.enabled = true;
                PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
            }
            EditorUtility.SetDirty(binding); EditorUtility.SetDirty(guide);
            Undo.FlushUndoRecordObjects();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Save failed");
        }
    }
}
