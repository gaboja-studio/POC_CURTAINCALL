using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.HumanCannon.EditorTools
{
    /// <summary>Task-082 original Blender props. Changes presentation only; retains gameplay anchors.</summary>
    public static class HumanCannonPropArtBuilder
    {
        public const string Art = "Assets/Resources/Models/Objects/HumanCannonProps";
        public const string VisualName = "CC_PropArt";
        public const string GripName = "CC_GripArt";
        const string Waiting = "Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity";

        [MenuItem("Tools/CurtainCall/인간 대포/Blender 소품 프리팹 갱신")]
        public static void BuildPrefabs()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            AssetDatabase.Refresh();
            foreach (string name in new[] { "CC_PropAtlas", "CC_PropMetalSmooth" })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(Art + "/Textures/" + name + ".png");
                importer.sRGBTexture = name == "CC_PropAtlas";
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = true;
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
            }
            var materialPath = Art + "/Materials/CC_PropSurface.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/Textures/CC_PropAtlas.png"));
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/Textures/CC_PropMetalSmooth.png"));
            material.SetTexture("_OcclusionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/Textures/CC_PropMetalSmooth.png"));
            material.SetFloat("_OcclusionStrength", 1);
            material.EnableKeyword("_OCCLUSIONMAP");
            material.SetFloat("_Metallic", 1);
            material.SetFloat("_Smoothness", 1);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            // Pennants and appliques are deliberately thin cloth.
            material.SetFloat("_Cull", 0);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            foreach (string name in new[] { "CC_Cannon", "CC_Trampoline" })
            {
                if (name == "CC_Cannon" && AssetDatabase.LoadAssetAtPath<GameObject>(CannonBarrelArtBuilder.ModelPath) != null)
                {
                    CannonBarrelArtBuilder.BuildPrefab();
                    continue;
                }
                string modelPath = Art + "/Models/" + name + ".fbx";
                var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
                importer.importAnimation = false;
                importer.animationType = ModelImporterAnimationType.None;
                importer.addCollider = false;
                // HumanCannonOutline reads vertices at runtime.
                importer.isReadable = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.SaveAndReimport();
                var root = new GameObject(name);
                try
                {
                    var axis = new GameObject("ForwardAxis").transform;
                    axis.SetParent(root.transform, false);
                    // Blender +Y becomes Unity -Z and +X becomes -X with this FBX conversion.
                    axis.localRotation = Quaternion.Euler(0, 180, 0);
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
                    model.transform.SetParent(axis, false); // retain FBX root scale/rotation
                    foreach (var renderer in model.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = material;
                    if (name == "CC_Trampoline")
                    {
                        var handle = new GameObject("Handle").transform;
                        handle.SetParent(root.transform, false);
                        handle.localPosition = new Vector3(-2.8f, 0, 0);
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, Art + "/Prefabs/" + name + ".prefab");
                }
                finally { Object.DestroyImmediate(root); }
            }
        }

        [MenuItem("Tools/CurtainCall/인간 대포/현재 씬에 Blender 소품 적용")]
        public static void ApplyOpenScene() => Apply(SceneManager.GetActiveScene());

        public static string Apply(Scene scene)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty)
                throw new InvalidOperationException("Apply only to a saved scene outside Play mode.");
            if (scene.path != HumanCannonTestSceneBuilder.ScenePath && scene.path != Waiting)
                throw new InvalidOperationException("Only HumanCannon and WaitingRoomBase are supported.");
            var cannon = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/Prefabs/CC_Cannon.prefab");
            var trampoline = CurtainCall.Settings.GameSettings.Trampoline.VisualPrefab;
            if (!cannon || !trampoline) throw new InvalidOperationException("Build prop prefabs first.");
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            int cannons = 0, trampolines = 0;
            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply original circus prop art");
            try
            {
                if (scene.path == HumanCannonTestSceneBuilder.ScenePath)
                {
                    foreach (var station in all.Select(t => t.GetComponent<HumanCannonStation>()).Where(c => c != null))
                    { Replace(station.transform, cannon); cannons++; }
                    foreach (var t in all.Select(t => t.GetComponent<HumanCannonTrampoline>()).Where(c => c != null))
                    {
                        ApplyCommonTrampoline(t.gameObject);
                        trampolines++;
                    }
                }
                else
                {
                    // Scope to authored props; leave practice game logic, stage geometry and hidden storage untouched.
                    foreach (var t in all.Where(t => t.gameObject.activeInHierarchy && t.name == "HumanCannon"))
                    { Replace(t, cannon); cannons++; }
                    foreach (var t in all.Where(t => t.gameObject.activeInHierarchy && (t.name == "CarryTrampoline" || t.name == "PracticeTrampoline")))
                    { Replace(t, trampoline); trampolines++; }
                }
                CannonBarrelArtBuilder.ConfigureStations(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Scene save failed.");
                Undo.CollapseUndoOperations(undo);
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
            return $"{scene.path}: cannons={cannons}, trampolines={trampolines}; gameplay components and colliders retained";
        }

        /// <summary>외형과 손잡이 참조만 교체한다. 루트 배치·기물 수치·충돌·대포는 유지한다.</summary>
        public static void ApplyCommonTrampoline(GameObject target)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            var prefab = CurtainCall.Settings.GameSettings.Trampoline.VisualPrefab;
            if (prefab == null || prefab.transform.Find("Handle") == null)
                throw new InvalidOperationException("TrampolineSettings needs a common visual with a Handle.");
            var visual = Replace(target.transform, prefab);
            if (target.TryGetComponent<HumanCannonTrampoline>(out var trampoline))
            {
                var data = new SerializedObject(trampoline);
                data.FindProperty("handle").objectReferenceValue = visual.transform.Find("Handle");
                data.ApplyModifiedProperties();
                EditorUtility.SetDirty(trampoline);
                if (PrefabUtility.IsPartOfPrefabInstance(trampoline)) PrefabUtility.RecordPrefabInstancePropertyModifications(trampoline);
            }
        }

        [MenuItem("Tools/CurtainCall/트램펄린/현재 씬 공통 외형 적용")]
        static void ApplyCommonTrampolinesOpenScene() => Debug.Log(ApplyCommonTrampolines(SceneManager.GetActiveScene()));

        public static string ApplyCommonTrampolines(Scene scene)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid() || scene.isDirty)
                throw new InvalidOperationException("Use a saved scene outside Play mode.");
            var targets = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => t.GetComponent<HumanCannonTrampoline>() != null || t.name == "PracticeTrampoline"
                    || t.name == "CarryTrampoline" || t.name == "RoundCarryTrampolineVisual"
                    || t.name != VisualName && PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)
                    && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) == Art + "/Prefabs/CC_Trampoline.prefab").ToArray();
            foreach (var target in targets) ApplyCommonTrampoline(target.gameObject);
            if (targets.Length > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Trampoline visual save failed.");
            }
            return scene.name + ": common trampoline visuals=" + targets.Length;
        }

        static GameObject Replace(Transform target, GameObject prefab)
        {
            var previous = target.Find(VisualName);
            if (previous != null) Undo.DestroyObjectImmediate(previous.gameObject);
            foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                // A stacked prop can be a descendant of this one; do not disable its new art.
                if (renderer.transform.GetComponentsInParent<Transform>(true).Any(t => t.name == VisualName)) continue;
                Undo.RecordObject(renderer, "Hide previous prop renderer");
                renderer.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, target);
            Undo.RegisterCreatedObjectUndo(instance, "Add Blender prop");
            instance.name = VisualName;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            return instance;
        }
    }
}
