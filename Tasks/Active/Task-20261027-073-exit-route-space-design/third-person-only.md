# 탈출로 3인칭 전용 (2026-11-02)

- A EscapePerspectiveToggle/EscapeFirstPersonArms 컴포넌트2개와 스크립트·meta 삭제. 4키 전환/자동1인칭 복귀/달리기 손 Overlay 제거.
- HUD 원본의 AimDot 제거,전용 FirstPersonArms31층 이름 해제. 기존 프리팹 GUID 유지. HumanBodyDiagramGraphic/상시 절단 UI·추가 자식 EscapeCountdownDisplay 유지.
- EscapeBodyStatusHud는 EscapeCampaignContent의 현재 씬/활성 상태·스폰된 로컬 참가자에 연결. 카메라/시점 전환 객체 없이 동작한다.
- 미사용 EscapeEntryFirstPerson 프리셋과 구형 연결 builder의 생성·저장 코드를 삭제. 계단 끝 구역의 기존 이름/직렬화 참조는 도착3인칭 해제 트리거라 보존한다.
- 일반 이동·외줄·그네·대포의 기존3인칭 카메라 유지. 소지1/2·개발자 검토 비행3,8분 타이머/성공 집계·직전 그네/전기톱 변경 유지.
- 대기실 씬/공용 카메라 프레임워크·다른 콘텐츠 원본은 수정하지 않는다.
- 증거: .harness/local/third-person-only-20261102/ (19파일 해시 백업·편집 검사·선별 테스트).
- EditMode HUD11/11·타이머12/12 통과. A 누락 스크립트0·1인칭 컴포넌트/직접 프리셋/조준점0. 기본 FreeThirdPerson·대포3인칭,그네15/초록6/전기톱2 보존,타이머480초/표시 참조 보존. A 저장/Play stopped.
- 통합 check-work 결과는 log에 기록. 실제 Play/MPPM 미실행,commit/push 미요청.
