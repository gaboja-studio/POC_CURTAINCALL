using System.IO;
using UnityEditor.Build;
using UnityEngine;

namespace CurtainCall.Telemetry.Pipeline.Editor
{
    /// <summary>Pack the builder's configuration without creating a credential asset in Assets/Git.</summary>
    public sealed class TelemetryBuildProcessor : BuildPlayerProcessor
    {
        public override void PrepareForBuild(BuildPlayerContext context)
        {
            var tags = PlaytestBuildTags.Current();
            tags.Collection.Validate();
            if (tags.Has(PlaytestBuildTags.Logs) && !tags.AllowsLogs)
                throw new BuildFailedException("로그 태그가 켜져 있지만 현재 버전이 수집 대상이 아닙니다. 로그·설문 설정을 확인하세요.");
            if (tags.Has(PlaytestBuildTags.Surveys) && !tags.AllowsSurveys(Application.version))
                throw new BuildFailedException("현재 버전에서 실행할 설문이 없습니다. 설문 태그를 끄거나 로그·설문 설정의 대상 버전을 확인하세요.");
            string directory = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Library", "CurtainCallTelemetryBuild");
            Directory.CreateDirectory(directory);
            string manifest = Path.Combine(directory, PlaytestBuildTags.FileName);
            File.WriteAllText(manifest, JsonUtility.ToJson(tags));
            context.AddAdditionalPathToStreamingAssets(manifest, PlaytestBuildTags.FileName);
            if (!tags.AllowsLogs) return;
            var configuration = TelemetryConfiguration.FromEnvironment();
            if (!configuration.IsValid)
                throw new BuildFailedException("로그 전송 설정이 없습니다. 빌드 담당 PC의 로그 수신 주소·토큰을 먼저 설정해 주세요.");
            string file = Path.Combine(directory, TelemetryConfiguration.FileName);
            File.WriteAllText(file, LogJson.Serialize(configuration));
            context.AddAdditionalPathToStreamingAssets(file, TelemetryConfiguration.FileName);
        }
    }
}
