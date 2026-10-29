# Log

## 2026-10-29 참가자 끊김 수정 저장
- 사용자 저장 요청으로 중계 준비/ACK 비동기화·전송 간격·관련 회귀와 기록을 커밋한다. 이전 컴파일·28개 테스트 통과 결과를 재사용하고 verify-fast·diff 공백 검사·gh 작성자를 확인했다.
- private origin/main을 fetch해 확인했으나 98904299가 아직 main에 없어 공개 push의 기존 차단 조건이 유지된다. 우회·private main 병합 없이 로컬 커밋을 보존한다. TMP 폴백 폰트의 별도 변경은 stage하지 않는다.

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
- 구조+사용자 Lobby 씬은 `733ae312` 커밋 완료. 저장 후 Lobby.unity 추가 diff 0이며 별도 TMP 폴백 폰트 변경은 미커밋으로 보존했다.

## 2026-10-29 참가자 초기 끊김 수정
- 사용자 재현: 방 코드(Relay)로 참가한 P2만 3~5초 끊긴 뒤 정상, 다른 PC 에디터/빌드/MPPM에서 보이며 어제까지 없었다. 이 PC에서 실제 두 PC Relay 전체 입장은 재현하지 못했다.
- 코드/비용 근거: 참가자 전용 NetworkLogAdapter.Tick이 NextRelayPath에서 모든 봉인 파일을 정렬·읽기·JSON 검사하고 선택 파일을 다시 읽어 검사/해시했다. ReadReceipt도 메인에서 파일 재독출/해시/삭제, 성공 시 nextRelay=0으로 다음 프레임 즉시 다음 배치를 처리했다. 로컬 대기열 67파일에서 동기 후보 검색 3회 283.47/30.71/30.79ms를 측정했다. 전체 3~5초 프레임 재현과 구분하며 누적 대기열은 환경별로 다르다.
- PrepareRelayAsync/AcknowledgeRelayAsync로 봉인 파일 준비/ACK 작업을 Task.Run으로 옮겼다. Tick은 완료 전 프레임을 양보하고 NGO 전송만 메인에 남긴다. 재시도는 준비된 원본 바이트를 재사용, 정상 ACK 뒤 최소 1초 간격·미확인 재시도 3초. 연결 종료 시 취소·진행 참조 무효화, ID/개수/원본 해시 검증과 host 내구 저장 전 ACK 금지·현재 실행 우선·원본 보존을 유지한다.
- 동일 67파일에서 비동기 작업 제출의 메인 비용은 0.1648/0.0072/0.0082ms였다. worker 작업 시간이나 전체 게임 프레임이 이 값이라는 뜻은 아니다. 측정은 읽기 전용이며 기존 로그를 전송·삭제하지 않았다. `.harness/local/lobby-join-relay-profile.json`에 비교 범위를 기록했다.
- 컴파일 PASS, DurableOutboxTests 24/24·NetworkRelaySchedulingTests 3/3·LanLobby_RecordsRoomSignaturesAndVoluntaryHostExit PlayMode 1/1 PASS. 초기 스케줄 fixture 2건은 Unity Object의 bool 변환으로 Activator 오버로드가 잘못 선택되어 실패했으며 명시 object[]로 수정 후 3개 모두 통과했다. 로비 회귀는 격리 outbox를 사용해 외부 전송 없이 검사했다.
- 폐기한 가설/제한: 인증 제한시간이나 게임 입력/로비 배치를 추측으로 바꾸지 않았다. 이전 UploadLoop 개선과 별개인 참가자 중계 동기 비용을 확인했다. 최초 캐릭터/셰이더/음성 초기화가 모든 환경에서 무관하다는 결론은 아니며, 실제 두 PC Relay 재확인은 남아 있다.
- 결과 파일: `.harness/local/lobby-relay-{outbox-editmode,scheduling-editmode,flow-playmode}.json`. 새 끊김 수정은 미커밋, TMP 폴백 폰트 기존 변경은 보존한다. 공개 push는 앞선 private main 선반영 조건 때문에 여전히 차단 상태다.

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

- 2026-10-29 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
