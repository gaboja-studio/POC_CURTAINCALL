# Log

- 2026-11-09 외줄 단계 재개: 사용자 선택은 ‘먼저 씬별 차이를 정리하고 통일 기준 정하기’. 구현은 보류하고 비교/초안 문서만 갱신했다. doctor는 동일 Task에서 이미 실행했으므로 반복하지 않았다.
- Unity 씬/설정 dirty 없음 확인, 동적 폰트 백업 후 종료. origin/dev fetch/ff-only는 2d591802 동일, start-work로 같은 브랜치 재개. 종료 시 자동 초기화되는 폰트는 백업으로 복구 후 Unity 재실행. 기존 바닥 통일 코드와 다른 Task 문서 변경 보존.
- Unity 미리보기 씬으로 외줄 공연·대포 단독·대기실·탈출로 A/B/C를 읽었다. 최초 eval은 5초 응답 제한으로 결과를 받지 못했고, 제한을 명시한 프로젝트 밖 임시 run_script로 정상 완료했다. 씬 저장·Play 실행·게임 코드 수정 없음.
- 확인: 탈출로 A 줄 참조 15개/실제 폭 0.55~1.85m, B 1개/C 2개 폭 0.4m. 옆줄 설정 간격 A 2m, 나머지 3m. A의 일반 추락 보호 켬+균형 추락 허용, B/C 보호 끔. A만 목마 하차 발판 2개 지정. 대기실 코스는 리허설 때 생성, 대포 단독 씬에는 외줄 코스 없음.
- 이전 목록 정정: bidirectionalSceneTraversal는 저장 호환 칸이며 양방향 진입은 이미 공통. 카메라에 따른 입력 반전·독립 줄의 옆줄 방향/거리 조건·매달림 탐색 범위·생성 줄과 씬 줄의 가장자리 판정 차이를 추가했다.
- tightrope-unification-plan.md에 사용자 공통 규칙 초안과 단계별 기준안 정리. 탈출로 기준 외줄 점프 1.5m는 첫 제안이며 미확정·미적용. 사용자 언급대로 대포 코스 톱 낮추기는 후속 항목으로 유지한다.

- 2026-11-09 일반 바닥 통일 구현: 미저장 씬 없음 확인 후 Unity 종료, origin/dev fetch/ff-only 결과 2d591802 동일. 기존 문서 변경 보존. implement-code 스킬 적용.
- Base.Character.JumpHeight의 콘텐츠 덮어쓰기 제거. 일반 바닥은 공통 저장값 0.8 사용. 이동은 기존부터 Base FreeMoveSpeed·자세 배율 사용하므로 수치·조작 코드는 변경하지 않았다.
- 외줄·대포 사용 동작 보존 요청에 따라 기존 외줄 높이 계산만 RopeMovement getter로 이동. 기존 외줄 높이 1.0/1.5와 대포 발사·트램펄린·봉·부상 정책 유지. HumanCannonSettings의 기존 jumpHeight 필드와 GUID를 유지하고 설명만 정정했다.
- 회귀 검사를 공통 설정 변경·활성 씬 전환·등록 수명·실제 PlayerMover 점프 속도·재시작·부상에 맞춰 갱신. 일반 바닥 도달 거리 검사용 에디터 계산도 Base를 읽도록 정정했다.
- EditMode: CannonJumpSettingsTests 2/2, TightropeMovementFeedbackTests 11/11, RouteCourseTests 18/18 통과. HumanCannonWideCourseTests 5/6 통과, GroundSaws 검사는 톱 상단 0.42 > 공통 두 다리 손실 점프 0.4로 실패. 기존 1.2 기준 검사 대신 실제 공통 값을 읽게 했으며 이 실패를 숨기거나 톱 배치를 바꾸지 않았다.
- PlayMode 초기 실행은 기존 테스트 Call 도우미가 ServerCut의 선택 인자를 못 채워 실패했다. 도우미를 현재 시그니처에 맞춰 수정한 뒤 일반 점프 최고점·부상·재시작/종료와 공중 이동/재점프 2개 통과. 추가한 실제 이동 속도 검사는 처음 앉기 수직 변위까지 합산해 실패, 수평 거리 측정으로 정정 후 걷기/달리기/앉기 1개 통과. 기록: .harness/local/task114-floor-play.json 및 task114-floor-movement-final.json.
- PlayMode 대포 회귀: FourFinalFlights_RequireNewFAndPreserveHealthyLeglessAndOneArmBodies 1/1 통과. 온전함·다리 손실·외팔의 실제 탑승/발사/비행/안전 착지 확인. 최종 관련 PlayMode 4개 통과. 모든 씬 수동 순회·다중 클라이언트 검사는 수행하지 않았다.
- Unity 종료 직후 escape-labels.asset 동적 폰트 캐시 초기화 변경 발생. 종료 전 git clean 상태와 Editor-prev.log의 `Clearing [escape-labels]` 및 TMP_EditorResourceManager.quitting 코드를 확인했다. 자동 변경본을 .harness/local에 백업하고 종료 전 내용으로 복구·재임포트하여 Assets/Resources diff 없음 확인. UI 변경 없음.
- Unity 6000.3.25f1 PID 26568 ready, Start 씬 dirty=false, Play/컴파일 종료. BaseGameSettings.asset 선택, 실제 getter 점프 0.8·이동 3 확인. 커밋·push 없음. 다음 기획 검토는 톱 통과 조건이며 외줄·봉 통일은 별도 요청 범위다.

