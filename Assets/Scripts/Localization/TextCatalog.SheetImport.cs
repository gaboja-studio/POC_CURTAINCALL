using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CurtainCall.Localization
{
    public sealed partial class TextCatalog
    {
        // A workbook supplies one named sheet per file. Older CSV downloads are routed by key.
        public static Dictionary<string, string> MergeSections(IReadOnlyDictionary<string, string> originals,
            IEnumerable<KeyValuePair<string, string>> sheets, string baseline, out string nextBaseline, out int changed,
            CatalogKeys schema = null)
        {
            var owners = new Dictionary<string, string>(StringComparer.Ordinal);
            var graph = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (var section in originals)
                foreach (var row in TableRows(section.Value))
                {
                    if (!owners.TryAdd(row.Cells[0], section.Key))
                        throw new InvalidDataException("여러 CSV에 같은 문구 코드가 있습니다. " + row.Cells[0]);
                    if (schema == null) continue;
                    CatalogKeys.CheckOwner(section.Key, row.Cells[0]);
                    graph.Add(row.Cells[0], row.Cells);
                }

            if (schema != null) CatalogKeys.CheckVariants(owners.Keys);
            var patches = new Dictionary<string, StringBuilder>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var sheet in sheets)
                foreach (var row in TableRows(sheet.Value, fromSheet: true))
                {
                    var cells = row.Cells;
                    string key = schema?.Resolve(cells[0]) ?? cells[0];
                    if (key != cells[0])
                    {
                        cells[0] = key;
                        for (int i = 1; i < cells.Length; i++) cells[i] = schema.ConvertSlots(cells[i], true);
                    }
                    if (!seen.Add(key)) throw new InvalidDataException("가져올 문구 코드가 중복되었습니다. " + key);
                    if (!owners.TryGetValue(key, out var section)) throw new InvalidDataException("로컬 CSV에 없는 문구 코드입니다. " + key);
                    if (sheet.Key != null && sheet.Key != section)
                        throw new InvalidDataException($"{key} 문구는 ‘{section}’ 탭에 있어야 합니다. 현재 탭은 ‘{sheet.Key}’입니다.");
                    if (!patches.TryGetValue(section, out var patch))
                        patches.Add(section, patch = new StringBuilder("key,source,ko,en\n"));
                    patch.AppendLine(string.Join(",", cells.Select(QuoteCell)));
                }

            var updates = new Dictionary<string, string>();
            nextBaseline = baseline;
            changed = 0;
            var changedKeys = new List<string>();
            foreach (var patch in patches)
            {
                var csv = MergeEdits(originals[patch.Key], patch.Value.ToString(), nextBaseline, out nextBaseline, out int count, schema);
                if (count > 0)
                {
                    updates.Add(patch.Key, csv);
                    if (schema != null)
                        foreach (var row in TableRows(csv))
                        {
                            string key = row.Cells[0];
                            if (row.Cells.SequenceEqual(graph[key])) continue;
                            graph[key] = row.Cells;
                            changedKeys.Add(key);
                        }
                }
                changed += count;
            }
            schema?.CheckReferences(graph, changedKeys);
            return updates;
        }

        public static string MergeEdits(string original, string patch, out int changed) =>
            MergeEdits(original, patch, "", out _, out changed);

        // The baseline records what the sheet contained when last accepted by the team.
        // Sorting the sheet or omitting rows from a downloaded CSV does not change their identity.
        public static string MergeEdits(string original, string sheet, string baseline,
            out string nextBaseline, out int changed, CatalogKeys schema = null)
        {
            var localRows = TableRows(original).ToList();
            var localByKey = localRows.ToDictionary(row => row.Cells[0], StringComparer.Ordinal);
            var previous = ReadBaseline(baseline);
            var edits = new Dictionary<string, string[]>(StringComparer.Ordinal);
            bool advance = false;
            using var sha = SHA256.Create();
            foreach (var incoming in TableRows(sheet, fromSheet: true))
            {
                var cells = incoming.Cells;
                string key = cells[0];
                string next = Fingerprint(sha, cells);
                bool known = previous.TryGetValue(key, out var last);
                if (next == last) continue;
                if (!localByKey.TryGetValue(key, out var local))
                    throw new InvalidDataException("로컬 CSV에 없는 문구 코드입니다. " + key);

                string current = Fingerprint(sha, local.Cells);
                if (current != next)
                {
                    if (NormalizeBreaks(cells[1]) != NormalizeBreaks(local.Cells[1]))
                        throw new InvalidDataException("원문(source)이 다릅니다. 시트의 원문을 확인하세요. " + key);
                    if (known && current != last)
                        throw new InvalidDataException("로컬과 시트에서 같은 행을 각각 수정했습니다. 두 번역을 대조하세요. " + key);
                    ValidateText(cells, schema);
                    // Preserve the original source and unchanged language even if Sheets normalized a newline.
                    var merged = (string[])local.Cells.Clone();
                    for (int i = 2; i <= 3; i++)
                        if (NormalizeBreaks(cells[i]) != NormalizeBreaks(merged[i])) merged[i] = cells[i];
                    edits.Add(key, merged);
                }
                else if (known)
                {
                    // An interrupted import may already have installed the game CSV.
                    ValidateText(cells, schema);
                }
                previous[key] = next;
                advance = true;
            }

            nextBaseline = advance ? string.Concat(previous.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => p.Key + "\t" + p.Value + "\n")) : baseline;
            changed = edits.Count;
            if (changed == 0) return original;

            int position = 0;
            var output = new StringBuilder(original.Length);
            foreach (var row in localRows)
            {
                if (!edits.TryGetValue(row.Cells[0], out var replacement)) continue;
                output.Append(original, position, row.Start - position);
                output.Append(string.Join(",", replacement.Select(QuoteCell)));
                position = row.End;
            }
            output.Append(original, position, original.Length - position);
            return output.ToString();
        }

        static Dictionary<string, string> ReadBaseline(string text)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            using var reader = new StringReader(text);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                var fields = line.Split('\t');
                if (fields.Length != 2 || (!Key.IsMatch(fields[0]) && !CatalogKeys.ValidKey(fields[0])) || fields[1].Length != 44 ||
                    result.ContainsKey(fields[0]))
                    throw new InvalidDataException("시트 동기화 기준 파일을 확인하세요.");
                result.Add(fields[0], fields[1]);
            }
            return result;
        }

        static string NormalizeBreaks(string value) => value.Replace("\r\n", "\n").Replace('\r', '\n');

        static string Fingerprint(SHA256 sha, string[] row)
        {
            // Always quote every field so commas, quotes, and embedded line breaks are unambiguous.
            string record = string.Join(",", row.Select(x => "\"" + NormalizeBreaks(x).Replace("\"", "\"\"") + "\""));
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(record)));
        }

        static string QuoteCell(string value) => value.IndexOfAny(CsvSpecial) < 0
            ? value : "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}

