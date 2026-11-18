using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;

namespace CurtainCall.Sound.Editor
{
    /// <summary>
    /// 게임 스크립트(Assets/Scripts)에서 상황 키·시트 ID를 재생하는 줄을 찾는다.
    /// <c>SoundSituations.X</c>, <c>SoundCueIds.X</c> 상수와 <c>SoundManager.Play("...")</c> 글자 그대로를 센다.
    /// 정의 파일·에디터·테스트·확인 씬 스크립트와 주석은 제외한다.
    /// </summary>
    static class SoundCodeUsage
    {
        public readonly struct Hit
        {
            public readonly string Path;
            public readonly int Line;
            public readonly string Text;
            public Hit(string path, int line, string text) { Path = path; Line = line; Text = text; }
            public string Label => $"{System.IO.Path.GetFileName(Path)}:{Line}";
        }

        static readonly Regex Constant = new(@"\b(SoundSituations|SoundCueIds)\.(\w+)", RegexOptions.Compiled);
        static readonly Regex Literal = new("SoundManager\\.(?:Play|PlayAt|PlayLoop|Stop)\\(\\s*\"([^\"]+)\"", RegexOptions.Compiled);
        static readonly string[] Excluded = { "SoundSituations.cs", "SoundCueIds.cs", "SoundCuePreview.cs", "SoundManager.cs" };
        static Dictionary<string, List<Hit>> cache;

        public static IReadOnlyList<Hit> For(string keyOrId)
        {
            if (cache == null) Refresh();
            return !string.IsNullOrEmpty(keyOrId) && cache.TryGetValue(keyOrId, out var hits) ? hits : System.Array.Empty<Hit>();
        }

        public static void Refresh()
        {
            cache = new Dictionary<string, List<Hit>>(System.StringComparer.OrdinalIgnoreCase);
            var constants = new Dictionary<string, string>();
            foreach (var type in new[] { typeof(SoundSituations), typeof(SoundCueIds) })
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral && f.FieldType == typeof(string)))
                    constants[type.Name + "." + field.Name] = (string)field.GetRawConstantValue();

            foreach (var file in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
            {
                string path = file.Replace('\\', '/');
                if (path.Contains("/Editor/") || path.Contains("/Tests/") || Excluded.Contains(Path.GetFileName(path))) continue;
                string[] lines;
                try { lines = File.ReadAllLines(path); } catch (IOException) { continue; }
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (line.IndexOf("Sound", System.StringComparison.Ordinal) < 0) continue;
                    string trimmed = line.TrimStart();
                    if (trimmed.StartsWith("//")) continue;
                    foreach (Match match in Constant.Matches(line))
                        if (constants.TryGetValue(match.Groups[1].Value + "." + match.Groups[2].Value, out var value)) Add(value, path, i + 1, trimmed);
                    foreach (Match match in Literal.Matches(line)) Add(match.Groups[1].Value, path, i + 1, trimmed);
                }
            }
        }

        static void Add(string key, string path, int line, string text)
        {
            if (!cache.TryGetValue(key, out var hits)) cache[key] = hits = new List<Hit>();
            if (!hits.Any(h => h.Path == path && h.Line == line)) hits.Add(new Hit(path, line, text));
        }

        public static void Open(string path, int line)
        {
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if (script != null) AssetDatabase.OpenAsset(script, line);
        }
    }
}
