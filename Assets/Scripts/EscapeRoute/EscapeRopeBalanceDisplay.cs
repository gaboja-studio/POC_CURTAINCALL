using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Tightrope;
using CurtainCall.UI.PerformanceHudV1;
using CurtainCall.UI.SceneIntegration;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    /// <summary>기존 균형 UI에 원본 균형/매달림 값만 전달한다. 공연 정산이나 별도 게이지를 만들지 않는다.</summary>
    public sealed class EscapeRopeBalanceDisplay : MonoBehaviour
    {
        [SerializeField] PerformanceHUDView view;

        void OnEnable()
        {
            if (view == null || view.Balance == null) return;
            if (!view.Balance.TryGetComponent<BalanceRiskBlink>(out _))
                view.Balance.gameObject.AddComponent<BalanceRiskBlink>();
            if (!view.Balance.TryGetComponent<BalanceZoneColors>(out _))
                view.Balance.gameObject.AddComponent<BalanceZoneColors>();
        }

        void LateUpdate()
        {
            if (view == null) return;
            HudLayout.PlaceBalance((RectTransform)view.Balance.transform, GameSettings.Base.HudLayout.BalancePosition);
            view.Timer.gameObject.SetActive(false);
            view.Progress.gameObject.SetActive(false);
            var local = NetworkPlayer.Local;
            var course = TightropeCourse.Current;
            var balance = local != null ? local.GetComponent<PlayerBalance>() : null;
            var hangs = TightropeHangSystem.Current;
            bool hanging = hangs != null && hangs.IsHanging(local);
            bool active = local != null && local.State == PlayerState.Normal && balance != null
                && course != null && course.UsesRopeRules(local) && (balance.IsActive || hanging);
            view.Balance.SetContext(active, false);
            if (!active) { view.Balance.SetUnavailable(); return; }
            var reader = local.GetComponent<PlayerInputReader>();
            float posture = reader != null && reader.isActiveAndEnabled ? reader.Current.Posture : 0f;
            view.Balance.SetBalance(balance.Value / PlayerBalance.MaxValue,
                -balance.GreenLimit / PlayerBalance.MaxValue, balance.GreenLimit / PlayerBalance.MaxValue,
                -balance.WarningLimit / PlayerBalance.MaxValue, balance.WarningLimit / PlayerBalance.MaxValue,
                hanging ? hangs.HangDangerRatio(local) : balance.RedTimeRatio,
                "A", "D", !hanging && posture < -.5f, !hanging && posture > .5f);
        }
    }
}
