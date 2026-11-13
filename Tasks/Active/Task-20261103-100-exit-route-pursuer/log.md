# Log

## 2026-11-13 · 최신 A 전체 추격 완성 재개
- 사용자: 최신 dev 기준 시작부터 탈출까지 5m 거구 추격자를 완성. 추격자 전용 지형/통로/재등장 및 마지막 달리기 허용. 플레이 체감은 사용자 확인.
- Unity 정상 종료 후 원래 fix/rehearsal-entry-handoffs의 로컬 변경을 stash add9a3d47eedebc03f241f18ca8d03ceca247d5e에 보존했다. 복원하거나 삭제하지 않는다.
- feat/exit-route/pursuer 전환, origin/dev 4bf66bab fast-forward, Assets/_ThirdParty c46b6dc9 반영. 추격자 구 WIP stash 1b1eda6f의 독립 코드/에셋/Task만 복원. 공용 코드는 최신 구조에 맞춰 재연결한다.
- 작업 시작 doctor 1회: 엔진/도구 연결 가능, task-ids 중복 FAIL(077/078/079/082/083/086/094/097/102/115), CLI 버전 문서/skill link/worktree WARN. 게임 구현과 독립된 기존 하네스 상태이며 완료 검사에 재확인.
- 기존 log/todo는 History/20261104-*에 보존. 이전 PASS는 최신 맵 검증을 뜻하지 않는다.
- 작업 중: 최신 A 전체 기물/경로 실측, GameSettings 추격 조정값, 공용 포획 상태 연결, 전 구간 추격 전용 구조 제작.

## 2026-11-13 · 갑작스러운 PC 재부팅 후 점검
- Windows System: 02:39:37 Kernel-Power 41 / 02:40:23 EventLog 6008. BugcheckCode 0. 이 기록만으로 전원/하드웨어/드라이버 원인을 특정하지 않는다.
- Unity 복구 대화상자에서 Yes로 Assets/_Recovery/0 (2).unity를 별도 보존. 저장된 A/테스트 씬 SHA256이 2C32ABE7AE9361C89461C4BABBC626E994CE62EDC1D7BAC0D14A4EB49636DC96으로 일치. 최신 추격자 prefab도 저장돼 있다.
- origin/dev 재fetch: 4bf66bab로 현재 브랜치와 동일(0/0), Git connectivity fsck 정상. 기존 사용자 stash와 추격자 브랜치 유지.
- 씬 로드 검사: A/테스트 각각 6117 objects, missing script/prefab 0, 추격자 경로74, 필수 참조/앵커 누락0. 프리팹도 missing script0/경로74. 복구 백업은5872 objects이며 추격자 모듈이 없어 저장된 최신 씬으로 대체하지 않았다.
- Library/BuildProfileContext.asset 로드 오류 후 Unity가513byte로 재생성. 비어 있던 StateCache의 LayerSettings/SceneView JSON 각1개는 Unity가 자동 제거. 추가 광원 shadow atlas 축소 경고는 별도 렌더 설정 경고다. 소스 씬 손상 증거 없음. 캐시 재생성 후 재기동/Play 확인은 진행 중.
- 전체 A를 복제한 추격자 테스트 씬도 기존 임포트 워커 메모리 해제/복원 경로에 포함했다.

