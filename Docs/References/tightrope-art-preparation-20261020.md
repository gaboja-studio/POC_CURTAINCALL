# 외줄·공용 에셋 적용 준비 · 2026-10-20

## 요청과 예외

- 준비 단계 사용자 지시: 씬에 적용하지 않고 외줄타기 및 공용 오브젝트의 적용 준비와 반동·판정 정합성만 확인한다. 이후 "적용해줘 내가 플레이 해보게"로 씬 적용을 요청했다. 현재 상태는 아래 후속 적용 기록이 기준이다.
- 앞선 씬 적용 지시로 저장했던 아트 연결 6개는 에디터에서 해당 오버라이드만 해제했다. 씬은 Git 기준 차이 0이다.
- 사용자 직접 확인: "예외 허락 받았어 진행해". 기존 Task 없는 `feat/tightrope-enhancement`에서 이번 요청 범위의 작업을 승인했다.
- PM 승인 사실은 사용자가 전달했다. PM 승인 시각은 제공되지 않았다.
- 활성 Task가 없어 정식 `Tasks/Active/<Task>/exception.md`와 handoff를 만들 수 없으므로 이 문서에 범위와 검증을 남긴다. Task를 임의 발급하거나 검사 규칙을 변경하지 않는다.
- 급한 예외에 따른 변경은 PM의 사후 확인이 필요하다. 커밋·푸시·PR은 요청받지 않았다.

## 범위

- 준비 단계 종료 당시 씬: `Assets/Scenes/Tests/MainStage/Tightrope/Tightrope.unity` 미적용. 후속 적용은 아래에 기록한다.
- 자동 검사: `Assets/Scripts/MainStage/Tightrope/Rope/Editor/Tests/TightropePreparedAssetsTests.cs`, `Assets/Scripts/MainStage/Tightrope/Tests/PlayMode/`의 `TightropeSwingPolePlayTests.cs`·`TightropePracticePointerPlayTests.cs`.
- 준비본을 테스트 실행 중의 인스턴스에만 연결하고, 실제 물리·시각 동작을 검사한다.
- 공용 원본 코스 프리팹에는 빈 `artProfile` 필드만 직렬화됐다. 원래 스폰 좌표와 씬 외형은 유지한다.
- 외부 메시 9개에서 변경된 GUID만 저장소 원래 값으로 복구했다. 기존 사용자 임포트 설정 차이는 보존했다. 복구 전 메타 사본은 `.harness/local/`에 있다.
- 반동·중력·이동·피해 수치는 변경하지 않았다.

## 확인 결과

