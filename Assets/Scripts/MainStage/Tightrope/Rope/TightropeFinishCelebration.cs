using System;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    /// <summary>
    /// 외줄 도착 플랫폼의 폭죽 색종이·불꽃 연출(인간 대포 도착지점과 같은 구성, 2026-11-05 사용자 요청).
    /// 코스가 설정값으로 다시 만들어져도 도착 플랫폼 뒤 가장자리 가운데를 따라간다.
    /// 각 화면이 동기화된 플레이어 상태(Arrived)로 첫 도착을 보고 한 번 터뜨린다. 판정·정산은 TightropeRun이 맡는다.
    /// </summary>
    public sealed class TightropeFinishCelebration : MonoBehaviour
    {
        [Tooltip("폭죽마다 붙인 색종이·불꽃 파티클. 반복하지 않고 시작 시 재생하지 않는다.")]
        [SerializeField] ParticleSystem[] bursts = Array.Empty<ParticleSystem>();
        readonly FinishCelebrationState state = new();

        public ParticleSystem[] Bursts => bursts;

        void OnEnable()
        {
            NetworkPlayer.Restarted += Restart;
            // 화면 밖에서도 0초 버스트를 건너뛰지 않고, 공연 성공 후 Time.timeScale 0에서도 재생한다.
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
            FollowFinish();
            var run = TightropeRun.Current;
            if (run == null) return;
            bool arrived = false;
            foreach (var player in NetworkPlayer.All)
                if (player != null && player.IsSpawned && player.State == PlayerState.Arrived) { arrived = true; break; }
            if (state.TryCelebrate(run.CampaignPerformanceId, arrived)) Play();
        }

        /// <summary>도착까지 가는 줄들의 가운데, 도착 플랫폼 뒤 가장자리 윗면에 놓는다.</summary>
        public void FollowFinish()
        {
            var course = TightropeCourse.Current;
            if (course == null) return;
            var layout = course.Layout;
            float edge = course.FinishDistance + course.Shape.FinishPlatformLength;
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int lane = 0; lane < layout.LaneCount; lane++)
            {
                if (layout.GetLaneEnd(lane) < layout.FinishDistance) continue;
                sum += course.GetLanePosition(lane, edge);
                count++;
            }
            if (count == 0) return;
            transform.SetPositionAndRotation(sum / count, Quaternion.LookRotation(course.Forward, Vector3.up));
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
            state.RestartPractice(TightropeRun.Current != null ? TightropeRun.Current.CampaignPerformanceId : null);
        }
    }
}
