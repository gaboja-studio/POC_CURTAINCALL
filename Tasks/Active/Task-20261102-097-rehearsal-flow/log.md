# 작업 기록

이전 원문 기록: [History](History/002-before-20261104-log.md).

## 2026-11-04 MPPM 2인 트리거 그네 시작 도착 결함 조사
- 사용자 보고: 2-3 시작부터 P2가 도착점에 있고 뒤로 돌아갈 수 없어 P1의 협동 진행이 막힘. 이번 범위는 확인이며 기능 수정 없음.
- 실제 상태: Pipeline eval에서 playing=false, paused=false, WaitingRoomBase 씬, WaitingRoomRehearsal 인스턴스 0. 현재/이전 Editor.log에서 이번 2인 재현의 플레이어 좌표·회차별 로그를 확보하지 못함. 실접속 반복 재현은 수행하지 않음.
- 코드 결함: Update는 HostTick→EnsureCourse→LocalTick 순서. AdvanceStage가 arrived를 비우지만, 새 코스 생성 다음 프레임 HostJudgeStage는 원격 소유자 배치/현재 회차 위치 수신 확인 없이 IsFinished로 arrived에 추가한다. 기록은 다음 회차까지 유지된다. 일반 TightropeRun의 준비 판정과 달리 이 리허설 도착 판정에는 준비 경계가 없다.
- 실제 설정으로 Unity 메모리 계산: 2-2 Finish=38, 2-3 Finish=32, 코스 원점 z=-(Finish-1)/2. 이전 거리 38/38.25/38.5는 새 거리 35/35.25/35.5가 되어 모두 새 도착 허용 범위 [32,35.5] 안이다. 1줄→2줄 변경 뒤 이전 중앙 x=0도 새 허용 반폭 3m 안이며 높이는 동일하다. 원격 좌표 전송이 늦으면 보고한 오등록이 가능한 것을 확인했다. 이는 3개 좌표의 계산 검증이며 실제 MPPM 3회 재현을 뜻하지 않는다.
- 두 번째 증상: TrackPersonalProgress는 arrived에 있으면 현재 줄의 FinishDistance를 arrivalPose로 저장하고, IsFinished를 벗어나면 Relocate한다. 오등록된 P2는 출발대로 배치되어도 도착선으로 다시 이동하며 뒤로 갈 때 반복 복귀한다. P2가 트리거 역할을 할 수 없어 협동 진행이 막히는 보고와 일치한다.
- 수정 방향: 회차별 배치 준비/해당 위치 수신을 확인하기 전에는 도착 등록 금지. 배치 확인 응답만 먼저 받고 오래된 NetworkTransform을 판정하지 않도록 설계할 것. 적용 뒤 2-2→2-3 실제 2인 전환·P2 트리거 잡기·P1 통과·정상 도착자 제한 회귀 필요.
- 환경: doctor 1회 FAIL(기존 ThirdParty dirty, Task ID 중복, 에디터 설치 탐색 실패), gh 미로그인 WARN. Pipeline은 샌드박스 밖 localhost 접근으로 ready와 읽기 eval 성공. 에디터가 열려 있어 종료/브랜치 병합 안 함. 최초 fetch는 .git/FETCH_HEAD 쓰기 제한으로 실패했으며 동기화 미완료 상태의 현재 작업본을 조사했다. 기존 ThirdParty 변경 보존, 커밋/push 없음.

- 2026-11-04 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=SKIPPED(Unity 변경 없음); 테스트=SKIPPED(자동 테스트 선택 안 함)


