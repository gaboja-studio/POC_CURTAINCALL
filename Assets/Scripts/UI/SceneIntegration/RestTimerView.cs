using CurtainCall.Campaign;
using CurtainCall.UI.RestTimerV28;
using UnityEngine;
using PackageView = CurtainCall.UI.RestTimerV28.RestTimerView;

namespace CurtainCall.UI.SceneIntegration
{
    [RequireComponent(typeof(PackageView))]
    public sealed class RestTimerView : MonoBehaviour
    {
        public const string PrefabPath = "Prefabs/UIs/RestTimerV28/W1_RestTimer_v2_8";
        PackageView view;
        public PackageView View => view != null ? view : view = GetComponent<PackageView>();
        public bool IsShown => View.RequestedVisible;
        public string DayText => View.DayText;
        public string RemainingText => View.CountdownText;
        public RestTimerSnapshot Snapshot => View.Snapshot;
        public void Show(in CampaignSnapshot state, float durationSeconds) => View.SetSnapshot(RestTimer.Read(state, durationSeconds));
        public void Hide()
        {
            var snapshot = View.Snapshot;
            snapshot.visible = false;
            View.SetSnapshot(snapshot, false);
        }
    }
}
