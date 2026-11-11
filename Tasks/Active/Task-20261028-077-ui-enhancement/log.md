# Log

- 환경 준비·초기 HUD 배치와 추락 게이지 변경 원문: [History/001-setup-hud-log.md](History/001-setup-hud-log.md)

이전 시계·HUD 작업: [기록 원문](History/003-hud-log.md).

## 2026-10-28 사용자 재현: 방 입장·입력 끊김 추가 수정
- 사용자: 첫 수정 후에도 방 입장 직후부터 버벅임이 심하다. 정지 대기실에서 업로드만 측정한 검증의 한계를 확인했다. doctor는 기존 private 변경으로 FAIL 1건. 실제 LAN 호스트/네트워크 Lobby 로드 후 키 입력에서 193.80ms 프레임, Append 114.78ms/PendingBytes 112.46ms를 재현했다. 키 입력마다 누적 JSON 파일 전체의 FileInfo.Length를 메인스레드에서 조회하던 추가 원인이다.
- 시작 시 1회 색인하고 성공한 AtomicWrite/DeleteFile 때 용량·봉인 배치 수를 증분 갱신하도록 수정했다. 색인 잠금 안에는 파일 I/O가 없으며 쓰기 실패 시 집계는 그대로 유지한다. 즉시 영속화·복구·용량 한도·ACK 검증은 유지한다. Append/PendingBytes/AtomicWrite ProfilerMarker를 남겼다. 시계·HUD·씬 저장·설정 변경 없음.
- 자동 검사 DurableOutboxTests 20/20·TransportPolicyTests 7/7 PASS. 추가 3개는 UTF-8 덮어쓰기/Seal/ACK/복구와 색인 대조, 기존 64MiB 초과 원본 보존, 디렉터리 재조회 없음/쓰기 실패 롤백이다. 비동기 ACK와 동시 Append/Seal·relay 경합 검사에도 색인 대조를 추가했다.
- 첫 입력 반복 표본 1758프레임은 Append 최대 15.32ms/PendingBytes 0.0013ms였으나 최대 프레임 466.55ms(EditorLoop), 초기 EventSystem JIT 60.85ms가 있어 전체 최대를 개선치로 쓰지 않았다. 다음 표본은 실행 중 코드 import·Play 종료가 섞여 1442ms EditorLoop/씬 로드·GC 정지가 포함되어 통제된 비교에서 제외했다. 임시 eval의 delegate 타입 오류와 보존 범위를 벗어난 프레임 재조회 오류는 재실행으로 보완했다.
- 최종 코드 수정 없이 Lobby에서 F13 press/release 80회 예약해 UI 이동·버튼 동작과 분리했다. 572프레임 중앙값 16.63ms/p95 18.77ms/최대 22.79ms, 입력 저장 79프레임(마지막 예약 입력 처리 전 수집), 입력 프레임 중앙값 16.20ms/최대 22.79ms. Append 최대 16.40ms, PendingBytes 0.0009ms, AtomicWrite 최대 14.75ms. 50ms 초과 프레임 없음, LastError 없음·실제 업로드 ACK 확인. 동기 저장 비용은 남으며 빌드·2인 장시간 플레이 및 사용자 최종 체감 해결로 확대하지 않는다.
- 임시 입력 콜백은 완료 시 제거하고 키를 해제했다. 직접 실행한 Play 종료·Profiler 원복, 비밀값·원본 payload 출력 없음. 지식 문서를 갱신했다. commit/push 없음.

