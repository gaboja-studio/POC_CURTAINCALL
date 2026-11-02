# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

- 2026-11-02: 사용자 선택으로 메인 폴더 fix/fix-systems(=origin/dev 7259e8f4)에서 작업. doctor FAIL은 기존 Task ID 중복·에디터 설치 감지(실제 6000.3.25f1 설치됨).
- 원인: Task-091(864a2877)에서 ItemRightHand 마스크가 오른팔 → 몸통·머리·양팔로 바뀌어, 도구를 들고 걸으면 ItemHold 정지 포즈가 상체 전체를 고정. 팔 흔들기가 사라지고(왼손 앞뒤 폭 480mm → 23mm) 걷기 착지 때 골반이 한 프레임에 16~17mm 떨어지는 충격이 굳은 양팔에 그대로 보였다. 열쇠 전용 문제가 아니라 손에 드는 도구 공통.
- 수정: ItemRightArm 마스크·레이어 추가(ItemArtBuilder.InstallItemLayers, batch executeMethod로 설치). 상호작용 동작만 상체 레이어, 들기·조준·꺼내기·넣기는 오른팔 레이어. 수정 후 측정: 왼손 폭 480mm, 몸통 값은 빈손과 동일.
- 테스트: HumanoidPresentation PlayMode 24/27, 새 HeldKeyKeepsLocomotionTorsoAndLeftArmSwing PASS. 실패 3개(CrawlSlope…, InitialInjuryRebind… = "Crawl" 기대값이 ItemCrawl로 바뀐 기존 코드와 불일치, KeyboardEvents… 웅크리기 입력)는 수정 전 코드에서도 동일하게 실패.
- 배치 Unity 실행마다 escape-labels.asset이 재직렬화되어 되돌림(이번 변경 아님).

- 2026-11-02 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=ENV_BLOCKED(Unity 환경); 테스트=SKIPPED(자동 테스트 선택 안 함)
- check-work: 안전성 PASS, 문서/규칙 PASS, 컴파일 ENV_BLOCKED(열린 에디터 없음). 같은 코드로 배치 unity test가 컴파일·실행됨. 실제 멀티플레이·시각 확인은 미실시.

- 2026-11-02 씬 전환 튕김(대기실→탈출로): 화면 문구는 클라이언트 NGO 자체 Shutdown(TransportClientId≠0, LeaveAsync 아님). 남는 경로는 UTP RelayConnectionStatus.AllocationInvalid → TransportFailure → NGO Shutdown(true). Relay는 약 10초 무요청 시 할당 만료 = 메인 스레드 ~10초 정지. 상세: Harness/Project/Pitfalls/relay-stall-disconnect.md.
- 측정(에디터 배치, 렌더링 없음): W 위 escape 추가 로드 최대 프레임 0.4초, 활성화 1.0초, W 언로드 0.2초 → 이 PC에서는 10초 정지 재현 안 됨. 실제 정지 작업은 미확정(렌더링·GPU 업로드·메모리 후보).
- 수정: NetworkSessionManager OnTransportFailure 처리(종료 문구 TransportFailureMessage, [TransportFailure] 로그), 접속 중 3초 이상 프레임 정지 경고 로그. 회귀 테스트 StartConnectionTests.TransportFailureReportsStallReasonInsteadOfTransportShutdown.

- 2026-11-02 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=ENV_BLOCKED(Unity 환경); 테스트=SKIPPED(자동 테스트 선택 안 함)
