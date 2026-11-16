# Log

## 2026-11-16 기존 구간·보행 교정 및 전체 검증

- 최신 dev `0f95ba68` 확인 뒤 기존 변경과 원본 씬을 보존했다. Blender Walk/Run 파생, 강제 숙임 등장 제거, 추가 첫 대포의 Route/Bypass 등록을 반영했다. 반환 직선교를 두 묘기 조합으로 바꾸고 컨베이어 A/B/C 독립 동선을 구성했다. 중간의 컨베이어 4대포·12이동봉 안과 유예시간 연장은 폐기했다. 상세는 [007](History/007-space-and-gait.md).
- 등장·보행63/63, 입구 대포19/19, 최신 추격자 조합142/142 통과(142는 이전120·입구19·기존 진행도10·새 후발 압박12를 포함하므로 합산하지 않음). 탄도/착지/registry와 guide122개·신규11연결의74항목 통과. B는 공통 함수의 6전환×2좌석 대표 캡슐·가시선, C/반환은 좌석·다리 손실·반동·제동480조건 계산으로 구분했다. 반환 섬 옆면과 C 조명 지주의 비행 침범을 수정한 후 각 상태의 유효45/60도 선택지가 확인됐다.
- 최종 정적 감사는 failures0/warnings272/colliders1297/ropes25다. 경고는 구조 접합·높은 윗면 표본 등도 포함하며 전체가 무해하다고 확정하지 않았다.85개 옛 static chunk를 제거하고 살아 있는429개 원본 renderer를 복원했다. 삭제된129개는 되살리지 않았다. 기존 창틀 절단은 Prototype 전용3mesh에 보존했고 공유182파일 해시 유지. 추가 공면7개는9조각으로 보존했고, 컨베이어 앞 불필요한 깊은 박스2개를 제거·후방 지지1개 보존 후 Prototype NavMesh만 재생성했다. 마지막 콘크리트 하부 유격을 보정했다. 최종235,242개 직렬화 참조에서 스크립트·참조·사용 재질·메시·본 누락0이다.
- 준비 위치를 쓴 구간 시험에서3인 첫 외줄 착지·추격 진입·포획 후 생존자 전환,4인 첫 대포 동시 착지,A 하차/B 두 좌석 전7봉/C 두 봉과 섬 착지를 확인했다.05에서는 한 명 포획 뒤3명이 합류 외줄·레버·기존 외줄을 넘어 새Return 발판에 도착했다. 최종 공간 빌드(errors0/warnings9)의 별도 `continuous-arms-4-01`은 **실제 입구에서 시작해3명 포획 뒤 마지막 생존자 성공 엔딩, 배선24/24**를 확인했다. 준비/부활/강제 절단/재시작0이며 지연 출발과 자동 조작 재개를 포함한다. 자연스러운4인 동시 완주나 모든 선택지의 연속 완주는 아니다.
- 실기 중 하차장4m 앞에서 손이 concrete fin에 막혀 공격 없이 후퇴·재출현하는 버그를 재현했다. 실제 준비 동작의 손·몸통 SphereCast를 모두 통과하는 공격점으로 이동하도록 수정했다. 실제 씬 약1m 옆 위치의 경로 clearance와 최신05 빌드의 공격 예고→포획→투척을 확인했다. 실패했던01/02/06의 관측·입력 타이밍 문제와05의 동일 점 다인 도착 판정 문제는 원본 기록을 남겨 구분했다.
- 연속4인 검사에서 입구 생존자를 선두 진행도 점수가 무한히 밀어내는 문제도 확인했다. 기존45초 유예+45초 압박 상승 동안 실전진/위협이 없는 사람 중 가장 오래 방치된 사람을 먼저 추격하도록 수정했다. 실제4m 전진 또는 공격 예고만 시간을 갱신하며 표적 선택이나 왕복 이동은 갱신하지 않는다. 순간이동·공통 이동·협동 규칙 변경은 없다. 해당 수정의 새 빌드 실기와3인 성공 엔딩은 후속 검사 중이다.

## 현재 범위와 남은 검증

