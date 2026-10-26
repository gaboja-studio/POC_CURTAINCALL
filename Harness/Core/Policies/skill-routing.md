# Skill Routing

역할별 호출 제한은 없다. 아래 표현과 뜻이 같으면 해당 Skill을 따른다.
위치: `Harness/Core/Skills/`, `Harness/Engine/Unity/Skills/`.

| 요청 | Skill |
|---|---|
| Task 만들어줘 | scaffold-task |
| Task 시작해줘 / 이어서 해줘 | start-work |
| 계획 잡아줘 | plan-task |
| 구현·기능 수정 | implement-code (씬·프리팹이면 integrate-editor) |
| 저장해줘 | save-work |
| 검사해줘 | check-work |
| 제출해줘 | submit-work |
| 버그·QA 등록해줘 | report-issue |
| 오늘 여기까지 / 인계 메모 | write-handoff (log 갱신) |
| 막혔어 / 이상해 / 오류 | investigate-bug |
| GitHub 연결해줘 | setup-gh |
| 통합 브랜치 만들어줘 | start-integration (선택) |
| 인계해줘 / 인계 전 점검 | handoff-to-member (목적·남은 일 안내, 별도 승인 없음) |
| 통합 병합 진행해줘 | merge-integration |
| 변경 받아줘 / 동기화 PR | sync-integration |
| 통합 마무리해줘 | close-integration |
| 급한 예외 적용해줘 | urgent-exception (과거 구역 예외는 불필요) |

스킬·MCP 사용·추가를 PM 전용으로 제한하지 않는다. 외부 쓰기·저장·병합은 사용자가 요청한 범위에서 한다.
