---
summary: Unity CLI 버전, 주요 명령, 로그 경로
status: active
updated: 2026-09-29
source: 2026-09-29 unity --help 확인
---

# Unity CLI

## 언제 읽나

CLI로 에디터 상태 확인, 명령 실행, 테스트, 프로젝트 열기를 할 때.

## 사실

- CLI 버전: `1.0.0-beta.8` (`unity --version`)
- 주요 명령 (모든 에디터 조작에 `--project-path` 필수):
  - `unity status [--project-path p] --json` — 연결된 에디터 목록. 없으면 `STATUS_NO_INSTANCES`
  - `unity command [name] [args] --project-path p` — 에디터 명령 실행. 이름 생략 시 목록, `--query`로 검색, `--timeout` 기본 30초
  - `unity list` — Pipeline이 등록한 도구 목록
  - `unity test [project] --mode EditMode|PlayMode --filter <p> --output <xml>` — 테스트 실행 후 NUnit XML
  - `unity open [project]` — `ProjectVersion.txt`에 맞는 에디터로 열기
  - `unity close <project>` — 저장하지 않고 종료(주의)
  - `unity mcp --project-path p` — MCP 서버
  - `unity editors --json` — 설치된 에디터 목록
- Editor 로그(macOS): `~/Library/Logs/Unity/Editor.log`, 이전 세션 `Editor-prev.log`

## 확인 방법

`unity --version`, `unity <명령> --help`. 버전이 바뀌면 이 파일과 `Pitfalls/pipeline-command-drift.md`를 확인한다.
