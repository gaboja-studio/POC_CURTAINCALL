#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.Player.Validation.Editor
{
    public static class AnimationPreviewBuilder
    {
        public const string Folder = "Assets/_ThirdParty/Task062/Mixamo";
        public const string PrefabPath = Folder + "/AnimationPreview.prefab";
        public const string ScenePath = "Assets/Scenes/Tests/Systems/HumanoidCharacters/MixamoAnimationPreview.unity";

        [MenuItem("CurtainCall/Validation/Build Task062 animation preview")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Editor must be idle outside Prefab Mode.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Unsaved scene; refusing to overwrite.");
            RequireAbsent(PrefabPath);
            RequireAbsent(ScenePath);
            var paths = AssetDatabase.FindAssets("t:Model", new[] { Folder }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            if (paths.Length != 8) throw new InvalidOperationException("Expected eight user clips.");
            var clips = paths.Select(p => AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>().Single(c => !c.name.StartsWith("__preview__"))).ToArray();
            if (clips.Any(c => !c.humanMotion)) throw new InvalidOperationException("All clips must be Humanoid.");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_ThirdParty/Task062/SequentialValidation/Validation.prefab");
            if (source == null) throw new InvalidOperationException("Sequential validation prefab missing.");
            Scene previous = SceneManager.GetActiveScene();
            Scene preview = default, output = default;
            string prefabGuid = null, sceneGuid = null;
            bool committed = false;
            try
            {
                preview = EditorSceneManager.NewPreviewScene();
                var wrapper = (GameObject)PrefabUtility.InstantiatePrefab(source, preview);
                PrefabUtility.UnpackPrefabInstance(wrapper, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                wrapper.SetActive(false);
                wrapper.name = "Task062 Mixamo Animation Preview";
                var old = wrapper.GetComponent<SequentialCutValidation>();
                var original = old.Original;
                var bodies = old.Bodies;
                var animator = old.Animator;
                foreach (var limb in old.Limbs) limb.enabled = false;
                Object.DestroyImmediate(old);
                var view = wrapper.AddComponent<AnimationClipPreview>();
                view.Animator = animator;
                view.Original = original;
                view.Bodies = bodies;
                view.Clips = clips;
                view.Labels = paths.Select(Path.GetFileNameWithoutExtension).ToArray();
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                view.SelectBody(0);
                wrapper.SetActive(true);
                RequireAbsent(PrefabPath);
                var prefab = PrefabUtility.SaveAsPrefabAsset(wrapper, PrefabPath, out bool saved);
                prefabGuid = AssetDatabase.AssetPathToGUID(PrefabPath, AssetPathToGUIDOptions.OnlyExistingAssets);
                if (!saved || prefab == null) throw new InvalidOperationException("Preview prefab save failed.");
                output = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                PrefabUtility.InstantiatePrefab(prefab, output);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                SceneManager.MoveGameObjectToScene(floor, output);
                floor.name = "Preview floor";
                floor.transform.position = new Vector3(0, -.05f, 0);
                floor.transform.localScale = new Vector3(10, .1f, 10);
                var camera = new GameObject("Preview camera").AddComponent<Camera>();
                SceneManager.MoveGameObjectToScene(camera.gameObject, output);
                camera.tag = "MainCamera";
                camera.transform.position = new Vector3(3, 1.8f, 3.5f);
                camera.transform.LookAt(new Vector3(0, .7f, 0));
                camera.backgroundColor = new Color(.08f, .1f, .14f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                var light = new GameObject("Preview light").AddComponent<Light>();
                SceneManager.MoveGameObjectToScene(light.gameObject, output);
                light.type = LightType.Directional;
                light.intensity = 1.5f;
                light.transform.rotation = Quaternion.Euler(40, -30, 0);
                RequireAbsent(ScenePath);
                bool sceneSaved = EditorSceneManager.SaveScene(output, ScenePath);
                sceneGuid = AssetDatabase.AssetPathToGUID(ScenePath, AssetPathToGUIDOptions.OnlyExistingAssets);
                if (!sceneSaved) throw new InvalidOperationException("Preview scene save failed.");
                committed = true;
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (output.IsValid() && output.isLoaded) EditorSceneManager.CloseScene(output, true);
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
                if (!committed)
                {
                    if (!string.IsNullOrEmpty(sceneGuid) && AssetDatabase.AssetPathToGUID(ScenePath) == sceneGuid) AssetDatabase.DeleteAsset(ScenePath);
                    if (!string.IsNullOrEmpty(prefabGuid) && AssetDatabase.AssetPathToGUID(PrefabPath) == prefabGuid) AssetDatabase.DeleteAsset(PrefabPath);
                }
            }
        }

        static void RequireAbsent(string path)
        {
            if (File.Exists(path) || Directory.Exists(path) || File.Exists(path + ".meta") || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets)))
                throw new InvalidOperationException("Existing output: " + path);
        }
    }
}
#endif
