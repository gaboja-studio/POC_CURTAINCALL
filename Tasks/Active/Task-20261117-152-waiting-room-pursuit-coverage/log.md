# Log

## 2026-11-17 원인
- 계단 앞 정지(환풍구→지하 계단·계단실): 앉기 몸통 검사가 발목부터 시작해 Ramp_*/FlightRamp·Pallet를 막힘으로 판정 → 속도 0, TickStuck 미작동으로 영구 정지.
- 계단실 FlightRamp: 런타임 NavMeshRepair가 경사로 자체를 물체 속으로 보고 깎음.
- 화물 통로(무대 장치실): 꺾임 1.1m 난간(CargoNorthB_Deck_7, CargoNorthA_Deck_0, East_Deck_0/1·South_Deck_11)이 통로를 섬으로 분리. 남쪽 통로는 바닥에서 접근 불가. 동·서·북의 5m 구멍은 의도된 끊김.
- 트램펄린: 운반 트램펄린은 굽기 밖 → 경로가 몸통을 통과, 앉기 막힘 정지. 깎인 NavMesh 위 켜진 agent는 복구 경로가 없었다.
- 배치 창: N4_MachineEntry(환풍구 출구 계단)는 기준 층 -5에만 표시되어 0층(환풍구)에서 방으로 잡히지 않음.

## 전수 조사(편집 상태)
- 방 범위 밖 NavMesh: 도달 불가 지붕뿐(로비 돔·서비스 갤러리 지붕 등). 도달 가능한 것은 무대 복도 옆 작은 홈(약 5m²) 1곳.
- 걸을 수 있는 바닥 중 NavMesh 없음: 큰 누락 없음(소품 윗면·벽 가장자리 띠만, 소품은 직접 오르기로 처리).

## 검사
- PlayMode `PursuitCoverage_StairsVentAndRailCornersReachTarget` 1/1 통과(통합 대기실, 4경로 같은 층 도달, 두 모서리 넘기 사용·최저 5.64m 유지). 첫 실행 2회는 요청 중복 중단·NavMesh 없는 기본 변형 로드로 무효, 테스트 수정 후 통과.
- EditMode 'Pursuit' 212개 중 209 통과. 실패 3: PursuitClimbLinkTests 2(충돌체 없는 합성 NavMesh가 67f369e4의 벽 지지 검사에 막힘), UnifiedWaitingRoomSceneTests 1(방 목록 26개 고정, 현재 31개). 모두 이번 변경 전부터의 실패로 판단, 수정하지 않음.
- 문 개폐 재탐색: 코드 경로 확인(깎기 직후 + 0.25초 뒤 경로 버전 재증가, isPathStale, 끼임 분리). 별도 문 PlayMode 실험은 하지 않음.
- 트램펄린 깎기는 컴파일·코드 검토만, 실제 운반 플레이 검증은 사람 확인 필요.

- 2026-11-17 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 씬 저장: N4 ExtraFloors=[0]과 사용자 S06 구역 등록, 기존 방 ExtraFloors 직렬화만 변경. 사용자 GameSettings 조명 값 보존. 커밋/푸시 없음.

- 2026-11-17 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-17 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-11-17 제출
- 저장 커밋 59b55df2(코드·테스트·씬·사용자 GameSettings·기록). 폰트 캐시·재질 소수점 차이·_ThirdParty 변경은 제외하고 로컬 유지.
- origin/dev 9커밋 병합(스크립트·셰이더·문서만, 씬/프리팹/설정 겹침 없음). Unity 종료 명령이 실행 환경에서 막혀 에디터는 저장 완료·대기 상태로 열어 둔 채 병합.
- dev에 같은 번호 Task-20261117-148(camera-foreground-occlusion)이 있어 이 Task를 151로 변경.
- 병합 후 check-work 안전성·문서/규칙·컴파일·공통 설정 PASS, PursuitCoverage PlayMode 1/1 재통과.
- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/412 (fix/waiting-room/pursuit → dev). 병합하지 않고 검토 대기.

## 2026-11-17 부서지는 오브젝트 후속(사용자 요청)
- 기존 실패 테스트의 "상자"는 게임 오브젝트가 아니라 (-20000,0,20000)의 길찾기 전용 가상 상자(충돌체 없음)였다.
- 실제 부서지는 대상: BreakableWall 3, ExplorationGate(상자·문·덮개) 다수, ToolTarget(잠긴 가구). 막는 충돌체는 모두 PursuitDoorObstacle로 굽기 제외+실행 중 깎기. 기계실 녹슨 벽은 막는 충돌체 없음(통로 아님), ToolTarget 가구는 통로가 아님.
- 넘기 규칙이 부서지는 상자(소음 없음 장애물)까지 막던 것을 풀어, 1.3m 이하 상자는 좁은 길에서 넘는다(문·출입구·환풍구 덮개는 계속 금지). 넘기 탐색 거리 1.7m까지.
- 새 PlayMode `PursuitBreakables_PathsRecomputeBeforeAndAfterOpening` 1/1: 대상 31개 디버그 열기 전/후 비교. 높은 벽·문 12곳 전 PathPartial→후 직선 통과, 상자 자리 NavMesh 복구, 열린 뒤에도 남는 steelBox는 막힘 유지, 부수기 전 상자 위 대상에게 올라서기·부순 BreachWall 실제 통과 성공. 숙소 상자 2개의 72m 경로는 벽에 붙은 상자 뒤편 측정점이 옆방이라 정상.
- 테스트 갱신: PursuitClimbLinkTests 고정 상자에 충돌체 추가(벽 지지 검사 반영), UnifiedWaitingRoomSceneTests는 통합 대기실 기준(방 등록·배치 참조·모든 부서지는 막이의 장애물 등록)으로 교체.
- 결과: EditMode Pursuit 212/212, PursuitCoverage 1/1 재통과.

- 2026-11-17 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
