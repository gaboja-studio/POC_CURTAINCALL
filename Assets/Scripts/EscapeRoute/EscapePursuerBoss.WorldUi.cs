using CurtainCall.Settings;
using CurtainCall.WorldIndicators;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    public sealed partial class EscapePursuerBoss
    {
        AreaIndicatorView leapLanding;
        PathIndicatorView dashCorridor;

        // True suppresses the legacy ring, including unsupported or inactive cues.
        bool PresentBossWorldUi(float age)
        {
            bool leap = state.Phase == Phase.LeapWarning || state.Phase == Phase.Leaping;
            bool dash = state.Phase == Phase.DashWarning || state.Phase == Phase.Dashing;
            if (!isActiveAndEnabled || !chase || !chase.isActiveAndEnabled || !HasCurrentRoundState
                || !GameSettings.WorldUi.enabled || !leap && !dash)
            { HideBossWorldUi(); return true; }
            if (!leap && leapLanding) leapLanding.Hide();
            if (!dash && dashCorridor) dashCorridor.Hide();
            if (!GameSettings.WorldUi.style) { HideBossWorldUi(); return false; }
            if (!chase.TryWorldUiFootprint(state.Goal, transform.rotation, out var goal, out float radius)
                || !chase.TryWorldUiSupport(goal, out var support))
            { HideBossWorldUi(); return true; }
            if (leap)
            {
                if (!leapLanding) leapLanding = WorldIndicatorFactory.Area(transform, true);
                if (!leapLanding) return false;
                float elapsed = age + (state.Phase == Phase.Leaping ? Tuning.bossLeapWarning : 0);
                leapLanding.Show(support.point, Quaternion.FromToRotation(Vector3.up, support.normal),
                    Vector2.one * (radius * 2f), elapsed / Mathf.Max(.001f, Tuning.bossLeapWarning + Tuning.bossLeapSeconds));
                return true;
            }
            if (!dashCorridor) dashCorridor = WorldIndicatorFactory.Path(transform);
            if (!dashCorridor) return false;
            bool warning = state.Phase == Phase.DashWarning;
            float progress = age / Mathf.Max(.001f, warning ? Tuning.bossDashWarning : Tuning.bossDashSeconds);
            var from = warning ? state.From : Vector3.Lerp(state.From, state.Goal, Mathf.SmoothStep(0, 1, progress));
            if (!chase.TryWorldUiFootprint(from, transform.rotation, out var center, out _)
                || !chase.TryWorldUiSupport(center, out var start))
            { dashCorridor.Hide(); return true; }
            dashCorridor.Show(start.point, support.point, radius * 2f, warning ? progress : 1);
            return true;
        }

        // Also call from the main partial's OnDisable and showcase early-return path.
        internal void HideBossWorldUi()
        {
            if (leapLanding) leapLanding.Hide();
            if (dashCorridor) dashCorridor.Hide();
            if (landingRing) landingRing.enabled = false;
        }
    }
}