- 최신 등록은 봉36/외줄25/대포10/실제 추격 출현15곳이다. 검사동3봉·열 회수4봉·상부3봉과 총20외줄은 이전 단계이며 [전체 구현·검증 범위](labyrinth-build-qa.md)에 보존했다. 검사동 두 자리 전체와 동일 시작/합류 기준의 난이도 비교는 미완료다. 최종 공간의4인 마지막 생존자 성공은 위007 기록을 따른다.
- 첫 대포 이후 봉의 위협 부재와 두 번째 대포 정면 출현은 미해결 인수 조건이다. [회귀 조건](remake-regression.md)을 유지하며 새 배치나 과거 정적 검사로 해결을 선언하지 않는다.
- 봉 재설계 이전 실기에서 당시 기존 경로와 새 외줄 6개·대포 2개 통과를 관측했다. DEV 동기화 전 세 실행은 18/24·8/24·4/24, DEV·arena 수정 뒤 두 실행은 19/24·20/24에서 전원 탈락했다. 출발지 수정 뒤 `labyrinth-entrance-3-01`도 이전 쉬운 검사동 분기로 22/24 후 전원 탈락했다. 여섯 실행을 성공 완주나 최신 10봉 검증으로 표시하지 않는다. 상세는 [플레이 기록](labyrinth-play-qa.md)에 있다.
- 새 압박 빌드(errors0/warnings25) `pressure-fresh-3-01`에서도 두 명 포획 뒤 마지막 생존자가 실제 입구부터 배선24/24·성공 엔딩에 도달했다(최종injury4). 체크포인트/부활/강제 절단/재시작0, 작업장pilot의 과보수 판정 뒤 같은 톱을 든 일반Walk와 조작 재개를 포함한다. 기존4인 성공과 빌드 범위를 구분한 상세는007에 있다. 새4인 후발 압박 실기, 추출 대포 앞 서 있는 지점의 공격 사각 수정, 최종check-work/DEV 동기화는 진행 중이다.
- 이전 로그와 모든 디버깅 수치·실행 근거는 [History/001-followup-log.md](History/001-followup-log.md)에 원문 그대로 보존했다.

## 기존 수정본의 최근 검증 이력

- 복사 씬과 생성 코드에 화염 복도 끝 대포대 3m 연장, 상부 외줄 측면 착지 깊이 4→7.8m, 권상교 대기턱 0.85m 연장을 반영했다. Unity에서 접합 높이, 봉 잡기 거리, 봉 아래 틈 보존을 확인했다.
- Windows 개발 빌드에서 3·4인 LAN으로 입구부터 체크포인트 없이 다시 실행했다. 팀원들이 추격자에게 잡힌 뒤 선두가 협동 잠금 5곳과 보정한 봉 구간을 통과했다. 3인은 절단기를 집은 뒤, 4인은 배선 8/24 및 추격자 부위 1/8 절단 뒤 포획됐다. 상세는 `continuous-pursuit-qa.md`에 기록했다. 무체크포인트 성공 엔딩은 미확인이다.

- 2026-11-15 3·4인 재개. Unity PID26788 종료 후 origin/dev c8bc4c2e를 작업 브랜치에 fast-forward하고 비공개 서브모듈을 e6c3a7e3으로 반영했다. 기존 작업을 보존했다.
- 스탠드얼론 클립 샘플 오류와 후반 등장 발판 선택을 수정했다. 최종 판정 전 후방 생존자 추격, 시간·전진 기반 압박과 최대 압박 관측, 유인자·선두 도착·후방 이탈 조합을 기록했다.
- 마지막 생존자가 상승 대포 및 양방향 합류교에 갇힐 수 있어 기존 봉을 직접 잡으면 동력이 고정되도록 수정했다. 탈출로 EditMode 446/446 통과.
- 3·4인 체크포인트 성공 엔딩, 3인 마지막 주자의 상승 대포 단독 발사·상부 외줄 착지, 입구에서 4인 컨베이어 하차장 바닥 도달을 확인했다. 입구부터의 연속 성공 완주는 미완료다. 실행과 증거는 [campaign-play-qa.md](campaign-play-qa.md)에 기록했다.
- 2026-11-15 check-work 안전성·문서/규칙·컴파일·공통 회귀 712/712 통과. Unity PID40396 종료 확인 후 origin/dev fetch·ff-only를 마쳐 HEAD와 origin/dev 일치(c8bc4c2e), 서브모듈 e6c3a7e3. 플레이어 빌드가 자동으로 추가한 ProjectSettings AppUI 목록 한 줄을 원상 복원했다.
- 마지막 생존자의 컨베이어 전원·반출교 레버를 각각 기존 하차장과 외줄 높이에 추가했다. 실제 씬의 시야·지지와 마지막 주자 활성 조건을 검사했다. 최종 탈출로 EditMode 449/449, check-work 공통 712/712 통과. 최신 레버를 포함한 연속 완주는 미완료로 남긴다. Unity PID34200을 종료한 뒤 origin/dev를 재확인했고 HEAD=c8bc4c2e와 서브모듈 e6c3a7e3에 추가 변경은 없었다.
- 2026-11-15 후속. 승객이 중심선에서 0.70m 비껴 외줄에 진입하면 떨어지는 것을 실제 원격 입력으로 재현했다. 복사 씬 외줄 진입 가이드, 10곳의 역진입 충돌 경계와 공개 발판 작업등, 같은 호스트 시계의 벽 접촉·후속 동작을 적용했다. 3인 걷기/점프 횡단과 목마 버튼, 4인 하차/전원/추격 포획 및 호스트·원격 렌더를 확인했다. [conveyor-junction-qa.md](conveyor-junction-qa.md)에 범위와 증거를 정리했다. 자동 검사 452개 모두 통과. 첫 check-work에서 공통 712개가 통과한 뒤 QA 기록 동시 수정으로 fingerprint가 달라져 다시 검사했다.
- 재검사 check-work는 안전성·문서/규칙·컴파일·공통 진행 회귀 712/712 통과. 컨베이어의 대포·이동 봉·하차장·외줄 입구·복귀 대포 관측점 5곳에서 숨은 경로 7~19번 앵커 노출 0을 확인했다. 새 에셋은 복사 씬에 저장했다.

