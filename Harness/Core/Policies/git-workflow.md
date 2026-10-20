# Git Workflow

비개발 작업자도 몇 가지 말만으로 작업하고, 합칠 때 문제가 없게 하는 브랜치 규칙이다.
결정 기록: `Harness/Project/Decisions/branch-strategy.md`. 권한: `team-roles.md`.

## 브랜치 종류

| 종류 | 용도 | 만드는 사람 | 합치는 곳 | 이름 |
|---|---|---|---|---|
| `dev` | 팀 공용 원본 | — | — | `dev` |
| `integration/*` | 기능 묶음 합치기 | PM | 메타 Base(`dev` 또는 부모 통합) | `integration/<기능-slug>` |
| `feat/*` | 기능 추가 | PM(준비) | `integration/*` | `feat/<slug>` |
| `fix/*` | 완성된 기능 수정 | PM(준비). 급하면 바로 | `dev` 또는 `integration/*` | `fix/<slug>` |
| `refactor/*` | 동작 유지, 코드 정리 | PM(준비) | `integration/*` | `refactor/<slug>` |
| `resource/*` | 리소스 추가 (Task 없음) | 아트·에셋 관리자 | `dev` 또는 `integration/*` | `resource/<분류>` |
| `builds/*` | 빌드 버전 보관 | PM | 없음 (dev에서 받기만) | `builds/<버전>` |
| `tests/*` | 임시·실험 | 누구나 | **없음** (끝나면 삭제) | `tests/<자유>` |

작업 브랜치의 slug는 목적을 나타내는 소문자 kebab-case로 쓴다(예: `feat/project-overview`). 날짜·Task 번호·관리 코드는 이름에 넣지 않는다. 같은 종류/slug는 중복 배정하지 않으며, Task ID·폴더명은 유지하고 `meta.md`의 Branch로 연결한다.

## 흐름 (한 방향)

```
feat/* · refactor/*  ──PR(Squash)──▶ integration/* ──PR(Merge commit)──▶ dev ──PR──▶ builds/*
fix/* · resource/*   ──PR(Squash)──▶ dev 또는 integration/*
dev                  ──PR(Merge commit)──▶ integration/*   (공용 변경 내려받기)
```

- `feat/*`, `refactor/*`는 **항상** `integration/*`로만 제출한다. 작은 기능도 PM이 작은 integration을 만들어 받는다.
  - 예외: PM이 담당자 한 명의 단일 Task로 승인하고 Task `meta.md`에 `Integration: dev`를 선언하면 `dev`로 바로 Squash PR한다(`Get-PrFlow`가 head 브랜치의 Task 선언을 확인). Task 폴더는 그 작업 브랜치에서 PM이 준비하고, 병합 후 PM이 Done으로 옮긴다.
- `fix/*`, `resource/*`는 새 기능 개발이 아니므로 통합 순서를 지키지 않고 `dev` 또는 특정 `integration/*`로 바로 PR할 수 있다.
- 공용 변경은 `dev → Base=dev 통합` 또는 **선언된 부모 → 직계 자식 통합** PR로 내려받는다(`sync-integration`, Merge commit). 임의 통합 간 동기화·자식의 dev 직행은 금지한다. 기존 일반 작업 브랜치는 GitHub **Update branch**로 받는다.
- 명시적 준비 브랜치만 **소속 통합 → 그 Task 브랜치** 배정 문서 동기화 PR(Merge commit)을 허용한다. PR 생성은 실제 인계/착수 허가가 아니며 자동 merge·force 이동하지 않는다.
- **어떤 작업 브랜치도 `builds/*`로 바로 PR하지 않는다.** `builds/*`는 `dev`에서만 받는다.
- `dev`, `integration/*`, `builds/*`는 **PR로만** 바뀐다. 직접 push하지 않는다(브랜치 보호는 PM이 GitHub에서 설정).
  - 예외: PM은 `integration/*`에 준비 작업(통합 기록, Task 폴더, 테스트 씬·폴더)과 마무리 작업(Done 이동)을 직접 올린다. 브랜치 보호의 **우회(bypass) 목록에 PM 계정만** 넣는다. `dev`는 PM도 PR로만 반영한다.
