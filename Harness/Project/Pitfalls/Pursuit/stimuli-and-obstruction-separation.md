---
summary: 환경 자극은 호스트의 승인된 동작에서 발생시키고, 끼임 보정은 실제 캡슐 겹침·같은 층 지지·다른 벽 통과 여부를 모두 검사한다.
status: active
updated: 2026-11-17
source: Task-20261116-143
---

# 환경 자극과 추격자 끼임

## 언제 읽나
문·소품 소음을 연결하거나 추격자 밀어내기·UV 유인을 수정할 때.

## 원인과 대처
- NavMeshObstacle carving은 새 경로를 막을 뿐 이미 겹친 추격자를 빼내지 않는다. `PursuitActor.Separation`은 피격 밀림 옵션과 별개로 문·트램펄린·소품과의 겹침을 같은 높이에서 해소한다.
- 이 프로젝트 Unity 6000.3.25f1에서 비활성 CapsuleCollider로 `ComputePenetration`하면 실제 겹침도 false였다. 동기 검사 동안만 활성화하고 finally에서 복원한다. 검사 프로브를 게임 충돌체로 남기지 않는다.
- 수직 최소 이동 벡터가 나오는 셔터·트램펄린은 수평 출구를 탐색한다. 이미 겹친 물체 외의 벽을 가로지르거나, 바닥에 서 있던 개체를 지지 없는 곳으로 보내면 안 된다. 출구가 없으면 순간이동하지 않는다.
- 오르기 중에는 움직이는 물체 침입만 보정한다. 오르기를 중단할 때 활성 링크에서 ResetPath를 먼저 호출하면 끝점으로 순간이동하므로 agent를 먼저 끈다.
- 환경 소음은 `PursuitStimuli`로 전달하고 Director의 권한·추격 상태를 확인한다. 발사/잡기/반동은 승인된 게임 동작, 문은 실제 blocker 상태·움직임, TV는 켜진 동안 발생시킨다. 문이 아닌 상자/파괴 벽의 obstacle은 Noise=None이다.
- 큰 소음은 같은 청각 쿨타임의 발소리를, 최고 소음은 큰 소음을 덮어쓸 수 있다. 소리는 위치를 기억하며 기존 직접 시야 추격은 유지한다.
- UV는 원뿔·거리·물리 가림을 통과한 추격자를 비춘 플레이어로 유인한다. 지상 TickBrain과 소품 위 TickPerch 모두 같은 대상 선택을 쓴다. 접촉 포획은 계속 우선하므로 UV 테스트의 다른 플레이어는 포획 반경 밖에 둔다.
- `Hear(Vector3,float)`은 기존 반사 호출 PlayMode 검사가 사용한다. 우선순위 인자는 별도 HearStimulus로 확장하여 기존 메서드 시그니처를 유지한다.
- 기존 행동 검사의 WaitingRoomPursuit 씬은 NavMesh 정점이 0이었다. 실제 게임의 Integrated(WaitingRoom) 씬으로 검사한다. 기상 대기 실패 메시지만 보고 활성 단계나 이동 코드를 바꾸지 말고 NavMesh 존재부터 확인한다.
- UnityTest 코루틴 실패 시 finally 복원이 실행되지 않아 공통 설정이 메모리에 남을 수 있다. 변경 전 JSON을 보관하고 NUnit TearDown에서도 복원한다. 이미 오염되었다면 저장하지 말고 InternalEditorUtility.LoadSerializedFileAndForget로 디스크 에셋을 읽어 CopySerialized로 복원한다.

## 검증
`PursuitStimuliSafetyTests`와 `PursuitUpgradeRuntimeTests`: 벽 옆 문, 넓은 소품, 완전히 막힌 공간, 지지 없는 발판, 소음 우선순위, UV 가림/재유인, 전투 옵션 및 기존 오르기/환풍구.
