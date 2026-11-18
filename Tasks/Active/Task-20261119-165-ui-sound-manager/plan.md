# Task-20261119-165

- **Title:** UI 사운드 매니저와 결정 사운드 적용
- **Type:** feat
- **Assignee:** @MoHoDu
- **Branch:** feat/ui-sound-manager
- **Base:** dev

## Context
사운드 시트(Google Sheet `1hy9U_T6...`) UI 탭에서 결정된 행(URL 칸이 채워진 UI-104·701·905·906)의 원본 에셋을 `_ThirdParty`에 넣고 게임에 연결한다.
클립을 갈아끼우고 음량·구간을 조절할 수 있도록 사운드 매니저·상황별 큐 데이터·설정 창을 먼저 만든 뒤 적용한다.

## 계획
- `Assets/Scripts/Systems/Sound/`: `SoundCatalog`(SO, Resources/Settings/Sound/SoundCatalog) + `SoundCue`(ID·클립·음량·피치·시작 위치·길이·페이드·지연·재사용 간격·믹서 그룹), `SoundManager.Play(id)`(2D 풀, LocalAudioController 경유), `SoundCueIds`(코드 연결 ID).
- 설정 창: `Tools/CurtainCall/사운드 설정` — 큐 추가·삭제, 클립 교체, 재생 구간 슬라이더, 미리듣기(편집 모드 가능), 플레이 중 게임 재생.
- 연결: UI-104 `ExplorationTimerView`(임시 합성 종소리 제거), UI-701 `PerformanceEndingPresentation.ObserveDeath`·`CommonWaitingRoomUI.ObserveDeaths`(내 단원만), UI-905·906 `SceneIntegration.RestTimerView.Show`(`RestTimerSoundCues`, 표시 상태 Warning/Critical 문턱과 동일).

## 2차 (적용 상황·API·탭)
- 재생 API: `SoundManager.Play(상황 키 | 시트 ID [, SoundPlayOptions])` → `SoundHandle`(루프 Stop·음량 배율). 옵션: Loop·VolumeScale·Pitch·Delay·IgnoreCooldown. `PlayLoop`, `Stop`, `StopAll`, 재생 기록(`History`, 호출 파일·줄).
- 데이터 2층: 사운드(시트 ID: 클립·음량·구간·루프) ← 적용 상황(코드 키 → 시트 ID·켜기·음량 배율). 키 상수 `SoundSituations`.
- 적용 상황 6개: 소집 경보(ExplorationTimerView), 공연 중 내 사망(PerformanceEndingPresentation → HudUIManager.ShowDeathWarning), 대기실 내 사망(CommonWaitingRoomUI → 같은 함수), 탈출로 내 사망·포획(새 EscapeLocalDeathSound, 카드 없음), 타이머 30초·10초 틱(RestTimerView).
- 설정 창 탭: 사운드 / 적용 상황(시트 ID 선택·켜기·배율·코드 위치 링크·코드 한 줄 복사·추가/삭제/복원) / 재생 기록.
- 테스트 씬: 실제 함수 경로로 재현(비활성 ExplorationTimerAnnouncer + ExplorationTimerView, ShowDeathWarning, EscapeLocalDeathSound.Notify, RestTimerView).

## 미정·주의
- UI-906 시트 URL 칸에 `UIClick_Verbsynth UI Bell`도 적혀 있으나 임포트된 것은 `UI Back 1`뿐이라 Back 1을 연결했다.
- 탈출로에는 현재 카운트다운이 없어 905·906은 대기실 휴식·밤 정비 타이머에만 울린다.
- 동료 사망은 UI-701이 아니라 경고 배너음(UI-102, 미결정) 몫으로 남겼다.
- 탈출로 사망은 시트 설명(사망 카드)과 달리 카드가 없어 상태 변화로 연결했다. 원치 않으면 적용 상황 탭에서 끈다.
- 시트 README의 Master_BGM/SFX/UI 믹서는 아직 없다. 큐마다 믹서 그룹 칸만 두었고 음량은 기존 LocalAudioController(효과음·배경음 설정)를 거친다.
