# Log

## 2026-11-16 착수
- doctor 1회: 외부 submodule 접근/기존 Task 번호 중복/Unity 설치 감지 실패. 샌드박스 밖 Unity 6000.3.25f1 연결 정상, 외부 에셋 파일 존재. 관련 기능 검사로 실제 영향 확인 예정.
- 기존 씬·설정·재질 등 12개 수정 보존. Unity 저장/정상 종료 후 origin/dev fetch: HEAD와 동일. 전용 feat 브랜치 생성 후 Unity 재시작.
- Blender MCP 연결 정상(빈 기본 씬, protocol 15/서버 16 fallback). Unity MCP는 세션에 노출되지 않아 명시 경로 Pipeline CLI 사용.

## 구현 및 확인
- 직접 오르기·생성 링크·기존 링크 통과에 실제 연속 벽 지지 검사. 허공 아래 판만 있는 가장자리는 거부하고 다른 가장자리로 접근한다.
- NavMesh 반경 .24m, 높이 공통 CrouchBodyHeight(현재 1.08m), voxel .08m 재베이크. 플레이어 Crouch/CrouchWalk를 리타깃하고 중형까지 천장에 닿지 않도록 발을 유지한 무릎 IK 보정. 환풍구 착지 때도 앉기 자세로 전환한다.
- 실제 숙소·화장실 덮개 개방 상태에서 각각 약 3.1m의 벽 오르기 경로 PathComplete, 환풍구 내부 경로 PathComplete 확인. 임시 개방은 원복. 덮개 개방 시 NavMesh carve 이후 링크도 갱신하도록 연결했다.
- 절단기 투사체 1개당 호스트가 1회 피해 판정. 현재 뼈 위치의 피격 범위, 자세를 보존한 반동, 충돌을 고려한 밀림/낙하, 사망 동작. 소형 3회·중형 5회·10초 부활을 GameSettings와 배치 편집기에 연결했다.
- 체력/피격/재스폰 상태를 스냅샷 v7에 추가. 최초 패킷 크기 검사 실패(1255B)를 발견해 정수 가변 길이와 선택적 타이머 전송으로 수정했다. 버전/정밀도/26개체 패킷 검사 통과.
- S06_StageCorridor 실제 65m 복도와 입구를 추격 Rooms 및 타이머 안전지대에 등록했다. 이전 미등록 영역의 안전 기본값 유지, 개체 배치와 위험/안전 변경 가능.
- 봉합 선택: 대상 4부위, 원본 캐릭터/의상/팔다리, 어깨·골반 또는 팔꿈치·무릎. 생성 메시/실은 인스턴스 전용이며 해제 시 원본 sharedMesh 복원. 캐릭터의 짧은 정강이 때문에 신발이 커지는 문제를 축별 변환으로 보정. 정면·측면 확인(Captures/).
- 변환한 3개 동작은 기존 플레이어 동작과 같은 비공개 아트 저장소 `Assets/_ThirdParty/curtaincall/Pursuit/Resources/Animations/Pursuit`에 보관. Blender의 원본 모델은 수정하지 않았다.
- 초기 배치에 NavMeshAgent를 추가할 때 2cm 이동하는 문제를 발견해 초기 위치를 복원했다. 통로 테스트 종료 경계의 `>`/`>=` 차이도 수정했다.
- 미리보기 저장으로 바뀐, 착수 시 깨끗했던 폰트 캐시 3개는 착수 기준으로 복원했다. 기존 12개 사용자 변경은 보존했다.

