---
summary: 탈출로 자유비행의 사망 방어는 Fallen뿐 아니라 Retired를 포함해야 한다.
status: active
updated: 2026-11-03
source: Task-20261027-073
---

# 자유비행·관전의 입력/상태 경계

- EscapeInspectionSession.AllowState가 Fallen/Arrived만 제외하면 탈출로 함정이 쓰는 Retired는 자유비행 중에도 통과한다. 검사 중인 참가자는 세 종료 상태를 모두 차단하되 검사 종료 후에는 기존 상태 판정을 복원한다.
- 관전 카메라는 PlayerInputReader.Current.Posture를 직접 읽는다. 메뉴에서 보이는 키를 숨기는 것만으로 입력은 막히지 않으므로 CameraSpectator.ReadDirection도 InputGate.IsBlocked를 확인한다.
- OriginalMechanicsTests의 inspection 상태 검사와 PresentationReuseTests의 관전 입력 차단 검사가 EditMode에서 이 경계를 확인한다. 실제 다인 관전 체감 검증을 뜻하지 않는다.
- 추격 불꽃 사망은 Retired다. 관전은 Retired/Fallen 모두 대상으로 삼지만 A 리그의 CameraSpectator 활성 및 사망 관찰1.5초 뒤 생존 동료 유무까지 확인한다. 현재 PresentationReuseTests는 두 사망 상태의 대상 선택/재시작과 생존자 없음도 확인한다.
