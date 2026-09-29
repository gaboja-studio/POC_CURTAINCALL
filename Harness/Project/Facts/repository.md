---
summary: 원격 저장소, 기본 브랜치, 브랜치·worktree 명명
status: active
updated: 2026-09-29
source: 2026-09-29 git remote -v 확인
---

# 저장소

## 언제 읽나

브랜치 생성, 커밋/푸시, worktree 생성, 선행 작업 병합 여부를 확인할 때.

## 사실

- 원격: `origin` → `https://github.com/gaboja-studio/POC_CURTAINCALL.git`
- 기본(Base) 브랜치: `dev`
- 작업 브랜치: `<feat|fix|refactor>/YYYYMMDD-NNN-slug`, 통합 브랜치: `integration/<기능-slug>` (전체 규칙: `Decisions/branch-strategy.md`)
- PM 전용 경로 지정: `.github/CODEOWNERS`
- 라벨: `bug`(버그 제보), `qa`(QA 검증 요청) — 이슈 양식이 자동으로 붙인다
- worktree 루트: `.env`의 `WORKTREE_ROOT`, 없으면 리포 상위 폴더의 `github-worktrees/POC_CURTAINCALL`
- Git LFS·Unity 병합 설정: `.gitattributes`

## 확인 방법

```
git remote -v
git symbolic-ref refs/remotes/origin/HEAD
```