## 2026-11-04 후속 요청: 기획 설명 및 결함 수정
- Google Drive로 최종 기획서 원문 재조회: 모든 참가자 새 소구간 출발대 배치→한 명이 위쪽 트리거를 잡음→아래쪽 줄 연결→동료가 통과. 도착자만 도착대 이탈 제한. 최종 승인 규칙의 전원 도착/과반 동의(2인 2표)는 유지한다.
- Unity playing=false/dirtyScenes=[] 확인 후 정상 종료 요청. CLI는 30초 timeout을 반환했지만 이후 UnityLockfile 제거·Package Manager 종료 로그로 종료를 확인했다. origin/dev fetch 성공, 이미 HEAD에 포함되어 병합 변경 없음. 기존 미커밋 문서·ThirdParty 변경 보존. doctor 기존 환경 FAIL은 이전 조사와 같으며 실제 Unity 실행 파일은 존재한다.
- WaitingRoomRehearsal: 회차별 placed 집합 및 Placed 요청 추가. 호스트는 현재 회차·유효한 출발 좌표·해당 소유자의 실제 수신 좌표가 일치할 때만 승인. 승인 상태는 기존 상태 메시지에 함께 공유한다. 클라이언트는 승인 전 이동을 잠그고 요청을 재전송하며, 코스 변경/종료 때 잠금을 해제한다. 도착 판정은 승인된 참가자만 수행한다. 출발 슬롯·기물·도착자 제한·투표 기준 변경 없음.
- 합성 원격 회귀 W6_Rehearsal_TriggerStageWaitsForRemotePlacement 추가: 2-2 도착선이 2-3 도착 범위에 겹치는 조건, 위치 수신 전 요청 거절, 옛 회차/NaN 거절, 현재 배치 승인 후 정상 도착. 기존 VoteAndPersonalCheckpoint와 함께 PlayMode 실행 요청. 실제 다인 입력 PASS로 취급하지 않는다.

- 2026-11-04 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=SKIPPED(-SkipUnity); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 최종 검증: Unity 일반 에디터 컴파일 completed/failed=false/errors=[] 및 실제 PlayMode 실행 확인. TriggerStageWaitsForRemotePlacement 1/1 PASS(76.43s), VoteAndPersonalCheckpoint 1/1 PASS(81.39s). 결과는 .harness/local/rehearsal/placement-regression-passed.json 및 vote-regression-passed.json. 실제 MPPM 입력 재검증은 미실시.
- 검사 시도 구분: 첫 -nographics 배치 2건은 커튼 닫힘 확인 실패로 본 검증 전 FAIL. GUI 최초 새 테스트는 합성 NetworkTransform 기본 좌표 보간 때문에 재현 조건 실패, 합성 원격의 보간을 비활성화해 수신 좌표 직접 제어로 보완. 이후 1회는 Vivox Editor HTTP403으로 SetUp 실패(placement-vivox-blocked.json), 동일 테스트 재실행은 PASS. Pipeline 정규식 묶음 필터는 0건이어서 정확한 이름별로 실행했으며 0건 결과를 PASS에 포함하지 않음.
- check-work -SkipUnity: 안전성/문서 PASS, 컴파일 생략으로 exit2. 위 별도 에디터 컴파일·PlayMode 2건 PASS와 구분한다. 최종 git diff --check 통과. 테스트로 생성된 폰트 아틀라스 3개는 작업 전 clean을 확인한 파일이며 .harness/local/rehearsal/generated-assets에 사본 보관 후 HEAD의 원래 바이트로 복원. 기존 ThirdParty 변경 보존, 커밋/push 없음.

- 2026-11-04 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)


