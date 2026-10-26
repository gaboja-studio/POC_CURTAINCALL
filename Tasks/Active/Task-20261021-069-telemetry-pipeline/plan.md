# Task-20261021-069

- **Title:** 전체 게임 로그·Google 기반 및 공개 이벤트 연결
- **Type:** fix
- **Assignee:** @MoHoDu
- **Branch:** fix/dashboard-refresh-cache
- **Base:** dev

## Context
- 기본 게임 로그를 디스크에 보존하고 Drive·Sheets로 중복 없이 누적한다.
- 무료 Apps Script 대시보드의 수동/30분 갱신 기반을 구현한다.
- Systems/Telemetry는 외줄 로컬 시도 기록. 기존 기능을 보존한다.
- NetworkSessionManager, CampaignRun.StepChanged/RunEnded, PerformanceRewards.ResultConfirmed 공개 진입점을 사용.
- 대상: Start, Lobby, Tightrope, HummanCannon, WaitingRoomBase.

## 현재 참고
- 현재 제출: 14a4263에 로그·설문 변경58파일 저장, PR220 → dev. 사용자 병합 요청에 따라 필수 검사 통과 후 Squash 예정. 공동 검증은 별도 보류하며 완료로 처리하지 않는다.
- 2026-10-26 저장·dev 반영 요청: 현재 로그·설문·버전 변경만 제출한다. 아래 과거 단계의 미배포/미커밋 표기는 당시 이력이며, 최신 배포·빌드 상태는 첫 빌드 인계 행과 History/021 기준이다. 다른 Task 문서 변경은 이번 제출에서 제외한다.
- 2026-10-26 M2 빌드 인계: Windows0.2.0 Development+LOGS/설문끔, Dev Mode 실행 바로가기 생성. 빌드 오류0·수집 시작 확인. Google 수신v35/대시보드v36 기존 URL 반영 완료. 직접 플레이는 PM 진행. [빌드 위치·증거](History/021-m2-windows-build.md).
- 2026-10-26 M2 후속: 게임 0.2.0, 로그/설문별 정확 버전·패치 범위·전체 선택, 밤 정비/퇴장 폼·회차 설정 화면 추가. PM은 새 빌드 적용 방식 선택. M1은 0.1.x, 이 PC 로그 켬·설문 끔. [안내](../../../../Harness/Project/Guides/Log-system/versioning.md). Google 재배포 전으로 새 계약 수집 테스트 보류.
- 2026-10-26 참가자 누락 수정: 최신 dev 7d87630 FF 반영, 기존 변경 백업·복원. 공식 명단과 호스트의 플레이어별 시작/구간/진행/도착 관측, 타임라인 미수집 구분·중요 사건 보존. 현재 브랜치 로컬 수정이며 Google 재배포·빌드·commit/push 없음. [검증·적용 순서](History/019-host-timeline.md).

## 남은 확인
- [남은 작업](../../../../Docs/Log-system/remaining-work.md): 통로 막힘/우회·외줄 발판/지속 손잡기 의미는 기획 보류. 새 원천이 필요한 미도달 실패/함정 밖 충돌 미연결.
- CODE 두PC·재접속·강제 종료/PC 전체 오프라인·정상 공연/구매/의료 공동 플레이, Mac 빌드 보류.
- 실제 설문 답변→시트/대시보드 수치 대조·다른 계정 인증·실제 삭제 미실행. 응답0은 표시 검증이며 실제 응답 검증이 아님.
- Task는 공동 검증 완료/done으로 처리하지 않는다. PR 생성 후 링크·Status·todo 기록.

- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/220

## 기록
- 현재 할 일은 [todo.md](todo.md), 특별한 문제·결과·다음 행동은 [log.md](log.md).
- [이전 계획](History/Harness-v1/plan.md) · [현재 상태·미검증 원문](History/Harness-v1/handoff.md) · [목표·결정 원문](History/Harness-v1/meta.md).
- [기존 경로·참조 정보](History/Harness-v1/setup.md)는 참고용이다. 작업 구역·인계·Status·순차 공동 테스트 제한은 적용하지 않는다.
