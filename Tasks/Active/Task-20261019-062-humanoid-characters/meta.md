# Task-20261019-062

- **Title:** 휴머노이드 모델 교체·입장 순서 캐릭터 배정·부상 절단
- **Type:** feat
- **Status:** submitted
- **Assignee:** @MoHoDu
- **Domain:** player-control
- **Current Skill:** submit-work
- **Updated:** 2026-10-20

## Workspace

- **Integration:** integration/humanoid-characters
- **Branch:** feat/humanoid-characters
- **Previous Task:**

## Goal

- 기존 임시 모델을 실제 휴머노이드 모델로 교체하고, 최대 4인에게 입장 순서대로 1~4번 캐릭터를 배정한다.
- 기존 신체 부상 상태와 연결하여 절단 표현을 구현하고 온라인 관찰자에게 일관되게 보이도록 한다.

## Scope

- setup.md의 넓은 PM 작업 구역을 적용한다. 2026-10-20 PM이 왼다리 검증 후 4사지 순차 절단·부상 표현 확장을 승인했다. 단일 모델 독립 검증이며 4인 배정·온라인·실제 Player/공용 이동·밸런스 연결은 제외한다.
- AI Setup Allowed: Local worktree, Task documents, single-model sequential validation code/assets/tests/scene, cut impulse, official free animation acquisition/license evaluation (2026-10-20). 미확보 부상 클립의 절차적 대안은 확인 후 별도 구현.
- 2026-10-20 추가 승인: 사용자 제공 8개 FBX의 private 이동·Humanoid 변환·Body Type 1 독립 미리보기/검증. 상하체 레이어·새 부상 동작 제작·기존 MISSING 정지 정책 변경은 제외한다.
- 2026-10-20 실게임 연결 추가 승인: Body Type 1 실제 Player 외형·정상/공연 애니메이션·현재 부상/독립 비산/복구·기존 서버 슬롯 1~4 색상과 BaseGameSettings Inspector 지정. 이전 독립 검증 제한은 해당 검증에만 적용하며, 검증 MISSING 정책은 보존한다. 실게임은 미확보 한발 모션에 정상 클립, 양다리/한팔 상태에 army_crawl 임시 재사용(전용 완성 아님), 실제 이동/점프 규칙 유지. 새 동작 제작·새 게임 내 색상 UI·네트워크 설정 편집 동기화는 제외한다.
- 로컬 PM 직접 착수 승인: 2026-10-19 사용자 요청. 2026-10-20 저장·제출 승인: private main 6030fe10 저장 완료, 최신 origin/dev 기반 원격 integration/humanoid-characters 준비. 공개 작업 브랜치 저장 완료. 사용자가 CC-152 연결·본문 승인 후 PR #188 제출; 병합 승인은 별도.

## Decisions

- Open: 모델 에셋 선정, 퇴장/재접속 슬롯 정책, 부위별 절단/복구 연출, 애니메이션 적용 범위.
- Decided: 별도 worktree·기존 dev 보호·PM 직접 구현·최대 4인 입장 순서 배정. 2026-10-20 검증은 Body Type 1 고정 순서 4사지·양쪽 캡·독립 조각으로 확장(최종 아트 승인 아님). 외부 부상 동작 미확보는 MISSING으로 표시하고 임의 대체하지 않는다.