## 2026-11-04 후속: 스케줄표·패널·카메라
- 사용자 확인: Google Docs 최종 기획서 11월 3일 17:23 수정본이 최신 패널 기준. 첨부 밤 패널 화면과 대조. 2일차 마지막 방향 안내는 명시적으로 보류.
- 에디터 clean/플레이 종료 확인 후 정상 종료, origin/dev fetch 성공(HEAD에 이미 포함). 기존 리허설 배치 수정·ThirdParty 변경 보존.
- 첫날 확정 전 입장 시 로컬 설명 안내 추가. 집결뿐 아니라 시간 만료/추격 종료를 통한 미확정 공연 진입도 차단. 런에 둘째 공연 적용 실패 시 스케줄표만 확정되던 순서 수정.
- 스케줄표는 기존 구성된 뷰/편집 버튼을 판 위 월드 캔버스 사본에서 재사용. 원본 어댑터는 공용 UI 수명을 유지하며 화면 팝업은 숨김. 비편집 상태의 판은 입력을 받지 않음. 실제 편집 상태로 다음 날/데모 전환의 모달 판정을 수정. 같은 모델의 반복 렌더/글리프 갱신 제거.
- 패널은 설명 좌측·퀘스트 우측 유지, 실측 높이+8 UI 단위로 쌓고 둘 이상일 때 마지막 추가 항목만 느낌표. 상단 제목만 연한 주황. 설명의 무조건 느낌표와 삭제된 폰트 소유자 재사용도 수정.
- 카메라는 활성 충돌체로 비동기 NavMesh를 만들고 통행 가능한 바닥 경로의 눈높이에서 왕복한다. 구간별 SphereCast 안전 검사, 경로 없으면 관통하는 직선 연출 금지, 이동 길이에 맞춘 시간/설명 표시 유지, 취소 시 NavMesh·브레인 블렌드 정리.
- 검사 중 Task log 길이 hard 초과를 발견해 이전 log/plan 원문을 History/002·003에 보존. verify-fast 재검사 PASS(기존 WARN 남음). 최초 check-work는 문서 길이로 FAIL이며 별도 최종 컴파일/테스트와 구분.
- EditMode RestTimeRulesTests 4/4 PASS(확정 전 집결·시간 만료 차단 포함). W7 첫 실행은 씬 해제 중 SetParent 오류를 발견해 원본 이동 대신 뷰 사본과 파괴 방식으로 수정. 다음 실행은 Dev 시작 대기 초과 및 검사 중 외부 eval의 Pipeline 시간 초과 로그로 실패했다. 오류 로그를 숨기지 않고 개별 재실행한다. 결과 JSON은 .harness/local/rehearsal/followup/에 보존.
- 최종 월드 스케줄표 W7 PASS(1/1, 53.88s): 카드 두 번 변경, 확정 버튼 실제 EventSystem 클릭, 첫날 확정, 입구 차단 안내, 닫은 뒤 입력 해제. 판보다 큰 캔버스 때문에 확정 버튼이 화면 밖에 있던 결함을 캡처로 찾아 75% 안쪽 여백으로 수정했다. 확정 전 둘째 칸의 잘못된 확정 도장과 편집 중 퀘스트가 판을 가리는 문제도 함께 수정했다.
- 패널 W7 PASS(1/1, 46.48s): 실제 설명 패널 좌측, 퀘스트 둘 우측, 간격 8, 마지막 추가 패널만 `!`, 긴 제목 잘림 없음. 최신 문구 28개는 기획서와 정규화 대조 일치.
- 튜토리얼 W5 PASS(1/1, 95.99s): G11 카메라 경로 생성 완료, 3개 이상 경로점, 이동·복귀 뒤 입력 복원과 기존 안내 기억 회귀 통과. 밤 스케줄 W3 PASS(1/1, 65.01s): 편성·확정·다음 공연 난이도 조절 유지. 테스트 기대 문구도 최신 `3단계가 개방되었습니다`로 갱신.
- 캡처: `.harness/local/rehearsal/followup/world-schedule.png`, `panels.png`, `tutorial-camera.png`. 각 PASS JSON을 같은 폴더에 보존. 2일차 마지막 방향 안내는 사용자 지시대로 보류. 커밋/push 없음.
- 최종 check-work: Safety/Structure/Knowledge/Fast/Compile PASS, Tests는 스크립트 기본값으로 SKIPPED(위 개별 PlayMode 4건과 EditMode 4건으로 검증). 기존 문서 길이 WARN 47건과 ThirdParty 미저장 2건은 범위 밖으로 보존. 테스트 생성 아틀라스 2개 원복, diff --check PASS, Unity는 clean WaitingRoomBase로 복원.