- 2026-11-15 컨베이어 재피드백: GDC·The Level Design Book·Nintendo·Valve 개발 자료를 검토해 복사 씬의 중복 단차·버튼 배치·미사용 구조를 걷어내고, 하차장 전원 레버 하나와 추락 경계 난간으로 정리했다. 대포 대기는 개인별 16초/첫 좌석 13초, 일반 및 최대 압박 이동 속도를 낮췄다. [conveyor-flow-review.md](conveyor-flow-review.md)에 3인 LAN 대기→발사→이동 봉→하차 및 만료 후 포획, 4인 원격 레버→외줄 횡단, 최종 난간의 비침범 편집 검사 범위를 기록했다.
- 탈출로 EditMode 453/453, check-work 공통 진행 712/712 통과. 최종 저장된 하차장 바닥 770점·실제 입력 경로의 난간 비침범 93점·다섯 계단 구간 134개 지지면을 확인했다. 최신화는 작업 시작/재개와 제출 직전 Unity 종료 후 fetch/ff-only로 수행했으며 HEAD=origin/dev c8bc4c2e, 서브모듈 e6c3a7e3. 커밋·push 없음.

- 2026-11-15 추격자 조합 검증: 실제 3·4인 LAN에서 부상 외줄, 목마와 봉의 동시 사용, 트램펄린 운반자 포획 뒤 반동과 인계, 대포 대기열, 앉은 부상자 추격의 다섯 시나리오를 검사했다. [scenario-qa.md](scenario-qa.md)에 추격자의 최초 공격·피해자·점유 해제·생존자 전환과 실패 원인을 기록하고 `scenario-play.ps1`로 반복할 수 있게 했다.
- 입구 문틀에서 몸통은 대상을 보지만 실제 손이 막히고 정지 거리 안에서 이동이 끝나는 문제를 재현했다. 정지 거리만 줄인 검증도 실패했다. 실제 준비 동작의 손이 닿고 연결된 이동면으로 갈 수 있는 옆 위치를 찾도록 수정했다. 같은 장소의 3인 재현과 4인 목마·봉 추격을 수정판에서 다시 통과했다.
- 최종 check-work 안전·문서·컴파일·공통 진행 712/712 통과, 탈출로 EditMode 453/453 통과. 시나리오 실기는 시작 위치·부상을 준비하고 조작은 실제 소유자 입력으로 진행했다. 전체 구간 연속 완주는 이번 결과와 구분한다.
- 제출 직전 Unity 종료 후 origin/dev fetch와 ff-only 확인: HEAD=origin/dev c8bc4c2e. 커밋·push 없이 복사 씬을 다시 연다.

