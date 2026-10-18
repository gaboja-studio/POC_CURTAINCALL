using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace CurtainCall.HumanCannon.EditorTools
{
    public static class HumanCannonBotBuilder
    {
        [MenuItem("CurtainCall/Human Cannon/Build Local Practice Bot")]
        public static string Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("Stop Play Mode before preparing the bot.");
            string path = HumanCannonPracticeBot.ExecutablePath;
            EnsureSceneRegistered();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity" },
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.InvalidOperationException($"Bot build failed: {report.summary.result}, errors={report.summary.totalErrors}");
            return path;
        }

        public static void EnsureSceneRegistered()
        {
            const string scene = "Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity";
            var scenes = EditorBuildSettings.scenes.ToList();
            var existing = scenes.Find(s => s.path == scene);
            if (existing != null && existing.enabled) return;
            if (existing != null) existing.enabled = true;
            else scenes.Add(new EditorBuildSettingsScene(scene, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
