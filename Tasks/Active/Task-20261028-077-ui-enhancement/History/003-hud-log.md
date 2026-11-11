# HUD 초기 단계 · log

## 2026-10-28 하루 시계 v2.2 연결
- 지정 private 패키지의 Runtime/Editor를 원위치에서 사용했다. importer는 새 마스크 11개만 관리하고, 조립기는 기존 정본 공유 Sprite 6개를 참조하도록 경로·프로젝트 진입점만 보완했다. 원본 그림/폰트/문서/GUID는 보존했다.
- 공개 `DayClockProjectBuilder`로 `Resources/Prefabs/UIs/DayClock/W1_DayClock_v2_2.prefab`, `_Schedule.asset`, 전용 `_HourOutline.mat` 및 `Resources/Fonts/DayClock` Static TMP 3종을 생성했다. 공용 폰트 머티리얼 수정 없음. 같은 이름의 기존 출력은 덮어쓰지 않는다.
- `CommonWaitingRoomUI`에 새 프리팹을 직렬화 연결했다. 좌상단 (48,-24), 패키지 크기 192×208. 기존 W1/코드 생성 원·링은 사용하지 않고 자금/집결/알림/인벤토리는 유지한다. Start 씬 미저장 변경 없이 편집 API로 해당 씬만 저장했다.
- 일정 프로필 Start/End가 링·활성 구간·경계·바늘에 함께 반영된다. 오전/오후 반일 분리, 자정 경유, 수면 여집합 지원. 겹침/무효 시각은 마지막 유효본을 유지한다. 실제 남은 초는 호스트 런 상태를 쓰며 무제한과 미수신을 구분한다.
- BaseGameSettings Run에 아침/낮/첫날 가이드/밤 정비의 제한 없음·초 설정을 추가하고 기존 튜닝을 이관했다. 기본 300/300/300초·밤 무제한. 단계 진입 때 적용하며 집결/탈출/침대 종료를 유지한다. 유한 밤 만료는 잡힘 없이 기존 Dawn→전원 ACK→다음날 경로를 쓴다. enum Unlimited=4 추가, wire 필드 순서·개수 보존.
- 새 테스트의 Unity NUnit 미지원 NonParallelizable로 첫 컴파일 실패 후 해당 속성을 제거했다. 최종 컴파일 PASS. 대상 EditMode DayClock 7/7·RunTimeLimit 15/15·기존 RestTimeRules 2/2·DemoRunEnding 7/7 PASS. 대기실 PlayMode는 외부 잘못된 `editor status` 로그로 첫 1/2 실패 후 재실행 2/2 PASS(실제 프리팹·밤 무제한·재등록 포함).
- 확대 EditorTools 일괄 실행은 두 차례 61개 중 59 통과·2 실패로 집계됐으나 도메인 전환 뒤 상세 응답은 마지막 4개뿐이었다. 최초 socket 종료 뒤 재요청에서는 TestRunner PlayMode 정리 예외도 발생했다. 일괄 실행은 PASS로 기록하지 않으며 2개 실패의 상세 원인은 미확정이다. 클래스를 분리한 검사는 같은 61개 모두 PASS(DayClock 7, DemoRunEnding 7, DevStartPoint 14, DifficultySelection 2, HudLayout 2, NotificationTextLayout 3, PerformanceFeedback 1, RestTimeRules 2, RunTimeLimit 15, StartConnection 5, TightropeGuide 3). PlayMode 2개를 합쳐 개별 대상 63/63 PASS다. 검사 후 Start 씬 clean·EditMode·컴파일 오류 없음·300/300/300초·밤 무제한 복원 확인.
- check-work: 안전성/문서·규칙/컴파일 PASS, 기존 길이 경고 14건. 자동 테스트는 MCP로 별도 실행했다. doctor는 private 미추적으로 FAIL; private 새 패키지 2개 미추적은 유지하며 commit/push는 하지 않았다.
- 설정·계약·주의는 `Harness/Project/Facts/day-clock.md`에 기록했다. 최종 외형·2인 네트워크 플레이는 미검증이다.

