## 2026-11-05 대포 주변 낙사·세 갈래 외줄 보수
- 사용자 재현은 2인 호스트·참가자 모두. 시작 시 origin/dev fetch: 44f4a4af를 현재 877bc0f7이 포함하고 작업 트리는 깨끗했다. 추가 병합 없이 진행했다. doctor는 기존 중복 Task ID FAIL·CLI 버전 WARN이고 이번 에디터/컴파일은 사용 가능했다.
- 일반 이동도 공연 Running 상태면 외줄의 1m 깊이를 적용하던 경로를 복합 씬에서 제외했다. A04B 대포대는 인접 줄보다 6m, A06 대포대는 8m 낮아 정상 발판에서도 조건이 참이었다. 실제 외줄 조작·균형 실패·공연/리허설 바닥 실패·A의 심부/컨베이어 낙사 영역은 유지한다.
- A05/A06 3줄을 2m 평행 간격, 같은 시작/끝으로 정렬했다. 중앙 줄은 각각 5→8.5m, 16→19m로 연장하고 기존 공유 메시를 바꾸지 않고 배칭 Transform·케이블·고정부를 맞췄다. 줄 15개의 길이 배열(기존 11개)과 A04A 상부 줄의 오래된 12m도 실제 15.108m에 맞췄다. B/C 씬·외부 에셋은 변경하지 않았다.
- `TightropeSurfaceControlTests` 55/55(새 4개 회귀 포함), `FallVoidTests` 13/13 통과. 실제 A의 두 3줄 묶음에서 양방향/앞뒤 이동 중 점프와 도착 좌표 일치를 검사했다. 착지 구역 52곳 468표본의 낙사 영역 침범 0, 정상/부상 점프 캡슐 264표본의 벽·기물 겹침 0. 기존 낙사 경계(-46/-56m) 위는 비살상, 경계/심부는 살상으로 유지된다.
- 증거: `.harness/local/cannon-surface-tests.json`, `cannon-fallvoid-tests.json`, `cannon-platform-void-audit.json`, `cannon-lane-clearance.json`. 첫 씬 수정 eval은 기존 길이 배열 11개를 넘겨 중단되어 배열을 확인·확장한 뒤 정상 저장했다. 실제 Play/2인 통신 시험은 미실행이며 사용자가 확인한다.
- 사용자가 저장·제출을 요청했다. 최종 검사 후 작업 브랜치 commit/push 및 dev PR 제출, 직접 병합하지 않는다.
- 첫 check-work는 추가 지식 문서가 60줄 제한을 넘어 문서 검사에서 중단됐다. 이번 원인·검증을 `escape-cannon-platform-lanes.md`로 분리해 기존 기록을 보존하고 다시 검사한다.

이전 기록: [모든 그네 참가자 보수 전 로그](History/041-before-swing-client-log.md).

