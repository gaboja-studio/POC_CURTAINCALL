# Task-20261029-080

- **Title:** 디자인 UI 패키지 역할별 통합 및 미사용 자료 정리
- **Type:** refactor
- **Assignee:** @MoHoDu
- **Branch:** refactor/ui-design-assets
- **Base:** fix/waiting-room-resttimer

## Context
사용자가 디자인 패키지 전체를 역할별 하나의 폴더로 통합하고 필요한 것만 보존하도록 요청했다. 실제 사용 코드·그림·폰트·라이선스와 재생성 의존성을 보존한다. 이후 가벼운 플레이 검사와 저장/push·PR 반영까지 진행한다. 대기실 타이머는 기존 좌상단 위치를 유지한다. Unity는 CLI만 사용한다.

## 계획
1. curtaincall_* UI 패키지 25개의 직렬화 GUID·경로 로딩·프리팹 조립/내보내기 의존성을 조사한다. PerformanceBackground와 Asset Store 패키지는 범위 밖이다.
2. private CurtainCallUI 아래 버튼·설정·타이머 등 역할별 폴더로 이동한다. GUID/.meta는 Unity AssetDatabase로 보존하며 경로 코드·조립 레시피를 갱신한다.
3. 참조 없는 검토 목업/시트·과거 생성 보고서·중복 제공 코드/사본을 정리하고 이동/삭제 목록과 유지보수 계약을 남긴다. 불명확한 의존성은 보존한다.
4. 컴파일·누락 참조 감사·시작/대기실/설정/공연 UI의 가벼운 회귀 후 check-work를 수행한다.
5. 관련 변경만 저장/push하고 private/main 및 공개/dev 대상 PR을 준비한다. PR 병합은 사용자 명시 지시 전에는 하지 않는다.

## 주의
- 기존 폰트 2개(CheerGauge Bold/TMP fallback)의 별도 변경은 보존하고 커밋에서 제외한다.
- doctor는 기존 Task-077 중복으로 실패, CLI 버전/skill-link 경고. private/LFS와 대상 에디터 연결은 정상이다.
- 공개 push 훅은 gitlink가 private main에 있어야 한다. private 작업 PR을 먼저 준비하며 훅을 우회하지 않는다.
- Task-077 디자인 UI 및 Task-079 타이머와 경로가 겹치므로 기존 생성 결과물은 재생성하지 않고 GUID 이동·경로 연결만 수행한다.
