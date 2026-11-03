# 추격 대기실 최적화: 진행 상황과 남은 작업

- 작성: 2026-11-03 · 담당 @MoHoDu · Task-20261103-099
- 브랜치: `fix/pursuit/network-perf` (`feat/pursuit/light-effects` 위에서 분기 → 전등 연출 커밋 19872e4f 포함)
- 이어서 작업할 곳: 다른 PC의 `feat/waiting-room/pursuit-system`에 이 브랜치를 병합한 뒤 계속

## 1. 보고된 문제
1. 추격 대기실 렌더링이 무겁다.
2. 씬 로딩이 오래 걸린다.
3. 멀티플레이 중 일부 또는 전원이 타임아웃으로 튕긴다.

## 2. 원인 정리
| 원인 | 근거 | 상태 |
|---|---|---|
| 씬 활성화 중 메인 스레드 장시간 정지 → Relay 연결 끊김 | Relay는 메인 스레드가 약 10초 멈추면 할당을 끊는다(`NetworkSessionManager` 주석, 2026-11-02). PlayMode 테스트에서 처음 여는 로드에 `[Session] 메인 스레드 정지 12503ms scene=WaitingRoomPursuit` 재현 | **미해결 (최우선)** |
| 추격 스냅샷 과다 전송 | 추격자 26체를 JSON 문자열로 0.1초마다 신뢰성 분할 전송: 17,288B ≈ 클라이언트당 169KB/s. 로딩 중인 클라이언트에도 전송 | **해결** (아래 3) |
| 씬 규모 과다 | GameObject 30,952 / MeshRenderer 23,299(정적 배칭 180) / LODGroup 12,579 / Collider 3,764 / 씬 파일 79MB | 미해결 |
| 로비 렌더 부하 | batches 6,110, 그림자 캐스터 4,608, 삼각형 230만 | 미해결 |
| 실시간 조명 | 268개 모두 실시간, 그림자 조명 11개(점광원 그림자는 6면) | 미해결 |

참고: Unity Transport의 `DisconnectTimeoutMS`(30초)를 늘려도 Relay의 10초 기준은 바뀌지 않는다.

## 3. 완료한 것
### 3-1. 반복 가능한 측정 도구
- 메뉴: `Tools/Curtain Call/Performance/Measure Pursuit Waiting Room` (`Assets/Scripts/WaitingRoom/Pursuit/Editor/PursuitPerformanceProbe.cs`)
- 동작: 플레이 진입 → 추격 대기실 로드 → 최장 멈춤 프레임과 그 프레임의 자체 시간 상위 15개 → 씬 규모 → 3개 방(S02_Dormitory, Lobby, S09_PracticeRoom) 렌더 통계 → 스냅샷 크기 → 플레이 종료. 약 30초.
- 결과: `Logs/Performance/pursuit-waitingroom-<시각>.json`과 프로파일러 `.data`(Profiler 창에서 Load). `Logs/`는 git 제외이므로 기준 결과는 Task 폴더에 복사해 둔다.
- 기준값(WARN 표시): 최장 멈춤 5,000ms(에디터 기준, Relay 10초의 절반), 스냅샷 1,100B, 방별 batches 3,000, 그림자 캐스터 2,000.
- 한계: 에디터 측정이고 씬을 이미 연 따뜻한 상태라 처음 로드보다 낮게 나온다(1.0초 vs 테스트 중 12.5초).

### 3-2. 기준 측정 (`baseline-20261103.json`)
- 로드 3.5초, 최장 프레임 1,041ms. 그 프레임의 큰 항목: `Application.LoadLevelAsync Integrate`(합계 791ms), `GC.Collect` 286ms, `Mono.JIT` 94ms, `ClothScene.Update` 34ms.
- 렌더: Lobby 6,110 batches(기준 초과), 숙소 2,527, 연습실 151.

