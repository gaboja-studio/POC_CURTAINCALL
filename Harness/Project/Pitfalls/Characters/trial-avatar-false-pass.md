---
summary: 캐릭터 시험 Avatar 유효성·상태명·렌더만으로 애니메이션 호환을 합격시키지 않는다
status: active
updated: 2026-10-30
source: Task-20261029-078
---

# 시험 캐릭터의 거짓 합격

## 언제 읽나
새 캐릭터의 Humanoid Avatar와 기존 Controller를 연결해 CLI/MCP에서 동작을 시험할 때.

## 증상
- 토끼의 자동 Avatar가 isValid/isHuman=true인데 귀를 LeftEye/RightEye로 잡고 발가락을 누락했다.
- 같은 eval 안에서 상태를 바꾸며 Camera.Render를 반복하면 본 위치는 바뀌지만 렌더된 사지는 이전 포즈처럼 보였다. 첫 이미지는 비기도 했다.
- 유한 좌표와 상태명 검사는 통과하지만 기어가기·앉기에서 지면/의상 간섭이 남았다.

## 원인
- Humanoid 자동 추론은 캐릭터의 귀/발가락 의도를 모른다. 유효성은 원하는 본 매핑의 보장이 아니다.
- 동일 Editor 프레임의 연속 렌더에서 SMR 갱신 지연이 관찰됐다. Update(.016)를 두 번 호출해도 렌더 문제는 남았고, CPU BakeMesh를 별도 MeshRenderer로 표시하면 해소됐다. 내부 GPU 캐시 원인은 추정이며 일반 실행 프레임의 게임 결함과 구분한다.

## 대처
1. humanDescription.human을 의도한 본에 명시적으로 매핑. 손 접점용 MiddleProximal, 발가락, 귀의 눈 매핑 부재를 실제 Animator에서 조회한다.
2. 표본 시각 검사는 현재 포즈 BakeMesh를 별도 MeshRenderer로 촬영한다. 원래 SMR을 잠시 숨기고 현재 씬 대신 PreviewScene에서 실행하며 finally로 정리한다.
3. 바인드 행렬·본 하위 관계·가중치·실제 관절 위치와 이미지를 교차 확인한다. 상태명이 맞는 것만으로 스킨 성공이라 하지 않는다.
4. 바닥 최저점, 옷 간섭, 손 접촉, 실제 이동/물리/절단을 별도 검사한다. Editor 표본은 PlayMode/온라인 검사 대체가 아니다.

## 후속 제작에서 확인한 함정
- 새 FBX에 이전 `humanDescription` 전체를 복사하면 `skeleton`의 루트/Armature 이름이 이전 파일 이름으로 남아 valid=true/human=false가 될 수 있다. `human` 매핑과 별개로 새 자산의 실제 휴지 Transform으로 skeleton을 재구성한다.
- Blender `mesh.materials.clear()`가 면의 material_index를 0으로 바꾼다. 재질 번호를 먼저 보관하고 새 슬롯 구성 뒤 복원한다. 단면·웨이트가 맞아도 절단 몸체 전체가 흰색이면 submesh별 삼각형 수와 실제 렌더를 확인한다.
- 새 bmesh 결과 Mesh를 복제 Object에 할당하면 vertex group 이름 목록이 비어 있을 수 있다. deform index가 남아 있어도 그룹 이름/순서가 없으면 Blender 변형은 실패한다. 원본 그룹 순서를 복원한다.
- 기존 HumanoidModel의 Tint는 표면 전체 BaseColor를 덮는다. 고유 단색을 재질 BaseColor에만 두면 분리 순간 색을 잃는다. v05 시험은 고유색을 BaseMap으로 옮겨 white Tint를 보존했으며 팀 식별색 분리는 별도 미완료다.

