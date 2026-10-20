#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.Player.Validation.Editor
{
    /// <summary>기존 검증 에셋을 보존하고 매 단계에서 직전 휴지 포즈 본체를 절단한다.</summary>
    public static class SequentialValidationBuilder
    {
        private const string Parent = "Assets/_ThirdParty/Task062";
        private const string Output = Parent + "/SequentialValidation";
        private const string Source = Parent + "/HumanoidValidation/Body Type 1.fbx";
        private const string SourceController = Parent + "/HumanoidValidation/Validation.controller";
        public const string ScenePath = "Assets/Scenes/Tests/Systems/HumanoidCharacters/SequentialHumanoidCharacters.unity";
        private static readonly string[] LimbNames = { "LeftLeg", "RightLeg", "LeftArm", "RightArm" };
        // 대칭 절단면과 기존 캡 정점의 일치를 피하는 Body Type 1 전용 고정 프로파일. 자동 재시도하지 않는다.
        private static readonly float[] CutFractions = { .45f, .47f, .45f, .47f };
        private static readonly HumanBodyBones[] UpperBones = {
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm
        };
        private static readonly HumanBodyBones[] LowerBones = {
            HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg,
            HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm
        };

        [MenuItem("CurtainCall/Validation/Build Task062 sequential limbs")]
        public static void Build()
        {
            RequireIdleCleanScenes();
            RequireAbsent(Output + "/Validation.prefab");
            // 이전 폴더나 실패한 부분 결과를 무단 재사용하지 않는다.
            RequireAbsent(Output);
            RequireAbsent(ScenePath);
            if (!AssetDatabase.IsValidFolder(Parent) ||
                !AssetDatabase.IsValidFolder("Assets/Scenes/Tests/Systems/HumanoidCharacters"))
                throw new InvalidOperationException("Required Task062 output parent folders are missing.");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            if (source == null || AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(SourceController) == null)
                throw new InvalidOperationException("Build the existing single validation first; its copied readable FBX and normal controller are required.");

            var transient = new List<Object>();
            var ownedAssets = new Dictionary<string, string>();
            string ownedFolderGuid = null;
            Scene preview = default;
            Scene validationScene = default;
            Scene previousActive = SceneManager.GetActiveScene();
            bool committed = false;
            try
            {
                // 임시 객체는 preview 씬에 격리해 사용자의 저장 씬을 보호한다.
                preview = EditorSceneManager.NewPreviewScene();
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source, preview);
                if (model == null) throw new InvalidOperationException("Copied FBX instantiation failed.");
                model.name = "Body Type 1 sequential validation";
                var animators = model.GetComponentsInChildren<Animator>(true);
                var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (animators.Length != 1 || skins.Length != 1)
                    throw new InvalidOperationException("Expected one Animator and one source skinned renderer; refusing to omit geometry.");
                Animator animator = animators[0];
                SkinnedMeshRenderer skin = skins[0];
                if (animator.avatar == null || !animator.avatar.isValid || !animator.isHuman)
                    throw new InvalidOperationException("Invalid Humanoid Avatar.");
                Mesh originalMesh = skin.sharedMesh;
                if (originalMesh == null || !originalMesh.isReadable || originalMesh.subMeshCount < 1)
                    throw new InvalidOperationException("Existing copied FBX must already contain a readable mesh; importer will not be changed.");
                int surfaceSubmeshes = originalMesh.subMeshCount;
                var bodies = new Mesh[4];
                var limbs = new Mesh[4];
                var reports = new string[4];
                try
                {
                    // Animator.Update/Rebind나 샘플링 없이 엄격한 휴지 포즈 검사를 유지한다.
                    for (int i = 0; i < 4; i++)
                    {
                        Transform upper = animator.GetBoneTransform(UpperBones[i]);
                        Transform lower = animator.GetBoneTransform(LowerBones[i]);
                        if (upper == null || lower == null)
                            throw new InvalidOperationException("Missing Humanoid mapping for " + LimbNames[i]);
                        skin.sharedMesh = i == 0 ? originalMesh : bodies[i - 1];
                        try
                        {
                            var split = FixedLegMeshBuilder.Build(skin, upper, lower, CutFractions[i]);
                            bodies[i] = split.body;
                            limbs[i] = split.leg;
                            transient.Add(split.body);
                            transient.Add(split.leg);
                            bodies[i].name = "Body" + (i + 1);
                            limbs[i].name = LimbNames[i];
                            int expectedSubmeshes = surfaceSubmeshes + i + 1;
                            ValidateStageMesh(bodies[i], expectedSubmeshes, originalMesh.bindposes.Length);
                            ValidateStageMesh(limbs[i], expectedSubmeshes, originalMesh.bindposes.Length);
                            reports[i] = LimbNames[i] + ": " + split.report;
                        }
                        catch (Exception exception)
                        {
                            throw new InvalidOperationException("Sequential stage " + (i + 1) + " (" + LimbNames[i] + ") failed; strict geometry guards were not relaxed. " + exception.Message, exception);
                        }
                    }
                }
                finally { skin.sharedMesh = originalMesh; }

                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader == null) throw new InvalidOperationException("No supported surface shader found.");
                var surface = new Material(shader) { name = "Sequential surface", color = new Color(0.26f, 0.65f, 0.82f) };
                transient.Add(surface);
                var cap = new Material(shader) { name = "Sequential cap", color = new Color(0.75f, 0.12f, 0.18f) };
                transient.Add(cap);
                skin.sharedMaterials = Enumerable.Repeat(surface, surfaceSubmeshes).ToArray();
                var bodyRenderers = new SkinnedMeshRenderer[4];
                var limbRenderers = new SkinnedMeshRenderer[4];
                for (int i = 0; i < 4; i++)
                {
                    // 마지막 절단면뿐 아니라 이전 캡의 서브메시와 재질도 모두 보존한다.
                    var materials = Enumerable.Repeat(surface, surfaceSubmeshes)
                        .Concat(Enumerable.Repeat(cap, i + 1)).ToArray();
                    bodyRenderers[i] = AddSkin("Capped body " + (i + 1), skin, bodies[i], materials);
                    limbRenderers[i] = AddSkin("Capped " + LimbNames[i], skin, limbs[i], materials);
                }
                var wrapper = new GameObject("Task062 Sequential Humanoid Validation");
                SceneManager.MoveGameObjectToScene(wrapper, preview);
                model.transform.SetParent(wrapper.transform, false);
                wrapper.AddComponent<PlayerCondition>();
                var validation = wrapper.AddComponent<CurtainCall.Player.Validation.SequentialCutValidation>();
                validation.Original = skin;
                validation.Bodies = bodyRenderers;
                validation.Limbs = limbRenderers;
                validation.Animator = animator;
                skin.enabled = true;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                // 네 단계 메시와 컴포넌트 연결을 검증한 다음에만 에셋을 저장한다.
                RequireIdleCleanScenes();
                RequireAbsent(Output);
                RequireAbsent(ScenePath);
                ownedFolderGuid = AssetDatabase.CreateFolder(Parent, "SequentialValidation");
                if (string.IsNullOrEmpty(ownedFolderGuid) || AssetDatabase.GUIDToAssetPath(ownedFolderGuid) != Output)
                    throw new InvalidOperationException("Could not create the exact private sequential output folder.");
                for (int i = 0; i < 4; i++)
                {
                    CreateAsset(bodies[i], Output + "/Body" + (i + 1) + ".asset", ownedAssets);
                    CreateAsset(limbs[i], Output + "/" + LimbNames[i] + ".asset", ownedAssets);
                }
                CreateAsset(surface, Output + "/Surface.mat", ownedAssets);
                CreateAsset(cap, Output + "/Cap.mat", ownedAssets);
                string controllerPath = Output + "/Sequential.controller";
                RequireAbsent(controllerPath);
                bool copied;
                try { copied = AssetDatabase.CopyAsset(SourceController, controllerPath); }
                finally { RememberCreated(controllerPath, ownedAssets); }
                if (!copied) throw new InvalidOperationException("Normal controller clone failed.");
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
                if (animator.runtimeAnimatorController == null) throw new InvalidOperationException("Cloned normal controller could not be loaded.");
                string prefabPath = Output + "/Validation.prefab";
                RequireAbsent(prefabPath);
                GameObject prefab;
                bool prefabSaved;
                try { prefab = PrefabUtility.SaveAsPrefabAsset(wrapper, prefabPath, out prefabSaved); }
                finally { RememberCreated(prefabPath, ownedAssets); }
                if (!prefabSaved || prefab == null) throw new InvalidOperationException("Sequential prefab save failed.");

                // Additive 생성으로 현재 열린 단일 검증 씬과 파일을 보존한다.
                RequireIdleCleanScenes();
                validationScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                if (SceneManager.GetActiveScene() != validationScene && !SceneManager.SetActiveScene(validationScene))
                    throw new InvalidOperationException("Could not activate the new validation scene.");
                if (PrefabUtility.InstantiatePrefab(prefab, validationScene) == null)
                    throw new InvalidOperationException("Sequential scene prefab instantiation failed.");
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Validation floor";
                floor.transform.position = new Vector3(0f, -0.05f, 0f);
                floor.transform.localScale = new Vector3(10f, 0.1f, 10f);
                var camera = new GameObject("Validation camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.transform.position = new Vector3(3f, 1.8f, 3.5f);
                camera.transform.LookAt(new Vector3(0f, 0.7f, 0f));
                camera.fieldOfView = 55f;
                camera.backgroundColor = new Color(0.08f, 0.1f, 0.14f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                var light = new GameObject("Validation light").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.5f;
                light.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
                RequireAbsent(ScenePath);
                bool sceneSaved;
                try { sceneSaved = EditorSceneManager.SaveScene(validationScene, ScenePath); }
                finally { RememberCreated(ScenePath, ownedAssets); }
                if (!sceneSaved) throw new InvalidOperationException("Sequential validation scene save failed.");
                committed = true;
                Debug.Log("[Task062 Sequential] Stages/masks: original=0, LeftLeg=4, legs=12, legs+LeftArm=13, all=15. Normal controller cloned; no stage-specific clips/controllers generated.\n" + string.Join("\n", reports));
            }
            finally
            {
                // 씬 구성을 복구하고 기존 에셋이나 단일 검증 결과는 정리 대상으로 삼지 않는다.
                try
                {
                    if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
                    if (validationScene.IsValid() && validationScene.isLoaded)
                        EditorSceneManager.CloseScene(validationScene, true);
                }
                catch (Exception exception) { Debug.LogError("[Task062 Sequential] Scene cleanup failed: " + exception.Message); }
                try { if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview); }
                catch (Exception exception) { Debug.LogError("[Task062 Sequential] Preview cleanup failed: " + exception.Message); }
                if (!committed) RollBack(ownedAssets, ownedFolderGuid);
                foreach (Object value in transient)
                    if (value != null && !EditorUtility.IsPersistent(value)) Object.DestroyImmediate(value);
            }
        }

        private static void RequireIdleCleanScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Editor is busy or in Prefab Mode; sequential build aborted.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Unsaved scene: abort without overwriting.");
        }

        private static void RequireAbsent(string path)
        {
            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets)) || File.Exists(path) || Directory.Exists(path) || File.Exists(path + ".meta"))
                throw new InvalidOperationException("Existing output must be inspected, not overwritten: " + path);
        }

        private static void ValidateStageMesh(Mesh mesh, int expectedSubmeshes, int boneCount)
        {
            if (mesh == null || !mesh.isReadable || mesh.vertexCount == 0 || mesh.subMeshCount != expectedSubmeshes ||
                mesh.bindposes.Length != boneCount || mesh.GetIndexCount(expectedSubmeshes - 1) == 0)
                throw new InvalidOperationException("Stage mesh or its final cap submesh is invalid.");
        }

        private static SkinnedMeshRenderer AddSkin(string name, SkinnedMeshRenderer source, Mesh mesh, Material[] materials)
        {
            if (materials.Length != mesh.subMeshCount) throw new InvalidOperationException("Material/submesh count mismatch.");
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, source.gameObject.scene);
            // 원본이 루트 렌더러여도 생성 조각을 모델/프리팹 내부에 둔다.
            if (source.transform.parent == null)
                go.transform.SetParent(source.transform, false);
            else
            {
                go.transform.SetParent(source.transform.parent, false);
                go.transform.localPosition = source.transform.localPosition;
                go.transform.localRotation = source.transform.localRotation;
                go.transform.localScale = source.transform.localScale;
            }
            var result = go.AddComponent<SkinnedMeshRenderer>();
            result.sharedMesh = mesh;
            result.bones = source.bones;
            result.rootBone = source.rootBone;
            result.sharedMaterials = materials;
            result.localBounds = source.localBounds;
            result.updateWhenOffscreen = true;
            result.quality = source.quality;
            result.enabled = false;
            return result;
        }

        private static void CreateAsset(Object asset, string path, Dictionary<string, string> owned)
        {
            RequireAbsent(path);
            try { AssetDatabase.CreateAsset(asset, path); }
            finally { RememberCreated(path, owned); }
            if (!EditorUtility.IsPersistent(asset) || !owned.ContainsKey(path))
                throw new InvalidOperationException("Asset save failed: " + path);
            AssetDatabase.SaveAssetIfDirty(asset);
        }

        private static void RememberCreated(string path, Dictionary<string, string> owned)
        {
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (!string.IsNullOrEmpty(guid)) owned[path] = guid;
        }

        private static void RollBack(Dictionary<string, string> owned, string folderGuid)
        {
            // GUID를 개별 확인하며 무관한 내용이 추가된 폴더를 재귀 삭제하지 않는다.
            foreach (var asset in owned.Reverse())
            {
                try
                {
                    if (AssetDatabase.AssetPathToGUID(asset.Key) == asset.Value && !AssetDatabase.DeleteAsset(asset.Key))
                        Debug.LogError("[Task062 Sequential] Could not roll back own asset: " + asset.Key);
                }
                catch (Exception exception) { Debug.LogError("[Task062 Sequential] Rollback failed: " + asset.Key + " " + exception.Message); }
            }
            try
            {
                if (!string.IsNullOrEmpty(folderGuid) && AssetDatabase.AssetPathToGUID(Output) == folderGuid &&
                    Directory.Exists(Output) && Directory.GetFileSystemEntries(Output).Length == 0 && !AssetDatabase.DeleteAsset(Output))
                    Debug.LogError("[Task062 Sequential] Could not remove own empty output folder.");
            }
            catch (Exception exception) { Debug.LogError("[Task062 Sequential] Folder rollback failed: " + exception.Message); }
        }
    }
}
#endif
