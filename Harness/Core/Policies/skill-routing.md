# Skill Routing

사용자가 아래처럼 말하면 해당 Skill 파일을 읽고 그 절차를 따른다. 표현이 조금 달라도 뜻이 같으면 같은 Skill이다.
Skill 위치: `Harness/Core/Skills/<name>.md`, `Harness/Engine/Unity/Skills/<name>.md`

## 작업자

| 사용자가 하는 말 | Skill |
|---|---|
| "Task-○○○ 시작해줘", "이어서 해줘" | `start-work` |
| (구현 요청) "커튼이 3초 동안 열리게 해줘" | `implement-code` (씬·프리팹 조작이면 `integrate-editor`) |
| "저장해줘" | `save-work` |
| "검사해줘" | `check-work` |
| "제출해줘" | `submit-work` |
| "버그 제보해줘", "QA 요청해줘" | `report-issue` |
| "오늘 여기까지", "인계 메모 써줘" | `write-handoff` |
| "막혔어", "이상해", "오류가 떠" | `investigate-bug` |
| "GitHub 연결해줘" | `setup-gh` |

## PM

| 사용자가 하는 말 | Skill |
|---|---|
| "○○ 통합 브랜치 만들어줘" | `start-integration` |
| "○○ feat Task 만들어줘, 담당은 A" | `scaffold-task` |
| "Task-○○○ 계획 세워줘" | `plan-task` |
| "Task-○○○ 인계 전 점검해줘", "인계해줘" | `handoff-to-member` |
| "○○ 통합 병합 진행해줘" | `merge-integration` |
| "○○ 통합 마무리해줘" | `close-integration` |

## 공통 규칙

- 작업자가 PM용 말을 하면 수행하지 않고 "PM 권한이 필요한 작업"이라고 알린다(`team-roles.md`).
- Skill 추가·삭제·수정은 PM만 한다.
