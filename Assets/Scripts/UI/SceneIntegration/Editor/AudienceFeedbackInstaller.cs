#if UNITY_EDITOR
using System;
using System.Linq;
using CurtainCall.Settings;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration.Editor
{
    /// <summary>Restore the original C10 visuals while retaining the new local audience sound bed and reactions.</summary>
    public static class AudienceFeedbackInstaller
    {
        const string AudioFolder = "Assets/_ThirdParty/AudienceReactionsTask144";
        static AudioClip Source(string name)
        {
            string path = AudioFolder + "/" + name + "-source.mp3";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad; settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.preloadAudioData = true; importer.defaultSampleSettings = settings; importer.SaveAndReimport();
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path); clip.LoadAudioData(); return clip;
        }
        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            var cheers = Enumerable.Range(1, 4).Select(n=>AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "/cheer-" + n + ".wav")).ToArray();
            var boos = Enumerable.Range(1, 4).Select(n=>AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "/boo-" + n + ".wav")).ToArray();
            if (cheers.Any(c=>c==null) || boos.Any(c=>c==null)) throw new InvalidOperationException("Reviewed v3 reaction clips are required.");
            var murmur = AudienceAmbienceBaker.Build(Source("murmur"), AudioFolder + "/murmur-loop.wav");
            var root = PrefabUtility.LoadPrefabContents(PerformanceFeedbackInstaller.PrefabPath);
            try
            {
                var adapter = root.GetComponentInChildren<PerformanceFeedback>(true);
                if (adapter == null || adapter.View == null) throw new InvalidOperationException("Original C10 view must be preserved.");
                adapter.View.ResetForRuntime();
                var rejected = root.GetComponentInChildren<NumericFeedbackView>(true);
                if (rejected != null) UnityEngine.Object.DestroyImmediate(rejected.gameObject);
                var audio = root.GetComponentInChildren<AudienceReactionAudio>(true);
                if (audio == null)
                {
                    var host = new GameObject("AudienceAudio"); host.transform.SetParent(root.transform, false);
                    audio = host.AddComponent<AudienceReactionAudio>();
                }
                AudioSource Voice(string name, bool loop)
                {
                    var child = audio.transform.Find(name);
                    if (child == null) { child = new GameObject(name).transform; child.SetParent(audio.transform, false); }
                    var source = child.GetComponent<AudioSource>();
                    if (source == null) source = child.gameObject.AddComponent<AudioSource>();
                    source.playOnAwake = false; source.loop = loop; return source;
                }
                audio.Configure(cheers, boos, Voice("CheerVoice", false), Voice("BooVoice", false));
                audio.ConfigureAmbience(Voice("AudienceMurmur", true), murmur);
                adapter.ConfigureNumbers(null, audio);
                PrefabUtility.SaveAsPrefabAsset(root, PerformanceFeedbackInstaller.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            EditorUtility.SetDirty(GameSettings.Base); AssetDatabase.SaveAssetIfDirty(GameSettings.Base);
            return "Original C10 restored; continuous 30s audience murmur plus four cheer/four boo one-shots.";
        }
        public static string BakeHeavyReactions()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            AudienceRecordingBaker.Build(Source("applause"), AudioFolder, "cheer", new[] { .4f, 9.2f, 21f, 33f }, false);
            AudienceRecordingBaker.Build(Source("boo"), AudioFolder, "boo", new[] { .65f, 8.05f, 16.85f, 24.6f }, true);
            return "Eight heavier human reaction takes baked; existing clip GUIDs retained.";
        }
    }
}
#endif
