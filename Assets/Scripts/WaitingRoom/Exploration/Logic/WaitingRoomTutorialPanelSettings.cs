using System;
using System.Collections.Generic;
using System.Globalization;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 튜토리얼 설명 패널 수치(2026-10-31 사용자: 패널도 직접 수정). 사람이 고치는 설정 파일(`이름 = 값`, #은 설명)을 읽는다.
    /// 모르는 이름·숫자가 아닌 값은 건너뛰고 기본값을 쓴다. 범위를 벗어난 값은 범위 안으로 맞춘다(툴팁 패널 크기 한도).
    /// </summary>
    public sealed class WaitingRoomTutorialPanelSettings
    {
        /// <summary>왼쪽 위 기준 위치(1920×1080 UI 단위, 아래가 +).</summary>
        public float X = 36f, Y = 300f;
        /// <summary>툴팁 패널 너비 한도 256~480.</summary>
        public float Width = 440f;
        /// <summary>안내 기본 표시 시간(초). 문구 파일 4번째 칸이 있으면 그 안내만 그 값.</summary>
        public float Seconds = 8f;
        /// <summary>앞 안내가 사라진 뒤 다음 안내까지(초).</summary>
        public float Gap = .6f;
        /// <summary>글자 크기. 0이면 패널 기본(제목 28, 본문 24).</summary>
        public float TitleSize, BodySize;

        static readonly Dictionary<string, Action<WaitingRoomTutorialPanelSettings, float>> Keys = new()
        {
            ["위치X"] = (s, v) => s.X = Clamp(v, 0f, 1700f),
            ["위치Y"] = (s, v) => s.Y = Clamp(v, 0f, 900f),
            ["너비"] = (s, v) => s.Width = Clamp(v, 256f, 480f),
            ["표시시간"] = (s, v) => s.Seconds = Clamp(v, 1f, 60f),
            ["간격"] = (s, v) => s.Gap = Clamp(v, 0f, 10f),
            ["제목크기"] = (s, v) => s.TitleSize = v <= 0f ? 0f : Clamp(v, 18f, 32f),
            ["본문크기"] = (s, v) => s.BodySize = v <= 0f ? 0f : Clamp(v, 16f, 30f),
        };

        public static WaitingRoomTutorialPanelSettings Parse(string text)
        {
            var settings = new WaitingRoomTutorialPanelSettings();
            if (string.IsNullOrEmpty(text)) return settings;
            foreach (var raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int hash = line.IndexOf('#');
                if (hash >= 0) line = line.Substring(0, hash);
                var parts = line.Split(new[] { '=' }, 2);
                if (parts.Length < 2) continue;
                string key = parts[0].Replace(" ", "").Trim();
                if (Keys.TryGetValue(key, out var apply) && TryNumber(parts[1], out float value)) apply(settings, value);
            }
            return settings;
        }

        public static bool TryNumber(string text, out float value) =>
            float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !float.IsNaN(value) && !float.IsInfinity(value);

        static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
    }
}
