---
summary: 에셋 공유용 sparse worktree의 루트 파일 누락과 정리 실패를 방지한다
status: active
updated: 2026-10-27
source: Task-20261027-072 actual helper worktree and local Git fixtures
---

# 에셋 공유용 sparse worktree 정리

## 증상
공유 PR은 생성되지만 `dev-pr-*` 임시 worktree가 남고 루트 파일들이 삭제 상태로 표시된다.

## 원인
`worktree add --no-checkout` 후 `read-tree HEAD`는 인덱스만 채운다. 이어 sparse 설정을 해도 이미 인덱스에 있는 루트 파일이 디스크에 없어서 `worktree remove`가 거부한다. 실패 코드를 무시하면 공유만 성공한 것처럼 보인다.

## 대응
빈 인덱스에서 sparse 설정을 먼저 하고 `read-tree -mu HEAD`로 인덱스·파일을 함께 채운다. 삭제는 force 없이 실행하며 실패를 사용자에게 표시한다. 회귀에서 숨김 루트 파일까지 포함하고 worktree 목록·실제 폴더가 모두 사라졌는지 검사한다.

병합 후 브랜치는 도우미 완료 기록·예상 커밋·통합 여부·사용 중 worktree를 확인한 뒤 정리한다. 게임 작업·복구 브랜치와 변경된 임시 폴더는 보존한다.
