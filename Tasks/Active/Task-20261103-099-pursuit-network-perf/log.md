# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

- 2026-11-03 기준 측정(baseline-20261103.json, 에디터·오프라인, 씬을 이미 여러 번 연 따뜻한 상태):
  - 로드 3.5초, 최장 프레임 1,041ms(GC.Collect 286ms, LoadLevelAsync Integrate 791ms 포함).
  - 씬: GameObject 30,952 / MeshRenderer 23,299(정적 배칭 180) / LODGroup 12,579 / Collider 3,764 / 실시간 조명 268(그림자 11).
  - 렌더: Lobby batches 6,110·그림자 캐스터 4,608·삼각형 230만(기준 초과), 숙소 2,527, 연습실 151.
  - 스냅샷: 추격자 26체 JSON 17,288B(≈169KB/s/클라이언트) → 바이너리 419B(≈4KB/s), 41배 감소.
- 2026-11-03 중요: PlayMode 테스트 중 추격 대기실 로드에서 `[Session] 메인 스레드 정지 12503ms`(차가운 로드) 경고. Relay 10초 기준 초과 → 튕김 원인 재현. 측정 도구의 1.0초와 차이 = 첫 로드(셰이더·에셋 캐시) 영향으로 추정.
- 2026-11-03 PlayMode 실패 3건 처리:
  - PursuitCheats_ReceivedCancellation: 스냅샷 타입 public 변경으로 리플렉션(NonPublic) 실패 → 테스트에 Public 포함(이번 변경 영향).
  - PursuitBehaviour_CaptureFourDirections: 단독 재실행 통과(순서 의존 불안정).
  - PursuitBehaviour_GraphHearing: dev 설정 에셋 1단계 순찰 이탈 30m로 100m 이동 시 Search 대신 복귀(dev에서도 실패하는 기존 문제) → 테스트 픽스처에서 이탈 제한 0.
- 2026-11-03 테스트: Pursuit EditMode 42/42 PASS. PlayMode 전체 재실행 12/14 — 실패 2건은 순서·환경 의존(CaptureFourDirections: 카메라 테스트 직후 런처 실패, 단독 PASS / GraphHearing: Vivox `Target Object Does Not Exist` 오류 로그, 단독 PASS). 이번 변경 영향 아님으로 판단.
- 다음: 다인 실기, 차가운 로드 멈춤 분석(후속 Task).

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-11-03 현재 브랜치 통합
- feat/waiting-room/pursuit-system에서 90a3f05c 병합. 새 피드백/치트/배치 단계와 전등·경량 스냅샷을 함께 유지.
- v3 바이너리에 poseEpoch/보호 치트/포획 누적 추가. 상태 신뢰성(4Hz+변경), 위치 비신뢰성(20Hz) 경로 분리 유지. 두 경로 준비 알림 적용.
- 코덱 왕복은 추가 상태값까지 검증. 컴파일 PASS, Pursuit EditMode46/46 PASS. 초기 check-work는 에디터 재시작 중 ENV_BLOCKED였으며 준비 완료 후 컴파일 통과.
- PlayMode 전체16개 중14개 PASS. 실패2개: 카메라 테스트 직후 런처 시작 실패(Activation), Vivox Target Object Does Not Exist(CaptureFourDirections). 두 항목을 개별 재검증한다. 다중 이름 세미콜론 필터는 NO_PROJECT_TESTS여서 결과로 세지 않음.
- 실패2개 각각 단독1/1 PASS. 전체14/16 + 개별2/2로 기록(전체 통과 아님). 이어서 사용자 요청대로 통합 커밋/푸시 후 P1을 진행한다.
- 통합 e72ac5e0 푸시 완료. save-work의 add -A가 Play 검증에서 생성된 동적 글꼴 캐시3개를 포함해 별도 정정 커밋으로 원래 git blob을 index에 복원했다. 생성 파일은 worktree에 그대로 보존하며 이후 명시 경로만 stage한다. 기존 데이터/이력 폐기·force 없음.

