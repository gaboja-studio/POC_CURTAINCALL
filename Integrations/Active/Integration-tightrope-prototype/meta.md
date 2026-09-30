# Integration-tightrope-prototype

- **Feature:** 멀티플레이 기반 + 외줄타기 묘기 프로토타입
- **Status:** preparing
- **Branch:** integration/tightrope-prototype
- **Base:** dev
- **PM:** @MoHoDu
- **Merge Time:** 순차 병합 — 1단계 작업은 2~3일차, 연결 작업은 4일차, 공동 테스트·dev 반영은 5일차 (착수 후 5일 기한)
- **Updated:** 2026-09-30

Status 순서: preparing(Task 준비) → working(작업 중) → merging(순차 병합) → testing(공동 테스트) → to-dev(dev로 PR) → done. 막히면 blocked.

## 목표

- 4명이 온라인 방에 모이면 게임이 시작되고, 줄 9개 위에서 이동·균형·옆줄 이동·목마·해제를 함께 테스트할 수 있다.
- 쉬운 설명·작업 공간 전체: [plan.md](plan.md)

## Tasks

| Task | 종류 | 담당 | 브랜치 | PR | 상태 |
|---|---|---|---|---|---|

## 공용 파일 소유 표

공용 씬·프리팹·설정 파일은 이 Integration 안에서 Task 1개만 소유한다. 배정된 파일은 해당 Task `setup.md`에도 적는다.

| 파일 | 소유 Task | 이유 |
|---|---|---|

(Task 번호 발급 후 [plan.md](plan.md)의 "함께 쓰는 파일" 표를 Task ID로 옮긴다.)

## Decisions

- Open: 담당자 4명의 GitHub 계정 / Confluence 문서 반입(PM) / 패키지 설치(PM)
- Decided: 2026-09-30 @MoHoDu — NGO + Multiplayer Services(Host), 이동은 클라·판정은 호스트, 패키지 PM 선설치, 조작용 로드 에셋은 `Assets/Resources/`, AI는 컴파일만·플레이 테스트는 작업자, 음성·런 데이터 제외, 4인 기준 ([결정](../../../Harness/Project/Decisions/multiplayer-stack.md), [검증](../../../Harness/Project/Decisions/prototype-verification.md))
