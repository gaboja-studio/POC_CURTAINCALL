# Task-20261102-097

- **Title:** 상점 기물별 툴팁 전용 문구 칸
- **Type:** fix
- **Assignee:** @MoHoDu
- **Branch:** fix/shop/piece-descriptions
- **Base:** dev

## Context
상점 기물마다 마우스 올림 툴팁에만 보이는 문구를 따로 적을 칸을 만든다(사용자, 2026-11-02). 지금은 설명 칸과 툴팁이 같은 `shopDescription`을 쓴다.

## 계획
1. `ItemCatalog.Entry.shopTooltip` 추가, `ItemCatalog.Tooltip(id)`.
2. `PanelHoverTooltip`: 기물 줄(`item:<id>`)은 `shopTooltip`을 Row Tooltip의 `{description}`에 넣고, 비면 기존 설명(설명 칸과 같음)을 쓴다.
3. 툴팁·주석 갱신, PlayMode 검사.

## 미정·주의
- 툴팁 전용 문구에는 설명 칸 공통 안내(Item Description Footer)가 붙지 않는다. 가격·사유 줄은 Row Tooltip 형식대로 붙는다.
- 실제 기물별 문구 입력은 사용자가 `ItemCatalog.asset` 인스펙터에서 한다.
