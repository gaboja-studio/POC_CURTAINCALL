# 작업 로그

- 이전 PR #294 제출·검증 기록: [History/007](History/007-before-bug-followup-log.md).

## 2026-11-05 후속 제출
- 사용자 저장·제출 요청. Unity 종료 후 origin/dev 7bfa26c6 최신 확인, 자동 생성 escape-labels 캐시는 별도 stash로 보존·제출 제외했다. PR #294가 병합되어 후속 브랜치 feat/rehearsal-flow-v19를 만들었다.
- 원본 WaitingRoom 씬·Base GUID·기본 공간 설정·마네킹·외부 에셋 gitlink 변경 없음. 변경/버그 수정/검사 범위는 [v19 감사](wiki-v19-audit.md)에 정리했다. 신규 플레이 검사는 사용자에게 맡기고 저장 전 필수 검사만 진행한다.

## 2026-11-05 기획서 재반영·양방향 진입
- Unity 종료 후 origin/dev=f7cf9ef5 반영 확인(0/0), 기존 캐시 stash 보존. 기획서 v19 본문/첨부 5개 재확인. 사용자 답변으로 3단계 현행 유지·마네킹 보류. 이번 묶음 commit/push 요청 없음.
- 1단계 2줄/중간 발판, 2-1 낮은 원본 봉1/2-2 봉2, 2-3 단일 원본 트리거/아래줄 연결, 실제 학습 판정·도착 후 구간 투표, 대포 기획상 포기만 허용, 조건 가이드/첫날·2일차/노란 점선 반영. 상세 [v19 감사](wiki-v19-audit.md).
- 추가 사용자 요청에 따라 일반 공연/리허설도 시선 기준 W/S·균형·옆줄 입력 적용. 새 실제 입력 학습3/3 PASS, 최초 대포6/6·첫날1/1 PASS. 기존 외줄14 중3건은 옛4줄 기대값이 남아 FAIL, 수정 후 원본HUD/설정·도착 전환 각1 PASS.
- 실제 W 입력 재검사에서 출발 .45m 옆 배치가 좁은 줄 진입을 놓치는 결함 재현. 줄 중심에 배치하도록 보정 예정. 대포 완료 퀘스트의 공연 전환 잔류/소개14초를 수정한 뒤 대포7/7 PASS. 4인 새 빌드/전체 완주와 최종 필수 검사는 계속 진행 중이며 이전 PASS로 대체하지 않는다.

## 2026-11-05 강제 종료 복구·추가 버그 목록
- 강제 종료 뒤 Unity의 편집 씬 미저장0·컴파일 정상 확인 후 종료. 기존 변경/생성 캐시를 별도 stash로 보존했다. safe transition으로 dev 121874d8/private c1e64a6d 동기화하고 WIP 707c16f7 복원, 충돌 없음.
- 사용자 직접 플레이 테스트로 범위 변경. 새 4클라이언트 인수 실행 대신 기능/진행 차단 회귀를 수행하며, 이전 4인 결과를 새 구현의 결과로 쓰지 않는다.
- 대포 전체 +2m·원본 착지 표시 복사·소유자 배치 승인 전 낙사 유예·넘기기 이수 복원/문구 잘림·새 퀘스트 느낌표 갱신·원본 매달림 구조 안내 구현.
- 원본 비교3/3, 대포8/8 PASS(174.76초). 양 시작 발사/착지·획득/편성 클릭·원격 합성 배치경합·낙사 복구·넘기기 전문/클릭/복귀 검사.
- 외줄 학습5 중4 PASS: 낮은 봉1/2 실제 완주, 양 끝 실제 W/S, 학습 없는 도착 거절. 구조 안내1건은 표시 실패하여 원인 확인/수정 중.

