# Task-20261105-106

- **Title:** 0.2.0 공간 로그와 재생 타임라인 확장
- **Type:** feat
- **Assignee:** @MoHoDu
- **Branch:** feat/telemetry-spatial-timeline
- **Base:** dev

## Context
0.2.0 대기실·추격·리허설·탐색·탈출로·공연 사건을 수집하고 Google 대시보드에서 층별 탑뷰와 재생 가능한 동선을 제공한다. 사용자의 2026-11-05 지시가 Confluence 초안보다 우선한다. 기존 Task-069 계약·시트·보관 정책은 호환한다.

## 계획
1. 각 소유자가 1초/0.5m 대기실, 5초 공연 위치와 사건을 저장. 호스트는 공용 추격자·물체·판정 기록.
2. NGO 서버 시간으로 합성. 디스크 체크포인트와 전송을 분리하고 씬/활동 종료 후 직접 제출. 원본 eventId/ACK/재시도 유지.
3. 실제 씬 구조를 X/Z 도식으로 저장, 층·시간·사건 탐색, 기존 로그 도식 유지.
4. Node 계약/재생 및 C# 컴파일·큐 경계 검사, 로컬 브라우저 확인, 수신/대시보드 배포, 기획자 변경표.

## 미정·주의
기존 본체 Unity는 D:/github/POC_CURTAINCALL에 열려 있다. 이 worktree를 대상으로 검증하고 본체 씬은 변경하지 않는다. CampaignRun/PerformanceRewards/ExplorationState/ScheduleBoard/EscapeExitSaw에 확정 사건 연결이 추가되므로 해당 기능 후속 변경과 겹침 주의. commit/push는 요청하지 않았으므로 하지 않는다.

후속: 사용자 피드백에 따라 방별 런·호응/토큰·기믹·탈출 결과 관측을 수정하고 실제 운영 TEST 집계에서 확인한다.