- 2026-11-15 첫 등장 연출을 벽 타격과 팔 회수, 숙인 걸음, 기립으로 연결했다. 등장 뒤 1.8초에 0.95초 포효가 시작되며 이후 달려서 추격한다. 3·4인 체크포인트 없는 LAN에서 추격 사망 15건과 생존자 공격 전환을 측정했다.
- 실제 진행 중 재현한 화염 복도 출구 봉의 짧은 공중 잡기 구간을 복사 씬에서 1.1m 접근 발판으로 수정했다. 3인 연속 실행의 지지와 실제 F 잡기를 확인했고, 4인 선두는 출구 봉 앞에서 포획됐다. 두 인원 구성의 입구→성공 엔딩은 미완료다. 시간, 입력, 실행별 범위는 [입구부터 추격 검증](continuous-pursuit-qa.md)에 정리했다.
- check-work의 안전성·문서 점검과 Unity 컴파일은 통과했다. 공용 설정 스크립트는 에디터 응답 시간 제한에 걸렸다. 별도로 다시 실행한 탈출로 EditMode 453/453과 공통 진행 EditMode 712/712가 모두 통과했다.
- 제출 직전 Unity 종료 후 origin/dev를 fetch하고 ff-only로 확인했다. 작업 브랜치와 origin/dev가 모두 `c8bc4c2e`다. 커밋·push 없이 작업 상태를 유지했다.

## 미궁 구현 착수

- 2026-11-15 리메이크 구현 착수: Unity 종료 후 origin/dev fetch/ff-only, c8bc4c2e 동일. 기존 변경 ZIP·SHA256·binary patch를 `.harness/local/pursuer-remake-planning/baseline-before-build/`에 보존했다. 기존 수정 씬에서 별도 LabyrinthPrototype을 만들고 공통 기물 기반 3개 선택 동선, 플레이어 분기 가이드, 검사동 파괴 출구, 공장 서사 5곳을 추가했다.
- 새 길의 스케일·벽 잔여물·중복·착지 면 겹침·기존 화염 분사함 간섭을 배치 중 발견해 수정했다. 1인 일반 입력으로 첫 대포와 검사동 외줄 연결을 확인했고, 새 반입 설비가 첫 등장 통로를 막는 것도 실제 실행에서 발견해 측면으로 옮겼다. 상세 범위와 미완 검증은 [리메이크 구현·검증 범위](labyrinth-build-qa.md)에 분리했다.

## 2026-11-15 초기 전체 공간 검사 — 출발지·10봉 재설계 이전

- 원본 씬 해시를 유지한 채 최종 작업장과 단일 출구의 공개 공간까지 생성했다. MeshCollider 외벽 절개, 독립 바닥 메시의 깨진 접합부, 초기 등장/숨은 왕복 이동/낙하 공간 간섭을 수정했다. 마지막 작업장 arena 폭 14→34m로 새 양측 바닥의 대상 누락을 수정하고 10점 경계 검사를 추가했다.
- 이 단계의 공간 감사 failures 0, missing scripts 0, isolated props 0. 당시 충돌체 781/등록 외줄 21/서사 5, 바닥 후보 1,240표본. 벽·구조물 윗면 교차 경고 222개는 남으며 새 공개 발판/작업장 통로의 해당 경고 0. 후속 출발지/봉 코스 감사 수치나 실제 완주로 재사용하지 않는다.
- DEV 동기화 전 탈출로 EditMode 463/463 및 check-work 공통 712/712, 컴파일·문서 PASS. 첫 check-work의 Task 길이 초과는 기존 기록을 원문 그대로 History로 보존한 뒤 재검사했다.
- Unity 저장·종료 확인 후 최신 origin/dev 10633c71을 fast-forward했다. 기존 동적 폰트 캐시만 병합을 막아 단일 파일 stash와 별도 복사본으로 보존한 뒤 원래 bytes를 복구했다(기준 해시 일치). 다른 기존 변경은 유지했고 브랜치 커밋/push는 하지 않았다. 서브모듈 e6c3a7e3 유지.
- 최신 DEV와 arena 수정을 포함한 전체 Development 빌드 Succeeded(167.65초). 보고서의 error 1은 빌드 중 Pipeline 요청 60초 시간 제한이며 실제 컴파일 오류가 아니다. 최신 빌드 다인 실행과 병합 뒤 최종 검사는 후속 결과에 기록한다.

## 2026-11-15 출발지 baseline과 최신 봉 재설계의 분리

