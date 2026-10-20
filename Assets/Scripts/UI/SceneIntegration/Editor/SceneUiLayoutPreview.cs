#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.UI.SceneIntegration.Editor
{
    /// <summary>편집용 표시만 임시 변경한다. 위치/크기 편집은 유지하고 저장·실행 전에 표시 상태를 복원한다.</summary>
    [InitializeOnLoad]
    public static class SceneUiLayoutPreview
    {
        const string ShowMenu = "Tools/Curtain Call UI/Preview selected UI in Scene View";
        const string StopMenu = "Tools/Curtain Call UI/End layout preview";
        static readonly Dictionary<GameObject, bool> Active = new();
        static readonly Dictionary<CanvasGroup, float> Alphas = new();
        static readonly Dictionary<Canvas, bool> Canvases = new();
        static GameObject previewRoot;
        static int revision;
        public static bool IsPreviewing => previewRoot != null;

        static SceneUiLayoutPreview()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
            EditorApplication.playModeStateChanged += _ => Stop();
            EditorSceneManager.sceneClosing += (_, __) => Stop();
            EditorSceneManager.sceneSaving += BeforeSave;
            EditorSceneManager.sceneSaved += AfterSave;
        }

        [MenuItem(ShowMenu)]
        static void ShowSelected()
        {
            Preview(Selection.activeGameObject);
            if (!IsPreviewing) return;
            SceneView.lastActiveSceneView?.FrameSelected();
        }
        [MenuItem(ShowMenu, true)]
        static bool CanShow() => CanPreview(Selection.activeGameObject);
        static bool CanPreview(GameObject root) => !EditorApplication.isPlayingOrWillChangePlaymode
            && root != null && !EditorUtility.IsPersistent(root) && root.scene.IsValid()
            && root.scene.isLoaded && PrefabStageUtility.GetCurrentPrefabStage() == null
            && root.GetComponent<RectTransform>() != null && root.GetComponentInParent<Canvas>(true) != null;

        public static void Preview(GameObject root)
        {
            Stop();
            if (!CanPreview(root)) return;
            previewRoot = root;
            Show();
        }
        static void Show()
        {
            if (previewRoot == null) return;
            // 한 프레임에 여러 번 저장해도 표시 중 상태를 원래 상태로 덮어쓰지 않는다.
            if (Active.Count != 0 || Alphas.Count != 0 || Canvases.Count != 0) return;
            // 선택한 패널과 부모만 활성화한다. 비활성 자식은 다른 화면/장식일 수 있으므로 유지한다.
            for (var node = previewRoot.transform; node != null; node = node.parent)
            {
                Active[node.gameObject] = node.gameObject.activeSelf;
                node.gameObject.SetActive(true);
                foreach (var group in node.GetComponents<CanvasGroup>()) Reveal(group);
                if (node.TryGetComponent<Canvas>(out var canvas))
                { Canvases[canvas] = canvas.enabled; canvas.enabled = true; }
            }
            foreach (var group in previewRoot.GetComponentsInChildren<CanvasGroup>(true)) Reveal(group);
            Canvas.ForceUpdateCanvases();
            SceneView.RepaintAll();
        }
        static void Reveal(CanvasGroup group)
        {
            if (!Alphas.ContainsKey(group)) Alphas.Add(group, group.alpha);
            group.alpha = 1;
        }
        static void RestoreVisibility()
        {
            foreach (var pair in Alphas) if (pair.Key != null) pair.Key.alpha = pair.Value;
            foreach (var pair in Canvases) if (pair.Key != null) pair.Key.enabled = pair.Value;
            foreach (var pair in Active) if (pair.Key != null) pair.Key.SetActive(pair.Value);
            Alphas.Clear(); Canvases.Clear(); Active.Clear();
            SceneView.RepaintAll();
        }
        [MenuItem(StopMenu)]
        public static void Stop()
        {
            revision++;
            RestoreVisibility();
            previewRoot = null;
        }
        [MenuItem(StopMenu, true)]
        static bool CanStop() => IsPreviewing;
        static void BeforeSave(Scene scene, string path)
        {
            if (previewRoot != null && previewRoot.scene == scene) RestoreVisibility();
        }
        static void AfterSave(Scene scene)
        {
            if (previewRoot == null || previewRoot.scene != scene) return;
            int expected = revision;
            EditorApplication.delayCall += () =>
            {
                if (expected == revision && CanPreview(previewRoot)) Show();
            };
        }
    }
}
#endif
