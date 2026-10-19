using System.Collections.Generic;

namespace CurtainCall.HumanCannon
{
    /// <summary>지상 점프의 첫 반동 응답을 R/사망/다른 시도의 응답과 구분한다.</summary>
    public sealed class GroundBounceRequest
    {
        public bool Pending { get; private set; }
        public uint Sequence { get; private set; }
        public uint Epoch { get; private set; }

        public void Begin(uint sequence, uint epoch)
        {
            Sequence = sequence;
            Epoch = epoch;
            Pending = true;
        }

        public bool Matches(uint sequence, uint epoch) => Pending && Sequence == sequence && Epoch == epoch;
        public void Cancel() => Pending = false;
    }

    /// <summary>호스트는 장치 초기화 이전 요청과 플레이어별 중복 첫 반동을 거부한다.</summary>
    public sealed class GroundBounceHistory
    {
        readonly Dictionary<ulong, uint> latest = new();

        public bool TryAccept(ulong actor, uint sequence, uint epoch, uint currentEpoch)
        {
            if (sequence == 0 || epoch != currentEpoch) return false;
            if (latest.TryGetValue(actor, out uint previous) && sequence <= previous) return false;
            latest[actor] = sequence;
            return true;
        }

        public void Clear() => latest.Clear();
    }
}