- v06d 루트 scale=1.00148368에서 기본 `BakeMesh` 결과에 TransformPoint를 적용하면 귀 포함 높이2.165031m였지만 `BakeMesh(mesh,true)`는2.161824m였다. bone.localToWorldMatrix×bindpose로 직접 계산한 가중 위치와 비교한 최대오차는 각각3.218mm / 0.000596mm였다. 이 자산의 비교·접지 검사·미리보기는 true 경로로 통일했다. 엔진 내부 원인은 확정하지 않았으며 다른 계층/스케일에서는 같은 직접 계산으로 검증한다.
- smooth normal은 정상 몸체의 면을 추가하지 않고 음영을 완화한다. 절단 후 천을 접기 위한 국소 세분화는 별도 비용이다. 정점 감소와 삼각형 감소를 혼동하지 않는다.

## 연결 손과 굴곡 검사에서 확인한 함정
- 손가락을 같은 Object로 join해도 메시가 연결되지는 않는다. v08 첫 국소 재구성에서 중지/약지가 별도 성분으로 남아 실제 뿌리를 연장한 후 각 팔+손이1성분/열린 경계0임을 확인했다.
- 손 연결을 위해 팔 전체에 voxel remesh/decimate를 적용하면 관절 루프가 불규칙해지고 확대 굴곡 이미지가 오히려 악화될 수 있다. v08은 손만 재구성하고 팔의 규칙적인 고리를 복원해 경계를 실제 연결했다. 연결성·유한 좌표 통과는 외형 품질 합격이 아니다.
- 하나의 UpperArm 객체로 합친 손은 이름별 분기 외에 손가락별 위치와 Hand→Proximal 전이가 필요하다. 최근접 손가락만 선택하면 먼 손바닥도 끌리므로 축까지 거리 감쇠를 함께 적용하고 벌림·쥠을 비교한다. 기존 입체 봉제선이 새 표면에서 뜨면 해당 연결 성분만 정리한다.
- 프릴의 팔 교차는 측면 여유와 Shoulder/UpperArm 웨이트를 함께 검사한다. v09의6자세 비공면 교차0건도 프릴 자기겹침·머리/몸통·연속 전환 합격은 아니다. 높은 팔에서 과도한 접힘과 기본 실루엣 변화가 남는지 이미지로 확인한다.
- PreviewScene의 카메라가 RenderTexture를 참조한 채 텍스처를 먼저 Release하면 콘솔 오류가 발생한다. 씬/카메라 정리 후 텍스처를 해제한다.
- 독립 PlayMode가 playing=true/paused=false인데 frame2에 멈추면 runInBackground=false와 Editor 비활성 상태를 확인한다. v07/v08은 시험 동안만 true로 두고 종료 때 원래 false 및 시작 씬을 복원했다.

## 확인 방법
Task-078 `Source/configure-rabbit-import.cs`, `test-rabbit-animation.cs`, `test-rabbit-cuts-v05.cs`, `repair-rabbit-cut-materials.cs`. v04 기준 렌더는 v04c, 후속 절단 렌더는 v05b이며 v04/v04b 및 재질 수정 전 v05 절단 렌더를 최종 시각 근거로 쓰지 않는다. 재실행 전 기존 결과 보호 guard와 출력 경로를 확인한다.

