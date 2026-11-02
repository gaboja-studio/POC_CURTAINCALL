using System;
using CurtainCall.Network.PlayerSync;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>
    /// 도착지점 폭죽의 색종이·불꽃 연출. 각 화면이 동기화된 플레이어 위치로 첫 도착을 보고 한 번 터뜨린다.
    /// 판정·정산은 HumanCannonDestination이 맡으며 여기서는 바꾸지 않는다.
    /// </summary>
    public sealed class HumanCannonFinishCelebration : MonoBehaviour
    {
        [Tooltip("폭죽마다 붙인 색종이·불꽃 파티클. 반복하지 않고 시작 시 재생하지 않는다.")]
        [SerializeField] ParticleSystem[] bursts = Array.Empty<ParticleSystem>();
        readonly FinishCelebrationState state = new();

        public ParticleSystem[] Bursts => bursts;

        void OnEnable()
        {
            NetworkPlayer.Restarted += Restart;
            // 화면 밖이면 자동 컬링이 시뮬레이션을 멈춰 0초 버스트를 건너뛴다. 도착 순간 카메라 방향과 무관하게 터지게 한다.
            // 공연 성공이 확정되면 PerformanceRewards가 Time.timeScale을 0으로 멈추므로 실제 시간으로 재생한다.
            foreach (var burst in bursts)
                if (burst != null)
                    foreach (var system in burst.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        var main = system.main;
                        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                        main.useUnscaledTime = true;
                    }
        }
        void OnDisable() => NetworkPlayer.Restarted -= Restart;

        void Update()
        {
            var destination = HumanCannonDestination.Current;
            if (destination == null) return;
            bool arrived = false;
            foreach (var player in NetworkPlayer.All)
                if (destination.HasArrived(player)) { arrived = true; break; }
            if (state.TryCelebrate(destination.PerformanceId, arrived)) Play();
        }

        public void Play()
        {
            foreach (var burst in bursts)
            {
                if (burst == null) continue;
                burst.Clear(true);
                burst.Play(true);
            }
        }

        void Restart(NetworkPlayer player)
        {
            if (player == null || !player.IsLocal) return;
            var destination = HumanCannonDestination.Current;
            state.RestartPractice(destination != null ? destination.PerformanceId : null);
        }
    }
}
