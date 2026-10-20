# Handoff
## 현재
- 2026-10-21 W2 상점·서비스·장부 적용 완료, commit/push 미요청. [실제 수정 파일·사용자 확인·검증](../../../Docs/Guides/Ui-scene-integration/waiting-room-shop.md).
- Assets ShopPanel 정본을 CommonWaitingRoomUI/UI/ShopPanelCanvas 인스턴스로 연결. WaitingRoomBase Phone의 구형 IMGUI off, ExpertServicePhonePanel 제거. 원본 프리팹/9-slice/package 변경 없음.
- WaitingRoomShopUI가 실제 품목/가격/잔액/최근20 장부·배송/수령, 의료/시설 서비스 연결. 기존 거래 API에 요청ID 왕복·응답 이벤트 추가, 가격/판정 유지. 커서·입력과 씬 수명은 기존 공통 구조 사용.
- 무응답15초는 장부·배송 확인 안내(거래 취소/자동 재시도 아님). 판매 API 없음. 게임 품목 자체는 기존 임시 도구/배낭이며 UI 데모 품목은 추가하지 않음.
- 이전 연결3ac025f push 후 사용자 요청으로 ui-resources b5bee0e 수신92effe2. private gitlink f32c85f, 제작 Task 문서는 History/Ui-resources-b5bee0e 보존.
- 이전 W4·W5/수화기: [가이드](../../../Docs/Guides/Ui-scene-integration/waiting-room-worldlabels.md). 서쪽 배송구 위 Phone(-18,1.5,70.8), 배송 방향+0.6. 실제 데이터·선반4칸/소지 표지 연결.
- [커서](../../../Docs/Guides/Ui-scene-integration/interaction-cursor.md), [상호작용](../../../Docs/Guides/Ui-scene-integration/interaction-highlights.md), [대기실 HUD](../../../Docs/Guides/Ui-scene-integration/waiting-room-hud.md), [공연](../../../Docs/Guides/Ui-scene-integration/performance-upgrade.md).
- Dev 진입: Mac ⌘+Shift+U / Windows Ctrl+Shift+U / 상단 배지. [사용법](../../../Docs/Guides/Ui-scene-integration/dev-start.md). 새 테스트 세대는 자금0.
- 기존 폰트1/재질3·ui-workspace.md 변경은 별도 보존하며 이번 작업 산출물/저장 범위 아님. 기존 stash 유지.
## Verification
- 구역 검사: PASS (10-21)
- 1 문서/규칙: PASS (10-21)
- 2 컴파일: PASS (별도 verify-unity -Compile; check-work 자체는 SkipUnity)
- 3 테스트: W2 신규3/3·기존 포함 UI33/33 PASS, 문구 축약 후 최종W2도3/3(w2-final3.json). 자금·신체 손실은 주입, 거래/예약은 실제 LAN 호스트/API/버튼 이벤트. fixture 첫 오류와 Pipeline 재시도는 가이드 기록.
- 실제1920×1080 세 탭 확인, 시설 강화 자금500→480·장부 일치, EventSystem1개. Play 프레임6027→18741, 현재 Console 오류0/경고0. 캡처 w2-items/services/ledger.png. 설명 말줄임은 문구 두 줄로 축약.
- 종료: Edit mode, WaitingRoomBase+CommonWaitingRoomUI dirty0, 원래 Dev 설정 복원. 재로드 정본 연결/참조·MissingScript0·구형 서비스0 확인. 제공 키트 CS0618 등 기존 컴파일 경고와 실행 오류 구분.
- 기존 카메라15/17의2 FAIL(구형 crouch 축소·고정1인칭 기대), 확장 SpaceLink25 중6 FAIL(구형 씬/전환)은 이전 기록 유지·이번 재실행 없음. 기본 SpaceLink6/6·Dev EditMode14/14는 이전 결과.
- 미실행: 실제2~4인 동시 요청/재접속·지연/끊김, 물리 마우스·Mac 입력·앱 포커스 왕복, 의료 예약→입장→복구 전체 사람 동선, 다른 화면비·빌드·전체 프로젝트 테스트/성능.
## 다음
- W2 사용자 확인: F 열기·세 탭·자금 있는 런의 구매/수령·의료1/3일 밤·시설2일 밤·ESC/닫기/커서. 상세 가이드 참조.
- [남은 계획](../../../Docs/Guides/Ui-scene-integration/next-plan.md): W3(1~3일,4일 편성 요구 제거) → 기록/C12 → C16 → C10/HUD → 메뉴/설정 → 중앙 타이틀 시안 → Lobby → 전체 검증.1인 Dev 요약 재사용.
- 기존 상단 Dev·밤 안내/음성 테스트 표시 겹침은 후속 HUD 정리 항목. W2로 대체되지 않은 시스템 UI는 이번에 제거하지 않음.
## PM이 확인할 것
- 교훈 제안(record-knowledge): ShopPanel의 Closed는 Hide에서도 발생한다. 이전 요청ID를 폐기하고 Phone/InputGate를 함께 닫아야 재열기·씬 전환 후 잔상이 없다.
- 교훈 제안: RewardSnapshot은 장부 마지막 잔액과 Balance 일치를 검증한다. 테스트 자금만 주입하면 스냅샷 실패하므로 테스트 장부도 함께 준비한다.
- 이전 교훈: UIInputBlocker와 카메라 커서 연결/ESC 중복 소비; 배치 시 벽 앞면·배송 방향·아트 충돌체 확인; UI 순간이동은 방 내부에서; NGO Host 직전 additive UI 로드 완료 대기.