- 2026-11-09: origin/dev fetch 결과 2d591802로 기존 HEAD와 동일. 새 브랜치 refactor/common-game-settings 생성. 커밋·push 없음.
- 기존 Task-112 및 Facts/Decisions 변경을 보존. 새 worktree는 에셋 8.01GB + Library 12.58GB 대비 여유 약 15GB로 사용하지 않고, 이번에 만든 깨끗한 worktree만 git worktree remove로 정리했다.
- doctor 1회 실행: 기존 Task ID 9개 중복으로 FAIL. CLI beta.13/문서 beta.15 차이, 에디터 미연결, skill-links 등의 WARN. Unity 버전·설치·ThirdParty·gh는 OK. 이번 조사에 필요한 도구는 동작했다.
- submodule 원격 fetch 및 고정 gitlink c1e64a6d update, 양쪽 LFS pull 완료. 외부 에셋 원격 최신 커밋으로 임의 이동하지 않았다.
- 정적 조사: GameSettings는 기존 정본이다. Base 점프 getter의 CharacterJumpHeightOverrides, TightropeCourse.sceneShape, HumanCannonPole·Trampoline의 직렬화 수치, 대포 부상 정책으로 적용 경로가 분산되어 있다.
- Unity open 성공: C:/Unity/POC_CURTAINCALL, 6000.3.25f1, 실제 Editor 프로세스 확인. Pipeline 준비 완료 확인 중.
- 게임 코드·씬·세팅 값 수정 없음. 현재 조사는 정적 근거이며 실제 플레이 측정은 아직 하지 않았다.
- 에디터 ready 확인: PID 4284, C:/Unity/POC_CURTAINCALL, Start 씬, Play/컴파일 중 아님. GameSettings.asset을 선택하고 실제 참조 6개(Base/Tightrope/HumanCannon/WaitingRoom/Exploration/EscapeRoute)가 모두 지정 경로에 연결된 것을 읽기 검사했다.
- 다음: 기존 공통 설정을 유지하며 점프 덮어쓰기부터 제거·이관할 범위를 정한다. 수치 미정 상태에서 임의로 전 씬 성능을 바꾸지 않는다.
- 추가 요청 조사: 자체 런타임 C# 624개에서 명시적 직렬화 필드 1,217개, public 후보 1,476개, 상수 640개를 Roslyn 구문 트리로 추출했다. public 후보에는 상태·DTO 포함. GameSettings 11개 에셋의 읽기 값 876개 및 30개 씬의 직접 저장 스칼라 9,187행을 별도 CSV로 저장했다.
- 에디터 임시 스크립트의 Roslyn 참조 실패는 프로젝트 밖 임시 조사 코드에 한정. 소스 구문 분석은 PowerShell 호스트 Roslyn으로 수행하고, Unity 임시 읽기 스크립트는 에셋 값 조회만 성공 실행했다. 프로젝트 컴파일 실패로 기록하지 않는다.
- settings-inventory.md, settings-inside.md, settings-outside.md, settings-overrides.md와 Inventory/에 전체 목록 저장. 프리팹 상속 전개·모든 함수 내부 상수 의미 분석·플레이 실측은 포함하지 않았다.
- 사용자 지시 UI 제외 반영: settings-without-ui.md에 행동·판정 중심 31개 묶음과 덮어쓰기 12경로 정리. 원본 CSV는 증거로 보존하고 UI 포함 수량임을 표시했다. 게임 입력·상호작용은 UI 폴더 구현 여부와 무관하게 유지하며 UI 코드·에셋 삭제는 하지 않았다.

