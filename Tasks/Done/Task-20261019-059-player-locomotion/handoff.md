# Handoff

## 지금 상태
- 단계: 제출(PR #181 → `integration/player-locomotion`, 2026-10-19). 컴파일·EditMode·PlayMode(Locomotion_*) 통과. 공동 테스트는 미실시로 진행(PM 결정, 2026-10-19).
- 동작하는 것(자동 테스트 확인): LShift/LCtrl 홀드 역할, 공용 달리기 1.5·앉기 0.5·높이 0.6·동시 홀드 앉기 우선, 달리기 점프 속도 유지, 앉기 점프 금지·천장 아래 앉기 유지·공간 생기면 기립, 모델 슬롯 발 기준 축소·복구, 1인칭 눈높이 두 경로, 미정 조작 로그(플레이어별 누른 순간 1번).
- 코드 기준(사람 확인 전): 배치 미리보기·인간 대포 플랫폼 공용 규칙, 앉기 상태 온라인 공유, 외줄 전환기·배치 미리보기의 자기 규칙만 해제.
- 앉기 겉모습 축소는 임시 표현(사용자, 2026-10-19: 나중에 애니메이션으로 교체). `PlayerModelSlot.squashWhenCrouched`(기본 켬)를 추가해, 애니메이션 도입 때 끄고 애니메이터가 `PlayerMover.IsCrouched`를 읽도록 함. 교체 방법은 `Docs/Domains/player-control.md`.
- 작업 환경: 두 번째 PC는 dev 작업과 섞이지 않게 별도 worktree `D:\github\POC_CURTAINCALL-locomotion`과 그 경로 전용 Unity 에디터로 검사함.
- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/181

## 다음 할 일
1. PM: PR #181 Squash 병합 → `integration/player-locomotion` → `dev` 반영·통합 마감.
2. plan.md 공동 테스트 3항목은 미실시. 나중에 확인하면 아래 "공동 테스트 결과"에 기록.

## PM이 확인할 것
- 확인 요청: 앉은 채 두 다리를 잃는 순간 다리 손실 모델 위치 보정(`LimbDismemberment`, 수정 범위 밖)이 축소된 슬롯 상태에서 측정될 수 있음 — 공동 테스트 3에서 관찰.
- 확인 요청: 외줄 `CourseControlSwitcher`가 해제될 때(로컬 플레이어 사라짐·코스 비활성화) 자기가 끼운 규칙을 빼고 기본 규칙으로 돌린다. 이전에는 플랫폼 규칙이 남아 있었다.
- 확인 요청: `SpaceLinkPlayTests` 2개가 dev에서도 실패하는 것으로 보임(대기실 씬 이름 기대값 `DummyWaitingRoom` ↔ 설정 `WaitingRoomBase`). 대기실·공간 연결 담당 fix 필요(이 Task 범위 밖).
- 확인 요청: 새 worktree에서 Unity를 처음 열 때 외부 에셋 `LeartesStudios/AbandonedFactory/HDRP/Scene/AbandonedFactory.unity`(바이너리 씬) 로딩 실패 뒤 빈 YAML 씬(3.5KB)으로 덮어써짐을 발견, 서브모듈 원본으로 되돌림(커밋 없음). 다른 팀원 PC에서도 생길 수 있음.
- 확인 요청: 저장소의 `.githooks/pre-push`가 실행 권한 없이(100644) 커밋돼 있어 push 때 무시된다(기존 문제, 이 Task 범위 밖).
- 대기 중인 결정: 없음
- 공용 파일 요청: 없음(`PlayerRagdoll.cs`는 배정됐으나 모델 슬롯 분리 처리로 수정 불필요)

## Verification
- 구역 검사: PASS (10-19)
- 1 문서/규칙: PASS (10-19)
- 2 컴파일: PASS (10-19)
- 3 테스트: PASS (EditMode, 577/577, 10-19). PlayMode 99개 중 95 통과·2 실패·2 생략(10-19, `squashWhenCrouched` 추가 전 실행 — 기본값 켬이라 동작 같음): Locomotion_* 3개 포함 이 Task 관련은 모두 통과. 실패 2개 `SpaceLinkPlayTests.Single_FollowsPhasesAndKeepsRunState`·`Additive_KeepsHubAndSwapsSpaceScenes`는 대기실 씬 설정이 `WaitingRoomBase`로 바뀐(#123, dev) 뒤 테스트가 `DummyWaitingRoom`을 기대해 생기는 기존 문제로 이 Task 범위 밖. 생략 2개는 로컬 봇 실행파일이 필요한 FinalCannon 검사.
- 4 실행 로그: SKIPPED
- 5 공동 테스트: 미실시 (PM 결정, 2026-10-19)

## 공동 테스트 결과
- 일시:
- 참여자:
- 재현:
- 예상:
- 실제:
