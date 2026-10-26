# 공연 UI 고도화 검증 · 2026-10-20

## 수신
- origin/dev864ed37 → f0ceb0b. 인간대포 공간 씬은 dev를 채택한 뒤 UI/호응 연결을 Editor에서 재적용했다. 최신 아트/묘기 코드를 유지한다.
- 원본 프리팹 재생성 없음. Spectator/WorldLabels 정본 인스턴스만 배치. 설치 첫 호출은 Pipeline5초 응답 제한에 걸렸으나 씬 저장은 끝났음을 조회로 확인했다. 이후 idempotent 재실행 성공.

## 실행 근거
- 최종 verify-unity -Compile -RunTests -Mode PlayMode -Filter CurtainCall.UI.SceneIntegration.Tests: 컴파일 PASS,21/21 PASS(동료 사망 재생 방지 포함).
- 외줄타기에서도 같은 합성 검증 성공: frame452→505, 대상2→가상 D→대상3→NoTarget→부활 Hidden. upgrade-tightrope.txt/png 및 upgrade-tightrope-empty.png.
- 최초 UI 테스트20개 중 WorldLabel 동적 Configure 후 원래 표시 복원1개 실패. Configure의 원래 enabled 상태 보존을 수정하고20/20 통과했다.
- 테스트에 TMP참조가 없어 첫 컴파일 실패; 테스트 asmdef 참조 추가 후 컴파일 PASS. 이후 빈 오류 응답은 에디터 재컴파일 완료를 확인한 뒤 다시 검사했다.
- 호스트 합성: 실제 NetworkPlayer를 소유자99/100으로 추가, CameraSpectator 기존 로직 확인. Spectating/플레이어2 → 가상 Keyboard D → 플레이어3 → 전원 사망 NoTarget → 부활 Hidden. 기존 IMGUI false, 관전 카메라 컴포넌트는 활성 유지. 별도 실제 클라이언트 접속 검증은 아니다.
- 인간대포 정본 라벨42개 중 기존 활성36개 표시, 기존 비활성6개 숨김, 원래TMP와 중복0. 구간 문구의 누락 글리프를 런타임 fallback으로 보완한 최종 캡처에서 한글 확인.
- 인간대포 성공 합성: 호응80/수입80/잔액0→80/대성공/자동전환4초 표시 확인. 최신 아트 로드로9초 시점엔 전환 중이었으며 후속 조회에서 WaitingRoomBase, black=false/input=false/transitioning=false 확인.
- 근거 파일: .harness/local/upgrade-spectator.txt, upgrade-two-targets.txt, upgrade-result.txt 및 PNG. 합성 캐릭터·입력·씬 위치는 저장하지 않았다.

## 미실행·남은 제한
- check-work: Scope FAIL373(병합 이력의 타 Task 파일 포함), 뒤 단계 SKIPPED. 개별 verify-fast는 OK/기존 경고9, git diff --check 통과. 개별 컴파일/21개 테스트 결과는 위와 같다.
- 현재 수정분만 verify-scope -Base HEAD: FAIL1. CameraSpectator.cs가 Task062 구역과 중첩된다. 본 Task setup에 사용자 요청에 따른 표시 속성 예외를 기록했으나 타 Task 배정까지 변경하지 않았다.
- 실제2~4클라이언트, 실제 키보드 완주/불 접촉, 연결 해제/재접속, 다른 해상도/빌드, 전체 스위트는 미실행.
- 일부 기존 개발 IMGUI/음성 상태 패널이 남는다. 이번에는 관전의 중복 IMGUI만 대체했다.
- 에디터 과거 VectorGraphics 미리보기의 정점 한도 오류와 이번 설치 응답 타임아웃은 최종 게임 컴파일/테스트 결과와 분리한다.
- 최종 상태: Start 씬/Play 정지/runInBackground=false, console_status compilationFailed=false/consoleErrors=0. 검증 중 자동 저장된 재질3개의 소수점 재직렬화만 원래 값으로 돌려 그래픽 변경을 남기지 않았다.

## 후속 편집 미리보기·임시 UI 정리
- [편집 가이드/제거 목록](../../../Docs/Guides/Ui-scene-integration/layout-editing.md). SceneUiLayoutPreview와 SceneUiCleanup 추가. 사용자 P1 PosY80 유지.
- 임시 씬으로 표시→위치160→120 수정→저장 전 숨김 복원→재로드 후 숨김/위치 보존 확인. 임시 씬은 제거했다.
- 구형 UI 제거 후 UI PlayMode21/21, 실제 외줄 탑승/경고폭/이동속도 검사1/1 PASS. 최초 외줄 검사는 Unity Multiplayer Editor Matchmaker ApiOperation 예외로 SetUp 중단, 재실행 통과. 최초/최종 결과를 cleanup-first-test.json/cleanup-tightrope-test.json/cleanup-ui-tests.json에 보존.
- 인간대포 호스트 합성: 표지42/표시36/중복0, 대상2→가상D→3→대상 없음→부활 숨김 확인(cleanup-cannon.txt/png). 실제 원격 클라이언트가 아니다.
- 정리 도구 첫 호출은 Pipeline5초 제한이었으나 저장 후 원래 씬 복원/dirty=false/구형 게이지0을 조회로 확인했다.
- Fast OK(기존 경고9). 미실행 범위는 위와 같다. 최종 편집 씬은 사용자가 보던 Tightrope로 복원하고 P1 편집 미리보기를 켠다.
- 최종 컴파일 PASS. 정리 후 Scope -Base HEAD는 기존 CameraSpectator/Task062 중첩1건 FAIL, diff --check PASS. 테스트 자동 변경 폰트/재질4개만 수신 원본으로 복원했다.
- 최종 Scene View 캡처 layout-preview-clean.png: 구형 게이지 없이 새 P1만 표시, PosY80/alpha1(임시)/dirty=false. Play 정지, runInBackground=false.
