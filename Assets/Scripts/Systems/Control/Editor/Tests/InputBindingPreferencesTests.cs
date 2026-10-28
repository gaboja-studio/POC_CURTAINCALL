using System.Collections.Generic;
using System.Reflection;
using CurtainCall.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CurtainCall.Control.Editor.Tests
{
    public sealed class InputBindingPreferencesTests
    {
        readonly List<Object> owned = new();
        bool hadPreferences;
        string previousPreferences;

        [SetUp]
        public void SetUp()
        {
            hadPreferences = PlayerPrefs.HasKey(InputBindingPreferences.PlayerPrefsKey);
            previousPreferences = PlayerPrefs.GetString(InputBindingPreferences.PlayerPrefsKey);
            PlayerPrefs.DeleteKey(InputBindingPreferences.PlayerPrefsKey);
        }

        [TearDown]
        public void TearDown()
        {
            // Reader를 먼저 제거해서 이후 Preferences 변경을 받지 않게 한다.
            for (int i = owned.Count - 1; i >= 0; i--)
            {
                if (owned[i] == null) continue;
                // 한 번도 활성화되지 않은 EditMode 테스트 GO에는 Unity가 OnDestroy를 보내지 않는다.
                if (owned[i] is GameObject go && go.TryGetComponent(out PlayerInputReader reader))
                    typeof(PlayerInputReader).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(reader, null);
                Object.DestroyImmediate(owned[i]);
            }
            owned.Clear();
            if (hadPreferences) PlayerPrefs.SetString(InputBindingPreferences.PlayerPrefsKey, previousPreferences);
            else PlayerPrefs.DeleteKey(InputBindingPreferences.PlayerPrefsKey);
            PlayerPrefs.Save();
        }

        InputActionAsset Draft()
        {
            var draft = InputBindingPreferences.CreateDraft();
            Assert.NotNull(draft);
            owned.Add(draft);
            return draft;
        }

        [Test]
        public void Draft_ListsCompositeParts_AndDoesNotMutateResourceOrSaveOnCancel()
        {
            var draft = Draft();
            Assert.IsFalse(draft.enabled);
            var entries = InputBindingPreferences.GetBindings(draft);
            Assert.AreEqual(10, entries.Count);
            int parts = 0;
            foreach (var entry in entries) if (entry.IsPartOfComposite) parts++;
            Assert.AreEqual(4, parts);
            var move = draft.FindAction("Common/Move");
            Assert.IsTrue(InputBindingPreferences.TryRebind(draft, move.bindings[1].id, "<Keyboard>/g", out _));
            Assert.AreEqual("<Keyboard>/g", move.bindings[1].effectivePath);
            Assert.AreEqual("<Keyboard>/s", Resources.Load<InputActionAsset>(InputBindingPreferences.ResourcePath)
                .FindAction("Common/Move").bindings[1].effectivePath);
            Assert.AreEqual("<Keyboard>/s", Draft().FindAction("Common/Move").bindings[1].effectivePath);
            Assert.IsFalse(PlayerPrefs.HasKey(InputBindingPreferences.PlayerPrefsKey));
        }

        [TestCase("<Keyboard>/escape")]
        [TestCase("<Keyboard>/ESCAPE")]
        [TestCase("<Keyboard>/m")]
        [TestCase("<Keyboard>/M")]
        [TestCase("<Keyboard>/anyKey")]
        [TestCase("<Keyboard>/*")]
        [TestCase("<Keyboard>/notAKey")]
        [TestCase("<Keyboard>/shift")]
        [TestCase("<Mouse>/leftButton")]
        [TestCase("/Keyboard/g")]
        [TestCase("")]
        public void InvalidOrReservedPaths_AreRejected(string path)
        {
            Assert.IsFalse(InputBindingPreferences.TryValidateKeyPath(path, out _, out var error));
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void KeyboardValidation_DoesNotRequireConnectedKeyboard_AndNormalizesCase()
        {
            Assert.IsTrue(InputBindingPreferences.TryValidateKeyPath("<keyboard>/LEFTSHIFT", out var normalized, out _));
            Assert.AreEqual("<Keyboard>/leftShift", normalized);
        }

        [Test]
        public void DirectOverrides_AreValidatedAtomically_AndDuplicatePolicyIncludesParts()
        {
            var draft = Draft();
            var action = draft.FindAction("Common/Action");
            Assert.IsFalse(InputBindingPreferences.TryRebind(draft, action.bindings[0].id, "<Keyboard>/w", out var error));
            Assert.IsNotEmpty(error);
            Assert.AreEqual("<Keyboard>/space", action.bindings[0].effectivePath);
            action.ApplyBindingOverride(0, "<Keyboard>/w");
            Assert.IsFalse(InputBindingPreferences.TryApply(draft, out error));
            Assert.IsNotEmpty(error);
            Assert.IsFalse(PlayerPrefs.HasKey(InputBindingPreferences.PlayerPrefsKey));
            action.ApplyBindingOverride(0, "<Keyboard>/escape");
            Assert.IsFalse(InputBindingPreferences.TryApply(draft, out _));
        }

        [Test]
        public void SaveLoad_ResetAndMalformedData_UseSafeDefaults()
        {
            var draft = Draft();
            draft.FindAction("Common/Action").ApplyBindingOverride(0, "<Keyboard>/g");
            Assert.IsTrue(InputBindingPreferences.TryApply(draft, out var error), error);
            Assert.AreEqual("<Keyboard>/g", Draft().FindAction("Common/Action").bindings[0].effectivePath);
            Assert.IsTrue(InputGate.IsBlocked);
            InputBindingPreferences.ResetDefaults();
            Assert.IsFalse(PlayerPrefs.HasKey(InputBindingPreferences.PlayerPrefsKey));
            Assert.AreEqual("<Keyboard>/space", Draft().FindAction("Common/Action").bindings[0].effectivePath);
            PlayerPrefs.SetString(InputBindingPreferences.PlayerPrefsKey, "not-json");
            Assert.AreEqual("<Keyboard>/space", Draft().FindAction("Common/Action").bindings[0].effectivePath);
        }

        [Test]
        public void CustomAsset_OnlyMatchingActionAndBindingGuidsAreTouched()
        {
            var draft = Draft();
            draft.FindAction("Common/Action").ApplyBindingOverride(0, "<Keyboard>/g");
            Assert.IsTrue(InputBindingPreferences.TryApply(draft, out _));
            var compatible = Draft();
            compatible.RemoveAllBindingOverrides();
            InputBindingPreferences.ApplySavedOverrides(compatible);
            Assert.AreEqual("<Keyboard>/g", compatible.FindAction("Common/Action").bindings[0].effectivePath);
            var replacedBinding = compatible.FindAction("Common/Action");
            replacedBinding.ChangeBinding(0).Erase();
            replacedBinding.AddBinding("<Keyboard>/h");
            replacedBinding.ApplyBindingOverride(0, "<Keyboard>/j");
            InputBindingPreferences.ApplySavedOverrides(compatible);
            Assert.AreEqual("<Keyboard>/j", replacedBinding.bindings[0].effectivePath, "액션 GUID만 같고 binding GUID가 다르면 보존");

            var custom = ScriptableObject.CreateInstance<InputActionAsset>();
            owned.Add(custom);
            var action = custom.AddActionMap("Common").AddAction("Action", InputActionType.Button, "<Keyboard>/h");
            action.ApplyBindingOverride(0, "<Keyboard>/j");
            InputBindingPreferences.ApplySavedOverrides(custom);
            Assert.AreEqual("<Keyboard>/j", action.bindings[0].effectivePath);
        }

        [Test]
        public void AllExistingReaders_IncludingDisabledClones_RefreshOnApplyAndReset()
        {
            var first = CreateReader();
            var second = CreateReader();
            second.enabled = false;
            var draft = Draft();
            draft.FindAction("Common/Action").ApplyBindingOverride(0, "<Keyboard>/g");
            Assert.IsTrue(InputBindingPreferences.TryApply(draft, out _));
            AssertReaderPath(first, "<Keyboard>/g");
            AssertReaderPath(second, "<Keyboard>/g");
            InputBindingPreferences.ResetDefaults();
            AssertReaderPath(first, "<Keyboard>/space");
            AssertReaderPath(second, "<Keyboard>/space");
        }

        PlayerInputReader CreateReader()
        {
            var go = new GameObject("Binding preferences test reader");
            go.SetActive(false);
            owned.Add(go);
            var reader = go.AddComponent<PlayerInputReader>();
            typeof(PlayerInputReader).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(reader, null);
            // EditMode에서는 Reader의 Destroy 예약에 의존하지 않고 복제본도 직접 정리한다.
            owned.Add((InputActionAsset)typeof(PlayerInputReader).GetField("actions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(reader));
            return reader;
        }

        static void AssertReaderPath(PlayerInputReader reader, string expected)
        {
            var actions = (InputActionAsset)typeof(PlayerInputReader)
                .GetField("actions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(reader);
            Assert.AreEqual(expected, actions.FindAction("Common/Action").bindings[0].effectivePath);
        }
    }
}
