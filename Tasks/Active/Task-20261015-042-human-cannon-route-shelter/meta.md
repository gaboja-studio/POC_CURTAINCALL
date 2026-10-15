# Task-20261015-042

- **Title:** 인간 대포 3단계 — 분리 코스와 쉼터
- **Type:** feat
- **Status:** scaffolded
- **Assignee:** @RagSpirin
- **Domain:** MainStage / HumanCannon
- **Current Skill:** scaffold-task
- **Updated:** 2026-10-15

## Workspace

- **Integration:** integration/main-stage-human-cannon
- **Branch:** feat/human-cannon-route-shelter
- **Previous Task:** Task-20261015-041
- 040은 PR #93으로 integrated. 현재 Active Task/Allowed/테스트 씬 단독 소유는 041이며 이 단계는 기술 순번 대기다.

## Goal

- 지상 이동장치/비행 대포 3대로 서로 분리 출발→첫 받기→생존 비행자 전원 합류 후 쉼터 F 승강·교대·재출발을 검증한다.
- 마지막 출발 여부와 무관한 예고 후 불꽃 압박·구간 경계 종료를 검증하고 쿠션/최종 구간에 별도 타이머를 두지 않는다.

## Scope

- 작업 구역·공용 파일·수정 금지는 setup.md가 기준이다. AI Setup Allowed: None.
- 이 단계 구현·기능 검증·공동 테스트는 미실행이다. 선행 최신 구현 수신은 단계 전환 때 확인한다.
- 2026-10-15 PM @MoHoDu가 @RagSpirin에게 040~044 인계·순차 실행을 승인했다. 추가 PM 인계·착수 승인은 불필요하다: [순차 인계 결정](../../../Harness/Project/Decisions/Human-cannon/sequential-handoff.md).
- 순서 040→041→042→043→044. 선행 구현 통합 후 최신 부모 수신·Active Task/Allowed/공용 소유권의 한 단계 이관·문서 갱신을 수행한다. 공동 테스트는 전체 통합 후 실행한다.

## Decisions

- 기준: cooperative-route.md — 대포 무대→코스 도보길 없음; 쉼터 해제 상태 유지/해제만으로 출발 안 함; 모든 생존 비행자 합류 후 담당이 장치를 싣고 F 상승.
- 낮은 손잡이·문턱 없는 진입, 교대는 선택이다. 불꽃은 공중/지상 모두 압박하며 쉼터/쿠션 경계에서 종료한다.
- Decided: CC-128 연결·3단계 제목 확인 완료. 2026-10-15 PM @MoHoDu의 @RagSpirin 담당·전체 인계·순차 실행 승인 완료; 소유권은 기술 순번에 따라 이관한다.
- Open: 불꽃 접촉 사망은 설계안, 속도/판정은 플레이테스트 조정 대상이다.
- 지상 2명 규칙·중계 대포·호응/보상 수치·2~3인 후속은 미정이며 구현 목표에서 제외한다.
