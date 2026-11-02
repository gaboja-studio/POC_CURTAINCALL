namespace CurtainCall.HumanCannon
{
    /// <summary>공연마다 첫 도착에서 한 번만 축하한다. 공연이 아니면 연습 한 판(R로 다시 시작) 단위다.</summary>
    public sealed class FinishCelebrationState
    {
        public const string Practice = "practice";
        string celebratedFor;

        public static string KeyFor(string performanceId) => string.IsNullOrEmpty(performanceId) ? Practice : performanceId;

        /// <summary>이번 공연(또는 연습 판)에서 아직 축하하지 않았고 도착자가 있으면 true를 한 번만 돌려준다.</summary>
        public bool TryCelebrate(string performanceId, bool anyArrived)
        {
            string key = KeyFor(performanceId);
            if (!anyArrived || celebratedFor == key) return false;
            celebratedFor = key;
            return true;
        }

        /// <summary>연습 중 R 재시작만 다시 축하할 수 있게 한다. 공연은 새 공연 ID가 와야 다시 터진다.</summary>
        public void RestartPractice(string performanceId)
        {
            if (KeyFor(performanceId) == Practice && celebratedFor == Practice) celebratedFor = null;
        }
    }
}
