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
        // 눕기·일어나기는 지금 시선의 좌우 방향을 그대로 두고 위아래만 움직인다.
        // 침대 방향으로 Slerp하면 시선과 침대 방향이 다를 때 화면이 옆으로 기울며 크게 돈다(Task-082 조사).
        const float LyingPitch = -75f;
        float standingPitch, yaw;
        float blend;
        int day;
        bool morning;
        CurtainCall.Network.PlayerSync.NetworkPlayer restingPlayer;
        bool beganWithNetworkPlayer;
        public bool IsResting => view != null;
        internal static void EndForRehearsal()
        {
            CampaignRun.Instance?.WakeForRehearsal();
            foreach (var rest in FindObjectsByType<BedRestView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                rest.End();
            CurtainCall.Network.PlayerSync.NetworkPlayer.Local?.SetRestingBed(-1);
        }
        public void Begin(Bounds bed, Quaternion rotation)
        {
            if (view != null || CampaignRun.RehearsalPendingOrActive) return;
            day = CampaignRun.Instance.State.Day;
            morning = false; blend = 0;
            restingPlayer = CurtainCall.Network.PlayerSync.NetworkPlayer.Local;
            beganWithNetworkPlayer = restingPlayer != null;
            blocker = gameObject.AddComponent<UIInputBlocker>();
            blocker.BlockWhileActive = false; blocker.SetOpen(true);
            var go = new GameObject("BedRestCamera"); go.transform.SetParent(transform, false);
            view = go.AddComponent<CinemachineCamera>(); view.Priority = 220;
            view.transform.position = bed.center + Vector3.up * .6f;
            var standing = (Camera.main != null ? Camera.main.transform.rotation : rotation).eulerAngles;
            standingPitch = Mathf.DeltaAngle(0, standing.x);
            yaw = standing.y;
            view.transform.rotation = Quaternion.Euler(standingPitch, yaw, 0);
        }
        void Update()
        {
            if (view == null) { if (blocker != null || beganWithNetworkPlayer) End(); return; }
            var run = CampaignRun.Instance;
            if (run == null) { End(); return; }
            if (WaitingRoomRehearsal.IsActive || WaitingRoomCannonRehearsal.IsActive)
            {
                run.WakeForRehearsal();
                End(); return;
            }
            var player = CurtainCall.Network.PlayerSync.NetworkPlayer.Local;
            if (beganWithNetworkPlayer && (restingPlayer == null || player != restingPlayer)) { End(); return; }
            if (player != null && !PlayerInteraction.CanInteract(player))
            {
                if (run.State.Phase == CampaignPhase.Night && run.IsLocalReady) run.SetReady(false);
                End(); return;
            }
            var s = run.State;
            if (s.Phase == CampaignPhase.Night && run.IsLocalReady)
            {
                var k = Keyboard.current;
                var input = player != null ? player.GetComponent<PlayerInputReader>() : null;
                if (blend > .2f && (input?.WasPressedThisFrame("Interact") == true || k != null && k.escapeKey.wasPressedThisFrame)) run.SetReady(false);
            }
            bool asleep = s.Phase == CampaignPhase.Dawn || (s.Phase == CampaignPhase.Night && run.IsLocalReady);
            if (s.Day > day && s.Phase == CampaignPhase.RestTime) morning = true;
            if (!asleep && !morning) { End(); return; }
            blend = Mathf.MoveTowards(blend, morning ? 0 : 1, Time.unscaledDeltaTime / .8f);
            view.transform.rotation = Quaternion.Euler(Mathf.Lerp(standingPitch, LyingPitch, Mathf.SmoothStep(0, 1, blend)), yaw, 0);
            if (morning && blend <= 0) End();
        }
        void End()
        {
            // A replacement local avatar can already be using another bed. Clear only this session's owner.
            if (restingPlayer != null) restingPlayer.SetRestingBed(-1);
            restingPlayer = null; beganWithNetworkPlayer = false;
            if (view != null) DestroyPresentation(view.gameObject);
            view = null;
            if (blocker != null) { blocker.SetOpen(false); DestroyPresentation(blocker); }
            blocker = null;
        }
        static void DestroyPresentation(Object target)
        {
            if (Application.isPlaying) Destroy(target); else DestroyImmediate(target);
        }
        void OnDisable() => End();
    }
}
