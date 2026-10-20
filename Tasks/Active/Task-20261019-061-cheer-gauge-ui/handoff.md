# Handoff
## 현재
- 2026-10-21 W4·W5/수화기 및 상호작용 커서 수정, human-verify. 사용자 commit/push 요청으로 feat/ui-scene-integration에 선택 저장. 기존 폰트/재질 캐시4개와 ui-workspace.md는 제외·보존.
- [커서 수정·Canvas 연결·사용자 확인](../../../Docs/Guides/Ui-scene-integration/interaction-cursor.md): CameraViewDirector에서 InputGate 직접 연결. UI 열기→표시, 마지막 창 닫기→게임 조작/숨김 복귀, ESC 중복 해제 차단.
- [실제 수정 파일·데이터·사용법·검증](../../../Docs/Guides/Ui-scene-integration/waiting-room-worldlabels.md). Assets WorldLabelCloth/Item/Shelf 정본 사용, package 미사용, 원본/9-slice 유지.
- 수화기: 아트 ba661044와 현재 모두 작은 Cube가 폐소품 옆에 남아 있었음. 기존 Assets 전화기 모델을 서쪽 배송구 위 (-18,1.5,70.8)에 연결. 배송구 -0.6 배출 방향을 +0.6으로 고쳐 벽 뒤 배송 해소.
- UI/WorldLabels: W4 실제 전문가 상태, W5 배송/손/바닥/선반 이름과4칸. 기존 ExpertRoomSign 제거, 네 뷰 IMGUI 이름표 off. 선반 실제 아트/자기 충돌체 연결. C6 주문정보는 보조 줄.
- Dev 시작: Mac ⌘+Shift+U / Windows Ctrl+Shift+U / F8·상단 배지. 실제 LAN 호스트/새 테스트 세대. [Dev 가이드](../../../Docs/Guides/Ui-scene-integration/dev-start.md).
- 이전 적용: dev4d75f7e/981fe7f 수신, 셔터 Z70/폭3m와 공간영역 수정, CommonInteractionUI 최근접2개, 대기실 W1/C4/C5/W6·방송·알림, 공연 UI/P1 Y80 유지.
- [공통 상호작용](../../../Docs/Guides/Ui-scene-integration/interaction-highlights.md), [기존 검증](../../../Docs/Guides/Ui-scene-integration/interaction-verification.md), [HUD](../../../Docs/Guides/Ui-scene-integration/waiting-room-hud.md).
- private62250960 clean, public54a6c49/privateaa1c499/dev수신전b00d1b8 stash 보존. 기존 폰트1/재질3 캐시·ui-workspace.md 보존, 복원/삭제 없음.
## Verification
- 구역 검사: PASS (10-21, 기존 명시 예외 적용; 원래 병합/중첩431건 exception.md)
- 1 문서/규칙: PASS (10-21)
- 2 컴파일: PASS (10-21 별도 verify-unity -Compile; check-work 자체는 SkipUnity)
- 3 테스트: UI PlayMode30/30 PASS, 카메라15/17 PASS·2 FAIL(변경 전/후 동일). .harness/local/interaction-cursor-ui30.json 및 camera17-first/baseline 기록. 커서 수정 전1/1 실패→후1/1 통과; 실제 LAN 호스트/공개 API/합성 ESC, 물리 입력 검증 아님.
- 카메라 기존 실패: Locomotion_Crouch...는 모델0.6 축소 기대지만 현 Player.prefab squashWhenCrouched=0. Proto_Tightrope...는 고정3인칭 기대지만 현 공급자는 상황별1인칭. 이번 변경을 뺀 HEAD CameraViewDirector로 동일 실패 확인 후 수정본 복구. 기능/기대값 수정 없음.
- 이전 W4/W5 첫 검사3/4에서 배송 방향 오류 발견/수정 후4/4 통과; 이번 UI30 전체에도 포함. 실제 구매/수령/바닥/선반 요청, 자금/전문가 단계는 주입. .harness/local/w4w5-interaction4-final.json 보존.
- 이전 W4/W5 실제1920×1080 수화기/배송·W4·선반 검사. 장식/판자 가림 보정, Play 프레임2547→3678, 당시 Console 오류0/경고0. 제공 키트 CS0618 컴파일 경고 유지. 이번 커서는 자동 테스트로 확인.
- 종료 상태: Edit mode, WaitingRoomBase + CommonInteractionUI dirty0. 정본 prefab 연결5개 확인. 새 scene 저장 완료.
- 이전 Dev EditMode14/14·UI27/27·기본 SpaceLink6/6 통과. 확장 SpaceLink25는19 PASS/6 FAIL(구형 기대/전환)로 남아 있고 이번에 재실행/해결하지 않음.
- 미실행: 커서의 물리 마우스 클릭/앱 포커스 왕복·Mac 입력, 사람 전체 동선/의료 구매~복구, 2~4인/재접속, 다른 화면비, 빌드/전체 프로젝트 테스트/성능.
## 다음
- Dev로 수화기/주문 물건/손/바닥/선반과 전문가실 상태를 사용자 확인. W4·칸표지는 UI/WorldLabels에서 조절, 숨은 근접 표지는 Preview selected UI in Scene View 메뉴 사용.
- 후속 UI 순서는 next-plan.md 참고. W4/W5 완료를 반영하며 미제작 W2/W3 등은 Assets 정본 유무를 다시 확인한다.
## PM이 확인할 것
- 교훈 제안: UIInputBlocker는 입력 요청만 등록하므로 공통 카메라도 InputGate를 읽어야 한다. 창 닫기 ESC를 수동 커서 해제와 중복 처리하면 닫은 후 커서가 계속 남는다.
- 교훈 제안(record-knowledge): 배치 이동 시 모델 유무뿐 아니라 벽 앞면·배송 배출 방향·기존 아트의 큰 충돌 박스를 확인해야 한다. 기존 텍스트 좌표 그대로 UI를 넣으면 벽/장식 뒤에 숨을 수 있다.
- 교훈 제안: WaitingRoomExitWatch는 쉬는 시간에 나간 캐릭터의 재입장을 되돌린다. UI 테스트 순간이동을 방 밖으로 했다가 돌아오면 위치 검사가 실패하므로 내부 동선으로 검사한다.
- 이전 교훈: 통로/셔터와 Area 합 대조; 테스트에서 ExpertService 씬을 먼저 unload; NGO Host 직전 additive UI 로드 완료 대기 및 공간전환 중 UI 로드 보류.
