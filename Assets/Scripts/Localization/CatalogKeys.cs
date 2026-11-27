using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CurtainCall.Localization
{
    public sealed class CatalogKeys
    {
        public const string CurrentValue = "COMMON_VALUE_CURRENT";
        internal static readonly Regex Reference = new Regex(@"\{(?<name>[\w.· -]+)(?::(?<particle>[가-힣]+))?\}(?:\((?<case>을/를|이/가|은/는|과/와|으로/로|이다/다)\))?");
        const string Segment = "[A-Z]+(?:-[A-Z]+)*";
        static readonly Regex Qualified = new Regex(@"\A(?<base>" + Segment + "(?:_" + Segment + @"){2,3})(?:_(?<variant>0|[1-9][0-9]*))?\z");
        internal static readonly Regex PreviousKey = new Regex(@"\A[\p{L}\p{Nd}]+(?:[ ·][\p{L}\p{Nd}]+)*\.[\p{L}\p{Nd}_]+\.[\p{L}\p{Nd}_]+\z");
        readonly Dictionary<string, string> aliases, arguments, codes;
        readonly Dictionary<string, string> previousArguments = new Dictionary<string, string>(StringComparer.Ordinal);

        public CatalogKeys(string aliasesText, string argumentsText)
        {
            aliases = Map(aliasesText);
            arguments = Map(argumentsText);
            codes = arguments.ToDictionary(p => p.Value, p => p.Key, StringComparer.Ordinal);
            foreach (var pair in aliases)
                if (codes.ContainsKey(pair.Value) && PreviousKey.IsMatch(pair.Key)) previousArguments[pair.Value] = pair.Key;
        }

        static Dictionary<string, string> Map(string text)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            using var reader = new StringReader(text);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                var pair = line.Split('\t');
                if (pair.Length != 2 || !Qualified.IsMatch(pair[1]) || !map.TryAdd(pair[0], pair[1]))
                    throw new InvalidDataException("문구 키 연결표를 확인하세요. " + line);
            }
            return map;
        }

        public string Resolve(string key) => key != null && aliases.TryGetValue(key, out var next) ? next : key;
        public bool TryArgument(string key, out string argument) => codes.TryGetValue(key, out argument);
        internal string ArgumentKey(string name) => arguments.TryGetValue(name, out var key) ? key : name;
        internal string PreviousArgument(string key) => previousArguments.TryGetValue(key, out var old) ? old : key;
        internal bool IsArgument(string name) => name == CurrentValue || codes.ContainsKey(name);
        internal static bool ValidKey(string key) => Qualified.IsMatch(key);

        internal string ConvertSlots(string text, bool toSheet)
        {
            return Reference.Replace(text, m =>
            {
                string name = m.Groups["name"].Value;
                string particle = Particle(m);
                if (toSheet)
                {
                    name = Resolve(ArgumentKey(name));
                    return "{" + name + "}" + (particle.Length == 0 ? "" : "(" + ParticlePair(particle) + ")");
                }
                if (!codes.TryGetValue(name, out string slot)) return m.Value;
                return "{" + slot + (particle.Length == 0 ? "" : ":" + particle) + "}";
            });
        }

        internal static string Particle(Match match) => match.Groups["case"].Success
            ? match.Groups["case"].Value.Split('/')[0] : match.Groups["particle"].Value;

        static string ParticlePair(string particle) => particle switch {
            "을" or "를" => "을/를", "이" or "가" => "이/가", "은" or "는" => "은/는",
            "과" or "와" => "과/와", "으로" or "로" => "으로/로", "이다" or "다" => "이다/다",
            _ => throw new InvalidDataException("알 수 없는 조사입니다. " + particle)
        };

        internal string MigratePreviousReferences(string text) =>
            text.IndexOf('.') < 0 || text.IndexOf('{') < 0 ? text : Reference.Replace(text, m =>
                aliases.ContainsKey(m.Groups["name"].Value) ? ConvertSlots(m.Value, true) : m.Value);

        internal static void CheckOwner(string section, string key)
        {
            if (!ValidKey(key) || !key.StartsWith(section + "_", StringComparison.Ordinal))
                throw new InvalidDataException($"‘{section}’ 탭의 키는 {section}_소분류_항목 형식으로 대문자 영문을 씁니다. {key}");
        }

        internal static void CheckVariants(IEnumerable<string> keys)
        {
            var all = new HashSet<string>(keys, StringComparer.Ordinal);
            foreach (var group in all.Select(k => Qualified.Match(k)).Where(m => m.Groups["variant"].Success)
                .GroupBy(m => m.Groups["base"].Value))
            {
                int count = group.Count();
                if (count < 2 || all.Contains(group.Key) || Enumerable.Range(0, count).Any(i =>
                    !all.Contains(group.Key + "_" + i.ToString(System.Globalization.CultureInfo.InvariantCulture))))
                    throw new InvalidDataException("같은 항목의 변형에는 _0부터 빠짐없이 번호를 붙이세요. " + group.Key);
            }
        }

        internal void CheckReferences(IReadOnlyDictionary<string,string[]> rows, IEnumerable<string> roots)
        {
            // Check each language independently. A changed reference may pass through several unchanged rows.
            for (int column = 1; column <= 3; column++)
            {
                var done = new HashSet<string>(StringComparer.Ordinal);
                var path = new HashSet<string>(StringComparer.Ordinal);
                void Visit(string key)
                {
                    if (done.Contains(key)) return;
                    if (!path.Add(key)) throw new InvalidDataException("문구가 서로를 다시 참조합니다. " + key);
                    foreach (Match match in Reference.Matches(rows[key][column]))
                    {
                        string target = match.Groups["name"].Value;
                        if (!ValidKey(target) || match.Groups["particle"].Success)
                            throw new InvalidDataException($"중괄호 안에는 대문자 영문 전체 키를 넣고 조사는 밖에 적으세요. {key} → {match.Value}");
                        if (!rows.ContainsKey(target)) throw new InvalidDataException($"참조한 키를 찾을 수 없습니다. {key} → {target}");
                        if (target == CurrentValue && !codes.ContainsKey(key))
                            throw new InvalidDataException($"현재값은 게임이 값을 전달하는 행에서 쓸 수 있습니다. {key}");
                        Visit(target);
                    }
                    path.Remove(key);
                    done.Add(key);
                }
                foreach (string key in roots) Visit(key);
            }
        }
    }
}
