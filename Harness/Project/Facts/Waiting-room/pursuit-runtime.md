---
summary: 추격 복제 씬·단계별 설정·마네킹 파생 리그·동적 NavMesh·현재 검증 범위
status: active
updated: 2026-11-03
source: Task-20261102-094, Unity CLI 및 Blender MCP 제작/플레이 확인
---

# 추격 구현 진입점

## 언제 읽나
추격 배치/설정/모션을 조절하거나, 문을 연 뒤에도 추격자가 통과하지 못하는 문제를 조사할 때.

- 추격 씬은 `Assets/Scenes/InGame/WaitingRoom/WaitingRoomPursuit.unity`. `Tools/Dev Mode/대기실 씬 설정` 또는 BaseGameSettings/SpaceTransition/Waiting Room Variant로 Base/Pursuit 선택(기본 Base). 런 재시작 후 호스트가 실제 로드, Dev 시작·탐색도 동일 설정을 사용. 두 씬 모두 빌드 유지, 로그/카메라/공통 UI/음성은 두 씬을 대기실로 인식한다.
- `WaitingRoomSettings.asset`의 Pursuit/Stages: 단계 시작 초·시야·순찰 속도·추격 배율·벽 투시·최인접 상시 추격·문구. Pursuit 씬 선택과 Enabled가 런 생성 시 새 만료 흐름을 선택하며, 나머지 값은 실행 중 읽는다.
- Hierarchy `PursuitSystem`: 26개 영역 중 탐색 영역 11곳 기본 1체. 현재 씬은 PursuitRoom.UsePlacements/Placements → PursuitPlacement의 타입/Transform/개별 PatrolPoints를 사용한다. 이전 Count/SpawnPoints/PatrolPoints는 이전 씬 호환용으로 보존한다.
- 코드: `Assets/Scripts/WaitingRoom/Pursuit/`. Director는 호스트 AI/포획·NGO 주기 스냅샷, Actor는 내비게이션/모션/상체 방향, VictimPose는 피해자 연출/입력 잠금/가림.
- 정적 FBX 10종(원본은 뼈대 없음)을 Blender에서 15-bone 파생 리그로 만들었다. FBX10·모션70·프리팹10·NavMesh·제작 원본은 모두 `Assets/_ThirdParty/curtaincall/Pursuit/`.
- NavMesh는 물리 충돌체 기반, 좁은 통로용 반경 .24m/높이1.7m. 문·셔터·슬라이드·파괴 가능한 벽을 정적 소스에서 제외하고 PursuitDoorObstacle이 실제 Collider 상태에 맞춰 carving한다. 닫힌 문을 정적 소스에 넣으면 열어도 길이 영구적으로 막힌다.
- 단차 링크는 벽/머리 높이/바닥 지지를 검사해서 생성했다. 생성 링크는 재베이크 때 보존한다. 열린 상태 기준 초기 11개 스폰 모두 로비와 완전 경로를 확인했다.
- 잡힌 인원은 집결에서 제외하되 탈출 이동 명단에는 포함한다. EscapeSceneTransfer는 이 인원의 원래 먼 방 좌표 대신 합류 영역 안의 상대 위치를 배정한다.
- 규칙6·기존 쉬는 시간2·제한시간16 테스트 통과. 오프라인 및 실제 NGO LAN 호스트에서 이동/포획/절단/외팔/공연 복귀 확인. 다인 수신 화면·동시 포획·늦은 접속 실기는 미검증.

## 모션·복제 씬 UI 수정 (2026-11-02)
- `Concepts/01-display.png`~`07-tear.png`: 애니메이션 수정 전 만든 4방향×4키 원화. `Source/rebuild-rigs-v2.py`, `pursuit-rigs-v2.blend`가 현재 리그 원본이다. 원래 정적 메시의 서로 다른 전시 자세에 맞춰 관절 위치를 잡는다. 머리 없는 모델의 전체 높이로 다른 뼈까지 축소하면 안 된다.
- 단순 최근접 뼈·희소 표면 시드는 손목과 몸통의 가중치를 혼합한다. Blender surface heat 이후 메시 연결을 따라 고신뢰 영역을 전파해 손/전완과 몸통의 가중치를 분리한다. 없는 신체 부위의 뼈는 deform을 끈다.
- `PursuitMotionBuilder`는 캐릭터 공간으로 회전축을 구성하고 발 목표점/2-bone IK로 무릎을 앞쪽으로 굽힌다. 수입 FBX의 local Euler 축을 관절 굽힘 축으로 가정하지 않는다. 각성은 local pose 보간으로 길이를 유지한다. 상체 추적도 캐릭터 up을 사용한다.
- `PursuitMotionReview.Export()`는 Unity가 실제 스키닝한 70클립을 샘플링한다. Blender의 `Source/render-motion-review.py`는 그 결과로 4방향 검토 시트를 만든다. FBX renderer의 100배 스케일은 BakeMesh에 이미 포함되므로 TransformPoint를 또 적용하면 안 된다.
- 복제 대기실의 ESC·인벤토리는 고정 씬 이름 목록에 없어서 숨겨졌다. CommonMenuUI/InventoryUI는 이제 등록된 `CurrentWaitingRoom` binding의 씬을 인식한다. Start에서 LAN 호스트 → Dev Start 2일차 아침 → ESC 열기/닫기 → 동일 지점 재진입과 슬롯 2개 표시를 확인했다.