## 2026-11-04 추가 수정: 패널 의미·튜토리얼 카메라·스케줄표 입력
- `[tutorial]`은 조건 달성형 퀘스트 제목에만 붙이고, 설명은 지정 시간 후 제거되도록 분리. W6 VoteAndPersonalCheckpoint 1/1 PASS(37.17s).
- 상점 카메라 실패 원인은 카운터 안쪽의 도달 불가 NavMesh 섬을 목표로 잡은 `PathPartial`. 목표 주변의 연결된 후보를 고르고, 복귀를 진입 시간의 1/3로 설정. 복귀·브레인 블렌드 완료 후에만 다음 안내를 시작. 밤 N02 상점→N04 진열대→N03 스케줄표 1/1 PASS(101.18s), G11 무대 입구 1/1 PASS(79.69s).
- 스케줄표는 판면 가로·세로를 가득 채우고 클로즈업 거리를 화면비에 맞게 재계산. 공연 선택 불가 원인은 런타임 `InputSystemUIInputModule` 포인터 액션 미연결. 기본 액션 연결 후 실제 Mouse 입력으로 카드 2회·확정 1회 W7 1/1 PASS(32.99s). 최종 캡처는 `.harness/local/rehearsal/followup/world-schedule.png`.

## 2026-11-04 추가: 첫날 밤 [tutorial] 중복 제거·dev 반영 전 점검
- 원인: 첫날 밤에 튜토리얼 N01(오른쪽 고정 패널)과 첫날 진행 퀘스트가 둘 다 `[tutorial]` 패널로 오른쪽에 쌓임.
- 사용자 지시로 N01을 `[tutorial]` 없는 왼쪽 설명 패널로 전환. 밤 연계 N01→N02→N04→N03 첫 안내로 G01과 같은 4초 텍스트 잠금. 제목은 기존 본문 머리말 `오늘 밤 할 일`을 사용. 오른쪽 고정 패널·TodoVisible 제거, 코드 기본 문구도 문구 표와 일치시킴. W5 테스트 기대값 갱신(N01 설명 패널 표시·[tutorial] 없음).
- dev 반영 전 이전 수정 대조: 패널 표기(설명 무표기·퀘스트 표기), 카메라(바닥 경로 왕복·복귀 진입의 1/3 시간·복귀와 블렌드 복원 후 다음 안내), 스케줄표(판면 채움·화면비 맞춤 클로즈업·입력 모듈 기본 액션) 코드가 요청과 일치. 리허설 QuestTitle 주석이 반대로 적혀 있던 것만 수정.
- 확인된 한계: 스케줄표는 판의 가로·세로에 각각 맞춰 늘리므로 판 비율이 1056:552와 다르면 글자가 약간 늘어날 수 있음. 카메라 경로가 없으면 이동 연출 없이 안내만 진행(경고 로그). 클로즈업 중 다른 화면 알림(예: 인간 대포 획득)이 판 위를 가릴 수 있음(캡처 확인).
- 일반 에디터 컴파일 completed/failed=false/errors=[]. N01 변경 PlayMode는 사용자 요청 전이라 미실행.
- 테스트 부산물로 보이는 폰트 아틀라스 2개·escape-labels.asset 변경은 사용자 확인 전이라 되돌리지 않음. origin/dev 미반영(HEAD 8b8910da, 겹침은 ThirdParty·remote-player-judged-before-placement.md).
- 스케줄표 판 맞춤 변경: 실제 판 2.4×1.4m(1.71) vs UI 1.91로 기존에는 세로 약 12% 늘어남. 판 사본에만 균등 배율(가로 기준)을 쓰고 남는 높이 64 UI는 패널 배경·종이 확장, 일차 칸은 가운데, 버튼줄(닫기·안내·확정)은 아래로 이동. 공용 프리팹은 미변경.
- 스케줄표 화면 가림: ScheduleScreenHold 추가. 열 때 켜진 화면 캔버스(HUD·알림·자막·안내)를 끄고 닫으면 복원, 카드 설명 툴팁은 유지. 중앙 안내는 보는 동안 온 것을 대기열에 넣어 닫은 뒤 출력 순서대로 표시, 보던 안내·튜토리얼 설명·기물 알림은 남은 시간 정지. 튜토리얼 새 안내는 닫은 뒤 자기 대기열 순서로 표시. 개발용 IMGUI HUD(음성/마이크)는 대상 아님. 자막(BroadcastCaption)은 가려지지만 시간은 계속 흐름.
- 첫날 밤 퀘스트 night-schedule/night-schedule-wait 본문에서 "상점과 물려줄 선반을 이용해 정비할 수 있습니다." 제거(사용자 지시, 새로 추가 예정). 밤 연계 N02 상점·N04 선반 클로즈업은 유지.
- 검증: W7 1/1 PASS(109.04s, HUD 가림·복원, 보던 안내 A 재표시, 대기 안내 B→획득 순서), W5 밤 튜토리얼 1/1 PASS(98.48s, N01 왼쪽 설명·[tutorial] 없음). 캡처 world-schedule-before-confirm.png, world-schedule-after-close.png, tutorial-n01.png. 첫 W7 실행은 테스트의 기대 순서 오류(획득 안내가 한 프레임 늦게 출력)로 FAIL 후 테스트 수정.
- dev 반영(사용자 요청): MPPM 2인 확인 준비 중 메모리로만 바꾼 시나리오(보조 에디터 1개)·활성 설정은 원복(파일 변경 없음). Unity 정상 종료 후 origin/dev 12커밋 빨리감기(8b8910da→b22a5515). 겹친 remote-player-judged-before-placement.md는 dev의 이동 장애물 절과 리허설 절을 모두 유지해 수동 병합. _ThirdParty는 dev 커밋 f0d16e98로 맞추고 로컬 머티리얼 2개 수정 보존. 테스트 부산물이던 폰트·escape-labels 변경은 에디터 종료 후 사라져 원복 작업 불필요. Unity 재실행 컴파일 오류 0(Vivox 403 2건은 기존 환경). 커밋/push 없음. MPPM 2인 실제 확인은 아직 미실시.

