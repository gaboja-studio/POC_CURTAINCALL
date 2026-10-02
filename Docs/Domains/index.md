# Domain Index

기능 영역별 지도. 현재 Task와 관련된 도메인 1개만 읽는다.
Domain Map은 담당 파일·진입점·의존성·수정 주의점을 적는다. 함정·결정은 여기가 아니라 `Harness/*/Pitfalls|Decisions/`에 둔다.

## Domains

현재 에셋 구조 지도는 아래에 있다. 게임 기능별 지도는 코드가 생기면 `Harness/Core/Skills/update-domain-map.md`에 따라 추가한다.

| Domain | Map | 현재 근거 |
|---|---|---|
| Unity Assets 구조·코드 작업 구역 | [assets-structure.md](assets-structure.md) | 에셋 배치·Resources·Task별 Scripts 경로를 찾거나 Unity 작업을 계획할 때 |
| 플레이어 조작(입력·이동·균형·겉모습) | [player-control.md](player-control.md) | 플레이어 입력·조작 규칙·이동·점프·균형·모델을 쓰거나 고칠 때 (Task-004) |
| 네트워크 세션(방 만들기·게임 상태) | [network-session.md](network-session.md) | 방 접속·게임 상태(대기/진행/종료) 진입점을 쓰거나 네트워크 기능을 만들 때 (Task-003) |
| 플레이어 동기화(온라인 플레이어·호스트 판정) | [player-sync.md](player-sync.md) | 내 캐릭터·다른 플레이어 상태·추락/재시작 판정을 쓰거나 기능에 동기화를 붙일 때 (Task-007) |

## Domain Map 파일 규칙

- 경로: `Docs/Domains/<domain>.md` (소문자 kebab-case)
- 크기: `Harness/Core/Policies/context-budget.md`의 `domain` 기준
- 권장 목차: 목적 / 담당 파일(존재·planned 구분) / 진입점 / 의존 도메인 / 수정 주의점 / 관련 지식 파일 링크

## References

기획서·외부 자료 원본은 `Docs/References/`에 둔다. 기본 읽기 대상이 아니다.
