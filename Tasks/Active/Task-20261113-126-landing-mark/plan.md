# Task-20261113-126

- **Title:** 착지 그림자 표시 고도화
- **Type:** fix
- **Assignee:** @MoHoDu
- **Branch:** fix/ui/landing-mark
- **Base:** dev

## Context
공중 플레이어 발밑 착지 표시(`HumanCannonGroundMarkers`)를 원형 그림자로 고도화한다(2026-11-13 요청, 참고: 크래시 밴디쿠트 4 노란 외곽선 링).
- 모든 플레이어의 착지 위치를 원형 그림자로 표시.
- 높이 차이가 선명 높이를 넘으면 뿌옇게 번지며 사라지고, 선명 높이 이하는 선명한 원.
- 내 캐릭터(관전 중이면 관전 대상)의 그림자만 선명 높이 이하에서 외곽선. 색은 플레이어 옷 색, 두께·투명도는 설정.

## 계획
- 구조(2026-11-13): 공통 `Systems/Player/LandingShadow/LandingShadowMarkers`가 그림자·외곽선·안전 판정을 담당. 씬 전용 표시가 없으면 실행 시 자동 생성(`Resources/Materials/LandingShadow/LandingShadow.mat`)되고, `HumanCannonGroundMarkers`는 이를 상속해 대포 좌석·비행·코스 바닥 규칙만 더한다(동작 중이면 공통 표시는 쉼).
- 착지면 모양(2026-11-13): 착지면이 BoxCollider(외줄·발판)일 때 좁은 쪽 폭 < '좁은 면 기준 폭'(0.5m)이면 원을 면 모양으로 잘라 그 경계에 외곽선(두께 ≤ 면 반폭×0.35로 자동 얇게). 넓은 면은 '넓은 면 바깥 옅게'(기본 켬)면 원 유지·면 밖만 '면 밖 불투명도'(0.25) 배율, 끄면 자르기. 박스가 아닌 면·트램펄린은 원 그대로.
- 공간별 표시: 공연·탈출로 켬, 대기실(연습실 포함)·로비 끔, 기타 테스트 씬 켬. 등록 씬 이름 → 이름·경로(WaitingRoom·Escape·MainStage) 순으로 공간 판정.
- 안전 판정은 이제 씬 토글과 무관하게 항상 동작. `showSafeLandingFeedback`은 외줄 착지 강조만 켜고 끈다. 안전 판정 규칙은 `WalkableSupport.IsSafeLanding`(외줄 접촉 → 일반 지지면)으로 모음.
- 설정: `BaseGameSettings` > 착지 표시(`LandingMarkerSettings`) — 선명 높이 3m, 번짐 구간 4m, 확산 배율 1.6, 외곽선 0.1m·투명도 0.9. 외곽선 색 설정은 없앴고 각 플레이어의 옷 색(`NetworkPlayer.OutfitColor`, 캐릭터 > 플레이어 자리 색상)을 쓴다(2026-11-13 결정).
- 외곽선 구간(2026-11-13 결정): 높이 차이 ≤ 안전 체크 높이(1.5m) = 안전/비안전 색(내 캐릭터·관전 대상만, 씬 컴포넌트 '안전 착지 표시'가 켜진 곳), ≤ 선명 높이 = 옷 색, 그 위 = 흐린 그림자만. 두께·투명도는 공통 값. 안전 기본 초록, 비안전 기본 빨강.
- 트램펄린 운반자 옵션(기본 켬): 내 캐릭터(관전 대상)가 트램펄린을 잡고 있으면 모든 플레이어 그림자에 외곽선. 선명 높이 조건은 같다.
- 컴포넌트 값(반지름·탐색 거리·안전 착지 초록 표시)은 `HumanCannonGroundMarkers`가 있는 오브젝트에 둔다: HumanCannon.unity `HumanCannonEnhancement`, escape_route·UnderstagePursuerPlaytest `EscapeCampaignFrame`, `RehearsalCourseFromPerformance.prefab` 루트.
- 셰이더 `CurtainCall/HumanCannon/GroundMarker`에 `_OutlineWidth`·`_Blur` 추가. `_RimColor` = 외곽선 색(알파 = 투명도).
- 마커별 값은 MaterialPropertyBlock으로 넣어 공유 머티리얼은 바꾸지 않는다.

## 미정·주의
- 표시는 공중일 때만.
- 외곽선 기준 높이는 선명 높이와 같다(별도 값 없음).
- 외줄 착지 강조(탈출로만 켜짐)는 내 캐릭터·관전 대상이 안전 체크 높이 안일 때만 뜬다.
- 공통 표시는 일반 위쪽 면(법선 y≥0.7)을 착지면으로 쓴다. 외줄 공연에서 장식물 위에 그림자가 생기면 코스별 규칙이 필요하다.