### 3-3. 추격 스냅샷 경량화
- `PursuitSnapshotCodec`: 바이너리(위치 cm 단위 short, 범위 밖이면 float / 압축 쿼터니언 / 플래그). 알림 문구는 받는 쪽 설정에서 조회. 메시지 이름 v2(이전 빌드와 혼용 불가).
- `PursuitDirector`: 클라이언트가 씬을 불러와 `v2.ready`를 보낸 뒤에만 전송. 1,100B 이하면 UnreliableSequenced(매번 전체 상태라 유실 자동 복구), 넘으면 ReliableFragmentedSequenced. 받을 클라이언트가 없으면 스냅샷 생성 생략.
- 결과: 17,288B → 419B(41배), 169KB/s → 4KB/s. 추격자가 늘면 1체당 약 15~20B 증가하며 자동으로 대응한다(다시 할 필요 없음).
- 테스트: 코덱 왕복·정밀도·크기·손상 데이터(`PursuitSnapshotCodecTests`). PlayMode 테스트 2개 수정(타입 public 변경 반영, 기존 순찰 이탈 30m 설정과 충돌하던 픽스처).

## 4. 남은 작업 (우선순위 순)
### P0. 다인 실기 검증
- [ ] 2~4인 Relay 접속으로 추격 동기화(위치·포획·단계·전등) 정상 여부
- [ ] 늦은 입장자가 준비 알림 후 스냅샷을 받는지
- [ ] 튕김 재현 여부와 튕긴 PC의 로그 `longestStallMs`, `[Session] 메인 스레드 정지` 값 수집(저사양 PC 포함)

### P1. 처음 여는 로드의 멈춤 줄이기 (튕김 핵심)
- [ ] 측정 도구에 "처음 여는 상태" 측정 추가(에디터 재시작 직후 또는 빌드 측정), 빌드에서 Profiler 연결 측정
- [ ] 셰이더 미리 컴파일: ShaderVariantCollection 작성 → 커튼 전환/로딩 화면에서 WarmUp (`GraphicsSettings.m_PreloadedShaders`가 현재 비어 있음)
- [ ] 활성화 프레임 분산: 추격자 26체 생성·행동 그래프 초기화를 여러 프레임으로, 로드 직후 GC.Collect 위치 확인
- [ ] 무거운 Awake 스크립트 찾기(프로파일러 `.data`에서 Integrate 하위 확인), Cloth 컴포넌트 필요 여부
- [ ] 목표: 저사양 PC 빌드에서 최장 멈춤 5초 미만

### P2. 씬 로딩 시간
- [ ] 정적 소품을 방 단위로 메시 합치기(GameObject·LODGroup·Collider 수 감소). 작업물이 추가될 때마다 다시 하지 않도록 빌드/저장 시 자동 처리 방식으로 만든다
- [ ] 작은 소품 LODGroup 정리, 보이지 않거나 중복된 콜라이더 제거, 편집 전용 오브젝트 `EditorOnly`
- [ ] 장기: 층·구역별 Additive 분할 로드로 한 번의 멈춤을 짧게
- [ ] 텍스처 압축·크기 점검, 비동기 업로드 버퍼(현재 16MB / 2ms) 조정

### P3. 렌더링
- [ ] 정적 배칭이 23,299개 중 180개뿐인 원인 확인(Static 플래그·Player Settings·런타임 생성 여부)
- [ ] GPU Resident Drawer 검토(Forward+ + SRP Batcher 전제, 현재 꺼짐)
- [ ] 오클루전 컬링 재굽기: 추격 씬은 원본 복제라 연결된 데이터가 원본 것일 수 있음
- [ ] 로비 그림자 캐스터 4,608 감소: 그림자 조명 수/거리/해상도, 작은 소품 그림자 끄기
- [ ] 조명: 정적 조명 라이트맵 굽기 또는 플레이어 근처 방 조명만 활성(`PursuitLighting`의 방-조명 매핑 재사용 가능)
- [ ] `PursuitLighting`: 가까운 방/값이 바뀐 조명만 갱신(현재 매 프레임 268개 설정, 비용은 작음)

### 씬 담당자와 협의 필요
P2의 메시 합치기·분할 로드와 P3의 조명 굽기·오클루전은 씬 YAML을 크게 바꾼다. 착수 전 담당자에게 알리고 겹치는 작업을 확인한다.

