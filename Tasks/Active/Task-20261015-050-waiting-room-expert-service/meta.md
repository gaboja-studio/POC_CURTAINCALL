# Task-20261015-050

- **Title:** 대기실 5단계 — 전문가 서비스
- **Type:** feat
- **Status:** working
- **Assignee:** @gyumin9048-ctrl
- **Domain:** common-game-systems
- **Current Skill:** start-work
- **Updated:** 2026-10-17

Status 순서: scaffolded(PM 준비) → assigned(기술 배정, 실전달 별도) → working(작업 중) → submitted(PR 제출) → integrated(검증 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/waiting-room-base
- **Branch:** feat/waiting-room-expert-service
- **Previous Task:**

## Goal

- 상점에서 서비스 구매 → ⑧ 전문가 대기실 문 열림 → 구매자만 입장 → 신체 복구 → 재등장을 4인 동기화한다.
- 날짜별 담당자와 '담당자 부재중' 표시: 1·3일차 밤 의료 복구, 2일차 의료 비활성.

## Scope

- 작업 구역·공용 파일·수정 금지는 setup.md가 기준이다. AI Setup Allowed: None.
- 구역: `Assets/Scripts/WaitingRoom/ExpertService/`, `Assets/Scenes/Tests/WaitingRoom/ExpertService/`. 1단계 Base 구역은 읽기만 한다.

## Decisions

- 기준: CC-132 전체 개발 순서, `Harness/Project/Decisions/Waiting-room/night-maintenance.md`. 게임 흐름 기획과 충돌하면 게임 흐름 우선.
- Open: 전문가 서비스 상세는 별도 기획서(open-items.md 5.1) — 확정 전 임시 규칙만.
- Open: 서비스 가격·이용 횟수는 임시값.
- Decided: 2026-10-15 PM — 실제 전달·인계 완료, 단계 순서 gate 해제. 앞 단계 완료·PM 보고 없이 바로 착수. JIRA는 CC-132.
- Decided: 의존 — 4단계(049), 3단계 상점(048), 공용 부상(CC-109). 공용 수정은 별도 소유 Task.
- Temp: 2026-10-17 담당자(@gyumin9048-ctrl) 지시로 임시값 적용 — 의료 1개당 30·시설 조정 20(공동 자금), 커튼 닫힘 뒤 복구 3초, 퇴실 커튼 열림 2초, ⑧ 방은 한 번에 1명. 밤 종료·구매자 접속 끊김 때 예약 취소(환불 없음). 상세 기획 확정 시 교체.
- Decided: 2026-10-17 대기실 담당(기획서 작성자) — 의료 1회 이용에 잃은 팔다리 모두 복구, 비용 = 개수 × 1개 가격. ⑧은 붉은 커튼 좌우 열림(기획서 3.3.1 8번 비고): 구매 뒤 구매자가 커튼 앞에 오면 열리며 황금빛, 입장 후 닫힘, 복구 뒤 다시 열림. 구매 전 커튼 앞 F는 안내만.
- Decided: 2026-10-17 대기실 담당 — 시설 담당자(2일차 밤)는 구매·선택만: 장애물 강화/약화를 팀당 그날 밤 1회 사서 다음 날(3일차) 적용값을 `ExpertService.ObstacleFor(day)`로 남긴다. 실제 장애물·보상 반영은 공연장 쪽 별도 작업(수치 미정, 기획서 5.1 #2) — PM 요청 필요.
- Note: 테스트 씬 전용 마우스 시점 — 공용 CameraViewZone + 공용 FreeThirdPerson 시점(왼쪽 클릭으로 커서 잠금, 마우스로 화면 회전). 공용 카메라 코드·설정은 수정하지 않음.
- Note: 수화기 서비스 칸은 3단계 ShopPhone을 고치지 않으려고 같은 오브젝트의 UIInputBlocker.IsBlocking(창 열림)을 읽어 옆에 그린다. 통신은 3단계 ShopChannel을 재사용(별도 이름). 신체 복구는 공용 NetworkPlayer.ServerRestorePart.
