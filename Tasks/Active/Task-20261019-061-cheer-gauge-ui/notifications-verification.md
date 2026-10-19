# Notifications 검증
## 범위·상태 (2026-10-20)
- Task working. 승인 계획 `C:/Users/imvag/.claude/plans/rustling-foraging-llama.md`, 작업 구역 setup.md. 메인 세션에서 실제 실행·관찰한 결과를 기록한다.
- C8 시스템 배너·오른쪽 토스트의 제공 표시/모션·독립 합성 씬 검증. 실제 게임/네트워크/구매·알림 큐/중복 억제/우선순위·게임 정책·사운드·딤 연결 없음.
- PM 저장 요청으로 알림 private136파일 커밋 `0a705e6cd6cac63dad3d2a3effe6b980cc596b53` push, 자동README 반영 main `1c0f6f0c94ac9d2ff9f13a7a79a9b6553ca572e3` 확인. 공개 알림 결과는 이 문서 포함 save-work 실행. 기존 private220변경/4886삽입/931삭제·미추적 unity_only·다음 C6 키트는 제외·보존. 삭제·PR·공개 통합 병합 없음.
## 제작·컴파일
- 제공 Runtime/Builder namespace 격리, 안전 조립 진입점·Preview·PlayMode9건 작성.
- U+2028/U+2029 literal 컴파일 오류 수정 후 Unity compile errors[] 통과.
- `NotificationsProjectBuilder.Build()` 실제 성공. `Assets/Resources/Prefabs/UIs/Notifications/`에 SystemNotifications/SystemBanner/Toast 프리팹3종, `Assets/Scenes/Tests/Notifications/Notifications.unity` 생성 완료.
- API/hold 필수/unscaled/레이어·폰트·정본 계약: [프로젝트 사실](../../../Harness/Project/Facts/notifications.md).
## 신규 PlayMode
| 실행 | 결과 | 제한 |
|---|---|---|
| 최초 전체9건 | 8/9 | 15번 시간정지 Mouse: Expected1 Actual0 |
| 코드 수정 없이 입력 단독 | 1/1 PASS | 최초 실패 원인을 확정하지 않음 |
| 코드 수정 없이 전체 재실행 | 9/9 PASS, 3.01s | 최초 실패 이력은 삭제하지 않음 |
| 화면 보정 후 강화 전체 | 9/9 PASS | C1 Canvas·비활성 포함 Static/글리프 검사 추가 |
| 기존 UI 회귀 후 강화 전체 재검사 | 9/9 PASS | 최초 실패 원인 해결로 간주하지 않음 |
- 최초 입력 실패 원인 미확정. 재실행 통과를 원인 해결 또는 일관된 재현성 확보로 해석하지 않는다.
## 화면 검사·증분 보정
- 발견: 문맥 C1이 Canvas 밖, Controls가 P1을 가림, 정적 문맥 폰트가 과도하게 치환됨.
- 기존 정본이 아닌 신규 확인 씬 인스턴스만 증분 보정: C1을 PerformanceHud Canvas 아래 배치, 기존 Static 유지/동적 C1만 전용 Static, Controls19개 bottom320+row72/status560. builder도 동기화.
- 실제 Play1920×1080/1600×1000 각22상태(11샘플×어두운 공연/밝은 백스테이지), 총44장 캡처. 정적 목업 PNG와 구분한다.
- 두 report failures[]; Static/glyph/전체빈mesh0 보고.
- 이미지 열람: 최종 1920/1600 전체 비교이미지 모두 메인 Read로 목시 확인. 긴 문구 Ellipsis·제어문자·None·단독 표시·밝은 배경 및 주변 HUD 간격을 확인했으며, 사람 외형 승인과 구분한다.
- DEV MODE·인원규칙 예외 badge는 기존 전역 디버그 표시로 문맥 C1/W1 상단과 겹침. 기존 전역 표시 정책은 변경하지 않았다.
- 사람 최종 외형 승인·공동/네트워크 검증은 위 합성/캡처 검사와 별개다.
## 전수·참조·보호 검사
- [notifications-inventory.csv](notifications-inventory.csv): meta 제외 원본58실파일/PNG22, 표시15=신규 배너5+재사용 정본10, 코드5/미래 제작·확장38 전부 보존. 누적96종과 현재 실파일 수 구분.
- 재사용 정본10: PerformanceHud smallplate5/BasicPanels warning1/CommonIcons4. 읽기 전용 대조, 신규5만 importer 설정; 기존 정본 재생성/정책 수정 없음.
- 원본 시트 crop15 pixel 일치. importer15/platform75 PASS.
- refs201/missing0, EventSystem1, 실제 Canvas sorting HUD0/Notifications5/Controls10, GraphicRaycaster Controls1, 검사 failures[].
- 보호 baseline111 변경0. 기존 정본/공유 Dynamic폰트·무관 private 변경 보존; ProjectSettings/Packages 미변경.
## 환경·종료 제약
- Play 캡처 동안 Application.runInBackground=true 임시 사용 후 false 복원, timeScale1 확인.
- 독립 확인 씬 Play 연속2회: 매 진입 ActionCount0/두 슬롯 숨김/timeScale1, Pause→Normal 총2입력·비활성 시간 복구·재활성 Reset 통과. 각 진입에서 모션 공개 Configure로 세 phase 길이를 실제 unscaledDeltaTime의 1/8씩 설정, 실제 Update 한 번으로 전부 소모됨 확인 후 원래 길이 복구(실행 인스턴스만).
- 최종 Play stopped/Notifications 씬 clean/1920×1080/timeScale1/runInBackground=false 확인. Domain/Scene reload 설정 변경 및 both OFF 지원 주장 없음.
- doctor는 이미 실행·보고된 상태로 반복하지 않음. 기존 보존할 private 변경 관련 FAIL은 지우지 않는다.
## 남은 검증·기존 미결
- 첫 개별 회귀: Notifications9/9·Backstage7/7·Cheer3/3 PASS. Performance8/9(WarningButton… 478행 Expected1 Actual0), Panels2/3(Input…149행 Expected1 Actual0). Icons 상태 응답 data=null에서 수집 스크립트가 AttributeError로 중단, 후속 직접 조회3/3 PASS. 응답 null 원인 미확정.
- 안전한 응답 처리로 재개한 개별 회귀: Icons3/3·Buttons2/2·Performance9/9·Panels3/3·Notifications9/9 PASS. 기존 제품 코드/폰트 수정 없이 재실행했으며 입력 실패 원인은 미확정. 전체 PlayMode 일괄 검사는 미실행.
- 최종 `pwsh -NoProfile -File Scripts/check-work.ps1` exit0: 구역·문서/규칙·컴파일 PASS, EditMode798/798 PASS. 이전 HUD 검사 결과가 아닌 이번 실제 실행 결과다. 기록 후 문서 변경분은 verify-fast로 재검사한다.
- 공유 Dynamic TMP2개(CheerGauge Bold/CommonButtons SemiBold)는 시작부터 dirty, baseline95항목 중93동일/2개 추가 SHA 차이. 원인 미확정·이전 dirty 백업 없음; 임의 복구/폰트 재작성 금지·PM 확인 필요.
- 이전 Buttons 전체2건 FAIL(글리프/후속 클릭), 빈 호스트 재현·입력 단독1/1 PASS 이력은 [backstage-hud-verification.md](backstage-hud-verification.md) 보존. 이번 전체2/2 PASS지만 baseline111 변경0이며 원인 해결 조치는 없었으므로 기존 공유 폰트/실패 원인 미결은 유지한다.
- 지식 정책 record-knowledge/knowledge-routing/context-budget 준수. 사용법·원본 보존·API는 프로젝트 사실에, 실제 실행 이력·실패·제약은 이 문서에 분리했다.
