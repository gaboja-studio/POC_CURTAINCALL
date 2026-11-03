# Task-20261104-100

- **Title:** 리허설·추격 시스템 통합 대기실
- **Type:** feat
- **Assignee:** @MoHoDu
- **Branch:** feat/waiting-room/unified-scene
- **Base:** dev

## Context
Base의 공연 리허설과 Pursuit의 추격 시스템을 합친 `Assets/Scenes/InGame/WaitingRoom/WaitingRoom.unity`를 만들고 기본 대기실로 연결한다. Base/Pursuit/Integrated 선택, 카메라·공통 UI·치트와 추격 배치 편집을 유지한다.

## 계획
- 원본 두 씬의 공통 오브젝트·컴포넌트와 차이를 비교하고 Unity 에디터로 새 씬을 생성한다.
- 추격 씬의 배치·순찰·문 장애물·내비게이션 참조를 유지하고 Base의 최신 연습실 모니터 배치를 가져온다. 리허설은 공통 런타임 생성 경로를 쓴다.
- 씬 선택/라우팅·빌드 등록·추격 사용 여부·배치 도구의 현재 씬 대상을 확장한다.
- EditMode 참조/배치 보존과 PlayMode 리허설·카메라·UI·치트를 검증한다.

## 미정·주의
기존 Task-099의 지연 환경/4인 성능 비교는 별도 미완료 작업으로 보존한다. 기존 GripYellow.mat, play-land.png 변경은 이 Task 범위에 포함하지 않는다. 원본 Base/Pursuit 씬은 수정하지 않는다.
