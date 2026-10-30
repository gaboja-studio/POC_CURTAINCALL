using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.HumanCannon.EditorTools
{
    public static class CannonBarrelArtBuilder
    {
        public const string Art = HumanCannonPropArtBuilder.Art;
        public const string Gallery = "Assets/Scenes/Tests/MainStage/HumanCannon/CannonBarrelGallery.unity";
        public const string ModelPath = Art + "/Models/CC_CannonMotion.fbx";
        public const string PrefabPath = Art + "/Prefabs/CC_Cannon.prefab";

        [MenuItem("Tools/CurtainCall/인간 대포/포신 애니메이션 프리팹 갱신")]
        public static void BuildPrefab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.isReadable = true;
            importer.addCollider = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.SaveAndReimport();
            if (!AssetDatabase.IsValidFolder(Art + "/Animations")) AssetDatabase.CreateFolder(Art, "Animations");
            var sourceClips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToArray();
            var root = new GameObject("CC_Cannon");
            try
            {
                var axis = new GameObject("ForwardAxis").transform;
                axis.SetParent(root.transform, false); axis.localRotation = Quaternion.Euler(0, 180, 0);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
                model.transform.SetParent(axis, false);
                // Playback is sampled explicitly from the shared clock, including late-join seek.
                foreach (var animator in model.GetComponentsInChildren<Animator>()) Object.DestroyImmediate(animator);
                var material = AssetDatabase.LoadAssetAtPath<Material>(Art + "/Materials/CC_PropSurface.mat");
                foreach (var renderer in model.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = material;
                var view = root.AddComponent<CannonBarrelPresentation>();
                var config = new SerializedObject(view);
                config.FindProperty("animationRoot").objectReferenceValue = model;
                config.FindProperty("barrelPivot").objectReferenceValue = model.transform.Find("BarrelPivot");
                foreach (string name in new[] { "Active", "Inactive", "FireDisable", "Recover" })
                {
                    var source = sourceClips.Single(c => c.name == "CC_Cannon_" + name);
                    string path = Art + "/Animations/CC_Cannon_" + name + ".anim";
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
                    clip.ClearCurves(); clip.name = "CC_Cannon_" + name; clip.frameRate = 60;
                    // Exported static transforms are intentionally excluded: only the barrel is animated.
                    foreach (var binding in AnimationUtility.GetCurveBindings(source).Where(b => b.path == "BarrelPivot"))
                        AnimationUtility.SetEditorCurve(clip, binding, AnimationUtility.GetEditorCurve(source, binding));
                    var settings = AnimationUtility.GetAnimationClipSettings(clip);
                    settings.loopTime = name == "Active" || name == "Inactive";
                    AnimationUtility.SetAnimationClipSettings(clip, settings);
                    EditorUtility.SetDirty(clip); AssetDatabase.SaveAssetIfDirty(clip);
                    config.FindProperty(char.ToLowerInvariant(name[0]) + name.Substring(1)).objectReferenceValue = clip;
                }
                config.ApplyModifiedPropertiesWithoutUndo();
                view.Sample(CannonBarrelPhase.Active, 0);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        public static int ConfigureStations(Scene scene)
        {
            int count = 0;
            foreach (var station in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<HumanCannonStation>(true)))
            {
                if (station.GetComponentInChildren<CannonBarrelPresentation>(true) == null) continue;
                var config = new SerializedObject(station);
                config.FindProperty("animateBarrel").boolValue = true;
                config.ApplyModifiedProperties(); count++;
            }
            return count;
        }

        [MenuItem("CurtainCall/Validation/Open cannon barrel gallery")]
        public static void OpenGallery()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (System.IO.File.Exists(Gallery)) EditorSceneManager.OpenScene(Gallery);
            else BuildGallery();
        }

        public static string BuildGallery()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save existing scenes first.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var gallery = new GameObject("Cannon Barrel Gallery").AddComponent<CannonBarrelGallery>();
            var cannon = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            gallery.Cannon = cannon.GetComponent<CannonBarrelPresentation>();
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 3;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.075f, .09f, .12f);
            gallery.ViewCamera = camera;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor"; floor.transform.position = new Vector3(0, -.08f, 0); floor.transform.localScale = new Vector3(8, .1f, 8);
            var light = new GameObject("Key Light").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.5f; light.transform.rotation = Quaternion.Euler(40, -35, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.58f, .62f, .68f);
            gallery.SetCamera(135);
            if (!EditorSceneManager.SaveScene(scene, Gallery)) throw new InvalidOperationException("Gallery save failed.");
            return Gallery;
        }
    }
}
