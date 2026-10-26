# Task-20261019-062

- **Title:** 휴머노이드 모델 교체·입장 순서 캐릭터 배정·부상 절단
- **Type:** feat
- **Assignee:** @MoHoDu
- **Branch:** feat/humanoid-characters
- **Base:** integration/humanoid-characters

## Context
- 기존 임시 모델을 실제 휴머노이드 모델로 교체하고, 최대 4인에게 입장 순서대로 1~4번 캐릭터를 배정한다.
- 기존 신체 부상 상태와 연결하여 절단 표현을 구현하고 온라인 관찰자에게 일관되게 보이도록 한다.
2026-10-20 PM이 Body Type 1 왼다리 절단·충격을 직접 확인하고 오른다리→왼팔→오른팔 순차 절단과 한발 이동·앉은 한발 이동·기기·팔 점프 표현을 요청했다. 승인된 확장 계획에 따라 기존 단일 검증을 보존하고 독립 순차 씬을 만든다. Task 전체의 4인 배정·온라인·실게임 연결은 이번 범위 밖이다.

## 현재 참고
- 2026-10-20, Task submitted(PR #188). 실제 Player 연결 구현, 사람 공동 플레이/다중 클라이언트 완료 아님. worktree D:/github/POC_CURTAINCALL-humanoid-characters, feat/humanoid-characters, 전용 Unity6000.3.25f1/PID44020/7801만 사용. 공개 구현/검증72ec843·JIRA 연결e100d68 push 완료, 최신 통합 기준5b3a85c 반영; PR #188 등록.
- Player.prefab → private Task062/PlayerPresentation/Humanoid.prefab. HumanoidPresentation이 기존 Slot/Condition/Mover/공연 상태를 읽는다. root motion=false/squash=false, 기존 이동·입력·충돌·부상 판정 보존.
- 정상 Idle/Walk/Run/Crouch/CrouchWalk/JumpStart·Loop·Land/Fall, 외줄 Tightrope, 대포 Flying·Crouch, 소품·트램폴린 Carry 상체 레이어 연결. pickup/reach hand는 미리보기만. 기존 CatchPose(600) 팔/BalancePose(500) 부모 슬롯 소유권 유지.
- 한발은 정상 모션, 양다리/한팔은 army_crawl 임시 재사용(정지/공중 지지 포즈). hop/한팔 끌기/팔점프 완성 아님. CrawlHeightOffset=-.05, 정상 키 맞춤과 분리.
- mask0/4/12/13/15: 신규 손실 모두 현재 pose bake → 본체/캡 전환 → 단면 중심 Impulse. 독립 Mesh/Rigidbody, 부모·본·Joint 없음. 중력/충돌/수명/복구 정리, legacy 절단기 상호 배제.

## 남은 확인
- SKIPPED: 다중 클라이언트/실제4슬롯·late join·퇴장 재접속·씬 전환, PropCarry 소품 운반 실연결, 사람 플레이, 전체 프로젝트 PlayMode, 순차50회. 초기 네트워크 flag 합성 검사를 실제 late join 완료로 취급하지 않는다.
- 사람 공동 테스트는 기능 완성 후 한 번에 ①4인 색상 ②부상·비산·복구/원격 ③이동·공연·카메라. Task done 처리하지 않음. 부상 전용 클립/손 접점/자연스러운 접지 후속 필요.
- 사용자8개 private Mixamo는 Humanoid/49본 기준 TRS 보정, SkeletonBone name/parentName 보존. GUID/localID/길이/60fps/원본 바이너리 유지. 개별 출처·배포 권리는 미확인. Quaternius Standard CC0/실제43클립. 외부 공개/구매/업로드 없음.
- dev/다른 worktree·LeartesStudios dirty·TightropeCourse.prefab 기존 위치4곳·.playwright-mcp 보존. 기존 검증 씬/검증 MISSING 정지 정책 유지. HumanoidPlayerBuilder.Build 재실행 금지(기존 출력 guard). CLI beta.6 named --caller/--skill 호환 예외 유지.
- 저장: private Task062 c10ca97e/main6030fe10·공개 b230924 push 완료. private main 반영은 사용자 직접 실행. 최신 dev dcfbd22 기반 원격 integration 준비 및 작업 브랜치 merge5b3a85c 완료. 초기화 보완·최종 검증72ec843 및 CC-152 연결e100d68 push 완료. 지식: Harness/Project/Facts/humanoid-validation.md, Harness/Engine/Unity/Pitfalls/Initialization/editmode-awake-fixture.md.
- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/188 — CC-152, 본문 승인 후 등록; 병합 미실시/별도 승인.

## 기록
- 현재 할 일은 [todo.md](todo.md), 특별한 문제·결과·다음 행동은 [log.md](log.md).
- [이전 계획](History/Harness-v1/plan.md) · [현재 상태·미검증 원문](History/Harness-v1/handoff.md) · [목표·결정 원문](History/Harness-v1/meta.md).
- [기존 경로·참조 정보](History/Harness-v1/setup.md)는 참고용이다. 작업 구역·인계·Status·순차 공동 테스트 제한은 적용하지 않는다.
