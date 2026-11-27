using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CurtainCall.Localization
{
    public static class GameLanguage
    {
        const string PreferenceKey = "CurtainCall.Language";
        public const string CatalogFolder = "Localization";
        public static CatalogKeys LoadKeys()
        {
            var aliases = Resources.Load<TextAsset>("LocalizationKeyAliases");
            var arguments = Resources.Load<TextAsset>("LocalizationArguments");
            if (!aliases || !arguments) throw new InvalidDataException("문구 키 연결표를 찾을 수 없습니다.");
            return new CatalogKeys(aliases.text, arguments.text);
        }

        static string ReadControl(string slot)
        {
            switch (slot)
            {
                case "side": case "turn": case "interact": case "jump": case "action": case "forward":
                case "balance": case "move": case "uv": case "slot1": case "slot2": case "drop":
                    var input = Application.isPlaying ? WaitingRoom.WaitingRoomHudProfile.LocalInput : null;
                    string template = "{" + slot + "}";
                    if (input || !Application.isPlaying)
                    {
                        if (slot == "drop") return input ? input.BindingLabel("DropItem", "T") : "T";
                        return UI.SceneIntegration.PerformanceHudProfile.FormatKeys(template, input);
                    }
                    // The start menu and standalone world signs can render before a player spawns.
                    if (savedControls == null)
                    {
                        savedControls = new Dictionary<(string,string),string>();
                        var draft = Player.InputBindingPreferences.CreateDraft();
                        if (draft)
                        {
                            foreach (var binding in Player.InputBindingPreferences.GetBindings(draft))
                                savedControls.TryAdd((binding.ActionName, binding.IsPartOfComposite ? binding.PartName : ""), binding.DisplayName);
                            UnityEngine.Object.Destroy(draft);
                        }
                    }
                    string Part(string name, string part, string fallback) =>
                        savedControls.TryGetValue((name, part), out string label) ? label : fallback;
                    string Action(string name, string fallback) => Part(name, "", fallback);
                    return slot == "drop" ? Action("DropItem", "T") :
                        UI.SceneIntegration.PerformanceHudProfile.FormatLabels(template, Action, Part);
                default: return null;
            }
        }

        public static TextCatalog LoadCatalog()
        {
            // Some legacy network messages arrive with their arguments already substituted.
            // Preserve their original match when multiple templates can describe the same text.
            var priority = Resources.Load<TextAsset>("LocalizationTemplateOrder");
            if (!priority) throw new InvalidDataException("기존 치환 문구의 우선순위 파일이 없습니다.");
            return TextCatalog.ReadSections(Resources.LoadAll<TextAsset>(CatalogFolder)
                .ToDictionary(asset => asset.name, asset => asset.text, StringComparer.Ordinal),
                priority.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries), LoadKeys(), ReadControl);
        }
        static TextCatalog catalog;
        static bool initialized;
        static bool korean;
        static readonly Dictionary<string, string> cache = new Dictionary<string, string>(StringComparer.Ordinal);

        public static string Preference => PlayerPrefs.GetString(PreferenceKey, "auto");
        public static string Code { get { EnsureLoaded(); return korean ? "ko" : "en"; } }
        public static event Action Changed;
        static Dictionary<(string,string),string> savedControls;
        static void ClearDisplayCache()
        {
            savedControls = null;
            cache.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            initialized = false;
            catalog = null;
            ClearDisplayCache();
            Player.InputBindingPreferences.Changed -= ClearDisplayCache;
            Player.InputBindingPreferences.Changed += ClearDisplayCache;
            Changed = null;
        }

        public static string Resolve(string code, SystemLanguage system)
        {
            if (string.IsNullOrEmpty(code) || code.Equals("auto", StringComparison.OrdinalIgnoreCase))
                return system == SystemLanguage.Korean ? "ko" : "en";
            var primary = code.Split('-', '_')[0];
            return primary.Equals("ko", StringComparison.OrdinalIgnoreCase) || primary.Equals("kr", StringComparison.OrdinalIgnoreCase)
                ? "ko" : "en";
        }

        static void EnsureLoaded()
        {
            if (initialized) return;
            initialized = true;
            korean = Resolve(Preference, Application.systemLanguage) == "ko";
            try { catalog = LoadCatalog(); }
            catch (InvalidDataException e) { Debug.LogError("[Localization] " + e.Message); }
        }

        public static void SetPreference(string code)
        {
            EnsureLoaded();
            string preference = string.IsNullOrEmpty(code) || code == "auto" ? "auto" : Resolve(code, Application.systemLanguage);
            if (preference != Preference)
            {
                PlayerPrefs.SetString(PreferenceKey, preference);
                PlayerPrefs.Save();
            }
            bool next = Resolve(preference, Application.systemLanguage) == "ko";
            if (next == korean) return;
            korean = next;
            cache.Clear();
            Changed?.Invoke();
        }

        public static string Get(string key, string fallback)
        {
            EnsureLoaded();
            return catalog?.Get(key, fallback, korean) ?? fallback;
        }

        public static string Display(string source)
        {
            if (string.IsNullOrEmpty(source)) return source;
            EnsureLoaded();
            if (catalog == null) return source;
            if (WaitingRoom.WaitingRoomText.TryGetFormat(source, out _) || catalog.UsesReferences(source, korean))
                return catalog.Display(source, korean);
            if (cache.TryGetValue(source, out string value)) return value;
            value = catalog.Display(source, korean);
            if (cache.Count >= 2048) cache.Clear();
            cache.Add(source, value);
            return value;
        }
    }
}

