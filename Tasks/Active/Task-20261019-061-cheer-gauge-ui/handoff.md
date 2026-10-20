# Handoff
## 현재
- Dev 시작 창: Mac ⌘+Shift+U / Windows Ctrl+Shift+U / F8·배지. 일차·공연 순번·콘텐츠·아침/공연 사이/밤 선택. 실제 LAN 호스트와 새 테스트 세대로 진입하고 같은 씬도 재로드한다. [수정 파일·사용법·검증](../../../Docs/Guides/Ui-scene-integration/dev-start.md).
- dev4d75f7e 수신/981fe7f 위에서 대기실 HUD·셔터·공통 상호작용·Dev 시작 적용 완료. 2026-10-21 저장 대상은 이 기능·검증·가이드·현재 Task 기록이며 feat/ui-scene-integration에 올린다.
- 셔터를 실제 통로 Z70/폭3m에 맞춰 배율1로 수정. Area_EastRooms가 통로를 포함하던 영역과 Area_Expert 이중 오프셋도 수정했다. 게임 판정 코드 유지.
- CommonInteractionUI 별도 씬 + 4개 공간 UI/InteractionSceneBinding. Assets InteractionPrompt 정본 C6, 기본4m 안 화면에 보이는 최근접2개 외곽선, 이동/시야/가림/삭제/입력 잠금 반영.
- 전화기는 서쪽 배송구 위 world(-18,1.45,70). C6는 실제 ShopReach.Focused에 연결. 스케줄표 F도 공통 선택을 사용하며 기존 작성창·상점 기능은 유지한다.
- [후속 수정 파일·데이터·확인법](../../../Docs/Guides/Ui-scene-integration/interaction-highlights.md), [검증·별도 보존 파일](../../../Docs/Guides/Ui-scene-integration/interaction-verification.md).
- 이전 CommonWaitingRoomUI의 W1/C4/C5/W6·방송·알림 유지. [대기실 HUD](../../../Docs/Guides/Ui-scene-integration/waiting-room-hud.md). 이전 공연 UI와 사용자 P1 Y80 유지.
- ui-workspace.md, private62250960, 기존 public54a6c49/privateaa1c499/dev수신전b00d1b8 stash 보존. 정본 프리팹/PNG/9-slice 수정 없음.
## Verification
- Dev 개별 Compile PASS, EditMode14/14, UI PlayMode27/27, 기본 SpaceLink6/6 PASS. 두 공연·동일 씬 재시작·부상/자금 초기화·밤/마지막 아침·HUD1개 확인. 이전 통로/상호작용 검증 포함.
- SpaceLink 확장 실행은25개 중19 PASS/6 FAIL: proto_0.0.2 기대 불일치1개 및 구형 공연 정산/도착/전환 timeout5개. 전체 통과 아님. 별도 조사 대상으로 dev-start.md에 기록.
- 직접 Play: 2일차 아침·다음 HumanCannon·WaitingForExit·입력 복귀, F8·1920×1080 창/아침 확인. 10-21 단축키 후속 Compile PASS 및 Windows에서 Ctrl/Meta 조합 합성 입력 열림/닫힘·잠금 해제 PASS. 실제 Mac 미실행.
- 1920×1080 전화기·낮/밤 셔터 화면 확인. 인간대포 1인 LAN 호스트에서 실제 NetworkPlayer 위치 변경으로 최근접 대포2개 교체, 뒤를 보면0개 확인.
- 위치/카메라/밤 상태는 API 합성. 대기실 신규 오류/경고0. 인간대포 편집/실행 구간 기존 LabelTMP 폰트 경고11개, 최종 Play console 오류0/경고0. 세부 기록은 위 검증 문서.
- 구역 검사: 최초 FAIL431(기존 병합/다른 Task 문서/구역 중첩). 이전 사용자 저장 예외 승인을 exception.md에 기록한 재검사 PASS(원래431건은 예외로 공개). PR에서 PM 확인 필요.
- 1 문서/규칙: 저장 미리보기 Fast OK(경고10/hard0), diff-check PASS.
- 2 컴파일: 저장 미리보기 Compile PASS, 열린 씬 dirty0 확인.
- 3 테스트: 저장 검사에서는 미재실행. 기능별 실제 실행·실패 결과는 위와 같음.
- 실제 사람 전체 플레이·2~4인·재접속·로비/트램펄린/봉 협동 조작·다른 해상도·빌드·전체 스위트·성능 프로파일 미실행.
## 다음
- Dev 사용법으로 원하는 시점부터 사용자 확인. Task human-verify. 다음 UI는 W4/W5 이름표, Start/Lobby, W2/W3 정본 확인 순서(next-plan.md). 확장 구형 검사 실패는 별도 조사 필요.
- 폰트1/재질3 캐시·직렬화 차이와 ui-workspace.md는 보존하고 이번 커밋에서 제외한다. 복원·삭제 없음. 백업 .harness/local/interaction-preserved-assets/.
## PM이 확인할 것
- 교훈 제안(record-knowledge): 통로를 옮기면 셔터 transform뿐 아니라 WaitingRoomExitWatch의 영역 합도 실제 바닥/벽 경계와 대조해야 함. Z70 테스트가 숨겨진 영역 중첩을 발견했다.
- 교훈 제안: 테스트 종료 시 대기실 씬의 ExpertService를 먼저 unload해야 보상 관리자가 재생성되어 다음 테스트를 오염시키지 않음.
- 교훈 제안: NGO Host 시작 때 로딩 중 additive UI도 초기 씬 목록에 들어갈 수 있어 즉시 Single 로드 시 UnloadSceneAsync=null 예외. UI 작업 완료 후 Host 시작, 공간 전환 중 새 UI 로드 보류로 해결.
