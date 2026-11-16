# Log

## 2026-11-13
- doctor: task-ids 중복(기존 Task들)·unity-editor 버전 FAIL은 이 작업과 무관. 에디터 연결은 정상.
- 테스트: CannonGroundMarkerTests 4/4, CannonLandingFeedbackTests 4/4(EditMode), GroundMarker_ShowsOnJumpAndFlight 1/1(PlayMode, 내 캐릭터 외곽선 확인 추가) 통과.
- CannonLandingFeedbackTests의 기본 외곽선 색 기대값(0.85)을 설정 색으로 변경.

- 2026-11-13 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- check-work: 안전성·문서/규칙·컴파일·공통 설정 PASS, 테스트는 위 개별 실행으로 대신(SKIPPED).
- 운반자 옵션 추가 후 같은 테스트 9개 재실행 통과. 운반자 판정 자동 테스트는 없음(사람 확인 항목).
- 외곽선 색 → 플레이어 옷 색으로 변경. 옷 색 규칙을 `NetworkPlayer.OutfitColor`로 모아 HumanoidPlayerPresentation도 사용. 같은 테스트 9개 통과(PlayMode에 옷 색 일치 검사 추가).

- 2026-11-13 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 안전 체크 높이·안전/비안전 색 설정 추가, 구간 판정을 `LandingMarkerSettings.BandAt`으로 분리해 EditMode 테스트. 같은 테스트 9개 통과.

- 2026-11-13 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 공통 착지 그림자 분리 후: EditMode LandingShadowSpaceTests 3, CannonGroundMarker 4, CannonLandingFeedback 4, RouteCourse 18, RehearsalCannonSource 3 통과. PlayMode 인간대포 GroundMarker 1(바닥 점프는 안전 색으로 기대값 변경), 외줄 RopeJump_ShowsCommonLandingShadow 1(신규), RopeJumpReachesConfiguredHeight 1 통과.
- 테스트 실행 때마다 `Pretendard-Bold SDF.asset`이 바뀌어 되돌림(작업 무관).

- 2026-11-13 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 착지면 자르기 추가: SurfaceClip EditMode 1 신규, 착지 표시 EditMode 12, PlayMode 외줄·인간대포 2 통과. 임시 씬 렌더로 줄(띠)·발판 모서리(잘린 원)·일반 바닥(원) 모양 확인.

- 2026-11-13 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 좁은 면 외곽선 자동 얇게·넓은 면 바깥 옅게 설정 추가: EditMode 13(SurfaceShape 신규), PlayMode 외줄·인간대포 2 통과. 임시 씬 렌더로 줄·판자(자르기)·넓은 발판(바깥 옅게)·바닥(원) 확인.

- 2026-11-13 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-13 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=ENV_BLOCKED(Unity 환경); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-13 PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/370 (fix/ui/landing-mark → dev)
- PR #370 제출: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/370 (fix/ui/landing-mark → dev, Squash). 사람 플레이 확인 대기.

- 2026-11-13 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-13 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-13 후속 수정(사용자 요청 4건):
  - 안전 판정: 일반 바닥 전부 안전·트램펄린 위험이던 문제 수정. 트램펄린(반동 가능) = 안전, 일반 면은 `NetworkPlayer.PredictsSafeLanding`로 실제 부상 여부 예측. `CompleteFall`의 면제 규칙을 `IsSafeLandingSpot`으로 묶어 같이 사용. `HumanCannonTrampoline.BounceReady` 공개.
  - 공간 설정에 리허설(기본 켬) 추가, 대기실과 분리. 공간 설정은 외곽선만 끄고 그림자·번짐은 항상 표시.
  - 핫픽스: `HumanCannonPoleSystem.GrabTeammate` 추가, `CommonInteractionUI`가 외줄 공중그네(CanAttachToPole)·외줄 봉 출구·인간대포 그네 사슬 잡기 때 매달린 팀원 캐릭터에 외곽선(안내 문구는 그대로).
  - 테스트: 컴파일 PASS(recompile). EditMode LandingShadowSpace 6(신규 1·변경 1), CannonLandingFeedback 4, CannonGroundMarker 4, NetworkFallSegment 19 통과. PlayMode 인간대포 GroundMarker 1, 외줄 RopeJump_ShowsCommonLandingShadow 1 통과. 그네 외곽선 자동 테스트 없음(사람 확인).
- 2026-11-13 검사: 안전성=PASS; 문서/규칙=FAIL(다른 Task-081 log.md 143줄 hard 초과, 이 작업과 무관·미수정); 컴파일=SKIPPED(선행 실패, 별도 recompile PASS); 테스트=SKIPPED(위 개별 실행으로 대신)
- 2026-11-13 PR #372 제출: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/372 (fix/ui/landing-mark → dev). 공통 설정 착지 표시 값 조정 포함. 사람 플레이 확인 대기.
