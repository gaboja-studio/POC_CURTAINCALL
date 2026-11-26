# Task-20261126-206

- **Title:** M3 공연·기물/서비스 피드백 수정
- **Type:** fix
- **Assignee:** @MoHoDu
- **Branch:** fix/m3-stage-shop-feedback
- **Base:** dev

## Context
M3 플레이테스트 피드백(2026-11-26 사용자 요청). 결과는 Confluence M3(50659329) "4. 공연", "5. 기물 / 서비스" 아래 펼치기에 캡처·영상으로 기록한다.
'방법 찾기' 항목은 레퍼런스 조사 → 사용자 보고 → 허락 뒤 작업.

## 계획
공연
1. 떨어지려는 동료를 끌어올리다 같이 떨어짐 → 구조 중 구조자 균형 위험 시간 회복, 구조 성공 시 구조자 균형 안정화(`PlayerBalance.StabilizeAfterRescue`).
2. 불 고리 배경과 겹쳐 안 보임 → 실행 중 덧입히는 발광(`CheerPointGlow`). 밝기 과하다는 후속 의견 반영해 낮춤 + 가까우면 줄임. 수치는 GameSettings › 공연 장비.
3. 큰 호응 시 공연장 양옆 아래 불꽃(`PerformanceFireworks`, 공유 보상 상태로 각 화면이 연출, 네트워크 메시지 추가 없음).
4. (방법 찾기) 위험 동료 방향 표시 — 조사·보고 대기.
5. 배경 허공 마차 제거 — 비공개 서브모듈 `performance_background.unity`의 철창 마차 6개 삭제 + 제작 도구에서 배치 제거.

기물/서비스
1. 신체 복구 가격 인상 제거 → 부위당 고정 가격(`bodyRestorePriceStep` 폐기 설정 등록).
2. 설명 점검: 구조 장갑 대상 문구가 실제(조준점에 가장 가까운 보이는 동료, 8m)와 달라 수정. 나머지 일치.
3. 장부 탭 제거, 탭 = 탐색/공연/서비스. 키트 3칸 고정이라 칸 용도만 바꿈(Items=탐색, Services=공연, Ledger=서비스).
4. 공연 탭에 해금 전 기물: 검은 실루엣(`ItemArt/Icons/*-locked.png`, 아이콘 키 `locked:기물ID`), "해금되지 않은 기물입니다.", 구매 불가.
5. (방법 찾기) 팔다리 선택 UI(인체 그림 초록/빨강/흰색) — 조사·보고 대기.
6. (방법 찾기) 공연 기물·말뚝·머리핀 사용 유도 — 조사·보고 대기.

## 미정·주의
- 비공개 서브모듈(`Assets/_ThirdParty`) 수정 2건: `performance_background.unity`(마차 삭제), 상점 키트 `ShopData.cs`·`ShopPanelView.cs`(셋째 탭 버튼 허용·장부 문구 제거). 서브모듈에 이전부터 있던 다른 수정 파일 10개는 섞지 않는다. 포인터 갱신은 제출 요청 때.
- 해금 전 기물 이름은 "???"로 가림(ShopUiTexts에서 변경 가능). 위치 힌트는 추후 `lockedDescription`에 덧붙인다.
