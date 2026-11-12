using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CurtainCall.Player
{
    /// <summary>
    /// 게임 입력 막기(공용). 어느 UI든 <see cref="SetBlocking"/>으로 "열림/닫힘"을 알리고, 요청한 쪽이 하나라도 남아 있으면 막는다.
    /// 막힘이 풀려도 모든 게임 입력(입력 에셋의 공통 역할)과 <see cref="AddReleaseKeys"/>로 등록한 키를 놓은 다음 프레임에 다시 받는다. 물리는 멈추지 않는다.
    /// 게임 입력을 읽는 코드는 <see cref="IsBlocked"/>가 참이면 입력을 버린다(<see cref="PlayerInputReader"/>는 이미 처리).
    /// 씬 UI는 <see cref="UIInputBlocker"/>를 붙이면 된다.
    /// </summary>
    public static class InputGate
    {
        static readonly HashSet<PlayerInputReader> readers = new();
        static readonly HashSet<object> blockers = new();
        static readonly HashSet<Key> releaseKeys = new();
        static bool rearming;
        static int checkedFrame = -1;
        static int blockedFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            readers.Clear();
            blockers.Clear();
            releaseKeys.Clear();
            rearming = false;
            checkedFrame = blockedFrame = -1;
        }

        /// <summary>막아 달라고 요청한 쪽이 하나라도 있는지(다시 받기 전 대기는 빼고).</summary>
        public static bool IsRequested
        {
            get
            {
                if (blockers.Count > 0 && blockers.RemoveWhere(IsDestroyed) > 0 && blockers.Count == 0)
                    RequireRelease();
                return blockers.Count > 0;
            }
        }

        static bool IsDestroyed(object requester) => requester is UnityEngine.Object owner && owner == null;

        /// <summary>지금 게임 입력을 막는지. 요청이 있거나, 풀린 뒤 아직 키를 다 놓지 않았거나, 막힘이 바뀐 그 프레임이면 참.</summary>
        public static bool IsBlocked
        {
            get
            {
                if (IsRequested) return true;
                if (!rearming) return blockedFrame == Time.frameCount;
                if (checkedFrame != Time.frameCount)
                {
                    checkedFrame = Time.frameCount;
                    blockedFrame = Time.frameCount;
                    bool released = true;
                    foreach (var reader in readers)
                        if (reader != null && reader.isActiveAndEnabled && !reader.ControlsReleased)
                            released = false;
                    var keyboard = Keyboard.current;
                    if (keyboard != null)
                        foreach (var key in releaseKeys) released &= !keyboard[key].isPressed;
                    if (released) rearming = false;
                }
                return true;
            }
        }

        /// <summary>
        /// <paramref name="requester"/>(UI 컴포넌트 등)가 입력을 막을지 알린다. 열릴 때 true, 닫힐 때 false.
        /// 같은 요청자는 한 번만 센다. 막힘 여부가 바뀌면 남은 입력을 지우고 키를 놓을 때까지 기다린다.
        /// </summary>
        public static void SetBlocking(object requester, bool blocking)
        {
            if (requester == null) return;
            bool before = IsRequested;
            if (blocking) blockers.Add(requester);
            else blockers.Remove(requester);
            if (before != IsRequested) RequireRelease();
        }

        /// <summary>다시 받기 전에 떼야 하는 키를 더한다(입력 에셋 밖의 개발·디버그 키). 게임 역할 키는 입력 에셋에서 자동으로 본다.</summary>
        public static void AddReleaseKeys(params Key[] keys)
        {
            foreach (var key in keys) releaseKeys.Add(key);
        }

        public static void Register(PlayerInputReader reader)
        {
            readers.Add(reader);
            RequireRelease();
        }

        public static void Unregister(PlayerInputReader reader) => readers.Remove(reader);

        /// <summary>
        /// 진단용: 지금 막고 있는 요청자와 "키를 놓을 때까지 대기" 상태를 한 줄로 적는다. 동작은 바꾸지 않는다.
        /// 파괴됐는데 해제하지 않은 요청자는 "(파괴됨)"으로 표시한다(Task-20261105-108).
        /// </summary>
        public static string Describe()
        {
            var parts = new List<string>();
            foreach (var requester in blockers)
            {
                if (requester is Type type) parts.Add(type.Name);
                else if (requester is UnityEngine.Object unity)
                    parts.Add(unity == null ? requester.GetType().Name + "(파괴됨)"
                        : $"{requester.GetType().Name}({unity.name}{(unity is Component c ? "@" + c.gameObject.scene.name : "")})");
                else parts.Add(requester.GetType().FullName);
            }
            var waiting = new List<string>();
            if (rearming)
            {
                foreach (var reader in readers)
                    if (reader != null && reader.isActiveAndEnabled && !reader.ControlsReleased) waiting.Add(reader.name);
                var keyboard = Keyboard.current;
                if (keyboard != null)
                    foreach (var key in releaseKeys) if (keyboard[key].isPressed) waiting.Add(key.ToString());
            }
            return $"요청자[{string.Join(", ", parts)}] 키놓기대기={rearming}" + (waiting.Count > 0 ? $"[{string.Join(", ", waiting)}]" : "");
        }

        /// <summary>남은 입력을 지우고, 모든 키를 놓을 때까지 입력을 막는다(장면 전환·재시작 직후 등).</summary>
        public static void RequireRelease()
        {
            rearming = true;
            checkedFrame = -1;
            blockedFrame = Time.frameCount;
            foreach (var reader in readers)
                if (reader != null) reader.ClearPendingInput();
        }
    }
}
