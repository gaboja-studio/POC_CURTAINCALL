# Task-20261113-123

- **Title:** 공연 종료 여유 시간·마지막 생존자 공연 포기
- **Type:** fix
- **Assignee:** @MoHoDu
- **Branch:** fix/main-stage/result-and-rewards
- **Base:** dev

## Context
2026-11-13 사용자 요청. 공연 2종(외줄·인간대포), 전 난이도 공통 정산·결과·보상 고도화.
1. 첫 생존 도착 뒤 n초(기본 5초, 설정) 여유 시간 후 정산. 상단 시스템 안내 "공연 종료 알림" / "n초 후에 공연이 종료됩니다"를 1초마다 갱신.
2. 생존자가 1명만 남으면 그 플레이어에게만 투표 UI "공연 포기" / "공연을 포기하고 대기실로 돌아갈까요?" / 계속하기·포기하기. 5초 동안 선택이 없으면 계속하기. 카드 맨 아래에 줄어드는 시간 게이지.
3. 포기하면 지금 위치에서 사망한 것과 같게 정산·결과·보상을 처리하고, 그 플레이어는 지금 부상 그대로 대기실로 돌아간다.

## 계획
- 설정: `GameSettings.Base.Run.FinishGraceSeconds`(5), `ForfeitPromptSeconds`(5).
- 공용 정산 `PerformanceRewards.ReportArrivals`: 첫 도착에 여유 시간을 열고, 그동안 보고된 사람을 모은다. 끝나면 살아 있는 사람만 도착으로 성공 정산한다. 남은 초는 공유 상태 메시지로 보낸다.
- 외줄(Bridge 경로·SpaceLink 외부 관리 경로)·인간대포 도착 판정이 즉시 정산 대신 `ReportArrivals`를 부른다. 여유 시간에는 장애물은 계속 돌고 시간 초과 실패는 미룬다.
- 포기: `PerformanceRewards.RequestForfeit` → 호스트가 대상 확인 → `NetworkPlayer.ServerForfeit`(사망 원인 Forfeit, 장소의 사망 상태). 생존자 0명 실패 정산으로 이어진다. `ServerRevive`는 원인이 Forfeit이면 외팔로 바꾸지 않는다.
- 표시: `PerformanceEndingPresentation`(상단 안내·포기 투표), `HudRoleCard.SetTimerGauge`(투표 카드 맨 아래 게이지).

## 미정·주의
- 혼자 출연한 공연에서는 포기를 묻지 않는다(2명 이상 출연한 공연에서 생존자 1명일 때만). 공연마다 한 번만 묻는다.
- 여유 시간 중 도착자가 모두 사망·이탈하면 공연을 이어 가고, 다음 도착이 여유 시간을 다시 연다.
- 포기한 사람도 결과 표시는 "사망"이다(정산이 사망과 같으므로).
