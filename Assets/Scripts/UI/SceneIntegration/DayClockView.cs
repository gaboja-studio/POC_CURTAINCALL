using CurtainCall.Campaign;
using CurtainCall.UI.DayClockV2;
using UnityEngine;
using PackageView = CurtainCall.UI.DayClockV2.DayClockView;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>제공된 하루 시계 프리팹과 게임 상태를 연결한다. 그림을 코드로 재생성하지 않는다.</summary>
    [RequireComponent(typeof(PackageView))]
    public sealed class DayClockView : MonoBehaviour
    {
        public const string PrefabPath = "Prefabs/UIs/DayClock/W1_DayClock_v2_7";
        PackageView view;
        public PackageView View => view != null ? view : view = GetComponent<PackageView>();
        public bool IsShown => View.RequestedVisible;
        public string DayText => View.HeaderText;
        public string RemainingText => View.CountdownText;
        public DayClockSnapshot Snapshot => View.Snapshot;

        public void Show(in CampaignSnapshot state, float totalSeconds)
        {
            if (DayClock.TryRead(state, totalSeconds, View.AcceptedSchedule, out var snapshot))
                View.SetSnapshot(snapshot);
            else Hide();
        }

        public void Hide() => View.SetVisible(false);
    }
}
