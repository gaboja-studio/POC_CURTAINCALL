# 인간 대포 소품 v01

Task-20261030-082. 사용자 제공 2장 컨셉, 봉제인형 캐릭터와 붉은/아이보리 서커스 대기실·공연장 톤을 기준으로 Blender에서 직접 제작한 정적 모델. 외부 생성 서비스/구매 에셋 없음.

- `human-cannon-props-construction.blend`: 부품별 편집 정본. 캐릭터 작업 파일과 분리.
- `human-cannon-props-game.blend`: 병합된 게임 메시 정본.
- `build-props.py` → `export-props.py`: Blender에서 실행. 미터, Blender +Y 전방/Z 상방. 처음부터 재현할 때 새 Blender 파일에서 실행한다.
- `../Models/CC_Cannon.fbx`: 16,350 triangles, 게임 렌더러 1개.
- `../Models/CC_Trampoline.fbx`: 23,526 triangles, 게임 렌더러 3개(프레임/천/손잡이).
- `../Textures/`: 공용 1024×512 BaseColor·MetallicSmoothness 2장. URP Lit 재질 1개. 별도 Animator/리그/클립/추가 Collider 없음.
- `props-preview.png`: Blender 렌더. 실제 Unity 검토 이미지는 Task의 `Previews/`.

## Unity 재적용

메뉴 `Tools/CurtainCall/인간 대포/Blender 소품 프리팹 갱신`, 이어서 저장된 HumanCannon 또는 WaitingRoomBase 씬에서 `현재 씬에 Blender 소품 적용`.
도구는 `Assets/Scripts/MainStage/HumanCannon/Editor/HumanCannonPropArtBuilder.cs`. 기존 기능·앵커·충돌체는 보존하고 옛 Renderer를 숨긴다. 원본 프리팹과 숨겨진 모델은 삭제하지 않는다. FBX 변환 결과에 Y=180° 보정 부모를 두며 FBX 자체 root scale/rotation을 보존한다.

## 계약

- 대포 바퀴/허브 폭 1.92m, 바닥 y=0, 포구 중심 (0,2,1.5), 기존 Seat (0,1.2,0), BoardingPoint (0,0,-1.5).
- 트램펄린 실제 천 반경 2m, 높이 .5m, 장식 봉제 최고 .509m. 프레임 바깥 반경 2.4753408m. 장식도 이 반경 내로 배치.
- Handle=(-2.8,0,0), 가죽 그립 높이 .6m. 기능 손잡이 아래 `CC_GripArt` Renderer로 기존 잡기 상태 녹색 표시 유지.
- 현재 런타임은 천 원형 교차뿐 아니라 장치 전체 렌더러 AABB 접촉을 사용한다. Surface 상대 접촉 min=(-2.875,-.5,-2.4753408), max=(2.4753408,.15,2.4753408)을 원본과 동일하게 보존한다. 장식·손잡이 변경도 이 경계를 검사해야 한다.
- 운반 속도·반동 속도·원형 판정·게임 밸런스는 변경하지 않았다.
- Mesh read/write는 기존 HumanCannonOutline의 런타임 정점 읽기에 필요하다.

## 가져오기 함정

UV 레이어 이름을 `UVMap`으로 통일한 후 join한다. 한글 기본 이름과 섞이면 UV 첫 채널에 일부 부품이 빠진다. 금속도 맵은 색 변환 없는 RGBA8이며 R=metallic/G=occlusion/A=smoothness. 안쪽 검은 철재의 환경 반사를 낮춘다. Blender RGB 이미지 저장으로 알파가 유실되지 않도록 export가 직접 데이터 PNG를 기록한다.

최종 미술 감상, 4인 온라인 운반·동시 반동과 실제 성능 평가는 별도 사람 확인 항목이다.
