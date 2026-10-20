# C13·C14 공통 메뉴와 음성 설정

2026-10-21. 직전 C10/기본 자금 작업은5433637로 feat/ui-scene-integration push 완료. 이 문서의 후속 변경은 미커밋.

## 구성
- `Assets/Scenes/UI/CommonMenuUI.unity`: UI 아래 C13_MenuCanvas, C14_SettingsCanvas, C14_TeammateSettingsCanvas, ModalBackdrop, VoiceStatusCanvas. 실제 Start/Lobby/WaitingRoomBase/Tightrope/HumanCannon에서 로컬 additive 로드. 공간 EventSystem을 우선 사용하고, 대기실처럼 없으면 공통 UI가 InputSystemUIInputModule과 함께 하나만 생성한다. 공간 EventSystem이 복귀하면 자체 것은 비활성화·제거한다.
- Assets 완성 BasicPanels/SettingsUi/SettingsPanel/CommonButtons/Button_General 프리팹 인스턴스. 원본 프리팹·이미지·9-slice·importer 보존. 추가 한글은 기존 SettingsUi 폰트를 인스턴스에 지정한다.
- Start의 설정 버튼은 공통 C14, 나머지 공간은 ESC로 C13. 계속하기/설정/나가기 확인. 방장은 방 종료 안내, 단원은 이탈 안내. 나가기 확정은 기존 StartLobbyFlow.LeaveToStart·네트워크 종료/공연 이탈 판정 사용.
- Start/Lobby 씬에서 대체된 임시 SettingsPanel·로비 메뉴를 삭제. 구형 제작/테스트 씬·범용 클래스는 유지.
- ESC는 가장 위 창 하나만 닫는다. 상점/스케줄 등 다른 입력 점유 중에는 공통 메뉴를 열지 않는다. InputGate로 이동·상호작용을 막고 커서를 표시한다. 계속하기 뒤 마우스 시점 공간은 잠금/숨김 복귀, 로비/Start는 기존 커서 정책. Time.timeScale 변경 없음.

## 설정·음성 연결
| 화면 | 실제 연결 |
|---|---|
| 게임 | ClientSettings.Volume → AudioListener, Fullscreen → 화면 모드, PlayerPrefs 저장. 조작 안내 |
| 내 마이크 | VoiceChat 입력 음량·음소거. 적용 후 PlayerPrefs 저장, M 단축키도 동일 상태 저장 |
| 팀원 음성 | Vivox 출력 음량·전체 로컬 음소거. 게임 효과음과 별도, 적용 후 저장 |
| 단원별 음성 | 현재 음성 채널의 최대3명, PlayerId 기준 로컬 음량/음소거. 해당 창 적용이 독립 확정이며 뒤의 전체 설정 초안은 유지 |
| 좌하단 마이크 표시 | 72×36의 테두리 없는 마이크 선 아이콘 + M. 음소거/입력0일 때 붉은 사선, 켜짐은 밝은 아이콘. M 또는 커서가 있을 때 클릭 |

- 전체 설정은 적용할 때만 반영한다. 취소/ESC는 초안 폐기. 단원별 설정도 자신의 적용/취소를 사용한다.
- Vivox 장치·단원 음량은 100%=SDK 기본0, 0%=음소거, 중간값은-50~0으로 매핑. 증폭 최대치+50을 기본값으로 사용하지 않는다.
- 장치 설정은 다음 실행에도 유지. 개별 단원 설정은 이번 실행의 PlayerId로 유지하고 생존/사망 채널 입장 시 재적용. 자리 번호 재사용으로 다른 단원에게 적용되지 않는다. 다른 사람의 송신 장치를 끄는 기능은 아니다.
- 음성 연결 전에도 장치 설정 가능하며 Vivox 초기화 후 적용된다. 생존 거리 감쇠·사망자 채널 분리 규칙은 그대로다.
- 실제 음성 채널에 다른 단원이 없으면 단원별 버튼을 비활성화하고 이유를 표시한다. HUD는 사용자 요청에 따라 마이크 on/off만 표시한다. 음성 연결/발화 진단은 기존 Dev 음성창을 사용한다.

## 수정 파일
- 씬: CommonMenuUI 신규, Start/Lobby 임시 UI 정리, EditorBuildSettings에 공통 씬 등록.
- SceneIntegration: CommonMenuUI, CommonSettingsUI, VoiceStatusUI, MicrophoneStatusIcon, CommonUiSceneLoader, Editor/CommonMenuInstaller, Tests/PlayMode/CommonMenuPlayTests 및 신규 meta.
- Systems/StartLobby: StartMenu·LobbyRoom 공통 메뉴 진입. Systems/Voice: VoiceChat·VoiceChatPreferences 장치/개별 설정, VoiceChatDevOverlay 메뉴 중 숨김.
- Task setup/todo/plan/handoff와 이 가이드·next-plan 업데이트. 기존 폰트2/재질3/ui-workspace.md는 보존하며 이번 산출물에서 제외.

