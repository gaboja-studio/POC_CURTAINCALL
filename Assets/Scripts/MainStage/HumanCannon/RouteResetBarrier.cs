namespace CurtainCall.HumanCannon
{
    /// <summary>로컬 재시작 이후 요청한 응답만 받아 옛 운반 상태의 재적용을 막는다.</summary>
    public sealed class RouteResetBarrier
    {
        public bool Waiting { get; private set; }
        public int Token { get; private set; }

        public int Begin()
        {
            Token = Token == int.MaxValue ? 1 : Token + 1;
            Waiting = true;
            return Token;
        }

        public bool Accept(int acknowledgment)
        {
            if (!Waiting) return true;
            if (acknowledgment != Token) return false;
            Waiting = false;
            return true;
        }

        public void Clear() { Waiting = false; Token = 0; }
    }
}
