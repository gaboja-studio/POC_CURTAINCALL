using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace CurtainCall.Localization.Editor
{
    // Read the cached values in a Sheets XLSX export. Only translation tabs enter the game.
    internal static class TranslationWorkbook
    {
        static readonly XNamespace Spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        static readonly XNamespace OfficeRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        static readonly XNamespace PackageRel = "http://schemas.openxmlformats.org/package/2006/relationships";
        static readonly Dictionary<string,string> PreviousTabs = new Dictionary<string,string>(StringComparer.Ordinal)
        {
            ["COMMON"] = "공통 UI",
            ["LOBBY"] = "시작·로비",
            ["SETTINGS"] = "설정·조작",
            ["WAITING-ROOM"] = "대기실",
            ["EXPLORATION"] = "탐색·탈출",
            ["SHOP"] = "상점·기물",
            ["MEDICAL"] = "의료·생존",
            ["SCHEDULE"] = "일정·보상",
            ["REHEARSAL"] = "리허설",
            ["TIGHTROPE"] = "외줄 공연",
            ["CANNON"] = "인간대포",
            ["AUDIO"] = "소리·자막",
            ["DEV"] = "개발 도구"
        };

        public static Dictionary<string, string> Read(Stream stream, IEnumerable<string> sections)
        {
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, true);
            var book = Load(zip, "xl/workbook.xml");
            var relations = Load(zip, "xl/_rels/workbook.xml.rels").Root.Elements(PackageRel + "Relationship")
                .Where(r => (string)r.Attribute("TargetMode") != "External")
                .ToDictionary(r => (string)r.Attribute("Id"), r => (string)r.Attribute("Target"));
            var sheets = book.Descendants(Spreadsheet + "sheet").ToDictionary(s => (string)s.Attribute("name"),
                s => (string)s.Attribute(OfficeRel + "id"), StringComparer.Ordinal);
            var strings = zip.GetEntry("xl/sharedStrings.xml") == null ? Array.Empty<string>()
                : Load(zip, "xl/sharedStrings.xml").Root.Elements(Spreadsheet + "si").Select(StringValue).ToArray();
            var result = new Dictionary<string, string>();
            foreach (string section in sections)
            {
                string title = section;
                if (!sheets.ContainsKey(section) && PreviousTabs.TryGetValue(section, out var oldTitle)) title = oldTitle;
                if (!sheets.TryGetValue(title, out var id) || !relations.TryGetValue(id, out var target))
                    throw new InvalidDataException($"통합 문서에 ‘{section}’ 번역 탭이 없습니다.");
                var path = new Uri(new Uri("https://workbook.invalid/xl/workbook.xml"), target).AbsolutePath.TrimStart('/');
                result.Add(section, SheetCsv(Load(zip, path), strings, section));
            }
            return result;
        }

        static XDocument Load(ZipArchive zip, string path)
        {
            var entry = zip.GetEntry(path) ?? throw new InvalidDataException("통합 문서에 필요한 항목이 없습니다. " + path);
            using var input = entry.Open();
            using var xml = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            return XDocument.Load(xml, LoadOptions.PreserveWhitespace);
        }

        static string StringValue(XElement container)
        {
            if (container == null) return "";
            var text = string.Concat(container.Elements().Select(e => e.Name == Spreadsheet + "t" ? e.Value
                : e.Name == Spreadsheet + "r" ? (string)e.Element(Spreadsheet + "t") : ""));
            return Regex.Replace(text, "_x([0-9A-Fa-f]{4})_", m =>
                ((char)int.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToString());
        }

        static string SheetCsv(XDocument sheet, string[] strings, string section)
        {
            var csv = new StringBuilder();
            int width = 0;
            foreach (var row in sheet.Descendants(Spreadsheet + "sheetData").Elements(Spreadsheet + "row"))
            {
                var cells = new string[5];
                int used = 0;
                foreach (var cell in row.Elements(Spreadsheet + "c"))
                {
                    string address = (string)cell.Attribute("r") ?? "";
                    string value = (string)cell.Element(Spreadsheet + "v") ?? "";
                    string type = (string)cell.Attribute("t");
                    if (type == "s") value = strings[int.Parse(value, CultureInfo.InvariantCulture)];
                    if (type == "inlineStr") value = StringValue(cell.Element(Spreadsheet + "is"));
                    if (value.Length == 0 && cell.Element(Spreadsheet + "f") == null) continue;
                    if (address.Length < 2 || address[0] < 'A' || address[0] > 'E' || !char.IsDigit(address[1]))
                        throw new InvalidDataException($"‘{section}’ 탭의 열을 확인하세요. {address}");
                    int index = address[0] - 'A';
                    cells[index] = value;
                    used = Math.Max(used, index + 1);
                }
                if (used == 0) continue;
                if (width == 0) width = used;
                for (int i = 0; i < Math.Max(used, width); i++)
                {
                    if (i > 0) csv.Append(',');
                    csv.Append('"').Append((cells[i] ?? "").Replace("\"", "\"\"")).Append('"');
                }
                csv.Append('\n');
            }
            return csv.ToString();
        }
    }
}