- `labyrinth-entrance-build-result.json`: 전체 빌드 Succeeded, errors 0/warnings 32, 207.884초. `labyrinth-entrance-3-01`의 세 타임라인·입력 기록을 명시적으로 요약했다. 새 출발지→첫 대포→이전 검사동 외줄/화염 직행 대포→기존 상부 경로→최종 작업장에서 22/24 후 전원 탈락했다. continuous true, 재시작/준비/피해 명령 0건, review error 0건, 도착자 0명이다. 최신 10봉은 실행 당시 존재하지 않았다(모든 호스트 표본의 봉 15개).
- 실패 뒤 slot 1의 capture 명령 4가 성공 기록됐고, 223.425초 첫 해당 표본은 Ending/EscapeAllRetired와 error 공란이었다. `client-1-4.png`를 열어 실패 연출 장면을 확인했다. 활성 출력 Base 카메라 대체 선택으로 이전 리뷰 캡처 예외가 이번에는 발생하지 않았다. 게임플레이/증거 입력 규칙이나 성공 판정은 변경하지 않았다.
- 이후 선택 경로의 단순함을 보완하기 위해 공통 봉 10개·총 외줄 20개로 제작 중이다. 검사동은 컨베이어 관측장으로 재합류, 열 회수는 3봉→확장 착지섬→4m 외줄→4번째 봉, 상부는 25m 외줄→3봉→24m 외줄로 구성한다. 하단·상단의 탄도 차이 때문에 열 회수 착지섬을 X145..149/Z60.5..69로 확장했다. 공통 수치와 낙하 피해는 유지한다.
- 열 구역 전체 바닥 절개·봉 지지보 조명을 반영한 바닥 유도선 삭제 전 생성본의 `audit.json`은 failures 0/warnings 271/colliders 952/ropes 20이다. 사용자 요청으로 입구 경로 6선·대포 승차선·모든 발판 가장자리 유도선 생성 코드를 삭제했다. 이 시점의 재생성·확인 대기는 아래 후속 기록에서 완료 범위를 구분한다.
- `labyrinth-parity-solo-01`은 공란인 campaign에 Escape를 기다리는 하네스 전제 때문에 입력 없이 포획됐다. `solo-02`는 첫 대포 착지 28.379초, 새 단외줄 뒤 첫 새 봉 전 발판 46.929초까지 일반 입력으로 진행했지만 목표 `(-278.9,32.8)`가 몸 중심 잡기 반경 3m 밖이어서 봉에 탑승하지 못한 채 47.698초 포획됐다. 교정 목표 `(-279.4,32.3)`의 `solo-03`은 49.129초 봉 15 점유를 확인했으나 50.961초 다음 봉 전환 중 포획됐고 봉 16 점유는 미관측이다. 첫 잡기를 코스 통과로 확대하지 않는다.
- 기존 Factory static 22/23/24 계열의 0.01m safety-ochre 진행 도색도 추가로 식별했다. 당시 복사 씬 평면 도색 제거를 계획했고 아래 후속 기록에서 완료했다. 난간·봉 도색은 유지하며 기존 캡처는 제거 뒤 화면의 증거가 아니다.
- 이 시점에는 새 봉 간 연속 전환이 미검증이었다. 아래 후속 구간 시험과 구분하며, 동일 시작/합류점의 시간·대기·추격 압박 비교, 3·4인 성공 엔딩·최종 check-work는 미완료로 유지한다. 기존 어그로 평균 N=2에 새 baseline이나 부분 시도를 합산하지 않는다.

- 바닥 유도선 제거 후 복사 씬을 재생성했다. 입구선·승차선·발판 가장자리·작업장 표식과 기존 평면 도색을 제거했고 FloorMarks 조회는 removedNames/horizontalPaint 모두 빈 배열, 화면 확인 완료다. 최종 생성 감사 failures 0/warnings 271/colliders 952/ropes 20. 봉 지지보의 주광을 넓히고 같은 고정 렌즈의 약한 전방 보조광으로 벽·다음 착지면 판독을 보정했다. 전역 밝기와 공통 움직임은 유지하며 전체 최종 조명 완료는 아니다.

## 2026-11-15 새 봉 구간 실기와 입구 로딩 가드

