# Handoff

## 지금 상태

- 공용 배경 씬: Assets/_ThirdParty/curtaincall/PerformanceBackground/Scenes/performance_background.unity
- 원형 지름 1164, 천장 Y315, 하부 Y-405, 무대 기준 Y0. 활성 루트 CircularVenue.
- 관중석 3단/24구역, 하부 정비 구역 60개 및 기존 21개 지점 연결. 외곽 탑 3개만 무대 위로 올림.
- 사용자 게임 스크립트·카메라·콜라이더 없음. 조명 27개. 상세 이력은 private Documentation/handoff.txt.
- PM 승인 예외: 현재 대화 사용자의 직접 지시로 Task 소급 등록과 private 저장/공개 gitlink 갱신. 승인 계정과 근거는 exception.md.
- PR: 없음. 저장만 요청됨.

## 다음 할 일

1. 다른 PC에서 feat/main-stage-background를 받고 submodule/LFS를 갱신한다.
2. Unity 6000.3.25f1에서 위 씬을 연다.
3. 실제 플레이 시각 검토와 성능 측정을 진행한다.

## Verification

- 제작 직후 컴파일: PASS(up_to_date), Tests: SKIPPED.
- 구역 검사: PASS (10-20)
- 1 문서/규칙: PASS (10-20)
- 2 컴파일: PASS (10-20)
- 3 테스트: PASS (EditMode, 815/815, 10-20)
- .meta 누락 0, 메시·재질 누락 0, 기존 변경 머티리얼 참조 0.
- 두 묘기와 Editor Additive 로드 확인. Play Mode/성능/공동 실행 미실행.
- 기존 원본 머티리얼 219개는 별도 백업 및 stash로 보존하고 이번 공연장 커밋에서 제외.

- private 에셋 커밋 a096ad29 및 통합 커밋 8414a73e: 원격 main과 resource/third-party/performance-background에 push 완료.
- 공개 작업 브랜치 저장은 save-work 검사 후 진행. 다른 PC에서는 이 Task의 gitlink가 가리키는 private 커밋을 받는다.
