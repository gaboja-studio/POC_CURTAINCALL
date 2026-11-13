# 실게임 엔딩 연결 결과

2026-11-12 · feat/escape-ending-concept · @RagSpirin

## 동작
- 기존 키트 Canvas/행 프리팹을 실제 escape_route 엔딩에 연결했다. 플레이어·최종 기물·공연별 누적 통계·최고 기록·수입/지출은 게임 상태에서 공급한다.
- 성공 집계 후 실제 소유자 플레이어를 밤 갈대밭으로 배치하고 기존 PlayerMover/달리기 애니메이션으로 달린다. Cinemachine이 낮은 정면에서 추적한다. 원경 서커스는 점점 멀어지고 120m 바닥3개가 뒤에서 순환한다.
- 일반 입력은 잠그되 UI는 사용 가능하다. 6초 뒤 결과창이 열리고 호스트의 버튼 전에는 로비로 가지 않는다. 통계 재동기화는 현재 탭을 유지한다.
- 로비 환경 활성화 후 도착/리타이어 상태와 신체를 복구한다. 세션 Ended는 유지하며 기존 레버 F를 사용해야 다시 준비한다.

## 실제 화면
![플레이어 결과](Previews/actual-ending-players.png)
![런 통계](Previews/actual-ending-statistics.png)

일반 에디터의 실제 LAN 호스트1명 캡처다. 빠른 검사에서 공연을 건너뛰었으므로 통계 —/0은 실제 미시도 값이며 예시 성과를 넣지 않았다. 원본 Camera를 RenderTexture로 렌더할 때 UI Canvas만 일시적으로 ScreenSpaceCamera로 바꿨다가 복구했다. 이미지 보정/합성은 없다.

## 검사 근거
| 검사 | 결과 |
|---|---|
| Unity6000.3.25f1 컴파일 | PASS, compilationFailed=false |
| 저장된 씬 | missing script0, Canvas/자식 View/모델 순서/바닥3개 참조 확인 |
| 신규 누적 통계 EditMode | 3/3 PASS |
| 기존 EscapeEndingTests | 4/4 PASS |
| 실제 엔딩 | 원본 NGO Arrived 플레이어 moving/sprint=true, 활성 Cinemachine front tracking |
| 반복 결과 동기화 | 통계 탭 유지 |
| 버튼 복귀 | 실제 씬 Lobby, Normal, control/simulated=true, InputGate 해제 |
| 실제 InputSystem 키 입력 | W 0.5초에1.5503m 이동, F 레버로 Ending/Ended→None/Waiting |
| check-work | 안전성/구조/지식 PASS, 기존 Task-073 log132줄>130으로 문서 FAIL; 후속 Unity 단계 생략. 위 Unity 검사는 별도로 수행 |

`Verification/`에 CLI 결과 JSON을 보관한다. 전체 보상65개 최초 검사에서 신규 fixture 오류3개를 고쳐 신규3개 재검사 통과. 남은 기존 AudienceReactionTests.CutIsAwardedOnceAndSurvivorDoublesCheer는 Expected50/Actual150이며 이번 변경은 보상 계산 규칙을 수정하지 않는다. 전체65개가 통과했다고 기록하지 않는다.

## 재현
일반(비배치) Unity에서 작업 프로젝트를 열고 Pipeline `run_script`로 `Tools/ending-integration-probe.cs`를 실행한다.
1. Edit Mode `PrepareStart`, Play 진입, `HostAndEscape`: 실제 Start→LAN 호스트→Lobby→Escape 경로.
2. `CaptureEnding`: 서버 도착/성공 경계를 주입하고 8초 기다려 두 탭 캡처. 전 구간 수동 완주 검사는 아니다.
3. `ReturnAndInput`: 실제 버튼 PointerClick→씬 전환→W→레버 근처 배치→F. 상태/이동/준비 전환이 실패하면 예외를 낸다.
4. Play 종료 뒤 `Restore`: 가상 키보드와 원래 Dev Mode 복구.

## 한계·보존
- 2~4개 실제 클라이언트, 생존/리타이어 혼합, 부상별 애니메이션·네트워크 지연은 추가 플레이 확인이 필요하다. 원본 소유권·NetworkTransform 경로를 유지했다.
- C16 커튼은 배치 모드를 의도적으로 거부한다. 배치에서 실패한 전환은 일반 에디터의 실제 흐름으로 다시 확인했으며 커튼 보호 규칙은 수정하지 않았다.
- 원본 프로젝트/에디터의 사용자 변경은 보존했다. 작업용 private worktree에 키트가 설치돼 있으며 public/private 커밋과 push는 하지 않았다.
- 씬은 Unity API로 저장했다. 기존 fileID 삭제0 확인, 추가 rig/컴포넌트 외 큰 줄 변경은 네이티브 직렬화 순서 변경을 포함한다.
