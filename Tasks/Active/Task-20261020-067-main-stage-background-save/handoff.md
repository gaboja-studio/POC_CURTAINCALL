# Handoff

## 지금 상태

- 씬: Assets/_ThirdParty/curtaincall/PerformanceBackground/Scenes/performance_background.unity
- 원형 지름 1164, 천장 Y315, 하부 Y-405, 무대 Y0. 관중석 3단/24구역, 활성 렌더러 1250/조명 27개.
- 사용자 요청으로 기존 재질 219개의 로컬 수정만 원복. 원본 재질과 메타는 유지하며 공연장 참조 0개.
- 이전 비활성 시안 1025개 루트, 미사용 메시 239개/재질 12개/프리팹 4개 제거. 전용 폴더 약 1008.5→535.1MiB(473.3MiB 절감).
- 외부 에셋 13073개 기준 전용 폴더 역참조 0개 확인. 이전 시안은 Git a096ad29에서 복구 가능.
- private 정리 커밋 ef13d70a는 원격 main과 resource/third-party/performance-background에 저장 완료. 이 공개 커밋에서 gitlink와 인계 문서를 갱신한다.
- PM 승인: 현재 대화 사용자의 저장 및 불필요 에셋 제거 지시. 범위는 exception.md. PR 없음.

## Verification

- 구역 검사: PASS (10-20)
- 1 문서/규칙: PASS (10-20)
- 2 컴파일: PASS (10-20)
- 3 테스트: PASS (EditMode, 815/815, 10-20)
- 정리 전후 및 재로드 후 활성 Transform/메시/재질 참조/Renderer/Light 서명 동일. 누락 메시·재질·셰이더 0, 사용자 스크립트·카메라·콜라이더 0, 저장 상태 확인.
- 직전 검사: 컴파일 PASS, EditMode 815/815. 공연장 실제 플레이/성능/공동 실행은 미실행.
- Git 과거 이력/LFS 캐시는 유지하므로 작업 폴더 절감이 전체 clone 크기 절감을 뜻하지 않는다.

## 다음 할 일

1. 다른 PC에서 feat/main-stage-background를 pull하고 submodule/LFS를 갱신한 뒤 Unity 6000.3.25f1에서 위 씬을 연다.
2. 실제 플레이 카메라와 대상 PC 프레임 시간 검토. 현재 정점 약 525만이므로 추가 절감은 반복 결합 메시의 공용 모듈화부터 검토한다.
3. PR 제출 시 PM 승인 예외와 최신 dev에서 유입된 기존 변경을 구분해 확인한다.
