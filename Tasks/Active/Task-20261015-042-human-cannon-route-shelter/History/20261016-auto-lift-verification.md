# 042 자동 승강·쉼터 낙차 — 2026-10-16

## 결정과 구현

- 사용자: 모든 승강기는1명 탑승 시 초록색→1.5초 뒤 자동 이동. 쉼터 도착자가 아래 승강기로 점프하면 닿지 않고 추락할 배치 요청.
- 추가 응답 “전원 도착 조건만 유지”: 쉼터 스위치/F 출발 제거, 생존자 전원 도착 조건은 유지. 최신 지시가 이전 F 기획을 대체한다.
- LiftBoardingState가 같은 탑승자/자격을1.5초 유지해야 Ready. 이탈·점프·과승·합류 조건 상실·R 시 취소하고 새 카운트를 요구한다.
- 장치 소지 여부와 무관하게1인 자동 탑승. 쉼터 승강 대기자를 제외한 다른 Normal 생존자 전원 도착을 검사한다. 고정3명 조건은 없다.
- 호스트가 탑승/과승/출발 확정. snapshot은 header24/transit83/fire9B, Boarding Rider/시각·Occupied 추가. 이동/도착 운반도 장치 소지와 분리했다.
- 바닥 Renderer2개의 MaterialPropertyBlock만 초록으로 표시. 다른 장식/공유 머티리얼 불변, 하차 시 원래 파랑 복원.
- 쉼터/이후 구간을2m 올려6m. 하부(-25,0,40)→상부(-18,6,48)로 대각 승강, 상부는 쉼터 왼쪽 가장자리와 접한다.
- 하부 발판 x[-28,-22],z[36,44], 쉼터 x[-15,15],z[42,54]. 접근 통로 x[-28,-15],z[33,36],y0. 첫 관문 z32 뒤 울타리만 z33..36 출입구를 냈다.
- 일반 점프의6m 하강 수평거리4.744m, 캡슐/skin 양끝 여유 포함5.504m. 리프트와의7m/통로와의6m 틈을 넘지 못한다. 쉼터 측면 낙하 공간에는 고정 지면이 없다.
- 쉼터 앞쪽에서 기존 코스로 되돌아가는 모든 경로를 막지는 않았다. 요청한 하부 승강기 방향의 직접 점프를 차단한다.

## 자동 검사

- doctor는 같은 작업 최초 FAIL0/WARN2를 유지, 중복 실행 안 함.
- check-work -Mode PlayMode: scope FAIL13, 후속 SKIPPED. 위임 문서8개+기존 폰트/MPPM/PlayMode 에셋·메타5개. 이번 구현은 배정 코드·씬·Task 문서 안이다.
- 기존 폰트·MPPM·NewPlayModeScenario 시작/종료 hash 동일. 기존 변경 보존, commit/push/PR 미실행.
- 별도 컴파일 PASS(up_to_date), EditMode269/269 PASS(failed/skipped/inconclusive0). 이전241+대기 상태23+씬4+색상1.
- 최초268/269의 색상 검사 실패는 PropertyBlock float 왕복 오차였다. 0.00001 허용오차로 바꾸고 전체269 통과 확인.
- PlayMode NO_PROJECT_TESTS. 별도 문서 검사는 handoff 참조. 소유 씬 저장/dirty0, Missing Script0.

## 실행 검증

- 단일 LAN 호스트 실제 로컬1명 + owner999 합성 플레이어. 초기 배치 fixture, 실제 원격 프로세스 없음.
- 첫 리프트: 탑승0.4초 Waiting/counting/green, 이탈 후 Waiting/not counting/blue. 재탑승1.25초 Waiting/잔여0.25, 1.5초 뒤 Moving, Arrived 위치(-9,0.05,22).
- 쉼터: 0/1 미합류로2초 이상 Waiting. 1/1 도착 때 카운트 시작, 위 인원 이탈 시 취소. 재합류 후 새1.5초를 기다려 자동 Moving→Arrived(-18,6.05,48).
- 초기 쉼터 fixture 시도는 불꽃에 합성 인원이 사망해0/0이 된 경우, 원격 보간이 위치를 Spawn으로 되돌린 경우라 전원 조건 증거에서 제외했다. R로 Normal을 복구하고 합성 인원의 NetworkTransform 보간/이동을 끈 뒤 위 결과를 다시 확인했다.
- 쉼터 재출발: 2인 과승은 trap=true/countdown=false/Waiting으로 우선 처리. 비운 후 복귀, 혼자 다시 타1.5초 후 자동 이동/Arrived(-9,6.05,68), R 초기화.
- 실제 Input System 가상 A+Space: 쉼터(-14.3,6.08,42.5)에서 하부 발판 방향으로 점프해 하부 발판에 닿지 못하고 Fallen(-25.40,-43.18,42.50). 초기 위치만 fixture, 공용 이동/점프/추락은 실제 경로다.
- .harness/local/task042-auto-lift-runtime-console.json, 이번 구간109175~109217 새 게임 Console 오류0. 과거 전역 buffer 오류 카운트와 구분한다.
- .harness/local/task042-auto-lift-layout.png에서 쉼터·측면 승강기·출입구·후속 구간 배치를 확인했다. 캡처 PNG/meta는 Assets 밖으로 옮겼다.
- Stop 후 임시 Keyboard/합성 플레이어 제거. InputSettings/공용 Player/Settings는 바꾸지 않았다.

## 사람 확인

- 2클라이언트로 초록/1.5초·하차 취소·과승을 확인하고 담당 호스트/참가자를 교환한다.
- 쉼터 전원 도착 전 대기/합류 후 자동 상승, 올린 쉼터까지 첫 받기/봉/반동 연속 진행과 부상 접근은 사람 확인 필요.
- 실제4인 공동 테스트·원격 지연/재접속은 미실행. 단일 호스트 fixture로 PASS를 대체하지 않는다.
