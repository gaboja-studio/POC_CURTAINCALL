---
name: start-work
description: plan과 todo로 작업을 시작·재개한다. 인계·상태·구역 승인은 없다.
---

# start-work

1. 현재 브랜치·미저장 변경을 확인하고 기존 작업을 보존한다.
2. Task plan·todo를 읽는다. 특별한 문제나 이전 결과가 필요하면 log·History를 읽는다.
3. `pwsh -File Scripts/start-work.ps1 -Id <Task-ID> [-Base <시작 기준>] [-Fetch]`.
4. 같은 브랜치는 미저장 변경을 보존하고 이어 간다. 새 브랜치는 선택 기준에서 만든다. 다른 미저장 작업이 있으면 별도 작업 공간을 사용한다.
5. 인계 확인·Jira·Status·앞 Task 완료·작업 구역을 착수 조건으로 요구하지 않는다.
6. 현재 목적을 구현하며 plan·todo를 갱신한다.