- `integration/*`는 메타 **Base**의 최신 원격에서 만든다(기본 dev, 선택 부모 integration 존재·계보·순환 검사). 작업은 소속 통합, fix/resource는 PR 대상, builds는 빌드 시점 dev에서 만든다.
- 합치기 단계에서 기능끼리 연결하는 작업도 **별도 Task(연결 Task)**로 만들어 같은 흐름을 탄다.
- 특정 프로젝트에서 PM이 승인한 공급 브랜치 직접 pull 예외는 `Harness/Project/Decisions/branch-strategy.md`의 명시된 방향·범위에 한해 우선한다. 일반 Task/소유권·최종 PR 흐름은 유지한다.
- `Scripts/new-pr.ps1`이 이 흐름에 어긋나는 PR을 거부한다.

## fix/* · resource/*

- `fix/*`: 보통은 Task로 분리해 작업한다(`scaffold-task`). **급한 사안이면 Task 없이 바로 수정**할 수 있다. 이때 PR 본문에 급한 이유와 수정 범위를 적는다.
- `resource/*`: 아트·에셋 관리자 등이 **리소스 파일만** 넣는 브랜치다. 보통 `Assets/Resources/` 안에 넣어 충돌이 거의 없으므로 **Task를 만들지 않는다.**
  - 지정 에셋 관리자는 `Assets/_ThirdParty` private submodule 에셋도 추가·수정·저장·push하고 공개 포인터를 제출할 수 있다(`Harness/Project/Decisions/third-party-assets.md`). Task 관련 포인터는 해당 Task 브랜치에서도 허용한다.
  - 공개 저장소의 코드·씬·공용 프리팹 수정이 필요하면 `resource/*`가 아니라 Task로 분리한다.
  - 리소스 위치 기준: `Docs/Guides/assets-folder-rules.md`. 새 에셋의 `.meta`를 함께 올린다.

## 병합 방식

- 작업 → `integration/*` (`fix/*`·`resource/*` → `dev` 포함): **Squash**. Task 하나가 기록 한 줄이 되어 문제 Task를 찾고 빼기 쉽다.
- 통합 → 메타 Base(`dev`/부모 통합), 선언된 역방향 동기화: **Merge commit**. `merge-integration.ps1 -Merge`는 작업 head Squash 전용이며 dev/integration head는 거부한다.
- Base가 dev가 아닌 통합은 기존 `close-integration`(dev 마감/Done 정리 전용)으로 닫지 않는다. 자식 통합 마감 도구는 이번에 확장하지 않는다.
- 병합한 작업 브랜치에서 계속 작업하지 않는다(Squash 후 기록이 어긋난다). 추가 작업은 새 Task로 한다.

## 순차 준비·실제 인계

- 선택 `Active Task`가 없는/빈 통합은 기존 방식 유지. 값이 있으면 그 Task 하나만 기술 배정·착수하며 후속은 scaffolded·`예약 구역`(Allowed 아님)을 유지한다.
- `## 준비 브랜치` 표: `Task | 브랜치 | 준비 기준 SHA | 승인 SHA`(전체 40자리). 동일 Task/Type/Integration·부모 승인 이력만 수용하며 독립 구현 커밋이 있으면 배정 거부한다.
- `assign-task -UsePreparedBranch`는 기술 점검/assigned·배정 문서 동기화 PR만 준비한다. 실제 전달은 PM 확인 때 해당 todo만 체크한다. legacy 합쳐진 인계 체크도 자동 체크하지 않는다.
- 착수 전에 최신 원격 Active Task·실제 전달·최신 배정 문서 유입을 검사한다. assigned/working 외 상태는 브랜치 이동 전에 차단한다.
- 다음 단계는 실 Jira URL·Previous Task integrated·실제 공동 검증 근거 후에만 연다. PM이 이전 Allowed/공용 소유를 반환(이력 보존)하고 다음 한 Task에만 배정한다. scope 권한은 확대하지 않는다.

## 작성자

- 커밋은 **gh에 로그인된 현재 GitHub 계정으로만** 한다. 누가 작업했는지가 GitHub 기록에 보여야 한다.
- AI(Claude, Claude Code 등)의 이름을 작성자·공동 작성자로 쓰지 않는다. 커밋 메시지와 PR 본문에 `Co-Authored-By: <AI>`, `Generated with <AI>` 같은 줄을 넣지 않는다.
- 설정: `pwsh -File Scripts/setup-gh.ps1`이 이 저장소의 작성자를 gh 계정(`<id>+<login>@users.noreply.github.com`)으로 맞추고 커밋 검사 훅(`.githooks/commit-msg`)을 켠다.
- 강제: 커밋하는 스크립트는 작성자가 gh 계정이 아니면 멈추고, 훅은 AI 이름이 있으면 커밋을 거부하며, PR에서는 `verify-commits` 검사가 한 번 더 확인한다.
- GitHub에 연결되지 않은 PC에서는 커밋하지 않는다(`setup-gh` Skill로 연결).