## 2026-11-04 기획서(Confluence 33619991) 가이드 패널 문구 반영
- 사용자 지시: 문구는 기획서대로, 구현 기능은 오류 외 유지. 퀘스트 패널 제목을 조건별 rehearsal.quest.{key}.title로 분리(없으면 "[tutorial]"). 설명 패널 단계 제목은 rehearsal.stage.{id}.guide-title로 분리해 재연습 버튼 이름(.title)은 유지. 집결 퀘스트 제목도 조건별 키. 대기실·1일차 밤 퀘스트, 동료 대기, 즉시 클리어 보조 문구를 기획서 문구로 변경.
- 기획서와 달리 유지: 1일차 밤 참가자 퀘스트의 상점·선반 문장(사용자 이전 지시로 삭제 유지), 기획서 오타 "퀘무대"는 제외. 2일차 아침 [tutorial] 다음 공연 준비하기는 신규 기능이라 미구현.
- 사용자 지시로 BaseGameSettings 대기실 씬을 Integrated→Base로 변경(에디터 반영 확인).
- 컴파일 PASS. W6 VoteAndPersonalCheckpoint 1/1 PASS(70.20s), W5 밤 튜토리얼 1/1 PASS(123.67s). 캡처 .harness/local/rehearsal/panels/*.png, followup/tutorial-n01.png.
- 조정 항목을 작업노트 하위 페이지로 정리: Confluence 34799625 「[M2] 리허설 가이드 패널 — 기획서 대조 후 조정 필요 항목 (11/04)」.
- 11/04 플레이테스트 버그 정적 조사(재현 전): ①끊긴 줄 비동기화·호스트 잡기 시 줄 미생성 = 리허설 해제봉 무제한 유지(UnlimitedHoldSeconds 1e6, RelayGate.Pending.cs:42)가 클라이언트 스냅샷 검증(RelayGate.Network.cs:89 until>Clock+125)에서 거절됨. ②그네 메커니즘 차이 = 리허설은 공연 TightropeTrapeze(초록 표시) 대신 연쇄 회전봉, 코스 JSON을 리허설 전용으로 작성(RehearsalStage.cs:143-152)하고 practice mode 사용. ③4인 길안내 깜빡임 = 각자 로컬 기준은 정상, 0.1초마다 재계산 실패 시 즉시 숨김(RehearsalPathGuide.cs:58,65) 추정. 수정 전.
- 트리거 줄 동기화 수정: 지정 자리 해제봉(리허설)의 무제한 유지를 1e6초 대신 0(PoleGateState 무제한 점유)으로 전달(RelayGate.Pending.cs). 클라이언트 스냅샷 상한 검사에 걸리지 않게 되고, 호스트 Tick 만료도 없음. 리허설 봉 잡기에서 끈끈이 장갑 시간 연장 호출 제외. 1e6 상수 삭제. 공용 상호작용 안내는 무제한 봉이면 남은 시간 대신 "SPACE로도 놓기"만 표시(IsUnlimitedHold 추가). 일반 공연 해제봉(유한 시간) 경로는 변경 없음. 컴파일 PASS, 테스트·2인 실제 확인 미실시.

## 2026-11-04 리허설 발판·투표 패널·재연습·링 장식
- 사용자 결정: 발판 폭 3배·길이는 원형 벽(지름 53m) 안까지, 넘기기는 전체 인원 과반, 재연습 상호작용은 외줄만(인간대포 리허설 미구현).
- 코스: 출발대·도착대 6m, 옆 여백 4.5m(RehearsalStage.Course). 1단계 중간 발판은 옆 줄을 덮지 않게 폭을 줄 간격 이하로 제한(TightropeCourse, 중간 발판은 리허설 전용).
- 도착대: 도착한 사람 화면에만 보이지 않는 벽 4면(앞 벽은 도착선 0.4m 앞), 기존 되돌리기 삭제.
- 링 장식: FindRing이 씬 재로드 뒤 파괴된 옛 연습실을 계속 써서 PracticeLanes를 숨기지 못하던 결함 수정(플레이 모드에서 리허설 중 탑·사다리·그물·대포 레일 표시 확인). 사다리 뒤 막이(CollisionRepairs)도 숨김.
- 우측 하단 투표 패널(BasicPanels Menu_C13 + Button_General): 리허설 넘기기(과반) → 중앙 안내 "리허설이 종료됩니다." 2초 → 검은 화면 페이드 아웃 → 호스트 EndRehearsal(첫 공연) → 새 화면 준비 뒤 페이드 인(최대 25초 대기). 넘긴 리허설은 이수로 기억. 재연습 중 넘기기는 연습 종료.
- 리허설 다시 하기: 이수 뒤 쉬는 시간·밤에 연습실 외줄(PracticeLanes/Rope) 아래에서 F → 전원에게 동의/미동의 패널, 미동의 1명이면 취소 안내, 전원 동의면 외줄 리허설 1단계부터 재연습(공연·1일차로 이어지지 않음). 기존 호스트 소구간 재연습 버튼은 Dev 모드 전용.
- 구간별 "다음 구간으로" IMGUI 투표와 Dev 버튼은 패널 위로 이동.
- 컴파일 PASS. W6 테스트 기대값을 투명 벽 기준으로 갱신. 테스트·2인 실제 확인 미실시.
- 캡처 검증 W6_Rehearsal_SkipVoteEndsAndPracticeVoteNeedsEveryone 1/1 PASS(84.59s, 합성 원격 1명): 발판 값·링 소품 숨김·넘기기 1/2 미통과·2/2 통과·종료 안내·페이드 아웃·첫 공연·이수 기록·외줄 상호작용·재연습 투표 미동의 취소·전원 동의 재연습(RestTime 유지). 캡처 .harness/local/rehearsal/vote/. 첫 실행에서 패널 한글 □ 깨짐(글꼴 보완 미적용)과 공연 뒤 검은 화면 유지(커튼 활성 조건) 발견 후 수정. 두 번째 실행은 테스트의 공연 연출 대기 부족으로 실패 후 보완.
- 캡처 뒤 패널을 소지품 칸 위(아래 120)로 올림, 컴파일만 확인. 재연습 중 첫날 퀘스트가 리허설 퀘스트와 함께 쌓이는 것 관찰(미수정).
- 기획서 갱신(11/04 오타·문구): 집결 퀘스트 "바닥에 표시되는 길안내 선을 따라 무대 입구 통로로 이동하세요…", 1일차 밤 참가자 "호스트가 다음 날 일정을 확정해야 합니다." 반영. 설명 패널(4.3)은 구현과 동일.
- 4인 길안내 깜빡임: 경로 계산 실패 시 직전 경로 1초 유지, 실패 원인(발밑·목적지 샘플, 경로 상태) 1회 로그, 목적지 변경 시 초기화. 컴파일 PASS, 4인 재현 확인 미실시.
- 그네·외줄 규칙 대조(Unity 값 비교): 리허설 코스 값은 공연 TightropeSettings와 줄 수·길이·구간 표시·그네/해제봉/공중그네 사용 여부·발판 값만 다르고 균형·이동 관련 코스 값은 같다. 리허설 인간 그네는 공연 회전봉(TightropeSwingPole)과 같은 컴포넌트·같은 초록 표시(놓기 가능)·언제든 Space 놓기. 공연의 공중그네(TightropeTrapeze, 2인 봉 교대)는 리허설에 없다. 어떤 "기존 시스템"과 다르다는 것인지 사용자 확인 필요로 수정 보류.
- 공중그네 교체(사용자 결정: 2-1·2-2, 3단계는 회전봉 유지): 코스 값에 공연 TightropeTrapeze 켬(2-1 시작 10m·길이 12m, 코스 32→34m / 2-2 시작 10m·길이 16m). 공중그네는 시작 10m 이상·구간 뒤 도착까지 10m 초과가 필요(HasTrapezeGap). 회전봉 자리 제거. 퀘스트: 그네 앞 5m 탑승, 앞쪽 봉 잡기·비행 중 "다음 그네", 마지막 봉 "건너편 줄로 도착". W6_Rehearsal_SwingStagesUsePerformanceTrapeze 1/1 PASS(43.08s, 첫 실행은 시작 8m로 공중그네 미생성 FAIL 후 수정). 캡처 .harness/local/rehearsal/trapeze/. 실제 봉 잡기·비행 입력은 미확인.
- 기존 회전봉 기준 테스트(W6_Rehearsal_CoursesFollowStageDiagrams 2-1 그네 단언, 레이아웃 감사 2-1·2-2 그네 자리·SwingAcross)는 이번 교체로 기대값이 맞지 않게 됨. 미수정.
- 인간 그네 상호작용 불가(사용자 보고): 교체한 공연 공중그네의 첫 봉이 목마 도약(PiggybackSystem.CanEnterTrapeze)으로만 잡히는 공연 규칙 때문. 리허설만 TightropeTrapeze.SoloEntry로 줄 끝 F 탑승 허용(봉 위치 그대로, 잡기 반경 안). 사용자 지시로 리허설 중 PiggybackSystem.Suppressed=true(목마 연결·안내 없음), 종료·파괴·재시작 때 해제. 컴파일 PASS, 실제 잡기 입력 미확인.
- 균형바 비교 W6_Rehearsal_BalanceBarMatchesPerformanceCapture PASS: 공연·리허설 모두 같은 PerformanceHud 프리팹의 P1_Balance(위치·크기 동일). 차이는 캔버스 맞춤(공연 Expand, 리허설 MatchWidthOrHeight)과 공연 전용 상단 조작 안내·진행 거리 표시. 맞춤을 Expand로 맞춤. 캡처 .harness/local/rehearsal/balance/.
- 리허설 전체 동선은 기획서 3.3.1·3.4.1과 코드 대조상 순서 일치(추가된 리허설 넘기기 제외). 사용자가 말한 차이 지점 확인 필요.
- 사용자 지적: 2-1을 기획(그네 1개 잡고 건너편 줄)과 다른 연속 공중그네로 바꾼 것은 잘못. 2-1·2-2를 기존 회전봉 그네로 복원(2-1 32m·그네 8~16m, 2-2 연속 그네 2개). 공중그네 관련 추가(리허설 구간 값·퀘스트 분기·TightropeTrapeze.SoloEntry·공중그네 테스트) 모두 제거, TightropeTrapeze는 HEAD와 동일. 목마 끄기(PiggybackSystem.Suppressed)는 사용자 지시로 유지.
- W6_Rehearsal_CoursesFollowStageDiagrams: 2-1 그네 자리·2-2 연속 그네 실제 잡기(SwingAcross)·2-3·3단계 구간은 통과, 마지막 완료 뒤 무대 입구까지 경로 걷기에서 (6.16,0.08,82.64) 지점 이동량 4.0075(기준 <4)로 FAIL. 그네 복원과 무관한 경로 걷기 구간이며 원인 미조사.
- 경로 걷기 FAIL 조사: 캡처상 리허설 완료 직후(16:43:10~16) ESC 메뉴가 열려 이동 입력이 막힘. 메뉴는 ESC/게임패드 취소 입력으로만 열리고 테스트는 누르지 않음 → 테스트 중 외부 키 입력으로 판단. 같은 테스트 재실행 W6_Rehearsal_CoursesFollowStageDiagrams 1/1 PASS(90.07s, 2-1 그네·2-2 연속 그네 실제 잡기·2-3·3단계·철창·무대 입구 경로 걷기 포함).
- 균형 UI(사용자: 공연 균형 UI 그대로): 공연은 PerformanceHud 프리팹에서 균형바만 남긴 TightropeBalanceCanvas(Expand)를 쓰고 진행도·응원은 공용 공연 UI가 맡는다. 리허설은 같은 프리팹 전체를 띄워 빈 응원 게이지(가운데 위)·진행 바(오른쪽 위)가 함께 보였다. 바인딩이 갱신하는 진행도·응원 게이지는 남기고 CanvasGroup으로 화면에서만 숨김(타이머는 바인딩이 이미 끔), Expand 유지. 컴파일 PASS, 화면 확인은 사용자 진행.
- 리허설 진행 불가(사용자 보고, 링 위 코스 없음): 균형 UI 숨김에서 GetComponent<CanvasGroup>() ?? AddComponent가 에디터 가짜 null로 MissingComponentException(C1CheerGauge(Clone)) → BuildPerformanceUi 예외로 코스 활성화 전 중단. TryGetComponent로 수정, 투표 패널의 같은 패턴도 수정. 컴파일 PASS.
- 튜토리얼 카메라(사용자 보고: G10 의료용 서비스 패널만, 카메라 이동 없음): Editor.log "바닥 경로 없음: (-5.00,0.08,69.90)→(9.16,0.08,88.76), 후보 없음". 경로 출발점을 3인칭 카메라 위치의 바닥으로 잡아 벽 너머·틈의 다른 NavMesh 섬에 붙던 결함. 플레이 모드 재현(플레이어 기준 출발)에서는 경로 5점 정상. 출발점을 플레이어 발로 변경, 경로는 카메라→플레이어 눈높이→바닥 경로(첫 구간은 막힘 검사 제외). 비연계 안내도 앞 클로즈업 복귀·카메라 전환 완료(IsBusy) 전에는 시작하지 않게 함. 컴파일 PASS, 확인은 사용자 진행.
- 저장·제출: c2f689bb 커밋·push(feat/rehearsal-flow). 테스트 부산물 폰트 2·escape-labels, ThirdParty 머티리얼 2, BaseGameSettings 대기실 Base는 로컬 전용으로 제외. PR #287 → dev(Squash, 지정 팀원 검토): https://github.com/gaboja-studio/POC_CURTAINCALL/pull/287
