using UnityEngine;

namespace CurtainCall.Network.PlayerSync
{
    /// <summary>
    /// 테스트 전용(묘기 진행 005가 대체): 호스트에서 접속한 플레이어가 모두 추락하면 잠시 뒤 모두 재시작한다.
    /// 재시작 자체는 <see cref="NetworkPlayer.ServerRestartAll"/>이 한다.
    /// </summary>
    public sealed class TestRoundRestart : MonoBehaviour
    {
        [Tooltip("전원 추락 후 모두 재시작까지 기다리는 시간(초). 기획에 값이 없어 테스트 값.")]
        [SerializeField, Min(0f)] float restartDelay = 3f;

        float allFallenSince = -1f;

        void Update()
        {
            var players = NetworkPlayer.All;
            if (players.Count == 0 || !players[0].IsServer)
            {
                allFallenSince = -1f;
                return;
            }

            foreach (var player in players)
            {
                if (player.State != PlayerState.Fallen)
                {
                    allFallenSince = -1f;
                    return;
                }
            }

            if (allFallenSince < 0f) allFallenSince = Time.time;
            if (Time.time - allFallenSince < restartDelay) return;

            allFallenSince = -1f;
            NetworkPlayer.ServerRestartAll();
        }
    }
}
