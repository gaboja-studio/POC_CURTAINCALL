using UnityEngine;

namespace CurtainCall.Settings
{
    /// <summary>Layer names shared by authored assets and runtime generation. Collision policy lives in Project Settings / Physics.</summary>
    public static class GamePhysicsLayers
    {
        public const string Trampoline = "Trampoline";
        public const string DetachedLimb = "DismemberedLimb";
        public const string TrickHazard = "TrickHazard";

        public static void AssignHierarchy(Transform root, string name)
        {
            int layer = LayerMask.NameToLayer(name);
            if (root == null || layer < 0) return;
            Assign(root, layer);
        }

        static void Assign(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root) Assign(child, layer);
        }
    }

    /// <summary>
    /// 기획 조정값의 시작점. 세팅 5묶음(공통 / 각 묘기 / 대기실 / 탐색 / 탈출로)을 연결만 한다.
    /// 에셋: Assets/Resources/Settings/GameSettings/GameSettings.asset. 결정: Harness/Project/Decisions/game-settings.md
    /// 코드는 <see cref="Base"/>(공통)·<see cref="Tightrope"/>(묘기)·<see cref="WaitingRoom"/>·<see cref="Exploration"/>·<see cref="EscapeRoute"/>로 읽는다.
    /// 플레이 중 수정이 바로 반영되도록 값은 쓸 때마다 읽는다.
    /// 에셋이 없으면 코드 기본값(= 2026-10-03 프리팹 값)으로 만든 임시 세팅을 쓰고 경고한다.
    /// </summary>
    [CreateAssetMenu(menuName = "CurtainCall/Settings/Game Settings", fileName = "GameSettings")]
    public sealed class GameSettings : ScriptableObject
    {
        const string ResourcePath = "Settings/GameSettings/GameSettings";

        [Tooltip("공통 세팅(세션·런 진행·기물 소지·캐릭터·입력·균형 공통·연출·동기화).")]
        [SerializeField] BaseGameSettings baseSettings;

        [Tooltip("외줄 묘기 세팅(코스·진행·줄 위 동작·목마·보상).")]
        [SerializeField] TightropeSettings tightrope;

        [Tooltip("인간대포 코스 세팅(이번 빌드 톱날 배치 비율·사선 각도). 일반 바닥 점프는 Base Settings, 봉은 Poles, 트램펄린은 Trampoline에서 조정한다.")]
        [SerializeField] HumanCannonSettings humanCannon;

        [Tooltip("일반 봉·스위치 봉의 공통 인원(1~4명)·반동 조작·매달림 시간·도약·외형. 이 칸에 연결된 PoleSettings 에셋을 선택해 조정한다.")]
        [SerializeField] PoleSettings poles;
        [Tooltip("모든 씬 공통 트램펄린 반동·운반·운반자 조건.")]
        [SerializeField] TrampolineSettings trampoline;

        [Tooltip("대기실 세팅(상점·소품실·서비스·밤 정비).")]
        [SerializeField] WaitingRoomSettings waitingRoom;

        [Tooltip("탐색 세팅(백스테이지 탐색·탈출 단계·추격자).")]
        [SerializeField] ExplorationSettings exploration;

        [Tooltip("탈출로 세팅(탈출로 구간·추격·엔딩).")]
        [SerializeField] EscapeRouteSettings escapeRoute;

        static GameSettings current;
        static BaseGameSettings fallbackBase;
        static TightropeSettings fallbackTightrope;
        static HumanCannonSettings fallbackHumanCannon;
        static PoleSettings fallbackPoles;
        static TrampolineSettings fallbackTrampoline;
        static WaitingRoomSettings fallbackWaitingRoom;
        static ExplorationSettings fallbackExploration;
        static EscapeRouteSettings fallbackEscapeRoute;

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

        /// <summary>공통 세팅.</summary>
        public static BaseGameSettings Base => Linked(Current.baseSettings, ref fallbackBase);

        /// <summary>외줄 묘기 세팅.</summary>
        public static TightropeSettings Tightrope => Linked(Current.tightrope, ref fallbackTightrope);

        /// <summary>인간대포 묘기 세팅.</summary>
        public static HumanCannonSettings HumanCannon => Linked(Current.humanCannon, ref fallbackHumanCannon);

        /// <summary>일반 봉과 스위치 봉의 씬 공통 설정.</summary>
        public static PoleSettings Poles => Linked(Current.poles, ref fallbackPoles);
        public static TrampolineSettings Trampoline => Linked(Current.trampoline, ref fallbackTrampoline);

        /// <summary>대기실 세팅.</summary>
        public static WaitingRoomSettings WaitingRoom => Linked(Current.waitingRoom, ref fallbackWaitingRoom);

        /// <summary>탐색(백스테이지) 세팅.</summary>
        public static ExplorationSettings Exploration => Linked(Current.exploration, ref fallbackExploration);

        /// <summary>탈출로 세팅.</summary>
        public static EscapeRouteSettings EscapeRoute => Linked(Current.escapeRoute, ref fallbackEscapeRoute);

        static T Linked<T>(T settings, ref T fallback) where T : ScriptableObject
        {
            if (settings != null) return settings;
            return fallback != null ? fallback : fallback = CreateFallback<T>();
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
