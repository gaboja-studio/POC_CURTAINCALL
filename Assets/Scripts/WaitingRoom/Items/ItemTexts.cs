namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 아이템 이름의 사람이 고치는 문구(대기실 문구 파일 `item.&lt;id&gt;.name|worn`). 키가 없으면 기물 목록 에셋 값을 쓴다.
    /// 상점 설명·발동 문구는 기물 목록(ItemCatalog.shopDescription·useCue)에서 고친다(2026-11-01 사용자).
    /// </summary>
    public static class ItemTexts
    {
        public static string Name(string id, string fallback) => WaitingRoomText.Get("item." + id + ".name", fallback);
        public static string WornName(string id, string fallback) =>
            string.IsNullOrEmpty(fallback) && !WaitingRoomText.Has("item." + id + ".worn") ? fallback : WaitingRoomText.Get("item." + id + ".worn", fallback);
    }
}
