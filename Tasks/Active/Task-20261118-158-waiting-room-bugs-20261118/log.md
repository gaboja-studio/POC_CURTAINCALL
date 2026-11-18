# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

- 2026-11-18 잠긴 문 옆 비침 1차 판단(틀림): 전경 투명화의 상자 범위 판정으로 보고 `CameraOcclusionFade`를 실제 면 검사로 바꿨으나 사용자 플레이에서 그대로 → 코드 변경 되돌림(카메라 코드 변경 없음).
- 원인(플레이 중 실시간 확인): 숙소 문 앞 전경 투명화 대상은 `DormEntryEvent/DormDoor/Frame` 하나. 잠긴 문 4곳(숙소 DormDoor, 소품실 CraftPowerDoor, 관리실 ManagementCautionDoor, 기계실→탈출로 MachinePowerDoor)의 문틀이 모두 `SM_Bld_Bunker_Wall_Caution_Door_01`(약 5m 벽 판 한 덩어리)이고 `CameraCollisionSurface`·충돌체가 없었음. 다른 벽은 카메라용 벽 표시가 있어 카메라가 앞에서 멈추는데, 이 문틀은 카메라가 파고들어 판 전체가 디더링됨.
- 수정: WaitingRoom.unity의 문틀 4개에 `CameraCollisionSurface`(UseMeshGeometry, 메시 읽기 가능) 추가. 씬 diff는 컴포넌트 4개 추가뿐(+82줄). 빌더(FinalLayoutBuilder)는 그대로 → 다시 돌리면 빠짐.
- 정적 확인(문 앞뒤 서 있을 수 있는 위치 × 시선 방향, 카메라 벽 충돌 반영): 문틀이 투명화되는 시점 관리실 72→9, 소품실 126→36, 기계실 54→0, 숙소 99→27. 남은 경우는 문틀 실제 면이 카메라와 캐릭터 사이에 있는 경우(일반 벽과 같은 동작).
- 사용자 재확인: 그대로. 점무늬로 비치다가 더 다가가면 아예 문 너머(숙소 방 안)가 보임. 카메라용 벽 표시는 카메라만 막고 캐릭터는 못 막음 → 노란 문틀에 물리 충돌이 없어 캐릭터가 문틀(속이 빈 껍데기) 안으로 들어가고, 카메라 기준점도 따라 들어가 뒷면 없는 안쪽에서 문 너머가 보이는 것으로 판단.
- 수정 2: 문틀 4개에 `MeshCollider`(문틀 메시, 비볼록) 추가. 문틀 범위 안 캐릭터(반지름 0.3)가 설 수 있던 자리 숙소 7→0, 소품실 11→0. 문 구멍은 열린 상태에서 통과 가능(4곳 모두 문틀 충돌 없음, 폭 1.66~1.96m). 씬 diff는 추가만(+178줄, 삭제 0). 앞서 붙인 `CameraCollisionSurface`는 유지.
- 주의: 추격자 길찾기(NavMesh)는 다시 굽지 않음. 문틀 기둥이 벽 쪽이라 영향은 작을 것으로 보지만 확인 필요.
- 검사: 컴파일 PASS, 공통 설정(씬 직렬화 포함) PASS. 테스트 미실행(코드 변경 없음). 사용자 플레이 확인 완료(잠긴 문 비침 해결).
- 2026-11-18 소품실 선반 안내(N04) 카메라: 바닥 경로(NavMesh)를 따라 진행 방향을 바라보며 이동해 소품실에 들어서면 문 쪽을 비췄다가 선반으로 돌던 문제. `WaitingRoomTutorialCamera.Begin(..., straightThrough)` 추가 — 선반 안내만 경로 계산 없이 지금 카메라 자리에서 선반 앞까지 곧장(벽 통과) 이동하고, 가는 내내 선반을 바라봄. 다른 안내는 그대로. 이어 가기(Continue)는 해당 없음(선반은 밤 연계에서 제외됨).
- 검사: 컴파일 PASS(경고는 기존), 공통 설정 PASS, WaitingRoomTutorial EditMode 10/10. 플레이 확인 전.
- 2026-11-18 낡은 비디오 테이프1·2(사용자): 숙소 TV 규칙(넣으면 반복 재생, 길게 눌러 꺼내기, 빈 숙소에서 꺼짐)은 그대로.
  - 아이템 `video-tape-1`·`video-tape-2`(이름 "낡은 비디오 테이프1·2", 실행 중 카탈로그 추가, 문구 파일 item.*.name). 모델·아이콘은 `video-tape` 공용(`ItemVisualAssets.ArtId`).
  - TV 소켓(dorm:tv, toolId video-tape)은 비디오 테이프 계열 모두 받음(`ItemUses.ToolMatches`). 넣은 테이프 ID를 서버가 기억해 꺼낼 때 같은 테이프를 돌려주고, 공유 장부에 `dorm:tv@video-tape-N` 기록 → `TapeScreen.tapeClips`가 모든 화면에서 같은 영상 선택. 기본 테이프는 기존 DormVideoTape.
  - 영상: `Assets/Resources/Videos/WaitingRoom/PerformerRecord1.mp4`(playmovie_record_1, 1920×1080 32.1초, 67MB), `PerformerRecord2.mp4`(record_2, 45.5초, 94MB). LFS 추적.
  - 배치(WaitingRoom.unity, `FinalLayout/Manual/DormTv` 아래 FoundItemSpot): 테이프1 이전 공연자 생활 공간 서쪽 침대 옆 바닥(-18.20, 0.02, 43.77, uidOffset 85), 테이프2 B1 관리실 안쪽 끝(-10.10, -4.98, 107.90, uidOffset 86). 바닥·빈자리(기물 반경 0.25, 캐릭터 캡슐 0.3) 확인.
  - 검사: 컴파일 PASS, 공통 설정 PASS, WaitingRoom EditMode 569/572. 실패 3건은 문구 파일 테스트(WaitingRoomTextTests: 미등록·미사용·씬 대상 키)로, 메시지에 이번에 추가한 키는 없음. dev 기준 재현 여부는 확인하지 않음. 플레이 확인 전.

