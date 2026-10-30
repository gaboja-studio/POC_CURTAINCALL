# Handoff

## 현재 상태
- Task-067 / feat/main-stage-background. [이전 제작 기록](History/001-background-art-before-trick-integration.md).
- 공통 원본: Assets/_ThirdParty/curtaincall/PerformanceBackground/Scenes/performance_background.unity. 지름 1164, 바닥 Y-1080, 무대 Y0, 천장 Y315.
- 원본은 주황빛·스모그 8곳·철골 지하 도시·배경 톱날 15개·원형 관람대 9곳·연속 원형 관중석 3층×6줄. 총 4800석/정적 관객 4431명.
- 등기구 축소는 취소로 복구됨. 10-21 후속 요청으로 기존 69개 도시 메시의 높이 상승을 취소하고 하부 관람대 좌석도 함께 낮춤. Renderer 1362/정점 9,465,276/삼각형 5,184,096/활성 Light 44.
- 공유 프리팹: PerformanceBackground/Prefabs/performance_background.prefab. 원본의 활성 구성만 복사하고 비활성 구시안/꺼진 조명 73개 제거, 원본 씬은 보존.
- 적용 씬: Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity 및 Assets/Scenes/Tests/MainStage/Tightrope/Tightrope.unity.
- 두 씬 배경 배율 1/3(지름 388/하부 깊이 360). 인간 대포 중심 Z81.125/root Z54.4583, 외줄 중심 Z82.5/root Z55.8333, root Y0.
- 10-21 깊이 후속 요청: 바닥 134.67→360m, 27_DeepAbyss에 심부 작업장 54개/3층, 기초 철골·외벽 연장, 먼 스모그 3곳. [재배치 전 기록](History/002-before-wall-district.md).
- 중앙 도시를 외벽 주변으로 재배치: 원본 반경 작업장 445~524/관람대 410~470/심부 440~529, 외벽 반경 582까지 통로·받침 연결. 톱날/기초/좌석 동반 이동. 조명 색·세기는 유지하고 도시 광원 위치만 외벽 쪽으로 이동. 원본·공유 프리팹 반영.
- 배경 광원 거리 ×1/3·세기 ×1/9, 안개 밀도 ×3. Ignore Raycast 레이어로 배경과 기존 코스 광원의 영향을 분리. 비행 구간을 가로지르는 CircusRigging_Black만 두 인스턴스에서 숨김.
- 기존 코스/캐릭터/판정은 유지. 배경에는 카메라·콜라이더·런타임 스크립트 없음. 원본 수정 후 프리팹은 자동 갱신되지 않으므로 명시적 재반영 필요.
- 현재 Unity: 적용된 HumanCannon 씬, 편집 모드, 코스 옆에서 하부를 내려다보는 검토 시점. 모두 디스크 저장, 미커밋·미푸시.
- 기존 private stash 3개 및 Packages/ProjectSettings/외부 UI meta의 별도 변경 보존. 임의 적용·삭제/공연장 작업과 혼합 금지.

## 검증
- 외벽 재배치 검증(10-21): 구역 PASS(기존 예외)/문서 PASS/컴파일 PASS(up_to_date), dirty 0. 전체 EditMode는 앞선 리로드 정체 이력으로 재실행하지 않음.
- 실제 Play에서 외줄 165m/6개 줄 생성과 배경 1개, 인간 대포 대포 10개와 배경 1개 확인. 호스트 접속·다인 완주는 미실행.
- 저장 파일 비교: 기존 변경은 RenderSettings/Camera 배경/Light 마스크/SceneRoots뿐, 각 씬 배경 PrefabInstance 1개 추가. 게임 구성 요소 변경 0.
- 앞선 Play에서 외줄 프리팹 Slot0~3이 자동 저장되는 부작용을 복구한 이력 있음. 기존 전체 코스 렌더는 Logs/cannon-background-*.png 및 tightrope-background-*.png. 현재 dirty=false.
- Console에는 기존 Vivox 자격 조회 HTTP 403이 남음. 배경 확인은 통과했으나 음성 서비스 정상으로 보고하지 않음.
- 외줄 기존 SafetyNet은 Y-6.05의 불투명 판(29×187)으로 바로 아래 시야를 가림. 동작/외형 변경은 하지 않음.
- 원본·인간 대포·외줄 저장본 재조회: 바닥 -1080/-360/-360, 누락 0, 광원 44/배경 Collider·Camera 0. 도시 69개 메시 Transform 상승 해제 및 최소 반경 388.90 확인, 기존 Light 구성 요소 변경 0. Logs/wall-district-side.png/down.png 렌더.
- TightropeCourse.prefab 기존 4줄 변경은 보존. 이번 배치 변경의 실제 플레이 체감·프레임 시간 미측정.

## 다음 할 일
1. 사용자 플레이 시점 미감 확인, 특히 외줄 안전망 외형과 배경 톱날/실제 장애물 구분.
2. 실제 플레이·다인 코스 완주 및 대상 PC 프레임 시간 검증. 정적 관객 비용 미실측.
3. 저장 요청 시 private 에셋 먼저 저장 후 공개 씬/문서/gitlink 저장. integration/main-stage PR은 별도 요청 때.
