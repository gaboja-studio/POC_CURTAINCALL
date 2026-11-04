using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    /// <summary>
    /// 추격 설계 창에서 대기실 문구 표(WaitingRoomTexts.txt)의 <c>pursuit.*</c> 줄을 직접 읽고 고친다(Task-103).
    /// 설명 줄·다른 구역·줄 순서는 그대로 두고 해당 키 줄만 바꾼다. 새 키는 마지막 <c>pursuit.</c> 줄 뒤에 넣는다.
    /// 값은 파일 표기 그대로 다룬다(줄바꿈은 <c>\n</c> 두 글자). 비우면 줄을 지워 코드 기본 문구로 돌아간다.
    /// </summary>
    static class PursuitTextTable
    {
        public const string AssetPath = "Assets/Resources/Settings/WaitingRoom/WaitingRoomTexts.txt";
        const string StagePrefix = "pursuit.stage.";
        static readonly Regex StageLine = new(@"^\s*pursuit\.stage\.(\d+)\s*\|", RegexOptions.Compiled);

        static List<string> lines;
        static string newline = "\r\n";
        static DateTime loadedAt;

        static string FullPath => Path.GetFullPath(AssetPath);

        static List<string> Lines()
        {
            var stamp = File.Exists(FullPath) ? File.GetLastWriteTimeUtc(FullPath) : DateTime.MinValue;
            if (lines != null && stamp == loadedAt) return lines;
            loadedAt = stamp;
            string text = stamp == DateTime.MinValue ? "" : File.ReadAllText(FullPath, Encoding.UTF8);
            newline = text.Contains("\r\n") ? "\r\n" : "\n";
            lines = new List<string>(text.Replace("\r\n", "\n").Split('\n'));
            return lines;
        }

        static int Find(List<string> all, string key)
        {
            var pattern = new Regex(@"^\s*" + Regex.Escape(key) + @"\s*\|");
            for (int i = 0; i < all.Count; i++) if (!all[i].TrimStart().StartsWith("#") && pattern.IsMatch(all[i])) return i;
            return -1;
        }

        /// <summary>파일 표기 그대로의 값. 줄이 없으면 null.</summary>
        public static string Raw(string key)
        {
            var all = Lines();
            int i = Find(all, key);
            if (i < 0) return null;
            int bar = all[i].IndexOf('|');
            string value = all[i].Substring(bar + 1).Trim();
            return value.Length > 0 ? value : null;
        }

        public static void Set(string key, string raw)
        {
            var all = Lines();
            raw = Clean(raw);
            int i = Find(all, key);
            if (raw.Length == 0) { if (i >= 0) all.RemoveAt(i); }
            else if (i >= 0) all[i] = key + " | " + raw;
            else all.Insert(InsertAt(all), key + " | " + raw);
            Save(all);
        }

        /// <summary>단계 알림 목록(0부터). 표에 없는 단계는 null.</summary>
        public static List<string> StageTexts(int count)
        {
            var result = new List<string>();
            for (int i = 0; i < count; i++) result.Add(Raw(StagePrefix + (i + 1)));
            return result;
        }

        /// <summary>단계 복제·제거 뒤 번호가 밀리지 않게 단계 알림 줄을 통째로 다시 쓴다.</summary>
        public static void RewriteStages(IList<string> texts)
        {
            var all = Lines();
            int at = -1;
            for (int i = all.Count - 1; i >= 0; i--)
                if (StageLine.IsMatch(all[i])) { all.RemoveAt(i); at = i; }
            if (at < 0) at = InsertAt(all);
            int n = 0;
            for (int i = 0; i < texts.Count; i++)
            {
                string raw = Clean(texts[i]);
                if (raw.Length > 0) all.Insert(at + n++, StagePrefix + (i + 1) + " | " + raw);
            }
            Save(all);
        }

        static string Clean(string raw) => (raw ?? "").Replace("\r\n", "\\n").Replace("\n", "\\n").Trim();

        static int InsertAt(List<string> all)
        {
            int last = -1;
            for (int i = 0; i < all.Count; i++) if (all[i].TrimStart().StartsWith("pursuit.")) last = i;
            if (last >= 0) return last + 1;
            // 추격 구역이 없으면 파일 끝에 새로 만든다.
            while (all.Count > 0 && all[^1].Trim().Length == 0) all.RemoveAt(all.Count - 1);
            all.Add(""); all.Add("# ── 추격 (Task-103, 추격 설계 창에서 고침) ──");
            int end = all.Count;
            all.Add("");
            return end;
        }

        static void Save(List<string> all)
        {
            File.WriteAllText(FullPath, string.Join(newline, all), new UTF8Encoding(false));
            loadedAt = File.GetLastWriteTimeUtc(FullPath);
            AssetDatabase.ImportAsset(AssetPath);
            // 플레이 중이면 다음 표시부터 바로 반영한다.
            if (Application.isPlaying) WaitingRoomText.Install(string.Join("\n", all));
        }
    }
}
