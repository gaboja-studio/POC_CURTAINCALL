# Domain Index

기능 영역별 지도. 현재 Task와 관련된 도메인 1개만 읽는다.
Domain Map은 담당 파일·진입점·의존성·수정 주의점을 적는다. 함정·결정은 여기가 아니라 `Harness/*/Pitfalls|Decisions/`에 둔다.

## Domains

현재 에셋 구조 지도는 아래에 있다. 게임 기능별 지도는 코드가 생기면 `Harness/Core/Skills/update-domain-map.md`에 따라 추가한다.

| Domain | Map | 현재 근거 |
|---|---|---|
| Unity Assets 구조·코드 작업 구역 | [assets-structure.md](assets-structure.md) | 에셋 배치·Resources·Task별 Scripts 경로를 찾거나 Unity 작업을 계획할 때 |

## Domain Map 파일 규칙

- 경로: `Docs/Domains/<domain>.md` (소문자 kebab-case)
- 크기: `Harness/Core/Policies/context-budget.md`의 `domain` 기준
- 권장 목차: 목적 / 담당 파일(존재·planned 구분) / 진입점 / 의존 도메인 / 수정 주의점 / 관련 지식 파일 링크

## References

기획서·외부 자료 원본은 `Docs/References/`에 둔다. 기본 읽기 대상이 아니다.
