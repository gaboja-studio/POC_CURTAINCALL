using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.Player.Validation.Editor
{
    public static class HumanoidValidationBuilder
    {
        const string Output = "Assets/_ThirdParty/Task062/HumanoidValidation";
        const string Source = "Assets/_ThirdParty/mighty_handful/Hyper-Casual Characters (43 BodyTypes)/Models/Body Type 1.fbx";
        public const string ScenePath = "Assets/Scenes/Tests/Systems/HumanoidCharacters/HumanoidCharacters.unity";

        [MenuItem("CurtainCall/Validation/Build Task062 single leg")]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling) throw new InvalidOperationException("Editor is busy");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Unsaved scene: abort without overwriting");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Output + "/Validation.prefab") != null)
                throw new InvalidOperationException("Validation already exists; inspect before rebuilding");
            string copy = Output + "/Body Type 1.fbx";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(copy) == null && !AssetDatabase.CopyAsset(Source, copy))
                throw new InvalidOperationException("FBX copy failed");
            var importer = (ModelImporter)AssetImporter.GetAtPath(copy);
            importer.isReadable = true;
            importer.SaveAndReimport();
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(copy));
            try
            {
                model.name = "Body Type 1 validation";
                var animator = model.GetComponentInChildren<Animator>();
                if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.isHuman)
                    throw new InvalidOperationException("Invalid Humanoid Avatar");
                var skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
                var split = FixedLegMeshBuilder.Build(skin, animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg), animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg));
                AssetDatabase.CreateAsset(split.body, Output + "/Body.asset");
                AssetDatabase.CreateAsset(split.leg, Output + "/LeftLeg.asset");
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                var surface = new Material(shader) { name = "Validation surface" };
                surface.color = new Color(0.26f, 0.65f, 0.82f);
                var cap = new Material(shader) { name = "Validation cap" };
                cap.color = new Color(0.75f, 0.12f, 0.18f);
                AssetDatabase.CreateAsset(surface, Output + "/Surface.mat");
                AssetDatabase.CreateAsset(cap, Output + "/Cap.mat");
                skin.sharedMaterials = Enumerable.Repeat(surface, skin.sharedMesh.subMeshCount).ToArray();
                var materials = skin.sharedMaterials.Concat(new[] { cap }).ToArray();
                var body = AddSkin("Capped body", skin, split.body, materials);
                var leg = AddSkin("Capped left leg", skin, split.leg, materials);
                body.enabled = false;
                leg.enabled = false;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var controller = AnimatorController.CreateAnimatorControllerAtPath(Output + "/Validation.controller");
                string root = "Assets/_ThirdParty/Kevin Iglesias/Human Animations/Animations/Male/";
                string[] paths = {
                    "Idles/HumanM@Idle01.fbx", "Movement/Walk/HumanM@Walk01_Forward.fbx",
                    "Movement/Run/HumanM@Run01_Forward.fbx", "Movement/Crouch/HumanM@Crouch01_Idle.fbx",
                    "Movement/Crouch/CrouchWalk/HumanM@Crouch01_Walk_Forward.fbx"
                };
                string[] names = { "Idle", "Walk", "Run", "Crouch", "CrouchWalk" };
                for (int i = 0; i < names.Length; i++)
                {
                    var clips = AssetDatabase.LoadAllAssetsAtPath(root + paths[i]).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
                    if (clips.Length != 1) throw new InvalidOperationException("Expected one clip: " + root + paths[i]);
                    var state = controller.layers[0].stateMachine.AddState(names[i]);
                    state.motion = clips[0];
                    if (i == 0) controller.layers[0].stateMachine.defaultState = state;
                }
                animator.runtimeAnimatorController = controller;
                var wrapper = new GameObject("Task062 Humanoid Validation");
                model.transform.SetParent(wrapper.transform, false);
                wrapper.AddComponent<PlayerCondition>();
                var validation = wrapper.AddComponent<HumanoidCutValidation>();
                validation.Original = skin;
                validation.Body = body;
                validation.Leg = leg;
                validation.Animator = animator;
                PrefabUtility.SaveAsPrefabAsset(wrapper, Output + "/Validation.prefab");
                UnityEngine.Object.DestroyImmediate(wrapper);
                model = null;
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Output + "/Validation.prefab"));
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Validation floor";
                floor.transform.position = new Vector3(0, -0.08f, 0);
                floor.transform.localScale = new Vector3(8, 0.1f, 8);
                var camera = new GameObject("Validation camera").AddComponent<Camera>();
                camera.transform.position = new Vector3(3f, 1.8f, 3.5f);
                camera.transform.LookAt(new Vector3(0, 0.85f, 0));
                camera.backgroundColor = new Color(0.08f, 0.1f, 0.14f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                var light = new GameObject("Validation light").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.5f;
                light.transform.rotation = Quaternion.Euler(40, -30, 0);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                Debug.Log("[Task062] " + split.report);
            }
            finally
            {
                if (model != null) UnityEngine.Object.DestroyImmediate(model);
            }
        }

        static SkinnedMeshRenderer AddSkin(string name, SkinnedMeshRenderer source, Mesh mesh, Material[] materials)
        {
            var go = new GameObject(name);
            go.transform.SetParent(source.transform.parent, false);
            go.transform.localPosition = source.transform.localPosition;
            go.transform.localRotation = source.transform.localRotation;
            go.transform.localScale = source.transform.localScale;
            var skin = go.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = mesh;
            skin.bones = source.bones;
            skin.rootBone = source.rootBone;
            skin.sharedMaterials = materials;
            skin.localBounds = source.localBounds;
            skin.updateWhenOffscreen = true;
            return skin;
        }
    }
}
