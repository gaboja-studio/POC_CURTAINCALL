using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CurtainCall.Telemetry.Pipeline.Editor
{
    /// <summary>
    /// 빌드 도우미(build-helper.vbs)가 Unity를 batchmode로 실행해 부르는 진입점.
    /// 버전·로그/설문 태그를 이 PC 설정에 반영하고, 개발자(Development + Dev Mode 고정) 또는 라이브 Windows 빌드를 만든다.
    /// 설문 설정 파일은 도우미가 먼저 저장하고, 검증은 기존 TelemetryBuildProcessor가 빌드 시작 때 한다.
    /// </summary>
    public static class PlaytestBuildRunner
    {
        public const string DevModeDefine = "CURTAINCALL_DEVMODE_BUILD";

        [Serializable] sealed class Result { public bool success; public string message, output, version, kind; public bool logs, surveys; }

        /// <summary>인자: -ccVersion 0.2.2 -ccLogs true -ccSurveys false -ccKind Dev|Live -ccOutput &lt;exe 경로&gt; -ccResult &lt;json 경로&gt;</summary>
        public static void Build()
        {
            var result = new Result();
            string resultPath = Arg("-ccResult");
            int code = 1;
            try
            {
                string version = Arg("-ccVersion")?.Trim();
                if (version == null || !Regex.IsMatch(version, @"^[0-9]+\.[0-9]+\.[0-9]+$"))
                    throw new ArgumentException("버전은 0.2.2 같은 숫자 3개 형식이어야 합니다.");
                string kind = Arg("-ccKind");
                if (kind != "Dev" && kind != "Live") throw new ArgumentException("빌드 종류는 Dev 또는 Live여야 합니다.");
                string output = Arg("-ccOutput");
                if (string.IsNullOrWhiteSpace(output) || !output.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("저장할 .exe 경로가 필요합니다.");
                bool logs = Arg("-ccLogs") == "true", surveys = Arg("-ccSurveys") == "true";
                result.version = version; result.kind = kind; result.logs = logs; result.surveys = surveys; result.output = output;

                PlayerSettings.bundleVersion = version;
                AssetDatabase.SaveAssets();
                EditorPrefs.SetBool(PlaytestBuildTags.PreferenceKey(PlaytestBuildTags.Logs), logs);
                EditorPrefs.SetBool(PlaytestBuildTags.PreferenceKey(PlaytestBuildTags.Surveys), surveys);

                string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
                if (scenes.Length == 0) throw new InvalidOperationException("Build Settings에 켜진 씬이 없습니다.");
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));

                bool dev = kind == "Dev";
                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = dev ? BuildOptions.Development : BuildOptions.None,
                    extraScriptingDefines = dev ? new[] { DevModeDefine } : Array.Empty<string>()
                };
                Debug.Log($"[BuildHelper] {version} / {(dev ? "개발자(TEST)" : "라이브(LIVE)")} / 로그 {(logs ? "켬" : "끔")} / 설문 {(surveys ? "켬" : "끔")} → {output}");
                BuildReport report = BuildPipeline.BuildPlayer(options);
                result.success = report.summary.result == BuildResult.Succeeded;
                result.message = result.success
                    ? $"빌드 완료 ({report.summary.totalTime:hh\\:mm\\:ss}, {report.summary.totalSize / (1024 * 1024)} MB)"
                    : $"빌드 실패: {report.summary.result}, 오류 {report.summary.totalErrors}개. 로그 파일을 확인하세요.";
                code = result.success ? 0 : 1;
            }
            catch (Exception error)
            {
                result.success = false;
                result.message = error.Message;
                Debug.LogError("[BuildHelper] " + error);
            }
            if (!string.IsNullOrEmpty(resultPath))
            {
                try { File.WriteAllText(resultPath, JsonUtility.ToJson(result, true)); }
                catch (Exception error) { Debug.LogError("[BuildHelper] 결과 파일 저장 실패: " + error.Message); }
            }
            EditorApplication.Exit(code);
        }

        static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
