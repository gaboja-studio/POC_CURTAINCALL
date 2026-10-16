using System;
using UnityEngine;

namespace CurtainCall.Dev
{
    /// <summary>
    /// 1인 테스트용 개발자 모드 스위치(run-rules.md #5). 시작 인원 규칙을 예외 처리하고, 판정은 호스트가 이 값을 읽어 한다.
    /// 켜는 방법: 에디터는 메뉴 Tools/CurtainCall/Dev Mode(이 PC에만 저장), 빌드는 실행 인자 <c>-devmode</c>. 기본은 꺼짐.
    /// 게임 진행 중에는 바뀌지 않는다(에디터 메뉴는 Play 중 잠김, 빌드는 시작 때 한 번 읽음).
    /// </summary>
    public static class DevMode
    {
        public const string CommandLineArg = "-devmode";
        public const string EditorPrefsKey = "CurtainCall.DevMode";

        static bool? _enabled;

        /// <summary>개발자 모드가 켜져 있는지.</summary>
        public static bool Enabled => _enabled ??= ReadInitial();

        /// <summary>켜짐/꺼짐이 바뀔 때(에디터 메뉴에서만 일어난다).</summary>
        public static event Action<bool> Changed;

        /// <summary>
        /// 게임 시작 최소 인원(호스트 포함). 개발자 모드면 1명. 정식값은 세팅의 최소 인원을 넘긴다.
        /// </summary>
        public static int MinPlayers(int official) => Enabled ? Mathf.Min(1, official) : official;

        /// <summary>
        /// 묘기를 계속하는 최소 생존자 수. 현재 공통 정식값도 1명이므로 개발자 모드와 같은 기준이다.
        /// </summary>
        public static int MinSurvivors(int official) => Enabled ? Mathf.Min(1, official) : official;

#if UNITY_EDITOR
        /// <summary>에디터 메뉴 전용. Play 중에는 바꾸지 않는다.</summary>
        public static void SetEditorEnabled(bool value)
        {
            UnityEditor.EditorPrefs.SetBool(EditorPrefsKey, value);
            if (_enabled == value) return;
            _enabled = value;
            Changed?.Invoke(value);
        }
#endif

        // 도메인 리로드 없이 Play에 들어가도 다시 읽게 한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => _enabled = null;

        static bool ReadInitial()
        {
#if UNITY_EDITOR
            return UnityEditor.EditorPrefs.GetBool(EditorPrefsKey, false);
#else
            return Array.IndexOf(Environment.GetCommandLineArgs(), CommandLineArg) >= 0;
#endif
        }
    }
}
