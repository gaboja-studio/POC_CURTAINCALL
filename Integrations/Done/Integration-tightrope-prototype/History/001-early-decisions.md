# 초기 통합 결정 기록

meta.md의 과거 결정 원문, 2026-10-06 이동. 아래 결정은 기존 기록이며 최신 승인/공용 소유 표를 대체하지 않는다.

## Decisions

- Open: 005~010 담당자 GitHub 계정 (미배정, 작업 상황 보고 PM이 결정). 011은 @MoHoDu 확정
- Decided: 2026-09-30 @MoHoDu — NGO + Multiplayer Services(Host), 이동은 클라·판정은 호스트, 패키지 PM 선설치, 조작용 로드 에셋은 `Assets/Resources/`, AI는 컴파일만·플레이 테스트는 작업자, 음성·런 데이터 제외, 4인 기준 ([결정](../../../../Harness/Project/Decisions/multiplayer-stack.md), [검증](../../../../Harness/Project/Decisions/prototype-verification.md))
- Decided: 2026-10-01 @MoHoDu — 패키지 설치·Unity Cloud 연결(gaboja-studio) 완료, Confluence 2건 요약 반입, 외줄 규칙 확정([결정](../../../../Harness/Project/Decisions/tightrope-rules.md))
- Decided: 2026-10-01 @MoHoDu — 005(외줄 기본)를 "외줄 코스·진행"(005)과 "외줄 위 캐릭터 동작"(011)으로 나눈다. 004를 먼저 진행하고 006~011은 004 진입점 확정 후 진행, 005는 004와 병렬
- Decided: 2026-10-02 @MoHoDu — 007(플레이어 동기화)을 실제 플레이어 프리팹 기준으로 먼저 하고, 이후 기능 Task(005·011·006·008)는 기능+동기화를 같이 만든다. 순서 007 → 005 → 011 → 006 → 008(교환 가능) → 009·010(최종 씬·예외로 축소) ([결정](../../../../Harness/Project/Decisions/feature-with-network.md))
