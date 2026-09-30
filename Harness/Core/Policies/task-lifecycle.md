# Task Lifecycle

## Task 단위

**Task 하나 = 공동 테스트에서 "통과"를 말할 수 있는 한 묶음 = 작업자 한 명.**
범위가 늘거나 확인 중 새 요구가 나오면 현재 Task에 붙이지 않고 PM이 새 Task로 만든다.
합치기 단계에서 기능끼리 연결하는 작업도 별도 **연결 Task**로 만든다.

## 식별자와 위치

- ID: `Task-YYYYMMDD-NNN`. **PM만** `Scripts/new-task.ps1`로 발급한다(작업 트리와 로컬·원격 ref의 Active/Done Task ID를 확인해 번호 중복 방지).
- 폴더: `Tasks/Active/Task-YYYYMMDD-NNN-slug/` → 완료 후 `Tasks/Done/`
- 브랜치: `<feat|fix|refactor>/<slug>`, 소속 `integration/<기능-slug>`에서 만든다. Task 연결은 `meta.md`의 Branch 값을 기준으로 한다.
- 통합 기록: `Integrations/Active/Integration-<기능-slug>/` (PM 관리)

## 파일

| 파일 | 내용 | 작성 | 기본 읽기 |
|---|---|---|---|
| `handoff.md` | 지금 상태 / 다음 할 일 / PM이 확인할 것 (덮어쓰는 스냅샷) | 담당자 | O (가장 먼저) |
| `meta.md` | 종류·담당·브랜치·목표·결정 | PM → 담당자 | O |
| `setup.md` | JIRA, 작업 구역, 공용 파일, 수정 금지, 인계 체크, 테스트 씬 처리 | **PM** | O |
| `todo.md` | 체크리스트 | 담당자 | O |
| `plan.md` | 계획, 공동 테스트 항목 | 담당자(PM 합의) | O |
| `log.md` | 중요한 사건 1줄 기록 | 담당자 | X |
| `History/` | 한도를 넘어 옮긴 과거 내용 | 담당자 | X |

크기 한도와 압축: `context-budget.md`.

## 상태

`scaffolded` → `assigned` → `working` → `submitted` → `integrated` → `done` (어디서든 `blocked` 가능)

| 상태 | 뜻 | 누가 바꾸나 |
|---|---|---|
| scaffolded | PM이 폴더·씬·브랜치를 준비함 | PM |
| assigned | 인계 체크 완료, 담당자에게 전달 | PM |
| working | 작업 중 | 담당자 ("시작해줘") |
| submitted | `integration/*`로 PR 제출 | 담당자 ("제출해줘") |
| integrated | 병합 후 공동 테스트 통과 | PM |
| done | `dev`에 반영, Task 폴더를 Done으로 이동 | PM |

## 단계별 책임

1. **준비(PM)**: `new-task.ps1`로 Task·브랜치 생성 → Unity에서 테스트 씬·스크립트 폴더 생성 → `setup.md` 작성 → `plan.md` 공동 테스트 항목 합의 → 인계 체크.
2. **작업(담당자)**: `setup.md` 작업 구역 안에서만 수정. 공용 파일이 필요하면 PM에게 요청.
3. **제출(담당자)**: 검사(`verification-ladder.md` 1~4단계) → 최신 integration 합쳐 보기 → PR.
4. **통합(PM)**: 순차 병합 → 공동 테스트(5단계) → 실패 항목은 후속 Task.
5. **마무리(PM)**: Task를 `Tasks/Done/`으로 이동, 테스트 씬 유지·삭제는 **사람이 결정**해 `setup.md`에 기록(스크립트는 씬을 지우지 않음), `dev`로 PR.

테스트가 없으면 `NO_PROJECT_TESTS`로 적고 통과했다고 주장하지 않는다.
