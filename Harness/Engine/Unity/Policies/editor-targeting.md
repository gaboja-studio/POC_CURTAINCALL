# Editor Targeting

AI가 Unity 에디터를 조작할 때 엉뚱한 프로젝트를 건드리지 않기 위한 규칙.

## 도구

- Unity CLI(`unity`)와 Pipeline 패키지(`com.unity.pipeline`)로 에디터를 조작한다.
- 프로젝트 MCP는 `.mcp.json`의 `unity-editor-mcp`이며 `Scripts/unity-mcp.ps1`이 현재 리포/worktree 루트를 대상으로 실행한다.
- 명령 목록과 버전 정보는 `Harness/Engine/Unity/Facts/unity-cli.md`.

## 규칙

- 에디터를 조작하는 모든 CLI 호출에 `--project-path <task-worktree>`를 붙인다.
- 명령 이름을 가정하지 않는다. `unity command --project-path <p> --query <키워드>`로 먼저 확인한다(`Pitfalls/pipeline-command-drift.md`).
- 수정 전에 읽기 전용 호출로 확인한다: `unity status`, `editor_status`, 열린 씬 목록.
- 연결 확인을 위해 오브젝트 생성·이름 변경·저장·import·Play Mode 진입을 하지 않는다.
- 연결된 에디터의 프로젝트 경로가 Task worktree와 다르면 멈춘다.
- 에디터가 안 보이면 샌드박스가 가린 것일 수 있다. 에디터가 꺼졌다고 단정하지 말고 사람에게 확인한다(`Pitfalls/editor-not-visible.md`).
- 에디터가 연결되어 있는 동안 `.unity`/`.prefab`/`.asset` YAML을 손으로 편집하지 않는다.

## 비밀값

Pipeline 로컬 토큰, OAuth 자격 증명, API 키를 `.mcp.json`, `.env`, Task 문서에 넣지 않는다.
