using CurtainCall.UI.SceneIntegration;
using CurtainCall.UI.TooltipsV1;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 튜토리얼 가이드 전용 패널(2026-10-31 사용자: 가이드와 나레이션은 다른 패널). 툴팁 정본(Tooltips 프리팹)의 패널 모드를
    /// 화면 왼쪽(탐색 시계 아래)에 띄운다. 나레이션(인간 대포 획득·신체 복구 예약 등)은 그대로 <see cref="WaitingRoomGuidePanel"/>을 쓴다.
    /// 입력을 막지 않고 정해진 시간 뒤 사라진다. 새 가이드가 오면 앞 가이드를 바꾼다.
    /// </summary>
    public sealed class WaitingRoomTutorialPanel : MonoBehaviour
    {
        /// <summary>패널 수치 파일: Assets/Resources/Settings/WaitingRoom/WaitingRoomTutorialPanel.txt (`이름 = 값`, 사용자가 직접 수정).</summary>
        const string SettingsPath = "Settings/WaitingRoom/WaitingRoomTutorialPanel";

        static WaitingRoomTutorialPanel instance;
        static WaitingRoomTutorialPanelSettings settings;

        /// <summary>위치·너비·표시 시간·간격·글자 크기. 플레이마다 한 번 읽는다.</summary>
        public static WaitingRoomTutorialPanelSettings Settings
        {
            get
            {
                if (settings != null) return settings;
                var file = Resources.Load<TextAsset>(SettingsPath);
                settings = WaitingRoomTutorialPanelSettings.Parse(file != null ? file.text : null);
                return settings;
            }
        }
        HudRoleCard latest;
        public static bool IsVisible => instance != null && instance.latest != null && !instance.latest.Closing;
        public static string CurrentTitle => IsVisible ? instance.latest.Title : "";
        public static string CurrentBody => IsVisible ? instance.latest.Content : "";
        public static void KeepVisible() { if (IsVisible) instance.latest.Extend(1); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; settings = null; }

        /// <param name="seconds">0 이하면 설정 파일의 기본 표시 시간.</param>
        public static bool Show(string heading, string text, float seconds,string key=null)=>ShowAs(RoomNotice.Guide,heading,text,seconds,key);
        public static bool ShowAs(RoomNotice role,string heading,string text,float seconds,string key=null)
        {
            if (instance == null)
            {
                var go = new GameObject(nameof(WaitingRoomTutorialPanel));
                DontDestroyOnLoad(go);
                instance = go.AddComponent<WaitingRoomTutorialPanel>();
            }
            // 스케줄표를 보는 동안에는 새 안내를 띄우지 않는다. 튜토리얼은 자기 대기열 순서대로 닫은 뒤 다시 시도한다.
            if (ScheduleScreenHold.IsHeld) return false;
            instance.latest=WaitingRoomHudProfile.Emit(role,instance.transform,heading,text,seconds > 0 ? seconds : Settings.Seconds,key);
            return true;
        }
        public static void Hide()
        {
            if(!instance)return;
            HudUIManager.DismissOwned(instance.transform,HudRole.Guide);
            HudUIManager.DismissOwned(instance.transform,HudRole.SystemEvent);
            HudUIManager.DismissOwned(instance.transform,HudRole.Feedback);
            instance.latest=null;
        }
        void OnDestroy() { if (instance == this) instance = null; }
    }
}
