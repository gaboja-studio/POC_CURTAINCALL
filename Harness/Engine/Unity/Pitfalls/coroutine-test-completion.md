---
summary: 코루틴 테스트에서 한 번 yield한 뒤 Update·지연 Destroy·연출 전환이 끝났다고 가정하지 않는다
status: active
updated: 2026-10-29
source: Task-20261027-076 Humanoid·더미 회귀 테스트 조사, Task-20261027-074 외줄 통합 실패 조사
---

# 코루틴 완료 시점 검사

## 언제 읽나

`yield return null` 뒤 애니메이션 정지·`Destroy`·결과 연출·씬 전환 검사가 실패하거나 실행마다 결과가 다를 때.

## 원인

- 코루틴 재개 시점은 검사 대상의 `Update`나 프레임 끝 파괴가 완료됐다는 보장이 아니다.
- 정산 데이터 확정과 결과 연출 후 시간대 전환은 별도 시점일 수 있다.
- 기다림을 없애거나 제품의 연출을 삭제해 테스트를 맞추면 실제 계약이 달라진다.

## 대처

1. 실제 완료 상태를 조건으로 기다리고 실시간 제한을 둔다. 제한 후에도 최종 assertion으로 실패를 보고한다.
2. 지연 파괴는 즉시 비활성/장부 제거와 최종 Unity null을 나누어 확인한다.
3. 완료를 무한 대기하거나 긴 고정 sleep으로 덮지 않는다. 실행 결과와 미검증 사항은 Task log에 남긴다.

## 입력 준비 시점

- `Running`/`PerformanceOpen`만으로 참가자 배치 확인과 입력 재개가 끝났다고 가정하지 않는다. 외줄 fixture는 `IsParticipantReady(player)`와 `!InputGate.IsBlocked`를 함께 기다린다.
- `PlayerInputReader`를 활성화하면 `InputGate.Register`가 키 해제 대기를 재설정한다. 같은 프레임에 `CommonInteractionUI.LateUpdate`를 직접 호출하면 정상적으로 숨김 처리된다. 리더 활성화 후 입력 재개를 조건 대기하고 카메라 시점을 다시 맞춘 뒤 UI를 검사한다.
- `InteractionPromptView.Show` 직후에는 진입 연출의 alpha가 0일 수 있다. `IsVisible`을 제한 시간 안에서 기다리되, 테스트 카메라 시점이 매 프레임 유지되는지도 확인한다.
- 테스트에서 gate를 강제로 해제하지 않는다. 실제 차단·키 재무장 계약을 그대로 통과해야 한다.

## 프로젝트 사례

Task-076: Humanoid 미리보기는 `IsPaused`, 복구 파편은 Unity null, 더미 공연은 `CampaignPhase`를 기다리게 했다. 더미의 정산 확정은 즉시지만 Tightrope/HumanCannon 결과 연출 대기는 기본5.8초였다. 최신 실행 결과는 해당 Task log를 참조한다.
