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
            var configuration = TelemetryConfiguration.FromEnvironment();
            if (!configuration.IsValid)
                throw new BuildFailedException("로그 전송 설정이 없습니다. 빌드 담당 PC의 로그 수신 주소·토큰을 먼저 설정해 주세요.");
            string directory = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Library", "CurtainCallTelemetryBuild");
            Directory.CreateDirectory(directory);
            string file = Path.Combine(directory, TelemetryConfiguration.FileName);
            File.WriteAllText(file, LogJson.Serialize(configuration));
            context.AddAdditionalPathToStreamingAssets(file, TelemetryConfiguration.FileName);
        }
    }
}
