using System.Collections.Generic;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 기물 사용 표시(2026-11-02, Task-094). 판정 화면이 내구도를 1 깎을 때마다 플레이어별 마지막 사용을 기록해
    /// 소지 상태와 함께 모든 화면에 보낸다. 소지 칸 UI가 번호가 바뀐 것을 보고 해당 칸에 사용 연출을 띄운다.
    /// - 도구(<see cref="UseItem"/>)·장비(<see cref="UseEquipment"/>) 사용 모두 같은 기록을 쓴다.
    /// - 버리기·선반·판매로 사라진 것과 다 써서 사라진 것을 <see cref="UseMark.Destroyed"/>로 구분한다.
    /// </summary>
    public sealed partial class PropCarry
    {
        /// <summary>한 플레이어의 마지막 기물 사용. Serial은 사용할 때마다 1씩 오른다(0 = 사용 기록 없음).</summary>
        public readonly struct UseMark
        {
            public readonly int Serial, Uid;
            public readonly bool Destroyed;
            public UseMark(int serial, int uid, bool destroyed) { Serial = serial; Uid = uid; Destroyed = destroyed; }
        }

        readonly Dictionary<ulong, UseMark> useMarks = new();

        /// <summary>모든 화면: 이 플레이어의 마지막 기물 사용(없으면 Serial 0).</summary>
        public UseMark LastUse(ulong player) => useMarks.TryGetValue(player, out var mark) ? mark : default;

        void MarkUsed(ulong player, int uid, bool destroyed)
        {
            int serial = LastUse(player).Serial + 1;
            useMarks[player] = new UseMark(serial, uid, destroyed);
        }

        UseMark UseMarkOf(ulong player) => LastUse(player);

        void ReadUseMarks(PlayerData[] players)
        {
            useMarks.Clear();
            if (players == null) return;
            foreach (var p in players)
                if (p.usedSerial > 0) useMarks[p.client] = new UseMark(p.usedSerial, p.usedUid, p.usedBroken);
        }
    }
}
