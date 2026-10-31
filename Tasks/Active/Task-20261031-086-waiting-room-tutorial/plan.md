# Task-20261031-086

- **Title:** 대기실 튜토리얼 — 가이드 패널로 대기실 행동·규칙 안내
- **Type:** feat
- **Assignee:** @gyumin9048-ctrl
- **Branch:** feat/waiting-room/tutorial (feat/waiting-room/design-rooms 2e906f11에서 분기 — 안내 패널·공연 해금 등 M2 후속이 아직 dev에 없음)
- **Base:** dev

## Context
기획된 대기실↔공연 흐름에 맞춰 대기실에서 할 수 있는 행동과 익혀야 할 규칙을 패널 가이드로 안내한다. Task-079 보류 목록의 "튜토리얼"을 이 Task가 맡는다.

- 흐름: 도입 → 오전 외줄 → **가이드 쉬는 시간**(`RestKind.Guide`, `guideRestDuration`) → 오후 공연(인간 대포 미발견 시 외줄) → **1일차 밤 정비** → 침대 → **2일차 아침** → … → D-Day 아침.
- 기획 근거: `Harness/Project/Facts/game-flow.md`("가이드 쉬는 시간은 대기실 기능 안내와 서비스·상점 화면 안내").
- 재사용: `WaitingRoomGuidePanel`(BasicPanels Menu_C13, 입력 비차단, 현재는 새 안내가 앞 안내를 덮어씀), 1일차 판정 패턴 `TightropeGuideProgress.IsFirstDay`.

## 결정 (2026-10-31 사용자: 추천안 채택)
1. 비강제. 안내만 하고 단계 수행을 막지 않는다(시계가 흐르므로).
2. 가이드 쉬는 시간 길이는 기존 300초 유지(튜닝값 그대로).
3. 진행은 개인별. 팀 사건(공연 해금·동료 사망 등)은 모든 플레이어에게 반응.
4. 반복 범위: ~~PC 저장~~ → ~~런 단위~~ → **앱 재시작 때만**(게임을 완전히 종료하고 다시 시작했을 때만 처음부터. 날·세대 교체·로비 새 게임에는 다시 안 나옴, 2026-10-31 사용자). 끄기만 PC 설정, 설정의 "안내 처음부터"는 유지.
5. 인간 대포: "숨은 공연 기물이 있다"는 존재만 알리고 위치는 알리지 않는다.
6. 탐색 퍼즐: 단서·도구·열쇠의 쓰임까지만. 탈출 해법은 알려주지 않는다.
7. 다시 보기: ESC 메뉴의 안내 목록. 새 키 추가는 공통 조작키 합의가 필요해 쓰지 않는다. (추천 선택)

## 계획
1. 안내 항목 목록·문구 초안 → 기획자(사용자) 확정. 초안: [guide-copy-draft.md](guide-copy-draft.md).
2. 안내 문구 텍스트 파일(`WaitingRoomTutorialTexts.txt`, 사용자가 직접 수정) + 가이드 전용 패널(가이드≠나레이션, 2026-10-31 사용자)(대기열·단계 표시·재설정 키 표기·상점/조사/자막/알림과 겹침 회피).
3. 진행 관리자: 기존 이벤트만 구독(`CampaignRun.StepChanged`, `PropCarry`, `ExplorationState`, `UvMark.Discovered`, `PerformancePropPickup`, `ExpertService`, `NetworkPlayer`, `ScheduleBoard`, `NightBed`, `StageEntranceGather`). 게임 규칙은 바꾸지 않는다.
4. 연결 순서: ① 가이드 쉬는 시간 → ② 1일차 밤 정비 → 상황 안내(첫 부상·첫 사망자·슬롯 가득·도구 소진·시간 경보).
5. 완료 저장·설정 끄기/초기화·ESC 안내 목록. 선택: 위치 안내(승강기 안내의 카메라 회전·월드 마커 재사용).
6. 검사: PlayMode(1일차 가이드 진입→순서→밤→다음 날), MPPM 2인(서로 다른 진행), 사람 플레이(가독성·방해 여부).

## 미정·주의
- 안내 기준(2026-10-31 사용자): 현재 개발된 동작 위주, 추격자 제외. 고도화 때 문구 갱신.
- 문구 확정(3차): 물려줄 선반(N04)만 넣고 탈출 기한·2일차 아침 안내는 뺀다.
- ESC 메뉴(`common-menu-settings.md`)·설정 화면은 공통 UI라 수정 시 담당자에게 알린다.
- origin/dev 새 커밋 3개 미반영. 착수(start-work) 때 Unity 에디터 종료 확인 후 병합한다.
- design-rooms 브랜치가 dev에 먼저 합쳐지면 이 브랜치 PR 차이가 줄어든다. PR 순서는 제출 때 정한다.

## 후속 계획 (2026-10-31)
- 런 단위 초기화·지시형 할 일 가이드: [directive-guide-plan.md](directive-guide-plan.md). 결정 5건 대기.
