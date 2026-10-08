using CurtainCall.Network.PlayerSync;
using CurtainCall.Tightrope.UI;
using UnityEngine;

namespace CurtainCall.Tightrope.Prototype
{
    /// <summary>플레이 씬의 결과 UI 재도전과 입력 재무장을 연결한다.</summary>
    [RequireComponent(typeof(TightropeDemoUI))]
    public sealed class TightropePrototype : MonoBehaviour
    {
        TightropeDemoUI ui;

        void Awake() => ui = GetComponent<TightropeDemoUI>();

        void OnEnable()
        {
            ui.RetryRequested += Retry;
            NetworkPlayer.Restarted += HandleRestarted;
        }

        void OnDisable()
        {
            ui.RetryRequested -= Retry;
            NetworkPlayer.Restarted -= HandleRestarted;
        }

        static void Retry(string attemptId, int round)
        {
            var run = TightropeRun.Current;
            if (run != null && run.RetryByHost(attemptId, round))
                TightropeInputGate.RequireRelease();
        }

        static void HandleRestarted(NetworkPlayer player)
        {
            if (player != null && player.IsLocal) TightropeInputGate.RequireRelease();
        }
    }
}
