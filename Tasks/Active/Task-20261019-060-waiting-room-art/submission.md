# 제출 점검 — 2026-10-20

- 상태: 제출 보류, PR 미등록. 자동 검사 실패와 씬 충돌의 PM 조정 필요.
- 저장된 아트: `ff15645`, `origin/feat/waiting-room-art` 업로드 완료.
- 대상: `dev`(Task 메타의 PM 승인 예외), 확인한 원격 SHA `3a2bc88`.

## PR 초안

제목: `[feat] Task-20261019-060 대기실 그레이박스 실제 에셋 고도화`

- 대기실 그레이박스에 프로토타입의 실물 가구와 테마에 맞춘 천장·조명·안개를 적용하고, 공간 규모와 배치를 정리했습니다.
- 구조도를 기준으로 숙소를 구성하고 WaitingRoom 원본 침대 4개로 교체한 뒤, 폐기물 보관소와 소품실을 순차 구성했습니다. 최신 단계에서는 상점·현재 천막·무대 입구 통로를 보존했습니다.
- 에셋 교체 이력과 화면은 [Confluence 작업노트](https://hrjoo122770.atlassian.net/wiki/spaces/bYmkRlnpusng/pages/23855106)에 기록했습니다. 기존 외부 재질 변경 26건은 제출 범위에서 제외했습니다.

관련 Jira: CC-142 (`setup.md` 기준).
GitHub Issue: 추가 지정 없음.

## 검사 결과

- 구역·문서·컴파일 PASS. 저장 전 EditMode 573/573 PASS.
- 제출 검사 PlayMode: 총 96, 통과 91, 실패 3, 생략 2. 전체 PASS 아님.
- 실패: `PerformanceLinkPlayTests.SamePerformanceSpace_AdditiveRemovesPreviousConfiguredScene` — 탑승 후 자유 3인칭 전환 시간 초과.
- 실패: `SpaceLinkPlayTests.Additive_KeepsHubAndSwapsSpaceScenes` — True 기대값 불일치(200행).
- 실패: `SpaceLinkPlayTests.Single_FollowsPhasesAndKeepsRunState` — `DummyWaitingRoom` 기대, 실제 `WaitingRoomBase`(140행).
- 실패 원인은 확정하지 않았으며 공용 시스템 코드는 수정하지 않았습니다.
- 생략 2건: 별도 로컬 봇 실행파일이 필요한 2인 검사.
- 상세 자동 결과(로컬): `.harness/local/test-results-playmode.json`.
- 기존 대기실 확인: 동선 8구간/408샘플 새 막힘 0, 깨진 참조 0, 1인 호스트 보관소·소품실 바닥/카메라·선반 접근 거리 1.177m, 기능 판자 4칸 높이 유지. 호스트 전 기존 공용 카메라 오류 재현, 4인 공동 테스트 미실행.

## 통합 전 조정

- `git merge-tree --write-tree --name-only HEAD origin/dev` 사전 비교에서 `Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity` 내용 충돌 확인.
- 작업 파일을 바꾸는 실제 merge는 하지 않았습니다. 충돌 표식·병합 중 상태는 없습니다.
- dev의 Task-065(PR #191)가 추가한 게임플레이 구역·탐색 범위·출구 트리거와 현재 아트 배치를 함께 검토해야 합니다.
- 현재 수동 배치를 보호하기 위해 이전 전체 생성 도구를 일괄 재실행하지 않습니다.
- `submit-work` 규칙: 0~3단계 FAIL이면 제출 중단, 씬·프리팹 충돌은 AI가 해결하지 않음. 해결 후 재검사와 PR 초안 사용자 확인이 필요합니다.