- 2026-11-09 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=SKIPPED(Unity 변경 없음); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-09 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=SKIPPED(Unity 변경 없음); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-09 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=SKIPPED(Unity 변경 없음); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-09 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-09 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-11-09 외줄 공통화 구현
- Unity 종료 후 origin/dev fetch·ff-only merge: 이미 최신 2d591802. 기존 변경 및 TMP 종료 시 자동 초기화된 escape-labels를 작업 전 사본으로 보존. Task 재개, doctor 중복 실행 안 함.
- 외줄 이동·판정은 기존 GameSettings.Tightrope로 통일. 외줄 점프 1.5m 독립값, 폭 0.55m, 허용 높이 0.3m. HumanCannonSettings.jumpHeight 및 CharacterJumpHeightOverrides 제거. 기존 대포 발사 탄도는 변경하지 않음.
- 옆줄 선택은 실제 줄 위치와 3m 범위 사용. 배열 순서·같은 방향/높이·레이아웃 간격·구간 제한 대신 최근접 후보 선택. 높이 차이는 3D 거리 가정이며 질문에 아직 답변 없음. 상하 이동 궤적과 T 중간 전환에도 착지점 고정. 3m 경계에 1mm 수치 허용으로 원거리 float 좌표 오차 보정.
- 목마 서비스는 플레이어 생성 시 없으면 생성하며 authored 서비스가 있는 씬에서는 기존 서비스 사용. 바닥 지지 검사·안전 하차 자리 검사·F 입력·균형 분리 연결. 외줄에서 내려온 경우만 자동 하차하고 바닥에서 새로 탄 목마는 유지.
- Unity API로 설정 및 InputAction 에셋 저장. T/TurnAround 추가, 기존 바인딩 ID 유지. 불필요한 높이 덮어쓰기 소스 삭제도 AssetDatabase 사용.
- 초기 EditMode 표면 검사 65/66: 2.4m+1.8m 대각선 3m 경계 float 오차 실패를 보정. 초기 PlayMode 1/2: 1.5m 실제 점프·높은/낮은 줄 이동·공중 T 궤적 통과. 바닥 목마 fixture의 탑승 대상 없음 원인 조사 중.

- 사용자 추가 답변 수신: **같은 높이에 있는 줄만 수평 거리 3m 이내**. 초기 3D 거리/상하 줄 이동 가정은 폐기하고 후보 판정 및 테스트를 변경한다. 진행 중인 입력·일반 바닥·대포 회귀 검사가 끝난 뒤 관련 코드를 갱신한다.

### 최종 반영 및 검증
- 사용자 확정 답변 적용: 같은 높이의 줄만 수평 거리 3m 이내. 1mm float 오차 외 높이 차이는 거부한다. 목마 분리 점프의 몸 높이 차이는 출발한 줄과 도착 줄의 높이 판정과 분리했다. 높은 줄로 올라가는 궤적 지원은 폐기했다.
- 바닥 목마 fixture는 NGO InboundState/TransformStateUpdate를 통해 원격 소유자 위치를 고정했다. 코스 비활성→공통 서비스→재활성→코스 제거 복구, 바닥 F 탑승 유지/균형 비활성/F 홀드 안전 하차 통과. 리허설 루트를 껐다 켤 때 component.enabled를 잃는 수명 오류도 수정.
- EditMode 최종: TightropeSurfaceControlTests 68/68, CommonSystemBindingsTests 18/18, TightropeMovementFeedbackTests 11/11, InputBindingPreferencesTests 20/20, CannonJumpSettingsTests 1/1, FeedbackRegressionTests 3/3. 총 121/121.
- PlayMode 최종: CommonRopeRules 2/2(1.5m 실제 외줄 점프, 같은 높이 옆줄 이동·높이 다름 거부, T 중간 전환, 바닥 목마·서비스 수명); Ordinary 3/3(걷기·달리기·앉기, 부상/재시작/콘텐츠 종료 점프, 공중 방향 변경/재점프); FourFinalFlights 1/1(정상·다리 손실·외팔 대포 발사). 총 6/6.
- 증거: .harness/local/task114-{테스트 클래스명}.json, task114-CommonRopeRules-final.json, task114-Ordinary-final.json, task114-FourFinalFlights-final.json. 초기 실패 보고서는 별도 보존.
- 검사 한계: 두 실제 클라이언트 동시 플레이 및 모든 씬 수동 완주는 미검증. 기존 지상 톱 높이(0.42m 대 다리 손실 공통 점프 0.4m)는 사용자 지정 후속 작업으로 유지. 추락 3m/5m 피해·봉/대포 사용 동작·UI는 변경하지 않았다.
- Knowledge 검사 OK(17 폴더), diff 공백 검사 통과. 최종 check-work: 안전성·문서/규칙·컴파일 PASS. 문서 길이 경고 60개, hard 오류 0개. check-work 내부 테스트는 선택하지 않았고 위 127개 집중 테스트를 별도 실행했다. 커밋·push 없음.

