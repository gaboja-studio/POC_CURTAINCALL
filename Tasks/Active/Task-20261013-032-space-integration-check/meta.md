# Task-20261013-032

- **Title:** 공간 연결과 통합 확인
- **Type:** feat
- **Status:** submitted
- **Assignee:** @MoHoDu
- **Domain:** 공통 게임 시스템 (`Docs/Domains/common-game-systems.md`), 연결 Task
- **Current Skill:** submit-work
- **Updated:** 2026-10-16 (SpaceLink 씬 배치)

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/common-game-systems
- **Branch:** feat/space-integration-check

## Goal

- 더미 공간(035) 사이를 기획의 장소 전환(공연장 → 대기실 로비, 쉬는 시간 종료·전원 무대 입구 통로 → 공연장, 마지막 공연 → 밤 정비, 탈출 개시 → 탈출로 → 엔딩, 기한 초과 → 세대 교체)대로 오가며 런 한 바퀴가 돈다.
- 전환할 때 몸 상태·지닌 기물·공연 결과·공동 자금이 이어지고, 2명 이상(또는 개발자 모드 1명)이 모두 같은 일차·시간대·자금을 본다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: `SpaceLink.unity`에 "SpaceLink Hub"(`SpaceLinkHub` + `NetworkSessionTestUI`) 배치 (2026-10-16 @MoHoDu 승인)

## Decisions

- Open: 실제 공간 연결 시 공간 담당 소유 파일 배정은 4단계에서 공간 담당과 결정
- Decided: 2026-10-16 PM: 씬 전환은 NGO 통째 교체(Single)·추가 로드(Additive)를 공통 세팅 "장소 전환"으로 전환, 기본 통째 교체. 배정 공용: BaseGameSettings.cs·.asset, EditorBuildSettings.asset(plan.md)
- Decided: 2026-10-14 PM: 연결 Task. 이번 통합에서는 더미 공간으로만 연결하고 실제 공간 교체는 이후. 선행: 028·030·031·033·034·035 병합 후 인계. 통합 계획 3단계
