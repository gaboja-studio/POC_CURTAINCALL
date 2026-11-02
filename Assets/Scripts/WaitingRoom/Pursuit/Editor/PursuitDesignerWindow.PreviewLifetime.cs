using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public sealed partial class PursuitDesignerWindow
    {
        void SubscribePreviewLifetime()
        {
            EditorSceneManager.sceneClosing += PreviewSceneClosing;
            EditorSceneManager.sceneOpened += PreviewSceneOpened;
            EditorSceneManager.activeSceneChangedInEditMode += PreviewActiveSceneChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
            EditorApplication.quitting += Cleanup;
        }
        void UnsubscribePreviewLifetime()
        {
            EditorSceneManager.sceneClosing -= PreviewSceneClosing;
            EditorSceneManager.sceneOpened -= PreviewSceneOpened;
            EditorSceneManager.activeSceneChangedInEditMode -= PreviewActiveSceneChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= Cleanup;
            EditorApplication.quitting -= Cleanup;
        }
        void PreviewSceneClosing(Scene scene, bool removing)
        {
            if (director == null || director.gameObject.scene == scene) ResetPreviewContext();
        }
        void PreviewSceneOpened(Scene scene, OpenSceneMode mode) => ResetPreviewContext();
        void PreviewActiveSceneChanged(Scene previous, Scene next) => ResetPreviewContext();
        void ResetPreviewContext()
        {
            // HideAndDontSave objects survive scene unloading. Scene events must release them
            // even while this tab is hidden and its normal refresh is suspended.
            Cleanup(); EndPan();
            director = null; rooms = Array.Empty<PursuitRoom>(); selected = null;
            pointIndex = -1; addPoint = false; dragging = false; dragUndo = -1;
            viewInitialized = false; nextRefresh = 0;
            Repaint();
        }
    }
}
