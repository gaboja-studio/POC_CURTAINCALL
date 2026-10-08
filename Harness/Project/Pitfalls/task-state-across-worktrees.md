---
summary: doctor의 Task 상태 충돌이 서로 다른 worktree의 보관 시점 차이인지 확인할 때
status: active
updated: 2026-10-05
source: Task-20260930-006
---

# Worktree별 Task 상태 차이

## 언제 읽나

통합 브랜치에서 Task를 Done으로 보관한 뒤, doctor가 다른 작업 브랜치의 Active 복사본과 상태 충돌이라고 보고할 때.

## 증상

같은 checkout에는 중복이 없는데 여러 Task ID가 동시에 `task-ids` FAIL로 표시된다.

## 원인

`Get-AllTaskFolders`는 모든 등록 worktree를 열거한다. ID만으로 상태를 비교하면 서로 다른 브랜치의 정상적인 Active/Done 복사본을 실제 중복으로 오인한다. 병합 중에는 HEAD와 index·작업 디렉터리의 보관 위치도 다를 수 있다.

## 대처

`Scripts/doctor.ps1`은 동일 ID 안에서 Worktree별 Active/Done 동시 존재를 검사한다. 동일 ID의 서로 다른 폴더명 충돌은 전역으로 계속 검사한다. 공용 열거 함수를 현재 checkout 전용으로 바꾸거나, 오탐 해소를 위해 다른 worktree의 Task를 삭제하지 않는다.

## 확인 방법

`pwsh -File Scripts/doctor.ps1` 실행. 서로 다른 worktree의 상태 차이는 허용하되 동일 worktree Active+Done 및 전역 폴더명 충돌은 계속 FAIL이어야 한다. 이번 수정은 합성 사례 6개와 실제 Task 복사본 123개로 확인했다.
