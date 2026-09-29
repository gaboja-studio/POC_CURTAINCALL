---
summary: PR·이슈 제목 형식, PR 본문 칸, JIRA·이슈 번호 기록 위치, 이슈 자동 닫기 규칙
status: active
updated: 2026-09-29
source: human (2026-09-29 PR·이슈 등록 방식 결정)
---

# PR·이슈 형식

## 언제 읽나

PR이나 이슈를 만들 때, `.github` 템플릿을 고칠 때, 통합(integration)을 마무리할 때.

## 결정

- 2026-09-29 / PM:
  - 기준 템플릿은 `.github/PULL_REQUEST_TEMPLATE.md`, `.github/ISSUE_TEMPLATE/*.yml`이다(GitHub가 이 경로만 인식). 스크립트가 실행할 때마다 템플릿을 읽으므로, 템플릿을 고치면 바로 반영된다.
  - PR 제목: `[종류] Task번호 한 줄 요약` — 예: `[feat] Task-20260929-001 커튼 열기`
    - 통합 → dev: `[integration] 기능 묶음 이름`, dev → builds: `[build] 버전`
  - 이슈 제목: `[BUG] 한 줄 요약`, `[QA] 한 줄 요약` (각 yml의 `title:`이 기준)
  - PR 본문 칸: 작업 내용 요약 / 관련 JIRA 이슈 / Issue 번호(`closed #번호`) / 검사 결과
  - JIRA 번호는 PM이 Task 준비 때 `setup.md`의 JIRA 칸에 모두 적는다. PR에 자동으로 들어간다.
  - 이슈 폼의 칸마다 `id`를 둔다(웹페이지 방식에서 칸 미리 채우기용).
  - 등록 수단: `Scripts/new-pr.ps1`, `Scripts/new-issue.ps1`. gh가 준비되지 않으면 `setup-gh` Skill로 최대 2회 시도하고, 그래도 실패하면 사용자 동의 후 웹페이지 방식으로 등록한다.

## 이유

- 비개발 작업자는 AI에게 말만 하면 되고, 직접 쓰더라도 같은 모양이 된다.
- GitHub는 **기본 브랜치(dev)에 병합될 때만** `closed #번호`로 이슈를 닫는다. 그래서 통합을 마무리할 때 각 Task PR의 `closed #번호`를 dev PR 본문에 모은다(`Templates/Integration/checklist.md`).

## 바꾸려면

템플릿의 칸 제목(`## …`)을 바꾸면 `Scripts/new-pr.ps1`의 제목별 채우기 규칙(요약·JIRA·Issue·검사)도 확인한다.
