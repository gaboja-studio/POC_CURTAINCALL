인간대포 에셋 조합 준비본
작업자: 주형락 / 2026-10-20
기준 씬: Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity
미술 방향: 콘크리트·돌·금속 중심. 로프는 밧줄로 유지.

먼저 보기
HumanCannonArtKitShowcase.prefab: 인간대포에 쓸 외형 11종 모아보기.
공용 준비본 7종과 인간대포 규격에 맞춘 외형 4종으로 구성했습니다.
게임 스크립트·Collider는 없으며, 실제 씬에는 아직 적용하지 않았습니다.

이번에 맞춘 외형
1. CannonStationVisual
   현행 탑승·좌석·포구 기준점을 제공하는 공용 대포 Variant.
   SF/장난감 대포 외형 후보입니다. 최종 디자인은 확인이 필요합니다.
2. RoundCarryTrampolineVisual
   보유 원형 트램폴린을 사용. 원형 천 지름4m·상면0.5m·프레임 가로폭4.8m·최대 외곽 반경2.475m.
   손잡이 기준 Handle(-2.8,0,0). 실제 씬 적용 시 이 기준점 연결이 필요합니다.
   보유 금속 파이프 손잡이를 붙였습니다.
   기존 씬은 사각 판정을 유지합니다. Prepared/RoundTrampolinePrepared는 새 원형 옵션과 판정 연결이 완료된 준비본입니다.
3. CannonReleasePoleVisual
   공용 SharedReleasePole 기반. 기본 봉 폭4.4m·줄8m.
   기존 봉마다 실제 앵커 높이가 다를 수 있으므로 원래 값을 유지합니다.
   leftCable/rightCable에 LeftSwingCable/RightSwingCable을 연결합니다.
   케이블 자식 메시가 단위1m·중심0이므로 런타임 길이 조절과 호환됩니다.
   상속된 LeftCable/RightCable은 비활성 상태이며 연결 대상이 아닙니다.
4. CannonSawDiscVisual
   XY 평면·지름1.3m. 기존 함정이 회전과 수평 눕힘을 담당합니다.
   RotationPivot/Blade의 기준 회전을 보존하고, 시각 지름은 실제 판정과 맞춥니다.

같이 쓰는 공용 준비본
FloorModule: 콘크리트 바닥. StairModule: 콘크리트 계단.
SplitFloorTransport: 금속 양문 발판. 기존 경첩과 충돌 판정 유지.
LandingCushion, SawGateFrame, SpikeMace, CrossBlades 재사용.
독립된 새 에셋 요청 항목을 늘리지 않고 기존 시트 ID를 유지했습니다.

연결할 때
기존 오브젝트의 외형에 연결하고, Network/상호작용/충돌/카메라 수치는 유지합니다.
실제 게임 연결과 플레이 검증은 별도 단계입니다.

의존성·저장 상태
공용 준비본은 resource/performance-art-kit-shared의 0bf1ad1에 저장돼 있습니다.
private 에셋은 현재 로컬 b92b37a4 기준으로 확인했습니다.
인간대포 브랜치의 기록된 private 기준 c1045650에는 Battle Stadium이 없습니다.
다른 PC 배포 전에는 담당자가 공용 준비본과 최신 private 기준을 함께 반영해야 합니다.
원본 외부 에셋은 수정하지 않았습니다.

적용 준비본
Prepared/readme.txt와 readiness.json에 반동·판정·기준점 및 연결 항목을 정리했습니다.
