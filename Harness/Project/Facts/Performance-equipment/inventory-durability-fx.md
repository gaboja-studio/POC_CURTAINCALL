---
summary: 공통 슬롯의 사용 타격광·아이콘 펀치와 상승 후 All In 1 Fade 소멸
status: active
updated: 2026-11-18
source: Task-20261117-155; Task-094
---

# 기물 내구도 UI 연출

## 언제 읽나
공연 장비·탐색 도구의 내구도 감소·소진 피드백을 수정할 때.

## 사실
- All In 1 Sprite Shader가 `Assets/_ThirdParty/SeasideStudios/AllIn1SpriteShader`에 설치되어 있다. 기존 `curtaincall/InventorySlotFx/InventorySlotFx.mat`의 HITEFFECT_ON·FADE_ON을 재사용한다.
- `CommonMenuUI.unity` 슬롯 템플릿의 `InventorySlotUseEffect`가 공통 경로다. 일반 사용은 .4초 타격광·아이콘 펀치와 내구도 바 감소, 소진은 1.05초 중 앞 28%에 25 UI 단위 상승·1.2배 확대·-9도 회전 후 남은 구간에 분해한다. 마지막에는 알파를 함께 줄인다.
- 살아 있는 슬롯 아이콘 대신 사라진 기물의 스프라이트 복제 이미지를 연출한다. 중단/재사용/비활성화 시 원래 위치·크기·회전·재질을 복구한다. UI 시간은 timeScale과 독립적이다.
- `CommonInventoryUI`는 PropCarry의 실제 사용 마크로 일반 사용/소진을 구분한다. 공연 장비와 탐색 도구에 공통 적용되며 버리기·판매로 파괴 연출을 만들지 않는다.
- `InventoryFxPreviewCapture`는 실제 공통 슬롯 템플릿과 spring-shoes/magician-hairpin 아이콘을 복사하여 단계별 이미지를 만든다. 편집 모드 캡처는 Canvas 갱신 이후 비동기로 완료된다.

## 확인 방법
- PlayMode `InventorySlotLiftPlayTests.BreakLiftsBeforeDissolvingAndRestoresOnReplayAndDisable`: 상승 우선·분해·재질 복원·재사용/중단 복구.
- PlayMode `DevStartLauncherPlayTests.W2_InventoryDurabilityBarAndUseEffect`: 실제 대기실의 장비/도구 사용과 양쪽 소진 이벤트.
- 2026-11-18 각 1개씩 통과. 최종 미감·사람 플레이 확인은 별도다.
