---
name: start-work
description: 작업자의 작업 시작·이어서 하기. 내 Task 브랜치로 이동하고 최신 내용을 받은 뒤 인계 메모로 할 일을 정리한다.
---

# Start Work

1. Unity에서 저장했는지 사용자에게 먼저 확인한다.
2. `pwsh -File Scripts/start-work.ps1 -Id <Task-ID>`
   - 스크립트가 하는 일: 원격 최신 받기 → 작업 브랜치로 이동(없으면 원격에서 가져옴) → 최신 내용 반영 → 상태를 `working`으로 표시.
   - 저장하지 않은 변경이 있으면 멈춘다. 사용자에게 "저장해줘"를 먼저 할지 묻는다. AI가 임의로 변경을 버리지 않는다.
   - Task ID를 모르면 사용자에게 묻는다(PM이 인계 때 알려 준 번호).
3. `pwsh -File Scripts/doctor.ps1 -SkipEditor`로 환경을 점검한다. FAIL이면 쉬운 말로 알리고 멈춘다.
4. 읽는 순서: `handoff.md` → `meta.md` → `setup.md` → `todo.md` → `plan.md`.
5. 사용자에게 3줄로 보고한다:
   - 이 Task의 목표
   - 내 작업 구역(테스트 씬, 스크립트 폴더)
   - 다음 할 일(handoff의 "다음 할 일")
6. 공동 테스트 항목(`plan.md`)이 비어 있으면 작업 전에 PM과 합의하라고 알린다.