## 실제 Player 교체 시 (v12)
- 귀를 포함한 bounds 자동 맞춤은 정수리 신장을 줄인다. 미터 단위로 제작한 v12는 PlayerModelSlot.fitToHeight=false, 정수리1.73m/귀포함2.15m로 연결한다.
- 루트 BoxCollider는 이전 모델의 선택적 부품이다. 관절 래그돌 검사에서 무조건 GetComponent<BoxCollider>().enabled를 읽으면 새 모델에서 MissingComponentException이 발생한다. 루트 collider 부재/비활성, 관절 capsule 활성·물리·복구를 함께 검증한다.
- 표면 고유색을 보존하려면 HumanoidModel.PlayerTintMaterialSlots로 플레이어색 슬롯을 지정한다(v12=1). 분리 조각에도 같은 마스크를 쓰고 페이드 alpha와 절단 당시 색상 스냅샷을 검사한다. 빈 목록은 기존 전체표면 착색이다.
- 독립 Player 조작 씬은 PlayerBalance.activeOnStart=false가 필요하다. 외줄 콘텐츠가 없는 씬에서 기본 true이면 가만히 있어도 균형이 누적되어 래그돌로 전환된다. 실제 게임 prefab 값은 콘텐츠가 관리하므로 변경하지 않는다.
- Pipeline capture_game_view의 상대 save_path는 Assets/ 아래로 해석될 수 있다. Task 증거는 명시적인 파일 출력으로 저장하고 실제 반환 경로를 확인한다. 별도 연습씬의 Player는 프리팹 기본 y=1을 그대로 두지 말고 바닥에 배치한다.
- v13에서 `EditorUtility.CopySerialized(newMesh, existingMesh)`는 새 인덱스8328을 복사하면서 기존 정점5248개를 남겼다. GUID를 유지하려면 기존 Mesh.Clear(false) 후 정점/normal/UV/tangent/bindpose/weight/submesh를 Mesh API로 명시적으로 갱신하고 SetDirty·SaveAssets한다. 9메시 모두 maxIndex<vertexCount, UV/normal/weight 길이 및 JSON 정점 수를 확인한다.
- 곡면 패치는 경계만 가진 n-gon으로 만들면 중심이 머리에 묻힌다. 표면에 투영한 중심 정점으로 삼각형을 나누고 깊이 여유를 검사한다. 색상을 단일 UV로 샘플링할 때 단추 가운데 X실을 검정으로 오인하지 않는다. 새 귀 끝의 UV를 트림 슬롯에 두면 플레이어색이 원화색까지 바꾸므로 실제 슬롯색으로도 확인한다.
- v14에서 바지/부츠 사이18mm 발목 누락은 기존300포즈·절단면 검사로 발견하지 못했다. 접합부의 실제 메시 존재와 의상 안쪽 겹침을 별도 검사한다. ART_태그를 컷JSON에 남기고 정상/분리 스킨의 양끝이 바지/부츠 내부에 있는지 실제BakeMesh에서 확인한다. 첫 숨은 상단은 일부 굴곡에서 튀어나왔고 더 좁은 종아리 구간에 배치해560조건 실패0으로 수정했다. 확대Idle/Walk/Crouch도 함께 본다.
- v15 새 셰이더 직후 Camera.Render는 shaderErrors0이어도 비동기 variant 준비 중 모델이 빠질 수 있다. 증거 렌더에서만 ShaderUtil.allowAsyncCompilation=false로 기다리고 finally에서 복원한다. 에셋 생성 성공과 실제 렌더 성공을 구분한다.
- 표면 상태는 정상/Body4/Limb4의0/1슬롯을 함께 바꾸고 이후cap와PropertyBlock을 보존한다. 이미 분리된 객체에는 상태를 재전파하지 않아 절단 당시 sharedMaterials 스냅샷을 유지한다. UV 마스크는 포즈/분리 후에도 표면에 붙지만 월드좌표 노이즈는 움직이며 미끄러진다.
- 단추 위치를 고정 깊이에 두면 곡면 의상에서 16~32mm 뜰 수 있다. 옷에 투영하고 가중치를 보간해도 큰 삼각형에서는 스키닝된 삼각형의 보간과 보간 가중치의 스키닝이 달라 숙인 자세에서 묻힌다. 단추 바로 아래 옷 정점을 추가해 같은 가중치를 부여하고, 실제 BakeMesh에서 접촉 정점 간 거리를 검사한다. 토끼·곰의 최종 최대 거리는 단추 두께를 포함해 1.4/1.83mm였다.
- 좌우 봉제선을 복제할 때 이름 기반 가중치뿐 아니라 REGION_절단 소유 그룹도 바꾼다. 오른팔 봉제선18정점이 LeftLowerArm/REGION_LeftArmCut에 묶여 Idle에서 머리 위로 떠 있었다. T-pose·Avatar.isValid·절단 캡 검사로는 발견되지 않았다. 좌우 본의 휴지 위치·정점 위치와 분리 메시 소유 부위를 대조한다.
