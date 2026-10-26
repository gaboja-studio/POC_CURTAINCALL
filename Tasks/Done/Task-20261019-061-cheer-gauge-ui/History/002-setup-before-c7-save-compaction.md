# Setup

PM 자체 작업. 2026-10-20 원격 통합 준비·작업 저장/push 승인, 후속 알림 키트/공개 결과 저장 재요청 적용. PR·병합·정식 인계는 별도다.
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
- 패널 씬·코드: `Assets/Scenes/Tests/BasicPanels/`, `Assets/Scripts/Systems/BasicPanels/`.
- 패널 에셋: `Assets/Resources/Prefabs/UIs/BasicPanels/`, `Assets/Resources/Fonts/BasicPanels/`.
- PM 패널 키트: `Assets/_ThirdParty/curtaincall_panels_v1/` — 2026-10-20 조립 승인. 패널7종 importer/.meta, 제공 조립기·Runtime·동봉 버튼 namespace 격리 허용. 2026-10-20 PM 정상 동작 확인 후 저장·정리 승인: 미참조 중복 문서·검토 자료만 정리하고 필요한 설명은 하네스로 이관. 다른 패널 제작용 원본·공용 리소스·조립기·라이선스·GUID는 보존. 기존 CommonButtons 정본은 읽기 전용 재사용.
- PM 패널 기록: `Harness/Project/Facts/basic-panels.md`, 사실 index 및 현재 Integration 소유 표.
- 아이콘 씬·코드: `Assets/Scenes/Tests/CommonIcons/`, `Assets/Scripts/Systems/CommonIcons/`.
- 아이콘 에셋: `Assets/Resources/Prefabs/UIs/CommonIcons/`, 필요 시 `Assets/Resources/Fonts/CommonIcons/`.
- PM 아이콘 키트: `Assets/_ThirdParty/curtaincall_icons_v1/` — 2026-10-20 계획 승인. 신규22종 importer/.meta·제공 조립기 경로/namespace/자동화 보정 허용. 기존3종은 정본 읽기 전용 재사용. 키트 전체 보존, 삭제·Git 저장·실게임 연결 제외.
- PM 아이콘 기록: `Harness/Project/Facts/common-icons.md`, 사실 index, Task 전수 목록·검증 기록 및 현재 Integration 소유 표.
- 공연 HUD 씬·코드: `Assets/Scenes/Tests/PerformanceHud/`, `Assets/Scripts/Systems/PerformanceHud/`.
- 공연 HUD 에셋: `Assets/Resources/Prefabs/UIs/PerformanceHud/`, 필요 시 `Assets/Resources/Fonts/PerformanceHud/`.
- PM 공연 HUD 키트: `Assets/_ThirdParty/curtaincall_performance_hud_v1/` — 2026-10-20 계획 승인. 신규20 importer/.meta, 제공 조립기/Runtime의 namespace·경로·안전 진입점 보정 허용. 기존 아이콘3·C1 정본은 읽기 전용. 전체 원본 보존, 삭제·Git 저장·실게임 연결 제외.
- PM P1 보정 승인(2026-10-20): 최신 외줄과의 표시 불일치 수정. 위 HUD Runtime의 경고 경계 API·5구간 직렬화 참조, 조립기·증분 보정 도구·샘플·소유 prefab/씬·테스트 수정 허용. 기존 외줄 씬/Player/Settings는 읽기 전용, 실게임 연결·누적 계산·삭제·Git 저장 제외.
- 공연 HUD 기록: `Harness/Project/Facts/performance-hud.md`, 사실 index, Task 전수 목록·검증 기록 및 현재 Integration 소유 표.
- 백스테이지 씬·코드: `Assets/Scenes/Tests/BackstageHud/`, `Assets/Scripts/Systems/BackstageHud/`.
- 백스테이지 에셋: `Assets/Resources/Prefabs/UIs/BackstageHud/`, 필요 시 `Assets/Resources/Fonts/BackstageHud/`.
- PM 백스테이지 키트: `Assets/_ThirdParty/curtaincall_backstage_hud_v1/` — 2026-10-20 계획 승인. 신규14 importer/.meta, 제공 Runtime/조립기의 namespace·안전 API·초기화 보정 허용. 재사용18 정본 읽기 전용. 프리팹3종·독립 합성 확인 씬, 전체 원본 보존. 삭제·Git 저장·실게임 연결 제외.
- 백스테이지 기록: `Harness/Project/Facts/backstage-hud.md`, 사실 index, Task 전수 목록·검증 및 Integration 소유 표.
- 알림 씬·코드: `Assets/Scenes/Tests/Notifications/`, `Assets/Scripts/Systems/Notifications/`.
- 알림 에셋: `Assets/Resources/Prefabs/UIs/Notifications/`, 필요 시 `Assets/Resources/Fonts/Notifications/`.
- PM 알림 키트: `Assets/_ThirdParty/curtaincall_notifications_v1/` — 2026-10-20 계획 승인. 신규5 importer/.meta, 제공 Runtime/조립기의 namespace·안전 진입점·전역 초기화 격리 허용. 재사용10 정본 읽기 전용. 프리팹3종·독립 합성 확인 씬·제공 모션 검증, 원본 전체 보존. 삭제·Git 저장·실게임 연결·알림 정책 추가 제외.
- 알림 기록: `Harness/Project/Facts/notifications.md`, 사실 index, Task 전수 목록·검증 및 Integration 소유 표.
- C6 씬·코드: `Assets/Scenes/Tests/Interaction/`, `Assets/Scripts/Systems/Interaction/`.
- C6 에셋: `Assets/Resources/Prefabs/UIs/Interaction/`, 필요 시 `Assets/Resources/Fonts/Interaction/`.
- PM C6 키트: `Assets/_ThirdParty/curtaincall_interaction_v1/` — 2026-10-20 승인 계획에 따른 신규 링2 importer/.meta·제공 Runtime/Builder namespace·안전 진입점·인스턴스 초기화 보정 허용. 재사용6 정본은 읽기 전용, 전체 원본 보존. 삭제·커밋·push·실게임 연결 제외.
- C6 기록: `Harness/Project/Facts/interaction.md`, 사실 index, Task 전수 목록·검증 및 Integration 소유 표.
- 툴팁 씬·코드: `Assets/Scenes/Tests/Tooltips/`, `Assets/Scripts/Systems/Tooltips/`.
- 툴팁 에셋: `Assets/Resources/Prefabs/UIs/Tooltips/`, 필요 시 `Assets/Resources/Fonts/Tooltips/`.
- PM 툴팁 키트: `Assets/_ThirdParty/curtaincall_tooltips_v1/` — 2026-10-20 PM C15 근접·패널 품목 설명 묶음 요청. 제공 명세·코드·조립기를 원위치 재사용하고 namespace/안전 진입점/인스턴스 초기화 및 신규 importer 보정 허용. 독립 확인 씬·표시 API만, 기존 정본/전체 원본 보존·삭제/실게임 연결 없음. 최초 C6 포함 저장 해석은 권한 차단 후 철회·보존. 이후 PM의 "지금까지 변경 사항을 commit, push" 명시 재요청으로 C6·C15 private/public 저장만 승인, 무관 변경 제외. C7은 저장 확인 후 다음 단계다.
- 툴팁 기록: `Harness/Project/Facts/tooltips.md`, 사실 index, Task 전수 목록·검증 및 Integration 소유 표.
- C7 씬·코드: `Assets/Scenes/Tests/Guides/`, `Assets/Scripts/Systems/Guides/`.
- C7 에셋: `Assets/Resources/Prefabs/UIs/Guides/`, 필요 시 `Assets/Resources/Fonts/Guides/`.
- PM C7 키트: `Assets/_ThirdParty/curtaincall_guides_v1/` — 2026-10-20 PM 제작 요청. 제공 Runtime/Builder 원위치 namespace·안전 진입점·인스턴스 초기화 보정과 신규8 importer 허용. 기존6 Sprite는 정본 SHA/규격 확인 후 읽기 전용 참조. 전체 원본 보존·삭제/실게임 연결/키 바인딩 결정/Git 저장 없음. 외부의 상위 HUD 차단은 수동 주입 API만 제공한다.
- C7 기록: `Harness/Project/Facts/guides.md`, 사실 index, Task 전수 목록·검증 및 Integration 소유 표.
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
- `Assets/_ThirdParty/` — 원본 폴더 변경 금지. PM 승인 예외: 이 worktree에 C1 키트 로컬 복사·Unity import/.meta 및 위 승인 정리·버튼·패널·아이콘·공연 HUD·백스테이지·알림·C6·툴팁·C7 가이드 키트의 위 조립 범위만 허용. 메인 원본·다른 private 자산 변경 금지. PNG·제공 코드 공개 재배포 금지.
## 인계 전 체크 (PM)

- [x] 관련 JIRA 번호를 모두 적었다
- [x] Unity에서 테스트 씬을 만들고 저장했다 (`.meta` 생성됨)
- [x] 스크립트·에셋 폴더를 만들었다 (빈 폴더 없음)
- [x] `plan.md`에 공동 테스트 항목을 합의해 적었다

정식 인계 없음. PM 본인의 승인된 로컬 구현이며 assign-task의 자동 커밋·push를 실행하지 않는다.
## 테스트 씬 처리

- 결정: 미정 (유지 / 삭제)
- 결정자·날짜:
