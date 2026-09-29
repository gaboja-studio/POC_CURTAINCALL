---
name: qa-feature
description: 합의된 공동 테스트 항목으로 기능을 검증하고 결과를 기록한다. 발견한 문제를 몰래 고치지 않는다.
---

# QA Feature

1. Task의 `plan.md` 공동 테스트 항목, `setup.md` 테스트 씬, handoff를 읽는다.
2. 항목별로 정상 동작, 경계 상황, 실패 후 복구, 주변 기능 영향을 점검 목록으로 만든다.
3. 자동 확인(`check-work`)을 먼저 하고, 사람이 해야 하는 Play 확인 순서를 1, 2, 3으로 안내한다.
4. 완료 조건을 바꾸거나 발견한 문제를 몰래 고치지 않는다.
5. 문제는 "상태/입력 → 실제 결과 → 기대 결과 + 증거"로 적고, 필요하면 `report-issue`로 버그 이슈를 만든다.
6. 통합 단계라면 결과를 `Integrations/Active/Integration-*/checklist.md` 공동 테스트 표에 적는다(PM).
