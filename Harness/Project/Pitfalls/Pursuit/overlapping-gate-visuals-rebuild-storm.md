---
summary: 계단 문을 연 뒤 추격 경로 무효화가 매 프레임 발생하면 ExplorationGate의 표시/숨김 목록 중복으로 문짝이 켜졌다 꺼지는지 확인한다.
status: active
updated: 2026-11-18
source: Task-20261118-156
---

# 계단 문 표시 목록 중복과 재계산 폭주

## 언제 읽나
`final:stairs-bar` 개방 뒤 대기실이 끊기거나 `[Pursuit] climb links=...`가 반복될 때.

## 확인된 원인
- 실제 Integrated 대기실의 `structure-a:stairs-door`는 `LockedDoorLeafL/R`을 `hiddenVisuals`와 `hideOnOpen` 양쪽에 참조한다.
- 이전 `ExplorationGate.Update`는 앞 목록에서 `SetActive(true)`, 뒤 목록에서 `SetActive(false)`를 매 프레임 호출했다. 최종적으로는 꺼져 보여도 `PursuitDoorObstacle.OnDisable`은 문짝마다 매번 실행된다.
- 수정 전 에디터 계측: 닫힌 문 10초/423프레임에서 무효화 0회, 열린 문 10초/417프레임에서 834회(프레임당 정확히 2회). 게이트 Update를 따로 호출해도 이 문만 2회 증가했다. 장애물 LateUpdate 호출에서는 증가하지 않았으므로 충돌체 크기 갱신 가설은 기각했다.
- `PursuitClimbLinks`의 지연은 게임 시간 0.25초다. 반복 요청은 프레임 시간·Update 호출 순서·오르는 추격자 유무에 따라 재계산을 계속 미루거나 연속 실행시킬 수 있다. 현재 에디터에서는 초기 빌드 186ms, 관찰 구간의 연속 Build는 0회였다. 사용자 첨부 로그의 450~520ms/348회는 별도 환경의 증거이며 이 실행에서 같은 수치가 재현됐다고 쓰지 않는다.

## 수정과 검증
- 뒤 목록이 최종 상태를 정할 오브젝트는 앞 목록에서 건드리지 않는다. 기존 우선순위 `showOnOpen > hideOnOpen > hiddenVisuals`와 개방 애니메이션 완료 조건을 유지한다.
- 씬 참조/사용자 설정을 삭제하거나 재계산 기능을 끄지 않는다. 문이 실제 열리고 닫힐 때의 무효화는 필요하다.
- `PursuitStairDoor_OpeningAndReturningDoNotContinuouslyInvalidateNavigation`은 실제 씬에서 열기, 닫기/재개방, 개방 상태 보존 재진입을 검증하고 안정된 구간의 무효화/빌드가 증가하지 않는지 검사한다.
- 수정 후 같은 열린 문 10초/395프레임에서 무효화 0회, Build 0회. 실제 개방 직후 필요한 Build 1회는 유지됐다. 위 PlayMode와 기존 개방 조건/암호/보상 테스트 2/2 통과.
