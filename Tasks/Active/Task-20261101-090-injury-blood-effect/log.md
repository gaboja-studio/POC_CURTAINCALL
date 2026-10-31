# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.
- 2026-11-01 환경: 이 워크트리에서 origin/dev(0fafe124, PR #255 병합 포함)로 새 브랜치 생성. 에디터 종료 확인 후 전환, 다시 열어 사용.
- 2026-11-01 검사: 컴파일 PASS(콘솔 compilationFailed=false). 기본 에셋 메뉴 실행 → 프리팹에 BloodSpurt 스크립트·재질 연결 확인.
  - PlayMode 신규 2개 PASS: 실제 호스트 절단 → 위팔·허벅지 뼈 추적 혈흔 1회 / 1인칭은 카메라 자식·화면 안 / 개인 설정 끄면 미재생, 설정 화면 토글 적용·게임 설정 끄면 항목 숨김·모두 꺼짐.
  - 회귀: SettingsUpgrade 6/6 PASS. HumanoidPlayerTests 19/20 PASS. 실패 1개 `KeyboardEventsReachReaderControllerAndHumanoidAnimation`(앉기 키) — 이 브랜치는 입력·조작 코드 미변경, 이 PC 에디터 PlayerPrefs에 키 재설정 값(`CurtainCall.Input.CommonBindings.v1`)이 남아 있음. 사용자 설정이라 건드리지 않음.
  - 미확인: 실제 2인 이상 팀원 화면(코드상 공유 상태 변화로 모든 화면이 각자 재생), 시각 품질.
- 2026-11-01 추가 버그(사용자 보고, 별도 커밋 예정): 외줄 매달림 중 캐릭터가 오른쪽 ↔ 왼쪽을 프레임마다 번갈아 봄(맨 왼쪽 줄에서 발견).
  - 원인: `HumanoidPlayerPresentation.ApplyHangPose`가 손 방향과 줄 방향 내적 부호로 돌 쪽을 골랐는데, 몸이 줄 방향을 보고 있어 두 방향이 거의 수직(내적≈0) → 흔들림 애니메이션마다 부호가 바뀌어 ±90° 뒤집힘. 혈흔·#255 변경과 무관한 기존 코드.
  - 수정: `RopeHangHandAxis`로 바라볼 쪽을 위치 기준 고정 규칙(코스 가운데 쪽, 가운데 줄·코스 없음은 줄 왼쪽)으로 정함. 모든 화면 동일.
  - 검사: 신규 `HangFacing_EveryLaneStaysOnOneSideFacingCourseCenter`(실제 외줄 씬, 모든 줄 45프레임) PASS. 수정 전 코드로 되돌리면 0번 줄 6프레임째 뒤집힘으로 FAIL(재현 확인 후 원복).
    신규 `RopeHangFacingStaysOnOneSideThroughSwayAndAnimation` PASS, 기존 `HangPalmsStayOnContact…` PASS, `HangRescue_` 4/4 PASS.
  - 참고: 한 번에 넓게 돌린 "Hang" 필터 실행에서 11개가 준비 단계 "기본 LAN 포트로 호스트 시작" 실패 → 개별 재실행은 모두 PASS(포트 환경 문제. 메인 폴더 에디터도 켜져 있어 같은 포트를 쓸 수 있음).
- 2026-11-01 저장: 94873a73(혈흔 연출), e99dd689(매달림 좌우 뒤집힘 수정). 테스트 부산물(폰트 아틀라스 3, Vivox 줄바꿈)은 미커밋.
- 2026-11-01 dev 동기화: 에디터 종료 확인 후 origin/dev(fe0cc441, PR #256 아이템 아트·부상 상호작용) 병합 5a7522d2. 충돌 없음.
  양쪽이 바꾼 파일은 `HumanoidPlayerPresentation.cs` 1개 — dev 아이템 레이어·정렬과 이 브랜치 매달림 손 방향이 모두 유지됨을 확인. dev에 혈흔 관련 코드 없음.
  병합 후 검사: 컴파일 PASS. BloodEffect 2/2, HangFacing 2/2 PASS. HumanoidPlayerTests 18/21 —
  실패 2개(`CrawlSlopeAndJump…`, `InitialInjuryRebind…`: 기대 "Crawl", 실제 "ItemCrawl_Cut2/3")는 dev(PR #256)가 기어가기 상태 이름을 바꾸고 테스트를 갱신하지 않은 기존 dev 문제, 1개는 이 PC 키 재설정 값(앞 기록).
- 2026-11-01 제출: PR #257 → dev https://github.com/gaboja-studio/POC_CURTAINCALL/pull/257 . `new-pr.ps1`은 미커밋 테스트 부산물 때문에 등록을 막아 같은 제목·본문으로 gh 직접 생성. 남은 일: 지정 팀원 검토, 사람 플레이 확인.
