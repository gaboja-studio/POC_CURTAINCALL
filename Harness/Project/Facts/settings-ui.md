---
summary: C14 설정 창·공용 탭/슬라이더/토글의 독립 조립과 staged 적용 토큰 계약
status: active
updated: 2026-10-28
source: Task-20261019-061, Task-20261027-076
---

# C14 설정 UI

## 언제 읽나
- 설정 창/공용 컨트롤을 조립하거나 실제 옵션 서비스에 연결하기 전에.

## 소유·제작 상태
- `Assets/Scripts/Systems/SettingsUi/` 전용 코드로 `Assets/Resources/Prefabs/UIs/SettingsUi/`에 SettingsPanel.prefab 및 공용 탭/슬라이더/토글 총4개, `Assets/Scenes/Tests/SettingsUi/SettingsUi.unity` 독립 씬 조립 완료. 독립 제작·자동/실행 캡처 검증 완료이며 상세는 [Task 검증 기록](../../../Tasks/Done/Task-20261019-061-cheer-gauge-ui/settings-ui-verification.md) 기준이다.
- Spectator Medium/WorldLabels Bold는 읽기 전용 재사용, `Assets/Resources/Fonts/SettingsUi/`에 전용 SemiBold Static8192(전체 한글+ASCII11325문자) 생성.
- 보정 전 Compile·C14 PlayMode23/23, 1920×1080/1920×1200 각 명암·세 탭·0/50/100%·disabled/focus 12장씩24 실행캡처 확인. 화면비별 합성 raycast15/클릭6/Move6·Preview 클릭17/비활성 콜백 차단1 통과(물리 Mouse/Keyboard·사람 승인 아님). 첫 입력 실패는 활성 직후 Graphic.depth=-1인 렌더 등록 전 검사 때문이며 QA220ms 대기로 통과, 제품 Runtime 불변.
- 참조2484/missing0/MissingScript0·기존 출력 덮어쓰기 거절, baseline626 대비 승인 Builder1/신규 importer20 외 변경0·PNG변경0/누락0. 당시 audit 씬 clean/Play stopped/timeScale1/runInBackgroundfalse.
- Preview 독립 샘플은1888×96 dark/raycastfalse 국소 배경과 함께 하단 고정해 밝은 배경 대비와16:10 중앙 패널 겹침을 방지한다. 실제 프리팹 색/입력 계약은 불변이다. 보정 후 두 화면비×명암24캡처·패널/샘플 비중첩·합성 입력 통과, 최종 Compile/C14 Play23/23·C16 회귀16/16·check-work/EditMode798/798 PASS. 물리 입력·사람 외형/실게임 검증과 commit/push는 별도다.
- 기존 `Assets/Scripts/Systems/Settings/`는 게임 규칙 설정이며 UI 소유 구역이 아니다.
- private `curtaincall_settings_v1` 제공 Runtime 원위치 사용. 복제 버튼만 `CurtainCall.UI.SettingsV1.CommonButtons`, 나머지 설정/공용 컨트롤은 `CurtainCall.UI`이다. 전체 원본/PNG/GUID/자료 보존, 재사용16 Sprite·기존 폰트 읽기 전용/신규20 importer만 보정한다.

## 실제 게임 연결 (Task-076, 2026-10-28)
- 아래 C14 원본 계약은 보존한다. 실제 게임은 `CommonSettingsUI`+`SettingsTabsView`의 게임/그래픽/조작/사운드/기타5탭을 사용하고 기존3탭 인스턴스는 숨긴다. 게임 탭은 런 진행 중만 활성, 긴 목록은 스크롤. 씬/프리팹/private 원본 변경 없음.
- 그래픽은 화면3모드·지원 해상도·FPS30/60기본/90/120/144/165/240/무제한. `ClientSettings` PC별 저장/기존 bool 이관, VSync0·시작/변경 시 FPS 적용(공통 Base 매 프레임 강제 제거), 에디터 Screen 변경 없음. 모든 탭 draft는 적용 때 저장·취소 때 폐기.
- 조작은 `InputBindingPreferences` Common 키10행·복합 축 part 재지정/중복 거절/기본값 복원/저장, 기존·새 Reader에 적용. ESC·M·마우스 시점·게임패드는 고정. 사운드는 전체/BGM/SFX·전체 음성/내 마이크/팀원별·마이크on/off·Vivox 입력/출력 목록/선택. `LocalAudioController.Register`로 BGM/SFX 분류, 현재 BGM 재생 코드 없음. 게임 출력 기기는 OS가 관리한다.
- 사용자 결정: 런 포기는 온라인 호스트 전용(오프라인 허용). 대기실 쉬는 시간/밤 재확인 후 `CampaignRun.AbandonRun`→정식 실패→다음 세대1일차 첫 공연, 방 유지. 권한/상태/전환·오래된 확인 재검사. 기타는 확인된 개발자·에셋 출처/Pretendard OFL 원문, BM 등 미확인 라이선스는 확인 중 표시.
- 현재5탭 검사: 입력17/17·음량7/7·설정3/3·메뉴/포인터3/3. 실제2인·장치 전환·PC 빌드 화면/재실행·게임패드 실기와 전체 라이선스 검토는 미완료. 이전 런 상태6/6·그래픽21/21은 앞 묶음 결과다.

## 표시·입력 계약
- 1920×1080/Match .5/PPU100 Canvas, Sprite PPU200. 패널784×552·stage 등비 축소, 전체 화면 딤/입력 차단판 없음. 외부 HUD 입력 차단·일시정지·커서 소유는 제공하지 않는다.
- Native Toggle/ToggleGroup 탭, Slider, Toggle와 별도 visual/focus. 값 초기화는 WithoutNotify. Slider handle의 Y sizeDelta=0으로 실제32×32를 유지하며 좌우 Navigation은 비워 native 값 변경을 사용한다.
- Play 초기 Hidden, `OpenDemo()`는 명시적 예시만. 실제 항목/기본값/범위/단위·저장/적용/취소 정책 결정이 아니다.

## 적용 서비스 계약
- `Open(SettingsPageDefinition[], gameplayContext)`는 1..3페이지×0..3행을 검증·복사한다. 페이지 ID 유일/행 ID 전체 유일, 유한0..1/Toggle 정확0 또는1. 탭 이동에도 편집값 보존, `GetSnapshot()`은 독립 읽기 전용 값 복사다.
- dirty일 때 `RequestApply()`→요청별 SessionVersion 증가→busy/Cancel만 유지→`ApplyRequested(snapshot)` 발송. Unity main thread에서 성공 후에만 `AcceptApplied(version)`, 실패는 `RejectApply(version,message)`로 응답한다. 이전/중복/취소한 토큰은 거부한다.
- Apply 구독이 없으면 연결 안내 후 재조작 가능. 서비스 예외는 외부 어댑터가 RejectApply로 변환해야 한다.
- Cancel/Hide는 UI 편집값 폐기일 뿐 이미 시작한 외부 쓰기 취소/롤백이 아니다. Cancel은 CancelRequested+Closed, Hide는 Closed만 통지한다. 취소 입력은 소유 컨트롤의 CancelRelay이며 전역 Escape polling 없음.
- AudioListener/Screen/PlayerPrefs/씬·네트워크·실제 설정 서비스는 연결하지 않는다. 사람 외형/실게임 입력 소유·safe area·공동/플랫폼 검증은 자동 검사와 별도다.