## Base 씬 기준 전환
- 사용자 지시로 WaitingRoom 내용을 Base에 먼저 적용하고 이후 작업/검사는 Base로 한정한다. Unity 종료·WIP ace9ebee 보존 후 최신 dev c62c2467 동기화/복원(코드 자동 병합, 충돌 없음).
- EditorSceneManager.SaveScene(asCopy)로 Base에 저장. 루트16개, 전체 계층/컴포넌트/로컬 변환 해시 일치, Base GUID 0ce9037bd7c44c8b9d57785c26620388 유지, 원본 WaitingRoom 파일 SHA256 불변·git 변경 없음. 결과 base-scene-migration-result.json.
- 지연 콜백이 편집기 비활성 중 실행되지 않아 동기 에디터 호출로 완료 확인했다. 남은 해당 콜백만 식별해 제거했으며 중복 복사는 예약되어 있지 않다.
- 대포 획득/진행 회귀와 외줄 출구 검사도 Base 경로로 변경. 검사의 런타임 공간 선택이 저장된 BaseGameSettings 값은 원래 dev의2로 Editor API 복구했다.
- Base W6 최초 전체 29개: 26 PASS/3 FAIL(757.45초). 대포9·학습5 모두 PASS. 두 번째 양쪽 대포도 실제 발사→원본 트램 반동→도착 판정/복귀 확인(동료 트램 위치는 검사에서 준비, 4인 접속 검증 아님).
- 실패: FirstDay 안내 완료/재연습 진입에서 이전 방의 완료 전송 키 재사용으로 잠금. CampaignRun 소유자 교체 시 키 초기화. 재연습 첫 안내가 일반 튜토리얼에 지워지지 않도록 표시 전에 실행 양보, 일반 집결 퀘스트도 숨김.
- 나머지1건은 Vivox Editor HTTP403 외부 오류. 사용자 지시로 원인 수정 범위에서 제외하며 게임 로직 실패로 세지 않는다. 즉시 클리어 기능 자체는 별도 재실행한다.
- 재연습 설명 새 단언의 첫 시도는 10초 도입 전 구간 제목을 기대해 FAIL. 도입→구간 설명 전환을 실제 시간에 맞춰 검사하도록 보완했다. 최초 안내 소실도 함께 재현/수정했다.
- 최종 추가 검사: 재연습1(34.51초)·첫날/밤/다음날1(63.37초)·즉시 클리어1·W7 패널/스케줄2·원본 비교3 모두 PASS. 전체 고유 PlayMode31/EditMode3 = 34 PASS. 최초 실패와 재실행 결과는 별도 JSON으로 보존한다.
- 원본 WaitingRoom SHA256 7344B9A1C18E78511EBF2032C6C0F5344CC4222B6E52D7EB78362FC3C8A93C78 및 Base .meta 불변 재확인. 테스트 뒤 Base 저장 상태 clean·기본 설정 variant2 복구.
- 최종 Unity 종료/fetch에서 dev 신규7개(a016f93e) 확인. 생성 escape-labels 캐시 c8b70f71, 전체 WIP 99b8753d 별도 stash 보존. safe transition으로 dev/private 동기화 후 WIP 복원, TightropeCourse/Trapeze 자동 병합·충돌 없음. 새 봉 동기화/목마 하차/발 접지와 겹치는 기능 추가 회귀 진행.
- a016f93e 영향 검사66/66 PASS: PlayMode 학습5·원본 공중 봉/목마1·트리거/출구1·원격 배치1·균형HUD1, EditMode 지지면/진입51·봉 스냅샷3·대포 원본 참조3. 결과 `a016f93e-*-result.json`. 실제 4인 접속 검증은 미실행. 사용자 재확인으로 추가 플레이 검사는 종료하고 저장/필수 컴파일 검사 후 직접 조작하도록 준비한다.

- 2026-11-05 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 마지막 Unity 종료/fetch에서도 dev a016f93e와 HEAD 차이0/0. 필수 check-work의 테스트 SKIPPED는 위 별도 실행 결과와 구분한다. 기존 문서 길이 WARN만 있고 hard0, 코드/씬 저장 완료. 새 commit/push는 하지 않았다.
- Base 외줄 리허설을 Play Mode로 열어 사용자 조작에 인계: stage0, Normal, 배치 승인=true, 입력 잠금=false, mover 조작/시뮬레이션=true, 입력 reader=true, QA 자동 조작 opt-in=false. 기본 씬 설정 에셋은 variant2 그대로이며 이번 플레이만 DontSave 복제 설정으로 Base를 사용한다. 결과 `final-user-play-state.json`.
- 후속: 사용자가 실제 플레이/다인 접속을 확인한다. 추가 자동 플레이 검사는 중단했다. 재생 중 발생하는 폰트/escape-labels 캐시는 기능 변경과 구분하며 이전 캐시 백업 stash는 유지한다.

