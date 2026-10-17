---
summary: 브랜치 전환 전에 public/private Git·LFS를 준비하고 실패 시 사용자 파일을 보존한다.
status: active
updated: 2026-10-17
source: Task-20261017-053
---

# 외부 에셋 안전 브랜치 전환

## 언제 읽나

브랜치 전환·FF 뒤 외부 에셋이 빠졌거나 `start-work` 등이 안전 전환을 차단할 때.

## 증상

공개 브랜치만 바뀌고 `_ThirdParty` HEAD가 다르거나 LFS 포인터·누락 파일이 남는다. 과거 잔여물 정리는 비추적 파일과 수정된 `.meta`까지 지울 수 있었다.

## 원인

전환 뒤 다운로드하면 수신 실패 전에 이미 HEAD가 바뀐다. `.git` 존재만으로 준비 완료를 판단하거나 `submodule.recurse=true`에 맡기면 실제 파일 상태를 놓친다.

## 대처

- `Invoke-SafeBranchTransition`은 대상 SHA의 public/private Git·LFS를 먼저 받아 캐시 hash를 확인한다. 중간 브랜치 tip도 준비한 뒤 switch/FF·캐시 반영·실파일 검증을 한다.
- 재귀·smudge 억제는 명령별로만 적용한다. 사용자 Git 설정, 비추적 파일, `.meta`, 기존 잠금을 자동 삭제·복구하지 않는다.
- dirty·숨긴 index 변경·Git 진행 상태·잠금·살아 있는 Git/LFS 프로세스가 있으면 중단한다. 다른 저장소의 프로세스도 보수적으로 차단할 수 있다.
- 협조적 잠금은 외부 직접 Git 실행까지 막지 못한다. 같은 폴더에서 전환·편집을 동시에 하지 않는다.
- 미초기화·submodule 추가/제거·중첩 구조는 별도 준비가 필요하다. gitlink가 있는 새 worktree 생성도 이번 경로에서 차단한다.
- 전환 후 실패는 자동 rollback하지 않는다. 현재 양쪽 HEAD와 파일 상태를 먼저 확인하고 PM에게 복구 판단을 요청한다.
- private 바이너리 씬 속성과 공개 gitlink 변경은 별도 승인 대상이다. 이 하네스 수정이 private 에셋 수정까지 적용한 것은 아니다.

## 확인 방법

- `Get-ThirdPartyState`로 Ready/Uninitialized/HeadMismatch/MissingFiles/LfsIncomplete/Dirty/Error를 구분한다. 전환은 hash까지 확인하고 doctor는 크기·상태를 확인한다.
- `Scripts/Tests/third-party-branch-sync.test.ps1 -ScratchRoot <프로젝트 밖 경로>`는 작은 local Git/LFS fixture로 검사한다. 실제 private 다운로드·worktree·팀 공동 테스트를 대체하지 않는다.
- 전체 작업 검사는 `Scripts/check-work.ps1`. Unity 연결 실패를 컴파일 통과로 표시하지 않는다.
