using System;
using System.IO;

namespace CurtainCall.Telemetry.Pipeline
{
    [Serializable]
    public sealed class TelemetryConfiguration
    {
        public const string FileName = "curtaincall-telemetry.json";
        public string endpoint;
        public string token;
        public bool IsValid => Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) && uri.Scheme == "https"
            && uri.Host == "script.google.com" && uri.AbsolutePath.StartsWith("/macros/s/", StringComparison.Ordinal)
            && uri.AbsolutePath.EndsWith("/exec", StringComparison.Ordinal) && string.IsNullOrEmpty(uri.Query)
            && token != null && token.Length >= 32;
        public static TelemetryConfiguration FromEnvironment()
        {
            var value = new TelemetryConfiguration
            {
                endpoint = Environment.GetEnvironmentVariable("CURTAINCALL_TELEMETRY_URL"),
                token = Environment.GetEnvironmentVariable("CURTAINCALL_TELEMETRY_TOKEN")
            };
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            value.endpoint ??= Environment.GetEnvironmentVariable("CURTAINCALL_TELEMETRY_URL", EnvironmentVariableTarget.User);
            value.token ??= Environment.GetEnvironmentVariable("CURTAINCALL_TELEMETRY_TOKEN", EnvironmentVariableTarget.User);
#endif
            return value;
        }
        public static TelemetryConfiguration FromFile(string directory)
        {
            string path = Path.Combine(directory, FileName);
            return File.Exists(path) ? LogJson.Parse<TelemetryConfiguration>(File.ReadAllText(path)) : null;
        }
    }
}
