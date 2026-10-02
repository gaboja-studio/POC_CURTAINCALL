# Handoff

## 지금 상태

- 단계: 작업 시작(2026-10-02, working). 구현 전, ①단계 합의 대기. Unity 에디터 연결됨
- 시작 시점 상태: 007 코드 없음, `Player.prefab`에 NetworkObject 없음, NetworkManager `PlayerPrefab` 비어 있음, 테스트 씬은 카메라·조명만. 004 혼자 기준 부분은 `plan.md` "시작 시점 상태"
- 방향: 테스트 캡슐 대신 004 실제 플레이어 프리팹에 온라인 적용. 이후 Task가 이 공개 기능을 씀 (`Harness/Project/Decisions/feature-with-network.md`). 참고: `Docs/Domains/player-control.md`(004 공개 함수·혼자 기준 주의점), `Docs/Domains/network-session.md`(`NetworkSessionManager`)
- 막힌 것: 없음
- PR: 없음

## 다음 할 일

1. ①단계(네트워크 플레이어 생성·출발 위치) 변경 내용을 작업자와 합의 → 구현 → 컴파일 → MPPM 확인.
2. 이후 `plan.md` 단계 ②~⑦ 순서대로. 단계마다 완료 기준을 작업자가 확인.
3. 공용 파일(플레이어 프리팹·UI·NetworkManager 프리팹) 수정은 `setup.md` 배정 범위 안에서만.

## PM이 확인할 것

- 확인 요청: unity-cli beta.12 변경점을 `Harness/Engine/Unity/Facts/unity-cli.md`에 기록하고 `Pitfalls/pipeline-command-drift.md`·`unity-test-editor-open.md`를 손봄(사용자 지시로 이 Task 브랜치에서 수정)
- 대기 중인 결정: 래그돌 물리 결과까지 모두에게 같게 맞출지(기본: 상태만 공유, 연출은 각자)
- 공용 파일 요청: 없음

## Verification

- 구역 검사: 미실행
- 1 문서/규칙: 미실행
- 2 컴파일: 미실행
- 3 테스트: 미실행
- 4 실행 로그: 미실행
- 5 공동 테스트: 미요청
