using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace CurtainCall.WaitingRoom.EditorTools
{
    /// <summary>현재 작업 사본으로 실제 4인 LAN 검사용 개발 빌드를 만든다. 원본 씬/빌드 목록은 수정하지 않는다.</summary>
    public static class RehearsalReviewBuilder
    {
        public static void PrepareNativeCannon()
        {
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-rehearsalSettingsBackup");
            if (at >= 0 && at + 1 < args.Length)
            {
                var original = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(args[at + 1]));
                foreach (var item in original)
                {
                    if (string.IsNullOrEmpty(item.Value)) EditorPrefs.DeleteKey(item.Key);
                    else EditorPrefs.SetString(item.Key, item.Value);
                }
            }
            RehearsalCannonSourceBuilder.CopySource();
        }

        public static void Build()
        {
            const string folder = ".harness/local/rehearsal/Player";
            Directory.CreateDirectory(folder);
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled
                && !s.path.EndsWith("/escape_route.unity", StringComparison.OrdinalIgnoreCase)
                && !s.path.EndsWith("/WaitingRoomPursuit.unity", StringComparison.OrdinalIgnoreCase)
                && !s.path.EndsWith("/WaitingRoomBase.unity", StringComparison.OrdinalIgnoreCase)).Select(s => s.path).ToArray();
            try
            {
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = scenes,
                    locationPathName = folder + "/RehearsalReview.exe", target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development | BuildOptions.CompressWithLz4 });
                File.WriteAllText(folder + "/result.json", Newtonsoft.Json.JsonConvert.SerializeObject(new {
                    result = report.summary.result.ToString(), errors = report.summary.totalErrors,
                    warnings = report.summary.totalWarnings, seconds = report.summary.totalTime.TotalSeconds,
                    bytes = report.summary.totalSize, scenes = scenes }, Newtonsoft.Json.Formatting.Indented));
                if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Rehearsal review build failed");
            }
            catch (Exception error)
            {
                File.WriteAllText(folder + "/failure.txt", error.ToString());
                throw;
            }
        }
    }
}
