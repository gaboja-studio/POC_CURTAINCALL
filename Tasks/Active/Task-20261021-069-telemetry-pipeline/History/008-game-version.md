# 게임 버전별 로그·대시보드

2026-10-22 사용자 지시. 게임 버전은 0.1.0, 기획자에게 쉬운 핵심 보고·최소 검사·두 PC 보류를 유지했다.

## 변경

- Player Settings의 bundleVersion은 이미 0.1.0이었다. 설정/씬 변경 없이 EmitFor에서 Application.version을 LogEvent.gameVersion으로 고정한다.
- 버전은 배치 schemaVersion=1과 별개다. 과거 무버전/기존 pending 원문은 소급 변경하지 않는다. 재전송·복구 시 발생 버전을 보존한다.
- Events는 기존 A~U열 그대로 두고 V열 gameVersion만 추가한다. 기존 21열 행은 빈 버전으로 해석하며 중복 비교에 버전도 포함한다. 같은 eventId에서 버전만 변경해도 충돌로 거절한다.
- 대시보드의 환경·게임 버전 필터가 묘기/사망/상호작용/구역/이용률/18단계 시간/자금 평균/런 검색/타임라인·도식에 함께 적용된다. 버전 변경 시 런·공연 선택을 비운다.
- 선택지는 해당 환경의 관측 버전과 기본 0.1.0에서 구성한다. 최초 조회는 숫자 부분 기준 최신, 무버전은 UNVERSIONED(화면 ‘버전 미기록’). 선택한 버전이 비어도 다른 버전으로 대체하지 않는다.
- LIVE 요약 5개 시트도 버전별로 집계한 뒤 마지막 gameVersion 열을 붙인다. Drive 원본은 같은 폴더에 누적하며 폴더/시트 수를 버전마다 늘리지 않는다.
- Apps Script 대시보드·수신 모두 코드 v12(02:05 KST)로 같은 배포 ID/URL에 갱신. 나만 대시보드·기존 모든 사용자 POST+token·30분 트리거·OAuth 범위 유지. 코드 v12와 게임 0.1.0은 별개다.
- Drive 설치 소스 code.txt/dashboard.html.txt는 같은 ID와 parents로 갱신했다.

## 최소 검사

- Doctor: 권한 있는 조회 WARN(FAIL 0). sandbox의 third-party/에디터 숨김과 구분한다. 사용자 Start Server 후 ready 확인.
- origin/dev 기준 Scope PASS(91개). 기본 check-work는 dev에서 수신한 구역 때문에 FAIL(77건), 그 결과를 전체 통과로 바꾸지 않는다. Fast PASS(기존 길이 경고).
- Unity 컴파일 PASS, outbox EditMode 8/8, 계약 EditMode 8/8, 기존 격리 재전송 PlayMode 한 건 1/1. 새 로그 0.1.0 및 복구 후 동일 값, 실패 원문 보존·실제 Google ACK·동일 원문 재POST 확인.
- Node 계약·집계·화면 문법·서버 49/49. 첫 실행은 테스트 Sheet mock의 열 오프셋 미지원으로 1건 실패했고 mock을 수정한 뒤 전체 0.1초 재실행했다.
- 실제 scheduledRefresh 완료 및 Events 760행에 TEST 검증 eventId 1행·gameVersion 0.1.0 확인. 동명 중복 적재는 없음.
- Google 웹 프레임의 빈 화면 이후 사용자 ‘0.1.0 버전 선택 나온다’ 확인. 실제 TEST/0.1.0 선택에서 갱신 완료 1건 및 과거 ‘버전 미기록’ 선택지 표시를 확인했다. 서버·HTML 저장 소스 readback도 로컬과 일치한다.
- 실제 프로세스 강제 종료·PC 전체 오프라인·두 PC 플레이는 반복하지 않았다. 씬/리소스/프로젝트 설정·기존 원천 12개 추가 변경·커밋/push 없음.

## 증거

- batchId ae66ee58-e4a4-4661-b9d6-b91368e14c94, eventId 024e790a-e9a9-46da-8938-18a16497ef80.
- SHA256 453696bc4d6b52f477ae68d933309f0aa7de5836ee4aa81b61a26c810719de13.
- .harness/local/telemetry-version-{contract,playmode}-20261022.json, telemetry-version-replay-proof.json.
- .harness/local/telemetry-code-v12.txt. token/OAuth/환경 변수 원문은 출력하지 않았다.
- 실제 화면에서 TEST/버전 미기록 758건 ↔ 0.1.0 1건 전환 확인, 0.1.0으로 복귀. 화면 증거 .harness/local/telemetry-version-v12.png.
