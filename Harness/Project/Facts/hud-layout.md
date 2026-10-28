---
summary: 실게임 HUD 좌상단 시계·호응/우하단 2칸 인벤토리와 균형·추락 게이지 1·2안 전환. HUD 배치 변경 전에 읽기
status: active
updated: 2026-10-28
source: Task-20261028-077, 사용자 HUD 위치 변경 지시
---

# 실게임 HUD 배치

- 정본 프리팹·독립 확인 씬 배치와 실게임 배치는 구분한다. 이번 수정은 공개 연결 코드와 실게임 공통 씬 인스턴스에 적용한다.
- 공연 C2 제한 시간은 `PerformanceSceneBinding`에서 숨긴다. 불 추격·공연 종료 판정은 변경하지 않는다. C3 진행도는 우상단을 유지한다.
- `CommonPerformanceUI`의 C1 팀 호응 게이지와 `CommonWaitingRoomUI`의 W1 시계는 좌상단 (48,-24)이다. W1 v2.2 [하루 시계 프리팹](day-clock.md)을 같은 부모에 배치하며 192×208 크기를 유지한다.
- `CommonMenuUI`의 `CommonInventoryUI`는 우하단 (-48,28), 64px 슬롯 2개·간격 8px이다. 1/2 키 선택과 안내 문구를 사용한다. 소지 판정은 기존 `ItemCarry.BaseSlots`(기본 2)가 담당한다.
- `Assets/Resources/Settings/GameSettings/BaseGameSettings.asset` → **HUD 배치 / 균형/추락 게이지 위치**에서 전환한다. `GameSettings.Base.HudLayout.BalancePosition`을 매 프레임 읽는다.
- 1안 `BottomCenter`(기본): 균형/추락 게이지 하단 중앙 (0,28). 2안 `TopCenter`: 상단 중앙 (0,-40). 기존 544×80 크기를 유지한다. 1안은 추락 게이지가 균형 게이지 위, 2안만 아래(균형 바와 8px 간격)로 배치한다. 설정을 되돌리면 기존 위쪽 배치로 복원한다.
- `HudLayout.PlaceBanner`와 `NotificationTextLayout`은 상단 배너를 중앙 (0,-40)에 둔다. 외줄의 2안에서는 (0,-140)으로 내려 상단 게이지 아래 20px 간격을 확보한다. 대기실·인간 대포는 상단 균형 게이지 공간을 예약하지 않는다.
- 오른쪽 토스트·중앙 상호작용 안내·화면 중앙 결과 팝업은 이번 상단 배너 이동 대상이 아니다.
- 최종 그림·플레이 경험 승인은 자동 테스트와 별개로 사람이 확인한다.
