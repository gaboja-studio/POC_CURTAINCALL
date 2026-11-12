using System;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>HUD display rules; TMP.text remains the original, unformatted message.</summary>
    internal static class HudText
    {
        const string Ratio = @"[0-9]+[ \t]*/[ \t]*[0-9]+";
        const string Progress = @"\[[ \t]*" + Ratio + @"[ \t]*\]|\([ \t]*" + Ratio + @"[ \t]*\)";
        const string StatusStart = "<size=80%><color=#AAAAAA>";
        const string StatusEnd = "</color></size>";
        static readonly Regex ProgressPattern = new(Progress);
        static readonly Regex RatioPattern = new(Ratio);
        static readonly Regex OnlyRatio = new(@"^\s*" + Ratio + @"\s*$");
        static readonly Regex Pieces = new(
            @"(?<progress>" + Progress + @")|(?<tag><[^<>\r\n]+>)|(?<space>\s+)|[^\s<\[(]+|[<\[(]");

        public static bool HasProgress(string value) => value != null && ProgressPattern.IsMatch(value);

        public static void Configure(TMP_Text label, bool statusRow = false)
        {
            if (label == null || label.textPreprocessor is Processor) return;
            label.textPreprocessor = new Processor(label.textPreprocessor, !label.richText, statusRow);
            label.richText = true;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.havePropertiesChanged = true;
            label.SetAllDirty();
        }

        public static void ConfigureView(Component view)
        {
            if (view == null) return;
            foreach (var label in view.GetComponentsInChildren<TMP_Text>(true)) Configure(label);
        }

        public static Vector2 Measure(TMP_Text label, float width = float.PositiveInfinity)
        {
            // TMP renders inside the label margins, but the explicit preferred-values overload
            // takes the available text width and adds the label margins back to its result.
            float textWidth = Mathf.Max(1, width - label.margin.x - label.margin.z);
            return label.GetPreferredValues(label.textPreprocessor?.PreprocessText(label.text) ?? label.text,
                textWidth, float.PositiveInfinity);
        }

        public static string FormatLiteral(string text) => Format(text, true, false);

        sealed class Processor : ITextPreprocessor
        {
            readonly ITextPreprocessor previous;
            readonly bool literal, statusRow;
            string input, output;
            public Processor(ITextPreprocessor previous, bool literal, bool statusRow)
            {
                this.previous = previous;
                this.literal = literal;
                this.statusRow = statusRow;
            }

            public string PreprocessText(string text)
            {
                text = previous?.PreprocessText(text) ?? text ?? "";
                if (text != input)
                {
                    input = text;
                    output = Format(text, literal, statusRow);
                }
                return output;
            }
        }

        static string Format(string text, bool literal, bool statusRow)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var result = new StringBuilder(text.Length);
            bool styleRow = OnlyRatio.IsMatch(text) || statusRow && RatioPattern.IsMatch(text);
            if (styleRow) result.Append(StatusStart);
            bool word = false, hasTextOnLine = false, afterStatus = false;
            string spaces = "";
            void CloseWord()
            {
                if (!word) return;
                result.Append("</nobr>");
                word = false;
            }
            foreach (Match part in Pieces.Matches(text))
            {
                string value = part.Value;
                if (part.Groups["space"].Success)
                {
                    CloseWord();
                    if (value.Contains('\n') || value.Contains('\r'))
                    {
                        result.Append(value);
                        spaces = "";
                        hasTextOnLine = afterStatus = false;
                    }
                    else spaces = value;
                    continue;
                }
                if (!literal && part.Groups["tag"].Success)
                {
                    if (value.Equals("<br>", StringComparison.OrdinalIgnoreCase))
                    {
                        CloseWord();
                        spaces = "";
                        hasTextOnLine = afterStatus = false;
                    }
                    result.Append(value);
                    continue;
                }
                if (part.Groups["progress"].Success)
                {
                    CloseWord();
                    if (hasTextOnLine) result.Append('\n');
                    if (!styleRow) result.Append(StatusStart);
                    result.Append("<nobr>").Append(value).Append("</nobr>");
                    if (!styleRow) result.Append(StatusEnd);
                    spaces = "";
                    hasTextOnLine = afterStatus = true;
                    continue;
                }
                if (afterStatus) result.Append('\n');
                else result.Append(spaces);
                afterStatus = false;
                spaces = "";
                if (!word)
                {
                    result.Append("<nobr>");
                    word = true;
                }
                // Keep user text literal in the combined event label and in originally non-rich labels.
                if (literal && value.Contains('<'))
                    result.Append("<noparse>").Append(value.Replace("</noparse>", "<\u200b/noparse>", StringComparison.OrdinalIgnoreCase)).Append("</noparse>");
                else result.Append(value);
                hasTextOnLine = true;
            }
            CloseWord();
            if (styleRow) result.Append(StatusEnd);
            return result.ToString();
        }
    }
}
