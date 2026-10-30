# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=ENV_BLOCKED(Unity 환경); 테스트=SKIPPED(자동 테스트 선택 안 함)
- 2026-10-30: 쉬는 시간 단계 조정·단계 버튼 UI·해금 안내 구현. 워크트리에 Unity 에디터(6000.3.25f1)와 `_ThirdParty` 서브모듈이 없어 컴파일·테스트는 실행하지 못함(ENV_BLOCKED). 다음: 에디터에서 컴파일·테스트·화면 확인.
- 2026-10-30: 워크트리를 `_ThirdParty`·LFS 없이 처음 열어 CS 오류 231건, PNG 47건 오류가 났다. 에셋 도우미로 `_ThirdParty`를 26d5009e에 맞추고, LFS 실제 파일을 확인했다. 그때 Unity가 망가뜨려 저장한 폰트 SDF 2개와 escape-labels.asset은 되돌렸다. `_ThirdParty/Kevin Iglesias`의 LFS 92개는 아직 포인터 상태다.

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 2026-10-30 검사: 컴파일=PASS; EditMode `DifficultySelectionTests` 4/4·`ScheduleBookTests` 15/15 PASS; PlayMode `W3_TierButtons_NightChoiceAndRestTimeAdjust`·`W3_EditConfirmReady_ThreeNightsAndFinalMorning` PASS. 첫 PlayMode 실행은 UDP 7777이 메인 체크아웃 MPPM 플레이어와 겹쳐 실패했고, 다시 실행해 통과했다(환경 문제). 화면 배치·실제 공연 연결·멀티 표시는 사람이 확인할 항목이다.

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 2026-10-30 카메라: 밤 → 아침 전환 때 눕기 카메라(`BedRestView`)가 서 있을 때 시선에서 침대 방향 누운 시선으로 Quaternion.Slerp해서, 두 방향이 다르면 화면이 최대 약 55° 옆으로 기울고 좌우로 크게 돌았다. 지금 시선의 좌우 방향은 유지하고 위아래(-75°)만 움직이도록 고쳤다. 회귀 테스트 `W3_BedRestCamera_KeepsYawWithoutRollThroughMorning`은 이전 코드에서 FAIL, 수정 후 PASS. 첫 실행은 외부 `editor status` 명령의 오류 로그 때문에 SetUp에서 실패했고(환경 문제), 다시 실행해 통과했다.
- 2026-10-30 dev 병합: origin/dev(41커밋)를 병합했다. 충돌은 없었고, 양쪽이 함께 고친 파일도 없었다. `_ThirdParty`는 dev의 a151164(기존 26d5009e 이후 커밋)로 맞췄다. 병합 후 컴파일 PASS(오류 0), EditMode 4/4·15/15 PASS, PlayMode `W3_TierButtons…`·`W3_EditConfirmReady…`·`W3_BedRestCamera…` PASS. Unity가 자동으로 바꾼 폰트·escape-labels 변경은 커밋하지 않고 stash(`task082-*`)에 보관했다.
