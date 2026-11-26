using System;

namespace CurtainCall.WaitingRoom
{
    public enum TerminalCheck { Incomplete, Correct, Wrong, Locked }

    /// <summary>
    /// 관리실 단말기 입력 상태(2026-11-26 사용자). 암호 두 개(날짜·암구호)를 각각 따로 입력하고 저장한다.
    /// - 입력란마다 정답 글자 수만큼만 칸이 있다(날짜 8칸, 암구호 RUN이면 3칸).
    /// - 두 칸 모두 자판의 모든 글자(숫자·영문)를 받는다.
    /// - 칸을 다 채우면 그 칸만 정답인지 본다. 맞으면 잠기고(줄 그음) 다음 칸으로, 틀리면 그 칸만 비운다.
    /// - 위·아래 방향키나 클릭으로 입력란을 바꾼다.
    /// - 단말기를 닫아도 이 상태를 유지한다(단말기가 보관). 두 칸이 모두 맞으면 기존 판정 요청(날짜 + "\n" + 암구호)으로 문을 연다.
    /// </summary>
    public sealed class TerminalEntry
    {
        public const int Count = 2;
        readonly int[] lengths;
        readonly string[] values = { "", "" };
        readonly bool[] solved = new bool[Count];
        readonly Func<int, string, bool> check;

        public TerminalEntry(int dateLength, int wordLength, Func<int, string, bool> check)
        {
            lengths = new[] { Math.Max(1, dateLength), Math.Max(1, wordLength) };
            this.check = check;
        }

        public int Active { get; private set; }
        public int Length(int index) => lengths[index];
        public string Value(int index) => values[index];
        public bool IsSolved(int index) => solved[index];
        public bool AllSolved => solved[0] && solved[1];
        /// <summary>판정 화면에 보내는 값(기존과 같은 형식).</summary>
        public string Combined => values[0] + "\n" + values[1];

        public static bool Accepts(char c) => c >= '0' && c <= '9' || c >= 'A' && c <= 'Z';

        public void Select(int index) { if (index >= 0 && index < Count) Active = index; }
        public void Move(int direction) => Select(Math.Clamp(Active + Math.Sign(direction), 0, Count - 1));

        /// <summary>지금 입력란에 한 글자를 넣는다. 칸이 다 차면 바로 확인한다.</summary>
        public TerminalCheck Type(char c)
        {
            c = char.ToUpperInvariant(c);
            if (!Accepts(c)) return TerminalCheck.Incomplete;
            if (solved[Active]) return TerminalCheck.Locked;
            if (values[Active].Length >= lengths[Active]) return Check();
            values[Active] += c;
            return values[Active].Length >= lengths[Active] ? Check() : TerminalCheck.Incomplete;
        }

        public void Backspace()
        {
            if (solved[Active] || values[Active].Length == 0) return;
            values[Active] = values[Active].Substring(0, values[Active].Length - 1);
        }

        public void ClearActive() { if (!solved[Active]) values[Active] = ""; }

        /// <summary>판정 화면이 거절했을 때 등: 모두 처음 상태로.</summary>
        public void Reset()
        {
            for (int i = 0; i < Count; i++) { values[i] = ""; solved[i] = false; }
            Active = 0;
        }

        /// <summary>지금 입력란을 확인한다(ENTER 또는 칸이 다 찼을 때).</summary>
        public TerminalCheck Check()
        {
            if (solved[Active]) return TerminalCheck.Locked;
            if (values[Active].Length < lengths[Active]) return TerminalCheck.Incomplete;
            if (check == null || !check(Active, values[Active])) { values[Active] = ""; return TerminalCheck.Wrong; }
            solved[Active] = true;
            for (int i = 1; i <= Count; i++)
            {
                int next = (Active + i) % Count;
                if (!solved[next]) { Active = next; break; }
            }
            return TerminalCheck.Correct;
        }
    }
}