## 검증
- Unity 컴파일 오류 0(compile.json).
- EditMode: PursuitUpgradeTests 7/7, PursuitDirectClimbTests 8/8, PursuitTraversalSafetyTests 5/5, PursuitSnapshotCodecTests 3/3.
- PlayMode: 신규 전투/환풍구 2/2, 기존 직접 오르기·내려오기·호스트/복제본 IK 2/2. 총 27개 통과. 결과 JSON은 이 폴더에 보관한다.
- Knowledge: OK(21 folders). 최종 check-work 및 origin/dev 동기화 결과는 아래에 기록한다.
- 실제 두 클라이언트 접속에서 피격·재스폰을 보는 실기 검사는 수행하지 않았다. 자동 검사는 직렬화/복제본 IK/격리 플레이 및 실제 씬 경로를 검증한다. 최종 게임 느낌은 사용자 플레이 확인 대상이다.
- 최초 check-work: 안전성 통과, 새 캡처 PNG 8개의 대문자 파일명 때문에 구조 검사 실패. 출력 로그를 검사 대상 폴더에 동시에 기록해 fingerprint에서도 파일 잠금 발생. 캡처 파일명/생성기를 소문자로 수정하고, 새 수정 묶음의 재검사는 임시 폴더에 출력 후 결과만 복사한다. 기존 검사 결과는 check-work-initial.txt에 보존.
- 수정 묶음 check-work: 안전성 PASS, 문서/규칙 PASS, 컴파일 PASS, 공통 설정 PASS. 이 명령의 자동 테스트 옵션은 미선택이며 위 27개 별도 실행 결과로 기록한다. 기존 문서 길이 경고 76건, private 저장소 신규 에셋 미커밋 안내는 남는다.
- 제출 전 Unity 정상 종료(미저장 없음), origin/dev fetch 및 ff-only 반영: Already up to date, HEAD...origin/dev 0/0. commit/push 없음. 비공개 저장소도 새 동작 에셋만 미커밋 상태다.
- Unity 6000.3.25f1 재시작 후 WaitingRoom 씬, 편집 모드 정상 확인. 종료 시 다시 저장된 미리보기 폰트 캐시 3개를 재복원하고 명시적으로 재임포트해 메모리도 디스크와 맞췄다. Crouch/CrouchWalk/Death 리소스 로딩 모두 확인.

- 2026-11-16 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-11-16~17 봉합 재작업
- 사용자 요청으로 위 구현의 실제 팔다리 이식·메시 교체·실 오브젝트 방식을 폐기했다. 원본 마네킹의 메시/정점/뼈/재질 참조를 유지하면서 선택한 캐릭터의 피부·의상과 봉합선만 UV 텍스처로 전사한다. 기존 선택 데이터 구조 유지.
- 착수 시 Unity 씬 저장·정상 종료 후 origin/dev fetch/ff-only 반영(0/0), 재시작 완료. doctor는 같은 Task에서 반복하지 않았다.
- `PursuitSuturedLimbs`는 레이어 property block과 공유 텍스처 수명만 관리한다. 원본 형상에는 추가/절단/리타깃 작업이 없다. 플레이어 프리팹은 읽기만 하며 복제해 붙이지 않는다.
- 서로 다른 원본 UV 섬 사이 보간으로 생기는 무늬 왜곡을 작은 2D 베이크 영역/BVH 근접 면 조회로 개선했다. 손가락 UV 경계에 여백을 추가하되 실밥 주변은 보호해 두께를 유지했다.
- 기존 TroupeV7 셰이더에 표면/실밥 색·높이 레이어를 추가했다. 그 위에 기존 때/마모/혈흔을 순서대로 적용한다. 피 표시 해제 시 봉합·오염 유지. 빈 선택은 원래 마네킹 표면이다.
- 첫 검사에서 MonoBehaviour 필드 생성 시 MaterialPropertyBlock이 null인 문제를 발견해 Apply 시 지연 생성으로 수정했다. 잘못된 테스트 명령 옵션으로 HTTP 400이 1회 발생했고 지원 옵션으로 재실행했다.
- 최종 EditMode: 표면 봉합 5/5, 업그레이드 7/7, 기존 외관 편집 5/5 = 17/17 통과. JSON 결과 보관. 원본 메시·정점·삼각형·뼈·재질 및 렌더러 수 불변, 4개 원본 캐릭터와 다른 부위 전사, 레이어/혈흔 숨김/해제를 검사했다.
- 4개 캐릭터 × 깨끗함/오염 × 정면/측면/손 근접 24개와 걷기·달리기·오르기·앉기·앉아걷기·사망 6개 캡처를 생성했다. 동작 캡처는 평가된 BakeMesh를 캡처 전용으로 사용해 같은 tick의 이전 GPU 포즈 재사용을 방지한다. 실제 게임 메시는 그대로다.
- 3부위 조합 첫 생성 약 1.0~1.3초(에디터 측정). 같은 메시/조합은 공유 캐시를 사용하며 프레임별 재생성하지 않는다. Blender 원본 수정 및 네트워크 동작 변경 없음.
- PlayMode 표면 생성/공유/해제 1/1 추가 통과. 실제 픽셀 생성, 두 개체의 공유 캐시, 한 개체 제거 후 유지, 마지막 선택 해제 후 GPU 텍스처 해제를 확인했다. 이번 재작업 총 18개 통과.