## 검증
- Unity recompile completed/failed=false. 구역·문서 검사 PASS. check-work는 이미 실행한 Unity 검증의 중복 실행만 SkipUnity로 생략.
- 새 집중 PlayMode2/2: 실제5개 진행 씬, ESC·설정 취소/적용·마이크 저장·메뉴 중 시간/프레임 진행·커서·로비 EventSystem1개·호스트 나가기/Start 복귀.
- 음성 전체/게임 음량 적용 추가 후 해당 검사1/1 재통과(13.24초). 상점/스케줄 ESC·커서 회귀1/1 통과(7.55초).
- 1920×1080 C13/C14 캡처 검토. 로비 조작 안내와 마이크 HUD 겹침 수정. 기존 공용 Medium 폰트의 '갑' 누락을 발견해 완성 SettingsUi 폰트 참조로 교체(원본 수정 없음).
- 기존 제공 키트 CS0618 컴파일 경고 존재. 테스트 종료 때 Vivox 채널 입장 취소 경고 발생, 실제 통화 성공으로 간주하지 않는다.
- 미실행: 실제2~4인 음성 송수신/단원별 청취·재접속, 단원 클라이언트 나가기와 호스트 종료의 원격 화면, Mac·게임패드·전체화면 빌드·다른 화면비·전체 테스트. 개별 단원 UI는 실제 상대 음성 연결 필요.

## Unity 사용자 확인
1. Start 설정에서 게임 음량·전체화면·마이크/팀원 설정을 바꾸고 취소/적용 비교. 다시 열거나 재실행해 적용 값 유지 확인. 전체화면은 빌드에서 확인.
2. Lobby/대기실/두 공연에서 ESC → 설정 → ESC → 메뉴 → ESC. 메뉴 중에도 불/공연은 진행되고 닫으면 이동·시점 복귀. 상점/스케줄 ESC는 해당 창만 닫힘.
3. 두 기기로 같은 방에서 M 음소거와 HUD 사선 표시, 내 마이크 크기, 팀원 전체 음량/음소거 확인. 팀원 음성 탭의 단원별 버튼으로 특정 단원만 조절. 생존/사망 채널 이동 후 적용 유지 확인.
4. 나가기 확인의 돌아가기는 게임 유지, 확정은 Start 복귀. 방장 종료 시 다른 단원도 복귀하는지 확인.
5. 편집은 CommonMenuUI의 해당 패널을 선택하고 Tools/Curtain Call UI/Preview selected UI in Scene View. 마이크 위치는 VoiceStatusCanvas/MicrophoneStatus RectTransform. 설치 메뉴는 최초 조립용이며 이미 적용된 씬에 재실행하지 않는다.

## 후속 수정: 작은 마이크·대기실 클릭 (2026-10-21)
- 대기실에는 기본 EventSystem이 없고 상점/스케줄은 열 때 생성했다. 공통 메뉴는 표시/API만 검사하여 실제 클릭 누락을 놓쳤다. CommonMenuUI가 공간 EventSystem 부재 시 생성하고 공간 EventSystem 복귀 시 자신의 것을 즉시 끈 뒤 제거하도록 수정.
- 큰 프리팹 버튼 표시를 테두리 없는72×36 마이크 선 아이콘+M으로 변경. 원본 프리팹은 보존. 사선과 붉은색으로 음소거 표시. 좌하단(24,70), 기존 조작 안내 위에 배치.
- 실제 WaitingRoomBase에서 합성 Mouse 상태를 InputSystemUIInputModule에 전달해 설정/취소/나가기 확인/돌아가기/계속하기 클릭, 최상단 Raycast 대상, EventSystem1개, M 상태/아이콘 검증1/1 통과(18.26초). 최초 테스트는 Start 흐름 없는 Dev 진입이라 M 조건 실패; 실제 조건을 갖춰 재검증. 컴파일·구역·문서 통과. 캡처 compact-microphone.png 확인.
- 이번 수정의 실제 다인·Mac·전체 흐름 재검증은 미실행.
- 최종 캡처에서 아이콘 CanvasRenderer 누락 발견·수정, 렌더러 검증 추가 후 같은 집중 검사1/1 재통과(10.16초), 아이콘+M 화면 확인.
