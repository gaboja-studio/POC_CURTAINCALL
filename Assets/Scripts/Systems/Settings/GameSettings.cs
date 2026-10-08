using UnityEngine;

namespace CurtainCall.Settings
{
    /// <summary>
    /// 기획 조정값의 시작점. 게임 기본 세팅과 묘기별 세팅을 연결만 한다.
    /// 에셋: Assets/Resources/Settings/GameSettings/GameSettings.asset. 결정: Harness/Project/Decisions/game-settings.md
    /// 코드는 <see cref="Base"/>·<see cref="Tightrope"/>로 읽는다. 플레이 중 수정이 바로 반영되도록 값은 쓸 때마다 읽는다.
    /// 에셋이 없으면 코드 기본값(= 2026-10-03 프리팹 값)으로 만든 임시 세팅을 쓰고 경고한다.
    /// </summary>
    [CreateAssetMenu(menuName = "CurtainCall/Settings/Game Settings", fileName = "GameSettings")]
    public sealed class GameSettings : ScriptableObject
    {
        const string ResourcePath = "Settings/GameSettings/GameSettings";

        [Tooltip("게임 기본 세팅(세션·캐릭터·입력·균형 공통·연출·동기화).")]
        [SerializeField] BaseGameSettings baseSettings;

        [Tooltip("외줄 묘기 세팅(코스·진행·줄 위 동작·목마·보상).")]
        [SerializeField] TightropeSettings tightrope;

        static GameSettings current;
        static BaseGameSettings fallbackBase;
        static TightropeSettings fallbackTightrope;

        /// <summary>지금 쓰는 시작점 세팅. 처음 부를 때 Resources에서 읽는다.</summary>
        public static GameSettings Current
        {
            get
            {
                if (current != null) return current;
                current = Resources.Load<GameSettings>(ResourcePath);
                if (current == null)
                {
                    Debug.LogWarning($"[Settings] Resources/{ResourcePath} 세팅을 찾지 못해 코드 기본값을 씁니다.");
                    current = CreateInstance<GameSettings>();
                    current.hideFlags = HideFlags.DontSave;
                }
                return current;
            }
        }

        /// <summary>게임 기본 세팅.</summary>
        public static BaseGameSettings Base
        {
            get
            {
                var settings = Current.baseSettings;
                if (settings != null) return settings;
                return fallbackBase != null ? fallbackBase : fallbackBase = CreateFallback<BaseGameSettings>();
            }
        }

        /// <summary>외줄 묘기 세팅.</summary>
        public static TightropeSettings Tightrope
        {
            get
            {
                var settings = Current.tightrope;
                if (settings != null) return settings;
                return fallbackTightrope != null ? fallbackTightrope : fallbackTightrope = CreateFallback<TightropeSettings>();
            }
        }

        static T CreateFallback<T>() where T : ScriptableObject
        {
            Debug.LogWarning($"[Settings] GameSettings에 {typeof(T).Name}이 연결되지 않아 코드 기본값을 씁니다.");
            var settings = CreateInstance<T>();
            settings.hideFlags = HideFlags.DontSave;
            return settings;
        }
    }
}
