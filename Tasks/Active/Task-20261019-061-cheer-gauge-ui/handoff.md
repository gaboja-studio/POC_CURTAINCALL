# Handoff
## 현재
- C10 피드백·공연 HUD 보완과 인원×10 기본 자금·C12 변경 완료, 미커밋. [파일/데이터/사용자 확인](../../../Docs/Guides/Ui-scene-integration/performance-feedback.md).
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
- 2 컴파일: PASS — 별도 Unity CLI recompile_status completed/failed=false. check-work는 중복 실행만 생략.
- 3 테스트: PASS — 별도 집중 검사 EditMode 보상47/47·C10필터1/1, 실제 두 공연 PlayMode1/1(32.99초). 합성 호응/손잡기/종료 API·C12·커튼 검증. check-work에서만 중복 실행 생략.
- 1920×1080 C10/C12 캡처 확인. 최종 Console 오류0/경고0, 편집 모드 Start dirty=false. 프리팹12 Image/누락Sprite0. 제공 키트 기존 CS0618 컴파일 경고와 실행 오류 구분.
- 초기 설치 eval5초 시간 초과 후 실제 프리팹/씬 저장 확인, 재설치하지 않음. 검증 후 진단 호출의 임시 Dev 객체는 즉시 정리, Start 저장 변경 없음.
- 미실행: 실제2~4인 동시 실행·재접속/네트워크 지연, 물리 손잡기·전체 완주, Mac·다른 화면비·빌드·전체 프로젝트 테스트. 기존 카메라2개/확장 SpaceLink6개 실패는 이전 결과 유지, 이번 재실행 없음.
## 다음/PM 확인
- [남은 계획](../../../Docs/Guides/Ui-scene-integration/next-plan.md): C13/C14 공통 메뉴/설정 → Start 중앙 시안 → Lobby. C16 원격 전원 차폐 ACK·다인 확인 남음.
- 사용자 확인: Dev로 두 공연 진입, 실제 호응 조건→C10/C1 일치, 손잡기/사망 표시, 실패 결과 기본 자금과 대기실 장부. C10 위치는 공통 씬 C10_Feedback RectTransform에서 조절.
- 이전 교훈 제안: additive UI/배경 때문에 sceneCount>1로 공간 진입 판단 금지. 실제 SpaceRoute 사용; ShopPanel.Hide의 Closed 이벤트/요청ID/커서 수명; RewardSnapshot 장부-잔액 일치.
- 의료 예약→복구·상점 다인·대기실3일밤→4일아침 사람 동선 확인은 기존 가이드의 미실행 항목 유지.
