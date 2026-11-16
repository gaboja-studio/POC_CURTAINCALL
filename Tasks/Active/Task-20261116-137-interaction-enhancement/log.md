# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

## 2026-11-16
- 기획 공백 7건 사용자 확인 후 Confluence "구현 세부 규칙"에 기록(v2·v3): 우선순위 분류, 아웃라인(범위 안 얇게+선택 강조), 적용 장면(전 장면), 목마 규칙(대기실만), 거리 기준(화면 외곽), 동순위(기준점 우선), 공중 잡기 제외, 연습 장치 1순위, 탈출로=공연 기준.
- 다른 작업자 커밋 49614ab6(fix/waiting-room-bugs-20261116) 파일(NightBed·ExplorationGate·ExplorationPanel·WaitingRoom.unity)은 수정하지 않음. 침대 상태는 NetworkPlayer.RestingBed로 PiggybackSystem에서 감지.
- 이 worktree에 Unity가 열리며 머티리얼 6개·WaitingRoom.unity(outsideIsSafe)·Vivox Settings.json이 재직렬화됨. 이 Task 변경이 아니므로 커밋에서 제외할 것.
- 검사: 컴파일 PASS. EditMode InteractionFocusRulesTests 3/3, CommonRulesFollowupTests 21/21, InputBindingPreferencesTests 30/30(키 개수 15→16 갱신), CommonMechanicsTests 28/28, WaitingRoomTutorialBookTests 10/10 PASS.
- check-work: Fast FAIL(verify-knowledge, 새 결정 문서 frontmatter 누락) → 수정 후 Knowledge OK. Context WARN은 기존 다른 Task 문서 길이.
- 다음: 사람 플레이 확인(대기실 상자+기물+목마 겹침, 공연 대포·봉, 기준점 크기·범위 체감).
- PlayMode InteractionPresentationPlayTests: `InFocusOnly_...`(새 규칙으로 갱신) PASS 2회. `ActualPhone_...`는 카메라가 전화기 피벗(메시에서 0.45m 떨어짐)을 보던 것을 수화기 메시를 보도록 수정 → 외곽선·안내·Focused 단언 PASS, 이후 F로 창 열기 단언은 같은 코드에서 1회 PASS·2회 FAIL(선택·Focused는 매번 전화기, 실패 메시지에 진단 추가). 타이밍 의존 불안정으로 판단, 사람 플레이로 F 열기 확인 필요.
- `WorldLabels_...`(Night 기대·RestTime), `ExpertLabel_...`(영업 중 기대·공연 중 휴업) FAIL: 캠페인 진행 단계 문제로 상호작용 변경과 무관(History/001에도 같은 테스트군 기존 실패 기록). 이번 Task에서 수정하지 않음.
- 탈출로 협동 버튼·절단기 배선 안내 후보는 외곽선·기준점 판정을 공통 상호작용 거리 안으로 제한(InCommonReach).
- 사용자 플레이 피드백(2026-11-16): 바닥 기물을 잡을 수 없음(카메라 상하 각도 제한으로 기준점 범위 8%에 못 들어감), 바닥 기물 작은 이름표 제거 요청.
  → 사용자 결정: 기준점은 범위를 거르지 않고 순서만(분류 순위 → 기준점 거리 → 플레이어 거리), 아웃라인은 상호작용 거리 안 전부 얇게+선택 강조. 설정의 기준점 범위·공중 예외·범위 표시 항목 삭제. FloorPropsView 이름표 제거. Confluence v5 갱신.
  검사: 컴파일 PASS, EditMode InteractionFocusRulesTests 3/3·CommonRulesFollowupTests 21/21·CommonMechanicsTests 28/28, PlayMode InteractionPresentationPlayTests 2/4(InReach…·ActualPhone PASS, 캠페인 단계 테스트 2건은 기존 실패).
- 아웃라인 설정 보강: `showInRange`(거리 안 다른 대상 표시 on/off) 추가, 거리 안/선택 대상 색에 알파(투명도) 편집 허용(ColorUsage showAlpha). 합성 셰이더가 SrcAlpha 블렌드라 알파가 투명도로 적용된다. 검사: 컴파일 PASS(사용자 요청으로 검증 최소화, 플레이 확인은 사용자).
- 제출: origin/dev 병합(충돌 없음, 컴파일 PASS) 후 PR https://github.com/gaboja-studio/POC_CURTAINCALL/pull/395 . Unity 재직렬화 파일(머티리얼·WaitingRoom.unity·Vivox 설정)은 제외, 씬 변경은 stash "task137-unity-reserialized-waitingroom-scene"에 보관.
