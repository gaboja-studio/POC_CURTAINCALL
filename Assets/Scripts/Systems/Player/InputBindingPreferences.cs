using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CurtainCall.Player
{
    /// <summary>
    /// 로컬 Common 키 설정. Draft는 비활성 복제본이며 호출자가 Destroy해야 한다.
    /// UI는 draft에 override를 편집한 뒤 TryApply한다. Escape는 취소, M은 마이크 전용이다.
    /// VoiceChat이 직접 읽는 M은 재지정하지 않으며 Common 밖의 키는 관리하지 않는다.
    /// </summary>
    public static class InputBindingPreferences
    {
        public const string PlayerPrefsKey = "CurtainCall.Input.CommonBindings.v1";
        public const string ResourcePath = "Input/PlayerControls";
        public const string MapName = "Common";
        public static event Action Changed;

        public readonly struct BindingEntry
        {
            public readonly Guid ActionId;
            public readonly Guid BindingId;
            public readonly string ActionName;
            public readonly string PartName;
            public readonly int BindingIndex; // action.bindings의 인덱스 (map 인덱스 아님)
            public readonly string Path;
            public readonly string DisplayName;
            public readonly bool IsPartOfComposite;

            internal BindingEntry(InputAction action, int index)
            {
                var binding = action.bindings[index];
                ActionId = action.id;
                BindingId = binding.id;
                ActionName = action.name;
                PartName = binding.name;
                BindingIndex = index;
                Path = binding.effectivePath;
                DisplayName = action.GetBindingDisplayString(index);
                IsPartOfComposite = binding.isPartOfComposite;
            }
        }

        [Serializable] sealed class SavedBindings
        {
            public int version = 1;
            public List<SavedBinding> bindings = new();
        }

        [Serializable] sealed class SavedBinding
        {
            public string actionId;
            public string bindingId;
            public string path;
        }

        /// <summary>현재 저장값을 반영한 독립 draft. 취소는 저장하지 않고 Destroy한다.</summary>
        public static InputActionAsset CreateDraft()
        {
            var draft = CreateDefaultDraft();
            if (draft == null) return null;
            try
            {
                var json = PlayerPrefs.GetString(PlayerPrefsKey, "");
                if (!string.IsNullOrEmpty(json))
                {
                    var saved = JsonUtility.FromJson<SavedBindings>(json);
                    if (saved == null || saved.version != 1 || saved.bindings == null)
                        throw new ArgumentException("Invalid binding preferences");
                    foreach (var entry in saved.bindings)
                    {
                        if (entry == null) throw new ArgumentException("Invalid binding entry");
                        var action = FindAction(draft, entry.actionId);
                        if (action == null) continue; // 에셋 개정으로 사라진 GUID는 무시한다.
                        int index = FindBinding(action, entry.bindingId);
                        if (index < 0 || action.bindings[index].isComposite) continue;
                        if (!TryValidateKeyPath(entry.path, out var path, out _))
                            throw new ArgumentException("Invalid saved key");
                        action.ApplyBindingOverride(index, path);
                    }
                    if (!TryValidateDraft(draft, out _)) draft.RemoveAllBindingOverrides();
                }
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                // 손상된/구버전 저장값 때문에 조작을 잃지 않게 기본값으로 복구한다. 읽기는 저장을 바꾸지 않는다.
                draft.RemoveAllBindingOverrides();
            }
            return draft;
        }

        /// <summary>저장값을 무시한 기본키 draft. 원본 Resources 에셋은 수정하지 않는다.</summary>
        public static InputActionAsset CreateDefaultDraft()
        {
            var source = Resources.Load<InputActionAsset>(ResourcePath);
            if (source == null) return null;
            var draft = UnityEngine.Object.Instantiate(source);
            draft.Disable();
            draft.RemoveAllBindingOverrides();
            return draft;
        }

        /// <summary>Composite 헤더는 제외하고 키 단위 part를 포함한다.</summary>
        public static IReadOnlyList<BindingEntry> GetBindings(InputActionAsset draft)
        {
            var entries = new List<BindingEntry>();
            var map = draft?.FindActionMap(MapName);
            if (map == null) return entries;
            foreach (var action in map.actions)
                for (int i = 0; i < action.bindings.Count; i++)
                    if (!action.bindings[i].isComposite &&
                        TryValidateKeyPath(action.bindings[i].path, out _, out _))
                        entries.Add(new BindingEntry(action, i));
            return entries;
        }

        /// <summary>실제 Keyboard Key 이름만 허용. wildcard/usage/chord/장치별 경로/ESC는 거절한다. 장치 연결 불필요.</summary>
        public static bool TryValidateKeyPath(string path, out string normalizedPath, out string error)
        {
            normalizedPath = null;
            error = "키보드의 단일 키를 선택해 주세요.";
            const string prefix = "<Keyboard>/";
            if (string.IsNullOrEmpty(path) || !path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
            var name = path.Substring(prefix.Length);
            if (string.Equals(name, "escape", StringComparison.OrdinalIgnoreCase))
            {
                error = "Escape는 메뉴 취소용으로 예약되어 있습니다.";
                return false;
            }
            if (string.Equals(name, "m", StringComparison.OrdinalIgnoreCase))
            {
                error = "M은 마이크 토글용으로 예약되어 있습니다.";
                return false;
            }
            var layout = InputSystem.LoadLayout("Keyboard");
            if (layout == null) return false;
            foreach (var control in layout.controls)
            {
                if (!string.Equals(control.layout.ToString(), "Key", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(control.name.ToString(), name, StringComparison.OrdinalIgnoreCase)) continue;
                normalizedPath = prefix + control.name;
                error = null;
                return true;
            }
            return false;
        }

        /// <summary>즉시 중복 검사 후 draft만 변경한다. 직접 ApplyBindingOverride해도 TryApply가 같은 검사를 수행한다.</summary>
        public static bool TryRebind(InputActionAsset draft, Guid bindingId, string path, out string error)
        {
            if (!TryValidateKeyPath(path, out var normalized, out error)) return false;
            BindingEntry? selected = null;
            foreach (var entry in GetBindings(draft))
            {
                if (entry.BindingId == bindingId) selected = entry;
                else if (string.Equals(entry.Path, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    error = $"이미 {entry.ActionName} {entry.PartName}에 배정된 키입니다. 다른 키를 선택해 주세요.";
                    return false;
                }
            }
            if (!selected.HasValue)
            {
                error = "변경할 키 바인딩을 찾을 수 없습니다.";
                return false;
            }
            var binding = selected.Value;
            draft.FindActionMap(MapName).FindAction(binding.ActionId.ToString(), true)
                .ApplyBindingOverride(binding.BindingIndex, normalized);
            error = null;
            return true;
        }

        /// <summary>전체 Common에서 중복 키를 거절한다(part끼리도 동일). 자동 교환/기존 배정 해제는 하지 않는다.</summary>
        public static bool TryValidateDraft(InputActionAsset draft, out string error)
        {
            error = "기본 Common 맵과 호환되는 draft가 필요합니다.";
            var source = Resources.Load<InputActionAsset>(ResourcePath);
            var defaults = source?.FindActionMap(MapName);
            var map = draft?.FindActionMap(MapName);
            if (defaults == null || map == null || defaults.id != map.id) return false;
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var original in defaults.actions)
            {
                var action = map.FindAction(original.id.ToString());
                if (action == null || action.bindings.Count != original.bindings.Count) return false;
                for (int i = 0; i < original.bindings.Count; i++)
                {
                    var binding = action.bindings[i];
                    var baseline = original.bindings[i];
                    error = "기본 Common 맵의 키 경로 override만 변경할 수 있습니다.";
                    if (binding.id != baseline.id || binding.path != baseline.path ||
                        binding.isComposite != baseline.isComposite || binding.isPartOfComposite != baseline.isPartOfComposite ||
                        binding.overrideInteractions != null || binding.overrideProcessors != null) return false;
                    if (baseline.isComposite)
                    {
                        if (binding.overridePath != null) return false;
                        continue;
                    }
                    if (!TryValidateKeyPath(binding.effectivePath, out var normalized, out error)) return false;
                    if (!used.Add(normalized))
                    {
                        error = $"{normalized} 키가 중복 배정되어 있습니다. 다른 키를 선택해 주세요.";
                        return false;
                    }
                }
            }
            error = null;
            return true;
        }

        /// <summary>검증 성공 시에만 저장하고 모든 Reader(비활성 포함)의 복제본에 즉시 반영한다.</summary>
        public static bool TryApply(InputActionAsset draft, out string error)
        {
            if (!TryValidateDraft(draft, out error)) return false;
            var saved = new SavedBindings();
            foreach (var entry in GetBindings(draft))
            {
                TryValidateKeyPath(entry.Path, out var path, out _);
                saved.bindings.Add(new SavedBinding
                {
                    actionId = entry.ActionId.ToString(), bindingId = entry.BindingId.ToString(), path = path
                });
            }
            PlayerPrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(saved));
            PlayerPrefs.Save();
            Changed?.Invoke();
            InputGate.RequireRelease();
            return true;
        }

        /// <summary>즉시 저장값을 초기화한다. 적용 전 미리보기는 draft.RemoveAllBindingOverrides()를 쓴다.</summary>
        public static void ResetDefaults()
        {
            PlayerPrefs.DeleteKey(PlayerPrefsKey);
            PlayerPrefs.Save();
            Changed?.Invoke();
            InputGate.RequireRelease();
        }

        /// <summary>커스텀 asset에도 action/binding GUID가 모두 일치하는 키만 적용한다. 나머지는 보존한다.</summary>
        public static void ApplySavedOverrides(InputActionAsset target)
        {
            if (target == null) return;
            var draft = CreateDraft();
            if (draft == null) return;
            try
            {
                foreach (var entry in GetBindings(draft))
                {
                    var action = FindAction(target, entry.ActionId.ToString());
                    int index = FindBinding(action, entry.BindingId.ToString());
                    if (index < 0 || action.bindings[index].isComposite) continue;
                    if (string.Equals(action.bindings[index].path, entry.Path, StringComparison.OrdinalIgnoreCase))
                        action.RemoveBindingOverride(index);
                    else action.ApplyBindingOverride(index, entry.Path);
                }
            }
            finally { DestroyDraft(draft); }
        }

        static InputAction FindAction(InputActionAsset asset, string id)
        {
            if (!Guid.TryParse(id, out _)) return null;
            return asset.FindActionMap(MapName)?.FindAction(id);
        }

        static int FindBinding(InputAction action, string id)
        {
            if (action == null || !Guid.TryParse(id, out var guid)) return -1;
            for (int i = 0; i < action.bindings.Count; i++)
                if (action.bindings[i].id == guid) return i;
            return -1;
        }

        static void DestroyDraft(InputActionAsset draft)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(draft);
            else UnityEngine.Object.DestroyImmediate(draft);
        }
    }
}
