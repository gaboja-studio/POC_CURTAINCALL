namespace CurtainCall.Voice
{
    /// <summary>
    /// 내가 들어가 있는 음성 무리. 두 무리는 서로 들리지 않는다. 결정: Harness/Project/Decisions/voice-chat.md
    /// </summary>
    public enum VoiceRoom : byte
    {
        /// <summary>음성 없음(오프라인·꺼짐·접속 준비 중).</summary>
        None = 0,
        /// <summary>생존자 무리: 거리에 따라 크기가 달라진다(위치 채널).</summary>
        Alive = 1,
        /// <summary>사망자 무리(추락·리타이어): 거리와 상관없이 같은 크기(그룹 채널).</summary>
        Dead = 2,
    }
}
