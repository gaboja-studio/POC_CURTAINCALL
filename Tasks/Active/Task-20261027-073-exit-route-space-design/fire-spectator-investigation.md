# 화염 사망 후 관전 조사

2026-11-03 / @RagSpirin / feat/exit-route/design-map.

## 확인 결과

- 직전 재사용 UI 조사 시 A의 CameraSpectator가 비활성이었다. 당시에는 사망 원인과 무관하게 관전 시작 자체가 막혔다. 직전 보수에서 활성화와 공용 C11 표시 연결을 저장했다.
- 이번 저장 씬 재감사: 관전 컴포넌트 enabled/active=true, 같은 리그의 CameraViewDirector 연결 있음. 열린 A dirty0, Play 중지.
- 마지막 EscapePursuitFire는 접촉 시 호스트에서 NetworkPlayer.ServerKill을 호출한다. 탈출로 사망은 SurvivalRules.DeathState(EscapeRoute)에 따라 Retired가 된다.
- CameraViewDirector.IsDead와 CameraSpectator.UpdateSpectating 모두 Retired/Fallen을 사망으로 취급한다. 상태 이름 차이 때문에 화염만 관전에서 빠지는 분기는 발견하지 못했다.
- 현재 A 일반 FireJet14개는 KillsOnContact=false로 절단 판정, 추격 불꽃1개는 기존 즉사 판정이다. 일반 불에 닿아 아직 살아 있다면 관전으로 전환하지 않는 것이 현재 확정 규칙이다.

## 관전 시점

1. 사망 직후 약1.5초 동안 사망 위치를 보여 준다.
2. 다른 Normal 상태 동료가 있으면 해당 동료를 대상으로 선택한다.
3. 생존자가 없으면 관전 대상 없음으로 남는다. 전체 종료/개발 재시작은 기존 게임 흐름이 처리한다.
4. 정상 상태로 재시작하면 자신의 카메라 대상으로 복귀한다.

## 검증과 판단

- 신규 EditMode 회귀3가지: Retired→생존 동료, Fallen→생존 동료, Retired→생존자 없음. 사망 관찰 대기·대상 선택·같은 사망으로 대기 재시작 금지·정상 복귀를 직접 검사했다.
- PresentationReuseTests6/6, PursuitFireTests14/14, FinaleHazardTests4/4 PASS. 총24개이며 실제 네트워크/플레이 검증은 아니다.
- 증거: `.harness/local/reuse-fix/fire-audit.json`, `fire-spectator-tests.json`, `fire-PursuitFireTests.json`, `fire-FinaleHazardTests.json`.
- 이전 비활성 설정은 제보 현상을 설명하는 유력 원인이다. 사용자가 겪은 당시 화면을 재현한 것은 아니므로 그 원인만 있었다고 확정하지 않는다. 현재 저장 상태의 사망→관전 코드 경로는 위 편집 회귀에서 통과했다.
- 이번 작업은 회귀 검사와 조사 문서를 추가했다. 화염 피해·관전 시간·성공 조건 등 게임 동작은 새로 변경하지 않았다. 실제 Play/MPPM은 실행하지 않았다.