- 적용 전 에디터: Unity 6000.3.25f1, 대상 프로젝트 일치, stopped/ready, Tightrope 씬 13개 루트·dirty false.
- 적용 전 코스 아트 프로필과 톱·발판 참조 5개는 모두 비어 있었다.
- 환경 점검: 외부 저장소에 기존 변경 있음, 현재 브랜치의 활성 Task 없음.
- Unity CLI beta.8의 `status`는 인스턴스 0개를 보고하지만 `command editor_status`는 정확한 프로젝트의 ready 에디터를 확인한다. 명령 연결을 직접 확인해 작업한다.
- 준비 단계 종료 당시 씬 적용: 미적용, 아트 프로필·톱·발판 외형 참조 6개 비어 있음.
- 최초 EditMode 검사: 221개 중 3개 실패. 발판 모델 GUID 변경으로 메시 참조 및 크기 검사 실패.
- 발판 GUID 복구 후: 220/221. 추가 안전망 메시 누락 확인.
- 공용 15종과 두 Showcase까지 확장 감사: 9개 모델의 GUID가 원래 값과 달랐다. 발판·안전망·트램펄린 2개·대포 2개·에어쿠션·십자날·가시추.
- GUID 복구 후 두 묶음 전체 참조 감사: 누락 메시·재질·미해결 GUID 0.
- 컴파일: PASS (completed, failed false, errors 0).
- 최종 EditMode 검사: **221/221 PASS**, 실패·생략 0. 공용 PerformanceArtKit 전체의 참조 검사도 포함했다.
- PlayMode 전체 실행: 72개 중 69 PASS·2 FAIL·1 SKIPPED. 준비본 외형·줄 제거/복구·톱 생성·협동 관문·재시작 등은 통과했다.
- 실패 1: 실행 중 아트 프로필을 임시 연결한 뒤 기존 봉 외형을 다시 만들지 않은 검사 준비 문제. 봉 외형을 재생성하도록 고쳤고 `SwingPoleUsesCannonMomentumAndAirControlThenTeammateCatchesTheLanding` 재검사 **1/1 PASS**.
- 위 검사는 W 반동부터 놓기·비행·동료 받기·제어 복구까지 실행하며, 반동 중 봉 외형과 물리 손잡이의 위치 오차 1mm 미만 및 추가 Collider 부재도 확인했다.
- 실패 2: 옆줄 점프 검사가 실제 프레임을 진행하지 않고 같은 프레임에 `Update`만 반복 호출했다. 고정 시간 간격을 먼저 적용하고 실제 프레임으로 진행하도록 보정했다. `LaneJumpIgnoresCannonAirInputAndAutomaticallyLandsOnChosenRope` 재검사 **1/1 PASS**.
- 최종 결과는 전체 실행과 실패 2건의 개별 재검사를 합친 것으로, 72개 전체를 다시 실행한 결과로 표기하지 않는다. 로컬 별도 봇 프로세스 검사 1개는 요청 조건이 없어 SKIPPED.
- 새 반동·착지 캡처 2장을 확인했다: `.harness/local/tightrope-swing-held.png`, `tightrope-swing-catch.png` (2026-10-20 촬영).
- 결과 원본: `.harness/local/tightrope-art-editmode.json`, `tightrope-art-playmode.json`, `tightrope-art-swing-playmode.json`, `tightrope-art-lane-playmode.json`.
- 문서/규칙: `verify-fast.ps1` PASS. 기존 문서 길이 경고 7개와 private 저장소 미저장 변경 경고는 유지한다.
- 정식 `check-work.ps1 -Mode PlayMode`: 활성 Task 부재로 실행 실패. 구역 검사를 통과했다고 보고하지 않는다. 사용자 예외 지시에 따라 Unity 명령으로 기능 검사를 별도 실행한다.
- 실제 별도 클라이언트와 4인 공동 플레이: 미실행.

## PM이 확인할 것

- 교훈 제안(함정): 외부 모델의 메타 GUID가 재생성되면 조합 프리팹의 메시가 사라진다. 경로와 fileID가 같아도 참조는 깨진다. 원래 GUID만 복구하고 기존 사용자 임포트 설정은 보존해야 한다.
- 교훈 제안(함정): `OnValidate`의 지연 스폰 배치가 원본 코스 프리팹의 스폰 좌표도 바꾸어 저장할 수 있다. 준비본 검사 후 공용 원본과 씬의 변경 범위를 다시 확인해야 한다.

## 후속 씬 적용 · 2026-10-20

- 사용자 직접 요청: "적용해줘 내가 플레이 해보게".
- 대상: `Assets/Scenes/Tests/MainStage/Tightrope/Tightrope.unity`의 기존 코스 및 톱 인스턴스.
- 저장: `TightropeArtProfile`과 가로 톱·정면 톱·일반 발판·협동 톱·협동 발판 참조 5개를 씬 오버라이드로 연결했다. 공용 바닥·줄·안전망·봉·그네는 프로필로 연결된다.
- 적용 직후 재조회: 루트 13개 유지, 6개 참조 정상, dirty false. 씬 파일에 참조 오버라이드 24줄 추가.
- 컴파일: PASS (`up_to_date`). 반동·이동·피해 수치 변경 없음.
- 적용 후 PlayMode 확인: `PreparedArtFollowsSupportRemovalPoleMotionAndSawRebuild` **1/1 PASS** (1.27초), 실패·생략 0. 결과: `.harness/local/tightrope-art-applied-playmode.json`.
- 완료 후 Unity는 Tightrope 씬 ready/stopped 상태다. 사용자가 직접 Play하고 방 생성·게임 시작을 누르면 된다.
