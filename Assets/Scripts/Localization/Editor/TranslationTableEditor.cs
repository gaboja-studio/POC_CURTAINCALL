using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace CurtainCall.Localization.Editor
{
    public static class TranslationTableEditor
    {
        internal const string Folder = "Assets/Resources/Localization";
        const string BaselinePath = "Scripts/Localization/sheet-baseline.tsv";
        const string Sheet = "https://docs.google.com/spreadsheets/d/1Qs0xnPnU4R-gWUDGhLk56h2RYMBIbh6IxJ-Qi0OFgKo";
        const string Export = Sheet + "/export?format=xlsx";
        const string Menu = "Curtain Call/Localization/";

        [MenuItem(Menu + "Open translation sheet")]
        static void OpenSheet() => Application.OpenURL(Sheet + "/edit#gid=278991085");

        [MenuItem(Menu + "Download workbook in browser")]
        static void Download() => Application.OpenURL(Export);

        [MenuItem(Menu + "Import downloaded workbook or CSV")]
        static void Import()
        {
            string path = EditorUtility.OpenFilePanelWithFilters("번역 통합 문서 또는 CSV 선택", "",
                new[] { "번역 파일", "xlsx,csv" });
            if (string.IsNullOrEmpty(path)) return;
            TryInstall(sections =>
            {
                if (string.Equals(Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase))
                    return new[] { new KeyValuePair<string, string>(null, File.ReadAllText(path, Encoding.UTF8)) };
                using var input = File.OpenRead(path);
                return TranslationWorkbook.Read(input, sections);
            });
        }

        [MenuItem(Menu + "Sync from Google Sheets")]
        static void Sync()
        {
            var request = UnityWebRequest.Get(Export);
            request.timeout = 45;
            request.SendWebRequest().completed += _ =>
            {
                try
                {
                    if (request.responseCode == 401 || request.responseCode == 403 ||
                        (request.GetResponseHeader("Content-Type") ?? "").StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!EditorUtility.DisplayDialog("Google 계정으로 불러오기",
                            "로그인된 브라우저에서 전체 번역 통합 문서(.xlsx)를 내려받으세요.", "브라우저 열기", "취소")) return;
                        Download();
                        if (EditorUtility.DisplayDialog("번역 통합 문서 불러오기", "다운로드가 끝나면 파일을 선택하세요.", "파일 선택", "취소")) Import();
                        return;
                    }
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        EditorUtility.DisplayDialog("번역표 연결 실패", request.error, "닫기");
                        return;
                    }
                    TryInstall(sections =>
                    {
                        using var input = new MemoryStream(request.downloadHandler.data, false);
                        return TranslationWorkbook.Read(input, sections);
                    });
                }
                finally { request.Dispose(); }
            };
        }

        internal static Dictionary<string, string> ReadLocal() => Directory.GetFiles(Folder, "*.csv")
            .ToDictionary(path => Path.GetFileNameWithoutExtension(path), path => File.ReadAllText(path), StringComparer.Ordinal);

        static void TryInstall(Func<IEnumerable<string>, IEnumerable<KeyValuePair<string, string>>> read)
        {
            try
            {
                var originals = ReadLocal();
                if (originals.Count == 0) throw new InvalidDataException("분류별 번역 CSV를 찾을 수 없습니다.");
                string baseline = File.ReadAllText(BaselinePath);
                var edits = TextCatalog.MergeSections(originals, read(originals.Keys), baseline, out var nextBaseline, out int changed, GameLanguage.LoadKeys());
                if (changed == 0 && baseline == nextBaseline)
                {
                    EditorUtility.DisplayDialog("번역표 확인 완료", "시트에서 새로 반영할 변경이 없습니다.", "닫기");
                    return;
                }
                if (changed > 0 && !EditorUtility.DisplayDialog("변경된 번역 검사 완료",
                    $"{edits.Count}개 파일의 {changed}행을 반영합니다.\n{string.Join(", ", edits.Keys)}", "반영", "취소")) return;

                using (File.Open("Library/LocalizationSheetImport.lock", FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
                {
                    var current = ReadLocal();
                    if (current.Count != originals.Count || originals.Any(p => !current.TryGetValue(p.Key, out var value) || value != p.Value)
                        || File.ReadAllText(BaselinePath) != baseline)
                        throw new IOException("검사 중 파일이 수정되었습니다. 다시 불러오세요.");
                    var writes = edits.Select(p => (path: Path.Combine(Folder, p.Key + ".csv"), contents: p.Value, bom: true)).ToList();
                    // Publishing the baseline last also allows retry after a process interruption.
                    writes.Add((BaselinePath, nextBaseline, false));
                    Install(writes);
                }
                foreach (var section in edits.Keys) AssetDatabase.ImportAsset(Folder + "/" + section + ".csv");
                Debug.Log($"[Localization] {edits.Count}개 파일에 번역 {changed}행 반영. 다음 플레이부터 적용됩니다.");
            }
            catch (Exception e) { EditorUtility.DisplayDialog("번역표를 적용할 수 없습니다", e.Message, "닫기"); }
        }

        static void Install(List<(string path, string contents, bool bom)> writes)
        {
            string work = Path.Combine("Library", "TranslationImport-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(work);
            string Stage(int index, string suffix) => Path.Combine(work, index + suffix);
            int installed = 0;
            bool clean = false;
            try
            {
                for (int i = 0; i < writes.Count; i++)
                    File.WriteAllText(Stage(i, ".new"), writes[i].contents, new UTF8Encoding(writes[i].bom));
                for (; installed < writes.Count; installed++)
                    File.Replace(Stage(installed, ".new"), writes[installed].path, Stage(installed, ".old"));
                clean = true;
            }
            catch
            {
                while (installed > 0)
                {
                    installed--;
                    File.Replace(Stage(installed, ".old"), writes[installed].path, null);
                }
                clean = true;
                throw;
            }
            finally
            {
                if (clean) Directory.Delete(work, true);
            }
        }
    }
}