## 복구 완료와 추가 구현
- 전부 NUL로 손상된 설정 에셋1054byte와 신규 프리팹 meta154byte를 발견. 손상본은 .harness/local/task100-complete/power-loss-backup에 보관하고, 설정은HEAD 원본/프리팹 meta는 씬에 기록된 원래GUID 3a6c3a3521e07534cb4132329b764b0b로 복구.
- 8,979 작업 파일의 전부NUL 검사 및8,317 텍스트의 내장NUL 검사 완료. 복구 후 새 Unity에서 A/테스트/프리팹 참조 누락0. EditMode EscapePursuerComplete 9/9 통과. 실제 Play 정상 진입. PC 종료 원인은 특정하지 않음.
- 사용자 요청으로 컴퓨터 사용 기능 종료. 이후 코드/Unity CLI만 사용.
- 독립 실행의 SurvivalPlace가 Performance여서 접촉 포획이 거절되는 문제를 발견. 공용 InjuryRevival.ServerApplyStep(Escape) 연결과 종료 복원으로 수정.
- 복도 자연 추격→포획→사지절단→대포 문까지 운반→실제 벽/바닥 궤적검사 후 투척→리타이어→재시작 확인. F6 8개 동작 동안 실제 플레이어 조작 유지 확인. 두 번째 외줄의 고정예고/2.1m 회피/7.75m 팔/재포획·투척, 후반 합류 도약, 출구 앞 경고까지 제어된 호스트 재현 통과. 전 구간 사람 완주/다인 테스트와 구별.
- 전용74점 경로 제작. 새 바닥까지 포함한 정밀 검사에서 블록식 경사 연결 간섭을 발견해 연속 메시 발판으로 교체, 급격한 꺾임과 지지대를 조정. 1,813 몸통 표본에서 동적으로 여는 셔터/전용차단막 외 지형 간섭0. 초기3개 NavMesh 경로 Complete. 주요18곳 공격 시야 검사 완료(마지막 경로 보정 후 재검사 예정).
- 추가 요청: 첫 등장 복도 끝으로 이동, 처음6m 보행 이후 위협적인 접근 달리기. 시작홀 원래벽 복원, 복도 끝막이 대신 파괴개구부 적용, 호스트 진입 감지/등장시각 동기화 추가. 새 위치 NavMesh/운영A/테스트 복사본 저장. 이 변경의 실제 Play 재검사는 예정.
- 이 변경 전에 Unity 정상종료 후 origin/dev 재fetch/ff 확인: 4bf66bab,0/0 유지. 커밋/push 없음.
- 최신 요청: 마지막 봉→추격자 점화→전기톱 획득 시 전방도약→8부위 절단 보스전/회피 대시/피/감속. 실제 기물 위치와 화염/발사체 API를 조사했고 구현 중.

## 2026-11-13 마지막 배선 작업장 구현
- 사용자 최종 수정: 문은 기존 필수 배선 절단으로만 개방. 추격자 격파는 배선 작업을 편하게 하는 선택이며 문 개방 조건에 연결하지 않았다.
- EscapePursuerBoss: 마지막 두 봉 사용→실제 정비로62번 점화 레버→추격→71번 합류 뒤 발사대 앞 (384,0,78) 도약·착지→빠른 접근/회피/포획. 8부위 고유 절단, 피·낙하 조각, 부위별7% 감속, 피격 회복, 사망·재시작과 서버 상태 동기화.
- 배선 발사대4개에 전투 연결. A의 전투 시작 뒤 카메라 중앙으로 상하 조준하며 서버가 소유자·방향·리비전을 검증한다. 기존 케이블 절단·문 규칙과 일반 씬 수평 발사는 유지한다.
- 마지막 불꽃벽은 후미 참가자 뒤 구간부터 원래 서버 시각+시작 구간 번호로 점화. 빠른 개발 진입에서도 음수 시각이 되지 않는다. X360 방화 프레임 이후 배선 작업장은 불꽃벽 대신 추격자가 압박한다.
- 실제 실행: 복도 진입 전 started=-1, 끝벽 등장 후6m 보행, 5.2m/s 달리기 접근, 포획 확인(corridor-probe.log). 초기 NavMesh3경로 PathComplete. 전용 정비로1813캡슐 샘플의 예외는 실제 열리는 첫 셔터/라이트커튼뿐; 바닥 공백0, 주요18개 공격 시야 막힘0, 플레이어 경로와 새 갤러리 겹침0.
- 도약69번 출발은 슈트와 간섭해71번 합류 이후로 제한. 허용 출발71~73의180샘플 충돌0. 발과 다리 판정 분리, 장식 절단 조각이 실제 톱날을 막지 않도록 수정.
- staged 호스트 검사(finale-probe.log): 점화→실제 발사대 획득→도약/착지→2.2초12.79m 접근→예고된 회피→실제 톱날 StepProjectiles로8개 절단(감속0.93~0.44)→사망해도 DoorOpen=false→실제 케이블 교차로 문 개방→재시작 복원 PASS. 정밀 절단 단계는 적 이동을 멈춘 통제 검사이며 사람의 전투 완주가 아니다.
- 추가 실제 압박 검사(work-pressure.log): 들고 있는 발사대의 상자 콜라이더가 포획을 차단하던 안전지대를 제거. 수정 후 가만히 있는 발사대 운반자에게 Warning→Grab→Throw→Retired PASS. 고정 지형은 여전히 손을 막는다.
- 실제 게임 카메라로 5m 모델·붉은 손발·기존 배선/출구 UI 시각 확인(boss-game.png). F6 기존8종 개별 동작 유지. 전투 전 구간 난이도·발소리 감상·실제2~4인 완주는 사람이 확인할 범위.
- 저장 A/test 각각5729개 객체, MissingScript/Prefab0, 경로74/부위8/발사대4/필수 참조 정상(final-reference.txt).
- 전원 종료 NUL 손상 함정을 Harness/Engine/Unity/Pitfalls/Initialization/power-loss-nul-assets.md에 기록. verify-knowledge OK(19 folders).