## 그네 앞쪽 절반 도약 (2026-11-05)
- 재개 전 Unity 종료/fetch: dev a016f93e, 차이0/0. 사용자 답변으로 앞쪽 최대 각도의50% 이상을 확정(왕복 경로의 중간이 아님).
- 리허설의 낮은 봉/연속 봉/공중 봉은 해당 각도에서 Space로 놓으면 다음 봉 또는 건너편 줄에 닿는 출발 속도로 보정. 원본 중력·높이 상한·공중 조절·F 잡기·호스트 비행 공유를 유지하고 공연 모드는 기존 물리를 사용한다. 단계 가이드3개도 반영.
- EditMode TightropeSwingGapTests 30/30 PASS(0.72초): 50% 경계/반대편/공연 제외, 30·120fps 탄도 도착, 아래 매달림·원격 속도 검증 범위. 컴파일 오류0. 플레이 검사는 사용자에게 맡긴다.
- 최종 fetch의 dev 7bfa26c6(탈출로/매달림 카메라) 반영. Unity 종료·전체 WIP stash 보존 후 private gitlink 불변 확인하고 public만 fast-forward. 코드 자동 병합, 지식 문서2개의 요약 충돌은 양쪽 내용 보존으로 해결. 그네 탄도 코드는 새 dev와 겹치지 않는다.
- 필수 check-work 안전성/문서/컴파일 PASS. 테스트 SKIPPED는 별도 EditMode30 PASS와 구분. 재사용 사실 문서를 v19/Base/50% 예외로 갱신하고 Knowledge16폴더 PASS.
- 사용자 정정대로 2-1로 건너뛰지 않고 Base 외줄1단계(stage0)부터 Play Mode 실행. Normal/배치 승인/입력 잠금 해제/조작 가능 확인, 자동 조작 없음. 결과 `half-angle-user-play-state.json`. 원본 WaitingRoom 씬과 기본 씬 설정은 보존, commit/push 없음.

- 2026-11-05 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-05 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
## 2026-11-05 첫 공연 뒤 완주자 정지·타이머 불일치
- 보고: 외줄 공연을 클리어한 사람만 대기실 튜토리얼·F 패널·외곽선이 없고 진행이 멈춤. 타이머도 화면별로 맞지 않음.
- 원인1: 공용 외줄은 ExternallyManaged라 ServerMarkArrivals로 완주자가 Arrived가 되고, 쉬는 시간 시작 부활은 Fallen만 되돌렸다. 대기실 표시는 Normal만 받아 완주자만 막히고, 튜토리얼 완료 미전송으로 가이드 타이머도 시작되지 않는다. 수정: InjuryRevival이 쉬는 시간·밤 시작에 Arrived도 Normal로 복귀.
- 원인2: 첫날 둘째 공연 미확정이면 호스트 Tick이 멈추지만 Timer는 Running으로 남아 받는 쪽만 계속 줄었다. 수정: SetRestTimerPaused가 미확정 동안 Stopped로 공유. 호스트 Tick 규칙은 유지.
- 검증: RestTimeRulesTests 5/5 PASS(신규1), RestStartReturnsPerformanceFinisherToNormal 1/1 PASS(LAN 호스트 1명). 실제 다인 재현은 사람 확인 필요. 타이머 증상을 사용자 화면에서 직접 재현하지는 않았다.
- 수정 파일은 origin/dev와 동일한 파일이다. Unity 에디터가 열려 있어 dev(62커밋 뒤) 동기화는 하지 않았고, 커밋·push 없음. 폰트 SDF 2개 변경은 에디터 동적 아틀라스 변경으로 이번 수정이 아니다.

- 2026-11-05 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- PerformanceEntryJudgeTests 전체 실행에서 기존 3건이 StartSession 인자 변경으로 실패(이번 수정과 무관)해 테스트 호출을 고쳤다. 재실행 5/5 PASS. check-work: 컴파일 PASS, 문서 검사 WARN(기존 파일 크기), 전체 자동 테스트는 미실행.
- Unity 종료 확인 후 origin/dev e95b6d9b까지 fast-forward. 변경은 stash로 보존/복원했고 log.md 충돌만 dev 내용+이번 기록으로 해결. `_ThirdParty`는 c1e64a6d로 갱신, verify-third-party OK. 커밋·push 없음.
- dev e95b6d9b 기준: 컴파일 up_to_date, RestTimeRulesTests 5/5 PASS. PerformanceEntryJudgeTests PlayMode 재실행은 에디터 종료로 결과를 받지 못했다(동기화 전 5/5 PASS). 실제 다인 플레이는 사람 확인 대기.
- 2026-11-05 PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/307 (feat/rehearsal-flow → dev). new-pr.ps1는 이번 작업과 무관한 미커밋 변경(escape-labels.asset, _ThirdParty 셰이더 2개)으로 중단되어 같은 템플릿으로 gh에서 직접 등록했다.
