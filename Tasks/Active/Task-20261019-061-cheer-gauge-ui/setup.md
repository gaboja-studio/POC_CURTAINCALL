# Setup
PM 자체 작업. 관련 JIRA 없음. 정식 인계·PR·병합은 별도이며 assign-task 자동 커밋/push는 실행하지 않는다.
최신 승인(2026-10-20): PM 월드 라벨 저장 후 C11 관전·사망 UI 제작 요청 및 settings 중복 타입 최소 수정 승인. 이전 C7/C9 제작 시 저장 제외는 후속 각 저장 요청으로 대체됐다. 삭제·실게임 연결·C11 추가 저장은 제외한다.
원문·지난 승인/체크: [History/002-setup-before-c7-save-compaction.md](History/002-setup-before-c7-save-compaction.md)
## 작업 구역
- 2026-10-20 PM 현재 요청 원문: "유니티에 컴파일 에러가 55개 떠있는데 이걸 먼저 수정한 뒤에 저장해줘." shop9파일 namespace 최소 격리 후 컴파일/테스트 통과, private 저장 완료. 대기 중 월드 라벨 공개 저장까지 포함한다는 AI 해석은 명시적 재승인으로 기록하지 않는다. 공개 commit/push는 권한 검사에서 실행 전 차단되어 사용자 확인 대기. 새 UI 조립·실게임 연결·삭제·무관 변경 저장 제외.
- 2026-10-20 PM 후속 요청: 월드 라벨 private/public commit·push 후 C11 관전·사망 UI 제작. 기존 월드 라벨 저장 제외는 이 요청으로 대체, 무관 변경 저장 제외는 유지한다.
- PM 선택 승인 `최소 수정 포함`: `Assets/_ThirdParty/curtaincall_settings_v1/`의 중복 CommonButtons 타입과 그 참조 namespace만 격리해 컴파일 충돌을 해소한다. 설정 UI 구현·PNG/자료 삭제·무관 코드 변경은 제외한다.
- PM 월드 라벨 v2 요청(2026-10-20): `Assets/Scripts/Systems/WorldLabels/`, `Assets/Scenes/Tests/WorldLabels/`, `Assets/Resources/Prefabs/UIs/WorldLabels/`, `Assets/Resources/Fonts/WorldLabels/` 신규 독립 조립·검증 허용.
- PM 키트 `Assets/_ThirdParty/curtaincall_worldlabels_v2/`: 제공 Runtime/Builder 원위치 namespace·안전 진입점·초기화/명세 불일치 보정 및 신규 importer/.meta 허용. 기존 정본은 읽기 전용 참조, 전체 원본/PNG/GUID 보존. 실게임 연결·삭제·commit/push 제외.
- 월드 라벨 기록: 이 Task 전수 목록/검증, `Harness/Project/Facts/worldlabels.md`·사실 index·현재 Integration 소유 표.
- C1 씬/코드: `Assets/Scenes/Tests/CheerGauge/`, `Assets/Scripts/Systems/CheerGauge/`.
- C1 에셋: `Assets/Resources/Prefabs/UIs/CheerGauge/`, `Assets/Resources/Fonts/CheerGauge/`.
- 버튼 씬/코드: `Assets/Scenes/Tests/CommonButtons/`, `Assets/Scripts/Systems/CommonButtons/`.
- 버튼 에셋: `Assets/Resources/Prefabs/UIs/CommonButtons/`, `Assets/Resources/Fonts/CommonButtons/`.
- PM 버튼 키트: `Assets/_ThirdParty/curtaincall_buttons_v1/` — importer/.meta·기존 조립기 수정, 제공 런타임 재사용 허용. PM 확인·삭제 승인(2026-10-20): 필수 의존성·라이선스·조립기·GUID 보존, 미사용 문서·미리보기·제작 자료만 Unity API로 정리.
- 패널 씬/코드: `Assets/Scenes/Tests/BasicPanels/`, `Assets/Scripts/Systems/BasicPanels/`.
- 패널 에셋: `Assets/Resources/Prefabs/UIs/BasicPanels/`, `Assets/Resources/Fonts/BasicPanels/`.
- PM 패널 키트: `Assets/_ThirdParty/curtaincall_panels_v1/` — 패널7종 importer/.meta·제공 조립기/Runtime·동봉 버튼 namespace 격리 허용. PM 정상 동작 확인 후 저장·정리 승인(2026-10-20): 미참조 중복 문서·검토 자료만 정리, 필요한 설명은 하네스로 이관。다른 패널 제작 원본·공용 리소스·조립기·라이선스·GUID 보존, CommonButtons 정본 읽기 전용.
- PM 패널 기록: `Harness/Project/Facts/basic-panels.md`, 사실 index·현재 Integration 소유 표.
- 아이콘 씬/코드: `Assets/Scenes/Tests/CommonIcons/`, `Assets/Scripts/Systems/CommonIcons/`.
- 아이콘 에셋: `Assets/Resources/Prefabs/UIs/CommonIcons/`, 필요 시 `Assets/Resources/Fonts/CommonIcons/`.
- PM 아이콘 키트: `Assets/_ThirdParty/curtaincall_icons_v1/` — 신규22종 importer/.meta·제공 조립기 경로/namespace/자동화 보정 허용. 기존3종 정본 읽기 전용, 키트 전체 보존. 삭제·실게임 연결 제외; 조립 당시 Git 저장 제외.
- PM 아이콘 기록: `Harness/Project/Facts/common-icons.md`, 사실 index·Task 전수 목록/검증·Integration 소유 표.
- 공연 HUD 씬/코드: `Assets/Scenes/Tests/PerformanceHud/`, `Assets/Scripts/Systems/PerformanceHud/`.
- 공연 HUD 에셋: `Assets/Resources/Prefabs/UIs/PerformanceHud/`, 필요 시 `Assets/Resources/Fonts/PerformanceHud/`.
- PM 공연 HUD 키트: `Assets/_ThirdParty/curtaincall_performance_hud_v1/` — 신규20 importer/.meta·제공 조립기/Runtime namespace·경로·안전 진입점 보정 허용. 기존 아이콘3·C1 정본 읽기 전용, 전체 원본 보존. 삭제·실게임 연결 제외; 조립 당시 Git 저장 제외.
- PM P1 보정 승인(2026-10-20): 최신 외줄 표시 불일치 수정. 위 HUD Runtime 경고 경계 API·5구간 직렬화 참조·조립기·증분 보정 도구·샘플·소유 prefab/씬·테스트 수정 허용. 기존 외줄 씬/Player/Settings 읽기 전용, 실게임 연결·누적 계산·삭제 제외; 조립 당시 Git 저장 제외.
- 공연 HUD 기록: `Harness/Project/Facts/performance-hud.md`, 사실 index·Task 전수 목록/검증·Integration 소유 표.
- 백스테이지 씬/코드: `Assets/Scenes/Tests/BackstageHud/`, `Assets/Scripts/Systems/BackstageHud/`.
- 백스테이지 에셋: `Assets/Resources/Prefabs/UIs/BackstageHud/`, 필요 시 `Assets/Resources/Fonts/BackstageHud/`.
- PM 백스테이지 키트: `Assets/_ThirdParty/curtaincall_backstage_hud_v1/` — 신규14 importer/.meta·제공 Runtime/조립기 namespace·안전 API·초기화 보정 허용. 재사용18 정본 읽기 전용, 프리팹3종·독립 합성 확인 씬·전체 원본 보존. 삭제·실게임 연결 제외; 조립 당시 Git 저장 제외.
- 백스테이지 기록: `Harness/Project/Facts/backstage-hud.md`, 사실 index·Task 전수 목록/검증·Integration 소유 표.
- 알림 씬/코드: `Assets/Scenes/Tests/Notifications/`, `Assets/Scripts/Systems/Notifications/`.
- 알림 에셋: `Assets/Resources/Prefabs/UIs/Notifications/`, 필요 시 `Assets/Resources/Fonts/Notifications/`.
- PM 알림 키트: `Assets/_ThirdParty/curtaincall_notifications_v1/` — 신규5 importer/.meta·제공 Runtime/조립기 namespace·안전 진입점·전역 초기화 격리 허용. 재사용10 정본 읽기 전용, 프리팹3종·독립 합성 확인 씬·제공 모션 검증·전체 원본 보존. 삭제·실게임 연결·알림 정책 추가 제외; 조립 당시 Git 저장 제외.
- 알림 기록: `Harness/Project/Facts/notifications.md`, 사실 index·Task 전수 목록/검증·Integration 소유 표.
- C6 씬/코드: `Assets/Scenes/Tests/Interaction/`, `Assets/Scripts/Systems/Interaction/`.
- C6 에셋: `Assets/Resources/Prefabs/UIs/Interaction/`, 필요 시 `Assets/Resources/Fonts/Interaction/`.
- PM C6 키트: `Assets/_ThirdParty/curtaincall_interaction_v1/` — 신규 링2 importer/.meta·제공 Runtime/Builder namespace·안전 진입점·인스턴스 초기화 보정 허용. 재사용6 정본 읽기 전용·전체 원본 보존. 삭제·실게임 연결 제외; 조립 당시 커밋/push 금지는 후속 C6·C15 명시 저장 승인으로 대체.
- C6 기록: `Harness/Project/Facts/interaction.md`, 사실 index·Task 전수 목록/검증·Integration 소유 표.
- 툴팁 씬/코드: `Assets/Scenes/Tests/Tooltips/`, `Assets/Scripts/Systems/Tooltips/`.
- 툴팁 에셋: `Assets/Resources/Prefabs/UIs/Tooltips/`, 필요 시 `Assets/Resources/Fonts/Tooltips/`.
- PM 툴팁 키트: `Assets/_ThirdParty/curtaincall_tooltips_v1/` — C15 근접·패널 품목 설명. 제공 명세/코드/조립기 원위치 재사용, namespace·안전 진입점·인스턴스 초기화·신규 importer 보정 허용. 독립 확인 씬·표시 API만, 기존 정본/전체 원본 보존·삭제/실게임 연결 없음. C6 포함 저장 최초 해석은 철회 후 PM 명시 재요청으로 C6·C15 private/public 저장만 승인, 무관 변경 제외.
- 툴팁 기록: `Harness/Project/Facts/tooltips.md`, 사실 index·Task 전수 목록/검증·Integration 소유 표.
- C7 씬/코드: `Assets/Scenes/Tests/Guides/`, `Assets/Scripts/Systems/Guides/`.
- C7 에셋: `Assets/Resources/Prefabs/UIs/Guides/`, 필요 시 `Assets/Resources/Fonts/Guides/`.
- PM C7 키트: `Assets/_ThirdParty/curtaincall_guides_v1/` — 제공 Runtime/Builder 원위치 namespace·안전 진입점·인스턴스 초기화 보정·신규8 importer 허용. 기존6 Sprite 정본 SHA/규격 확인 후 읽기 전용 참조. 전체 원본 보존·삭제/실게임 연결/키 바인딩 결정 없음. 상위 HUD 차단은 수동 주입 API만. 최초 Git 저장 제외는 최신 C7 명시 저장 승인으로 대체(공개9216489 push 완료).
- C7 기록: `Harness/Project/Facts/guides.md`, 사실 index·Task 전수 목록/검증·Integration 소유 표.
- C9 씬/코드: `Assets/Scenes/Tests/Captions/`, `Assets/Scripts/Systems/Captions/`.
- C9 에셋: `Assets/Resources/Prefabs/UIs/Captions/`, `Assets/Resources/Fonts/Captions/`.
- PM C9 키트: `Assets/_ThirdParty/curtaincall_captions_v1/` — 제공 Runtime/Builder 원위치 namespace·안전 진입점·인스턴스 초기화 보정 허용. 재사용 PNG9 정본 SHA/규격 읽기 전용 참조, 원본 전체 보존·오디오 동기/큐/노출 정책·실게임 연결·삭제·추가 Git 저장 없음.
- C9 기록: `Harness/Project/Facts/captions.md`, 사실 index·Task 전수 목록/검증·Integration 소유 표.
- 준비 기록: `Integrations/Active/Integration-cheer-gauge-ui/`; Task 문서: 이 Task 폴더(자동 포함).
- PM 승인 C1 정리: `Assets/_ThirdParty/curtaincall_c1_v1/` 미참조 제공 코드·문서·미리보기·제작 자료 및 동반 .meta 삭제. 필수 PNG8·TTF2·라이선스·상위 .meta 보존.
- PM 하네스 기록: `Harness/Project/Decisions/third-party-assets.md`, `Harness/Project/Facts/c1-cheer-gauge.md`, `Harness/Project/Facts/index.md`, `Harness/Project/Facts/common-buttons.md`.
## 예약 구역·배정된 공용 파일
- 예약 없음. 신규 전용 UI `Assets/Resources/Prefabs/UIs/CheerGauge/C1CheerGauge.prefab`; 버튼4종·폰트 `Assets/Resources/Prefabs/UIs/CommonButtons/`, `Assets/Resources/Fonts/CommonButtons/`.
- 최신 PM `C9 커밋하고 push해줘.` 요청으로 위 C9 추가 Git 저장 제외만 대체: 해당 private 키트 저장/main push 및 공개 C9 결과·기록/gitlink 저장 허용. 공유 폰트/Interaction 1.meta/무관 키트·Leartes 변경 제외.
## 수정 금지
- `Assets/Scenes/` 위 전용 테스트 씬 외; `Assets/Scripts/Systems/Reward/` 공개 API 읽기·구독만; `ProjectSettings/`, `Packages/`.
- `Assets/_ThirdParty/` 원본 폴더 변경 금지. PM 예외는 이 worktree C1 로컬 복사/import/.meta·위 승인 정리 및 버튼/패널/아이콘/공연 HUD/백스테이지/알림/C6/툴팁/C7/C9의 위 조립 범위만. 메인 원본·다른 private 자산 변경 및 PNG·제공 코드 공개 재배포 금지.
## 인계 전 체크·테스트 씬 처리
- [x] JIRA·Unity 씬 저장/.meta·스크립트/에셋 폴더(빈 폴더 없음)·plan 공동 테스트 항목 기록 완료. 정식 인계 없음.
- 테스트 씬 유지/삭제 결정 미정; 결정자·날짜 미기입.
