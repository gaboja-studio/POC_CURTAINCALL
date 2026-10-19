# Setup

PM 자체 작업. 2026-10-20 원격 통합 준비·작업 저장/push 승인. PR·병합·정식 인계는 별도다.

## JIRA

- 관련 JIRA: 없음

## 작업 구역

- 테스트 씬: `Assets/Scenes/Tests/CheerGauge/`
- 스크립트 폴더: `Assets/Scripts/Systems/CheerGauge/`
- 에셋 폴더: `Assets/Resources/Prefabs/UIs/CheerGauge/`, `Assets/Resources/Fonts/CheerGauge/`
- 버튼 씬: `Assets/Scenes/Tests/CommonButtons/`
- 버튼 코드: `Assets/Scripts/Systems/CommonButtons/`
- 버튼 에셋: `Assets/Resources/Prefabs/UIs/CommonButtons/`, `Assets/Resources/Fonts/CommonButtons/`
- PM 버튼 키트: `Assets/_ThirdParty/curtaincall_buttons_v1/` — importer/.meta 및 기존 조립기 수정 허용, 제공 런타임 재사용. 2026-10-20 PM 확인·삭제 승인: 필수 의존성·라이선스·조립기와 GUID를 보존하고 미사용 문서·미리보기·제작 자료만 Unity API로 정리.
- 준비 기록: `Integrations/Active/Integration-cheer-gauge-ui/`
- PM 승인 정리: `Assets/_ThirdParty/curtaincall_c1_v1/`의 미참조 제공 코드·문서·미리보기·제작 자료와 동반 .meta 삭제. 필수 PNG8·TTF2·라이선스·상위 .meta 보존.
- PM 하네스 기록: `Harness/Project/Decisions/third-party-assets.md`, `Harness/Project/Facts/c1-cheer-gauge.md`, `Harness/Project/Facts/index.md`, `Harness/Project/Facts/common-buttons.md`.
- Task 문서: 이 Task 폴더 (자동 포함)

## 예약 구역

- 없음

## 배정된 공용 파일

- `Assets/Resources/Prefabs/UIs/CheerGauge/C1CheerGauge.prefab` — 신규 전용 UI
- `Assets/Resources/Prefabs/UIs/CommonButtons/`, `Assets/Resources/Fonts/CommonButtons/` — 버튼4종·폰트

## 수정 금지

- `Assets/Scenes/` — 위 전용 테스트 씬 외
- `Assets/Scripts/Systems/Reward/` — 공개 API 읽기·구독만 허용
- `ProjectSettings/`
- `Packages/`
- `Assets/_ThirdParty/` — 원본 폴더 변경 금지. PM 승인 예외: 이 worktree에 C1 키트 로컬 복사·Unity import/.meta 및 위 승인 정리 및 버튼 키트 범위만 허용. 메인 원본·다른 private 자산 변경 금지. PNG·제공 코드 공개 재배포 금지.

## 인계 전 체크 (PM)

- [x] 관련 JIRA 번호를 모두 적었다
- [x] Unity에서 테스트 씬을 만들고 저장했다 (`.meta` 생성됨)
- [x] 스크립트·에셋 폴더를 만들었다 (빈 폴더 없음)
- [x] `plan.md`에 공동 테스트 항목을 합의해 적었다

정식 인계 없음. PM 본인의 승인된 로컬 구현이며 assign-task의 자동 커밋·push를 실행하지 않는다.

## 테스트 씬 처리

- 결정: 미정 (유지 / 삭제)
- 결정자·날짜:
