# 042 리프트 변경 검증 — 2026-10-16

## 결정·구현

- 사용자 초기 정상 작동 피드백 후 높은 출발대·2인 이상 과승 시 대칭 양문 추락·첫 구간 안전 바닥/계단 요청. 질문 응답 “모든 리프트에 적용”으로3대에 같은 규칙 적용.
- 출발4m/첫 코스0m/안전 바닥-4m, 계단40단(단차0.2m/깊이0.4m), 양문0.35초·빈 복귀 최소1.2초는 시험값이다.
- LiftTrapState는 과승·개방시각·정지 진행률·빈 복귀를 관리하며 route snapshot으로 공유한다. 이동 중에도 운반 고정을 풀고 양문 Collider를 해제한다.
- 각 리프트의 고정 지면을 비우고 실제 타일 Collider로 안전 착지를 판정한다. 검토에서 발견한 첫 도착 직전 z16~18 바닥도 제거했다.
- 열린 리프트의 낙하 영역에서는 트램폴린 로컬 접촉과 호스트 반동 승인도 막는다.

## 자동 검사

- doctor는 같은 작업 최초1회 FAIL0/WARN2 결과를 사용했다.
- check-work -Mode PlayMode: scope FAIL13, 이후 문서/컴파일/테스트 SKIPPED.
- 위반13은 순차 전환 위임 문서8개+기존 폰트/MPPM2개+사용자 PlayMode 폴더/설정 메타 포함3개다. 수동 감사에서 그 외 구역 밖 변경 없음.
- 기존 폰트·MultiplayerManager·NewPlayModeScenario의 시작/종료 hash 동일. 신규 구현은 배정 HumanCannon 코드/씬과 Task042 문서 안이다.
- 별도 컴파일 PASS(up_to_date). refresh 직후 첫 연결 검사는 domain reload 중 오류였고 ready 확인 후 재검사 성공.
- EditMode241/241 PASS(failed/skipped/inconclusive0): 기존184+상태42+배치/문8+씬3+이동 중 낙하3+반동 차단 영역1.
- PlayMode NO_PROJECT_TESTS. 자동 PlayMode 통과로 간주하지 않는다.

## 실행 결과와 한계

- LAN 호스트 실제 로컬1명+PlayerPrefab을 owner999로 Spawn한 합성 두 번째 인원. 실제 원격 참가 프로세스는 없다.
- 첫 대기 리프트 과승: open=true, left270/right90, 로컬은 안전 바닥 y-3.92에 Normal 착지.
- 혼자 트램폴린 F 잡기→발판 F로 Moving/도착 확인. 이동 중 추가 인원은 IsRiding=false/Simulated=true로 운반 고정을 풀고 안전바닥에 착지, 비워지면 Waiting/출발점 복귀.
- 최종 코드로3대 각각 open=true·좌우270/90·로컬 낙하·낙하 영역 반동 차단=true·인원을 뺀 뒤 reset=true/Waiting 확인.
- 계단 아래(-19,-3.95,6)에 배치 후 가상 Keyboard S를 Dynamic update마다 입력해 점프 없이 (-19,4.08,-11.51)까지 올라갔다. Normal 유지.
- 초기 위치/추가 승객은 fixture다. 실제2~4인 조작감·지연·원격 운반·접속 해제·부상 접근·전체 연속 플레이를 대체하지 않는다.
- .harness/local/task042-lift-runtime-console.json: 이번 실행 구간109105 이후 새 게임 Console 오류0. CLI eval 임시 delegate 타입 오류1회는 실행 전 수정했으며 프로젝트 컴파일 오류가 아니다.
- 최초 가상 키 단발 주입이 소실되는 경우가 있어 연속 Dynamic 주입으로 계단 검사. InputSettings 변경 없음. Play 중 코드 수정은 하지 않았다.
- SceneView 배치 이미지 .harness/local/task042-lift-layout.png. PNG/meta는 Assets 밖으로 이동했다.
- Stop 후 임시 Keyboard/합성 인원 제거, 소유 씬 저장 dirty0. 커밋·push·PR 미실행.

## 사람 확인

1. 첫 발판2명→양문 추락→안전바닥→왼쪽 계단 복귀→혼자 F 재출발.
2. 나머지2대·이동 중 추가 탑승도 확인하고 호스트/참가자 역할 교환.
3. 높인 출발대에서 첫 받기·쉼터 합류/승강·불꽃·R/재접속을 연속 플레이. 실제4인 결과는 전체 통합 후 기록.
