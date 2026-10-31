namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 아이템 이름·설명의 사람이 고치는 문구(대기실 문구 파일 `item.&lt;id&gt;.name|worn|desc`).
    /// 키가 없으면 기물 목록 에셋 값을 쓰고, 설명은 비어 있으면 표시하지 않는다.
    /// </summary>
    public static class ItemTexts
    {
        public static string Name(string id, string fallback) => WaitingRoomText.Get("item." + id + ".name", fallback);
        public static string WornName(string id, string fallback) =>
            string.IsNullOrEmpty(fallback) && !WaitingRoomText.Has("item." + id + ".worn") ? fallback : WaitingRoomText.Get("item." + id + ".worn", fallback);
        /// <summary>상점에 보이는 아이템 설명. 없으면 "".</summary>
        public static string Description(string id) => string.IsNullOrEmpty(id) ? "" : WaitingRoomText.Get("item." + id + ".desc", "");
    }
}
