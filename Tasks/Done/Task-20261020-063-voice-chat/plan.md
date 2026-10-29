# Plan

## Context

Start에서 시작해 이어지는 실제 흐름의 씬에서 음성 채팅이 필요하다. 결정: `Harness/Project/Decisions/voice-chat.md`.
확정 씬(2026-10-20 PM): `Start`·`Lobby`·`WaitingRoomBase`(대기실)·`Tightrope`(줄타기)·`HumanCannon`(인간 대포). 백스테이지·탈출로는 씬이 확정되면 세팅 목록에 추가한다.
생존자끼리는 거리 감쇠(2m 최대·20m 무음), 사망자(`Fallen`·`Retired`)끼리는 거리 무관 같은 크기, 두 무리는 서로 안 들린다. 상시 마이크 + 음소거 키.

확인된 근거
- Start 흐름: `StartLobbyFlow`(DontDestroyOnLoad)가 Start 씬에서 생겨 흐름 내내 유지된다. 테스트 씬을 직접 열면 없다.
- 세션: `NetworkSessionManager.ActiveConnector`가 `ServicesSessionConnector`면 `CurrentSession.Id`를 양쪽이 같게 안다. UGS 익명 로그인(실행마다 프로필 분리) 완료 상태.
- 사망: `NetworkPlayer.Local.State`(호스트 확정, 모든 화면 같음). 관전도 `Fallen`·`Retired`를 사망으로 본다.
- 공간 전환은 NGO 씬 관리로 모두 함께 이동 → 한 시점에 모두 같은 좌표계(거리 계산 유효).
- 게임 코드는 Assembly-CSharp(asmdef 없음). Vivox 런타임 어셈블리는 자동 참조.

가정
- Unity Cloud 대시보드에서 프로젝트(`0cceafe1-…`)의 Vivox가 켜져 있어야 로그인된다(사람이 확인).
- 후속 PR #190으로 Start 메뉴의 LAN 접속도 기본 음성 켜짐으로 dev 반영 완료. 실제 2인 송수신 검증은 별도로 남아 있다(기존 LAN 기본 꺼짐 전제를 대체).

## Approach

1. **동작 조건**: 세팅 켬 ∧ Start 흐름(`StartLobbyFlow.Instance` 있음) ∧ 현재 씬 이름이 세팅 `사용 씬` 목록에 있음. 아니면 채널에서 나간다.
2. **채널 2개(Vivox)**: 생존 = 위치 채널(`JoinPositionalChannelAsync`, 2/20m 직선 감쇠), 사망 = 그룹 채널(`JoinGroupChannelAsync`, 거리 무관). 한 번에 하나만 들어가 서로 안 들린다(채널 자체 분리).
3. **방 키**: Services 세션이면 `CurrentSession.Id`. LAN(세팅 켬)이면 호스트가 만든 GUID를 NGO named message로 요청·응답. 채널 이름 = `cc-<방키>-alive`/`-dead`.
4. **상태 맞추기**: 원하는 무리를 매 프레임 비교, 다르면 나가기 → 들어가기를 한 번에 하나씩. 실패 시 5초부터 최대 60초 간격으로 재시도.
5. **위치**: 생존 채널에서 내 캐릭터 입 높이를 말하는/듣는 위치로, 카메라 정면을 듣는 방향으로 `Set3DPosition`(0.1초 간격·움직였을 때만).
6. **로그인 순서**: UGS 초기화 → 익명 로그인 → `VivoxService.InitializeAsync` → `LoginAsync`(각 1회).
7. **음소거**: `MuteInputDevice`/`UnmuteInputDevice`, 키는 세팅(임시 `M`), 무리가 바뀌어도 유지. 입력칸 입력 중엔 무시.
8. **자동 생성**: `RuntimeInitializeOnLoadMethod`로 DontDestroyOnLoad 관리자 1개. 공용 프리팹·씬 수정 없음.
9. **세팅**: `BaseGameSettings` 끝에 `음성 채팅` 묶음(사용, LAN 사용, 사용 씬, 거리·감쇠, 입 높이, 음소거 키).
10. **확인 화면**: 개발자 모드(`DevMode.Enabled`)일 때만 뜨는 IMGUI 오버레이(무리·채널·마이크·참가자 말하기). 별도 테스트 씬 없음.

## Files

- 새로 만듦 (`Assets/Scripts/Systems/Voice/`)
  - `VoiceChat.cs` — 공개 진입점(관리자, 상태·이벤트·음소거)
  - `VoiceRoom.cs` — 무리 enum(None/Alive/Dead)
  - `VoiceRoomRules.cs` — 사망 판정·원하는 무리·사용 씬·채널 이름(순수 함수)
  - `VoiceRoomKey.cs` — 방 키 결정(Services 세션 Id / LAN named message)
  - `VoiceChatDevOverlay.cs` — 개발자 모드 확인 화면
  - `Editor/Tests/VoiceRoomRulesTests.cs` — EditMode
- 공용: `BaseGameSettings.cs`·`.asset`(음성 채팅 묶음), `Docs/Domains/index.md`, `Docs/Domains/voice-chat.md`
- 삭제: 인계 때 만든 `Assets/Scenes/Tests/Systems/VoiceChat/`(Start 흐름에서만 동작하므로 불필요, 2026-10-20 PM)
- 읽기 전용 참고: `StartLobbyFlow`, `NetworkSessionManager`, `ServicesSessionConnector`, `NetworkPlayer`, `PlayerState`, `DevMode`

## Risks

- Vivox 대시보드 미설정이면 로그인 실패 → 경고 로그만, 게임은 계속(음성 없음).
- 같은 PC MPPM은 스피커 소리가 마이크로 다시 들어가 울림. 실제 확인은 PC 2대 권장.
- 채널 전환 중 약 0.5~1초 무음(사망·부활·씬 목록 밖 전환 직후).
- 씬 이름으로 판단하므로 씬 이름을 바꾸면 세팅 목록도 고쳐야 한다.

## Auto Verification

- 구역 검사: `verify-scope`
- 1 문서/규칙: `verify-fast`
- 2 컴파일: `verify-unity -Compile`
- 3 테스트: EditMode `VoiceRoomRulesTests`(사망 판정·무리·사용 씬·채널 이름) + 기존 테스트 회귀
- 4 실행 로그: Start 흐름 1인 호스트(Services, 개발자 모드)로 로그인·로비 생존 채널 입장 로그

## 공동 테스트 항목

Start 씬에서 시작, PC 2대(또는 이어폰 낀 2인), 호스트 "방 만들기" + 참가 "방 코드 참가"(Services). 개발자 모드면 왼쪽 위 음성 확인 화면이 뜬다. 기록: handoff "공동 테스트 결과"에 예상/실제.

1. 생존 거리 감쇠: 로비·대기실에서 붙어서 말하면 크게, 멀어지면 작게, 20m 넘으면 안 들린다. 줄타기·인간 대포로 넘어가도 계속 들린다.
2. 사망 분리: 줄타기에서 A가 떨어지면(추락) A↔B가 서로 안 들린다. 둘 다 떨어지면 거리와 상관없이 같은 크기로 들린다. 다음 시간대에 부활하면 다시 거리 감쇠로 들린다.
3. 음소거 키: `M`을 누르면 상대에게 내 목소리가 안 들리고, 다시 누르면 들린다. 사망·부활·씬 이동 후에도 음소거 상태가 유지된다.
