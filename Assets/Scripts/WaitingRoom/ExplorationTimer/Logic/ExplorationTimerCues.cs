using System;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 탐색 제한 시간 방송 문턱(기본 30·10초) 판정. 엔진과 분리한 순수 로직이라 화면마다 따로 돌린다.
    /// 남은 시간이 문턱을 위에서 아래로 지나는 순간 한 번만 방송한다.
    /// - 처음 본 값이 이미 문턱 아래면(늦게 받은 화면 등) 놓친 방송은 내지 않는다.
    /// - 받는 쪽 보정으로 남은 시간이 조금(<see cref="RearmMargin"/> 이하) 늘어나도 다시 울리지 않는다.
    /// - 0초에 닿으면 쉬는 시간이 끝나므로 방송하지 않는다.
    /// 60초 소집 경보는 공용 <c>CampaignRun.RecallAlarm</c>을 쓰므로 여기서 다루지 않는다.
    /// </summary>
    public sealed class ExplorationTimerCues
    {
        /// <summary>문턱보다 이만큼(초) 넘게 다시 늘어나야 그 문턱을 다시 울릴 수 있다(디버그로 시간을 되돌린 경우).</summary>
        public const float RearmMargin = 1f;

        /// <summary><see cref="Step"/>이 방송할 문턱이 없을 때 돌려주는 값.</summary>
        public const float None = -1f;

        readonly float[] thresholds;
        readonly bool[] fired;
        bool hasLast;
        float last;

        public ExplorationTimerCues(params float[] thresholds)
        {
            this.thresholds = thresholds != null ? (float[])thresholds.Clone() : Array.Empty<float>();
            fired = new bool[this.thresholds.Length];
        }

        /// <summary>새 쉬는 시간: 모든 문턱을 다시 울릴 수 있게 한다.</summary>
        public void Reset()
        {
            hasLast = false;
            Array.Clear(fired, 0, fired.Length);
        }

        /// <summary>
        /// 한 프레임 진행. <paramref name="running"/>은 타이머가 흐르는 중인지(멈춤·대기 중이면 false).
        /// 이번에 방송할 문턱(초)을 돌려준다. 한 번에 여러 문턱을 지나면 가장 낮은 것 하나만 방송한다. 없으면 <see cref="None"/>.
        /// </summary>
        public float Step(bool running, float remaining)
        {
            if (!running)
            {
                hasLast = false;
                return None;
            }

            float result = None;
            for (int i = 0; i < thresholds.Length; i++)
            {
                float t = thresholds[i];
                if (fired[i])
                {
                    if (remaining > t + RearmMargin) fired[i] = false;
                    continue;
                }
                if (remaining > t) continue;

                fired[i] = true;
                bool crossed = hasLast && last > t && remaining > 0f;
                if (crossed && (result == None || t < result)) result = t;
            }

            hasLast = true;
            last = remaining;
            return result;
        }
    }
}
