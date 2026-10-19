# Integration-finished-records-cleanup

- **Feature:** 끝난 작업·통합 기록 정리
- **Status:** to-dev
- **Branch:** integration/finished-records-cleanup
- **Base:** dev
- **Active Task:**
- **PM:** @MoHoDu
- **Merge Time:** 미정 — PM이 dev PR을 직접 병합
- **Updated:** 2026-10-18

Status 순서: preparing(Task 준비) → working(작업 중) → merging(순차 병합) → testing(공동 테스트) → to-dev(dev로 PR) → done. 막히면 blocked.

## 목표

- dev의 진행 중 목록(Tasks/Active, Integrations/Active)에 실제로 진행 중인 기록만 남긴다. 이미 dev에 반영됐거나 취소된 기록은 근거와 함께 Done으로 옮긴다.

## Tasks

| Task | 종류 | 담당 | 브랜치 | PR | 상태 |
|---|---|---|---|---|---|

- 새로 배정한 Task 없음. PM 마무리 문서 전용 브랜치이며 대상과 근거는 result.md에 적는다.

## 작업 구역

- Task 039·051·052의 폴더 이동과 meta/handoff/todo/log 갱신.
- 통합 기록 harness-main-stage-workspaces·start-lobby·merged-task-records·document-project·parent-child-sync-pr·third-party-branch-sync의 폴더 이동과 meta/result 갱신. 이미 Done인 performance-scene-link의 정리 체크 갱신.
- 코드·씬·에셋·설정·private·다른 Task는 수정하지 않는다.

## 공용 파일 소유 표

| 파일 | 소유 Task | 이유 |
|---|---|---|

## Decisions

- Open: None
- Decided: 2026-10-18 — 사용자 승인 목록대로 정리. 공연장 작업자가 쓰는 Integration-main-stage·Integration-main-stage-human-cannon과 Task 025·040~044는 그대로 둔다(040~044는 인간 대포 통합 마감 때 정리).
- Decided: 2026-10-18 — Done은 dev 반영·취소 확인 기록이며 공동 테스트 PASS가 아니다. 취소된 통합은 cancelled 유지. 브랜치 삭제는 하지 않는다.
- Decided: 2026-10-18 — 사용자 요청으로 저장·push·dev PR 등록·온라인 검사 확인까지 진행. 병합은 PM이 직접 한다.
