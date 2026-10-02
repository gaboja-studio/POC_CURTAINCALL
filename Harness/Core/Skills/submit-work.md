---
name: submit-work
description: 작업자의 제출 요청. 검사 → 최신 통합 내용 합쳐 보기 → 미리보기·확인 → PR 등록(feat/refactor는 integration, fix/resource는 dev 또는 integration. .github 템플릿 사용).
---

# Submit Work

형식 규칙: `Harness/Project/Decisions/pr-issue-format.md`

1. Unity에서 저장했는지 확인한다. 현재 브랜치가 `feat|fix|refactor|resource/*`인지 확인한다(아니면 멈춤).
   - Task 없는 `resource/*`·급한 `fix/*`: 2·3번 대신 `save-work`로 검사·저장하고, 대상(`dev` 또는 `integration/<slug>`)을 사용자에게 확인해 `new-pr.ps1`에 `-Base <대상> -Title "<요약>"`을 넘긴다(구역 검사는 스크립트가 함). 급한 fix는 요약에 이유와 범위를 적는다.
2. 검사: `check-work` Skill을 수행한다. 0~3단계 중 FAIL이 있으면 쉬운 말로 알리고 제출을 멈춘다.
3. 최신 통합 내용 합쳐 보기: `git fetch origin` → `git merge origin/<meta.md Integration>`
   - 충돌이 나면 `git merge --abort`로 되돌리고 PM에게 넘긴다. 씬·프리팹 충돌은 AI가 해결하지 않는다.
   - 합친 뒤 컴파일에 영향이 있었으면 2번을 다시 한다.
4. 사용자에게 묻는다:
   - 작업 내용 요약: AI가 비개발자도 알 수 있게 1~3줄 초안을 쓰고 확인받는다.
   - 관련 이슈 번호: 없으면 "없음". (JIRA는 `setup.md`에서 자동으로 채워짐)
   - 직접 Play로 확인한 내용(검사 결과의 "직접 플레이 확인" 칸)
5. 미리보기: `pwsh -File Scripts/new-pr.ps1 -Summary "<요약>" [-Issues 12] -PlayCheck "<확인 내용>" -Push -DryRun`
   - 제목·본문을 보여 주고 **사용자 확인**을 받는다. `[문제]` 중 "-Push로 올리기" 외에는 먼저 해결한다.
6. `setup-gh` Skill로 gh 상태를 확인한다.
7. 등록: 5번 명령에서 `-DryRun`을 뺀다(웹페이지 방식으로 하기로 했으면 `-Browser`).
   - 웹페이지 방식이면 사용자가 Create를 누른 뒤 PR 링크를 받아 handoff의 `PR:`을 채우고, meta Status를 `submitted`로 바꾼다.
8. handoff를 갱신하고 `save-work`로 저장한 뒤, PR 링크를 전한다.