## 5. 다른 PC에서 이어서 하는 방법
1. Unity 에디터를 닫고 받는다.
   ```
   git fetch origin
   git switch feat/waiting-room/pursuit-system
   git pull
   git merge origin/fix/pursuit/network-perf
   ```
   - 2026-11-03 기준 원격 `feat/waiting-room/pursuit-system`(083c5747)은 이 브랜치에 이미 포함되어 있어, 그 PC에 푸시하지 않은 커밋이 없으면 fast-forward로 끝난다.
   - 이 병합에는 전등 연출(Task-20261103-098)도 함께 들어온다.
2. 에디터를 열고 컴파일 확인 → `Tools/Curtain Call/Performance/Measure Pursuit Waiting Room` 실행 → 결과를 이 폴더의 `baseline-20261103.json`과 비교(PC가 달라 절대값보다 추세 비교).
3. 회귀 확인: Test Runner에서 이름에 `Pursuit`가 들어간 EditMode·PlayMode 테스트.
   - PlayMode 알려진 불안정: `PursuitBehaviour_CaptureFourDirections…`(카메라 테스트 직후 실행 시 런처 실패), `PursuitBehaviour_GraphHearing…`(Vivox 오류 로그). 둘 다 단독 실행은 통과.
4. 작업 기록은 이 Task 폴더(`plan.md`/`todo.md`/`log.md`)에 이어서 남긴다.

## 6. 통합 후 현재 기준 (2026-11-03)
- `feat/waiting-room/pursuit-system`에서 f176e7af와90a3f05c를 기능 보존 병합. 위 3-3의419B/4KB/s는 이전 브랜치 단독 측정이다.
- 상태 v3 바이너리에 poseEpoch·치트·포획 누적·전등 상태를 포함한다. 상태4Hz+변경 신뢰성, 위치20Hz 비신뢰성 보간을 유지하고 양쪽 모두 수신 준비 알림을 적용한다. 이전 빌드 혼용 불가.
- 다음 구현 묶음: 이 PC 기준값 → 생성/그래프 초기화 프레임 분산·완료 전 수신/판정 차단 → 첫 씬 로드/반복 로드 구분과 실제 전송 주기 측정 → 전후 비교.
- 셰이더/GC/씬·배칭·그림자는 각 비용을 확인한 뒤 후속으로 다룬다. 저사양 PC 빌드5초 미만·실제 Relay 다인 검증은 별도 실측이 필요하다.

## 중간 제출 범위
- 초기화 프레임 분산(2ms 작업 예산·최대4체), 완료 전 상태/네트워크 차단 및 커튼 대기까지 제출한다. 단일 Instantiate/Behavior Init은 선점할 수 없다.
- 측정 도구의 첫 씬 로드/반복 구분, 초기화 시간/Profiler marker, 상태4Hz+위치20Hz payload 계산, 동일 조명 쓰기 생략 포함.
- 최신 dev bf0b88a5 통합(1f647037, 충돌0). private 에셋4c45f11은 origin/main 포함 확인.
- 이전 단일 측정의1273→1076ms는 GC 변동을 포함하므로 확정 개선율이 아니다. 최종 예산 방식 측정은 별도 JSON으로 보관한다.
- 아직 해결 완료로 보지 않는 것: 저사양 빌드 첫 로드/5초 목표, 실제2~4인 Relay, 로비 batches/그림자·정적 배칭, 셰이더/GC/씬 분할. PR 뒤 별도 후속 브랜치에서 진행한다.
- 최종 예산 방식 실측: baseline-local-final-budget.json.26체7프레임/실제 생성13.65ms/단일최대1.03ms, 최장1250ms(GC441ms/Integrate1112ms). 기준1273ms와 유사하며 전체 로드 개선 달성으로 표기하지 않는다. 준비2781ms; 로비6763 batches/그림자4699는 남아 있다.
- 중간 제출 PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/276 · 최신dev 포함/미병합. 이후 작업은 이 PR head에서 분기해 기존 PR 범위를 유지한다.
