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
- 예약 단계이며 현재 Active Task는 040뿐이다. 기존 feat 브랜치도 실인계가 아니며 scaffolded에서 착수할 수 없다.

## Goal

- 지상 이동장치/비행 대포 3대로 서로 분리 출발→첫 받기→생존 비행자 전원 합류 후 쉼터 F 승강·교대·재출발을 검증한다.
- 마지막 출발 여부와 무관한 예고 후 불꽃 압박·구간 경계 종료를 검증하고 쿠션/최종 구간에 별도 타이머를 두지 않는다.

## Scope

- 작업 구역·공용 파일·수정 금지는 setup.md가 기준이다. AI Setup Allowed: None.
- 준비 사실은 폴더·빈 씬(root 0)뿐이다. 인간 대포 기능 구현·공동 테스트는 미실행이다.
- 앞 단계 integrated·실제 4인 공동 테스트 근거·소유권 반납·실제 JIRA URL을 PM이 확인해야 다음 1개에만 Active Task/Allowed를 부여한다.
- 앞 단계 완료 후 최신 부모 이력을 받고 승인된 준비 브랜치에 배정 문서를 Merge commit으로 sync한다. 실제 전달 PM 확인 이후만 start-work한다.

## Decisions

- 기준: cooperative-route.md — 대포 무대→코스 도보길 없음; 쉼터 해제 상태 유지/해제만으로 출발 안 함; 모든 생존 비행자 합류 후 담당이 장치를 싣고 F 상승.
- 낮은 손잡이·문턱 없는 진입, 교대는 선택이다. 불꽃은 공중/지상 모두 압박하며 쉼터/쿠션 경계에서 종료한다.
- Decided: CC-128 연결·3단계 제목 확인 완료. 실제 전달은 2026-10-15 PM 사용자 확인 완료(@RagSpirin, Task-040~044 전체); 소유권 이관은 별도 대기.
- Open: 불꽃 접촉 사망은 설계안, 속도/판정은 플레이테스트 조정 대상이다.
- 지상 2명 규칙·중계 대포·호응/보상 수치·2~3인 후속은 미정이며 구현 목표에서 제외한다.