- 2026-10-28 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-28 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-28 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-28 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-28 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-28 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-28 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-28 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-29 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-10-29 시계 원 정렬·하단 명판
- 사용자 요청: 대기실 원 중심을 공연 C1과 일치시키고 일차/반일을 원 아래로 이동, AM/PM 표시·N일차 소폭 축소. C1은 변경하지 않고 시계 176×176 다이얼을 유지했다. Root (32,-16)/224×292, 공통 원 중심은 좌상단 기준 (144,-156), 명판 y260·6시 눈금과 12 UI 여백, N일차 26→22pt다.
- 기존 프리팹 GUID·씬 참조를 보존하여 Editor API로 갱신했다. 재생성 조립기와 구형 fallback도 같은 배치를 유지한다. V24 전용 TMP 3종에 AM/PM 글리프를 추가하고 Static으로 복원했다. 시간표·타이머·무제한·12시간 계약은 유지했다.
- 최초 컴파일에서 TMP sourceFontFile 내부 setter와 out 변수 중복 오류가 발생해 SerializedObject 설정/변수명으로 수정했다. 최종 컴파일 성공. EditMode DayClockTests 17/17, PlayMode WaitingRoomHudPlayTests 2/2 PASS. 후자는 HTTP400 응답 뒤 test_status에서 실제 완료 결과를 확인했다. 정렬·명판 간격·22pt·글리프·정오/자정 경계도 검사한다.
- Play Mode 임시 Canvas에 실제 프리팹을 배치해 PM 카운트다운/AM 무제한 화면을 캡처·확인했다. 1920×1080에서 실제 다이얼 중심 (144,924)을 확인했다. 임시 eval 생성자 인자 오류 1회는 snapshot 필드 할당으로 수정했다. 임시 UI는 Play 종료로 제거했고 Start 씬 dirty 0으로 복원됐다. 최종 2인 플레이·사람 외형 확인은 미실행이다.
- doctor는 private dirty와 Task077 중복/상태 충돌로 FAIL 2건, 작업 범위 밖 레지스트리는 수정하지 않았다. 완료 check-work는 안전성/문서·규칙/컴파일 PASS, 문서 길이 경고 15건·hard 0. 스크립트 전체 테스트는 생략했고 위 대상 검사 19/19는 별도 실행했다. commit/push 없음.

- 2026-10-29 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=SKIPPED(-SkipUnity); 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-10-29 반일 한글 복원·명판 글자 투명도
- 후속 지시로 AM/PM 런타임 표시·회귀 기대값을 오전/오후로 복원했다. N일차/반일 글자만 alpha 0.85를 프리팹·재생성 조립기에 반영했다. 원 중심·하단 명판·22pt·타이머·배경·구분선은 변경하지 않았다.
- 저장된 프리팹을 Editor API로 조회하여 두 텍스트 alpha=0.85, 오전 기본값, 한글 글리프 보유, 22/18pt를 확인했다. 사용자가 시작한 Play가 진행 중이라 중단하지 않았고 컴파일·자동 검사·새 렌더는 생략했다. check-work -SkipUnity는 안전성/문서·규칙 PASS, Unity 생략으로 exit 2이며 전체 PASS가 아니다. 당시 Play 종료 후 컴파일·DayClockTests 실행이 남았으며 아래 분침 작업에서 18/18 PASS로 확인했다. commit/push 없음.

## 2026-10-29 분침 비활성화
- 사용자 지시대로 분침 오브젝트·참조·회전 계산은 보존하고 프리팹의 71_MinuteHand를 비활성화했다. 패키지 DayClockView의 Show Minute Hand 기본값 false로 시간 갱신 시에도 다시 켜지지 않는다. 이 설정을 켜면 유효 시각에서 분침이 복원된다. 시침·일정 링·실제 타이머는 유지한다.
- 에디터는 stopped 상태여서 정상 컴파일·자산 갱신을 실행했다. 새 검사 최초 double→float 형식 오류 수정 후 컴파일 성공. DayClockTests 18/18 PASS: 반복 시간 갱신 중 분침 숨김/시침 유지, 설정 복원·기존 분침 각도, 미수신 시 숨김, 이전 한글·투명도 변경 포함. 별도 새 렌더·전체 자동 테스트는 생략했다.
- check-work 안전성/문서·규칙/컴파일 PASS, 문서 길이 경고 17건·hard 0. Start 씬 dirty 0, commit/push 없음.