## 2026-10-28 하루 시계 시간대 색상 구분
- 사용자 요청에 따라 실게임 `_Schedule.asset`의 아침/낮 휴식은 청록 #00A1BC, 오전/오후 공연은 주황·금 #C97E25, 밤 정비는 보라 #8846BB로 변경했다. 프리팹의 저장된 링 미리보기도 갱신했다. 수면·활성 단계 표시·시간 판정·실제 초 설정은 유지한다.
- 팔레트 검증은 어두운 표면 #111B20, 전체 쌍 기준으로 대비·정상 시각·색각 시뮬레이션 모두 PASS. 5개 시간대의 실제 Segment Image 색상이 프로필과 일치함을 임시 PreviewScene에서 검사했다. 3개 단계 렌더 이미지에서 색 구분·텍스트·배치 확인 후 PreviewScene을 닫았다. 실제 플레이 사람 확인과는 구분한다.
- 변경 후 DayClockTests EditMode 7/7 PASS. 색상/Start/End는 일정 프로필 Entries, 실제 초/제한 없음은 BaseGameSettings Run에서 설정하며 `day-clock.md`에 기록했다. 새 doctor 실패는 기존 private 미추적 변경 보존에 따른 것이며 commit/push는 하지 않았다.

## 2026-10-28 하루 전체 시간표 표시 수정
- 사용자 이미지 지적을 확인했다. 실제 일정은 08–10/10–12/12–14/14–16/16–20시로 맞지만, 기존 View가 오전/오후 720분 창으로 잘라 오후에 낮 휴식·오후 공연·정비·수면만 보였다. 이전 색상 검사는 이 누락을 놓쳤다.
- private View는 전체 compiled.intervals/actualBoundaries를 사용하도록 수정했다. 링 비율=지속분/1440, 경계·시침=분×0.25도. 조립기·공개 프리팹의 숫자는 위/오른쪽/아래/왼쪽 0/6/12/18시로 일치시켰다. 기존 색·일정·분침·실제 초/무제한 판정과 참조·GUID를 유지했다.
- DayClockTests EditMode 8/8 PASS. 추가 검사는 자정·오전·정오 직전/직후·오후·밤에도 명시 일정 5개+수면 2조각을 유지하고 실제 각도·비율·색·경계 6개·시침·숫자를 확인한다. 대기실 PlayMode 2/2 PASS(비동기 HTTP400 이후 test_status 최종 결과 확인). PreviewScene 오전/오후 렌더에서 동일한 하루 전체 링·다른 현재 위치를 확인했다. 사람 최종 플레이·2인 실플레이는 여전히 미검증이다.
- doctor는 기존 private 변경 보존으로 FAIL. private 코드 3개와 공개 시계 프리팹·테스트·문서만 이 요청에서 수정했으며 commit/push는 하지 않았다.

## 2026-10-28 최종 지시: 일반 12시간 시계 복원
- 사용자가 24시간 게이지를 거절했다. 직전 24시간 변경을 대체하여 숫자 12/3/6/9와 시침·경계 분×0.5도, 게이지 지속분/720, 현재 오전/오후 표시를 복원했다. 일정 시각·범주 색상·실제 초/무제한 판정은 유지한다. 조립기의 눈금도 복원하고 공개 프리팹은 에디터 API로 저장해 이후 생성도 일치시켰다.
- DayClockTests EditMode 8/8 PASS: 현재 반일의 구간·각도·색·경계, 정오 전환과 일반 눈금·시침 확인. 컴파일 PASS. 네트워크 포트 충돌 문의가 있어 상대 세션에 현재 EditMode·네트워크 테스트 미실행을 알렸다. PlayMode는 이번 묶음에서 생략했다. doctor는 기존 private 변경 보존으로 FAIL. commit/push는 하지 않았다.

