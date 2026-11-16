# Task-20261116-142

- **Title:** 라이선스 창 전환 시 남는 버튼 선택 해제
- **Type:** fix
- **Assignee:** @MoHoDu
- **Branch:** fix/main-menu-license-focus
- **Base:** dev

## Context
시작 메뉴에서 개발 및 라이선스 창을 열거나 닫은 뒤 이전 버튼이 강조된 채로 남지 않게 한다.

## 계획
1. 창 열기·닫기와 목록·전문 전환에서 나가는 메뉴의 EventSystem 선택을 해제한다.
2. 화면 전환 직후 버튼 자동 선택을 제거하고 마우스 hover·클릭을 유지한다.

## 미정·주의
PR #394가 병합된 최신 dev에서 새 작업 브랜치를 분기했다. 씬·프리팹 수정 없이 기존 StartMenuCreditsPanel에서 처리한다.
