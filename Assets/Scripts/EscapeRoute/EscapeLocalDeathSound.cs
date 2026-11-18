using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Sound;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    /// <summary>
    /// 탈출로에서 내 단원이 사망·포획 확정(Fallen/Retired)되는 순간 사운드 상황 <see cref="SoundSituations.EscapeLocalDeath"/>를 낸다.
    /// 탈출로는 사망 카드 없이 바로 관전으로 넘어가므로 카드 대신 내 캐릭터 상태 변화를 본다(이 화면에서만).
    /// </summary>
    public static class EscapeLocalDeathSound
    {
        static NetworkPlayer watched;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState() => watched = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            NetworkPlayer.LocalSpawned -= Watch;
            NetworkPlayer.LocalSpawned += Watch;
            NetworkPlayer.LocalDespawned -= Unwatch;
            NetworkPlayer.LocalDespawned += Unwatch;
            if (NetworkPlayer.Local != null) Watch(NetworkPlayer.Local);
        }

        static void Watch(NetworkPlayer player)
        {
            Unwatch();
            watched = player;
            if (watched != null) watched.StateChanged += HandleStateChanged;
        }

        static void Unwatch()
        {
            if (watched != null) watched.StateChanged -= HandleStateChanged;
            watched = null;
        }

        static void HandleStateChanged(PlayerState previous, PlayerState next) => Notify(previous, next, InEscape);

        static bool InEscape => CampaignRun.Instance != null && CampaignRun.Instance.State.Phase == CampaignPhase.Escape
            || EscapeRouteTimer.Current != null && EscapeRouteTimer.Current.isActiveAndEnabled;

        /// <summary>살아 있던 내 단원이 탈출로에서 사망·리타이어로 바뀌는지.</summary>
        public static bool IsLocalDeath(PlayerState previous, PlayerState next, bool inEscape) =>
            inEscape && previous is PlayerState.Normal or PlayerState.EscapeCaptured && next is PlayerState.Fallen or PlayerState.Retired;

        /// <summary>상태 변화를 받아 조건이 맞으면 소리를 낸다. 테스트 씬도 이 경로로 재현한다.</summary>
        public static void Notify(PlayerState previous, PlayerState next, bool inEscape)
        {
            if (IsLocalDeath(previous, next, inEscape)) SoundManager.Play(SoundSituations.EscapeLocalDeath);
        }
    }
}
