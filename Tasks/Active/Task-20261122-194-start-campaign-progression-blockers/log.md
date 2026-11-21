# Log

- 2026-11-22. DEV 8b0fa5db 기준. 이전 미제출 작업은 로컬 stash 06b64c5a에 보관했다. B·C, F8, 튜토리얼과 에셋은 제외했다.
- 탈출로 A. 연결을 끊은 참가자는 NGO 씬 시간 초과 집계에서 제외한다. 완료 메시지를 놓친 참가자의 재확인에 응답한다. 기존 아바타와 배치 대상을 구분하고 좌표를 최신 소유자 위치로 확인한다.
- 정식 묘기. 늦은 GameState에서도 등록을 준비한다. 초기 배치가 끝난 아바타만 옮기고 시작 부상을 기록한다. 움직임을 잠근 아바타가 바뀌거나 전환이 중단되면 기존 조작 상태를 복구한다. 인간대포는 진입한 씬의 출발점을 우선하고 이전 시도 결과를 이번 시도와 분리한다. 외줄을 재사용하면 시도 장부와 시작 준비를 새로 연다.
- 기존 비공개 블록 에셋 HEAD 불일치 및 중복 과거 Task ID는 이번 범위 밖이며 그대로 보존했다.
- EditMode 20건 통과, 실패 0건. 신규 Start 경로 사례 17개와 변경된 아바타 교체 조건 3개를 실행했다. 결과 .harness/local/start-campaign-audit-verified.xml.
- check-work 범위와 문서·규칙 통과, 컴파일은 별도 EditMode로 확인. 실제 Start부터 팀 플레이는 하지 않았다.

- 2026-11-22 PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/477 (fix/start-campaign-progression-blockers → dev)
- DEV 검토용 [PR #477](https://github.com/gaboja-studio/POC_CURTAINCALL/pull/477)로 제출했다.
