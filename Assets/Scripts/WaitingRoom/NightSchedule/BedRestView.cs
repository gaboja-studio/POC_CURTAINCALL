using CurtainCall.Campaign;
using CurtainCall.Player;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CurtainCall.WaitingRoom
{
    // Local presentation only. Ready authority and the all-client black-screen barrier remain in CampaignRun.
    public sealed class BedRestView : MonoBehaviour
    {
        CinemachineCamera view;
        UIInputBlocker blocker;
        Quaternion standing, lying;
        float blend;
        int day;
        bool morning;
        public bool IsResting => view != null;
        public void Begin(Bounds bed, Quaternion rotation)
        {
            if (view != null) return;
            day = CampaignRun.Instance.State.Day;
            morning = false; blend = 0;
            blocker = gameObject.AddComponent<UIInputBlocker>();
            blocker.BlockWhileActive = false; blocker.SetOpen(true);
            var go = new GameObject("BedRestCamera"); go.transform.SetParent(transform, false);
            view = go.AddComponent<CinemachineCamera>(); view.Priority = 220;
            view.transform.position = bed.center + Vector3.up * .6f;
            standing = Camera.main != null ? Camera.main.transform.rotation : rotation;
            lying = Quaternion.Euler(-75, rotation.eulerAngles.y, 0);
            view.transform.rotation = standing;
        }
        void Update()
        {
            if (view == null) return;
            var run = CampaignRun.Instance;
            if (run == null) { End(); return; }
            var s = run.State;
            if (s.Phase == CampaignPhase.Night && run.IsLocalReady)
            {
                var k = Keyboard.current;
                if (blend > .2f && k != null && (k.fKey.wasPressedThisFrame || k.escapeKey.wasPressedThisFrame)) run.SetReady(false);
            }
            bool asleep = s.Phase == CampaignPhase.Dawn || (s.Phase == CampaignPhase.Night && run.IsLocalReady);
            if (s.Day > day && s.Phase == CampaignPhase.RestTime) morning = true;
            if (!asleep && !morning) { End(); return; }
            blend = Mathf.MoveTowards(blend, morning ? 0 : 1, Time.unscaledDeltaTime / .8f);
            view.transform.rotation = Quaternion.Slerp(standing, lying, Mathf.SmoothStep(0, 1, blend));
            if (morning && blend <= 0) End();
        }
        void End()
        {
            if (view != null)
            {
                var player = CurtainCall.Network.PlayerSync.NetworkPlayer.Local;
                if (player != null) player.SetRestingBed(-1);
            }
            if (view != null) Destroy(view.gameObject);
            view = null;
            if (blocker != null) { blocker.SetOpen(false); Destroy(blocker); }
        }
        void OnDisable() => End();
    }
}
