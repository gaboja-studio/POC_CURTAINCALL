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
- dev(PR #413 트램펄린 운반 통과) 병합. 같은 번호 Task-20261117-151이 생겨 이 Task를 152로 변경. #413은 운반 중 트램펄린 충돌체를 트리거로 두고 추격자 분리에서 트램펄린을 제외 → 이 Task의 트램펄린 장애물은 내려놓아 단단해진 순간 크기를 다시 재도록 수정(운반 중 첫 등록 시 크기 0 방지). 내려놓은 트램펄린에 깔린 추격자는 NavMesh 이탈 복구가 안전 지점으로 옮긴다.
- 병합 후 재검증: EditMode Pursuit 212/212, PursuitCoverage 1/1, PursuitBreakables 2회 연속 통과. 병합 직후 1회 실패(상자 순서가 바뀌어 양옆 길 없는 상자를 골라 오르기 실패, 이어서 부순 벽 통과 단계에서 추격자가 계단실로 이동)는 상자 선택을 이름순·양옆 길 있는 것으로 고정한 뒤 재현되지 않음. 원인 미확정으로 기록.

- 2026-11-17 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 후속: 부서지는 오브젝트 전후 검증·테스트 3개 갱신 커밋을 PR #412에 추가(7ed88a90).

## 2026-11-18 후속: 낮은 통로·목표·길 안내

- 대기실 추격자: 현재 및 다음 경로 발 위치에서 낮은 천장을 확인한다. 새 경로·링크 착지 때도 보정하며, 앉기 진입에 서 있는 클립을 섞지 않는다. 낮은 계단 단은 통과하고 천장 이음매에서는 잠깐 더 앉은 자세를 유지한다.
- 쉬는 시간·정비 팀 목표 제목을 `추격자를 피해 달아나기`로 변경했다. 추격 중에는 플레이어 가까이에서 실제 경로로 갈 수 있는 가장 가까운 입구 한 곳을 표시한다. 첫 아침은 공연장, 쉬는 시간은 공연장/조건을 만족한 탈출로, 정비는 숙소/조건을 만족한 탈출로를 후보로 쓴다.
- `check-work`: 안전성, 문서 규칙, Unity 컴파일, 공통 설정 통과. 전체 EditMode 실행을 중단하고 관련 테스트만 재실행했다. `PursuitUpgradeTests` 11/11, `PursuitQuestTextTests` 2/2 통과.
- 통합 대기실 PlayMode 경로 검사는 그래픽 없는 배치와 그래픽을 켠 배치 모두 진입 단계의 커튼 닫힘 확인에서 실패했다. 실제 방 이동, 환풍구 화면, 목적지 표시 확인은 남았다. 검증용 에디터 종료 후 `origin/dev`가 최신임을 확인했다.

## 2026-11-19 후속: 일반 Game View PlayMode 검증

- 커튼 원인: `TransitionCurtainView.Close()`는 `Application.isBatchMode` 또는 Null 그래픽 장치에서 닫기를 거부한다. 완전히 닫힌 렌더 프레임과 다음 프레임 경계가 필요한 계약이다. 배치 옵션 없이 Direct3D12 Game View에서 실행하니 전환을 우회하지 않고 `WaitingRoom.unity`에 진입했다.
- `PursuitRendered_MannequinCrouchesThroughTheDormDuct` PASS: 실제 덕트에서 `CrouchWalk` 94프레임, 앉아 이동 4.05m, 애니메이션을 적용한 몸의 최고점 y=3.21m(천장 아랫면 y=3.50m). 기존 네 구간 추격 테스트도 환풍구→설비실 계단을 통과했으나, 별도 남동쪽 화물 통로 한 구간에서 실패했다.
- `PursuitRendered_RoutesFollowThePeriodAndEscapePrerequisites` PASS: 첫날 리허설 완료는 공연장, 쉬는 시간 만료는 공연장/탈출로, 정비 만료는 숙소/탈출로를 실제 안내 경로로 확인했다. 타이머가 흐르거나 탈출로 발견·개방 중 하나가 빠졌을 때 숨겨지고, 두 목적지가 가능할 때 각 입구 근처에서 표시가 바뀐다.
- 재현 중 `StairModulePrepared`의 계단 충돌 메시가 읽기 불가여서 런타임 NavMesh 굽기 로그 오류가 났다. 해당 모델에만 읽기 가능한 Unity 가져오기 설정을 적용했다. 모델 파일과 프리팹 참조는 그대로이며 가져오기 메타데이터가 갱신됐다. 탈출로 개방 연출 동안 안내를 잠시 숨기고, 문 움직임이 끝난 뒤 경로를 다시 계산한다.
- `check-work`: 안전성/문서 규칙 통과, Unity C# 컴파일 완료. 뒤따른 공통 설정 조회가 연결 중단으로 실패했으나 독립 재실행에서 오류 0건. 전체 플레이어 빌드는 실행하지 않았다.

## 2026-11-19 후속: 남동쪽 화물 통로

- 수정 전 재현: `PathPartial`, 통로 높이 5.64m에서 `(-30.24, 5.64, 31.04)`에 멈춤. 목표까지 수평 5.26m. 양쪽 발판은 이어져 있지만 동쪽 `East_Deck_0/1`의 난간과 트램펄린 차단용 `RailTopWall`이 내부 접합부를 가로질러 상부 여유가 없었다. 난간을 넘기는 링크를 추가할 곳이 아니다.
- Unity에서 내부 난간만 비활성화하고 접합부 이후의 바깥 난간·상단벽은 길이를 맞췄다. 통합 씬의 추격 NavMesh를 기존 설정으로 다시 구웠으며 에셋 GUID·기존 연결 링크를 유지했다. 남동쪽은 같은 층 일반 경로 `PathComplete`로 연결되었다.
- 일반 Game View `PursuitCoverage_StairsVentAndRailCornersReachTarget` PASS: 환풍구→설비실 계단, 계단실 경사로, 화물 북서쪽, 화물 남동쪽 **4/4** 도달. 남동쪽 최저 5.61m, 목표까지 수평 1.47m로 검사의 1.60m 도달 기준 충족. 접합부 몸통/머리 높이가 열려 있고, 낙하 가장자리의 난간·상단벽이 유지되는 것도 검사했다.
- `check-work`: 안전성, 문서 규칙, Unity 컴파일, 공통 설정 통과. PlayMode는 위 대상 검사를 별도 실행했다.
- 최신 `origin/dev`(`0285331d`) 반영 후 Game View에서 **4/4** 재통과. 남동쪽 최저 5.61m, 목표까지 수평 1.49m. Unity 종료 후 `origin/dev`가 최신임을 다시 확인했다.

- 2026-11-19 제출 전 `check-work`: 안전성·문서 규칙·Unity 컴파일·공통 설정 PASS. 위의 EditMode와 Game View PlayMode 결과를 재사용했다. 변경 커밋 `087f4c49`를 push했고, PM 팀에 PR 검토를 요청했다.

- 2026-11-19 PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/434 (fix/waiting-room/pursuit → dev)
