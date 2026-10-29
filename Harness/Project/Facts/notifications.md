---
summary: C8 배너·오른쪽 토스트 정본·표시 API·독립 확인 씬·전체 원본 보존. 알림 UI 사용/검증 전에
status: active
updated: 2026-10-20
source: Task-20261019-061-cheer-gauge-ui
---
# 시스템 배너·토스트
## 언제 읽나
- C8 배너/오른쪽 토스트를 배치하거나 표시 API·정본·제작 원본·검증 제약을 확인할 때.
## 사실
- 정본은 `Assets/Resources/Prefabs/UIs/Notifications/`의 `SystemNotifications.prefab`(두 슬롯), `SystemBanner.prefab`, `Toast.prefab`. 독립 씬은 `Assets/Scenes/Tests/Notifications/Notifications.unity`.
- 공개 Preview/안전 조립기/PlayMode 테스트는 `Assets/Scripts/Systems/Notifications/`. 생성 진입점 `NotificationsProjectBuilder.Build()` 실제 성공, Unity compile errors[] 통과.
- private 제공 Runtime/Builder는 원위치 재사용, namespace `CurtainCall.UI.NotificationsV1`/`.Editor`로 격리. 제공 코드를 공개 복제하지 않는다.
- `Banner.Show(title, subtitle, state, holdSeconds, iconChoice)` / `Toast.Show(message, state, holdSeconds, iconChoice)`를 사용한다. holdSeconds는 호출 시 명시해야 하며 게임 기본 수명을 정의하지 않는다.
- 배너 Normal/Danger, 토스트 Normal/Success/Failure. 기본·명시 아이콘·None, 부제 없음 지원. 같은 슬롯 Show는 즉시 교체하고 동문구도 재시작, 배너/토스트 슬롯은 독립이다.
- unscaled 등장/퇴장: 배너180/160ms·위8UI, 토스트160/140ms·오른쪽16UI, EaseOutCubic. hold는 등장 완료 후 시작하며 DOTween 의존성 추가 없음.
- 무효 hold/state/icon·편집/비활성 Show는 false/기존 내용 보존, 빈 제목/본문은 잘못된 인수 검사보다 먼저 즉시 숨김. 자연 만료/Hide와 HideImmediate/Reset의 문구 초기화 차이 및 첫 프레임 alpha0 가시성은 검증 문서 참고.
- Overlay1920×1080/Match0.5/Canvas PPU100/Sprite PPU200. 배너640×104·상단중앙(0,-244), 토스트384×56·우상단(-48,-160). Bold32/Medium18/토스트Medium24.
- HUD 입력은 비차단, 확인 Controls만 GraphicRaycaster 사용. 실제 Canvas sorting HUD0/Notifications5/Controls10, EventSystem1. 레이어 이름만으로 정렬을 가정하지 않는다.
- Builder는 위험한 편집 상태/dirty 씬/기존 출력 충돌을 사전 차단하고 소유 자산만 저장한다. 전역 PlayReset 대신 인스턴스 초기화 사용; 전역 저장/Selection/삭제 부작용 없이 조립한다. both OFF 지원은 주장하지 않는다.
- private `Assets/_ThirdParty/curtaincall_notifications_v1/`는 meta 제외58실파일/PNG22. 표시15=신규 배너5+기존 정본10, 코드5/미래 제작·확장38을 포함해 원본 전체 보존. 누적96종과 현재 실파일 수를 구분한다.
- 재사용10은 PerformanceHud smallplate5/BasicPanels warning1/CommonIcons4. 정본/importer는 읽기 전용, 신규 배너5만 importer 설정. PNG 바이트·규격·GUID 불일치 시 자동 대체하지 않는다.
- 폰트는 안전한 기존 Static 유지, 동적 C1 문맥 인스턴스만 전용 Static으로 대체. 공유 Dynamic2개·기존 HUD 정본은 수정하지 않는다.
- 확인 씬 C1은 PerformanceHud Canvas 아래, Controls19개 bottom320/row72/status560. C1 Canvas 이탈·P1 가림·과도한 Static 치환은 신규 씬 인스턴스만 증분 보정, builder 동기화.
- DEV MODE/인원규칙 예외 badge는 기존 전역 디버그 표시로 문맥 C1/W1 상단과 겹친다. 기존 표시 정책 변경 없음.
- 실제 게임/네트워크/구매 연결·알림 큐/중복 억제/우선순위·게임 정책·사운드·딤은 구현 범위 밖. 원본 삭제 없음. 2026-10-20 PM 저장 요청으로 private 키트0a705e6c/main1c0f6f0c push, 공개 결과·gitlink는 Task save-work 대상. Task working 유지.
## 확인 방법
- [알림 검증 기록](../../../Tasks/Done/Task-20261019-061-cheer-gauge-ui/notifications-verification.md), [전수 목록](../../../Tasks/Done/Task-20261019-061-cheer-gauge-ui/notifications-inventory.csv).
- 신규 PlayMode 최초8/9(시간정지 Mouse Expected1 Actual0) 후 무수정 단독1/1·전체 재9/9(3.01s), 화면 보정 후 강화9/9를 두 번 통과. 최초 입력 실패 원인 미확정. 독립 Play연속2회·시간 원복·실제 한 delta로 세 phase 소모 검사 통과.
- 실제1920×1080/1600×1000 각22상태·총44장 캡처, 두 report failures[]·Static/glyph/전체빈mesh0. 최종 전체 비교이미지 둘 다 목시 확인, 사람 외형 승인과 구분한다.
- importer15/platform75 PASS·참조201/missing0·원본crop15 pixel 일치·보호baseline111 변경0. 최종check-work 구역/문서/컴파일·EditMode798/798 PASS. ProjectSettings/Packages 미변경, 종료stopped/clean/1920×1080/runInBackground=false/timeScale1 확인.
- 기존 UI 최종 개별 회귀 Backstage7/7·Performance9/9·Cheer3/3·Panels3/3·Icons3/3·Buttons2/2 PASS. Performance/패널 첫 입력 실패 및 수집 응답null 중단 이력은 검증 문서에 보존. 이전 공유 Dynamic폰트2개/Buttons 실패 원인 미결은 최신 통과로 닫지 않는다.
