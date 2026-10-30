# Unity에서 토끼·곰 절단 직접 확인

2026-10-30 v14, 실제 Player/기존 조작·애니메이션·절단 경로.

1. Project에서 `Assets/Scenes/Tests/CharacterArt/RabbitGameplay.unity`를 열고 Play를 누른다.
   곰은 같은 폴더의 `BearGameplay.unity`에서 동일하게 확인한다.
2. Hierarchy의 최상위 **Player**를 선택한다. 자식 Original/Body/Limb가 아니라 Player의 상태를 바꾼다.
3. Inspector의 **Player Condition (Script) → Lost Parts**를 연다.
4. **Left Leg → Right Leg → Left Arm → Right Arm** 순서로 하나씩 추가 체크한다. 이전 체크를 유지해야 누적 절단이다.
5. Game에서 분리·낙하·애니메이션을 확인한다. Scene 탭에서는 확대/회전해 단면과 떨어진 사지를 가까이 볼 수 있다. Hierarchy에 `Detached LeftLeg` 등의 별도 오브젝트가 생긴다.
6. **None**을 고르면 온전한 몸으로 복구되고 분리 조각이 정리된다. Play를 끄면 편집 상태로 돌아온다.

| 단계 | 체크할 Lost Parts | 예상 상태 |
|---|---|---|
| 0 | None | 정상 |
| 1 | Left Leg | 왼다리 분리 |
| 2 | Left Leg, Right Leg | 양쪽 다리 분리·Crawl |
| 3 | 위 항목 + Left Arm | 오른팔만 남음 |
| 4 | 위 항목 + Right Arm | 네 사지 손실·Death |

현재 몸체 메시는 이 고정 누적 순서에 대응한다. 임의로 팔부터 고르거나 앞 단계 체크를 해제하지 않는다. 순차 복구를 보려면 반대 순서로 체크를 해제한다.
Unity Flags 메뉴의 **Everything**은 값이 -1이므로 사용하지 않는다. 네 항목을 순서대로 체크해 합계15인 상태를 만든다.

네 사지 손실의 Death 모션과 콘텐츠가 `PlayerRagdoll.GoLimp`로 시작하는 전신 래그돌은 별도 경로다. 이 연습 씬의 Lost Parts 조작은 절단/복구 확인용이며, 전신 래그돌 물리·복구는 PlayMode 회귀 검사에서 확인했다.

검증: Inspector 실제 클릭으로 0→1→2→0 및 조각수0→1→2→0 확인. 4단계 전부·분리사지 물리/본 굴곡·복구·조작은 `Source/V14/playmode-tests.json`의15개 테스트 통과. 발목 접합은560조건, 절단면16조건의틈0.
