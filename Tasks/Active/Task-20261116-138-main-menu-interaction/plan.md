# Task-20261116-138

- **Title:** 메인 화면 배경 움직임과 클릭 상호작용
- **Type:** fix
- **Assignee:** @MoHoDu
- **Branch:** fix/main-menu/interaction-effect
- **Base:** dev

## Context
시작 화면의 커튼·조명·의자 상호작용에 이어, 커튼 영역에서 의자 보호, 줄에 매단 타이틀의 반복 클릭 후 한 번만 내려앉는 동작, 편집 가능한 개발자·라이선스 창을 구현한다. 담당 @MoHoDu.
후속 요청: 게임 버전을 0.8.1로 올리고 모든 씬에 버전/배포 환경 HUD를 표시한다.

## 계획
1. StartMenu에서 같은 씬의 Start Stage를 연결한다. 현재 씬과 빌더가 만든 씬 모두 실행 시 적용한다.
2. 커튼 주름을 런타임 세분화 메시로 바꾸고 상단 고정·국소 스프링 반응을 구현한다.
3. 의자 클릭 지점에 물리 힘을 가한다. 연속 클릭/길게 누르기는 강도를 높인다. 무대 밖 이탈을 막는다.
4. 핀 조명에 25~55초 간격의 짧은 두 번 감광을 추가한다. 메뉴/설정/전환 중 배경 입력을 차단한다.
5. 묶음 완료 시 check-work와 가능한 컴파일·동작 검사를 진행하고 환경 제약을 기록한다.
6. 타이틀 가독성 개선, 꼬인 로프/금속 고리 프리팹, 좌우 여섯 번 입력 후 해당 줄이 한 번만 늘어남. 메뉴 실제 위치에서 낙하 높이 제한.
7. Project Settings와 ScriptableObject로 개발자/라이선스 CRUD를 제공한다. 4명 GitHub 닉네임과 근거가 있는 초기 라이선스 항목을 채운다.
8. PlayerSettings.bundleVersion을 0.8.1로 변경한다. 씬 독립 공통 HUD를 마이크 아래 (24,18)에 배치하고 기존 로그/설문과 같은 기준으로 Editor/Development Build는 TEST, 일반 빌드는 LIVE로 표시한다.

## 미정·주의
- StartMenu.cs 연결 한 줄 및 신규 StartMenuBackdrop.cs. 로비, 공통 게임 규칙, 외부 원본 에셋은 수정하지 않는다.
- 초기 미저장 변경 없음. origin/dev fetch 후 HEAD와 차이 0/0 확인.
- doctor: third-party 미초기화 판정, 기존 Task ID 중복, Unity 설치 감지 실패. 직접 확인한 Windows 설치 경로에는 실행 파일/참조 DLL이 있다.
- start-work.ps1은 현재 브랜치의 중첩 경로를 거부한다. new-task -UseCurrentBranch는 허용하므로 현재 브랜치를 유지하고 수동으로 이어간다.
- 후속 작업 시작 시 Windows Unity Pipeline 연결 성공. dev 11개 커밋 fast-forward 후 번호 134 충돌이 확인되어 이 미커밋 Task를 137로 재번호화했다.
