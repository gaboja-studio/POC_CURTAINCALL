---
summary: dev 공용 변경을 공간에 내려보낼 때 열린 PR이 없는 공간 개인 브랜치는 GitHub "Update branch"를 쓸 수 없다. 담당자가 git pull로 받거나 PM이 앞당기기·merges API로 반영한다
status: active
updated: 2026-10-14
source: Task-20261014-033
---

# PR 없는 공간 개인 브랜치는 Update branch가 없음

## 언제 읽나

공통 통합 → dev → 공간 통합(`integration/<공간>`)으로 공용 변경을 내려보낸 뒤, 공간 작업 브랜치(`feat/*`)까지 최신화하려 할 때.

## 증상

- git-workflow는 "작업 브랜치는 GitHub Update branch로 받는다"고 하지만, 아직 PR을 열지 않은 브랜치에는 그 버튼이 없다.
- AI가 남의 브랜치에 직접 push하려 하면 권한 확인에서 막힌다(작업자는 자기 브랜치에만 push).

## 원인

Update branch는 PR 화면의 기능이다. 공간 브랜치는 PR 없이 오래 작업하는 경우가 많다.

## 대처

- 기본: 담당자가 자기 브랜치에서 `git pull origin integration/<공간>` 한 뒤 push한다.
- PM이 대신할 때(2026-10-14 처리 방식):
  - 브랜치 자체 커밋이 없으면 앞당기기만: `git push origin origin/integration/<공간>:refs/heads/feat/<브랜치>` (앞당기기가 아니면 거부되어 안전).
  - 자체 커밋이 있으면 서버에서 합치기: `gh api -X POST repos/<owner>/<repo>/merges -f base=feat/<브랜치> -f head=integration/<공간>`.
  - 반영 뒤 담당자에게 `git pull`을 알린다.
- 먼저 `git merge-tree --write-tree origin/feat/<브랜치> origin/integration/<공간>`으로 충돌이 없는지 확인한다. 충돌이 있으면 담당자와 함께 해결한다.

## 확인 방법

`git rev-list --count origin/feat/<브랜치>..origin/integration/<공간>`이 0이면 반영된 것이다.
