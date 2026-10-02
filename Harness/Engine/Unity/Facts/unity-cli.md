---
summary: Unity CLI 버전, 주요 명령, 버전업으로 바뀐 동작, 로그 경로
status: active
updated: 2026-10-02
source: 2026-10-02 unity --help·unity changelog --target beta.9~12 확인, Task-20260930-007
---

# Unity CLI

## 언제 읽나

CLI로 에디터 상태 확인, 명령 실행, 테스트, 프로젝트 열기를 할 때. CLI 버전이 바뀌었을 때.

## 사실

- CLI 버전: `1.0.0-beta.12` (`unity --version`), Pipeline `com.unity.pipeline` 0.8.0-exp.1
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

## beta.8 → beta.12에서 바뀐 것 (스크립트·Skill 영향)

- **`unity command`를 이름 없이 실행하면 명령 대신 태그 목록이 나온다(beta.12).** 전체 목록은 `--detail full`(또는 `compact`). 태그 안 명령은 `--tag <태그>`, 태그만 명시적으로 보려면 `--tags`. `--query <키워드>` 검색은 그대로 동작한다(하네스 Skill·Policy가 쓰는 방식, 영향 없음).
- `unity command <name>`은 에디터가 잠깐 바쁠 때(Play 진입·스크립트 리로드) `--timeout` 안에서 기다렸다 실행한다(beta.12). 이름을 지정해 호출하는 `verify-unity.ps1`은 영향 없음.
- `--format ndjson` 목록의 마지막 줄이 `{"type":"result"}`(개수 `data.count`)다. 줄마다 행으로 읽는 스크립트는 그 줄을 건너뛴다(beta.12).
- `unity open --json`이 성공 시 결과 문서를 출력한다. 빈 출력으로 성공을 판단하지 않는다(beta.12).
- `unity install --list-components` → `--list-modules`로 이름 변경(beta.9).
- 추가: `unity test --affected --since <ref>`(beta.9), `unity recompile`(beta.11), `unity status --until-ready`, `unity commands --grep <패턴>`(beta.12).

## 확인 방법

`unity --version`, `unity <명령> --help`, `unity changelog --target <버전>`(릴리스 노트, 네트워크 필요). 버전이 바뀌면 이 파일과 `Pitfalls/pipeline-command-drift.md`를 확인한다. `doctor.ps1`은 이 파일에 현재 버전 문자열이 있는지로 WARN을 낸다.
