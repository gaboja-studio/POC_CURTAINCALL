using System;
using System.Linq;
using System.Text;
using CurtainCall.Cameras;
using CurtainCall.Network.Session;
using CurtainCall.Tightrope;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static class EscapeDevelopmentSetup
    {
        public static bool IsEscapeScene(Scene scene) => scene.path == "Assets/Scenes/Levels/escape_route.unity"
            || scene.path == "Assets/Scenes/Levels/escape_route_b.unity"
            || scene.path == "Assets/Scenes/Levels/escape_route_c.unity";

        [MenuItem("Tools/CurtainCall/탈출로/현재 구조를 공통 1인 개발환경으로 설정")]
        public static void ConfigureCurrent()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || !IsEscapeScene(scene)) throw new InvalidOperationException("Open an escape development scene in Edit mode");
            var behaviours = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            var hud = behaviours.OfType<EscapeCannonReviewHud>().Single(h => h.gameObject.activeInHierarchy);
            if (hud.GetComponent<EscapeDevelopmentSession>() == null) Undo.AddComponent<EscapeDevelopmentSession>(hud.gameObject);
            var camera = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).Single(c => c.CompareTag("MainCamera"));
            if (camera.GetComponent<EscapeInspectionSession>() == null) Undo.AddComponent<EscapeInspectionSession>(camera.gameObject);
            if (camera.GetComponent<EscapeInspectionCamera>() == null) Undo.AddComponent<EscapeInspectionCamera>(camera.gameObject);
            foreach (var course in behaviours.OfType<TightropeCourse>().Where(c => c.gameObject.activeInHierarchy))
                if (course.GetComponent<RopeLaneJumpRules>() == null) Undo.AddComponent<RopeLaneJumpRules>(course.gameObject);
            foreach (var room in behaviours.OfType<NetworkSessionTestUI>()) Undo.DestroyObjectImmediate(room);
            foreach (var item in behaviours.Where(b => b is CameraSpectator))
            {
                Undo.RecordObject(item, "Use common development recovery");
                item.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(item);
            }
            EditorSceneManager.MarkSceneDirty(scene);
        }

        public static string Validate(Scene scene)
        {
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).Where(b => b != null).ToArray();
            var active = all.Where(b => b.isActiveAndEnabled).ToArray();
            var report = new StringBuilder(scene.path + "\n");
            int failures = 0;
            void Check(string name, bool passed) { if (!passed) failures++; report.AppendLine((passed ? "PASS " : "FAIL ") + name); }
            Check("one shared automatic solo session", active.OfType<EscapeDevelopmentSession>().Count() == 1);
            Check("one common control/restart HUD", active.OfType<EscapeCannonReviewHud>().Count() == 1);
            Check("one restored inspection flight pair", active.OfType<EscapeInspectionCamera>().Count() == 1
                && active.OfType<EscapeInspectionSession>().Count() == 1);
            Check("no room creation UI", !all.OfType<NetworkSessionTestUI>().Any());
            Check("no spectator lock/overlay", !active.OfType<CameraSpectator>().Any());
            Check("no campaign/progression/result owner", !active.Any(b => b is CurtainCall.Campaign.CampaignRun
                || b is TightropeCampaignBridge || b is CurtainCall.HumanCannon.HumanCannonDestination
                || b is CurtainCall.UI.SceneIntegration.PerformanceSceneBinding));
            Check("one traversal-only rope course", active.OfType<TightropeCourse>().Count() == 1
                && active.OfType<TightropeCourse>().All(c => c.UsesSceneGeometry && c.HasIndependentRopes));
            Check("one original cannon system", active.OfType<CurtainCall.HumanCannon.HumanCannonSystem>().Count() == 1);
            report.AppendLine("Failures=" + failures);
            return report.ToString();
        }
    }
}
