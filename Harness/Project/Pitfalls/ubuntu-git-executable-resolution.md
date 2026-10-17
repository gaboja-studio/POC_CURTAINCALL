---
summary: Ubuntu CI에서 Git 경로가 여러 개 반환될 때 실행 파일 하나만 선택한다.
status: active
updated: 2026-10-17
source: Task-20261017-055
---

# Ubuntu Git 실행 경로 선택

## 언제 읽나

PowerShell Git mock 회귀가 Windows에서는 통과하지만 Ubuntu에서 명령을 찾지 못할 때.

## 증상

PR #147의 verify-fast job에서 integration-sync 첫 Created 검증이 `/usr/bin/git /bin/git`을 실행할 수 없어 실패했다. 문서 검사 자체는 성공했으며 뒤의 workspace-flow는 실행되지 않았다.

## 원인

`Get-Command git -CommandType Application`이 복수 실행 경로를 반환할 수 있다. `.Source`를 바로 받으면 배열이 되고 `&`에 단일 실행 파일로 전달할 수 없다.

## 대처

- `(Get-Command git -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source`로 첫 실행 파일 하나만 선택한다.
- `Scripts/Lib/common.ps1`의 Invoke-TransitionGit과 기존 workspace-flow·third-party-scope 회귀도 이 단일 선택 패턴을 사용한다.
- 테스트에서 실제 선택식을 읽어 검사한다. 검증용으로 수정식을 복제하면 원래 코드의 회귀를 놓칠 수 있다.

## 확인 방법

- `Scripts/Tests/integration-sync.test.ps1 -ScratchRoot <프로젝트 밖 경로>`에서 초기·mock 선택식 각각 후보 1개/2개, 첫 경로의 단일 문자열·Git 실행 성공을 확인한다.
- 격리 fixture는 사용자 파일·index·HEAD·private 보존과 외부 쓰기 경계도 검사한다.
- Windows 모사 회귀는 실제 Ubuntu CI가 아니다. 로컬 Ubuntu에 pwsh가 없으면 ENV_BLOCKED로 남기고 후속 PR의 GitHub Actions 전체 job을 확인한다. 실제 동기화 PR 운용·공동 테스트와도 구분한다.
