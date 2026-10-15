---
name: start-work
description: 작업자의 작업 시작·이어서 하기. 내 Task 브랜치로 이동하고 최신 내용을 받은 뒤 인계 메모로 할 일을 정리한다.
---

# Start Work

1. Unity에서 저장했는지 사용자에게 먼저 확인한다.
2. `pwsh -File Scripts/start-work.ps1 -Id <Task-ID>`
   - 이동/상태 변경 전에 assigned/working만 허용(후속 scaffolded·submitted/integrated/done 차단). 원격 최신→브랜치 이동→최신 내용 반영→working.
   - Active Task 선택값이 있는 통합만 최신 원격 일치·실전달 todo·최신 배정 문서 유입을 검사한다. 준비 브랜치 존재/기술 배정만으로 시작하지 않는다. 기존 통합에 순차 게이트를 소급 강제하지 않는다.
   - 저장하지 않은 변경이 있으면 멈춘다. 사용자에게 "저장해줘"를 먼저 할지 묻는다. AI가 임의로 변경을 버리지 않는다.
   - Task ID를 모르면 사용자에게 묻는다(PM이 인계 때 알려 준 번호).
3. 시작 시 `AGENTS.md`의 doctor를 아직 실행하지 않았다면 한 번 점검한다. 이미 실행한 같은 작업에서는 재호출하지 않는다. doctor의 에디터 경고 또는 `-SkipEditor` 통과는 필수 Unity 컴파일 검증 결과가 아니다. FAIL은 원인과 함께 보고하고 멈춘다.
4. 읽는 순서: `handoff.md` → `meta.md` → `setup.md` → `todo.md` → `plan.md`.
5. 사용자에게 3줄로 보고한다:
   - 이 Task의 목표
   - 내 작업 구역(테스트 씬, 스크립트 폴더)
   - 다음 할 일(handoff의 "다음 할 일")
6. 공동 테스트 항목(`plan.md`)이 비어 있으면 작업 전에 PM과 합의하라고 알린다.
