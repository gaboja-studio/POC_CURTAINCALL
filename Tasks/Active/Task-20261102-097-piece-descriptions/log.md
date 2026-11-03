# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

## 2026-11-02
- 구현: `ItemCatalog.Entry.shopTooltip`, `PanelHoverTooltip.TooltipDescription`(기물 줄만, 비면 기존 설명).
- 검사: 에디터(MCP) 컴파일 오류 0. PlayMode `ShopItemTooltipPlayTests.ItemRowTooltipUsesDedicatedTextAndFallsBackToDescription` PASS. `check-work.ps1`은 미실행(직전 실행에서 기존 Task-081 문서 길이 HARD로 문서 단계 FAIL).
- 문구: 기물 7종 `shopDescription`·`shopTooltip`을 첨삭본으로 입력(에디터 SerializedObject 저장). 초안의 {내구도}·{price} 이름표는 기물 칸에서 바뀌지 않아 숫자로 적고, 가격 줄은 Row Tooltip이 붙이므로 뺐다. 실제 효과와 다른 초안 표현(구조 장갑 '반동', 벌룬 바지 '높은 곳 낙하')은 실제 효과로 고침.
- 검사: `W2_ShopDescription...`의 '다칠 바닥' 확인을 새 문구 '다칠 수 있는 바닥'으로 갱신. PlayMode `W2_ShopDescription...`, `W2_ShopWording...`, `ItemRowTooltip...` PASS. 테스트가 바꾼 TMP 아틀라스 에셋 2개는 되돌림.
- 추가 요청(2026-11-02): 상점 패널 높이 +130(552→682, 화면 가운데 유지). 목록 296→426, 설명 칸 128.8→243.4(8.5줄, 넘친 줄 반만 보임 유지·가격 줄과 겹치지 않게), 아래 요소 130 내림. 공통 안내(itemDescriptionFooter) 비움. 툴팁: 키트(TooltipView, _ThirdParty) 최대 240 높이로 '…' 잘림 → 키트는 그대로 두고 `PanelHoverTooltip.FitWholeDescription`이 표시 직후 글 전체 높이로 늘림. 설명·툴팁을 단답형 공통 형식으로 교체(설명: 요약/빈 줄/• 적용·사용·소모·내구도, 툴팁: 요약/사용 · 내구도).
- 검사: 컴파일 오류 0. PlayMode `ShopItemTooltipPlayTests`(2), `W2_ShopDescription...`(긴 설명으로 스크롤 확인, 긴 툴팁 전체 표시 확인 추가), `W2_ShopWording...` PASS. 스크린샷 `.harness/local/shop-description-scroll.png`로 배치 확인. 테스트가 바꾼 TMP 아틀라스 2개 되돌림.
- 문구 형식 변경(사용자): 설명은 습니다체 문장 1~2개 + 빈 줄 + • 적용(공연만, 쉼표 구분)/• 사용/• 내구도/• '~할 때 내구도 소모'. 툴팁 첫 줄도 습니다체. 탐색 도구 3종의 적용은 공연이 아니라 '대기실 탐색'으로 적음(확인 필요). 끈끈이 장갑 적용은 코드 확인 결과 외줄타기·인간 대포(봉). 신체 복구 설명(medicalDescription)도 같은 형식으로 교체(가격은 아래 가격 줄이 보여 주므로 인상 안내만).
- 검사: `ShopItemTooltipPlayTests`(형식 검사 갱신) 2/2, `W2_ShopWording...`(신체 복구 설명은 설정값과 같은지로 변경) PASS.

- 2026-11-02 PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/270 (fix/shop/piece-descriptions → dev)
- PR #270 제출: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/270 · Jira CC-202 설명 갱신(남은 일: 실제 화면에서 글자 깨짐 확인, 탐색 도구 '적용' 줄 결정).
- origin/dev(169225f4, #269 소지 칸 손 아이콘) 병합: 겹치는 파일 없어 충돌 없음. 에디터 종료 상태라 병합 후 컴파일은 미확인.