- 2026-11-18 PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/421 (fix/bugs-20261118 → dev)
- 2026-11-18 낡은 비디오 테이프 삽입 안 됨(사용자): 기물 목록 에셋(ItemCatalog.asset)에 video-tape-1·2가 없었음. `EnsureVideoTape`는 에셋 생성용(FillFromDraft)에서만 불려 실행 중에는 추가되지 않음 → 목록에 없는 기물은 내구도 무제한(-1)으로 생성 → 소켓이 넣을 때 기물이 '다 써서 사라짐'이어야 하는데 사라지지 않아 CARRY_REFUSED. 에셋에 두 항목(Tool, 내구도 1, 가격 0) 추가. 플레이 중 에셋을 고쳐 지금 플레이 세션에는 반영 안 됨(다시 시작 필요). 커밋 안 함(PR #421은 이미 병합됨).
- 2026-11-18 테이프 삽입: 위 ItemCatalog.asset 수정 확인(video-tape-1·2 = Tool, 발견품 내구도 1). 플레이 확인 전.
- 2026-11-18 벽 낙서 단서 오버뷰 기울어짐(사용자): 벽 글자 단서 12개(graffiti·hidden-graffiti·password-prefix·uv-1)는 정면(viewFace)이 비어 있어 `ClueInspectable.ViewRotation`이 플레이어 카메라의 마지막 방향(yaw)과 -18° 기울기로 돌려 보여 줬음. 정면이 비어 있고 TMP 글자가 세워져 있으면 글자 읽는 방향을 정면으로 씀(`WallFace`). 정면이 지정된 단서·글자 없는 단서·눕힌 글자(노트북 메모)는 그대로.
- 2026-11-18 잠자기 때 침대 위에 뜸(사용자): 매트리스 윗면은 실측 0.65m로 코드 값과 같음(메시 충돌 레이 확인). 원인은 누운 몸의 가장 낮은 점을 모든 렌더러의 표시 범위(bounds)로 잰 것 — 스킨 메시 bounds는 엉덩이 뼈 기준의 넉넉한 상자이고 모델 아래 다른 표시물도 포함. 처음 누울 때 스킨 메시를 구워(BakeMesh) 실제 정점의 최저점으로 한 번 재고 그 높이를 유지. 캐릭터 프리팹 4종 정적 비교에서 0.04~0.13m 낮아짐(실행 중 추가 표시물 영향은 플레이로 확인 필요).
- 검사: 컴파일 PASS, 공통 설정 PASS, 대기실 EditMode 568/572. 실패 4건: 문구 파일 테스트 3건(기존과 같음), RehearsalCannonSourceTests.W6(CC_GripArt 재질이 CC_PropSurface와 다름 — 대포 손잡이 재질, 이번 변경과 무관. 로컬에 수정된 GripYellow.mat 또는 dev의 공용 기구 아트 변경 영향으로 보임, 미확인). 플레이 확인 전. 커밋 안 함.
- 2026-11-18 배관 연결 통로(PipeLink) 깜빡임·탈출로 문 열림 연출 점무늬(사용자): 구역에 겹친 같은 메시·같은 평면 얇은 판 없음(z-fighting 아님). 원인은 전경 투명화(`CameraOcclusionFade`) — (1) 맵 외곽 `ExteriorEnclosure/DarkOuterShell`(129×40×198m)의 범위 상자가 늘 카메라를 품어 항상 점무늬 대상, 벽 틈으로 보이는 곳에서 깜빡임. (2) 연출 카메라(탈출로 문 열림 등)가 화면을 잡는 동안에도 연출 카메라~내 캐릭터 사이 벽을 점무늬로 만듦.
  - 수정: 범위 상자에 걸린 읽기 가능 메시는 실제 면(`CameraMeshCollision`)으로 다시 확인. 내 캐릭터를 따라가는 카메라(같은 오브젝트의 CinemachineCamera)가 화면을 잡지 않으면 투명화 안 함.
  - 확인: 외곽 껍데기 판정 배관 연결 통로 12/12→0/12, 탈출로 문 앞 12/12→0/12(시점 12방향). 컴파일 PASS, 카메라 EditMode 31/31. 플레이 확인 전. 커밋 안 함.
