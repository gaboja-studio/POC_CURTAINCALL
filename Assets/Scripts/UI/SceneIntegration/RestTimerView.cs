using CurtainCall.Campaign;
using CurtainCall.Sound;
using CurtainCall.UI.RestTimerV28;
using UnityEngine;
using PackageView = CurtainCall.UI.RestTimerV28.RestTimerView;

namespace CurtainCall.UI.SceneIntegration
{
    [RequireComponent(typeof(PackageView))]
    public sealed class RestTimerView : MonoBehaviour
    {
        public const string PrefabPath = "Prefabs/UIs/Roles/Retained/RestTimerV28/W1_RestTimer_v2_8";
        PackageView view;
        readonly RestTimerSoundCues soundCues = new();
        public PackageView View => view != null ? view : view = GetComponent<PackageView>();
        public bool IsShown => View.RequestedVisible;
        public string DayText => View.DayText;
        public string RemainingText => View.CountdownText;
        public RestTimerSnapshot Snapshot => View.Snapshot;
        public void Show(in CampaignSnapshot state, float durationSeconds)
        {
            var snapshot = RestTimer.Read(state, durationSeconds);
            View.SetSnapshot(snapshot);
            // 30초 경고·마지막 10초 틱(UI-905·906). 소리는 사운드 설정 창의 큐에서 고친다.
            string cue = soundCues.Step(snapshot);
            if (cue != null) SoundManager.Play(cue);
        }
        public void Hide()
        {
            soundCues.Reset();
            var snapshot = View.Snapshot;
            snapshot.visible = false;
            View.SetSnapshot(snapshot, false);
        }
    }
}