- `labyrinth-parity-build-result.json`: Succeeded, 292.069초, warnings 32/errors 1. errors 1은 `/api/exec` main-thread 30초 timeout이며 실제 빌드 결과와 함께 보존한다. 아래 구간 시험은 가드 추가 전 이 Development 빌드를 사용했다.
- 3인 세션에서 setup 1건으로 시작한 단독 구간: inspection-fixture02는 15→16→17→릴레이 뒤 정밀 걷기가 대포 몸체에 막혔으나 추가 F로 45.059초 승차, 49.789초 컨베이어 도착/injury 0. heat-fixture01은 18→19→20→섬→4m 외줄→21→29.204초 합류/injury 0. transfer-fixture01은 22→23→24를 전환했지만 16.436초 X=-418.8757은 섬 X[-417,-411] 밖의 동쪽 외줄 위다. Y만 본 하네스 착지 라벨을 폐기하며, 제동 없이 넘어간 뒤 균형 입력 없이 추락한 실패로 기록한다. continuous=false로 timeline은 없고 실시간 도구 관측·actions·마지막 state가 근거다.
- setup 2건의 pair-fixture01은 봉 22 두 자리, 상단 Jump 차단, 아래부터 23→24 전환과 두 사람의 실제 섬 착지(26.825/29.305초, injury 0)를 확인했다. `fixture-observation-N.jsonl`과 마지막 X=-415.299/-414.640이 근거다. inspection-pair01은 너무 가까운 설정 위치 때문에 Jump가 공통 목마 상호작용으로 이어져 진행 검사가 아니다. pair02는 양쪽 15→16, slot0만 17 점유 후 두 사람 모두 릴레이 무부상 착지했으며 모든 봉의 두 자리 통과로 쓰지 않는다. 후속 heat-pair01(setup 2건)도 두 사람의 봉 18·19·20 점유와 S 제동 후 실제 섬 착지(slot1 29.050/slot0 30.902초, injury 0)를 확인했다. slot1은 19 이후 25.450초 섬에 먼저 착지한 뒤 20을 다시 잡았으므로 두 사람 모두의 연속 공중 재잡기는 아니다. 이후 4m 외줄·봉 21의 두 자리 진행도 미시험이다.
- cold-load inspection-fixture01은 이동 명령 없이 호스트가 Pursuit로 탈락했다. 복사 씬에서 Playing 뒤 입구 배치 ACK를 기다리는 동안 타이머가 시작될 수 있어 `RouteReady`에 `!EscapeDevelopmentSession.AwaitingEntrance`를 추가했다. 회귀는 대기 중 실제 Update에도 StartedAt=-1, ACK 완료 뒤 정상 시작을 검사한다. 해당 소스 가드는 이 구간 시험 당시 Development 빌드에 포함되지 않았으며, 아래 2026-11-16 전체 빌드에는 반영됐다.
- 첫 Compile+Tests 요청은 Compile PASS 뒤 설정 조회 Network error로 중단됐다. 재시도 `escape-tests-after-parity.json`의 `CurtainCall.EscapeRoute.Tests` 필터는 새 가드 회귀 포함 453/453 PASS(실패·건너뜀 0)다. 최신 전체 check-work·DEV fetch·3/4인 입구부터 성공 엔딩과 경로 난이도 동등성은 미완료다. 구간 시험을 기존 N=2 어그로 평균에 추가하지 않는다.
- 2026-11-15 당시 최신 3인 연속 입력은 모든 새 봉을 host가 통과했으나 최종 성공하지 못했다. 내부로 바뀐 컨베이어 난간 제거, 잘못된 원거리 경로 투영에 따른 AI 왕복 원인과 별도 setup 구간의 배선24/24·성공 엔딩을 `labyrinth-play-qa.md`에 기록했다. 당시 진행 중이던 AI 수정·최종 빌드의 후속 결과는 아래에 기록하며, 전체 완주·최종 check-work·DEV 동기화 완료로 해석하지 않는다.

## 2026-11-16 신뢰 진행도 빌드와 컨베이어 하차 시험 진단

- 전체 Development 빌드 `labyrinth-final-parity-build-result.json`은 Succeeded/errors 0/warnings 32/264.730초다. 내부 난간 제거·입구 ACK 가드·신뢰 진행도·물리적 홀 귀환·발사 진단을 포함했다. `.harness/local/pursuer-remake-planning/escape-tests-route-return.json`은 신뢰 진행도 회귀 10개 포함 463/463 PASS(실패·건너뜀·불확정 0)다. Entry→입구 및 목마 높이 대상의 8m NavMesh 접근 표본은 PathComplete였으나 편집 씬 검사이며 실제 전체 역주행·홀 추격 완료는 아니다.
- `labyrinth-parity-full-3-01`과 `labyrinth-parity-full-4-01`은 모두 실패다. 4인에서는 기존 경로 host0과 새 검사동 경로 actor2가 각각 컨베이어 봉에 탑승했지만 하차 완료를 못 하고 MapExit로 탈락했다. 전체 성공 엔딩·경로 난이도 동등성은 미완료이며 상세 시각과 입력은 `labyrinth-play-qa.md`에 보존한다.
- `labyrinth-conveyor-hold-fixture-02`의 명시 Hold 명령은 기존 conveyor Guide를 대체했다. 이후 hold=false/latchReady=true 표본은 단발 Hold 소비 프레임을 증명하지 않는다. Guide만 유지한 `labyrinth-conveyor-guide-fixture-03`은 33.615초 unloading=true/hold=true/holdProgress=1/latchReady=false/releasedFrames=0을 관측했다. Guide가 하차 직후 flying 상태에서 Tap으로 같은 봉을 재잡을 수 있는 코드 경로를 확인했으며, 공통 서버 Drop 거부로 확정하지 않는다.
- Review에 하차 시도 뒤 자동 재잡기를 막는 `conveyorDisembarking` 가드를 추가했다. scripts-only `labyrinth-drop-review-build-result.json`은 Succeeded/errors 0/warnings 25/69.6798초다. 위 빌드 보고서는 `.harness/local/pursuer-campaign-20261115/`에 있다. 후속 `labyrinth-conveyor-guide-fixture-04`는 39.38초 봉8/latchReady=true/Z182.85→39.754초 pole=-1/flying=true→40.12초 feet(-238,5.880,183.550), flying=false/airborne=false/Normal/injury 0과 이후 안정 상태를 관측했다. 공통 F 하차·수신 발판 착지는 확인했지만, 스크립트는 '점유 중 Z>183' 중간 상태가 0.35초 표본 사이에 지나가 timeout/exit 1로 끝났다. 전체 자동 fixture PASS가 아니며, 공통 게임 Drop 수정이 아닌 Review 재잡기 입력 교정 확인이다. 전체 완주·최종 check-work는 미완료다.

