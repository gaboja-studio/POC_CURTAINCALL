---
summary: C15 근접·패널 품목 설명 툴팁 정본·표시 API·독립 씬·원본 보존
status: active
updated: 2026-10-20
source: human (PM 툴팁 묶음 구현·저장 요청) + code
---
# C15·품목 툴팁
## 언제 읽나
근접/패널 설명을 배치·호출·검증하거나 제공 키트를 다시 조립할 때.
## 정본·의존성
- `Assets/Resources/Prefabs/UIs/Tooltips/Tooltips.prefab`: Canvas 포함·모드 전환1개, `80_Tooltips/C15_Tooltip/Motion` 아래 Image8/TMP2.
- 독립 씬 `Assets/Scenes/Tests/Tooltips/Tooltips.unity`, 공개 `Assets/Scripts/Systems/Tooltips/`의 Preview·안전 Builder·PlayMode 테스트.
- private `curtaincall_tooltips_v1/Assets/CurtainCallUI/Tooltips/`: Runtime/Builder 원위치 재사용, `CurtainCall.UI.TooltipsV1` 격리. 제공 PNG/코드 공개 복제 금지.
- 신규 PNG7(판4/아래 선1/꼬리2), 기존 PNG 재사용0. 전체49실파일/PNG14·폰트/도구/문서/제작 원본 보존. Task 전수 CSV가 최초 SHA 기준.
- 전용 Static Pretendard SemiBold28/Medium24, 공유 Dynamic 폰트 수정/참조 없음.
## 표시 계약
- `ShowNearby(title,description,topLeft,width=384,tailCenterUi=null)` / `ShowPanel(title,description,topLeft,width=384)`.
- 좌상단 부모 UI 좌표(+오른쪽/+아래), 폭256~480. 본문 폭W−48의 실제 TMP 높이+84를4단위 올림·160~240 제한, 자동축소 없이 Ellipsis.
- Nearby 꼬리16×12/중심24..W−24·기본W/2, 발자국H+8.5(모션 여유4 별도). 아래 선 높이4·꼬리 양옆 분리. Panel은 꼬리/오른쪽 선 숨기고 왼쪽 선을W−24로 연결.
- Body/TailBody alpha.92·Shadow.32/y−1, Canvas PPU100/Sprite200, plate border24/나머지0. Title 한 줄/본문 Wrap, richText/parseCtrlCharacters Off.
- stable root 유지·Motion만 숨김, unscaled120/100ms·아래4UI/EaseOutCubic. 반복 Show/Hide 비재시작·전환은 현재 alpha/위치 보존. 수명/자동 숨김 없음.
- `HasRuntimePresentation`은 첫 alpha0부터 Hide 전까지. `IsVisible`은 실제 alpha>0, 퇴장 중 참일 수 있음.
- `SetPosition`/`SetTailCenter`는 표시 중만, Panel 꼬리 변경 거부·거부값 비캐시. Nearby Show 꼬리 생략은 매번 중앙.
- 비유한/편집/비활성/미설정 먼저 거부·보존, 유효 빈 제목/본문만 즉시 숨김. 제목 줄바꿈→공백·본문 실제 개행 보존. HideImmediate/Reset·비활성 초기화, 선행 Awake Show 보호.
## 안전·경계
- Builder는 Play/컴파일/import/PrefabStage/dirty씬/기존 출력 충돌/플랫폼 override를 쓰기 전 거부. 새 씬에서 소유 출력만 저장, 실패 부분 결과 보존·삭제/전역 저장/전역 Play 훅 없음.
- Overlay sorting8(패널/확인창 위·커튼 아래 계약), Controls10. 모든 툴팁 Graphic/CanvasGroup 입력 비차단·Mask 밖. 숫자 이름과 실제 sorting 구분.
- 한 뷰에 설명 하나. 감지/거리/시선/추적/투영/가장자리 보정·C6 경쟁/우선순위·입력/구매/게임/네트워크는 호출자 소유, 이번 구현에 없음.
- reload 설정 변경/both OFF 지원 주장 없음. 기존 폰트/Buttons 미결은 별도 보존. 상세 실행 결과·실패 이력은 Task-061 `tooltips-verification.md` 참조.
