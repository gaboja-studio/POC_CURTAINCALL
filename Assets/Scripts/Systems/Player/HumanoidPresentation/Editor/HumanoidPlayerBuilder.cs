#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using CurtainCall.Player;
using CurtainCall.Player.Validation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.HumanoidPresentationEditor
{
    public static class HumanoidPlayerBuilder
    {
        public const string Folder = "Assets/_ThirdParty/Task062/PlayerPresentation";
        public const string ModelPath = Folder + "/Humanoid.prefab";
        public const string PlayerPath = "Assets/Resources/Prefabs/Characters/Players/Player.prefab";

        [MenuItem("CurtainCall/Player/Build Task062 humanoid player")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating
                || PrefabStageUtility.GetCurrentPrefabStage() != null) throw new InvalidOperationException("유휴 Editor가 필요합니다.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("미저장 씬 보존: 중단");
            if (AssetDatabase.IsValidFolder(Folder) || Directory.Exists(Folder) || File.Exists(Folder + ".meta"))
                throw new InvalidOperationException("기존 게임용 출력 보존: " + Folder);
            AssetDatabase.CreateFolder("Assets/_ThirdParty/Task062", "PlayerPresentation");
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_ThirdParty/Task062/SequentialValidation/Validation.prefab");
                var wrapper = (GameObject)PrefabUtility.InstantiatePrefab(source, preview);
                PrefabUtility.UnpackPrefabInstance(wrapper, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                wrapper.name = "Humanoid Body Type 1";
                var validation = wrapper.GetComponent<SequentialCutValidation>();
                var view = wrapper.AddComponent<HumanoidModel>();
                view.Animator = validation.Animator;
                view.Original = validation.Original;
                view.Bodies = validation.Bodies;
                view.Limbs = validation.Limbs;
                view.CrawlHeightOffset = -.05f;
                wrapper.AddComponent<BoxCollider>().enabled = false;
                Object.DestroyImmediate(validation);
                Object.DestroyImmediate(wrapper.GetComponent<PlayerCondition>());
                var normal = (AnimatorController)view.Animator.runtimeAnimatorController;
                var controller = AnimatorController.CreateAnimatorControllerAtPath(Folder + "/Player.controller");
                foreach (string name in new[] { "Idle", "Walk", "Run", "Crouch", "CrouchWalk" })
                {
                    var motion = normal.layers[0].stateMachine.states.Single(s => s.state.name == name).state.motion as AnimationClip;
                    Add(controller, name, motion, true);
                }
                string standard = "Assets/_ThirdParty/Task062/QuaterniusStandard/UAL1_Standard.fbx";
                var clips = AssetDatabase.LoadAllAssetsAtPath(standard).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
                Add(controller, "JumpStart", Find(clips, "Jump_Start", "JumpStart"), false);
                Add(controller, "JumpLoop", Find(clips, "Jump_Loop", "JumpLoop"), true);
                Add(controller, "JumpLand", Find(clips, "Jump_Land", "JumpLand"), false);
                Add(controller, "Death", Find(clips, "Death01", "Death_01"), false);
                Add(controller, "Crawl", UserClip("army_crawl"), true);
                Add(controller, "Tightrope", UserClip("tightrope_walk"), true);
                Add(controller, "Flying", UserClip("flying"), false);
                Add(controller, "Fall", UserClip("fall"), true);
                controller.layers[0].stateMachine.defaultState = controller.layers[0].stateMachine.states.Single(s => s.state.name == "Idle").state;
                var mask = new AvatarMask { name = "운반 상체" };
                for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
                foreach (var part in new[] { AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head, AvatarMaskBodyPart.LeftArm,
                    AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers }) mask.SetHumanoidBodyPartActive(part, true);
                AssetDatabase.CreateAsset(mask, Folder + "/Carry.mask");
                controller.AddLayer("Carry");
                var layers = controller.layers;
                layers[1].avatarMask = mask;
                layers[1].defaultWeight = 0;
                layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
                controller.layers = layers;
                var carry = controller.layers[1].stateMachine.AddState("Carrying");
                carry.motion = CopyClip("Carrying", UserClip("carrying"), true);
                view.Animator.runtimeAnimatorController = controller;
                view.Animator.applyRootMotion = false;
                view.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                view.Original.enabled = true;
                foreach (var skin in view.Bodies.Concat(view.Limbs)) skin.enabled = false;
                var model = PrefabUtility.SaveAsPrefabAsset(wrapper, ModelPath);
                if (model == null) throw new InvalidOperationException("게임용 모델 저장 실패");
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
                var player = PrefabUtility.LoadPrefabContents(PlayerPath);
                try
                {
                    var slot = new SerializedObject(player.GetComponent<PlayerModelSlot>());
                    slot.FindProperty("modelPrefab").objectReferenceValue = model;
                    slot.FindProperty("squashWhenCrouched").boolValue = false;
                    slot.ApplyModifiedPropertiesWithoutUndo();
                    if (player.GetComponent<HumanoidPlayerPresentation>() == null) player.AddComponent<HumanoidPlayerPresentation>();
                    PrefabUtility.SaveAsPrefabAsset(player, PlayerPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(player); }
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        static AnimationClip UserClip(string name)
        {
            string path = "Assets/_ThirdParty/Task062/Mixamo/" + name + ".fbx";
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single(c => !c.name.StartsWith("__preview__"));
        }
        static AnimationClip Find(AnimationClip[] clips, params string[] names)
        {
            var result = clips.SingleOrDefault(c => names.Any(n => c.name.Replace("-", "_").EndsWith(n, StringComparison.OrdinalIgnoreCase)));
            if (result == null) throw new InvalidOperationException("클립 없음: " + string.Join("/", names) + " | " + string.Join(",", clips.Select(c => c.name)));
            return result;
        }
        static AnimationClip CopyClip(string name, AnimationClip source, bool loop)
        {
            if (source == null || !source.humanMotion) throw new InvalidOperationException("유효 Humanoid 필요: " + name);
            var clip = Object.Instantiate(source);
            clip.name = name;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, Folder + "/" + name + ".anim");
            return clip;
        }
        static void Add(AnimatorController controller, string name, AnimationClip source, bool loop)
        {
            controller.layers[0].stateMachine.AddState(name).motion = CopyClip(name, source, loop);
        }
    }
}
#endif
