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

- 2026-11-02 열쇠 줍기 정책: 처음 놓인 열쇠는 ExplorationGate(IsKeyPickup)라 ShopReach가 조작(1) 정책을 적용하고 있었다. 떨어진 열쇠(FloorPropsView)는 이미 줍기(4). ShopReach.MotionKind로 분리하고 IsKeyPickup도 줍기(4)로 매핑. 애니메이션(Motions[1])과 시간(1.4초)은 그대로이며 바뀐 것은 정책뿐.
- 테스트(열린 에디터): HumanoidPlayerTests 24/27, 새 FirstKeyPickupUsesPickupPolicyLikeOtherPieces PASS. 실패 3개는 앞서 기록한 기존 실패와 동일.

- 2026-11-02 사용자 확인: 열쇠 줍기 중 key 설정의 이동 허용을 켜도 막힘. 열쇠 줍기가 pickup 설정을 읽고 있었음. RequestItemMotion(kind, point, policyKind)로 동작과 설정 항목을 분리하고 ShopReach.PolicyKind: 처음 놓인 열쇠·떨어진 열쇠(FloorPropsView.Item.IsKey) = 열쇠(3) 설정, 동작은 줍기(4).
- 반대 방향 키: 1D Axis 합성은 동시 입력 시 0(정지). PlayerInputReader가 OppositeKeyAxis로 Move(W/S)·Posture(A/D)·AuxDirection(Q/E)을 마지막에 누른 방향으로 읽고 먼저 누른 키는 다시 누를 때까지 무시. 키 재설정은 합성 부분 이름으로 찾아 유지, 합성이 아닌 바인딩은 기존 값.
- 테스트(열린 에디터): HumanoidPlayerTests 26/29. 새 KeyPickupPlaysPickupAndFollowsKeyPolicy·PolicyKindDecidesMovementLockSeparatelyFromMotion·OppositeKeysFollowLastPressedDirection PASS. 오버로드 추가로 테스트 Call 도우미를 인자 수로 고르게 수정. 실패 3개는 기존과 동일(Crawl 기대값 2, 웅크리기 입력 1).
- 2026-11-02 열쇠 줍기 이동 재확인(실제 WaitingRoomBase, Dev 시작 2일차 아침, LAN 호스트, key-1): key 설정 move=True에서 줍기 중 S 이동 1.27m(줍기 전과 동일), InputGate 막힘·이동 잠금 없음, 열쇠 획득 정상. 정면 W 0m는 칸막이 문 충돌(줍기 없이도 0m). 사용자가 본 막힘은 Play 중 수정이라 재컴파일 전 코드였을 가능성. 회귀 테스트 KeyPickupThroughShopReachAllowsWalkingWhenKeyPolicyAllows 추가.
