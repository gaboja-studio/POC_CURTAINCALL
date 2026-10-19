# Handoff

## 지금 상태
- 단계: 구현 완료(코드·입력 에셋·설정·도메인 문서·기존 테스트 확장). Unity 컴파일·테스트 미실행(디스크 부족으로 ENV_BLOCKED).
- 동작하는 것(코드 기준, 미검증): LShift/LCtrl 홀드 역할, 플랫폼·배치 미리보기·인간 대포 플랫폼 공용 달리기 1.5·앉기 0.5·높이 0.6, 앉기 점프 금지·기립 공간 검사, 모델 슬롯 발 기준 축소, 1인칭 눈높이 두 경로, 앉기 상태 온라인 공유, 줄 위·대포 탑승/비행 미정 로그, 외줄 전환기·배치 미리보기의 자기 규칙만 해제.
- 막힌 것: 첫 PC는 디스크 여유 2.7GB, worktree에 `Library/` 없음 → Unity 임포트·컴파일 불가. 다른 PC에서 이어서 하려고 미검증 작업 중 커밋으로 `feat/player-locomotion`에 올림(2026-10-19 사용자 승인). PR·병합 없음.
- PR: 없음

## 다른 PC에서 이어 하기
1. `git fetch origin` → `git switch feat/player-locomotion` → `git submodule update --init -- Assets/_ThirdParty`(private 읽기 권한 필요).
2. `git lfs version`이 3.3 미만이면 업그레이드(하네스가 `git lfs ls-files --json` 사용). `pwsh -File Scripts/setup-gh.ps1` → `pwsh -File Scripts/doctor.ps1`.
3. Unity로 열어 임포트(디스크 10GB 이상 여유 권장) 후 아래 "다음 할 일" 1번부터.

## 다음 할 일
1. Unity 임포트 후 `pwsh -File Scripts/check-work.ps1 -Mode EditMode`.
2. `pwsh -File Scripts/verify-unity.ps1 -ProjectPath <이 worktree 절대경로> -RunTests -Mode PlayMode` (CameraViewsPlayTests의 Locomotion_* 3개 포함).
3. 실패 구간 수정 후 관련 검사 재실행 → plan.md 공동 테스트 3항목을 사용자와 진행.

## PM이 확인할 것
- 확인 요청: 앉은 채 두 다리를 잃는 순간 다리 손실 모델 위치 보정(`LimbDismemberment`, 수정 범위 밖)이 축소된 슬롯 상태에서 측정될 수 있음 — 공동 테스트 3에서 관찰.
- 확인 요청: 외줄 `CourseControlSwitcher`가 해제될 때(로컬 플레이어 사라짐·코스 비활성화) 자기가 끼운 규칙을 빼고 기본 규칙으로 돌린다. 이전에는 플랫폼 규칙이 남아 있었다.
- 확인 요청: 저장소의 `.githooks/pre-push`가 실행 권한 없이(100644) 커밋돼 있어 push 때 무시된다(기존 문제, 이 Task 범위 밖).
- 대기 중인 결정: 없음
- 공용 파일 요청: 없음(`PlayerRagdoll.cs`는 배정됐으나 모델 슬롯 분리 처리로 수정 불필요)

## Verification
- 구역 검사: OK (22 files, 2026-10-19)
- 1 문서/규칙: OK (verify-fast, 경고 9·한도 초과 0, 2026-10-19)
- 2 컴파일: ENV_BLOCKED — 디스크 부족·worktree Library 없음, Unity 에디터 미연결(STATUS_NO_INSTANCES)
- 3 테스트: SKIPPED (컴파일 미실행). 추가: EditMode `ControlRulesTests` Locomotion_* 4개, PlayMode `CameraViewsPlayTests` Locomotion_* 3개
- 4 실행 로그: SKIPPED
- 5 공동 테스트: 미요청

## 공동 테스트 결과
- 일시:
- 참여자:
- 재현:
- 예상:
- 실제:
