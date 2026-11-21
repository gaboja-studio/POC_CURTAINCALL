using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>지상 점프의 첫 반동 응답을 R/사망/다른 시도의 응답과 구분한다.</summary>
    public sealed class GroundBounceRequest
    {
        public bool Pending { get; private set; }
        public uint Sequence { get; private set; }
        public uint Epoch { get; private set; }
        double nextAttempt, expiresAt;

        public uint NextSequence() => Sequence == uint.MaxValue ? 1u : Sequence + 1u;

        public void Begin(uint sequence, uint epoch, double now = 0d)
        {
            Sequence = sequence;
            Epoch = epoch;
            Pending = true;
            nextAttempt = now;
            expiresAt = now + 3d;
        }

        public bool Expired(double now) => Pending && now >= expiresAt;

        public bool TrySend(double now)
        {
            if (!Pending || Expired(now) || now < nextAttempt) return false;
            nextAttempt = now + .25d;
            return true;
        }

        public bool Matches(uint sequence, uint epoch) => Pending && Sequence == sequence && Epoch == epoch;
        public void Cancel() => Pending = false;
    }

    /// <summary>호스트는 장치 초기화 이전 요청과 플레이어별 중복 첫 반동을 거부한다.</summary>
    public sealed class GroundBounceHistory
    {
        readonly Dictionary<ulong, (uint Sequence, uint Epoch)> latest = new();
        readonly Dictionary<ulong, Vector3> acceptedContacts = new();

        public bool TryAccept(ulong actor, uint sequence, uint epoch, uint currentEpoch)
        {
            if (sequence == 0 || epoch != currentEpoch) return false;
            if (latest.TryGetValue(actor, out var previous) && unchecked((int)(sequence - previous.Sequence)) <= 0) return false;
            latest[actor] = (sequence, epoch);
            acceptedContacts.Remove(actor);
            return true;
        }

        public void RecordAccepted(ulong actor, Vector3 contact) => acceptedContacts[actor] = contact;

        public bool TryReplay(ulong actor, uint sequence, uint epoch, out Vector3 contact)
        {
            contact = default;
            return latest.TryGetValue(actor, out var previous) && previous == (sequence, epoch)
                && acceptedContacts.TryGetValue(actor, out contact);
        }

        public void Clear()
        {
            latest.Clear();
            acceptedContacts.Clear();
        }
    }
}
