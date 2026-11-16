using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static class EscapePursuerHeavyGaitBuilder
    {
        public const string Folder = "Assets/Resources/Animations/Enemies/CircusGolemHeavyGait/";
        public const string Audio = "Assets/Resources/Audio/Enemies/CircusGolemHeavyGait/";

        public static void Configure(EscapePursuerDoll doll)
        {
            var run = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "Golem_RunHeavy.anim");
            if (!run) return; // The original importer can run before the derived assets exist.
            if (doll.revision != 7) throw new InvalidOperationException("Heavy gait requires the Revision7 rig.");
            var manifest = AssetDatabase.LoadAssetAtPath<TextAsset>(Folder + "animation-manifest-run.json");
            if (!manifest) throw new InvalidOperationException("Heavy gait distance manifest is missing.");
            doll.clips[(int)EscapePursuerDoll.Motion.Run] = run;
            doll.runCycleDistance = (float)JObject.Parse(manifest.text)["clips"]["Golem_Run"]["cycle_distance_m"];
            var walk = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "Golem_WalkWeighted.anim");
            var gaitManifest = AssetDatabase.LoadAssetAtPath<TextAsset>(Folder + "animation-manifest-gait.json");
            if (walk && gaitManifest)
            {
                var gait = JObject.Parse(gaitManifest.text)["clips"];
                doll.clips[(int)EscapePursuerDoll.Motion.Walk] = walk;
                doll.walkCycleDistance = (float)gait["Golem_Walk"]["cycle_distance_m"];
                doll.runCycleDistance = (float)gait["Golem_Run"]["cycle_distance_m"];
            }
            EditorUtility.SetDirty(doll);
            if (!doll.chase) return;
            var actor = new SerializedObject(doll.chase);
            AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(Audio + name + ".wav")
                ?? throw new InvalidOperationException("Missing heavy sound: " + name);
            var source = (AudioSource)actor.FindProperty("footstep").objectReferenceValue;
            source.clip = Clip("HeavyFootfallL"); source.pitch = 1; source.dopplerLevel = 0;
            source.playOnAwake = false; source.loop = false;
            actor.FindProperty("alternateFootstep").objectReferenceValue = Clip("HeavyFootfallR");
            actor.FindProperty("bodyImpact").objectReferenceValue = Clip("HeavyBodyImpact");
            actor.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(source);
            if (PrefabUtility.IsPartOfPrefabInstance(source)) PrefabUtility.RecordPrefabInstancePropertyModifications(source);
            if (PrefabUtility.IsPartOfPrefabInstance(doll)) PrefabUtility.RecordPrefabInstancePropertyModifications(doll);
            if (PrefabUtility.IsPartOfPrefabInstance(doll.chase)) PrefabUtility.RecordPrefabInstancePropertyModifications(doll.chase);
        }

        [MenuItem("Tools/CurtainCall/탈출로/무거운 질주와 발소리 연결")]
        public static void Apply()
        {
            if (Application.isPlaying) throw new InvalidOperationException("편집 모드에서 실행하세요.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("미저장 씬이 있습니다.");
            if (!AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "Golem_RunHeavy.anim"))
                throw new InvalidOperationException("먼저 무거운 질주 에셋을 임포트하세요.");
            var prefab = PrefabUtility.LoadPrefabContents(EscapePursuerRevision7Builder.Prefab);
            try
            {
                foreach (var doll in prefab.GetComponentsInChildren<EscapePursuerDoll>(true)) Configure(doll);
                PrefabUtility.SaveAsPrefabAsset(prefab, EscapePursuerRevision7Builder.Prefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            foreach (string path in new[] { EscapeBackgroundBuilder.LabyrinthScenePath, EscapeMppmReviewConfig.PursuerScene }
                .Where(path => AssetDatabase.LoadAssetAtPath<SceneAsset>(path)))
            {
                var scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.IsValid() || !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    foreach (var doll in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EscapePursuerDoll>(true)))
                        Configure(doll);
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
            AssetDatabase.SaveAssets();
        }
    }
}
