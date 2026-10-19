# Plan

## Context

대기실 베이스 기능(Task-046~050)은 그레이박스 위에서 완성됐다. 이 Task는 그레이박스를 실제 에셋으로 고도화해 플레이 가능한 대기실 아트를 만든다. 기능 로직 변경이 목적이 아니며, 아트 교체로 깨진 연결만 고친다.

## Approach

1. `setup.md`의 작업 구역 안에서만 구현.
2. 새 로직은 가능하면 테스트를 함께 남김.
3. 공용 파일이 필요하면 직접 고치지 않고 PM에게 요청.
4. 검사 후 `dev`로 제출(PM 예외 승인, meta `Integration: dev`).

## Files

- 수정 예정: `Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity`, 같은 폴더 `Materials/`·`Prefabs/`, `Assets/Resources/`의 대기실용 에셋, 필요 시 `Assets/Scripts/WaitingRoom/`
- 읽기 전용 참고: `Assets/_ThirdParty/`, `Assets/Scripts/Systems/`

## Risks

- 아트 교체 시 기존 기능이 참조하는 오브젝트·콜라이더·스폰 지점이 끊길 수 있다.
- 외부 에셋 재질이 URP와 맞지 않을 수 있다(`_ThirdParty` 수정은 PM 요청).
- 큰 씬 파일이라 충돌 위험이 있으니 같은 씬을 다른 작업이 동시에 고치지 않게 한다.

## Auto Verification

- 구역 검사:
- 1 문서/규칙:
- 2 컴파일:
- 3 테스트:
- 4 실행 로그:

## 공동 테스트 항목

PM과 작업자가 구현 전에 합의한다. 최대 3개. 병합 후 공동 테스트에서 이 항목으로 통과 여부를 판단한다.
불필요하면 `불필요 — 이유`.

1. 미정 — 착수 전에 PM과 담당자가 합의해 최대 3개로 적는다(테스트 씬 `WaitingRoomBase.unity`, 인원·호스트/참가 역할, 재현 절차, 예상과 실제 기록 방법 포함).
