# Plan

## Context

사용자 제보: 첫째날 인간대포 사망 후 밤 정비 부활 로그와 달리 대기실에서도 관전이 지속된다. Editor.log 6673·6689·6715행에서 밤 정비 → 사망자 1명 부활 → 대기실 전환을 확인했다. 실제 재사망 상태 전이는 기존 로그에 없다.

## Approach

1. 출발점 확인 후 낙하 사망 → 밤 정비 부활 → 옛 씬 유지 회귀로 재현한다.
2. CampaignPerformanceEntry의 기존 서버 가드에 현재 공연 유효성 검사를 결합한다. 씬 전환 완료 전의 낙하·직접 피해를 거부한다.
3. 단독 연습·공연 중 사망·입장 보호·가드 복원 동작을 유지한다.
4. 기존 전환 PlayMode 회귀를 확장해 부활과 관전 복구를 확인한다.

## Files

- CampaignPerformanceEntry.cs, PerformanceEntryJudgeTests.cs, PerformanceLinkPlayTests.cs (모두 setup.md에 명시).
- 읽기 전용: NetworkPlayer, InjuryRevival, HumanCannonRouteSystem, CameraSpectator.

## Risks

- 부활 이전 지연 RPC를 reviveCount로 검증하지 않는 별도 공백은 이번 최소 수정 재현 결과에 따라 후속 판단한다.
- 합성 1인 LAN 검증은 실제 원격 참가자 공동 검증이 아니다.
- Task-062의 넓은 잔류 소유는 현재 Task에 필요한 세 파일만 재배정한다. 기존 Task 문서·검증 기록은 보존한다.

## Auto Verification

- 구역 → 문서 → 컴파일 → 관련 PlayMode 순으로 실행. 실패·생략을 handoff에 기록.
- 수정 전 실패와 수정 후 통과를 구분한다. 기존 fixture의 상태 주입은 로직 회귀로만 보고한다.
- 넓은 회귀 비교: 수정 적용 8/11, 가드 변경 제외 7/11. 대기실 회귀만 Normal 대신 Fallen으로 추가 실패했다. 줄타기 도착/정산 3건은 양쪽에서 동일한 시간 초과이며 이번 범위에서 수정하지 않는다.
  - Managed_CampaignChangesInsideResultEvent_DiscardsOldSettlement
  - Managed_LatePerformanceStart_PreservesInjuryAndSettlesExactlyOnce
  - Standalone_SyntheticStartAndFinish_KeepSessionEndAndGuardedRetry

## 공동 테스트 항목

2026-10-20 승인 계획 기준. 기능 완성 후 한 번에 확인하며, 미실행이면 미검증으로 기록한다.

1. 호스트가 첫째날 인간대포에서 사망 → 밤 정비 대기실: 외팔·Normal·이동·관전 해제 유지.
2. 참가자가 같은 경로로 사망·복귀: 지연된 상태 수신 후에도 재사망 없이 정상 복구.
3. 생존자 정상 복귀 및 다음 공연의 정상 사망 판정 유지.
