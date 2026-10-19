# Task-20261019-061

- **Title:** UI 리소스 확보·조합·테스트
- **Type:** feat
- **Status:** working
- **Assignee:** @MoHoDu
- **Domain:** Unity Assets 구조·코드 작업 구역
- **Current Skill:** implement-code
- **Updated:** 2026-10-20

Status 순서: scaffolded(PM 준비) → assigned(기술 배정, 실전달 별도) → working(작업 중) → submitted(PR 제출) → integrated(검증 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/ui-resources
- **Branch:** feat/ui-resources (C1·버튼 1ebe65d 원격 저장 완료, 패널 private a971b415 저장·공개 save-work 진행, 정식 인계 없음)
- **Previous Task:**

## Goal

- 승인된 UI 키트의 필요한 리소스 확보·조합·독립 테스트. 현재 C1·공용 버튼4종·메뉴/확인 패널2종이 대상이며 다른 UI는 사용자 지시 후 추가한다.
- C1 원본 미리보기와 같은 상단 중앙 프리팹을 확인하고, 환호·야유·사망에 따른 실제 합계와 단원별 원호 변화를 확인한다.
- 난도 상한100/150/200, 1~4명과 공연 내 색 고정을 지원한다. 기존 공연 씬 배치는 제외한다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: 전용 worktree의 setup.md 작업 구역 내 폴더·프리팹·폰트·테스트 씬 생성. private C1·버튼 키트 import와 setup의 조립기/namespace 수정 허용. 기존 메인 폴더/공용씬/보상 코드 변경 금지.

## Decisions

- Open: None
- Decided: 2026-10-20 PM 패널 정상 동작 확인, 저장·미사용 자료 정리·하네스 이관 승인. 다른 패널 제작용 원본·공용 리소스·조립기는 보존하고 private 무관 변경은 제외한다.
- Decided: 2026-10-20 PM 패널 키트 조립 계획 승인. 현재 Task에 BasicPanels 조립 프리팹·독립 확인 씬·제공 UI 전환 검증 추가. 기존 UI 정본 보존, 키트 삭제·Git 저장·실게임 연결 제외.
- Decided: 2026-10-20 PM이 원격 통합 준비·저장/push 승인. origin/dev 5e9b1b04에서 원격 integration/ui-resources 생성, private 필수 키트는 main c15d9a11 저장. PR·병합은 별도 요청.
- Decided: 2026-10-20 PM이 버튼 정상 동작을 확인하고 저장·미사용 제작 파일 정리·하네스 기록을 승인. 필수 의존성과 동작을 보존하고 삭제 전후 회귀 검사한다.
- Decided: 2026-10-19 PM 지시. 현재 Task를 각종 UI 리소스 확보·조합·테스트로 변경, 로컬 브랜치 ui-resources로 개명. 버튼4종/확인씬 추가 승인, C1 보존. Task ID/폴더는 이력 유지.
- Decided: 2026-10-19 PM 정리 계획 승인. 프리팹·기능 재확인 후 전용 private 복사본 필수 의존성/라이선스만 유지, 지정 Harness에 정보 기록. 최신 디자인과 메인 원본 보존, 커밋·push 없음.
- Decided: 2026-10-19 PM @MoHoDu 승인. 미리보기 그대로 제작. PM 자체 로컬 Task 준비/구현, private 키트 별도 복사본 import. 원본 보존, 자동 커밋·push·PR 없음. 정식 원격 배정과 구분.
