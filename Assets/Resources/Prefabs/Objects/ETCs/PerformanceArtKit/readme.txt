인간대포·외줄타기 보유 에셋 조합 준비본
작성: 주형락 / 2026-10-20
미술 방향: 콘크리트·돌·금속 중심. 나무 재질은 필요한 일부 소품에만 사용. 로프는 밧줄 재질 유지.

사용 방법
1. PerformanceArtKitShowcase.prefab을 열면 15종을 함께 볼 수 있습니다.
2. 개별 프리팹은 기존 게임 오브젝트의 Visual 자식으로 배치합니다.
3. 기존 충돌체·네트워크·게임 컴포넌트·이동 수치를 유지합니다.
4. 이 묶음은 외형만 포함합니다. 단독 배치만으로 게임 기능이 작동하지 않습니다.
5. 원본 메시·텍스처는 Assets/_ThirdParty를 참조합니다. private submodule이 필요합니다.
6. 원본 외부 에셋과 기존 코스는 이 작업에서 수정하지 않았습니다.

시트 ID / 파일 / 연결 기준
3D-041 SawDisc: XZ 평면·+Y 법선, 지름1.2m·두께0.1m. RotationPivot 회전.
  외줄 Saws의 horizontalPrefab/verticalPrefab 외형으로 연결 가능.
  최종 협동 톱은 현재 코드에서 자체 생성하므로 별도 연결이 필요합니다.
3D-042 FloorModule: 콘크리트 바닥 4×4m·두께0.25m·상면 y=0. 기존 바닥 Collider 위에 배치.
3D-043 StairModule: 2.5×2.5m 보유 계단. Bottom/Top을 실제 코스와 대조.
3D-044 HumanCannon: 전방+Z, BoardingPoint/Seat/Muzzle 제공.
  보유 장난감·SF 대포 외형 후보입니다. 최종 서커스 스타일 확인 필요.
  기존 Station의 좌석·포구 기준에 맞춰 배치하고 발사 수치를 변경하지 않습니다.
3D-045 CarryTrampoline: 지름4m·반동 면 y=0.5m, 후면 Handle 제공.
  원형 천과 현재 사각 반동 판정이 다릅니다. 자동 교체 대상이 아닙니다.
  최종 적용 전 천 크기 또는 실제 반동 영역의 일치를 확인해야 합니다.
3D-046 SharedReleasePole: 두 게임의 해제봉에 동일 프리팹 사용.
  Grip y=0, HangingFeet y=-1.5m, 로프2.5m, 봉 폭1.8m.
  VisualBar/LeftCable/RightCable/LeftAnchor/RightAnchor를 기존 봉에 연결.
3D-047 SawGateFrame: 인간대포용6×4m 지지부. SawVisualMount 제공.
  기존 Barrier에 크기를 맞춥니다. 외줄 거대 정면 톱에는 추가하지 않습니다.
3D-048 SpikeMace: 가시 머리 중심 원점·폭1.3m. 실제 피해 반경에 맞춰 사용.
3D-049 CrossBlades: 보유 도끼 날의 십자 조합 후보, XY 평면·+Z 회전.
  최종 칼날 형태는 아트 확인 필요. RotationPivot과 기존 날 길이를 대조.
3D-050 SplitFloorTransport: 금속 바닥 6×8m·두께0.25m·상면 y=0. 좌우 바닥·경첩 분리.
  LeftHinge -Z, RightHinge +Z 회전으로 바닥이 아래로 열립니다.
  기존 Transport의 경로·승객 판정·양문 Collider를 그대로 연결합니다.
3D-051 LandingCushion: 상면 y=0·4×4m 보유 에어쿠션 변형.
3D-052 RopeSegment: 전방+Z 3m·상면 y=0·두께0.2m.
  Start/End로 반복 배치. 코드에서 생성/제거하는 외줄에 별도 외형 연결 필요.
3D-053 Trapeze: SharedReleasePole의 프리팹 Variant. 공통 원형 변경을 상속.
3D-054 PressurePlate: 0.8×1.6m 발판. PressSurface 제공.
  기존 발판 신호에 맞춘 눌림·색상 연동은 별도 연결해야 합니다.
3D-055 SafetyNetModule: 수평6×2m 망. 기존 하부 판정 위에 반복 배치.

재질
Assets/Resources/Models/Materials/PerformanceArtKit/
RopeSection1~5: 현재 외줄 5개 구간 색상.
Steel/GripYellow/Rope: 조합부 시인성용 URP 재질.
ConcreteFloorSurface/MetalFloorSurface: 보유 콘크리트 텍스처와 금속 표면 표현용 URP 재질.
URP_*: 보유 Synty 메시와 텍스처를 위한 새 URP 연결 재질.
외부 원본 머티리얼·메시·텍스처를 복사하거나 수정하지 않았습니다.

완료 범위
15종 조합 프리팹 + 한눈에 보는 Showcase + 시트 ID별 출처(sources.json).
기존 씬 적용·게임 판정 변경·4인 플레이 검증은 이 묶음의 완료 범위가 아닙니다.
사운드 원본은 이번에 검색한 보유 에셋 폴더에서 찾지 못했습니다.
화염 VFX 원본은 PolygonGeneric/Prefabs/FX/FX_Fire_01 등에서 확인했습니다.
이번 묶음은 3D 오브젝트 준비본이며, 화염의 URP 표시·범위·수명 연동은 별도 확인이 필요합니다.

적용 준비본
Prepared/에는 공용7종의 물리·연결 준비본이 있습니다.
전체15종(봉의 기능 연결본 포함16개)은 HumanCannonArtKit/Prepared/readiness.json을 확인합니다.