- 2026-11-13 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=PASS
- 제출 전 Unity graceful 종료 확인 후 origin/dev fetch/fast-forward: 4bf66bab→5c7754ae(PR356), 충돌0/현재 분기0·0. 최신 공용 코드로 check-work -CommonRegression: 안전성·문서/규칙·컴파일·공용 설정·686/686 회귀 PASS. 기존 문서 길이 경고67개, hard0.
- 탈출로 EditMode 추가 검사: CurtainCall.EscapeRoute.Tests 248/248 PASS, 실패/skip0. 새 부위 교차·8절단 감속·NaN 등 비정상 입력 방어·화염 시작 구간·장식 조각 비차폐 테스트 포함. 최초 복합 testName 필터는0개 선택(NO_PROJECT_TESTS)이어서 네임스페이스 필터로 재실행했다. 근거: .harness/local/task100-complete/escape-tests-248.json, common-regression-686.json.
- Confluence 작업노트33161228의 최신 구현 섹션 갱신(snapshot d1:853). 예전 내용은 11월4일 이력으로 표시. 복도 끝/전 구간 경로/점화/선택형 전투/배선 전용 출구/실제 검사·사람 확인 구분을 반영했다.
- 남은 확인은 사용자 전 구간 플레이와 실제2~4인 클라이언트 관찰이다. 커밋·push·PR은 요청되지 않아 하지 않았다.

- 최종 정리: Unity 종료 확인 후 자동 생성 TMP 아틀라스 변동만 로컬 백업하고 정리. 최종 fetch/merge는 이미 최신(5c7754ae, HEAD...origin/dev 0/0), diff --check 정상. 브랜치 feat/exit-route/pursuer, 에디터 종료 상태. 기존 보존 stash는 그대로 유지.