## 저장(커밋)·올리기(push)

- AI는 사용자가 요청할 때만 커밋·push한다. "저장해줘", "제출해줘", "오늘 여기까지"가 요청에 해당한다.
- 커밋 전: `verify-fast`(문서) → 컴파일 확인(`verification-ladder.md` 2단계).
- 커밋 메시지: `<종류>: <요약>` (예: `feat: 커튼 열기 3초 연출`, `fix: …`, `docs: …`, `chore: …`)
- 작업자는 자기 작업 브랜치에만 push한다.
- hook·서명 우회, force push, reset, 이력 재작성은 PM의 명시적 요청 없이는 하지 않는다.

## PR과 순차 병합

- PR·이슈 형식: `Harness/Project/Decisions/pr-issue-format.md`. 등록은 `submit-work` / `report-issue` Skill.
- PM은 정해진 시간에 PR을 **하나씩** 병합한다(순서: 공용 파일 소유 Task → 의존 대상 Task → 나머지).
- 앞 PR 병합으로 뒤처진 **일반 작업 head(feat/fix/refactor/resource)**만 GitHub **Update branch**로 받는다. `merge-integration -Update`는 실제 PR head·대상 base·OPEN 상태를 확인한다. dev/integration head에 Update하면 자식 내용이 부모로 역유입되므로 금지한다.
- 충돌은 해당 Task 담당자와 PM이 함께 해결한다. **씬·프리팹 YAML 충돌은 AI가 해결하지 않는다.**
- 통합을 마무리할 때 각 Task PR의 `closed #번호`를 `dev` PR 본문에 모은다(GitHub는 dev 병합 때만 이슈를 닫음).

## 부모→자식 동기화 PR
- PM의 gh 로그인으로 `pwsh -File Scripts/sync-integration.ps1 -Child integration/<slug> [-Parent <branch>] -DryRun` 미리보기 → 방향·SHA·변경 확인 → **별도 실행 승인** 후 `-DryRun`을 빼고 실행한다. 일반 요청/미리보기 승인을 실행 승인으로 보지 않는다.
- 부모는 원격 child 메타 Base로 결정하며 `-Parent`는 그 값과 일치해야 한다. 로컬 체크아웃/메타와 독립적으로 필요한 원격 refs만 fetch하고 실제 부모→직계 child open PR을 생성/재사용한다.
- `Created`=신규 생성, `Reused`=기존 open PR 재사용, `UpToDate`=부모 변경이 이미 포함되어 생성 없음. DryRun은 원격 쓰기 없는 예상 결과이며 실행 결과를 보장하지 않는다.
- PR은 **리뷰·CI 대기 후 별도 승인된 Merge commit만** 허용한다(Squash·Update branch·자동 merge·보호 우회 금지). PR 생성/재사용이 병합 승인은 아니다.
- GitHub API로 부모·자식 SHA를 원자적으로 고정할 수 없다. 미리보기/생성 후 branch SHA·방향·상태가 바뀌면 중단하고 최신 차이·리뷰·CI를 재검토한다. 실패 시 PR/브랜치 삭제·되돌리기·자동 복구 없이 현재 상태를 보고한다.

## 안전
- 수정 전 `git status`로 기존 변경을 보존한다. 관련 없는 변경을 섞지 않는다.
- 비밀값, `.env`, 개인 설정, 생성된 Unity 폴더(`Library/` 등)를 커밋하지 않는다.
- 새 에셋의 `.meta`를 함께 커밋한다.
- 외부(서드파티) 에셋은 공개 레포에 커밋하지 않고 `Assets/_ThirdParty` submodule(private 레포)에 둔다(`Harness/Project/Decisions/third-party-assets.md`).
- 선행 Task가 소속 integration에 아직 병합되지 않았다면 체리픽으로 우회하지 말고 PM에게 알린다.
