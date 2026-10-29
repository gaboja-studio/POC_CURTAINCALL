# Log

- 2026-10-29 사용자 요청: 모든 디자인 패키지를 역할별 통합, 필요한 파일만 보존 후 가벼운 플레이 확인과 PR 반영. 사용자 답변으로 전체 범위 확정. 기존 보존 문서의 미정리 방침은 이번 사람 지시로 대체한다.
- Task-079의 공개 eaf59269/private80ff0ad7에서 refactor/ui-design-assets 분기. doctor: 기존 Task-077 중복 외 작업 환경 정상. 기존 공유 폰트 2개 dirty 보존.
- 공개 열린 PR 없음, private에는 무관한 에셋 PR #1만 있음. 새 작업 PR을 준비한다. private/main 미반영 gitlink에 대한 공개 push 훅 제약은 유지.
- 조사: UI 25패키지에 복제된 공용 PNG·원본 TTF·조립 JSON/CSV가 있다. 단순 GUID 참조뿐 아니라 공개 wrapper와 private Editor 조립기의 경로 의존성까지 보존해야 한다.
- 사용자 순서 변경: 기존 기능·디자인 에셋의 PR 반영을 먼저 진행하고 정리는 그 후 재개한다. 실제 에셋 이동/삭제 및 경로 수정은 아직 실행하지 않았다. 조사 계획은 .harness/local에만 있으며 정리 Task는 보류한다.
