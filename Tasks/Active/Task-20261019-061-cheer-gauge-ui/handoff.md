# Handoff
## 현재
- 메뉴/음성1671f2f commit/push 완료. Start 중앙 로고/세로 메뉴 적용(미커밋), 상세 Docs/Guides/Ui-scene-integration/start-title.md. 화면 캡처 확인, 콜백 검증은 Pipeline 시간 초과로 확인 불가.
- C10 피드백·기본 자금·C12는5433637 commit/push 완료. C13/C14·음성 설정/HUD 후속 적용 완료, 미커밋. [현재 파일/검증/사용자 확인](../../../Docs/Guides/Ui-scene-integration/common-menu-settings.md).
- Assets BasicPanels·SettingsPanel·CommonButtons 인스턴스를 CommonMenuUI/UI에 배치. Start/Lobby 구형 메뉴/설정 삭제. 실제5개 진행 씬 자동 additive·NGO UI 제외·ESC 최상단/커서/LeaveToStart 연결.
- 게임 음량/전체화면·마이크/팀원 전체 음량/음소거 저장. 단원별 로컬 조절은 PlayerId별 이번 실행 유지·채널 변경 시 재적용. 후속 사용자 요청: 작은 마이크 아이콘+M, 음소거 사선 HUD. 실제 음성 송수신은 미실행.
- C10 완성 프리팹 없음 확인 후 Assets 제공 키트 규격으로 신규 Feedback.prefab 조립, CommonPerformanceUI/UI/CommonFeedbackCanvas 연결. 원본/9-slice 보존, PNG 동일 바이트 복사본만 Sprite import.
- 실제 호응 증감 표시·초기/중복/건너뛴 revision·사망/이탈 필터, 손잡기 숫자 없는 안내. 대체 손잡기/목마 임시 표시 억제. Dev 음성창은 C3 아래, 메뉴/관전/결과 숨김. 미대체 외줄 조작 안내 유지.
- 모든 공연 시작 명부수×10을 성공/실패 모두 기존 Settlement 합계에 가산. 장부·중복 정산 방지 유지. C12 기본/호응 보상·이번 총액/잔액 표시.
- 직전856688f commit/push 완료: C12 1~4인·C16·dev 배경 병합·Start/Lobby 카메라/F/C6 수정. 사용자 로비 정상 동작 확인.
- 기존 폰트2/재질3·ui-workspace.md는 별도 보존, 이번 산출물 아님. 기존 stash 유지. commit/push는 새 요청 전 수행하지 않는다.
- 실제 Start/Lobby/WaitingRoomBase/Tightrope/HumanCannon 및 공통 UI만 대상으로 최종 필수 최소 검증한다. 과거 임시 씬 제외.
- 이전 적용: [W2](../../../Docs/Guides/Ui-scene-integration/waiting-room-shop.md), [W3](../../../Docs/Guides/Ui-scene-integration/waiting-room-schedule.md), [이름표/수화기](../../../Docs/Guides/Ui-scene-integration/waiting-room-worldlabels.md), [커서](../../../Docs/Guides/Ui-scene-integration/interaction-cursor.md), [Dev](../../../Docs/Guides/Ui-scene-integration/dev-start.md), [C12](../../../Docs/Guides/Ui-scene-integration/performance-results.md), [C16](../../../Docs/Guides/Ui-scene-integration/curtain-transition.md).
## Verification
- 구역 검사: PASS (10-21)
- 1 문서/규칙: PASS (10-21)
- 2 컴파일: PASS — 별도 recompile_status completed/failed=false. check-work 중복 실행만 생략.
- 3 테스트: PASS — 대기실 실제 포인터 입력으로 설정/취소/나가기 확인/돌아가기/계속하기·EventSystem1개·M/사선 HUD1/1(최종10.16초·아이콘 렌더러 포함). 기존 C13/C14 결과는 가이드 유지.
- C13/C14 1920×1080 캡처 확인. 마이크 HUD/로비 안내 겹침 수정. CommonMenuUI 누락 스크립트0·직렬화 EventSystem0(실행 중 없으면1개 생성)·한글 누락0·dirty=false. 제공 키트 CS0618 및 테스트 종료 Vivox 입장 취소 경고는 실제 통화 성공과 구분.
- C10/C12 이전 검증과 캡처는 performance-feedback.md 유지. 이번 Start/Lobby 저장은 구형 메뉴/설정 삭제·연결 플래그 변경.
- 미실행: 실제2~4인 동시 실행·재접속/네트워크 지연, 물리 손잡기·전체 완주, Mac·다른 화면비·빌드·전체 프로젝트 테스트. 기존 카메라2개/확장 SpaceLink6개 실패는 이전 결과 유지, 이번 재실행 없음.
## 다음/PM 확인
- [남은 계획](../../../Docs/Guides/Ui-scene-integration/next-plan.md): Start 중앙 시안 → Lobby. C16 원격 전원 차폐 ACK·다인 확인 남음.
- 사용자 확인: 실제2기기 음성 송수신·M/마이크 상태·개별 음소거·채널 이동, ESC 메뉴/설정/나가기. 빌드 전체화면과 Mac 미실행. C10 기존 사용자 확인은 이전 가이드 유지.
- 이전 교훈 제안: additive UI/배경 때문에 sceneCount>1로 공간 진입 판단 금지. 실제 SpaceRoute 사용; ShopPanel.Hide의 Closed 이벤트/요청ID/커서 수명; RewardSnapshot 장부-잔액 일치.
- 의료 예약→복구·상점 다인·대기실3일밤→4일아침 사람 동선 확인은 기존 가이드의 미실행 항목 유지.

- 후속: Start 참가/LAN 신규 프리팹 팝업 교체·참가/뒤로 참조 연결. 화면Canvas23개1920×1080 Expand 검증. Play 콜백/빈 코드 PASS, 캡처 확인. 다중 화면비 실제 플레이 미실행. 상세 start-title.md.
