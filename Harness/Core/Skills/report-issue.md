---
name: report-issue
description: 버그 제보·QA 요청. .github/ISSUE_TEMPLATE 폼의 칸을 쉬운 질문으로 채우고, 미리보기·확인 후 이슈를 등록한다.
---

# Report Issue

형식 규칙: `Harness/Project/Decisions/pr-issue-format.md`

1. 종류와 칸 확인: `pwsh -File Scripts/new-issue.ps1 -List`
   - "버그" → `bug`, "QA/검증 요청" → `qa`. 그 밖의 템플릿도 목록에 나온 이름으로 쓴다.
2. 채울 수 있는 칸은 먼저 자동으로 채운다:
   - QA `branch`: 현재 브랜치 전체 이름
   - QA `scene`: Task `setup.md`의 테스트 씬
   - QA `steps`: Task `plan.md`의 공동 테스트 항목 (1, 2, 3 순서로)
   - Bug `situation`: 대화나 Console/Editor.log에서 확인한 재현 순서
   - `references`: 관련 Task 번호, PR 링크, 로그 요약
3. 비어 있는 **필수 칸만** 쉬운 질문으로 하나씩 묻는다. 예:
   - "어떤 문제가 있었는지 한 문장으로 알려 주세요."
   - "문제가 생기기 전에 무엇을 눌렀는지 순서대로 알려 주세요."
4. 제목은 핵심 한 줄로 쓴다. 접두어(`[BUG]`, `[QA]`)는 스크립트가 붙인다.
5. 여러 줄 내용은 JSON 파일(`.harness/local/issue-fields.json`, `{ "id": "내용" }`)로 만들어 넘긴다.
6. 미리보기: `pwsh -File Scripts/new-issue.ps1 -Type <종류> -Title "<제목>" -FieldsFile .harness/local/issue-fields.json -DryRun`
   - 보여 주고 **사용자 확인**을 받는다. `[문제]`(필수 칸 누락 등)가 있으면 다시 묻는다.
7. `setup-gh` Skill로 gh 상태를 확인한다.
8. 등록: `-DryRun`을 빼고 실행한다(웹페이지 방식이면 `-Browser`). 이슈 링크를 사용자에게 전하고, 관련 Task가 있으면 `log.md`에 1줄 남긴다.
