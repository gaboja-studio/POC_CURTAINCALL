# 외줄 에셋 작업 인계 · 2026-10-20

## 지금 상태

- 사용자가 외형 문제 조사를 멈추고 다른 컴퓨터에서 이어갈 수 있도록 저장을 요청했다. 외형 수정은 시작하지 않았다.
- 브랜치: `feat/tightrope-enhancement`. 대상 씬: `Assets/Scenes/Tests/MainStage/Tightrope/Tightrope.unity`.
- 씬 적용 완료: `TightropeArtProfile` 및 가로 톱·정면 톱·일반 발판·협동 톱·협동 발판 참조가 씬 오버라이드로 저장돼 있다.
- 적용된 외형에 사용자 불만이 있다. **외형 완성으로 취급하지 않는다.** 하단의 거대한 하얀 구조물, 이상한 색 덧칠, 줄 외형이 문제다.
- 기능 준비·메시 참조 검사는 통과했지만 최종 미술 적합성을 보증한 결과는 아니다.
- PR: 없음. 통합·병합은 요청받지 않았다.

## 다음 할 일

1. 다른 컴퓨터에서 이 브랜치를 받아 private submodule도 맞춘 뒤 Unity 6000.3.25f1로 Tightrope 씬을 연다. private 기준 SHA: `b92b37a4db5ff840a82d26bf323c3b2831b2698a`.
2. 실제 플레이 화면을 재현해 아래 원인과 원본 모델의 모양·재질을 확인한다. 사용자와 미술 방향을 맞추고 최소 수정한다.
3. 시각 수정 후 줄 소실/복구·봉 움직임·톱·발판 판정 및 실제 화면을 함께 검증한다.

## 외형 문제: 확인한 사실과 남은 확인

- 하얀 구조물은 `SafetyNetModule.prefab`을 코스 하부 `SafetyNet`에 연결한 결과다.
- `TightropeCourse.cs`는 코스 전체 길이·폭의 SafetyNet 상자를 하나 만든다. `TightropeCourse.Art.cs`는 여기에 `TightropeArtFit.Box`로 안전망 **한 개를 전체 크기로 늘린다**.
- 제작 가이드의 안전망은 6×2m 반복 모듈이다. 실제 반복 배치가 구현돼 있지 않아 형태가 과장된다. 어떤 표현으로 고칠지는 아직 미결정이다.
- 색 덧칠: `TightropeArtFit.Tint`가 모든 하위 Renderer의 `_BaseColor`·`_Color`를 덮는다. 코스 구간색·봉 상태색 등이 원본 재질에 곱해진다.
- 줄 에셋은 존재한다: `PerformanceArtKit/RopeSegment.prefab`, 원본은 `Assets/_ThirdParty/Synty/PolygonGeneric/Models/SM_Gen_Prop_Rope_01.fbx`.
- `TightropeArtProfile.rope`도 연결돼 있다. 다만 3m 모듈을 각 구간 전체 길이로 늘리는 방식이라 원본 형태·반복·재질이 외줄에 적합한지는 실제 화면에서 다시 확인해야 한다.
- 원본 아트·게임 수치·색상 정책을 이 인계 단계에서 바꾸지 않았다.

## 변경과 검증

- 공용/외줄 조합본의 메시·재질 참조 감사에서 식별자 변경으로 끊긴 외부 모델 9개의 GUID만 원래 값으로 복구했다. 그 밖의 기존 사용자 임포트 설정 차이는 보존했다.
- 준비본 참조 검사를 공용 PerformanceArtKit까지 확장했다. 반동 중 봉 외형과 물리 손잡이 위치·추가 Collider 부재를 검사한다.
- 옆줄 점프 검사는 같은 프레임에 Update를 반복하던 방식에서 실제 프레임을 진행하도록 보정했다.
- 컴파일: PASS. EditMode: 221/221 PASS.
- PlayMode 전체: 72개 중 69 PASS·2 FAIL·1 SKIPPED. 검사 준비/프레임 진행을 보정한 뒤 실패했던 2개를 각각 재실행해 1/1 PASS. 전체 72개를 다시 실행한 결과는 아니다.
- 씬 적용 후 `PreparedArtFollowsSupportRemovalPoleMotionAndSawRebuild`: 1/1 PASS.
- 실제 4인 공동 플레이: 미실행. 사운드·화염 VFX의 최종 적용은 이 작업 범위가 아니다.
- 상세: `tightrope-art-preparation-20261020.md`. 로컬 결과 원본과 캡처는 `.harness/local/`에 있어 Git으로 전달되지 않는다.

## 저장 경계와 PM 확인

- 현재 브랜치에 대응하는 활성 Task가 없다. 사용자 직접 메시지로 PM 예외 허락을 확인했고 작업했다. PM 승인 시각은 제공되지 않았다.
- `save-work.ps1 -DryRun`은 Task 부재로 구역 검사 실패. 정식 구역 검사 PASS로 보고하지 않는다. 저장 시에도 기존 예외와 사용자 저장 요청을 기록하고 이번 변경만 명시적으로 선택한다.
- PM이 Task 배정 및 예외 변경 범위를 사후 확인해야 한다. 하네스·검사 스크립트·Git hook은 변경하거나 우회하지 않는다.
- private 저장소에는 작업 전부터 이어진 변경이 879개 있다. 이번 공개 작업 커밋에 포함하지 않는다. 복구한 9개 원래 GUID는 private 기준 커밋에도 이미 존재하므로 공개 포인터를 바꿀 필요는 없다.
- private 로컬 변경 전체를 다른 컴퓨터로 옮기는 별도 저장은 수행하지 않았다. 이 로컬 폴더를 지우거나 정리하지 않는다.

## 교훈 제안

- 크기·참조·판정 자동 검사만으로 미술 적용 완료를 판단하지 않는다. 반복 모듈의 전역 확대와 원본 재질의 일괄 덧칠은 실제 화면으로 검토해야 한다.
