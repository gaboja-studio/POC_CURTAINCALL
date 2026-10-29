---
summary: 실게임 좌상단 타이머·좌상단 호응/우하단 2칸 인벤토리·균형 게이지 전환. HUD 배치 변경 전에 읽기
status: active
updated: 2026-10-29
source: Task-20261028-077, Task-20261029-079, 사용자 HUD 위치·타이머 v2.8 교체 지시
---

# 실게임 HUD 배치

- 정본 프리팹·독립 확인 씬 배치와 실게임 배치는 구분한다. 이번 수정은 공개 연결 코드와 실게임 공통 씬 인스턴스에 적용한다.
- 공연 C2 제한 시간은 `PerformanceSceneBinding`에서 숨긴다. 불 추격·공연 종료 판정은 변경하지 않는다. C3 진행도는 우상단을 유지한다.
- 공연 C1 팀 호응 게이지는 좌상단을 유지한다. 대기실 W1은 [v2.8 타이머](day-clock.md)로 교체하여 좌상단 (48,-64), 192×220이며 이전 W1 두 종류는 비활성화한다.
- `CommonMenuUI`의 `CommonInventoryUI`는 우하단 (-48,28), 64px 슬롯 2개·간격 8px이다. 1/2 키 선택과 안내 문구를 사용한다. 소지 판정은 기존 `ItemCarry.BaseSlots`(기본 2)가 담당한다.
- `Assets/Resources/Settings/GameSettings/BaseGameSettings.asset` → **HUD 배치 / 균형/추락 게이지 위치**에서 전환한다. `GameSettings.Base.HudLayout.BalancePosition`을 매 프레임 읽는다.
- 1안 `BottomCenter`(기본): 균형/추락 게이지 하단 중앙 (0,28). 2안 `TopCenter`: 상단 중앙 (0,-40). 기존 544×80 크기를 유지한다. 1안은 추락 게이지가 균형 게이지 위, 2안만 아래(균형 바와 8px 간격)로 배치한다. 설정을 되돌리면 기존 위쪽 배치로 복원한다.
- `HudLayout.PlaceBanner`와 `NotificationTextLayout`은 기본 배너 (0,-40), 상단 균형 게이지 사용 시 (0,-140)을 유지한다. 대기실 타이머는 좌상단이므로 중앙 배너 예약 공간은 해제했다. `CommonWaitingRoomUI.TimerBannerTop`을 두 연결에서 공유해 매 프레임 위치를 덮어쓰는 충돌을 막는다.
- 오른쪽 토스트·중앙 상호작용 안내·화면 중앙 결과 팝업은 이번 상단 배너 이동 대상이 아니다.
- 최종 그림·플레이 경험 승인은 자동 테스트와 별개로 사람이 확인한다.
