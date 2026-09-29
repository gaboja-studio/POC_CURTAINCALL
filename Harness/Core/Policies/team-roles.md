# Team Roles

역할과 수정 권한, 여러 사람이 같은 문서를 고쳤을 때 합치는 기준.
PM 계정: GitHub `@MoHoDu` (`Harness/Project/Facts/team.md`).

## 역할

| 역할 | 하는 일 | 하지 않는 일 |
|---|---|---|
| PM | 통합 브랜치·Task 준비, 테스트 씬·폴더 생성, 인계, 순차 병합, 충돌 해결, dev 반영, 하네스 관리 | — |
| 작업자 | 자기 Task 구현, 저장, 검사, 제출, 이슈 등록 | 병합, 다른 사람 구역 수정, 하네스 수정 |

## 수정 권한

| 위치 | 수정할 수 있는 사람 | 동시에 고쳤을 때 |
|---|---|---|
| `Harness/`, `AGENTS.md`, `CLAUDE.md` | PM만 | — |
| 루트 `Scripts/`, `.github/`, `.githooks/` | PM만 (`Assets/` 안의 스크립트 폴더와는 다름) | — |
| Skill 추가·삭제, `.mcp.json`, `.claude/` | PM만 | — |
| `Tasks/<Task>/` | Task 담당자 | 담당자 내용 우선. 단 `setup.md`는 **PM 우선** |
| `Docs/Guides/` | PM만 | — |
| `Docs/Domains/` | 해당 기능 Task 담당자(작업 중), 평소엔 PM | **실제 코드와 맞는 쪽** 우선. 둘 다 맞으면 둘 다 남김. 최종 정리는 합치기 단계에서 PM |
| `Docs/References/` | 자료를 올린 사람 | 기존 파일을 고치지 않고 새 버전 파일로 추가 (예: `design-v2.pdf`) |
| `Integrations/` | PM만 | — |
| `Assets/` | `setup.md`의 작업 구역만 | `Harness/Engine/Unity/Policies/code-folders.md`, `asset-ownership.md` |

## 개인 도구

- 팀원의 MCP 서버와 개인 Skill은 **자기 PC 전역 위치**에 설치한다. 프로젝트 파일에 넣지 않는다.
  - MCP: `claude mcp add --scope user …`
  - Skill: `~/.claude/skills/`
- 프로젝트 공용 MCP(`.mcp.json`)와 Skill(`Harness/*/Skills/`)은 PM만 추가·삭제한다.
- `.claude/settings.local.json` 같은 개인 설정은 git에서 무시된다.

## 지키게 하는 장치

1. `.github/CODEOWNERS`: PM 전용 경로를 바꾼 PR은 PM 리뷰가 필요하다(브랜치 보호의 "Code Owner 리뷰 필수"와 함께 동작).
2. `verify-scope`: 작업자가 PM 전용 경로나 **진행 중인 다른 Task의 작업 구역**을 고치면 저장·제출을 멈춘다. PR에서도 GitHub Actions로 검사한다.
3. AI는 권한 밖 수정 요청을 받으면 수정하지 않고 "PM 요청이 필요하다"고 알린다.