- 2026-11-17 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 최종 check-work 안전성/문서/컴파일/공통 설정 PASS (`surface-check-work.txt`), 기존 문서 길이 경고 76건. 자동 테스트는 위의 개별 18개 결과로 기록했다.
- 제출 전 대상 Unity 정상 종료(미저장 없음). 별도 `fix-engagement-system-enhancement` 작업 트리 에디터는 유지했다. origin/dev fetch/ff-only 결과 Already up to date, HEAD...origin/dev 0/0. 기존 사용자 변경 보존, commit/push 없음.
- Unity 6000.3.25f1 재시작 완료. WaitingRoom 편집 모드, 씬 dirty 없음, 표면 봉합 셰이더 로드/컴파일 정상 확인.

## 2026-11-17 사용자 설정 저장 및 공유
- 사용자가 설정한 WaitingRoom 씬과 GameSettings, `FacilityConnections` 프리팹을 저장 범위에 포함. 저장 시 열린 씬에서 배치 29개, 봉합 선택 5개 확인. 순찰 지점과 단계별 추격 속도 조정도 함께 보존한다.
- 저장한 씬/프리팹으로 안전성·문서·Unity 컴파일·공통 설정 검사 통과. 동일 코드의 관련 테스트 18개는 앞선 결과를 재사용했다.
- 폰트 캐시와 부동소수점 직렬화 차이만 있는 재질은 로컬에 유지. 테스트 결과와 작업 문서를 남기고 검증용 캡처·프로브·원시 검사 로그는 로컬에 둔다.
- 제출 전 대상 Unity 정상 종료, origin/dev 기준 0/0 동기화. 비공개 에셋 main에 `dc76d169`를 먼저 올리고 작업 브랜치에서 그 포인터를 참조한다.

- 2026-11-17 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-11-17 난간 재현 및 치트 이동 핫픽스
- 사용자 Play 상태에서 화물 추격자 (-57.558,.052,75.195), 목표 (-57.558,5.680,75.195)를 반복 확인. 단계 제한은 허용이며 경로 Partial. 전방 벽 검사만으로 난간 입구 옆의 연속 벽을 제외한 코드 결함.
- 옆 벽의 수직/착지 구간 연속 지지를 검사하고 벽 방향과 착지 방향을 분리. 벽 타기 구간은 벽을 향하고 마지막에 착지 방향으로 회전, 손 IK도 실제 벽 방향 사용.
- 첫 실제 이동 검증에서 추가 결함 발견: 에이전트의 링크 진입 오차로 벽 검증 탈락, ResetPath가 링크를 즉시 완료하여 순간이동. link.startPos 기준으로 검증하고 거부 시 시작점 Warp 후 경로 해제.
- 실제 Play 기록: 4.11~8.11초 Climb 26개 샘플, 최대 손발 접점 3개, 이후 y=5.64 난간에서 목표까지 추격. 화물과 소품실 난간 경로 모두 PathComplete, 런타임 링크 281개.
- 추가 사용자 요청 치트 즉사 재현: 최초 호스팅 Start 씬에서 3.58초 y=-15.562로 떨어져 MapExit, 로딩 후에도 Fallen 유지. 같은 씬 재시작에서는 재현되지 않아 오래된 낙하 이력 가설을 제외.
- Dev 시작 준비/커튼 공간 전환 중 중력 이동·외부 속도 진행을 보류하고 이전 공간의 낙하 이력/WorldDeath 보고를 제외. 배치/커튼 해제 즉시 정상 판정 재개. 공통 사망 높이·낙하 피해 수치 변경 없음.
- 검사: PursuitUpgradeTests 10/10, PursuitDirectClimbTests 8/8, PursuitClimbIkTests 26/26, NetworkFallSegmentTests 24/24, 최초 Start→대기실 PlayMode 1/1. 새 경로 테스트의 초기 1실패는 실제 캡슐과 다른 구형 검사 볼륨 탓으로 판정하고 실제 BodyBottom/Top/Radius 캡슐 검증으로 교정.
- 사용자 씬/배치/봉합 설정과 기존 폰트 캐시·재질 미커밋 변경 보존. 이번 후속 수정은 아직 커밋/푸시하지 않음.