## 2026-10-29 저장·dev 통합
- 저장 순서·충돌 해결·검사 제약·PR 기록: [History/002-dev-integration.md](History/002-dev-integration.md).
- 공개 구현 dc693dfc·dev 통합 67ea57fe 저장 완료. EditMode 103/103, UI PlayMode 전체54/55 후 남은 클래스2/2 PASS. 공개 push/dev PR은 private PR7 main 미병합을 요구하는 훅에 차단됐다. PM 팀 검토 요청 명령은 성공했지만 실제 목록은 비어 있어 지정 확인 필요. 훅 우회·임의 병합 없음.

## 2026-10-29 무제한 구간 중앙 아이콘
- 무제한 구간의 카운트다운 TMP를 비활성화하고 활동/시간대 아이콘 쌍 중심을 다이얼 로컬 (112,-108)에 맞췄다. 유한 타이머 복귀 시 숫자와 아이콘 하단 배치를 복원한다. 시간 판정·설정·분침 off·명판·프리팹/씬 참조는 유지하며 자산 재생성은 하지 않았다.
- 컴파일 성공·DayClockTests 26/26 PASS. 아침/낮 휴식·정비·공연 무제한과 유한 복귀, 잔여 초/일시정지보다 무제한 우선, 미수신 구분을 검사했다. Play 임시 Canvas에 실제 Resources 프리팹으로 밤 정비 렌더를 확인했다(문자 없음·공구/달 중앙). 직접 시작한 Play 종료로 임시 UI를 제거했다. 이번 묶음의 2인 플레이·PlayMode 자동 회귀는 생략했고 commit/push 없음.

## 2026-10-29 v2.7 디자인 통합
- v2.7 PNG/기호·중앙 활동/시간대 짝·현재 조각 강조를 기존 v2 Runtime/Editor에 병합했다. 원본 v2.7 C#은 Reference.asmdef로 중복 컴파일에서 제외하며 .meta/GUID를 보존했다. 보조 AI 1명이 3개 패키지 C# 비교/병합만 담당했고 Unity·씬·테스트는 메인에서 수행했다.
- 새 W1_DayClock_v2_7.prefab을 생성하고 CommonWaitingRoomUI 씬의 기존 루트/DayClockView 컴포넌트 참조를 보존해 연결했다. Resources/재생성 경로도 동기화했다. 224×248/루트 (32,-48), 다이얼 로컬 (112,-108)로 공연 C1 중심 (144,-156)을 유지했다. 기존 일정 프로필·전용 V24 폰트·색·실제 초/무제한·마지막 유효 일정·오전후·글자 alpha .85·분침 off를 보존했다.
- 현재 조각은 phase와 유효 시각이 동시에 맞는 하나만 강조하며 비현재 채움은 Track 12% 혼합이다. 14 UI 링 기호는 48분 미만 조각에서만 숨긴다. 일차 22 SemiBold/크림, 오전후 18 Medium/회색. 중앙 활동(벤치/천막/공구/침대)과 시간대 기호는 외부 phase를 따른다. 무제한/실제 초는 Profile showCountdown보다 우선하며 경고와 구분한다.
- 최종 EditMode DayClockTests 22/22·RunTimeLimitTests 15/15, PlayMode WaitingRoomHudPlayTests 2/2로 39/39 PASS. 새 검사 최초 1건은 테스트 전용 프로필에 없는 Maintenance를 사용해 --:--가 나온 기대값 오류였으며 해당 프로필의 AfternoonShow로 수정 후 통과했다. PlayMode 요청 HTTP400 뒤 test_status에서 실제 완료 결과를 확인했다.
- Game View에서 실제 v2.7 프리팹의 낮 2:30·밤 제한 없음/기호·하단 명판을 확인했다. 1920×1080 원 중심 (144,924), 분침 false. 임시 Play 검증에서 명시적 180ms 전환 시작·반복 스냅샷 재시작 없음·분할 수면 최종 1개 alpha .82/나머지 .10을 확인했다. 실제 UI 단독 가산 로드는 런타임 씬 관리로 유지되지 않아 invalid scene 조회가 발생했고 임시 Canvas+실제 프리팹으로 렌더 확인했다. 실제 방 연결은 위 PlayMode 검사로 별도 검증했다. 임시 UI/프로필은 Play 종료로 제거했다.
- doctor는 기존 private dirty/Task077 중복·상태 충돌 FAIL 2건. 작업 밖 레지스트리는 수정하지 않았다. 최종 사람 외형·2인 플레이는 미확인, commit/push 없음. 첫 check-work는 log 143줄 hard130 초과로 문서/규칙 FAIL·컴파일 생략이었다. compact-docs 절차로 초기 환경/HUD 기록 원문을 History/001-setup-hud-log.md로 옮겨 보존하고 다시 검사한다. 완료 결과는 아래 자동 기록한다.

