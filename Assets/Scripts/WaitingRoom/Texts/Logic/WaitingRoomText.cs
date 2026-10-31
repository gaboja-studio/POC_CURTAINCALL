using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 대기실 화면 문구 표(2026-10-31 사용자: 대기실에서 나오는 모든 문구를 직접 수정). 사람이 고치는 파일
    /// Assets/Resources/Settings/WaitingRoom/WaitingRoomTexts.txt(`키 | 문구`)를 시작할 때 읽어 둔다.
    /// 코드는 <c>Get("키", "기본 문구")</c>로 쓰고, 파일에 그 키가 없으면 기본 문구를 그대로 쓴다.
    /// 바뀌는 값은 <c>{이름}</c>, 조사 자동 선택은 <c>{이름:을}</c>(을/를·이/가·은/는·과/와·으로/로·이다/다).
    /// </summary>
    public static class WaitingRoomText
    {
        static Dictionary<string, string> table = new();

        public static int Count => table.Count;
        public static bool Has(string key) => key != null && table.ContainsKey(key);

        /// <summary>문구 파일 내용을 표로 설치한다(이전 표는 버린다).</summary>
        public static void Install(string fileText) => table = Parse(fileText);
        public static void Clear() => table = new();

        /// <summary>`키 | 문구` 줄을 읽는다. #으로 시작하면 설명, 문구 안의 \n은 줄바꿈. 빈 문구·형식이 틀린 줄은 건너뛴다.</summary>
        public static Dictionary<string, string> Parse(string text)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(text)) return result;
            foreach (var raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int bar = line.IndexOf('|');
                if (bar <= 0) continue;
                string key = line.Substring(0, bar).Trim(), value = line.Substring(bar + 1).Trim();
                if (key.Length == 0 || value.Length == 0) continue;
                result[key] = value.Replace("\\n", "\n");
            }
            return result;
        }

        public static string Get(string key, string fallback) => Fill(Raw(key, fallback), null);

        public static string Format(string key, string fallback, params (string name, object value)[] values) =>
            Fill(Raw(key, fallback), values);

        static string Raw(string key, string fallback) =>
            key != null && table.TryGetValue(key, out var text) ? text : fallback ?? "";

        /// <summary>{이름}·{이름:조사}를 값으로 바꾼다. 모르는 이름은 그대로 둔다.</summary>
        public static string Fill(string template, (string name, object value)[] values)
        {
            if (string.IsNullOrEmpty(template) || values == null || values.Length == 0 || template.IndexOf('{') < 0) return template ?? "";
            var sb = new StringBuilder(template.Length + 16);
            int i = 0;
            while (i < template.Length)
            {
                int open = template.IndexOf('{', i);
                if (open < 0) { sb.Append(template, i, template.Length - i); break; }
                int close = template.IndexOf('}', open + 1);
                if (close < 0) { sb.Append(template, i, template.Length - i); break; }
                sb.Append(template, i, open - i);
                string token = template.Substring(open + 1, close - open - 1);
                int colon = token.IndexOf(':');
                string name = colon >= 0 ? token.Substring(0, colon) : token;
                if (TryValue(values, name, out string value))
                {
                    sb.Append(value);
                    if (colon >= 0) sb.Append(Particle(value, token.Substring(colon + 1)));
                }
                else sb.Append('{').Append(token).Append('}');
                i = close + 1;
            }
            return sb.ToString();
        }

        static bool TryValue((string name, object value)[] values, string name, out string text)
        {
            foreach (var (n, v) in values)
                if (n == name)
                {
                    text = v is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : v?.ToString() ?? "";
                    return true;
                }
            text = null;
            return false;
        }

        /// <summary>앞말의 받침에 맞는 조사. <paramref name="either"/>는 짝 중 아무거나(예: "을" 또는 "를").</summary>
        public static string Particle(string word, string either)
        {
            string with, without;
            switch (either)
            {
                case "을": case "를": with = "을"; without = "를"; break;
                case "이": case "가": with = "이"; without = "가"; break;
                case "은": case "는": with = "은"; without = "는"; break;
                case "과": case "와": with = "과"; without = "와"; break;
                case "이다": case "다": with = "이다"; without = "다"; break;
                case "으로": case "로":
                    int f = Final(word);
                    // ㄹ 받침은 '로'(예: 연습실로).
                    return f > 0 && f != 8 ? "으로" : "로";
                default: return either;
            }
            return Final(word) > 0 ? with : without;
        }

        /// <summary>마지막 글자 받침 번호(0 = 없음). 한글이 아니면 숫자는 읽는 소리, 나머지는 받침 없음.</summary>
        static int Final(string word)
        {
            if (string.IsNullOrEmpty(word)) return 0;
            for (int i = word.Length - 1; i >= 0; i--)
            {
                char c = word[i];
                if (c == ')' || c == ']' || c == '\'' || c == '"' || char.IsWhiteSpace(c)) continue;
                if (c >= '가' && c <= '힣') return (c - 0xAC00) % 28;
                // 영·일·삼·육·칠·팔 = 받침, 일·칠·팔은 ㄹ(8).
                return c switch { '0' => 21, '1' => 8, '3' => 16, '6' => 1, '7' => 8, '8' => 8, _ => 0 };
            }
            return 0;
        }

        /// <summary>문구 안의 {이름} 목록(조사 표기 제외). 테스트용.</summary>
        public static HashSet<string> Names(string template)
        {
            var names = new HashSet<string>();
            if (string.IsNullOrEmpty(template)) return names;
            int i = 0;
            while ((i = template.IndexOf('{', i)) >= 0)
            {
                int close = template.IndexOf('}', i + 1);
                if (close < 0) break;
                string token = template.Substring(i + 1, close - i - 1);
                int colon = token.IndexOf(':');
                names.Add(colon >= 0 ? token.Substring(0, colon) : token);
                i = close + 1;
            }
            return names;
        }
    }
}
