# Project Facts

이 POC의 확인 가능한 현재 상태. 상황에 맞는 파일 1개만 연다.

- [overview.md](overview.md) — 기획 개요(M2 2026-10-27)와 현재 구현 상태. 게임 규칙·UX·씬 Task를 계획할 때
- [game-flow.md](game-flow.md) — 최신 게임 흐름(M2 2026-10-27): 하루 시계·안전지대 없는 쉬는 시간·밤 정비·서비스 상시·기물 밤 구매·토큰 공동 자금·보상 확정 규칙·신체 복구 100/+25·난이도 단계 선택·탈출·세대 교체. 게임 흐름과 인간 대포 예외를 대조할 때
- [run-state.md](run-state.md) — 런 상태 범위(세션/런/플레이어, 묘기별 클리어 단계·신체 복구 가격 단계)·장소 전환 계기(대기실 통합)·전환 때 넘기는 데이터. 런 진행 관리자·장소 전환을 설계할 때
- [repository.md](repository.md) — 원격·기본 브랜치·명명 규칙. 브랜치·worktree·커밋 작업 전에
- [humanoid-validation.md](humanoid-validation.md) — Body Type 1 순차 절단·8개 미리보기·실Player 표현/슬롯 색상과 부상 전용 제약. 절단 모델 준비·외형 연결 시
- [unity-project.md](unity-project.md) — 씬·렌더·입력·코드·테스트 어셈블리 현황(2026-10-13). 에셋 경로를 찾거나 테스트를 만들 때
- [team.md](team.md) — 현재 공간 담당(공연장·탈출로 @RagSpirin, 대기실 @gyumin9048-ctrl)과 PM 계정·과거 담당 구분. 담당자 배정·권한 확인 때
- [local-environment.md](local-environment.md) — macOS 도구 경로. 스크립트·CLI가 실행되지 않을 때
- [title-logo.md](title-logo.md) — 타이틀 로고 정적2레이어·Sprite/1920기준 배치 계약. Canvas 배치·크기/그림자 조절 전에
- [results-ui.md](results-ui.md) — C12 공연 결과 표시·확인 의도·장식 동전 계약과 독립 씬. 조립/결과 어댑터 연결 전에
- [schedule-ui.md](schedule-ui.md) — W3 읽기 전용 3일 공연 일정표·독립 열 스크롤·현재/확정 표시 계약. 조립/일정 표시 어댑터 연결 전에
- [shop-ui.md](shop-ui.md) — W2 수화기 구매·판매 표시 패널·요청 토큰 계약과 독립 확인 씬. 조립/거래 어댑터 연결 전에
- [settings-ui.md](settings-ui.md) — C14 설정 창·공용 컨트롤/staged 적용 토큰 계약. 조립·실제 옵션 서비스 연결 전에
- [transition-curtain.md](transition-curtain.md) — C16 커튼 차폐/렌더 장벽·독립 프리팹·원본 보존. 전환 표시 배치·연결 전에
- [spectator.md](spectator.md) — C11 관전/사망 표시·비네트·하단 슬롯 계약·독립 씬·원본 보존. 배치/호출·검증 전에
- [worldlabels.md](worldlabels.md) — 월드 라벨 v2 소품3종·근접정보2종·명시 observer/camera·독립 씬. 정본 배치/표시·검증 전에
- [captions.md](captions.md) — C9 자막·화자·방송 정본·수용 검사/폭 API·독립 씬·원본 보존. 자막 표시/검증 전에
- [guides.md](guides.md) — C7 조작 가이드·키캡·마우스 정본·차단 API·독립 씬·원본 보존. 표시/검증 전에
- [tooltips.md](tooltips.md) — C15 근접·패널 품목 설명 정본·모드/배치 API·독립 씬·원본 보존. 툴팁 사용/검증 전에
- [interaction.md](interaction.md) — C6 안내판·내부 진행 링 정본·표시 API·독립 씬·원본 보존. 배치·호출·검증 전에
- [notifications.md](notifications.md) — C8 배너·오른쪽 토스트 정본·표시 API·독립 확인 씬·전체 원본 보존. 알림 UI 사용/검증 전에
- [backstage-hud.md](backstage-hud.md) — 백스테이지 W1/C4/C5/W6·로비 L1 정본·표시 API·전체 원본 보존. HUD 배치/게임 데이터 연결 전에
- [performance-hud.md](performance-hud.md) — 공연 HUD C2/C3/P1·표시 API·독립 확인 씬·제작 원본 보존. HUD 배치/게임 데이터 연결 전에
- [common-icons.md](common-icons.md) — 공용 아이콘25종 catalog·비교 씬·정본 참조·전체 제작 자료 보존. 아이콘 사용/재제작 전에
- [basic-panels.md](basic-panels.md) — 메뉴·나가기 확인 패널 정본·private 의존성·조립기. 패널 배치·수정 전에
- [common-buttons.md](common-buttons.md) — 공용 버튼4종·private 의존성·중복 코드 공존·조립기. 버튼 배치/키트 정리 전에
- [c1-cheer-gauge.md](c1-cheer-gauge.md) — C1 디자인 정본·필수 private 자산·재생성 한계. 프리팹 수정이나 키트 정리 전에
- [Waiting-room/](Waiting-room/index.md) — 기존 대기실 그레이박스를 복제·재배치할 때 원본 씬·분산 아트·기능 위치 확인
