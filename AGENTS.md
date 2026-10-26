# POC_CURTAINCALL Agent Guide

Unity 6 POC 프로젝트. 게임 개요: `Harness/Project/Facts/overview.md`.
이 파일은 읽는 순서만 안내한다. 링크를 재귀적으로 전부 읽지 않는다.

## 읽는 순서

1. 이 파일.
2. 작업 시작에 한 번 `pwsh -File Scripts/doctor.ps1` (문서·도구 작업은 `-SkipEditor`). 같은 작업은 반복하지 않는다. 실패가 해당 작업에 영향을 주는지 판단하고 보고한다.
3. 현재 Task의 `plan.md` → `todo.md`. `log.md`와 `History/`는 필요한 때만 읽는다.
4. `Docs/Domains/index.md`에서 관련 지도 1개, 현재 단계 Skill 1개.
5. 필요한 지식 폴더의 `index.md`와 관련 파일 1개.

요청은 `Harness/Core/Policies/skill-routing.md`로 연결한다.
Task가 없으면 목적·담당자를 확인하고 `scaffold-task`로 만든다. PM 인계·작업 구역 승인은 필요 없다.
게임/Unity 작업은 사용자가 요청한 목적 안에서 진행한다.

## 하네스 3층

- `Harness/Core/`: 공통 작업 흐름·Git·문서 관리.
- `Harness/Engine/Unity/`: Unity 조작·에셋 참조 보존.
- `Harness/Project/`: 이 프로젝트의 사실·결정·함정.
- 우선순위: 사람의 현재 지시 > Project > Engine > Core. 2026-10-27 간소화 결정은 이전 구역·인계·순차 진행 규칙보다 우선한다.

## 협업 원칙

- 권한: `Harness/Core/Policies/team-roles.md`. 작업 목적에 필요한 코드·문서·기능 연결을 직접 수정한다. 다른 기능의 읽기·참조·사용은 자유롭다.
- Task마다 작업 브랜치를 나눈다. `feat/*`, `fix/*`, `refactor/*`, `resource/*`, 선택 `integration/*` 이름을 유지한다. 중간 브랜치를 거치는 순서는 강제하지 않는다.
- `dev` 반영은 PR 1회와 지정 팀원 검토를 거친다. 다른 브랜치 변경은 필요한 작업 브랜치로 직접 받을 수 있다. `builds/*`는 dev에서만 받는다.
- Unity 변경 전 `code-folders.md`, `asset-ownership.md`를 확인한다. 작업 폴더 사전 배정은 없다. 겹치는 수정은 알리고, 씬·프리팹 YAML 충돌은 담당자와 해결한다.
- 에셋 추가·활용·새 머티리얼 생성은 자유롭다. 기존 에셋의 큰 수정·삭제는 PM·에셋 관리자가 관리한다.
- 외부 에셋은 `Assets/_ThirdParty` 비공개 저장소에 둔다. 비밀값·OAuth 상태·Pipeline 토큰은 추적하거나 출력하지 않는다.
- 게임 의도가 미정이면 `human-decision.md`를 따른다. 이미 요청한 변경과 일반 구현에 허락을 반복해서 묻지 않는다.
- 스킬·MCP 사용과 추가를 허용한다. 개인 인증값은 개인 설정에 보관한다.
- commit/push는 사용자가 요청할 때만. reset/discard/force도 명시적 요청 때만. 커밋 작성자는 gh 로그인 계정이며 AI 작성자·홍보 줄은 넣지 않는다.
- 보조 AI는 `subagent-usage.md`를 따른다.

## 기록과 검사

- 필수 Task 문서는 `plan.md`, `todo.md`, `log.md` 3개. 목적·담당·브랜치는 plan, 할 일은 todo, 문제·결과·다음 할 일은 log에 남긴다.
- 진행 여부는 `Tasks/Active/`, `Tasks/Done/`으로 구분한다. 인계 확인·세부 상태·Jira 등록은 착수 조건이 아니다.
- 검사는 개발·수정 묶음 완료 시 또는 사용자 요청 시 한 번: `pwsh -File Scripts/check-work.ps1`.
- 문서·도구 변경에는 Unity 검사를 요구하지 않는다. 자동 테스트는 필요할 때 `-RunTests`로 선택한다. 재미·방향·최종 플레이 경험은 사람이 확인한다.
- 검사 실패·생략·환경 제약은 그대로 기록한다. 테스트 없음은 `NO_PROJECT_TESTS`이며 PASS로 쓰지 않는다.
- 배운 함정·결정·사실은 `record-knowledge`로 해당 지식 폴더에 남긴다.

## 이름

하네스 폴더는 첫 글자만 대문자, 파일은 소문자. 외부 도구가 강제한 이름과 Unity `Assets/` 관례는 예외다.
