using System;
using System.Linq;
using System.Reflection;
using CurtainCall.Settings;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public sealed class PursuitPreviewLifetimeTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static object Call(PursuitDesignerWindow w, string name, params object[] args) => typeof(PursuitDesignerWindow).GetMethod(name, Private).Invoke(w, args);
        static object Field(PursuitDesignerWindow w, string name) => typeof(PursuitDesignerWindow).GetField(name, Private).GetValue(w);
        static bool IsPreview(GameObject go) => go.name.StartsWith("__Preview_Pursuit_", StringComparison.Ordinal) || go.name == "__PursuitDesignerCamera";
        static void Prepare(PursuitDesignerWindow w)
        {
            Call(w, "Refresh"); Call(w, "EnsureView", new Rect(0, 0, 400, 400)); Call(w, "PreparePreview", new Rect(0, 0, 400, 400));
        }
        [TestCase(false)]
        [TestCase(true)]
        public void LeavingPursuitReleasesModelsCameraAndTextureEvenWhenHidden(bool hiddenAndAdditive)
        {
            var window = ScriptableObject.CreateInstance<PursuitDesignerWindow>();
            try {
                var pursuit = EditorSceneManager.OpenScene(WaitingRoomScenes.PursuitPath);
                string dataBefore = string.Join("|", Object.FindObjectsByType<PursuitPlacement>(FindObjectsSortMode.None).OrderBy(p => p.GetInstanceID()).Select(JsonUtility.ToJson));
                Prepare(window);
                var camera = (Camera)Field(window, "previewCamera");
                var texture = (RenderTexture)Field(window, "texture");
                var models = Resources.FindObjectsOfTypeAll<GameObject>().Where(g => g.name.StartsWith("__Preview_Pursuit_", StringComparison.Ordinal)).ToArray();
                Assert.Greater(models.Length, 0);
                Assert.IsNotNull(camera); Assert.IsNotNull(texture);
                Assert.IsFalse(pursuit.isDirty);
                if (hiddenAndAdditive) Call(window, "OnBecameInvisible");
                string start = EditorBuildSettings.scenes.First(s => System.IO.Path.GetFileNameWithoutExtension(s.path) == "Start").path;
                var scene = EditorSceneManager.OpenScene(start, hiddenAndAdditive ? OpenSceneMode.Additive : OpenSceneMode.Single);
                SceneManager.SetActiveScene(scene);
                Assert.IsTrue(camera == null); Assert.IsTrue(texture == null);
                Assert.IsTrue(models.All(m => m == null));
                Assert.AreEqual(0, Resources.FindObjectsOfTypeAll<GameObject>().Count(IsPreview));
                Call(window, "Refresh");
                Assert.IsTrue((Object)Field(window, "director") == null, "An inactive additive pursuit scene must not supply Start's previews.");
                Assert.IsFalse(scene.isDirty);
                if (hiddenAndAdditive) {
                    string dataAfter = string.Join("|", Object.FindObjectsByType<PursuitPlacement>(FindObjectsSortMode.None).OrderBy(p => p.GetInstanceID()).Select(JsonUtility.ToJson));
                    Assert.AreEqual(dataBefore, dataAfter);
                    Assert.IsFalse(pursuit.isDirty);
                    SceneManager.SetActiveScene(pursuit);
                } else EditorSceneManager.OpenScene(WaitingRoomScenes.PursuitPath);
                Call(window, "OnBecameVisible"); Prepare(window);
                Assert.IsNotNull((Camera)Field(window, "previewCamera"), "Re-entry must recreate previews.");
            } finally { Object.DestroyImmediate(window); }
        }
    }
}