## 2026-10-28 버벅임·낮 시계 회색 구간 조사
- 최초 EditMode·Start clean·Profiler 미수집 상태. 시계 Show 1만 회의 단독 CPU 경로는 약 46.8ms였으나 렌더 비용이 빠져 있어 전체 성능 판정에는 쓰지 않았다. 기존 Dev 모드의 실제 DevStartLauncher로 2일차 Between/외줄을 실행하고 대기실 진입·화면을 확인했다. 최초 실행 eval timeout 뒤 동일 요청을 중복 실행하지 않고 정상 진입 완료를 확인했다.
- CPU Profiler 180프레임: 중앙값 16.68ms, p95 20.63ms, 최대 118.88ms. 같은 큰 프레임의 TelemetryRuntime.UploadLoop 코루틴 109.45ms. 추가 300프레임에서 최대 132.85ms와 해당 UploadLoop 118.64ms를 재현했다. CommonWaitingRoomUI.LateUpdate는 첫 표본 평균 0.054ms/최대 0.084ms, Canvas.BuildBatch 평균 0.010ms/최대 0.117ms다. 에디터·Profiler 부하 포함 수치이며 사용자의 모든 끊김 원인 확정으로 확대하지 않는다.
- 읽기 전용 보조 조사에서 UploadLoop의 yield 전 파일 탐색·읽기·요청 JSON/UTF-8 생성, 응답 후 ACK의 파일 재독출·역직렬화·해시·삭제가 메인스레드 동기 경로임을 확인했다(TelemetryRuntime.cs 307–344, DurableOutbox.cs 88–128). SendWebRequest의 네트워크 대기는 yield하므로 동기 네트워크 대기로 오해하지 않는다. 하위 개별 연산 중 어느 것이 지배적인지는 미확정이며 업로드 비활성화·동시성 수정은 하지 않았다.
- 실제 NoonBreak 스냅샷에서 오후 창은 12–14 청록, 14–16 금색, 16–20 보라, 20–24 수면 회색 #405158였다. 지난 일정 회색 처리는 없으며 오전 일정은 오후 창에 포함되지 않는다. 시계 형식·색상 규칙·기능 코드는 변경하지 않았다.
- 조사 종료 후 ProfilerDriver/Profiler 기록을 원래 꺼짐으로 복원하고 직접 시작한 Play를 종료했다. doctor는 기존 private 미추적으로 FAIL. 자동 회귀 테스트는 코드 변경이 없어 실행하지 않았다. 다음 수정 범위는 텔레메트리 준비/ACK의 작업 스레드 분리와 공유 Outbox 상태 보존 검증이며, 이번 응답은 원인 조사 결과다. commit/push 없음.

## 2026-10-28 순간 끊김 수정
- 사용자 요청에 따라 측정된 UploadLoop 경로만 수정했다. 시계·HUD·게임 규칙·네트워크 전송 스키마는 변경하지 않았다. doctor는 기존 private 변경 보존으로 FAIL 1건이며 이번 수정의 컴파일·실행을 막지 않았다.
- DurableOutbox의 봉인 파일 탐색·읽기·요청 JSON/UTF-8 준비와 ACK JSON/ID/개수/해시 재검증·삭제를 Task.Run으로 이동했다. Runtime은 프레임을 양보해 완료를 기다리고 UnityWebRequest·상태/오류 표시는 메인스레드에서 처리한다. 전송 권한은 준비 완료 후 다시 확인한다.
- active Append/Seal의 반환 전 영속화·복구 계약은 유지했다. worker가 쓰는 현재 실행 우선순위 집합은 동시성 컬렉션으로 교체하고, 준비 읽기·ACK 삭제·relay 재수신은 동일 봉인 파일의 비차단 게이트를 공유한다. 삭제와 겹친 용량 집계는 사라진 파일만 제외한다. 중단/종료 취소, 잘못된 ACK/실패 시 원본 보존·재시도를 유지한다.
- 읽기 전용 보조 검토에서 준비 읽기와 relay 삭제의 Windows 파일 공유 충돌 가능성을 찾아 준비 읽기도 같은 게이트로 보호했다. 새 테스트 최초 17개 중 16 통과·1 실패는 relay fixture의 이벤트 필수 필드 누락이었다. 유효 이벤트로 수정 후 DurableOutboxTests 17/17·TransportPolicyTests 7/7, 최종 24/24 PASS. 기존 복구·우선순위·ACK 테스트와 비동기 준비/동시 Append·Seal/변경된 내용/취소/경합 재시도/저장소 오류 7개를 포함한다.
- 실제 DevStartLauncher로 같은 2일차 Between/외줄 대기실에 진입해 플레이어·시계·HUD 화면을 확인했다. 600프레임: 중앙값 16.64ms, p95 20.45ms, 전체 최대 32.18ms, UploadLoop 최대 0.328ms(샘플 있는 32프레임). 별도 900프레임: 중앙값 16.60ms, p95 20.17ms, 전체 최대 31.62ms, UploadLoop 최대 0.910ms(36프레임). 표본 모두 40ms 초과 프레임 없음. 수정 전 109.45/118.64ms UploadLoop 정지는 이 표본에서 재현되지 않았다.
- 실제 업로드 ACK 시각이 두 확인 사이 전진했고 LastError 없음 확인. endpoint/token/실제 payload는 출력·기록하지 않았다. 에디터·Profiler 포함 제한된 표본이며 모든 렉·빌드 성능 해결로 확대하지 않는다. Append/Seal 동기 저장과 다른 씬·2인 장시간 플레이는 별도 잔여 확인이다. PlayMode 자동 네트워크 회귀는 이번 묶음에서 생략했다.
- 검증 종료 후 직접 시작한 Play를 종료하고 ProfilerDriver/Profiler를 꺼짐으로 복원했다. Start 씬 1개·dirty false·EditMode 확인. git diff --check PASS(기존 UI 파일 EOL 경고만). 지식은 Telemetry/upload-coroutine-stutter.md에 기록했다. commit/push 없음.

