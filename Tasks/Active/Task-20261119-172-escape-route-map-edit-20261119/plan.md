# Task-20261119-172

- **Title:** 탈출로 맵 수동 편집
- **Type:** feat
- **Assignee:** @RagSpirin
- **Branch:** feat/escape-route-map-edit-20261119
- **Base:** dev

## Context
사용자가 Unity에서 탈출로 맵을 직접 편집한다. AI는 전용 브랜치와 편집 씬을 준비한다.

## 계획
1. 최신 origin/dev에서 전용 브랜치를 만들고 기존 로컬 변경을 보존한다.
2. Assets/Scenes/Levels/escape_route.unity를 열어 수동 편집을 준비한다.
3. 사용자가 맵을 편집한 뒤 요청 시 변경 검사·기록을 진행한다.

## 미정·주의
- 편집 범위는 탈출로 맵이다. 이번 준비 과정에서 게임 코드·맵 내용은 수정하지 않는다.
- 시작 기준: 95070a3d. 기존 _Recovery 파일과 Unity가 만든 외부 에셋 로컬 변경은 보존한다.
- 탈출로 씬은 기존 탈출로 작업들과 겹치는 파일이므로 후속 병합 시 씬 변경을 확인한다.
- commit/push는 별도 요청 때만 한다.

## 편집 지원 추가 요청
- 추격자 경로가 실제로 사용하는 지지 발판과 파괴 패널만 원색 핑크 재질로 임시 표시한다. 주변 충돌 후보 전체, 선·오버레이 표시는 사용자 요청으로 폐기했다.
- Tools/CurtainCall/탈출로/추격자 발판·파괴벽 핑크색 메뉴로 색상만 켜고 끈다. 원래 재질 매핑은 EscapeFactoryArt/pursuer-edit-colors.json에 보관한다. 오브젝트 이동·삭제는 복구하지 않는다.
- SceneView 기본 카메라 설정을 복원하고 dynamicClip=false, nearClip=0.03, farClip=10000으로 편집 시야 잘림을 해소한다. 게임 카메라는 변경하지 않는다.

- 후속 요청: InspectionRelay 대포에서 공중 그네를 잡아 컨베이어 착지대로 넘어가게 재설정한다. 지정 그네를 잡기 전 착지나 다른 기물 이용으로 우회하지 못하게 한다. 사용자가 삭제한 구조물은 다시 만들지 않는다.
