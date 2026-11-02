# 추격 시스템 확인

- 씬: `Assets/Scenes/Tests/WaitingRoom/Pursuit/WaitingRoomPursuit.unity`. 런의 대기실/탐색 진입점은 현재 이 복제 씬이다.
- 설정: `Assets/Resources/Settings/GameSettings/WaitingRoomSettings.asset` → Pursuit → Stages. 시작 초, 시야 거리/각도, 순찰 속도, 정상 최대 달리기 대비 추격 배율, 벽 투시, 최인접 상시 추격, 알림 문구를 단계별 조절한다.
- 배치: Hierarchy `PursuitSystem/<방>`의 PursuitRoom → Count, SpawnPoints, PatrolPoints, RoamAcrossRooms, Area, Lights. 초기 26개 영역 중 탐색 구역 11곳에 1체씩, 모델 10종을 순서대로 사용한다.
- 모션/파생 FBX/프리팹/NavMesh/Blender 원본: `Assets/_ThirdParty/curtaincall/Pursuit/`. 기존 Synty 모델은 보존했다.
- 제작 도구: `Tools/Curtain Call/Pursuit`. 1은 파생 모델 프리팹/모션, 2는 NavMesh 베이크. 3은 최초 방 배치용이며 기존 편집을 덮어쓰지 않도록 재설치를 거부한다. 생성한 단차 링크는 재베이크 때 보존한다.
- 빠른 실행: 정상 LAN/온라인 런을 개발 모드로 시작한 뒤 대기실에서 PursuitDirector 컴포넌트 컨텍스트 메뉴 `개발/오프라인 추격 시작`. 2일차 아침/0.05초로 재현한다. 씬을 단독으로 실행하면 플레이어가 없으므로 정상 방 시작 또는 테스트용 플레이어 생성이 필요하다.

## 확인한 것
- 새 규칙 6개 + 기존 쉬는 시간 2개 + 기존 제한시간 16개 통과.
- 오프라인 플레이어와 실제 NGO LAN 호스트에서 이동→포획→조작 잠금→순차 절단→외팔→다음 공연 복귀 확인. `host-capture-report.json`, `host-transition-report.json`.
- 열린 상태 기준 11개 초기 스폰 모두 로비와 완전 경로. 닫힌 문·셔터·파괴 가능한 벽은 Collider 상태를 따라 NavMeshObstacle로 막는다.

## 모션·UI 수정 재확인
- **Play 전에 Tools/CurtainCall/Dev Mode 켜기 → Start에서 Play → LAN 호스트 → F8(DEV MODE 시작 지점)에서 2일차 아침 대기실** 경로를 사용하면 된다. 복제 씬 단독 Play보다 초기화가 갖춰진 이 경로를 권장한다.
- 이 경로와 같은 지점 재진입에서 ESC 열기/닫기, 이동 입력 잠금/해제, 슬롯 0·1 표시와 메뉴 1개 유지 확인. `ui-play-report.json`의 inputReleased=false는 가상 키보드를 키 누름 상태에서 제거한 직후 값이다. 키 해제 후 실제 재진입 결과 `ui-reentry-report.json`은 inputReleased=true.
- `_ThirdParty/curtaincall/Pursuit/Concepts`: 수정 전 제작한 7개 모션의 4방향 원화. `Review/Pursuit_Male_02-*-4views.png`는 최종 Unity 스키닝 결과의 동작 순서, `Review/all-variants-*.png`는 10종 비교. `Review/ui-escape-menu.png`, `ui-inventory.png`는 플레이 화면.
- 10종×7클립×61시점에서 뼈 길이 오차 최대 약 0.000001m, 무릎 굽힘 방향 최소 +0.011465m. 인체 관절 축·모델 결손·손/몸통 가중치 분리 확인.
- Start/Dev 진입·ESC·슬롯·재진입 자동 PlayMode 회귀 테스트 최종 1/1 PASS(36.71초): `ui-tests-final.json`.

## 남은 다인 실기 확인
- 호스트/클라이언트 양쪽에서 같은 마네킹 위치·모션·상체 방향·포획 대상·절단 시점·해당 방 조명만 보이는지.
- 두 플레이어 동시 포획, 포획 중 늦은 입장·연결 해제, 단계 강화 중 늦은 입장.
- 잡힌 인원을 제외한 생존자 전원 집결 후 공연 또는 탈출로 이동, 포획 인원의 외팔 복귀와 탈출로 합류 좌표.
- 4일차 전원 포획 즉시 런 실패, 생존자 남은 경우 탈출 계속 가능.
- 모션의 무게감·공포감·시야/속도/조명 빈도는 사람이 플레이하며 조정한다.
