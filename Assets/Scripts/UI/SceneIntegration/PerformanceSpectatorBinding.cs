using System.Collections.Generic;
using System.Linq;
using CurtainCall.Cameras;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.UI.SpectatorV1;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration
{
    [DefaultExecutionOrder(400)]
    public sealed class PerformanceSpectatorBinding : MonoBehaviour
    {
        [SerializeField] SpectatorView view;
        readonly Dictionary<CameraSpectator, bool> legacy = new();
        public SpectatorView View => view;
        public bool OccupiesBottomSlot => view != null && view.OccupiesBottomSlot;
        public void Configure(SpectatorView value) => view = value;

        public static SpectatorState ResolveState(bool dead, bool spectating, bool hasTarget) =>
            !dead ? SpectatorState.Hidden : !spectating ? SpectatorState.Dead :
            hasTarget ? SpectatorState.Spectating : SpectatorState.NoTarget;

        void LateUpdate()
        {
            if (view == null) return;
            var space = CommonUiSceneLoader.CurrentSpace;
            if (space == null) { Release(); return; }
            foreach (var camera in FindObjectsByType<CameraSpectator>(FindObjectsSortMode.None))
            {
                if (!legacy.ContainsKey(camera)) legacy.Add(camera, camera.ShowLegacyOverlay);
                camera.ShowLegacyOverlay = false;
            }
            var local = NetworkPlayer.Local;
            bool dead = local != null && (local.State == PlayerState.Fallen || local.State == PlayerState.Retired
                || (local.TryGetComponent(out PlayerBalance balance) && balance.HasFallen));
            var ending = PerformanceEndingPresentation.Current;
            if (ending != null && (ending.ResultVisible || ending.BlackVisible)) { view.HideImmediate(); return; }
            var state = ResolveState(dead, CameraSpectator.IsSpectating, CameraSpectator.Target != null);
            if (state == SpectatorState.Hidden) view.Hide();
            else
            {
                int alive = NetworkPlayer.All.Count(p => p != null && p != local && p.State == PlayerState.Normal);
                string detail = state == SpectatorState.Spectating ? $"플레이어 {CameraSpectator.Target.Slot + 1}" :
                    state == SpectatorState.NoTarget ? "관전 대상 없음" : "사망했습니다";
                view.Present(state, state == SpectatorState.Spectating ? "관전 중" : "사망", detail,
                    "A", "D", alive > 1 && !InputGate.IsBlocked);
            }
            if (!view.OccupiesBottomSlot) return;
            if (space.LocalBalance != null) space.LocalBalance.SetContext(false, true);
            CommonPerformanceUI.Current?.HideHints();
        }
        void Release()
        {
            if (view != null) view.HideImmediate();
            foreach (var pair in legacy) if (pair.Key != null) pair.Key.ShowLegacyOverlay = pair.Value;
            legacy.Clear();
        }
        void OnDisable() => Release();
    }
}
