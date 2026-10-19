# 음성 채팅 (생존자 거리 감쇠·사망자 분리)

## 목적

Start에서 시작해 이어지는 흐름의 확정 씬(`Start`·`Lobby`·`WaitingRoomBase`·`Tightrope`·`HumanCannon`)에서 상시 마이크 음성 채팅을 제공한다. 생존자끼리는 거리에 따라, 사망자(추락·리타이어)끼리는 거리와 무관하게 들리고, 두 무리는 서로 들리지 않는다. 결정: [voice-chat.md](../../Harness/Project/Decisions/voice-chat.md).

## 담당 파일 (2026-10-20, Task-20261020-063)

`Assets/Scripts/Systems/Voice/`
- `VoiceChat.cs` — **공개 진입점**. 실행 시 저절로 생성(DontDestroyOnLoad, 프리팹·씬 배치 불필요). Vivox 로그인, 무리에 맞는 채널 입장·전환, 생존 채널 위치 갱신, 음소거 키.
- `VoiceRoom.cs` — 무리 enum(`None`/`Alive`/`Dead`).
- `VoiceRoomRules.cs` — 사망 판정·사용 씬·원하는 무리·채널 이름(순수 함수, 테스트 대상).
- `VoiceRoomKey.cs` — 방 키(내부용). Services 세션 = 세션 Id, LAN = 호스트 키를 NGO named message `CurtainCall.Voice.RoomKey`로 요청·응답.
- `VoiceChatDevOverlay.cs` — **개발자 모드 전용** 확인 화면(IMGUI, 왼쪽 위). `VoiceChat`이 같은 오브젝트에 붙인다.
- `Editor/Tests/VoiceRoomRulesTests.cs` — EditMode.

세팅: 공통 세팅 `음성 채팅`(`BaseGameSettings.VoiceChat`) — 사용, LAN 사용(기본 끔), 사용 씬(기본 위 5개), 대화 거리 2m, 들리는 거리 20m, 감쇠 모양(직선)·세기, 입 높이, 음소거 키(임시 `M`).
테스트 씬 없음: Start 흐름에서만 동작하므로 Start 씬부터 실행해 확인한다(개발자 모드면 확인 화면 표시).
패키지: `com.unity.services.vivox` 16.12.1(통합 준비에서 PM 설치).

## 진입점

```csharp
var voice = VoiceChat.Instance;          // 항상 있음(첫 씬 로드 후)
voice.Room;                              // None / Alive / Dead
voice.RoomChanged += room => ...;        // 입장·전환·나감
voice.IsMicMuted; voice.SetMicMuted(true); voice.ToggleMicMuted();
voice.MicMutedChanged += muted => ...;
voice.Participants;                      // 지금 채널 참가자(VivoxParticipant: DisplayName "P<자리+1>", SpeechDetected, IsSelf)
voice.IsLoggedIn; voice.LastError;
```

## 현재 동작 (구현 사실)

- 동작 조건: 세팅 켬 ∧ Start 흐름(`StartLobbyFlow.Instance` 있음) ∧ 지금 씬(활성 씬) 이름이 `사용 씬`에 있음 ∧ 방 키 ∧ 내 캐릭터(`NetworkPlayer.Local`). 조건이 깨지면 채널에서 나간다. 테스트 씬을 직접 열면 동작하지 않는다.
- 무리: `State`가 `Fallen`·`Retired`면 Dead, 아니면 Alive. 매 프레임 비교해 다르면 나가기→들어가기를 한 번에 하나씩 한다. 실패하면 5초 뒤 다시 시도하고, 계속 실패하면 간격을 두 배씩 최대 60초까지 늘린다(게임은 계속).
- 생존 = Vivox 위치 채널(`cc-<방키>-alive`), 사망 = 그룹 채널(`cc-<방키>-dead`). 한 번에 한 채널만 들어간다.
- 생존 위치: 내 캐릭터 발 + 입 높이를 말하는/듣는 위치로, `Camera.main` 정면을 듣는 방향으로 0.1초 간격·움직였을 때만 `Set3DPosition`.
- 거리·감쇠 값은 채널에 들어갈 때 적용(플레이 중 바꾸면 다음 입장부터).
- 음소거는 `MuteInputDevice`라 무리·씬이 바뀌어도 유지. 글자 입력칸(IMGUI·uGUI·TMP)에 입력 중이면 키를 무시.
- UGS 초기화가 안 됐으면(LAN) 세션과 같은 방식으로 실행마다 다른 익명 프로필로 로그인한다.

## 의존 도메인

- [시작·로비](start-lobby.md) — `StartLobbyFlow.Instance`(Start 흐름 여부).
- [네트워크 세션](network-session.md) — `NetworkSessionManager.Instance`의 접속 상태·`ActiveConnector`(Services 세션 Id).
- [플레이어 동기화](player-sync.md) — `NetworkPlayer.Local`·`State`(호스트 확정 사망 상태).

## 수정 주의점

- Unity Cloud 대시보드에서 프로젝트의 Vivox가 켜져 있어야 로그인된다. 꺼져 있으면 `LastError`·경고 로그만 남는다.
- 백스테이지·탈출로 씬이 확정되거나 흐름 씬 이름이 바뀌면 세팅 `사용 씬`을 고친다(코드 수정 없음).
- 새 사망 계열 `PlayerState`를 추가하면 `VoiceRoomRules.IsDead`와 테스트를 함께 고친다(관전 `CameraSpectator`와 같은 기준 유지).
- LAN으로 음성을 확인하려면 세팅 `LAN 사용`을 켠다(기본 끔: LAN PlayMode 테스트가 로그인하지 않게).
- 같은 PC(MPPM)는 스피커 소리가 마이크로 다시 들어가 울린다. 사람 확인은 PC 2대 또는 이어폰.
