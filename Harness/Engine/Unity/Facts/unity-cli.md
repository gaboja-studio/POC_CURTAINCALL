---
summary: Unity CLI 버전, 주요 명령, 버전업으로 바뀐 동작, 로그 경로
status: active
updated: 2026-10-30
source: Task-20260930-007 beta.9~12; Task-20261027-076 beta.13·14; Task-20261029-078 beta.15 설치·명령 실행 확인
---

# Unity CLI

CLI로 에디터 상태 확인, 명령 실행, 테스트, 프로젝트 열기를 할 때. CLI 버전이 바뀌었을 때.

## 사실

- CLI 버전: `1.0.0-beta.15` (`unity --version`), Pipeline `com.unity.pipeline` 0.8.0-exp.1
- 주요 명령 (모든 에디터 조작에 `--project-path` 필수):
  - `unity status [--project-path p] --json` — 연결된 에디터 목록. 없으면 `STATUS_NO_INSTANCES`. `--until-ready`로 준비될 때까지 대기(기본 300초)
  - `unity command [name] [args] --project-path p` — 에디터 명령 실행. `--timeout` 기본 30초, `--result-only`로 결과만 JSON
  - `unity recompile --project-path p [--strict]` — 열린 에디터에서 재컴파일 후 컴파일 오류 보고(beta.11 추가)
  - `unity list` — Pipeline이 등록한 도구 목록
  - `unity test [project] --mode EditMode|PlayMode --filter <p> --output <xml>` — 배치 에디터를 새로 띄워 테스트 후 NUnit XML. 같은 프로젝트가 열려 있으면 거부(`Pitfalls/unity-test-editor-open.md`)
  - `unity open [project]` — `ProjectVersion.txt`에 맞는 에디터로 열기
  - `unity close <project>` — 저장하지 않고 종료(주의)
  - `unity mcp --project-path p` — MCP 서버
  - `unity editors --json` — 설치된 에디터 목록
- Editor 로그(macOS): `~/Library/Logs/Unity/Editor.log`, 이전 세션 `Editor-prev.log`

## 버전업으로 바뀐 동작 (beta.9~14, 스크립트·Skill 영향)

- **`unity status` 행의 `project`·`version`은 폐기 예정이다(beta.14).** `projectPath`·`editorVersion`을 읽는다. `verify-unity.ps1`은 `projectPath`를 먼저 읽는다. `unity editors` 행은 `version` 유지, `modules` → `moduleNames`.
- 실패 종료 코드가 원인별로 나뉜다(beta.14, 이전 6): 11 경로·릴리스 없음, 12 에디터 없음, 13 명령 없음, 14 시간 초과. `--format json` 오류는 stderr 일반 텍스트다. 결과는 stdout JSON `success`·`errors`로 판단한다(하네스 영향 없음).
- `--timeout`·`UNITY_*_TIMEOUT`은 초 단위 정수만 받는다(`30s`는 사용 오류). 생략하면 명령별 기본 시간을 쓴다(beta.14). 하네스는 직접 넘긴다.
- `unity command`를 이름 없이 실행하면 명령 그룹 목록이 나온다(beta.14, beta.12는 태그 목록). `--query <키워드>` 검색은 유지된다. `editor_status` 같은 이전 이름은 command group 이전 Pipeline에서 동작하며 `unity pipeline upgrade` 경고가 한 번 나올 수 있다. 에디터가 잠깐 바쁘면 `--timeout` 안에서 기다린다(beta.12).
- `--format ndjson` 마지막 줄은 `{"type":"result"}`이고, `unity open --json`은 성공 시 결과 문서를 출력한다(beta.12).
- 명령 뒤 `--version`은 CLI 버전이 아니다(beta.14). `unity mcp`는 `--project-path`가 없으면 `UNITY_PROJECT_PATH`를 읽는다(beta.13). 이름 변경·추가: `install --list-modules`·`test --affected --since`(beta.9), `recompile`(beta.11), `status --until-ready`·`commands --grep`(beta.12).

## 확인 방법

beta.15의 공통 `--timeout`과 Pipeline eval 인수를 함께 쓸 때는 [명령 인수 경계](../Pitfalls/pipeline-command-drift.md)를 확인한다. CLI 업데이트는 프로젝트 Editor/패키지 버전 고정값을 변경하지 않았다.

`unity --version`, `unity <명령> --help`, `unity changelog --target <버전>`(릴리스 노트, 네트워크 필요). 버전이 바뀌면 이 파일과 `Pitfalls/pipeline-command-drift.md`를 확인한다. `doctor.ps1`은 이 파일에 현재 버전 문자열이 있는지로 WARN을 낸다.
