---
name: integrate-editor
description: 요청 목적의 씬·프리팹·에셋을 올바른 Unity 에디터에서 수정한다.
---

# integrate-editor

1. workspace-isolation·asset-ownership·editor-targeting을 확인한다. worktree는 동시 작업 때만 선택한다.
2. plan의 목적과 기존 미저장 상태를 확인한다. Exclusive Assets·setup·PM 구역 배정은 요구하지 않는다.
3. 명시적인 프로젝트 경로로 에디터 연결과 가능한 명령을 조회한다. 명령 이름을 추측하지 않는다.
4. 변경 전후 하이어라키·컴포넌트·참조를 확인하고 필요한 에셋만 저장한다.
5. 큰 에셋 수정·삭제는 PM·에셋 관리자가 관리한다. 겹치는 수정은 담당자와 조율한다.
6. git 상태로 실제 저장을 확인한다. 개발·수정 묶음 완료 시 필요한 컴파일만 한 번 한다.
7. 결과·주의점·미검증 내용은 log에 남긴다. 재미·최종 플레이 경험은 사람이 확인한다.
