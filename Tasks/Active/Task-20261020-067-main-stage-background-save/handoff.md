# Handoff

## 현재 상태
- Task-067 / 제출 feat/main-stage-background-submit, 제작 feat/main-stage-background. [이전 제작 기록](History/001-background-art-before-trick-integration.md), [묘기 배경 제거 전](History/003-before-master-only.md).
- 유지하는 원본: Assets/_ThirdParty/curtaincall/PerformanceBackground/Scenes/performance_background.unity. 지름 1164, 바닥 Y-1080, 무대 Y0, 천장 Y315.
- 10-21 사용자 지시: 관객 모델을 제거하고 인간 대포·외줄 씬에서 공연장 배경을 제거. 작업한 원본 공연장만 유지.
- 관중석 4800석·계단·난간·3층 연속 원형 배치와 하부 관람대 9곳은 유지. 앉은 관객 4431명과 도시 내 서 있는 실루엣 232개 제거. 관객 전용 빈 메시/오브젝트 141개 정리.
- 주황빛·스모그·철골 지하 도시·배경 톱날 15개·외벽 연결 배치·심부 깊이는 유지. 기존 도시 69개 메시의 높이 상승은 취소된 상태.
- 현재 Renderer 1221/정점 7,445,292/삼각형 4,174,104/활성 Light 44. 원본 배경 Collider·Camera·런타임 스크립트 없음.
- 인간 대포·외줄은 배경 인스턴스 및 적용했던 환경/카메라 배경/광원 마스크 제거·복구. 두 씬 모두 HEAD 대비 git diff 0.
- 적용용 Prefabs/performance_background.prefab은 참조가 남지 않은 것을 확인한 뒤 삭제. 원본 씬과 종속 메시·재질은 유지.
- 현재 Unity: 원본 performance_background.unity, 편집 모드, 관중석과 외벽 도시를 함께 보는 시점. 디스크 저장. private 에셋 b6cc29c7은 origin/main에 반영 완료.
- 기존 private stash 3개, TightropeCourse.prefab의 기존 4줄 변경, Packages/ProjectSettings/외부 UI meta 변경 보존. 임의 혼합/삭제 금지.

## Verification
- 10-21 구역 PASS(기존 예외)/문서 PASS/컴파일 PASS(up_to_date). 최초 컴파일 검사는 에셋 정리 후 씬 dirty로 차단되어, 원본 재저장 후 재검사 통과.
- 원본/두 묘기 저장본 재조회: 두 묘기 배경 0개, 원본 누락 참조 0, 관객 전용 활성 배치 0, dirty=false.
- 묘기 제거 작업 전후 기존 구성 요소 비교: 인간 대포 5629개/외줄 79개, 의도한 카메라·광원 설정 외 변경 0. 기존 씬 파일과도 git diff 0.
- 생성기에서 사람 생성 부분을 제외해 좌석과 공유 재질 메시를 재작성. Logs/master-no-crowd.png 렌더 확인. 관객 제거 전 백업은 Logs/before-audience-removal/.
- 10-21 제출 전 제작 프로젝트 EditMode 815/815 PASS. 최신 원본은 렌더·참조 확인, 실제 Play/다인 완주·프레임 시간 미측정.
- 기존 Vivox 자격 조회 HTTP 403은 이번 작업과 무관하며 음성 서비스 정상으로 보고하지 않음.

- 구역 검사: PASS (제출 구역 별도 검사)
- 1 문서/규칙: PASS
- 2 컴파일: PASS (제작 프로젝트, up_to_date)
- 3 테스트: PASS (제작 프로젝트 EditMode 815/815, 2026-10-21)
- 4 직접 플레이: SKIPPED (최신 원본은 Scene 뷰/렌더 확인; 자동 묘기 로드 없음)

## 다음 할 일
1. 사용자가 관객 없는 원본 공연장의 배치·떠 보이는 지지 구조를 검토. 주기둥/브래킷/사슬 지지 보강은 아직 제안만 했으며 미적용.
2. 실제 플레이 적용은 사용자 재지시 후 진행. 원본의 전체 치수와 묘기용 1/3 치수 차이를 주의.
3. 저장 요청 시 private 에셋 먼저 저장 후 공개 문서/gitlink 저장. PR은 별도 요청 때.
