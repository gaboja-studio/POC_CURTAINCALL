# Plan

## Context

온라인 세션 전 공간(묘기·대기실·백스테이지·탈출로)에서 음성 채팅이 필요하다. 결정: `Harness/Project/Decisions/voice-chat.md`.
생존자끼리는 거리 감쇠(2m 최대·20m 무음), 사망자(`Fallen`·`Retired`)끼리는 거리 무관 같은 크기, 두 무리는 서로 안 들린다. 상시 마이크 + 음소거 키.

확인된 근거
- 세션: `NetworkSessionManager.ActiveConnector`가 `ServicesSessionConnector`면 `CurrentSession.Id`를 양쪽이 같게 안다. UGS 익명 로그인(실행마다 프로필 분리) 완료 상태.
- 사망: `NetworkPlayer.Local.State`·`StateChanged`(호스트 확정, 모든 화면 같음). 관전도 `Fallen`·`Retired`를 사망으로 본다.
- 공간 전환은 NGO 씬 관리로 모두 함께 이동 → 한 시점에 모두 같은 좌표계(거리 계산 유효).
- 게임 코드는 Assembly-CSharp(asmdef 없음). Vivox 런타임 어셈블리는 자동 참조.

가정
- Unity Cloud 대시보드에서 프로젝트(`0cceafe1-…`)의 Vivox가 켜져 있어야 로그인된다(사람이 확인).
- LAN 테스트 경로는 기본 꺼짐(세팅으로 켬). 기존 LAN PlayMode 테스트가 UGS·Vivox 로그인을 시도하지 않게 한다.

## Approach

1. **채널 2개(Vivox)**: 생존 = 위치 채널(`JoinPositionalChannelAsync`, `Channel3DProperties` 2/20m), 사망 = 그룹 채널(`JoinGroupChannelAsync`, 거리 무관). 한 번에 하나만 들어가 있어 서로 안 들린다(클라이언트 음소거가 아니라 채널 자체 분리).
2. **방 키**: Services 세션이면 `CurrentSession.Id`. LAN(세팅 켬)이면 호스트가 만든 GUID를 NGO named message로 요청·응답. 채널 이름 = `cc<방키>a`/`cc<방키>d`(Vivox 허용 문자만).
3. **상태 맞추기(reconcile)**: 원하는 무리 = (접속됨 ∧ 방 키 ∧ 내 캐릭터) ? (사망 ? Dead : Alive) : None. 현재와 다르면 나가기 → 들어가기를 한 번에 하나씩 순서대로. 끝나면 다시 비교(연속 상태 변화 대응).
4. **위치**: 생존 채널에 있는 동안 내 캐릭터 머리 높이를 말하는/듣는 위치로, 카메라 정면을 듣는 방향으로 `Set3DPosition`. 0.1초 간격 + 움직였을 때만.
5. **로그인 순서**: UGS 초기화(없으면 같은 프로필 규칙) → 익명 로그인 → `VivoxService.InitializeAsync`(1회) → `LoginAsync`(1회). 이벤트는 호출 전에 구독, 파괴 시 해제. 접속이 끊기면 채널 모두 나가기.
6. **음소거**: `MuteInputDevice`/`UnmuteInputDevice`. 키는 세팅(`Key`, 임시 `M`), 채널을 바꿔도 유지.
7. **자동 생성**: `RuntimeInitializeOnLoadMethod`로 DontDestroyOnLoad 관리자 1개. 공용 프리팹·NetworkManager 수정 없음.
8. **세팅**: `BaseGameSettings` 끝에 `음성 채팅` 묶음(사용 여부, LAN 사용, 대화 거리, 들리는 거리, 감쇠 세기·모델, 머리 높이, 음소거 키).
9. **테스트 씬**: NetworkPlayerSync 구성 복제 + `VoiceChatTestUI`(IMGUI: 무리·채널·참가자 말하기 표시·음소거·내 추락 요청·호스트 모두 재시작).

## Files

- 새로 만듦 (`Assets/Scripts/Systems/Voice/`)
  - `VoiceChat.cs` — 공개 진입점(관리자, 상태·이벤트·음소거)
  - `VoiceRoom.cs` — 무리 enum(None/Alive/Dead)
  - `VoiceRoomRules.cs` — 사망 판정·원하는 무리·채널 이름(순수 함수)
  - `VoiceRoomKey.cs` — 방 키 결정(Services 세션 Id / LAN named message)
  - `VoiceChatTestUI.cs` — 테스트 씬 전용 IMGUI
  - `Editor/Tests/VoiceRoomRulesTests.cs` — EditMode
- 공용: `BaseGameSettings.cs`·`.asset`(음성 채팅 묶음), `Docs/Domains/index.md`
- 문서: `Docs/Domains/voice-chat.md`
- 씬: `Assets/Scenes/Tests/Systems/VoiceChat/VoiceChat.unity`
- 읽기 전용 참고: `NetworkSessionManager`, `ServicesSessionConnector`, `NetworkPlayer`, `PlayerState`, `CameraSpectator`

## Risks

- Vivox 대시보드 미설정이면 로그인 실패 → 경고 로그만, 게임은 계속(음성 없음).
- 같은 PC MPPM은 스피커 소리가 마이크로 다시 들어가 울림. 실제 확인은 PC 2대 권장.
- 채널 전환 중 약 0.5~1초 무음. 사망 직후·부활 직후 잠깐 안 들릴 수 있다.
- 진행 중 참가 거절 정책 때문에 늦은 참가는 없음(로비에서 참가 시 그대로 동작).
- Set3DPosition 과다 호출 → 간격·이동 임계로 제한.

## Auto Verification

- 구역 검사: `verify-scope`
- 1 문서/규칙: `verify-fast`
- 2 컴파일: `verify-unity -Compile`
- 3 테스트: EditMode `VoiceRoomRulesTests`(사망 판정·무리 결정·채널 이름 규칙) + 기존 테스트 회귀
- 4 실행 로그: 테스트 씬 1인 호스트(Services)로 로그인·생존 채널 입장 로그 확인

## 공동 테스트 항목

테스트 씬 `Assets/Scenes/Tests/Systems/VoiceChat/VoiceChat.unity`, PC 2대(또는 이어폰 낀 2인), 호스트 1 + 참가 1, 테스트 UI "방 만들기(Services)" → 방 코드로 참가. 기록: handoff "공동 테스트 결과"에 예상/실제.

1. 생존 거리 감쇠: 둘 다 생존, 붙어서 말하면 크게 → 10m쯤 멀어지면 작게 → 20m 넘으면 안 들린다.
2. 사망 분리: A가 "내 추락"을 누르면 A↔B가 서로 안 들린다. 둘 다 추락하면 거리와 상관없이 같은 크기로 들린다. 호스트 "모두 재시작" 후 다시 거리 감쇠로 들린다.
3. 음소거 키: `M`을 누르면 상대에게 내 목소리가 안 들리고 UI가 꺼짐 표시, 다시 누르면 들린다. 사망·부활로 무리가 바뀌어도 음소거 상태가 유지된다.
