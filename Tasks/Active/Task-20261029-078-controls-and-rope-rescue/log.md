# Log

## 2026-10-29 브랜치 준비
- `fix/controls-and-rope-rescue` 생성·전환. 기준 dev/origin/dev `935f5bd6`, 담당 @MoHoDu. 코드 수정·commit/push 없음.
- 요청한 키 매핑/저장 정책과 구조 판정/균형 보호 요구사항을 plan·todo에 기록했다. 다음 작업은 조작 탭 목표 순서 확인과 현재 구현 조사다.
- doctor 1회 실행: FAIL(private submodule 미초기화·기존 Task-077 중복), WARN(Unity CLI 버전 차이·에디터 미연결·skill 링크 없음·gh 미로그인). 브랜치 준비는 가능하며 Unity 실플레이는 미검증이다.
- 샌드박스에서 Git LFS가 실패해 변경으로 표시된 5개 에셋은 권한 확장 후 정상 Git status로 clean임을 확인했다. 기존 에셋·Git 설정은 변경하지 않았다.

- 2026-10-29 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=SKIPPED(Unity 변경 없음); 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-10-29 설정창 수정
- 사용자 후속 지시: 설정창 먼저, Unity는 CLI만 조작. 이동 표시만 전방→후방으로 확정해 다른 순서와 입력 에셋/GUID는 보존했다. 구조 코드는 수정하지 않았다.
- CommonSettingsUI는 캡처한 키를 View draft에만 전달한다. 중복 키는 기존 조작명을 표시하는 예/아니오 확인 후 원래 키를 비우고 옮긴다. 아니오·ESC는 기존 draft를 보존한다.
- 미할당은 변경 가능한 행으로 유지하며 모든 탭에서 적용 버튼·직접 RequestApply를 차단한다. 다른 탭 초기화도 빈 키 draft를 보존한다. 저장 서비스도 빈 키를 거절해 기존 저장값을 유지한다.
- private `curtaincall_settings_v2` SettingsPanelView.cs/SettingsData.cs에 확인창·미할당 편집을 연결했다. private에도 `fix/controls-and-rope-rescue`를 만들었으며 commit/push·gitlink 변경은 없다. 기존 에셋·프리팹·meta는 수정하지 않았다. Task-077과 겹치는 CommonSettingsUI 및 private 설정 UI 두 파일은 제출 시 함께 반영해야 한다.
- 컴파일 PASS. InputBindingPreferencesTests EditMode 18/18, SettingsUpgrade PlayMode 5/5 PASS(중복 확인/아니오/ESC·빈 키 적용 차단·탭 이동·취소 폐기·적용 후 재열기·기본값 복원 및 기존 그래픽/음성/런 포기 회귀). 결과: `.harness/local/settings-input-editmode.json`, `settings-upgrade-playmode.json`.
- 시작 시 CLI가 인스턴스를 못 찾았으나 이후 `pipeline list`·`status`에서 현재 프로젝트 PID/port 연결을 확인했다. 첫 run_tests 연결 오류는 실행 안 됨을 확인하고 현재 명령 스키마의 editor/playmode 비동기 모드로 실행해 완료했다. 개인 인증값은 추적하지 않는다.
- 다음: 설정창 사람 최종 체감 확인 후 별도 묶음으로 줄 매달림 팀원 구조 판정·균형 보호를 진행한다. commit/push 미실행.

- 2026-10-29 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-10-29 구조·사용자 로비 저장 및 참가자 끊김 착수
- 사용자 요청: 구조 수정과 직접 편집한 Lobby.unity를 먼저 저장하고 두 번째 참가자의 입장 후 3~5초 끊김을 수정한다.
- Lobby.unity의 안내 글꼴/색상·가구 위치/회전·콜라이더·Canvas 채널 변경을 그대로 포함한다. 별도의 LiberationSans SDF - Fallback.asset 변경은 제외·보존한다. Unity 열린 Start 씬은 dirty 0이다.
- 앞 구조 검사(균형 EditMode 36/36·호스트 PlayMode 3/3)를 재사용하며 저장 직전 규칙·컴파일을 확인한다. 공개 push의 private main 선반영 차단 조건은 아직 같아 우회하지 않는다.

## 2026-10-29 설정창 저장·구조 수정 착수
- 사용자 요청으로 설정창 변경을 먼저 저장한다. private 설정 UI는 `98904299`로 커밋하고 같은 이름의 원격 작업 브랜치에 push했다. 공개 저장에는 해당 gitlink와 앞 묶음의 코드·검사·Task 문서를 함께 포함한다.
- `save-work` 절차를 사용하며 앞 묶음의 EditMode 18/18·PlayMode 5/5 결과를 재사용한다. Unity 조작은 CLI만 사용한다.
- 공개 `71bb378c` 커밋 저장 완료. 공개 push는 pre-push의 `verify-third-party -CheckSubmodule`이 private `98904299`가 main에 없다고 거절했다. private 작업 브랜치에는 push됐으나 main 병합·훅 우회는 하지 않았다. 공개 로컬 커밋을 보존한 뒤 다음 수정에 착수했다.

## 2026-10-29 줄 매달림 팀원 구조 수정
- 원인: 구조 시작 수평 1.5m·코스용 높이 허용만 사용하고 이동 입력이 있으면 F를 거절했다. 구조 중 보정 입력은 막지만 PlayerBalance 자체의 자연 흔들림/위험 누적은 계속됐다.
- HangSettings 초기값 수평 2.5m·전용 높이 허용 0.75m로 완화하고 이동 중 F를 허용했다. 시작 프레임 이동 입력을 지우며 공중·낮은 매달림 높이·그네/리프트/해제봉 탑승은 계속 거절한다. 안내와 호스트가 같은 WithinRescueReach를 쓴다.
- 호스트가 승인한 장부의 구조자에게 로컬 소유자만 IsRescueSupported를 적용한다. 클라이언트는 ReadState 수신 즉시 갱신한다. 자연 흔들림·보정·균형 충격·추락 위험 누적을 정지하며 IsSuspended와 별도로 관리한다. 완료/취소/거리 이탈/대상 소멸/부상/라운드 변경/세션 이탈/비활성화 후 해제한다. 위험물의 직접 ForceFall은 유지한다.
- Unity CLI 컴파일 PASS, TightropeBalanceFeedbackTests EditMode 36/36·HangRescue_ PlayMode 3/3 PASS. 실제 씬의 LAN 호스트와 합성 원격 소유 캐릭터로 범위·높이·공중 거절·이동 중 입력·2초 전체 보호·완료/취소/이탈/소멸/비활성화 해제를 검증했다. 실제 두 클라이언트 RPC 지연 검증은 아니다.
- Task-074와 겹치는 TightropeHangSystem(.Network)·PlayerBalance·TightropeSettings 변경에 주의한다. 씬/프리팹은 변경하지 않았다. 구조 수정은 아직 미커밋이며 원격 공개 push 차단은 위 기록대로 남아 있다.

- 2026-10-29 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-29 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
