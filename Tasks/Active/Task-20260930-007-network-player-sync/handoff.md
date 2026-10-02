# Handoff

## 지금 상태

- 단계: ①~⑤·#9·#10·줄 밖 착지 추락 확인·저장(2026-10-02). ⑤: `PlayerState`, `NetworkPlayer.RequestState`·`ServerCanChangeState`·`State`·`StateChanged`·`ServerRestartAll`·`Restarted`, 추락→조작 잠금·남의 화면 래그돌, `TestRoundRestart`(전원 추락 3초 후), HUD `RestartOverride`=모두 재시작 요청. 다음 ⑥·⑦
- ① 결과: `PlayerSync/NetworkPlayer.cs`(호스트가 자리 번호 0~3 배정·모든 화면에서 출발 위치에 세움, `Local`·`LocalSpawned`·`All`), `PlayerSpawnPoints.cs`, `PlayerMover.SetStartPose` 추가. 에디터: `Player.prefab`+NetworkObject·NetworkPlayer, NetworkManager `PlayerPrefab`=Player, `DefaultNetworkPrefabs`+Player, 테스트 씬에 Ground·PlayerSpawnPoints(줄 간격 2.0m, 4개)·SessionUI·임시 카메라 위치.
- ② 결과: `NetworkPlayer`가 남의 캐릭터면 `PlayerInputReader`·`PlayerController`·`PlayerBalance`를 끄고 `PlayerMover.Simulated=false`. `LocalPlayerCamera`(Cinemachine 리그 `Resources/Prefabs/Controllers/Camera/PlayerFollowCamera.prefab`, 뒤 4.5m·위 2.6m·가슴 1.2m 주시·FOV 60), `LocalPlayerViews`(게이지·HUD 대상=내 캐릭터, 자동 찾기 끔). 004 게이지·HUD에 `Target`·`AutoFindTarget` 추가. 테스트 씬: Brain·카메라 리그·BalanceGauge·DebugHud·LocalPlayerViews·LaneGuides(줄 표시)
- 방향: 테스트 캡슐 대신 004 실제 플레이어 프리팹에 온라인 적용. 이후 Task가 이 공개 기능을 씀 (`Harness/Project/Decisions/feature-with-network.md`). 참고: `Docs/Domains/player-control.md`(004 공개 함수·혼자 기준 주의점), `Docs/Domains/network-session.md`(`NetworkSessionManager`)
- 막힌 것: 없음 / PR: 없음 (이슈 #9·#10은 이 PR에서 닫음)

## 다음 할 일

1. ⑥ 래그돌 연출 정리(⑤에서 각자 화면 래그돌 동작함 — 남은 것 확인) → ⑦ 도메인 지도·`player-control.md` 갱신 → 검사 → 공동 테스트 → 제출.
2. 이후 `plan.md` 단계 ④~⑦ 순서대로. 저장 전 MPPM 가상 플레이어를 끈다(아래 확인 요청).
3. 공용 파일(플레이어 프리팹·UI·NetworkManager 프리팹) 수정은 `setup.md` 배정 범위 안에서만.

## PM이 확인할 것

- 확인 요청: `verify-unity.ps1`(저장 검사)이 MPPM 가상 플레이어(`Library/VP/…`)도 같은 경로 에디터로 세어 "에디터 2개, 모호"로 멈춤(beta.12 `unity status`가 VP를 표시). 끄고 저장함. 스크립트 수정은 PM 하네스 작업(교훈 제안: 함정)
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
