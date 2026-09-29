# Workspace Isolation

## 규칙

같은 Unity 프로젝트 폴더를 두 주체(사람과 AI, 또는 AI 두 개)가 **동시에** 수정하지 않는다.

## 팀 작업

- 팀원은 각자 자기 PC의 체크아웃에서 작업한다. 사람끼리는 worktree가 필요 없다.
- 작업자는 자기 작업 브랜치에서만 작업한다. 시작할 때 AI가 브랜치를 확인한다("시작해줘").
- AI가 Unity를 조작하는 동안 사람은 Unity를 만지지 않는다. 사람이 작업 중이면 AI는 Unity 조작을 하지 않는다.
- 브랜치를 바꾸기 전에 Unity에서 저장한다. 저장하지 않은 씬 변경은 브랜치 전환 때 사라질 수 있다.

## worktree (선택)

같은 PC에서 사람과 AI가 **동시에** 다른 Task를 진행할 때만 worktree를 쓴다.

- 위치: `.env`의 `WORKTREE_ROOT`. `Scripts/worktree.ps1 -Add <브랜치>`로 만든다.
- worktree는 별도 Unity 프로젝트로 열고(`unity open <path>`) import가 끝날 때까지 기다린다.
- `Library`, `Temp`, `Obj`, `Logs`, `UserSettings`를 worktree끼리 공유하거나 복사하지 않는다.
- 미커밋 변경, 미병합 커밋, 열린 에디터가 있는 worktree는 지우지 않는다. 정리는 `Scripts/worktree.ps1 -Remove <브랜치>`(안전 조건을 스크립트가 검사).

## Unity 대상 지정

`editor-targeting.md`를 따른다. 여러 에디터가 열려 있거나 대상이 애매하면 추측하지 말고 멈춘다.