## 2026-11-13 제한시간 제거
- 사용자 확정: 탈출로 전 구간을 구간별 추격/우회/재등장으로 압박하고, 플레이어는 빠르게 빠져나간다. 카운트다운과 제한시간 기능 제거.
- 기존 EscapeRouteTimer의 GUID/시작 트리거는 보존하고 내부 연출 시계로 축소. 480초 설정·Remaining/Expired API·시간 초과 Retired 코드를 제거했다. 서버 시작 시각·세대/리비전·재시작 토큰은 유지하며 네트워크는 EncounterClock v2(20bytes)로 구분한다.
- 저장 A와 전체 A 테스트 씬의 EscapeTimerCanvas/EscapeCountdownDisplay를 비활성화. 구형 씬의 display 참조도 항상 숨김 처리한다. 대기실 공용 타이머/프리팹은 수정하지 않았다.
- 로그 연결도 정리: ESCAPE_STARTED durationMs=0/timeLimitEnabled=false, 구간 remainingMs=null/elapsedMs 기록, ESCAPE_TIMEOUT 미발행, 종료 timeout=false. 케이블 문과 실제 위험물 판정은 유지.
- 실제 호스트 검사 no-deadline-play.log: UI 비표시·공유 시작 시각·복도 벽 파괴·6m 이상 접근·카운트다운 없는 재시작 PASS. 게임 화면에서도 좌상단 타이머 없음 확인. 480초 이후는 경계값 검사와 만료 경로 제거로 검증했으며 실제8분 대기는 하지 않았다.
- doctor: 기존 중복 Task ID10개로 FAIL, Unity 버전/외부 에셋/브랜치는 정상. 수정 중간의 컴파일 실패로 시작 중 멈춘 이번 작업의 에디터 프로세스만 정리한 뒤 최종 소스로 다시 열었고 컴파일 오류 없이 연결했다. 사용자 미저장 씬은 없었다.

- 2026-11-13 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=PASS

## 2026-11-13 시작 우측 벽·숨은 경로·표면 정리
- 첨부 하이어라키 첫 타설 콘크리트 벽과 대기실 진입(+X)의 오른쪽(-Z)을 대조해 중심(15,1,-1)을 확정. 모듈(15,0,-1),Y180°로 이동했다. 첫 대포 끝막이와 잘못 선택했던(25,7,23) 벽은 복원. 같은 이름만 보고 위치를 선택하지 않는다.
- 74개 연속 경로에 불투명 지붕/외장과149개 가동 패널을 배치. 가까운 추격자-플레이어 공격선의 앞/뒤 면을 모두 검사해 열고, 출입·머리핀 셔터는 몸보다 먼저 연다. 변하지 않는 패널 Transform은 매프레임 쓰지 않는다. F6에서는 가림 연출 중지.
- 플레이어 합류46~48/70~71 구간은 통로를 덮지 않고 출입 셔터 뒤에서 직접 합류. 복귀 대포 실제 탄도와 간섭한18번 지붕을5.3m로 낮췄다. 4대포 기본 탄도 capsule 검사에서 새 외장 충돌0(cannon-casing.txt).
- 벽·바닥의 동일/근접 평면 겹침을 EscapeCoplanarPolish로 정리:58개 메시,344개 원본 삼각형의 공유 렌더 영역만 절단. 원본 메시/충돌 형상 보존. 파생 파일은 GUID를 유지해 비공개 Task100/SurfaceReview로 이동. 다른 LOD 단계의 같은 표면은 제외했다.
- 실제 호스트 staged 검사:18개 압박점에서 패널 개방 뒤 팔 시야 막힘0, 재시작 시OpenBay=-1(concealment-play.log). 초기 우측 벽 파괴→6m 보행→달리기(run weight0.68)→EscapeCaptured 확인(right-wall-play.log). 이는 전 구간 실제 다인 완주가 아니다.
- 닫힌 외장 조사:1671개 몸통 표본의 접촉은 사전 개방하는 출입/머리핀 패널뿐. 가림 밖으로 나오는46~48/70번 입구를 제외한 통로 표본은 기존 구조/외장으로 차폐. 직선 플레이어 앵커 보간의16/17번 교차는 실제 대포 탄도로 따로 검증했다.
- 지식 escape-pursuer-layout.md 추가, verify-knowledge OK. 제한시간 제거 회귀는 공용686/686,탈출로250/250 PASS(새 패널 테스트 전).

