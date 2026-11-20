using System;
using System.IO;

namespace CurtainCall.Telemetry.Pipeline
{
    public static class SurveyLaunchPolicy
    {
        public const string Night = "NIGHT1", Exit = "EXIT";
        public const string NightForm = "https://docs.google.com/forms/d/e/1FAIpQLSf7S8t3P0098MWYzE-Xf6uaDuffXMAn_qErGyE8DnnTFiElIA/viewform";
        public const string ExitForm = "https://docs.google.com/forms/d/e/1FAIpQLSdK2_FooH6fIpRj_8etZ_8eMfZYTBLMqTJEL3gNs9L19rXMvA/viewform";
        // Set only from the owner's prefilled links; never guess a Google Forms entry ID.
        public const string NightEntry = "entry.280071335", ExitEntry = "entry.809038892";
        public static bool Configured => ValidEntry(NightEntry) && ValidEntry(ExitEntry);
        public static bool ValidEntry(string entry) => !string.IsNullOrEmpty(entry)
            && System.Text.RegularExpressions.Regex.IsMatch(entry, "^entry\\.[0-9]+$");
        public static string Code(string kind, string environment, string runId, int slot, string invitationId, DateTime openedAt)
        {
            if (kind != Night && kind != Exit || environment != "TEST" && environment != "LIVE"
                || !Guid.TryParseExact(runId, "D", out var run) || slot < 0 || slot > 3
                || !Guid.TryParseExact(invitationId, "N", out _)) throw new ArgumentException("Invalid survey context.");
            return string.Join(".", "CCS1", "0_1_0", environment, kind, run.ToString("N"), slot,
                invitationId, new DateTimeOffset(openedAt.ToUniversalTime()).ToUnixTimeSeconds());
        }
        public static string Url(string kind, string entry, string code)
        {
            if (!ValidEntry(entry) || kind != Night && kind != Exit) throw new ArgumentException("Survey entry not configured.");
            return (kind == Night ? NightForm : ExitForm) + "?usp=pp_url&" + entry + "=" + Uri.EscapeDataString(code);
        }
        public static string Code(SurveyDefinition survey, string version, string kind, string environment, string runId, int slot, string invitationId, DateTime openedAt)
        {
            string legacy = Code(kind, environment, runId, slot, invitationId, openedAt);
            if (!survey.Matches(version)) throw new ArgumentException("Survey is not enabled for this version.");
            if (version == "0.1.0" && survey.Legacy(kind)) return legacy;
            var parts = legacy.Split('.'); parts[0] = "CCS2"; parts[1] = Uri.EscapeDataString(version.Replace('.', '_'));
            return string.Join(".", parts) + "." + survey.id + "." + survey.round;
        }
        public static string Url(SurveyDefinition survey, string code)
        {
            if (!survey.Valid) throw new ArgumentException("Survey not configured.");
            return survey.formUrl + "?usp=pp_url&" + survey.entry + "=" + Uri.EscapeDataString(code);
        }
    }

    /// <summary>One local launch per installation, exact game version, survey kind and round.</summary>
    public sealed class SurveyLaunchCache
    {
        public const string LocalIdentity = "installation";
        readonly string root;
        public SurveyLaunchCache(string directory) => root = directory;
        /// <summary>이 플레이어의 설문 열림 표시만 지운다. 로그·하위 폴더·링크는 건드리지 않는다.</summary>
        public int ClearHistory()
        {
            if (!Directory.Exists(root) || (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) return 0;
            int count = 0;
            foreach (string file in Directory.GetFiles(root, "*.opened", SearchOption.TopDirectoryOnly))
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(file),
                    "^(?:0\\.1\\.0-(NIGHT1|EXIT)|campaign)-[0-9a-f]{64}\\.opened$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                    || (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) continue;
                File.Delete(file); count++;
            }
            return count;
        }
        string Marker(string identity, string kind)
        {
            if (kind != SurveyLaunchPolicy.Night && kind != SurveyLaunchPolicy.Exit || string.IsNullOrEmpty(identity))
                throw new ArgumentException("Invalid survey cache identity.");
            return Path.Combine(root, "0.1.0-" + kind + "-" + DurableOutbox.Sha256(identity) + ".opened");
        }
        public bool WasOpened(string identity, string kind)
        {
            if (File.Exists(Marker(identity, kind))) return true;
            // Respect markers from older builds that keyed the same PC by an anonymous account.
            return identity == LocalIdentity && Directory.Exists(root)
                && Directory.GetFiles(root, "0.1.0-" + kind + "-*.opened").Length > 0;
        }
        public bool TryOpen(string identity, string kind, string entry, string code, Action<string> open)
        {
            if (WasOpened(identity, kind)) return false;
            return Claim(Marker(identity, kind), SurveyLaunchPolicy.Url(kind, entry, code), code, open);
        }
        string CampaignMarker(string identity, string kind, SurveyDefinition survey, string version = null)
        {
            if (!survey.Valid || string.IsNullOrEmpty(identity)) throw new ArgumentException("Invalid survey cache identity.");
            string key = survey.id + ":" + survey.round + ":" + kind + ":" + identity;
            if (version != null) key = version + ":" + key;
            return Path.Combine(root, "campaign-" + DurableOutbox.Sha256(key) + ".opened");
        }
        public bool WasOpened(string identity, string kind, SurveyDefinition survey, string version)
        {
            if (!survey.Matches(version)) return false;
            string path = CampaignMarker(identity, kind, survey, version);
            if (File.Exists(path)) return true;
            if (!Directory.Exists(root)) return false;
            // Earlier builds stored the exact version in the invitation inside a shared marker.
            var oldPaths = !survey.Legacy(kind) ? new[] { CampaignMarker(identity, kind, survey) }
                : identity == LocalIdentity ? Directory.GetFiles(root, "0.1.0-" + kind + "-*.opened")
                : new[] { Marker(identity, kind) };
            string prefix = "CCS2." + Uri.EscapeDataString(version.Replace('.', '_')) + ".";
            foreach (string oldPath in oldPaths)
            {
                if (!File.Exists(oldPath)) continue;
                string code = File.ReadAllText(oldPath);
                if (!code.StartsWith(prefix, StringComparison.Ordinal)
                    && !(version == "0.1.0" && code.StartsWith("CCS1.0_1_0.", StringComparison.Ordinal))) continue;
                Claim(path, null, code, _ => { });
                return true;
            }
            return false;
        }
        public bool TryOpen(string identity, string kind, SurveyDefinition survey, string version, string code, Action<string> open)
        {
            if (!survey.Matches(version) || WasOpened(identity, kind, survey, version)) return false;
            return Claim(CampaignMarker(identity, kind, survey, version), SurveyLaunchPolicy.Url(survey, code), code, open);
        }
        bool Claim(string path, string url, string code, Action<string> open)
        {
            Directory.CreateDirectory(root);
            // CreateNew makes simultaneous callbacks/processes claim the same marker at most once.
            FileStream stream;
            try { stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); }
            catch (IOException) when (File.Exists(path)) { return false; }
            try { using (stream) { using var writer = new StreamWriter(stream); writer.Write(code); } }
            catch { File.Delete(path); throw; }
            try { open(url); return true; }
            catch { File.Delete(path); throw; }
        }
    }
}