- 2026-11-09 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-11-09 봉 씬별 차이 조사
- 요청: 봉 구현 전 씬별 다른 설정과 동작 정리. UI 제외, 새 수치·예외 확정 없음.
- 재개 동기화: Unity 편집 장면/설정 에셋 dirty=0 확인 후 정상 종료 요청. 종료 응답 지연으로 computer-use도 시도했으나 kernel assets 경로 오류로 UI 확인 불가. 이후 정상 종료되었고, 종료 전 백업한 동적 TMP 폰트를 복원하여 기존 변경을 보존했다. 강제 종료 없음.
- Unity 종료 후 origin/dev fetch, merge --ff-only 결과 Already up to date (2d591802). start-work 재개, Unity 6000.3.25f1 다시 열기 완료. 컴파일 idle / errors=[] 확인.
- 에디터 read-only preview scene 6개와 프리팹 3개 조사. 봉 64개, 활성·등록 36개. 외줄 리허설 생성은 소스 조사로 보완. 씬/프리팹 저장 없음.
- 결과: pole-differences.md 및 Inventory/pole-scene-values.csv. A 일반 봉 4인/협동 봉 1인, B/C 및 대포 활성 봉 1인, 외줄 공중그네만 사람 밑 2인. 외줄 스위치 고정 매달리기, 도약/방향/범위/타이머/연습 보정/장치 유지가 분리됨.
- 기본 외줄 공연의 RelayGap이 1인 SwingGap을 대체함을 확인. 대포 리허설 프리팹에는 봉이 없음. 비활성 구 배치를 활성 비교에서 제외함.
- 봉 코드·설정·씬 변경 및 플레이 테스트 없음. 기존 바닥/외줄 구현과 사용자 변경 보존. 다음은 공통 봉 정책 및 예외 결정 후 구현.

- 2026-11-09 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 봉 조사 마감 검사: verify-knowledge OK(17), check-work 안전성/문서/컴파일 PASS, 자동 테스트 선택 안 함. CSV 64개/활성 36개/활성 미등록 0개, 보고서 링크 누락 0개, git diff --check 통과. 씬·프리팹·TMP 폰트 추가 변경 없음, 에디터 dirty scene 0. Unity 열림 유지.

## 2026-11-10 외줄 양방향·재연습 제한 보완
- 사용자 요청: 모든 외줄 양방향 이동. 후속 지시로 튜토리얼 도착 대기만 예외이며 이후 재연습은 자유 왕복한다. GameSettings 공통 수치 사용 여부를 설명했다.
- 같은 Task doctor는 재실행하지 않았다. 미저장 씬/설정 에셋 0 확인, Unity 정상 종료 후 동적 TMP 폰트 백업 복원, origin/dev fetch + ff-only merge: Already up to date (2d591802), Task 재개 및 에디터 재실행. 기존 변경 보존.
- 기존 PlayerMover/TightropeControlScheme은 음수 이동과 T 전환을 처리하고 공통 진입은 양끝을 허용한다. 이를 씬별 옵션으로 이중화하지 않았다.
- WaitingRoomRehearsal: 도착 벽과 공중그네 학습 종료 도착대 복귀는 !practice일 때만 적용. 재연습에서 모든 인원이 도착해도 자동으로 코스를 교체하지 않고 기존 과반 다음 투표로만 진행한다. 튜토리얼 자동 이수와 도착 대기는 유지한다.
- 검사: RehearsalArrivalTests 최종 43/43. 확대 Rehearsal 검사 77개 중 76 통과/1 실패(수명 관리 18/18 포함). 실패는 RehearsalCannonSourceTests.W6, 기존 requireCarrierForBounce 씬1/복사 프리팹0 불일치. 해당 두 에셋과 트램펄린 코드는 수정 없음. 결과 .harness/local/task114-bidirectional-{arrivals-final,rehearsal-first}.json.
- PlayMode 실제 LAN 호스트1 검사 1/1: 씬 배치 줄/생성 줄 × 시작/끝 진입 4경우, W/S 왕복·T 뒤돌기·원래 플랫폼으로 복귀 통과. 결과 .harness/local/task114-bidirectional-play.json. 실제 두 클라이언트 검사는 하지 않았다.
- unity recompile --strict는 오류0/기존 경고37로 종료1을 반환했다. 후속 일반 컴파일 검사 PASS. 씬/프리팹/UI 변경 없음. 일반 외줄/재연습 공통 규칙과 튜토리얼 예외를 Decisions 및 Facts에 기록했다.

- 2026-11-10 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
