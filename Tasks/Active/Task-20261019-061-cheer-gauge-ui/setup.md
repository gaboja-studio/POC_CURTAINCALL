# Setup
PM 자체 작업. 관련 JIRA 없음. 정식 인계·PR·병합은 별도이며 assign-task 자동 커밋/push는 실행하지 않는다.
최신 승인(2026-10-20): C11 완료 보고 후 사용자 직접 "저장해줘" 요청으로 이번 C11 결과·기록 및 Schedule namespace 충돌 수정의 private 저장/main push·공개 결과/gitlink 저장을 승인했다. 아래 C11/Schedule 추가 저장 제외는 제작 당시 경계이며 이 저장 요청으로 대체한다. 기존 공유 폰트·Interaction 1.meta·정책/브랜치 전략·무관 private 키트/Leartes 변경 저장·삭제·실게임 연결·PR/공개 통합 병합은 제외한다.
원문·지난 승인/체크: [History/002-setup-before-c7-save-compaction.md](History/002-setup-before-c7-save-compaction.md)
## 작업 구역
- 2026-10-20 PM 질문 응답 `최소 수정 후 계속`: 새 `Assets/_ThirdParty/curtaincall_results_v1/`의 중복 CommonButtons 타입 namespace와 ResultsPanelView/Builder 참조만 최소 격리해 C16 컴파일 차단을 해소한다. results UI 제작·PNG/GUID/자료 변경·삭제·commit/push 제외.
- 2026-10-20 PM 현재 요청: "C16 장소 전환 커튼 묶음 UI를 작업하자. _ThirdParty\\curtain_v1로 저장되어 있어." 실제 경로 `Assets/_ThirdParty/curtaincall_curtain_v1/`의 제공 Runtime/Builder 원위치 안전 보정·namespace 격리·신규 커튼6 importer 조정 허용. 기존 별 정본·Static Medium은 읽기 전용 재사용하고 전체 원본/PNG/GUID/자료를 보존한다.
- C16 전용 구역: `Assets/Scripts/Systems/TransitionCurtain/`, `Assets/Scenes/Tests/TransitionCurtain/`, `Assets/Resources/Prefabs/UIs/TransitionCurtain/`, 필요 시 `Assets/Resources/Fonts/TransitionCurtain/`. 독립 조립·표시 API·확인 씬·자동 검증, Task 기록·`Harness/Project/Facts/transition-curtain.md`·사실 index·Integration 소유 표 허용. 실제 장소 로드/네트워크/입력 제한/영속화·삭제·commit/push·PR/병합 제외.
- 전체 업로드 준비의 전송 설정: GitHub 100MB 제한에 걸린 C11 전용 Static 폰트 한 파일을 `.gitattributes`에서 LFS로 지정한다. 폰트 실제 바이트·GUID·공유 폰트의 전송 방식은 바꾸지 않는다. 거절된 미공개 커밋만 amend하며 원격 이력 재작성·강제 push는 하지 않는다.
- 2026-10-20 PM 후속 직접 승인: 작업과 무관한 에셋도 예외적으로 전부 저장·push한다. 현재 공개 공유 폰트·Interaction 1.meta 및 private Leartes/미추적 키트 전체를 포함하고, 기존 UI 공급 방향 정책/브랜치 전략 기록3파일도 함께 저장한다. 제작 브랜치를 원격에 준비해 `feat/ui-scene-integration`에서 사용자가 직접 pull하도록 한다. 수신 worktree 수정·자동 병합·강제 이동·삭제는 하지 않는다. 아래 과거 무관 변경 저장 제외를 이번 저장에 한해 대체한다.
- 2026-10-20 사용자 직접 요청 "버그를 먼저 수정하고 계속 진행하자.": 현재 차단 원인인 `Assets/_ThirdParty/curtaincall_schedule_v1/` 제공 C#의 중복 버튼 타입 및 참조 namespace만 최소 격리하고 컴파일 확인 후 C11을 계속한다. Schedule 기능 제작·원본/PNG/GUID 변경·삭제·추가 commit/push는 제외.
- 2026-10-20 현재 PM 요청: 대기 저장 완료 후 `Assets/_ThirdParty/curtaincall_spectator_v1/` C11 관전/사망 UI를 진행한다. 제공 Runtime/Builder 원위치 namespace·안전 진입점·초기화 보정 및 신규 비네트2 importer 수정 허용. 기존8 Sprite 정본은 읽기 전용 참조, 원본/PNG/GUID/라이선스 보존.
- C11 전용 허용: `Assets/Scripts/Systems/Spectator/`, `Assets/Scenes/Tests/Spectator/`, `Assets/Resources/Prefabs/UIs/Spectator/`, `Assets/Resources/Fonts/Spectator/`. 독립 조립·확인 씬·Static Medium·표시 API/샘플/자동 검증과 Task 기록, `Harness/Project/Facts/spectator.md`·사실 index·현재 Integration 소유 표. 실게임/네트워크/카메라/사망 판정·슬롯 중재 정책 구현·삭제·C11 추가 commit/push는 제외.
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