## 2026-11-13 두 번째 전원 종료 복구
- Windows13:00 Kernel-Power41/EventLog6008 확인. Unity 종료 프로세스 없음, 최신 Unity 자체 크래시 덤프 없음. 이 기록만으로 Unity 코드/하드웨어 원인을 단정하지 않는다.
- 미커밋/미추적 작업572개를 .harness/local/task100-complete/power-loss-2-20261113-130631/preserved-work.zip에 보존. 빈 파일·전체0·텍스트NUL 손상0, 양 저장소 git fsck 연결성 정상. 이전 _Recovery 씬은 더 오래된 파일이라 덮어쓰지 않음.
- 재개 전 에디터 종료 확인→origin/dev fetch/ff:이미 최신5c7754ae. 기존 stash·미커밋 변경 보존. 이후 에디터/필수 참조/최종 검사 결과를 아래에 기록한다.

- 재시작을 막던 Recovering Scene Backups 창은 컴퓨터 사용으로 Yes(별도 보존)만 실행. _Recovery/0 (3).unity 생성 확인 후 컴퓨터 사용 JS 세션을 종료했다. 저장 A/test에는 덮어쓰지 않음.

- 2026-11-13 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-13 제출: [PR #357](https://github.com/gaboja-studio/POC_CURTAINCALL/pull/357), feat/exit-route/pursuer → dev. 구현 커밋8114eed9, private 에셋0f7585c8 게시 완료. GitHub 연결 도구의 PR 생성403은 로그인된 gh CLI로 처리했다. 병합하지 않았으며 지정 팀원 검토 대기다.
- 테스트 씬을 1인/Play 중지/dirty0으로 열어 두었다. F6 개별 동작과 일반 추격 모두 사용자 직접 확인 가능. [최종 검사 및 남은 사람 확인](verification.md).

- 복구 후 A/test 각각5879개 오브젝트, 누락 스크립트/프리팹0,74경로/8절단부위/4발사대/149패널 확인. 탈출로 EditMode256/256 PASS(실패·생략0). 최종 check-work는 컴파일·공용 설정·문서/규칙 PASS이며 자동 테스트는 중복 실행하지 않았다.
- 이동한 파괴벽의 Left wall/Floor/Side -1은 이전 위치 기둥에 맞춘 절단을 쓰지 않도록 원래 Cube 메시로 복구하고 A/test를 저장했다. 원본 에셋은 보존했다.
- 저장·제출 요청 후 Unity 정상 종료→origin/dev fetch/ff:5c7754ae, 이미 최신. private main의 추가분은 README뿐이며 게임 에셋 차이0. 신규 표면 에셋118파일을 0f7585c8로 저장하고 private main에 게시했다. 공개 저장소에는 gitlink만 반영한다.


## 2026-11-13 플레이 피드백: 콘크리트 뒤 경로·지속 추격

- 사용자6개 개선 요청에 따라 기존 feat/exit-route/pursuer에서 작업. 실수로 만든 feat/exit-route/pursuer-improvements는 기존 브랜치로 복귀 후 삭제했다. 작업 시작 전 Unity 종료·dev9a5baaa7 반영. 이번 개선의 커밋/push는 요청되지 않았다.
- 갈색 갤러리/구형 가림 외장은 비활성 보존. 74개 경로를 기존 콘크리트 뒤로 이동하고21개 서버 시각 기반 파괴구와 동일 재질 주변 벽을 생성했다. 원본 벽·에셋을 보존했다.
- 노출6.6/벽 뒤13m/s, 달리기 상태 유지·NavMesh 자동 감속 해제. 복도/대포/외줄 예고0.45/1.1/0.85초, 손 뻗기0.22초/회수0.3초/회복0.35초. 발소리 거리 감쇠·강도 조정. GameSettings 사용.
- 팀 출발5초 후 진행 팀 추격, 진행도+최근 공격6초 기억으로 타깃 재선정. 보스는 발사대 사용자를 우선 고려하며 막히면 측면 진행을 검사한다. 고정 공격선·호스트 접촉·보스8절단·케이블 출구 규칙 유지.
- 몸통 sweep/공격19선/플레이어74표본/4대포 기본 탄도 검사에서 새 막힘0. 마지막 도약 X332가 반출기와 충돌하는 것을 발견해 X350으로 변경, 실제 곡선121표본 충돌0. 시야 표본의 노출0은 모든 카메라의 비노출 보장이 아니다.
- 실제 호스트1인 구간 재현: 첫 등장→달리기→경고/손 접촉→EscapeCaptured·운반·리타이어, 벽 파괴1→재시작0. 전체 EditMode258/258 및 정리 후 추격자15/15 통과. 전 구간2~4인 실플레이는 미확인.
- 벽 절단 도중 새 콜라이더 bounds는 Physics.SyncTransforms 후 사용해야 한다. 표본마다 반복 절단하던 중간 산출물은 에디터에서 재생성해 정리했고 최종 생성 루트589 Transform이다. 마지막 점프는 몸통을 포함한 곡선 검사 필요. record-knowledge로 기존 배치 Facts 갱신, verify-knowledge OK.
- 제출 전 새 dev6커밋 및 가이드 hotfix를 발견했다. Unity 종료 후 WIP stash 보존→devc0c11212 fast-forward. 탈출로 루트 자식 목록1곳 충돌을 확인했고 사용자가 두 작업 보존 해결을 승인했다. 엔딩/체크포인트를 남기고 추격자1개를 연결해 해결. private16982ba2 반영. 최신 A/test 저장 및 경로/공격/탄도/도약 재검사 동일 결과.
- doctor 기존 중복 Task ID10개 실패, CLI/Pipeline 차이·기존 다른 worktree 경고. 초기 종료 요청은 재임포트 중 timeout이었고 재시도에서 정상 종료했다. 관련 stash·폰트 자동 변경을 버리지 않았다.

- 최신 dev 통합 후 전체 탈출로 EditMode258/258 재통과, 실패/생략0. 갱신 테스트 씬 실제 호스트22초206표본에서 run1·Warning/Strike/Grab·EscapeCaptured/운반 확인. 현재 콘솔 오류0. Review/improvement-verification.json에 범위와 수치 보존.

- 2026-11-13 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 최종 check-work 안전성/문서/컴파일/공용 설정 PASS. Base 메타데이터에 설명을 함께 적어 첫 실행이 중단된 것은 Base=dev로 바로잡았다. 자동 테스트는 별도258개 결과 사용. 검사 후 Unity 정상 종료→origin/dev 재fetch/ff, c0c11212 이미 최신·HEAD...origin/dev 0/0 확인. 커밋/push 없음.

## 2026-11-13 외부 에셋 적용·위협 행동 확장 진행
- doctor는 기존 중복 Task ID10개로 FAIL, CLI/Pipeline 및 기존 worktree 경고. 에셋 작업에 영향 없음. Unity 종료 후 origin/dev08600e05 fast-forward, 기존 미커밋 작업 보존.
- 다운로드된5패키지 경로를 _ThirdParty로 재매핑하고 GUID 중복을 보존해 임포트. Brute52/Mascot117/HumanBasic1/Boxer287/MonsterSounds1258항목. HumanBasic646항목 재사용. 데모 입력/이동/에디터 자동 설정 코드 및 데모 씬 제외, 원본 다운로드는 보존.
- Mascot3 원본은 Generic이며 Body/눈/치아3개의 skinned mesh. 복사본을 Humanoid로 전환하되 자동 매핑이 제어용 리그를 고를 수 있어 실제 pelvis/UE 변형 뼈대에 명시 매핑. 유효한 Human Avatar 확인. 원본 외부 FBX는 수정하지 않음.
- 사용자 추가: 돌진 준비/노림 신호, 봉·목마·비행 중 포획, 파편 투척 사지 피해, 단계적 사지 절단과 몸통 운반, 다른 참가자에게 몸통 투척, 무작위 파괴구 재등장. GameSettings/호스트 판정/기존 공통 절단과 점유 해제 API를 사용하여 구현 중.

- 사용자 최신 지시: 저장/제출을 철회했고 실제 commit/push/PR은 실행하지 않았다. 외형도 적용 보류. 기존 회색 외형·Legacy 동작·8부위 히트박스를 A/test/프리팹에 복원했고 Mascot 객체·파생 에셋은 비활성 보관했다.
- 새 카메라 컴포넌트 없이 기존 CameraSpectator/CameraViewDirector를 수정했다. 포획 동안 추격자의 이동 루트+2.5m를 중심으로1.5배 넓게 관전한다. 손/몸 스킨/포획 플레이어의 애니메이션 상하 이동은 따라가지 않는다. 실제 손 놓기 시 투척 대상 생존자를 우선 관전하고, 그 대상이 없거나 죽으면 기존 생존자 선택을 사용한다. 공용 카메라 수정 범위는 두 기존 파일과 회귀 테스트이며 다른 시점의 벽 충돌은 보존한다.
- 투척은 고정 길이 팔의 뒤로 젖히기→빠른 오버핸드 스윙→후속 동작으로 교체했다. Throw 동안 신장 팔/추가 손을 끄고 실제 손 위치에서 몸통을 놓는다. 낭떠러지 고정 Release 마커까지 팔을 뻗지 않으며 궤적도 낮췄다.
- 최초 실제32초 프레임 기록1920개에서 포획 카메라가 플레이어/손을 추적하고 Throw 진입 시 한 프레임6.10m 이동한 문제를 확인했다. 프레임 시간은 시작 재배치 한 번 외에는60fps 수준이었다. 새 Humanoid 외형은 사용 중지하여 그 동작 혼합은 현재 실행하지 않는다.
- 카메라 새 회귀의 초기 실패2회는 fixture가 이전 충돌 거리 초기화를 생략한 것과 public 전용 테스트 호출기로 internal 메서드를 찾은 것이었다. 실제 대상 변경 경로와 같은 초기화로 고쳐 재검사한다.
- 최종 dev 동기화: Unity 정상 종료 후 origin/dev2211c3c5로21커밋 fast-forward. stash abb00087350bf87c86536642f76c368fc7c256af를 적용해 모든 변경 복원했고 안전 사본은 보존했다. 씬/설정 자동 병합, CameraViewDirector 코드 충돌은 dev의 관전 대상 운반 기물 제외+이번 추격자 캡슐 제외를 함께 유지했다. private8ece127e60b95c2bbef0c773ee4bf62e56b432e2로 갱신, 신규 임포트 보존. staged 변경은 원래처럼 전부 해제했고 commit/push하지 않았다.
- 최종 카메라/투척 호스트 검사: 피포획자 상하30표본 카메라 변위0, 시점거리7.758m, Throw 신장 팔 비활성, 팔길이1.6043~1.6054m. 실제 놓기 시 지정 대상888 관전→사망 시777 대체. 몸통으로777 사지1부위 피해/벽 차단/포획 리타이어도 재통과.
- dev2211c3c5 통합 후 탈출로 EditMode274/274, 카메라 PlayMode1/1 통과(실패·생략0). A 씬 missing script0, 비활성 Mascot/기존8부위 및 dev 엔딩/체크포인트 보존 확인. 테스트 씬은 최신 A 복사본으로 열어 정지·수정 표시 없음 상태로 둠.
- 최종 check-work는 Task100과 기존 Task114가 같은 브랜치를 지정해 현재 Task를 고르지 못하고 중단했다. 다른 Task를 임의 변경하지 않고 verify-scope -Base origin/dev, verify-fast, verify-unity -Compile을 개별 실행: 안전성/문서/컴파일/공용 설정 모두 PASS. 통합 검사의 PASS로 기록하지 않는다. 기존 문서 길이 경고와 private 미커밋 알림 유지. 콘솔 이력의 Vivox403은 음성 서비스 오류이며 현재 compilationFailed=false, consoleErrors=0. commit/push/PR 없음.

## 2026-11-13 저장·제출 재요청
- 사용자가 저장·제출을 다시 요청했다. 외형 적용 보류는 유지하고 관련 변경만 기존 브랜치에서 저장한다. 기존 Graffiti 폰트 변경은 제외·보존한다.
- Unity 정상 종료 후 origin/dev371c0481의7커밋을 fast-forward하고 stash374b15423b5de796e77b005dde0dc2d39c4221af 복원. dev의 공연 도착자 관전 기능은 자동 병합. EscapeRouteSettings 충돌은 새 한글 툴팁과 이번 추격자 조정값·신규 설정을 함께 보존했다. 사용하지 않는 과거 달리기/정지 간격 설정은 되살리지 않았다.
- private의 기존 main을2ca6d78b까지 fast-forward하고 관련 신규3251파일을447a5b13에 커밋했다. 기존 회색 외형이 활성이고 Mascot 파생본은 비활성 보관이다.
- 제출 기준371c0481에서 탈출로 EditMode274/274, 포획 카메라 PlayMode1/1 재통과(실패·생략0). 첫 EditMode 호출은 어셈블리 이름을 testName에 넣어0건이었으며 성공으로 세지 않고 실제 namespace로 재실행했다.
- new-pr -DryRun도 기존 Task100/114 동일 브랜치 때문에 중단. Task 문서를 임의 수정하지 않고 동일 PR 양식과 명시 경로 stage로 직접 저장·제출한다. 통합 검사 중단 내역을 PR에 공개한다.
- 제출 검사: 안전성/문서/컴파일/공용 설정 PASS. 테스트는 별도274+1개 결과를 사용하며0건 호출은 제외했다. Unity 테스트 씬 정지/dirty0 확인.
- 사용자 추가 지시: 필요한 에셋만 업로드. 진행 중이던2.3GB LFS 전송을 중단했고 원격 main에447a5b13이 반영되지 않았음을 확인했다. 로컬 파일을 지우지 않고 미공개 커밋의 추적 목록을 수정했다. 일부 전송된 LFS 객체는 참조되지 않으며 이전 전체 패키지 커밋은 최종 main의 조상이 아니다.
- Unity 의존성 조회 결과 신규 실제 참조는 돌진/투척 WAV2개뿐이다. A/test/프리팹에서 미사용 Mascot·절단 단면27객체를 제거하고 제거 전 파일은 .harness/local/pursuer-assets/pre-minimal-upload에 보관. 음원2개와 폴더/파일meta9파일(502820바이트)만 private a06b1871에 저장했고 최신 private main과 병합한 a31de665를 push했다. 나머지 패키지/실험 에셋은 로컬 미추적 보존이다.
- 제출 대기 중 devb96a22fd 추가9커밋을 Unity 종료 상태에서 병합했다. 탈출 엔딩 낮/밤 설정과 추격자 설정은 자동 병합됐다. 기존 공개 미제출 커밋도 최소 에셋 참조로 수정해 전체 패키지 커밋을 가리키는 gitlink가 제출 이력에 남지 않게 했다.
- 최소 에셋·devb96a22fd 기준 최종 검사: EditMode274/274, 카메라 PlayMode1/1, 컴파일/공용 설정/안전성/문서/원격 private 포인터 검사 PASS. A6545객체에서 missing script0, 기존8부위 활성, 엔딩/체크포인트 보존 확인. 최신 테스트 씬 dirty0/Play 정지로 열어 둠. 로컬 미사용 에셋과 폰트 자동 변경은 제출에서 제외한다.