## 2026-11-16 강제 종료 후 복구와 재개

- Unity/시험 플레이어 종료를 확인하고 `origin/dev`를 fetch했다. 작업 브랜치는 `10633c71`에서 `0f95ba68`로 fast-forward했으며 14개 upstream 파일과 작업 변경이 겹치지 않았다. 병합 충돌 0, Git connectivity 검사 exit 0(기존 dangling 객체 알림만)이다.
- `.harness/local/pursuer-remake-planning/restart-recovery-20261116/`에 변경 파일 374개 ZIP·SHA256 목록·tracked patch를 보존했다. ZIP CRC 오류 0, 0바이트 변경 파일 0, 동기화 전후 374개 해시 전부 일치했다. 소스/YAML/설정/문서 텍스트 343개에 NUL 손상은 없다. 원본 PursuitRemodel 해시 `9556701bdc3e8f88b246c38b41f8253c22e3804aa7e33fb5d3f113cb00190814`도 유지된다.
- Windows Kernel-Power 41/6008에 비정상 종료 기록이 있다. 이 기록으로 종료 원인을 특정하지 않는다. 재시작 직후 RAM 15.9GB 중 free 0.5GB였으며 Unity 유휴 import worker를 세션 범위에서 0으로 낮춘 뒤 free 3.26GB가 됐다. 다음 다인 standalone 시험은 Editor를 닫고 진행한다.
- Unity의 `Temp/__Backupscenes/0.backup`을 별도 복사하고 복구 대화창에서 보존했다. `Assets/_Recovery/0 (5).unity`를 별도 YAML `RecoveredLabyrinthReview.unity`로 저장·비교했다. 저장된 Prototype과 차이는 복사 경로에 따른 `GlobalObjectIdHash` 1곳뿐이다. 저장본을 덮어쓰지 않았다. 두 씬 모두 missing scripts 0, 공통 봉 배열 25, 외줄 배열 20이다. 원래 Prototype을 다시 열었다.
- `labyrinth-parity-full-3-02`의 마지막 state-0/1/2 및 임시 state가 NUL로 손상됐고 timeline 끝에도 NUL이 있었다. 원본 로그를 변경하지 않고 위 복구 폴더의 `review-log-salvage/`에 유효 JSON만 보존했다. host 931/참가자 918·917개 유효 timeline, 파싱 실패 0. 마지막 유효 host 343.820초는 `EscapeAllRetired`/Ending이다. 성공 완주가 아니며 강제 종료 이전 실패 결과까지 복구됐다.
- 이 3인 세션에서는 양쪽 runner의 정상 컨베이어 하차·하차장 외줄, 선행자 트램펄린 운반/후행자 재사용, 열 회수와 상부 새 봉을 관측했다. 후행자의 마지막 틈에서 이미 놓인 트램펄린에 반동했지만 local pilot이 지상만 기다려 멈춰 입력을 재개했다(순간이동/setup/restart 없음). 이를 마지막 틈의 실제 bounce counter 증가+비행만 허용하도록 local pilot에서 고쳤다.
- 선행자 actor0가 반출교에서 포획·탈락한 뒤 actor1로 추격 전환됐다. actor1은 입구부터 300.200초 작업장까지 도달했지만 15/24배선 절단 후 포획됐다. 절단기 `carryBlocked`와 변위 정지를 확인했다. 당시 추격자가 약1.56m 옆에 있어 동적 운반 충돌이 의심되지만 충돌체 ID 증거는 없다. 정적 새 중앙 설비 원인으로 확정하지 않는다. 회피 입력을 실제 추격자 반대 방향/변위 확인 방식으로 수정 중이다.

