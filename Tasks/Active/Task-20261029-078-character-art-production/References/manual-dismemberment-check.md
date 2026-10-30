# Unity에서 토끼·곰·삐에로·분홍 소녀 절단 직접 확인

2026-10-30 v14, 실제 Player/기존 조작·애니메이션·절단 경로.

1. Project에서 `Assets/Scenes/Tests/CharacterArt/RabbitGameplay.unity`를 열고 Play를 누른다.
   곰은 같은 폴더의 `BearGameplay.unity`, 삐에로는 `BluePierrotGameplay.unity`, 분홍 소녀는 `PinkGirlGameplay.unity`에서 동일하게 확인한다.
2. Hierarchy의 최상위 **Player**를 선택한다. 자식 Original/Body/Limb가 아니라 Player의 상태를 바꾼다.
3. Inspector의 **Player Condition (Script) → Lost Parts**를 연다.
4. **Left Leg → Right Leg → Left Arm → Right Arm** 순서로 하나씩 추가 체크한다. 이전 체크를 유지해야 누적 절단이다.
5. Game에서 분리·낙하·애니메이션을 확인한다. Scene 탭에서는 확대/회전해 단면과 떨어진 사지를 가까이 볼 수 있다. Hierarchy에 `Detached LeftLeg` 등의 별도 오브젝트가 생긴다.
6. **None**을 고르면 온전한 몸으로 복구되고 분리 조각이 정리된다. Play를 끄면 편집 상태로 돌아온다.

| 단계 | 체크할 Lost Parts | 예상 상태 |
|---|---|---|
| 0 | None | 정상·CLEAN |
| 1 | Left Leg | 왼다리 분리·LIGHT |
| 2 | Left Leg, Right Leg | 양쪽 다리 분리·Crawl·LIGHT |
| 3 | 위 항목 + Left Arm | 오른팔만 남음·HEAVY |
| 4 | 위 항목 + Right Arm | 네 사지 손실·Death·HEAVY |

현재 몸체 메시는 이 고정 누적 순서에 대응한다. 임의로 팔부터 고르거나 앞 단계 체크를 해제하지 않는다. 순차 복구를 보려면 반대 순서로 체크를 해제한다.
Unity Flags 메뉴의 **Everything**은 값이 -1이므로 사용하지 않는다. 네 항목을 순서대로 체크해 합계15인 상태를 만든다.

네 사지 손실의 Death 모션과 콘텐츠가 `PlayerRagdoll.GoLimp`로 시작하는 전신 래그돌은 별도 경로다. 이 연습 씬의 Lost Parts 조작은 절단/복구 확인용이며, 전신 래그돌 물리·복구는 PlayMode 회귀 검사에서 확인했다.

검증: Inspector 실제 클릭으로 0→1→2→0 및 조각수0→1→2→0 확인. 4단계 전부·분리사지 물리/본 굴곡·복구·조작은 `Source/V14/playmode-tests.json`의15개 테스트 통과. 발목 접합은560조건, 절단면16조건의틈0.

삐에로 추가 후 PlayMode 전체19개와 최종 보정 후 삐에로1개 재검사 통과. BluePierrot/Source~의 `playmode-report.json`, `playmode-final-report.json` 및 실제 플레이 0/1/3부상 캡처 참조. 분리 조각은 절단 당시 재질을 유지하므로 이후 몸체의 부상 단계와 달라질 수 있다. 게임 설정의 모델 목록에서 **파란 삐에로**, 의상에서 원하는 색을 고르면 된다. 연습 씬은 파란 기본 재질로 미리 보이며 실제 로비에서는 선택한 의상이 적용된다.

분홍 소녀는 모델 목록에서 **분홍 소녀**를 선택한다. 기본 슬롯은 기존 설정을 유지하며 `PlayerAppearanceCatalog.asset`에서 슬롯별 Model/Outfit을 지정할 수 있다. 원화색 미리보기는 PinkGirlGameplay에 있고, 실제 로비에서는 선택한 4의상이 적용된다. 소녀 전용 회귀와 포즈/단면/단추/발목 검사는 `PinkGirl/Source~/README.md`와 보고서에 있다.