## 배치 창·추격 신호
- `Tools/Curtain Call/Pursuit/Mannequin Designer`: 방별 탑뷰/천장 절단·10종 팔레트·지면 보정·독립 순찰·단계 설정·전체 구조도. Play 중에는 편집을 잠그고 실제 Actor 위치/상태를 표시한다. [조작 안내](../../../../Tasks/Active/Task-20261102-094-waiting-room-pursuit-system/designer-guide.md).
- 2026-11-03 4인 피드백 후 근접 외곽선 제거. 전방 바닥 신호·단계 외형·통신 v2·관전 수정은 [후속 사실](pursuit-feedback.md) 참고. 실제 로컬 추격 대상의 화면 테두리는 유지한다.
- 전역 Stages.ChaseSpeed는 m/s. 0은 기존 ChaseSpeedMultiplier 호환값, 창은 환산한 실제 속도를 보여주고 수정 시 명시 m/s를 저장한다. 순찰 경로를 옮기지 않고 마네킹 시작점만 옮길 수 있다.
- Director.Awake에서 Placement.isActiveAndEnabled를 검사하면 배치의 OnEnable 호출 순서에 따라 일부 생성이 누락된다. 직렬화된 enabled와 GameObject.activeInHierarchy를 사용한다.
- 배치 창의 ActiveFloor/VisibleRooms는 평면 X/Z·측면 Z/Y/X/Y·탑뷰·히트 테스트에 공유한다. 층은 고정 방 순찰/스폰 높이(현재0/-5/-10m)로 나눈다. 마네킹 높이를 사용하면 침대 위로 이동할 때 숙소가2m 층으로 바뀌어 0m 화면에서 사라진다. 탑뷰 far plane은 바닥 아래 .35m, near plane은 위층 바닥 전에서 끊는다.
- 방 선택은 즉시 해당 층을 활성화하고, 같은 층 마커 직접 클릭/드롭은 중심 방과 달라도 실제 소속/드롭 위치 방을 타겟한다. 층을 바꾸면 선택을 해제하고 해당 층 방을 찾는다. Play 구조도도 실제 Actor 높이로 층을 필터링한다.
- 지면 검사 기준은 `PursuitAuthoring.FloorReference`(고정 방 순찰 중앙값→스폰→볼륨 하단)다. 가변 마네킹/개별 순찰이나 가구 위 ray hit로 층을 추정하지 않는다. Lobby minY=-3에서 minY+2.5로 ray를 시작하면 바닥Y=0 아래라 실패한다. 해당 층 위2.4m에서 아래 .75m까지만 검사하고 NavMesh 높이도 제한한다.
- 탑뷰는 빈 곳 좌클릭/중클릭 드래그로 이동하며, 선택 층에서 중심 X/Z를 포함하는 가장 작은 방을 자동 타겟한다. 타겟 변경과 카메라 맞춤을 분리하여 이동/확대가 튀지 않게 한다. 방 밖은 타겟 없음, F는 마지막 방 맞춤. 전체 배치 수는 개별 Placement 중복을 제거한 합계와 이전 Count 방식 방의 합계를 더하며 Play 실행 개수와 구분한다.
- Play 배치 창은 Live/LiveMap partial의2D 관찰 전용 UI로 전환한다. PreparePreview는 Play/진입 중 즉시 반환하고 전환 때 카메라/RenderTexture/복제 모델을 정리한다. 보이는 탭만10Hz Tick, 방/플레이어 목록·고정 층 기준은1초 캐시. 팔레트 AssetPreview·SerializedObject 설정 UI를 그리지 않는다.
- 플레이어 추적은 NetworkPlayer.Local 기본, 전체 플레이어/오프라인 목록 선택을 지원한다. 위치·층·중심 방을 함께 추적하고 드래그/방·층 선택 시 자유 모드. 대상 소멸 시 자동 로컬 선택, 씬 전환 중 대기 후 복귀. 선택 Actor만 경로/시야/추격선을 표시하며 전체 행동 이름은 선택 옵션이다.

- 대기실 설정/추격 저작 도구는 WaitingRoomScenes의 GUID 경로를 읽는다. Unity에서 .meta를 유지한 폴더 이동 시 자동 추적. 배치 탑뷰 마네킹 더블클릭은 Hierarchy 선택 후 Scene 뷰에서 모델을 근접 표시(순찰 지점 범위 제외); Scene 뷰 확인 조명을 사용한다.

- Scene/Inspector/탑뷰에서 Placement 이동 시 순찰 월드 위치 유지. RouteDetachDistance 기본10m(0=자동 해제 안 함), 닫힌 순찰 선분까지3D 거리 초과 시 해당 경로 제거·3초 알림. 공용 경로는 해당 Placement 연결만 해제. Scene Transform 후처리의 자식 삭제는 부모 Undo flush에 주의하며 CompleteUndo로 이전 자세를 보존한다.

- HideAndDontSave 미리보기는 씬 언로드만으로 사라지지 않는다. PreviewLifetime이 편집 씬 전환/닫힘·어셈블리 리로드·에디터 종료에 Cleanup하며 숨긴 탭도 적용. 편집 Director는 활성 씬에서만 조회한다. 같은 씬에서는 Scene 뷰 배치 확인용 미리보기를 유지한다.
