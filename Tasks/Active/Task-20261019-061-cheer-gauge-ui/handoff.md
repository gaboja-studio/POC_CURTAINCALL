# Handoff
## 현재
- 2026-10-21 W4·W5/수화기/커서 수정은 3ac025f로 push 완료. 이후 사용자 요청으로 origin/feat/ui-resources b5bee0e 수신; 제작 Task 문서5개는 History/Ui-resources-b5bee0e 보존, 연결 Task 유지. 이번 수신은 push 미요청.
- [커서 수정·Canvas 연결·사용자 확인](../../../Docs/Guides/Ui-scene-integration/interaction-cursor.md): CameraViewDirector에서 InputGate 직접 연결. UI 열기→표시, 마지막 창 닫기→게임 조작/숨김 복귀, ESC 중복 해제 차단.
- [실제 수정 파일·데이터·사용법·검증](../../../Docs/Guides/Ui-scene-integration/waiting-room-worldlabels.md). Assets WorldLabelCloth/Item/Shelf 정본 사용, package 미사용, 원본/9-slice 유지.
- 수화기: 아트 ba661044와 현재 모두 작은 Cube가 폐소품 옆에 남아 있었음. 기존 Assets 전화기 모델을 서쪽 배송구 위 (-18,1.5,70.8)에 연결. 배송구 -0.6 배출 방향을 +0.6으로 고쳐 벽 뒤 배송 해소.
- UI/WorldLabels: W4 실제 전문가 상태, W5 배송/손/바닥/선반 이름과4칸. 기존 ExpertRoomSign 제거, 네 뷰 IMGUI 이름표 off. 선반 실제 아트/자기 충돌체 연결. C6 주문정보는 보조 줄.
- Dev 시작: Mac ⌘+Shift+U / Windows Ctrl+Shift+U / F8·상단 배지. 실제 LAN 호스트/새 테스트 세대. [Dev 가이드](../../../Docs/Guides/Ui-scene-integration/dev-start.md).
- 최신 수신: W2 ShopPanel, W3 SchedulePanel, C12 ResultsPanel, C14 SettingsPanel/공용3종, C16 TransitionCurtain, TitleLogo. Assets/Resources/Prefabs/UIs/ 정본이며 실제 연결은 후속. 타이틀은 로고만 제공, 전체 메뉴 아님.
- [공통 상호작용](../../../Docs/Guides/Ui-scene-integration/interaction-highlights.md), [기존 검증](../../../Docs/Guides/Ui-scene-integration/interaction-verification.md), [HUD](../../../Docs/Guides/Ui-scene-integration/waiting-room-hud.md).
- private는 수신 gitlink f32c85f로 갱신·clean. 기존 stash 유지, 로컬 폰트1/재질3·ui-workspace.md를 b84517f stash에 보존하고 병합 뒤 복귀. 정본 에셋 충돌 없음. TimeManager 직렬화 형식 변경도 공급본 수신.
## Verification
- 구역 검사: PASS (10-21 실제 공급 -Base origin/feat/ui-resources, 654파일, 기존 exception.md 적용). 기본 통합 기준 검사는 공급 브랜치 불일치로 거부되어 실제 기준을 명시함.
- 1 문서/규칙: PASS (10-21)
- 2 컴파일: PASS (10-21 별도 verify-unity -Compile; check-work 자체는 SkipUnity)
- 3 테스트: 수신 후 UI PlayMode30/30 PASS(ui-resources-b5bee0e-ui30.json), 주요 프리팹6개 로드·MissingScript0·의존파일 누락0, public/private LFS fsck OK. 새 키트별 테스트/실게임 연결/화면 검사는 미실행. 이전 카메라15/17 PASS·2 FAIL 유지.
- 카메라 기존 실패: Locomotion_Crouch...는 모델0.6 축소 기대지만 현 Player.prefab squashWhenCrouched=0. Proto_Tightrope...는 고정3인칭 기대지만 현 공급자는 상황별1인칭. 이번 변경을 뺀 HEAD CameraViewDirector로 동일 실패 확인 후 수정본 복구. 기능/기대값 수정 없음.
- 이전 W4/W5 첫 검사3/4에서 배송 방향 오류 발견/수정 후4/4 통과; 이번 UI30 전체에도 포함. 실제 구매/수령/바닥/선반 요청, 자금/전문가 단계는 주입. .harness/local/w4w5-interaction4-final.json 보존.
- 이전 W4/W5 실제1920×1080 수화기/배송·W4·선반 검사. 장식/판자 가림 보정, Play 프레임2547→3678, 당시 Console 오류0/경고0. 제공 키트 CS0618 컴파일 경고 유지. 이번 커서는 자동 테스트로 확인.
- 종료 상태: Edit mode, WaitingRoomBase + CommonInteractionUI dirty0. 정본 prefab 연결5개 확인. 새 scene 저장 완료.
- 이전 Dev EditMode14/14·UI27/27·기본 SpaceLink6/6 통과. 확장 SpaceLink25는19 PASS/6 FAIL(구형 기대/전환)로 남아 있고 이번에 재실행/해결하지 않음.
- 미실행: 커서의 물리 마우스 클릭/앱 포커스 왕복·Mac 입력, 사람 전체 동선/의료 구매~복구, 2~4인/재접속, 다른 화면비, 빌드/전체 프로젝트 테스트/성능.
## 다음
- Dev로 수화기/주문 물건/손/바닥/선반과 전문가실 상태를 사용자 확인. W4·칸표지는 UI/WorldLabels에서 조절, 숨은 근접 표지는 Preview selected UI in Scene View 메뉴 사용.
- 다음 적용 후보: W2 실제 상점/거래·커서 → W3 일정 표시/기존 편집 규칙 대조 → C12 결과/C16 전환 → C14 설정 → 타이틀 로고. 새 Facts 및 제작 검증 문서의 표시/입력 계약 확인 후 기존 정본을 인스턴스로 연결한다.
## PM이 확인할 것
- 교훈 제안: UIInputBlocker는 입력 요청만 등록하므로 공통 카메라도 InputGate를 읽어야 한다. 창 닫기 ESC를 수동 커서 해제와 중복 처리하면 닫은 후 커서가 계속 남는다.
- 교훈 제안(record-knowledge): 배치 이동 시 모델 유무뿐 아니라 벽 앞면·배송 배출 방향·기존 아트의 큰 충돌 박스를 확인해야 한다. 기존 텍스트 좌표 그대로 UI를 넣으면 벽/장식 뒤에 숨을 수 있다.
- 교훈 제안: WaitingRoomExitWatch는 쉬는 시간에 나간 캐릭터의 재입장을 되돌린다. UI 테스트 순간이동을 방 밖으로 했다가 돌아오면 위치 검사가 실패하므로 내부 동선으로 검사한다.
- 이전 교훈: 통로/셔터와 Area 합 대조; 테스트에서 ExpertService 씬을 먼저 unload; NGO Host 직전 additive UI 로드 완료 대기 및 공간전환 중 UI 로드 보류.