- 2026-11-16 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(선행 실패·생략)

- 2026-11-16 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=FAIL

- 2026-11-16 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=PASS

- 후속 운반 수정 check-work-saw-overlap.txt는 exit0/common716/716(신규 FollowCarrier4개 포함)이다. 이후 읽기 전용 실제 카메라/전투 여부 관측을 추가한 컴파일과 scripts 빌드도 PASS(errors0/warnings25/62.757초). 3-03·4-03 연속 실행은 각각4/24·2/24 절단 후 전원탈락했다. 입력/거리 반올림/모서리 회피 문제와 게임 버그를 구분한 상세 증거는 History/005-restart-recovery.md에 보존했다.

- 복구 후 check-work 재시도는 최신 DEV의 문 테스트 전제 오류로 common 711/712였다. 가이드 완료 상태를 먼저 만들도록 해당 EditMode 테스트만 교정했고 런타임 잠금 규칙은 유지했다. 최종 check-work-recovery-corrected.txt는 exit0, 안전성/문서·규칙/컴파일/공통설정 PASS, common712/712 PASS다. scripts-only labyrinth-recovery-build-result.json은 Succeeded/errors0/warnings25/73.860초. 임시 import worker 설정을3으로 복원하고 Editor 종료를 확인한 뒤4인 연속 시험 labyrinth-parity-full-4-02를 재개했다.

- 4인 full-4-02는 EscapeAllRetired로 종료했다. 두 경로 정상 F 하차·트램펄린 재사용과 마지막 입구 생존자로 재추격을 관측했다. actor3는 같은 세션414.325초 작업장/15배선까지 진행했지만 local pilot의 중앙 보스 근접 사격점 거부와 경계 후퇴 후보 소진으로 회피 입력이 종료됐다. carryBlocked는 없었다. 전체 성공이 아니며 자세한 한계는 labyrinth-play-qa.md에 남겼다. 네 시험 프로세스를 정상 종료한 뒤 Editor를 다시 열었다.
- 코드 점검으로 절단기 FollowCarrier의 기존 겹침 이탈 누락을 확인했다. 공통 CanMove의 allowExistingOverlap을 !align일 때만 사용하며 새 규칙/설정은 만들지 않는다. 초기 정렬과 새 벽 차단은 유지한다. 부분 이탈·깊어짐·다른 벽·초기 정렬 회귀4개를 추가했고 검사 중이다. local exit-aware-side pilot은 기존 ray를 유지하는 측면 사격점과 안전거리8m 이상일 때 측면 회피를 사용한다.

- 3-04 추가 연속 실행은 첫 대포 양쪽 진행 후 inspection 발판 조기 착지로 pilot을 현 위치에서 재개했다. 마지막 actor1이 작업장4/24 후405.726초 전원탈락, 성공 아님. 회피 거리 강제의 마지막 local 교정은 오프라인만 확인했으며 실기 NOT RUN이다. 상세는 History/005. 모든 시험 프로세스를 종료하고 Editor가 닫힌 상태에서 origin/dev를 다시 fetch/FF 확인했다(0f95ba68, Already up to date). 리메이크 Prototype을 편집 모드로 다시 연다.

- 2026-11-16 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=PASS

- 2026-11-16 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-16 묵직한 질주/발소리 작업 시작·중간 결과는 History/006-heavy-gait.md. 보조 AI 2명은 읽기 조사 후 authoring 요청 제한으로 종료되어 주 에이전트가 소스 검토·제작·검증을 이어받았다. DEV 0f95ba68에서 진행, 커밋/push 없음.
- 무거운 질주 연결 완료: 보폭4.29m/주기8.58m, 저음2종과 별도 충격음, 공중 접지음/정지 후 박자/고속 연타 수정. Unity31/31·실제 showcase·고속 audio probe와 check-work 통과. 제출 전 DEV 재확인 후 새 클립이 연결된 clean Prototype을 열었다. 상세·청감/완주 검증 한계는 History/006.

- 2026-11-16 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-16 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(선행 실패·생략)
