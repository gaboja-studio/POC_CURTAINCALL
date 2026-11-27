using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CurtainCall.Localization
{
    // The gameplay and network layers continue to own the original text. TMP only
    // receives the localized copy when it measures or renders the label.
    public sealed class LocalizedDisplays : MonoBehaviour
    {
        sealed class Processor : ITextPreprocessor
        {
            readonly ITextPreprocessor previous;
            public Processor(ITextPreprocessor previous) { this.previous = previous; }
            public string PreprocessText(string text)
            {
                string result = GameLanguage.Display(text);
                return previous?.PreprocessText(result) ?? result;
            }
        }

        static LocalizedDisplays instance;
        static ConditionalWeakTable<TMP_Text, object> known = new ConditionalWeakTable<TMP_Text, object>();
        readonly HashSet<TMP_Text> pending = new HashSet<TMP_Text>();
        bool bindingsChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void StartListening()
        {
            if (instance) return;
            known = new ConditionalWeakTable<TMP_Text, object>();
            var owner = new GameObject("Game language");
            DontDestroyOnLoad(owner);
            instance = owner.AddComponent<LocalizedDisplays>();
            SceneManager.sceneLoaded += SceneLoaded;
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(TextChanged);
            GameLanguage.Changed += Refresh;
            Player.InputBindingPreferences.Changed += instance.BindingsChanged;
        }

        static void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var label in root.GetComponentsInChildren<TMP_Text>(true)) Bind(label);
                foreach (var label in root.GetComponentsInChildren<Text>(true)) BindLegacy(label);
                foreach (var label in root.GetComponentsInChildren<TextMesh>(true)) BindLegacy(label);
            }
        }

        static void TextChanged(Object sender)
        {
            if (sender is TMP_Text label && label && !known.TryGetValue(label, out _))
                instance.pending.Add(label);
        }

        void LateUpdate()
        {
            if (bindingsChanged)
            {
                bindingsChanged = false;
                Refresh();
            }
            if (pending.Count == 0) return;
            foreach (var label in pending) if (label) Bind(label);
            pending.Clear();
        }

        void BindingsChanged() => bindingsChanged = true;

        public static void Bind(TMP_Text label)
        {
            if (!Application.isPlaying || !label || known.TryGetValue(label, out _)) return;
            known.Add(label, new object());
            // A placeholder is authored UI; the editable value belongs to the player.
            var field = label.GetComponentInParent<TMP_InputField>(true);
            if (field && field.textComponent == label) return;
            label.textPreprocessor = new Processor(label.textPreprocessor);
            label.havePropertiesChanged = true;
            label.SetAllDirty();
        }

        public static T BindLegacy<T>(T label) where T : Component
        {
            if (!Application.isPlaying || !label) return label;
            var field = label.GetComponentInParent<InputField>(true);
            if (field && field.textComponent == label) return label;
            if (!label.TryGetComponent<LocalizedLegacyLabel>(out var binding))
                binding = label.gameObject.AddComponent<LocalizedLegacyLabel>();
            binding.Track(label);
            return label;
        }

        static void Refresh()
        {
            foreach (var label in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Bind(label);
                label.havePropertiesChanged = true;
                label.SetAllDirty();
            }
            foreach (var label in FindObjectsByType<LocalizedLegacyLabel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                label.Refresh();
        }

        void OnDestroy()
        {
            if (instance != this) return;
            SceneManager.sceneLoaded -= SceneLoaded;
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(TextChanged);
            GameLanguage.Changed -= Refresh;
            Player.InputBindingPreferences.Changed -= BindingsChanged;
            instance = null;
        }
    }
}