## 2026-10-28 v2.4 디자인 교체
- 사용자 확인: 입력 버벅임은 많이 개선되었다. 이후 요청은 디자인만 교체이므로 성능·게임 판정은 추가 변경하지 않았다. doctor FAIL 1건은 기존 private Dirty 보존이며 작업 진행을 막지 않았다.
- 새 `curtaincall_dayclock_v2.4`는 동일 namespace/type을 포함하고 프로젝트 확장 무제한/API는 없었다. 원본 C#·문서·PNG를 보존하고 비활성 define constraint assembly로 중복 컴파일을 제외했다. 기존 v2 런타임/직렬화 GUID와 일정 계산을 유지하며 optional HalfDayTMP·아이콘 색만 보완했다.
- 기존 조립기에 v2.4 레이아웃/새 그림/명판 9-slice를 적용했다. 새 공개 `W1_DayClock_v2_4.prefab`·눈금 머티리얼과 `Fonts/DayClock/V24` Static TMP 3종(오전/오후 포함)을 생성했다. 기존 `_v2_2_Schedule.asset`을 그대로 참조하며 이전 프리팹·폰트·공유 그림을 삭제/덮어쓰지 않았다.
- 대기실 씬은 PrefabUtility.ReplacePrefabAssetOfPrefabInstance의 ByName 매칭으로 교체했다. 기존 루트/DayClockView 컴포넌트 참조·부모·형제 순서 보존을 확인하고 크기 224×248·좌상단 (48,-24)로 저장했다. Resources 경로도 새 프리팹으로 변경했다.
- 최종 EditMode DayClockTests 17/17(기존 8+반일 경계/미수신 7+디자인/실제 씬 참조 2), RunTimeLimitTests 15/15, PlayMode WaitingRoomHudPlayTests 2/2 PASS. 초기 시계 실행은 컴파일 전의 옛 192×208 기대값으로 7/8이었고 AssetDatabase.Refresh 후 최신 17개 전체 통과했다. PlayMode 호출 HTTP400에도 test_status에서 2개 최종 성공을 확인했다. recompile/초기 eval의 일시적 HTTP400/timeout 후 상태를 확인하고 진행했다.
- 실제 WaitingRoomBase를 Play하고 StartOfflineRun→EndIntro→EndPerformance로 낮 휴식에 진입했다. Game View 1920×1080에서 외부 일차/오후 명판·큰 실제 카운트다운·새 바늘/낮 아이콘·기존 범주 색상·좌상단 배치 렌더를 확인했다. 집결→공연 종료로 밤 정비에 진입하여 제한 없음·달 아이콘·16시 바늘·오후·시계 1개를 확인했다. 실행만을 위한 게임 상태는 Play 종료로 폐기하고 Start 씬 clean으로 복원했다. 2인 실플레이·최종 사람 외형 확인은 별도다.
- 설정 위치·중복 코드 제외·기존 프로필 보존은 Facts/day-clock.md에 갱신했다. check-work 안전성/문서·규칙/컴파일 PASS. 문서 길이 경고 15건(Task077 log 경고 추가), hard 0. 스크립트 테스트 SKIPPED와 별도 MCP 34/34 통과를 구분한다. private 미저장 4건은 보존했다. commit/push 없음.
