---
summary: 절단 지면 동작은 손끝 최저점 대신 남은 발바닥 또는 골반의 실제 표면으로 접지한다
status: active
updated: 2026-10-30
source: Task-20261030-085
---

# 절단 동작의 지지면

## 언제 읽나
CircusMotionGallery나 실제 게임에서 양다리가 없는 몸통이 뜨는데 메시 최저점 검사는 통과할 때.

## 원인과 계약
- 전체 메시 최저점만 바닥에 맞추면 손끝이 몸통을 떠받치는 자세가 남는다. 외발은 남은 RightFoot/RightToes, 양다리 없음은 Hips/양 UpperLeg 가중치가 과반인 남은 표면의 최저점을 검사한다.
- `RabbitTrialGrounding`은 네 캐릭터 공통이다. 지면 서커스 동작에서 지지면을 먼저 맞추고 팔 표면 관통은 팔 관절만 보정한다. 장갑·손가락 두께는 기존 SupportSet을 사용한다.
- 스커트가 있는 캐릭터는 기존 .25m 보정 제한보다 큰 보정이 필요하다. 서커스 지면 동작만 humanScale 한도로 보정하고 기존 이동 동작은 기존 제한을 유지한다.
- 비행·점프·탑승·매달림에는 바닥 고정을 적용하지 않는다. 상태 전환 동안은 Animator 전환 비율로 접지 보정을 연결한다.
- 운반의 손잡이 정렬은 접지 이후 HumanoidPlayerPresentation에서 실행한다. 외발 정지 운반도 해당 Cut1 기본 레이어를 쓰고, 이동 중에는 보행 기본 레이어를 유지한다.
- 절단 마스크는 0/4/12/13의 순차 손실을 유지한다. 사망은 기존 래그돌로 처리한다.

## 확인
`CannonMotionTests.InjuredGroundActionsPlantTheActualPelvisOrSurvivingSole`은 네 아바타 × 세 절단 상태 × 세 동작 × 31시점을 별도 BakeMesh로 확인한다. `CircusGrounding_` PlayMode 검사는 실제 운반·점프·대포 착지를 확인한다.
절단 후 Apply가 Crawl 속도를 .0001로 만들므로, 수동 전환 검사에서는 Animator.speed=1로 돌린 뒤 시간을 진행해야 한다. 정적 Seek 검사만으로 전환을 검증했다고 기록하지 않는다.
