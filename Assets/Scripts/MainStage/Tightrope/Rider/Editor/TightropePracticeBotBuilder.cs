using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace CurtainCall.Tightrope.EditorTools
{
    public static class TightropePracticeBotBuilder
    {
        const string Scene = "Assets/Scenes/Tests/MainStage/Tightrope/Tightrope.unity";

        [MenuItem("CurtainCall/Tightrope/Build Local Practice Bot")]
        static void BuildFromMenu() => Build();

        public static string Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new System.InvalidOperationException("Stop Play Mode before building the practice bot.");
            var scenes = EditorBuildSettings.scenes.ToList();
            var existing = scenes.Find(s => s.path == Scene);
            if (existing == null) scenes.Add(new EditorBuildSettingsScene(Scene, true));
            else existing.enabled = true;
            EditorBuildSettings.scenes = scenes.ToArray();
            string path = TightropePracticeBot.ExecutablePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { Scene }, locationPathName = path,
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.InvalidOperationException($"Practice bot build failed: {report.summary.result}, errors={report.summary.totalErrors}");
            return path;
        }
    }
}