- 2026-11-17 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 제출 전 Unity 정상 종료(미저장 0) 후 origin/dev fetch/ff-only: Already up to date. check-work 안전성·문서/규칙·컴파일 PASS; 자동 테스트는 위 별도 실행 결과 69/69. 기존 문서 길이 WARN 76건(hard 0)은 유지.
- 마지막 측면 촬영에서 NavMeshAgent 자동 회전과 벽 바라보기 충돌 추가 발견. 오르기 중 updateRotation을 보류하고 착지/중단 시 원래 값을 복원하도록 보정.
- 회전 보정 후 실제 측면 3장 재촬영: 상승 진행률 .22/.48에서 정면 (-1,0,0)으로 벽을 향함, .82에서 (0,0,1) 착지 방향 전환. y=5.64 도착, updateRotation=true 복원, 플레이어 Normal. 추가 최초 치트 시작도 즉사 없이 확인.

## 2026-11-17 전투 옵션·환경 소음·UV·끼임 보정
- 작업 재개 시 대상 Unity만 정상 종료, origin/dev fetch/ff-only Already up to date 후 재시작. Unity MCP 미노출로 Unity Pipeline CLI 사용. 기존 사용자 설정·폰트/재질 및 앞선 미커밋 수정 보존.
- GameSettings의 SawCanKill/SawKnockback 분리, 모두 OFF로 저장. 체력 최소 1, 피격 표현 유지, 밀림 OFF 시 오르기/이동을 중단하지 않는다. 처치만 ON일 때 밀림은 켜지지 않으며 사망/10초 재스폰은 기존 규칙 유지.
- 최고 소음(40m): 절단기 발사·대포 발사·문 개폐. 큰 소음(24m): 절단기 잡기·트램펄린 반동·환풍구 개방·TV. 반경/TV 반복 간격은 공통 설정, 벽 감쇠는 현재 단계 설정. 승인된 동작에 연결, 호스트만 소비하며 기존 직접 시야 추격은 소리로 덮어쓰지 않는다.
- UV 직접 조명 시 비춘 플레이어 우선 2초, 재유인 4초. 원뿔·거리·벽 가림·방/순찰 제한 적용, 지상과 소품 위 공통 대상 선택. 접촉 포획은 계속 우선. 멀티플레이 복제 빛도 호스트에서 판정하도록 연결했으나 별도 다중 인스턴스 실험은 이번에 수행하지 않음.
- NavMesh carving만으로 기존 겹침이 풀리지 않는 문제를 실제 문에서 재현. 25Hz 캡슐 겹침 검사와 수평 출구 탐색, 같은 층 지지/다른 벽 통과 금지, 출구 없는 공간 순간이동 금지. 오르기 중 정적 지지 벽은 제외하고 동적 침입만 보정. 절단기 밀림 옵션과 독립.
- 실제 저장된 장애물 41개: Door 18, VentOpen 2, None 21. 사용자 배치 29개 유지. Unity 저장 시 끊어진 m_CorrespondingSourceObject 참조가 0으로 정리됐고 나머지 씬 차이는 Noise 직렬화/프리팹 오버라이드뿐임을 diff로 확인.
- 검사: 새 EditMode 12/12, 프리팹 PlayMode 5/5. 초기 물리 2실패는 비활성 Collider의 ComputePenetration false 문제로 검사 동안만 활성화/finally 복원. 초기 UV 1실패는 다른 플레이어가 포획 반경 안에 있었던 fixture 오류로 위치를 바르게 분리. 기존 Hear 2인자 반사 호출 호환도 유지.
- 실제 씬: 화장실/숙소 덮개 개방 VentOpen 2건, 문 개방 Door 1건, 숙소 입장 후 TV 주기 소음 확인. 계단 출입문 내부 추격자는 (4.51,.03,101.97)→(4.51,.04,102.56), SeparationCount 0→1, Clear=true, SawKnockback=false.
- 실제 화물 난간 회귀: 벽 방향 유지 후 (-57.74,5.64,75.45) 도착, 오르기 중 SeparationCount=0, 플레이어 Normal. 시작 치트 이동도 생존. 검증 중 게임 종료 Vivox 취소 스택은 기존 서비스 로그이며 변경된 추격 코드 예외는 관찰하지 못함.
- 기존 회귀 파일명 필터는 partial 클래스 이름과 달라 0건이었으며 PASS로 계산하지 않았다. 메서드명 실행의 초기 실패는 활성 단계 가설과 달리 WaitingRoomPursuit 구형 테스트 씬 NavMesh 정점 0/표본 없음 때문이었다(Integrated 씬은 편집 상태 9009정점). 해당 행동 검사만 현재 통합 씬을 사용하도록 수정. 실패 코루틴 finally가 복원하지 못한 설정은 디스크 에셋을 LoadSerializedFileAndForget/CopySerialized로 읽어 되돌렸고 TearDown 복원도 추가했다. 사용자 1단계 속도 7·시야 5 및 처치/밀림 OFF 복원 확인. 커밋/푸시는 이번 후속 요청에서 수행하지 않음.

