# Task-20261016-052

- **Title:** 시작 씬과 로비 씬 만들기
- **Type:** feat
- **Status:** done
- **Assignee:** @MoHoDu
- **Domain:** start-lobby
- **Current Skill:** write-handoff
- **Updated:** 2026-10-18

Status 순서: scaffolded(PM 준비) → assigned(기술 배정, 실전달 별도) → working(작업 중) → submitted(PR 제출) → integrated(검증 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/start-lobby
- **Branch:** feat/start-lobby-scenes
- **Previous Task:**

## Goal

- 시작 씬에서 온라인 코드·LAN으로 로비에 모여 명단에 서명하고, 방장 레버로 첫 장소에 함께 진입한다. 엔딩 후 같은 방 로비에서 다시 준비·시작하는 흐름을 2명 이상이 확인할 수 있다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None

## Decisions

- Open: None
- Decided: 2026-10-18 — 사용자 기록 정리 요청으로 Done 보관. dev 반영은 #145(d2d002c). 남았던 실제 공연 연결·낙하는 Integration-performance-scene-link(Task 057·058)에서 해결했고, 10-18 실제 2인 공동 테스트로 시작→로비→공연→엔딩 로비 복귀 흐름 PASS(PM 보고). 온라인 방 코드·설정 저장·빌드 화면 모드·4인 확인은 별도 기록 없음.
- Decided: 2026-10-16 PM — Canvas, LAN 참가 포함, 고정 3D 천막 로비, 출연 명단 서명 + 방장 커튼 레버. SpaceLink 허브 역할 대체·종료 후 같은 방 복귀. 공용 수정 범위는 setup.
