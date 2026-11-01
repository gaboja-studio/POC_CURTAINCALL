---
summary: A 쇠창살 시작홀 첫 진입부터8분·대기실 UI 재사용·시간 만료와 기존 결과 집계
status: active
updated: 2026-11-02
source: Task-20261027-073 사용자8분 승인·저장 씬 및 EditMode 검사
---
# 탈출로 제한 시간
- A EscapeCampaignFrame의 EscapeRouteTimer가480초를 담당한다. FacilityContent/53_EscapeCountdownStart는 시설 좌표 X0~30/Z0~22.5/Y0~5의 쇠창살 전망 시작홀이다. 계단 끝 접근 구역은 포함하지 않는다.
- 로딩/배치가 끝나고 정상 상태 참가자 첫1명이 진입하면 한 번만 시작한다. 부상은 진입을 막지 않으며 전원 집결·봉/점프 조건은 없다. 진입 전 UI는 숨기고,진입 후 뒤로 나가도 초기화하지 않는다.
- 호스트 ServerTime 절대 시작 시각/480초를 공유한다. 늦게 상태를 받아도 같은 마감 시각이며 재시작 토큰/증분 번호로 이전 타이머 재적용을 차단한다. 개발 R 재시작은 새480초로 초기화한다.
- 기존 RestTimer v2.8 프리팹의 A 씬 인스턴스를 EscapeBodyStatusHud/ EscapeCountdownDisplay에 배치한다. 좌상단(48,-64),192×220. 시간 계산은 A 전용이며 대기실 시간·UI 원본·공용 RestTimer.Read는 변경하지 않는다. 기존30초 경고/10초 긴급 표현을 사용한다.
- 만료 시 호스트가 아직 정상 상태인 참가자만 Retired로 바꾼다. 먼저 실행되는 기존 EscapeCampaignContent가 출구 도착을 확정하고,전원 도착/리타이어/이탈 후 도착1명 이상 성공을 그대로 결정한다. 최종 추격 화염 시간표는 별개로 유지한다.
- 설정: A EscapeCampaignFrame → EscapeRouteTimer → Duration Seconds/Start Area/Display. 새 결과 판정이나 대기실 합류 타이머는 추가하지 않는다.
- EditMode12/12·편집 연결/범위/원본 보존 확인. 실제 Play/MPPM은 사용자 요청으로 미실행. [구현·검사 범위](../../../Tasks/Active/Task-20261027-073-exit-route-space-design/escape-countdown.md).
