using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CurtainCall.Localization
{
    // The source column is the immutable input from existing scenes and network messages.
    // Translators can change both display columns without breaking those inputs.
    public sealed partial class TextCatalog
    {
        sealed class Entry
        {
            public string Source, Ko, En;
            public string Value(bool korean) => korean ? Ko : En;
        }

        sealed class Template
        {
            public Entry Entry;
            public int Priority;
            public string Anchor;
            public Regex Pattern;
            public Dictionary<string, string> Groups;
        }

        static readonly Regex Slot = CatalogKeys.Reference;
        static readonly Regex RichTag = new Regex(@"</?[A-Za-z][^<>\n]*>");
        static readonly Regex Key = new Regex(@"\A[a-zA-Z0-9][a-zA-Z0-9_.:-]*\z");
        CatalogKeys schema;
        Func<string, string> readControl;
        static readonly string[] Separators = { "\n", " · ", " — " };
        readonly Dictionary<string, Entry> keys = new Dictionary<string, Entry>(StringComparer.Ordinal);
        readonly Dictionary<string, Entry> sources = new Dictionary<string, Entry>(StringComparer.Ordinal);
        readonly HashSet<Entry> renderingButtons = new HashSet<Entry>();
        readonly List<Template> templates = new List<Template>();
        public int Count => keys.Count;

        public static TextCatalog Read(string csv) => ReadFiles(new[] { csv });

        public static TextCatalog ReadSections(IReadOnlyDictionary<string,string> files, IEnumerable<string> order,
            CatalogKeys schema, Func<string,string> readControl = null)
        {
            foreach (var section in files)
                foreach (var row in TableRows(section.Value)) CatalogKeys.CheckOwner(section.Key, row.Cells[0]);
            return ReadFiles(files.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Value), order, schema, readControl);
        }

        internal bool UsesReferences(string source, bool korean) => source.IndexOf('{') >= 0 ||
            source.StartsWith("@loc:", StringComparison.Ordinal) ||
            (sources.TryGetValue(source, out var entry) && entry.Value(korean).IndexOf('{') >= 0);

        public static TextCatalog ReadFiles(IEnumerable<string> files, IEnumerable<string> templateOrder = null,
            CatalogKeys schema = null, Func<string, string> readControl = null)
        {
            var result = new TextCatalog { schema = schema, readControl = readControl };
            var graph = new Dictionary<string, string[]>(StringComparer.Ordinal);
            var priorities = new Dictionary<string, int>(StringComparer.Ordinal);
            if (templateOrder != null)
                foreach (string key in templateOrder) priorities.Add(key, priorities.Count);
            foreach (var row in files.SelectMany(csv => TableRows(csv)))
            {
                var cells = row.Cells;
                string key = cells[0];
                if (schema != null && !CatalogKeys.ValidKey(key)) throw new InvalidDataException("문구 키 형식이 잘못되었습니다. " + key);
                var entry = new Entry { Source = schema?.ConvertSlots(cells[1], false) ?? cells[1], Ko = cells[2], En = cells[3] };
                bool hasSlots = ValidateText(cells, schema);
                if (!result.keys.TryAdd(key, entry))
                    throw new InvalidDataException("여러 CSV에 같은 문구 코드가 있습니다. " + key);
                if (schema != null)
                {
                    graph.Add(key, cells);
                    if (schema.IsArgument(key)) continue;
                }
                if (result.sources.ContainsKey(entry.Source)) continue;
                result.sources.Add(entry.Source, entry);
                if (entry.Source != cells[1]) result.sources.TryAdd(cells[1], entry);
                if (hasSlots) result.AddTemplate(entry, priorities.TryGetValue(key, out var rank) ? rank : int.MaxValue);
            }
            if (schema != null)
            {
                CatalogKeys.CheckVariants(graph.Keys);
                schema.CheckReferences(graph, graph.Keys);
            }
            if (result.Count == 0) throw new InvalidDataException("현지화 CSV 파일을 찾을 수 없습니다.");
            result.templates.Sort((a, b) => {
                int comparison = b.Anchor.Length.CompareTo(a.Anchor.Length);
                if (comparison != 0 || templateOrder == null) return comparison;
                comparison = a.Priority.CompareTo(b.Priority);
                return comparison != 0 ? comparison : StringComparer.Ordinal.Compare(a.Entry.Source, b.Entry.Source);
            });
            return result;
        }

        static bool ValidateText(string[] row, CatalogKeys schema = null)
        {
            string key = row[0], source = row[1];
            if (row.Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException("번역표의 네 칸을 모두 채워야 합니다. " + key);
            var slots = Names(source);
            var required = schema == null ? slots : slots.Where(schema.IsArgument).ToArray();
            var braces = BraceCount(source);
            if (schema != null && braces != (slots.Length, slots.Length))
                throw new InvalidDataException("원문의 중괄호 안에 전체 문구 키를 넣으세요. " + key);
            var tags = Tags(source);
            for (int i = 2; i <= 3; i++)
            {
                string text = row[i];
                if (InvalidTranslation(text))
                    throw new InvalidDataException("번역 셀에 오류 또는 로딩 중인 값이 있습니다. " + key);
                var names = Names(text);
                if (schema == null ? !slots.SequenceEqual(names) || braces != BraceCount(text)
                    : !required.SequenceEqual(names.Where(schema.IsArgument)) || BraceCount(text) != (names.Length, names.Length))
                    throw new InvalidDataException("원문과 번역의 치환 자리 또는 중괄호가 다릅니다. " + key);
                if (!tags.SequenceEqual(Tags(text)))
                    throw new InvalidDataException("원문과 번역의 글자 서식 태그가 다릅니다. " + key);
            }
            return slots.Length != 0;
        }

        static readonly char[] CsvSpecial = { ',', '"', '\r', '\n' };

        static string[] Names(string text) => Slot.Matches(text).Cast<Match>()
            .Select(m => m.Groups["name"].Value).OrderBy(s => s, StringComparer.Ordinal).ToArray();

        static (int open, int close) BraceCount(string text) =>
            (text.Count(c => c == '{'), text.Count(c => c == '}'));

        static string[] Tags(string text) => RichTag.Matches(text).Cast<Match>()
            .Select(m => m.Value).OrderBy(s => s, StringComparer.Ordinal).ToArray();

        static bool InvalidTranslation(string text) =>
            text.StartsWith("#ERROR!", StringComparison.Ordinal) || text.StartsWith("#REF!", StringComparison.Ordinal) ||
            text.StartsWith("#N/A", StringComparison.Ordinal) || text.StartsWith("#VALUE!", StringComparison.Ordinal) ||
            text.StartsWith("#NAME?", StringComparison.Ordinal) || text.StartsWith("#DIV/0!", StringComparison.Ordinal) ||
            text.StartsWith("로드 중... (", StringComparison.Ordinal) || text.Contains("Loading data…");

        public string Get(string key, string fallback, bool korean)
        {
            key = schema?.Resolve(key) ?? key;
            if (key == null || !keys.TryGetValue(key, out var entry)) return fallback;
            return schema == null ? entry.Value(korean) : Render("{" + key + "}", korean, null);
        }

        public string Display(string input, bool korean) => DisplayPart(input, korean, 0);

        string DisplayPart(string input, bool korean, int depth)
        {
            if (string.IsNullOrEmpty(input)) return input;
            if (WaitingRoom.WaitingRoomText.TryGetFormat(input, out var formatted))
            {
                var layers = new List<WaitingRoom.WaitingRoomText.FormattedText> { formatted };
                while (WaitingRoom.WaitingRoomText.TryGetFormat(formatted.Template, out var inner))
                { layers.Add(inner); formatted = inner; }
                string template = schema?.MigratePreviousReferences(formatted.Template) ?? formatted.Template;
                string key = formatted.Key == null ? null : "room." + formatted.Key;
                if (key != null && keys.TryGetValue(schema?.Resolve(key) ?? key, out var specific) && specific.Source == template)
                    template = specific.Value(korean);
                else if (sources.TryGetValue(template, out var found)) template = found.Value(korean);
                for (int i = layers.Count - 1; i >= 0; i--)
                    if (layers[i].MarkControls != null) template = layers[i].MarkControls(template);
                string Arg(string name)
                {
                    for (int i = layers.Count - 1; i >= 0; i--)
                    {
                        var layer = layers[i];
                        string value = layer.Argument(name);
                        if (value == null && schema != null)
                        {
                            string keyName = schema.ArgumentKey(name);
                            value = layer.Argument(keyName) ?? layer.Argument(schema.PreviousArgument(keyName));
                        }
                        if (value != null) return value;
                    }
                    return null;
                }
                return Render(template, korean, Arg);
            }
            if (input.StartsWith("@loc:", StringComparison.Ordinal))
                return Get(input.Substring(5), input, korean);
            if (schema != null) input = schema.MigratePreviousReferences(input);
            if (sources.TryGetValue(input, out var entry)) return Render(entry.Value(korean), korean, null);
            if (schema != null && input.IndexOf('{') >= 0 && Slot.Matches(input).Cast<Match>().Any(m => keys.ContainsKey(m.Groups["name"].Value)))
                return Render(input, korean, null);
            // A time, room code or progress counter should never require a template search.
            if (!input.Any(char.IsLetter)) return input;
            if (TryTemplate(input, korean, out string rendered)) return rendered;
            for (int i = depth; i < Separators.Length; i++)
            {
                string delimiter = Separators[i];
                if (input.IndexOf(delimiter, StringComparison.Ordinal) < 0) continue;
                var parts = input.Split(new[] { delimiter }, StringSplitOptions.None);
                for (int j = 0; j < parts.Length; j++) parts[j] = DisplayPart(parts[j], korean, i + 1);
                return string.Join(delimiter, parts);
            }
            return input;
        }

        string Render(string text, bool korean, Func<string, string> argument, string current = null)
        {
            if (text.IndexOf('{') < 0) return text;
            return Slot.Replace(text, match =>
            {
                string name = match.Groups["name"].Value;
                string value;
                if (name == CatalogKeys.CurrentValue && current != null) value = current;
                else if (schema != null && keys.TryGetValue(name, out var linked))
                {
                    if (schema.TryArgument(name, out string slot))
                    {
                        string raw = argument?.Invoke(slot) ?? readControl?.Invoke(slot);
                        if (raw == null) return match.Value;
                        value = Render(linked.Value(korean), korean, argument, SlotValue(slot, raw, korean));
                    }
                    else value = Render(linked.Value(korean), korean, argument);
                }
                else
                {
                    string raw = argument?.Invoke(name);
                    if (raw == null) return match.Value;
                    value = SlotValue(name, raw, korean);
                }
                string particle = CatalogKeys.Particle(match);
                return value + (korean && particle.Length != 0 ? WaitingRoom.WaitingRoomText.Particle(value, particle) : "");
            });
        }

        string SlotValue(string name, string value, bool korean)
        {
            switch (name)
            {
                case "interact": case "jump": case "action": case "forward": case "balance": case "move":
                case "side": case "turn": case "drop": case "uv": case "slot1": case "slot2": case "key": case "legacyInteract":
                case "legacyJump": case "legacyMove": case "legacyPosture":
                    // Each part is a physical key or mouse button. Preserve unlisted keys and separators.
                    return Regex.Replace(value, @"[^/·]+", m =>
                    {
                        string label = m.Value.Trim();
                        // A binding and its displayed label can refer back to each other after a rebind.
                        if (!sources.TryGetValue(label, out var button) || !renderingButtons.Add(button)) return m.Value;
                        try { return m.Value.Replace(label, Render(button.Value(korean), korean, null)); }
                        finally { renderingButtons.Remove(button); }
                    });
                case "items":
                    return string.Join(", ", value.Split(new[] { ", " }, StringSplitOptions.None).Select(v => Display(v, korean)));
                case "item": case "tool": case "trick": case "title": case "label": case "hint": case "description":
                case "reason": case "phase": case "content": case "condition": case "reward": case "choice":
                    return Display(value, korean);
                default:
                    return value;
            }
        }

        bool TryTemplate(string input, bool korean, out string output)
        {
            output = null;
            if (input.Length > 4096) return false;
            foreach (var t in templates)
            {
                if (input.IndexOf(t.Anchor, StringComparison.Ordinal) < 0) continue;
                Match match;
                try { match = t.Pattern.Match(input); }
                catch (RegexMatchTimeoutException) { continue; }
                if (!match.Success) continue;
                output = Render(t.Entry.Value(korean), korean,
                    name => t.Groups.TryGetValue(name, out string group) ? match.Groups[group].Value : null);
                return true;
            }
            return false;
        }

        void AddTemplate(Entry entry, int priority)
        {
            string anchor = "", source = entry.Source;
            var groups = new Dictionary<string, string>();
            var pattern = new StringBuilder(@"\A");
            int pos = 0;
            foreach (Match slot in Slot.Matches(source))
            {
                string literal = source.Substring(pos, slot.Index - pos);
                if (literal.Length > anchor.Length) anchor = literal;
                pattern.Append(Regex.Escape(literal));
                string name = slot.Groups["name"].Value;
                if (groups.TryGetValue(name, out var group)) pattern.Append(@"\k<").Append(group).Append('>');
                else
                {
                    group = "v" + groups.Count;
                    groups.Add(name, group);
                    pattern.Append("(?<").Append(group).Append(@">[^\r\n]*?)");
                }
                if (slot.Groups["particle"].Success)
                    pattern.Append(ParticlePattern(slot.Groups["particle"].Value));
                pos = slot.Index + slot.Length;
            }
            string tail = source.Substring(pos);
            if (tail.Length > anchor.Length) anchor = tail;
            // Never treat an unconstrained name, number or punctuation-only string as a sentence.
            if (!anchor.Any(char.IsLetter)) return;
            pattern.Append(Regex.Escape(tail)).Append(@"\z");
            templates.Add(new Template { Entry = entry, Priority = priority, Anchor = anchor, Groups = groups,
                Pattern = new Regex(pattern.ToString(), RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(8)) });
        }

        static string ParticlePattern(string particle)
        {
            switch (particle)
            {
                case "을": case "를": return "[을를]";
                case "이": case "가": return "[이가]";
                case "은": case "는": return "[은는]";
                case "과": case "와": return "[과와]";
                case "으로": case "로": return "(?:으로|로)";
                case "이다": case "다": return "(?:이다|다)";
                default: return Regex.Escape(particle);
            }
        }

        // RFC-style UTF-8 CSV, including quoted newlines, doubled quotes and Excel BOM.
        readonly struct CsvRecord
        {
            public readonly string[] Cells;
            public readonly int Start, End;
            public CsvRecord(string[] cells, int start, int end)
            { Cells = cells; Start = start; End = end; }
        }

        static IEnumerable<CsvRecord> TableRows(string csv, bool fromSheet = false)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            bool header = true, hasDescription = false;
            foreach (var row in CsvRows(csv))
            {
                if (header)
                {
                    hasDescription = fromSheet && row.Cells.SequenceEqual(new[] { "key", "key description", "source", "ko", "en" });
                    if (!hasDescription && !row.Cells.SequenceEqual(new[] { "key", "source", "ko", "en" }))
                        throw new InvalidDataException(fromSheet
                            ? "열 순서를 확인하세요. key,source,ko,en 또는 key,key description,source,ko,en"
                            : "첫 줄의 열 이름과 순서는 key,source,ko,en 이어야 합니다.");
                    header = false;
                    continue;
                }
                if (row.Cells.Length != (hasDescription ? 5 : 4))
                    throw new InvalidDataException("제목 줄과 열 개수가 다릅니다. " + row.Cells[0]);
                string key = row.Cells[0];
                if (!Key.IsMatch(key) && !(fromSheet && CatalogKeys.PreviousKey.IsMatch(key)))
                    throw new InvalidDataException("문구 코드 형식이 잘못되었습니다. " + key);
                if (!seen.Add(key)) throw new InvalidDataException("문구 코드가 중복되었습니다. " + key);
                yield return hasDescription ? new CsvRecord(new[] { key, row.Cells[2], row.Cells[3], row.Cells[4] }, row.Start, row.End) : row;
            }
            if (seen.Count == 0) throw new InvalidDataException("번역표에 문구가 없습니다.");
        }

        static IEnumerable<CsvRecord> CsvRows(string csv)
        {
            var cells = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false, closed = false;
            csv = csv ?? "";
            int start = csv.Length > 0 && csv[0] == '\uFEFF' ? 1 : 0;
            for (int i = start; i < csv.Length; i++)
            {
                char c = csv[i];
                if (quoted)
                {
                    if (c != '"') cell.Append(c);
                    else if (i + 1 < csv.Length && csv[i + 1] == '"') { cell.Append('"'); i++; }
                    else { quoted = false; closed = true; }
                    continue;
                }
                if (c == ',' || c == '\n' || c == '\r')
                {
                    cells.Add(cell.ToString()); cell.Clear(); closed = false;
                    if (c == ',') continue;
                    yield return new CsvRecord(cells.ToArray(), start, i);
                    if (c == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n') i++;
                    start = i + 1;
                    cells.Clear();
                }
                else if (!closed && c == '"' && cell.Length == 0) quoted = true;
                else if (closed || c == '"') throw new InvalidDataException("CSV 셀의 따옴표 형식이 잘못되었습니다.");
                else cell.Append(c);
            }
            if (quoted) throw new InvalidDataException("CSV 셀의 닫는 따옴표가 없습니다.");
            if (cells.Count > 0 || cell.Length > 0 || closed)
            { cells.Add(cell.ToString()); yield return new CsvRecord(cells.ToArray(), start, csv.Length); }
        }
    }
}