## P1 구현·측정 (2026-11-03)
- baseline-local-before.json: 같은 PC·에디터 반복 로드1790ms, 최장1273ms(GC451ms, Integrate1117ms 포함),26체 상태529B. 이전 Probe의5.17KB/s는10Hz 상태만 계산한 값으로 통합 후 실제 위치20Hz 경로를 누락했다.
- Awake는 생성 요청만 수집하고, Update에서 배치 순서를 유지해1체/프레임 생성+그래프 초기화. 마지막 Start가 끝난 다음 프레임 IsInitialized 완료. 완료 전 Bind/Ready/판정/스냅샷 적용 차단, 로컬 커튼 Reveal도 완료를 기다림.
- 생성 작업 합계/최장 단일 생성/분산 프레임/준비시간을 Probe에 추가. 첫 씬 로드 전용 메뉴는 같은 에디터 세션에서 이미 추격 씬을 연 경우 거부. OS/GPU 캐시 전체 초기화라는 뜻은 아님.
- 상태4Hz+위치20Hz payload 합산(상태 변경 추가 전송·NGO/UTP 헤더 제외). 조명은 동일한 결과값 쓰기를 생략하며 외부 WaitingRoomFlickerLight 변경은 실제 Light 값을 검사해 기존대로 덮는다.
- 새 초기화 단독 Play1/1 PASS, 추가 수신 방어/조기 snapshot 검사 포함 최종 코드 컴파일 PASS·Pursuit Edit46/46 PASS. 전체 Play/전후 측정 진행.
- 중간 실측(1체/프레임, after-warm): 로드1621ms, 준비3069ms, 최장1076ms, 초기화1952ms/실제작업21.8ms/단일최대1.47ms. 단일 전후 측정이며 GC 변동이 있어 인과/개선율 확정 불가. 대기 비용을 줄이기 위해 최종 구현은2ms 예산+최대4체/프레임으로 조정(단일 Instantiate/Init 자체는 선점 불가). 전체Play14/17의 메뉴 실패도 있어 새 fixture의 live singleton/handler 간섭을 방지하고 dev 통합 뒤 재검증한다.
- 최종2ms/최대4체 방식 컴파일 PASS·초기화 단독Play1/1 PASS. 중간 제출 전 check-work: 안전성/문서·규칙/컴파일 PASS, 테스트는 별도 기록. Task중복 매핑을 해소했고 로컬 동적 글꼴3개는 커밋 대상에서 제외한다.
- PR 직전 Unity 정상 종료·origin fetch. 최신 dev bf0b88a5 수신 및 병합(사람 대포/탈출로/설정 최신 변경 포함), 충돌0. 로컬 글꼴 캐시는 에디터 종료 시 원래 상태로 돌아옴. 통합 후 컴파일/추격 회귀 및 최종 예산 방식 측정 후 제출한다.
- dev bf0b88a5 통합 후 컴파일 PASS·Edit46/46 PASS. 최종 전체Play17개 중15 PASS/2 FAIL(초기화 신규·메뉴/재로드 포함15개 통과). 실패는 통합 전과 동일한 Activation 런처 순서 의존·CaptureFourDirections Vivox1001 로그. 전체결과 final-dev-play-tests.json 보존, 개별 재확인 및 최종 예산 방식 측정 진행.
- 최종 전체Play15/17 + 실패2개 각각 단독1/1 PASS(전체PASS로 표기하지 않음). final-dev-*-tests.json 보존. 최종2ms/최대4체 측정: 26체7프레임, 생성작업13.65ms/단일최대1.03ms, 로드1806ms/준비2781ms/최장1250ms. 기준1273ms와 유사해 전체 로드 개선으로 확정하지 않음. 남은 주요 비용 GC441ms/Integrate1112ms, 로비6763 batches/그림자4699.
- 최종 check-work 첫 시도는 최신dev에서 들어온 Task-081 log227줄 HARD로 중단(컴파일은 선행 규칙 때문에 SKIPPED). compact-docs로 완료된 과거 접두부 원문을 History/002-log-before-dev-integration.md에 내용 일치 확인 후 이동, 현재 로그66줄. 게임 코드/결정은 수정하지 않았다.
- 최종 검사 재실행: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 자동 테스트는 별도 결과(최신dev Edit46/46, 전체Play15/17·개별2/2). 이번 묶음을 중간 PR로 제출하며 남은 로드/렌더/Relay 실기를 완료로 표시하지 않는다.
- 중간 PR 제출: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/276 (feat/waiting-room/pursuit-system → dev, 최신dev bf0b88a5 포함). 미병합. 다음 최적화는 별도 브랜치에서 진행한다.