- 2026-10-29 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-29 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-29 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-29 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-29 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-29 PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/232 (feat/ui-enhancement → dev)

### 2026-11-10 · 역할별 HUD 출력
- 작업 브랜치 fix/ui/layout-and-position를 origin/dev로 fast-forward. Unity 6000.3.25f1 배치 에디터에서 역할 프리팹 8종·매니저를 생성/재로드 검증하고 줄타기 씬의 조작 전용 배너를 제거했다. 열린 에디터(Pipeline/CLI)에서도 생성 메뉴와 컴파일을 검증했다.
- 공연/탈출 외줄 조작·장애물 회피는 학습 카드, 기물 내려놓기는 대상 옆 프롬프트, 개인 사망은 중앙 결과, 동료 사망은 경보. 목표·투표·가이드 및 기존 전용 연출의 API와 인스펙터 설정은 role-hud.md 참조.
- 실에디터 신규 PlayMode 4개 통과: 경보/가이드 독립 만료·소유자 정리, 목표 높이와 투표 콜백, 장애물 안내 재생/종료, 리허설 입장 시 목표·가이드 표시 및 도착 전 투표 비표시. 소집 경보와 동료 사망 회귀 통과. 실화면: .harness/local/rehearsal/vote/role-hud-live.png.
- 별도 장면 회귀: 그래픽 없는 배치에서는 커튼 전환이 막혀 열린 에디터로 재검사. 대기실 튜토리얼 연계는 G11 완료 후 상점 접근 G03이 뜨지 않았고, 리허설 전체 시나리오는 투표 뒤 연습실 외줄 Reach가 음수여서 중단. 실제 두 클라이언트 완주 검증은 하지 않았다. 이 둘은 PASS로 집계하지 않는다.
- doctor: 기존 중복 Task ID/서브모듈 환경 진단 경고. 실제 에셋과 에디터 연결을 별도 확인했다. 테스트 중 생성된 기존 TMP 글리프 아틀라스 변경은 작업 결과에서 제외했다.
- 후속 프리팹 편집: 루트 RectTransform, 행 레이아웃, TMP 템플릿·고정 텍스트, 노출 시간 우선권을 프리팹으로 이전. 실제 역할 카드가 임의 피벗·위치·수동 크기·자식 좌표·고정 제목·색상·본문 치환을 유지하는 회귀 통과. 8역할 + 현재 구간 변형 CourseStatus. 사용법은 role-hud.md.
- 최종 열린 에디터: 컴파일 오류/경고 0, 프리팹 설정/연출 시간 우선권 포함 역할 테스트 4/4, 실제 리허설 입장 배치 1/1. 안전성 및 변경 공백 검사 통과. 전체 문서 검사는 현재 작업 외 Task-097 todo.md 기존 102줄(상한 80)로 차단.
- 역할 테스트 씬 HudRolePlayground 생성·에디터 열기. CLI 컴파일 오류 0, 실제 씬 PlayMode 2/2 통과: 8역할 출력·폼, 2대상 월드 투영·이동, 거리·폐색 시 비표시/입력 차단·재진입. 캡처 Logs/HudPlayground/interaction.png. doctor의 기존 Task ID/에디터 설치 탐지 실패와 별개로 실행 중인 Unity에 연결하여 검증.
- 9개 역할에 기존 원본 프리팹의 스프라이트·장식을 적용. 목표 말풍선 하단·가이드 TIP 키캡·상호작용 가로 키캡·정산 리본·구역 나무 표지 포함. 역할 비교는 role-catalog.png, 시스템 사건은 event-styles.png 참조.
- 열린 Unity 컴파일 오류/경고 0. 역할 폼·실제 원화·범위/가림·사건 1~2줄 및 색상 4/4, 역할 수명·배치·프리팹 편집 4/4, 대기실 실제 연습실→상점 표지 1/1, 탐색 방송 레이어 1/1, 동료 사망 단일 경보 1/1. 로컬 PlayMode 검증이며 다중 클라이언트 실플레이는 미검증.
- 기존 원본 38개를 Unity AssetDatabase로 Roles/Retained에 이동, 이동 전후 GUID 38/38 일치. 로드·테스트·조립기·현행 에셋 지도 경로 갱신. 외부 패널 조립기가 고정 경로를 요구하는 버튼 4개만 옛 위치에 호환 Variant를 둔다.
- 가이드 선택 키(기본 TIP), 동일한 상호작용 키캡, 대기실 바인딩과 외줄 조작 키 연결. SystemEvent의 Message와 런타임 본문 템플릿에 한글이 베이크된 Pretendard Medium 연결. EditMode 한글 실출력 1/1 + 기존 특수 UI 레이아웃 8/8, PlayMode 폼·배지·상호작용 범위 등 5/5 및 실제 전화기 1/1. 전화기 검사는 현재 데이터의 행동명·F·배지 스프라이트·재진입을 확인한다.
- doctor의 중복 ID·서브모듈·설치 탐지 실패와 별개로 열린 에디터 컴파일 오류/경고 0. origin/dev fetch 완료(3커밋 앞섬). 열린 에디터에서 변경 표시된 에셋이 확인되어 종료·브랜치 동기화는 보류했다.
- 공통 HudPresentationSettings: 타입별 교체/아래로 쌓기/위로 쌓기 및 상한. 가이드만 최신순 3개, 나머지는 기본 교체. 퇴장 중인 이전 카드도 즉시 닫고 새 카드의 등장 연출을 재생. 같은 임무를 매 프레임 다시 띄워 교체가 풀리지 않도록 대기실 표시 요청을 정리.
- 구역 표지는 짙은 HUD 프레임·황동 핀·최소 높이 48, 역할 상호작용은 최소 66. 실제 변경 전후 hud-plate-comparison.png. 열린 Unity 컴파일 오류/경고 0, PlayMode 11/11(플레이그라운드 5, 역할/중복 정책 5, 실제 대기실 구역 교체 1). doctor 기존 환경 진단 실패. origin/dev fetch, 열린 에디터 및 미저장 변경 보존으로 동기화 보류.
- 대기실 HUD 프로필과 편집 메뉴로 튜토리얼 24개 단계/사건별 역할/문구, 대상별 앵커를 분리. 보류된 스케줄 안내 순서와 캠페인 판정 보존. 설정 EditMode 2/2, 실제 대기실 PlayMode 5/5(전화기 조준·F, 방송, 침대, 스케줄표, 둘째 공연~다음 날 임무). 최종 Unity 컴파일 오류·경고 0. origin/dev fetch, 8커밋 차이; 열린 에디터와 미저장 에셋 보존으로 반영 보류.
- 이 묶음의 git diff --check 통과. check-work는 안전성/서브모듈 통과, 기존 Task-097 todo.md 문서 길이 상한으로 중단. 해당 검사 내 컴파일은 생략됐고 위 컴파일·테스트는 열린 에디터에서 별도로 실행.
- 대기실 HUD Inspector: 5개 한국어 섹션과 조건·사용법, 제목 기반 단계 선택, 기본 순서 이동, 새 안내·직접 구도 저장 지원. 기존 ID·필수 단계 유지. EditMode 3/3, PlayMode 1/1(임시 프로필의 G01 → 새 스케줄표 카메라 → G11), 최종 Editor 컴파일 오류/경고 0. origin/dev fetch 후 열린 에디터·기존 변경 보존으로 동기화 보류.
- 검사: git diff --check 통과. check-work는 기존 Task-097 todo.md 102줄(상한 80)로 중단. 위 Editor 컴파일·테스트는 별도 실행.
- 대기실 HUD Inspector에서 직렬화된 입력값을 바로 읽어 표시 예시를 그린다. 두 필드가 짝인 문구는 상대 필드의 덮어쓰기도 반영한다. 실행 시 전달되는 인자만 치환, 일반 방송 본문은 예시 표시. Unity recompile_status 완료, 관련 EditMode 4/4. origin/dev fetch, 열린 Editor 변경 보존으로 동기화 보류.
- 11/11 가이드 상단 Y -260 → -304. Unity에서 Guide 단일 프리팹 저장·재조회: 대기실 v2.8 날짜 표지 하단 -284, 간격 20 캔버스 단위. 생성 기본값도 일치시켰다. origin/dev fetch, 열린 에디터 변경 보존으로 동기화 보류.
- 리허설 HUD: 실제 그네 접근·대포 탑승 가능 조건에서 수정한 문구가 Guide이고 SystemEvent가 아님을 확인. 대포 이수 후 임무 제거까지 PlayMode 3/3, 조건 목록/기본 문구/변수 EditMode 1/1, 컴파일 오류·경고 0. git diff --check 통과. check-work는 기존 Task-097 todo.md 102줄 제한으로 중단. origin/dev fetch, 열린 Editor·기존 변경 보존으로 동기화 보류.
- 11/11 Confluence UI 시스템 고도화(38764627): ⑨ 공간 명판 역할·문구·배치 추가. 원본 영상과 분리해 Unity PlayMode 재현 화면 34개(리허설 10 / 카메라 4 / 대기실 20)와 설명을 오른쪽 열에 반영. 보관·줍기·내려놓기 성공, 침대 근접 및 2인 중 1인 준비 상태를 따로 재현. 촬영용 테스트 삭제. 페이지 재조회로 매체 수와 원본 보존 확인.
- 11/11 투표 Z/X 기본키·인게임 재지정·즉시 키 표시, 외줄/인간대포 공연 목표와 Guide 및 편집 프로필 적용. Unity 최종 컴파일 오류 0, InputBindingPreferences EditMode 21/21, 실제 두 공연과 투표 흐름 PlayMode 2/2, 역할 HUD PlayMode 5/5. check-work는 기존 Task-097 todo.md 102줄 제한으로 중단. doctor의 서브모듈·Task ID·에디터 탐지 경고와 별개로 실제 프로젝트 에디터 연결 확인. origin/dev fetch 완료, 열린 에디터와 진행 중 변경을 보존하여 브랜치 동기화 보류.
- 11/11 공연 HUD가 Settings/UI로 이동한 뒤 누락되던 런타임·편집 메뉴 공용 경로 수정. 기존 에셋 유지. 열린 Editor에서 실제 로드 경로 확인, 컴파일 및 StageHud_LiveRopeAndCannon PlayMode 1/1 통과. check-work는 기존 Task-097 todo 문서 길이로 중단. origin/dev fetch, 열린 Editor와 변경 보존으로 병합 보류.
- 11/11 공연 해금 제목·아이콘은 첫 줄, 본문은 실제 줄 수로 측정(말줄임 제거). 인간대포 안내를 짧게 편집하고 HUD 프로필 편집 목록에 등록. 로비 서명·시작, 추격 경보(호스트/복제)·포획, 탈출로 목표·가이드·장치와 귀환 버튼이 있는 결과 카드 적용. Unity 컴파일 오류 0, SpaceRoleHud PlayMode 4/4. 테스트 씬 캡처 Logs/HudPlayground/cannon-unlock-reward.png. check-work: 기존 Task-097-rehearsal-flow/todo.md 102줄로 중단. doctor 환경 탐지 실패와 별개로 열린 에디터에서 검증. origin/dev fetch 완료, 에디터와 미커밋 변경 보존으로 병합 보류.
- 11/11 출구층(절단기부터 문까지) 첫 진입을 호스트가 기록하고 기존 장치 스냅샷으로 공유. 출발 시 팀 임무 없음 → 첫 진입 후 탈출구 열기 → 개문 시 갈대밭 도착. 중간 참가 검증 통과. dev 85b86ed7로 fast-forward, 기존 미커밋 작업 재적용 및 충돌 10개 해결. 확정 없는 일정·수면, 시간대 방송과 대포 획득 투표·사망 규칙을 역할 HUD에 연결. 병합 후 실제 씬 회귀에서 발견한 개발용 직접 탈출과 대기 잠금 충돌도 수정. PlayMode 9개 개별 통과(실패 항목 수정 후 재실행 포함), 탈출 시작 날짜 EditMode 3/3. 원래 작업 사본은 autostash ead52883에 보존.
- check-work: 안전성·문서/규칙·Unity 컴파일 PASS(별도 실행한 PlayMode/EditMode 결과는 위 기록). 실행 중 채워진 두 TMP 글리프 캐시만 병합 직전 작업 사본과 동일하게 복구.
- 11/11 기능 커밋 23af8f0d를 fix/ui/layout-and-position에 푸시하고 [dev 대상 초안 PR #323](https://github.com/gaboja-studio/POC_CURTAINCALL/pull/323)을 열었다.
- 11/11 HUD 후속: 대기실·리허설 제목과 본문에 로컬 키 치환, Inspector에 기본 키 예시를 연결했다. 기존 `WaitingRoomHud.asset` 문구와 공용 버튼 변경은 보존했다. Unity로 Vote의 두 중첩 버튼에 자동 크기·줄바꿈을 저장했다.
- 표시 중인 제목·본문·배지가 같으면 기존 가이드를 유지한다. 공연과 대기실 사망을 우측 상단 위험 시스템 알림으로 표시하고, 공연 알림에만 호응도 안내를 넣는다. 대기실 진입 때 이미 사망한 단원은 다시 알리지 않는다.
- check-work 안전성·문서/규칙·컴파일 PASS. 별도 키 치환 EditMode 5/5, 역할 HUD·공연 종료·실제 대기실 사망 PlayMode 8/8. 초기 doctor의 서브모듈·설치 오류는 샌드박스 밖에서 실제 상태를 확인했고, Task ID 충돌은 기존 상태다.
- 11/12 리허설 문구: 외줄타기·인간대포 남은 제목·내용을 `WaitingRoomHud.asset` 덮어쓰기로 정리(가이드 `~합니다.`, 팀 임무 단답, 키 `[{key}]`), 구간 표시 번호 1~5 단일화(내부 ID 유지), 직접 작성 문구 오타·말투 정리.
- 11/12 Status 배경: 정사각형 인벤토리 슬롯(9-slice 없음, Simple)을 늘려 깨지던 문제를 smallplate 9-slice 판으로 교체, 빌더 `SliceStatusPlate` 추가. 인간대포 현재 구간 카드(CourseStatus)는 11-04 씬 정리 뒤 표시 조건(`sectionLabel`)이 비어 나오지 않았고, 핵심 정보가 아니라는 판단으로 사용자 승인 후 프리팹·매니저 연결·구간 표시 코드·플레이그라운드 선택·문서에서 제거. 컴파일·check-work PASS, PlayMode PerformanceSceneBinding 13/13, EditMode Hud 8/9·인간대포 654/658. 실패 5건은 변경과 무관: G01 덮어쓰기 가정 테스트, EscapeSwingDrive 3건, GroundSaw1C 레일 간격. 참고: `HumanCannonCourseSections`가 어느 씬에도 없어 로그 수집 인간대포 구간 값이 0으로 남는다(기존 문제).