- 2026-11-17 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 최종 개별 회귀: PursuitStimuliSafetyTests 12/12 + PursuitUpgradeRuntimeTests 5/5 + 기존 GraphHearingChaseSearchAndCulling 1/1, 총 18/18. 마지막 테스트 이후 설정 복원 확인. 실제 씬 검증도 완료.

- 2026-11-17 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 최종 check-work 안전성·문서/규칙·Unity 컴파일·공통 설정 PASS. 선택 자동 테스트는 SKIPPED이며 위 별도 실행 18/18로 검증. 기존 길이 경고 76개, hard 0. 대상 Unity PID 53300 정상 종료(unsaved 0) 확인 후 origin/dev fetch/ff-only Already up to date, HEAD...origin/dev=1/0. 다른 작업 트리 Unity는 유지.
- Unity 6000.3.25f1 재시작 ready(PID 46008), WaitingRoom 편집 모드로 복귀. 플레이 검증용 문 개방/순간이동 상태는 저장하지 않았다.

## 2026-11-17 사용자 설정 포함 커밋·dev PR 요청
- 열린 씬과 로드된 게임 설정의 저장 상태 확인: 미저장 0, Prefab Stage 없음. WaitingRoom 씬·공통 설정·앞선 난간 및 치트 이동 수정·이번 소음/UV/끼임 수정을 함께 저장한다.
- Unity 정상 종료 요청이 지연됐으나 강제 종료 없이 PID 46008 소멸 확인 후 origin/dev fetch/ff-only Already up to date(HEAD...origin/dev 1/0). 다른 작업 트리의 Unity는 조작하지 않았다.
- 검증된 코드·씬·게임 설정은 최종 check-work(02:41) 이후 수정되지 않았다. 컴파일/공통 설정 PASS 및 기능 회귀 18/18·이전 난간/낙하 69/69 결과를 재사용하고 안전성/문서 검사를 다시 수행한다.
- 사용자 배치·봉합·단계 값은 씬/설정과 기존 커밋에 포함한다. 동적 폰트 캐시, 재질의 소수점 직렬화 차이, 임시 프로브/캡처는 로컬에 유지한다. save-work의 전체 add 대신 명시 경로만 stage한다.
- new-pr -DryRun으로 dev 대상과 본문을 확인했다. 남겨 둘 로컬 캐시/프로브 때문에 전체 작업 트리 clean 검사는 충족되지 않으므로, 검사한 커밋을 push한 뒤 gh로 PR을 생성한다. 병합은 하지 않는다.
- 저장 직전 안전성 OK(191 변경 경로), 문서/규칙 Fast OK 확인. 실제 커밋 대상은 관련 코드·씬·설정·기록 47개 파일이며 Unity 검사/회귀 결과는 위 확정 결과를 재사용한다.
- 사용자 설정 포함 수정 커밋 f652a8e3를 feat/waiting-room-pursuer-upgrade에 push 완료. PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/406 (feat/waiting-room-pursuer-upgrade → dev). 병합하지 않고 검토 대기.
