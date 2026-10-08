# Plan

## Context

2026-10-06 승인: 테스트 IMGUI 대신 방 생성부터 결과까지 데모형 UI로 제공한다. 015 확정 결과와 023 수집 계약을 읽고 게임 판정·데이터 저장을 다시 구현하지 않는다.

## Approach

1. uGUI/TMP·Pretendard·설치된 DOTween을 재사용해 남색·아이보리·골드 패널·큰 버튼·짧은 전환으로 만든다. 외부 에셋/패키지를 추가하지 않는다.
2. 공개 세션 API로 Services 방 생성/참가와 보조 LAN·접속 중·오류·방 코드 복사·인원·나가기를 연결한다. 초기 상태를 읽고 비동기 반환과 실제 접속을 구별하며 중복 제출을 막는다. 호스트 CanStartGame 조건 유지.
3. 시간·거리·신체 상태·화재 예고·조작 안내와 기존 BalanceGauge를 사용한다. 결과는 015 불변 스냅샷의 고정 시간·직접/동반/사망/이탈·네 사지 정상/손실을 표시한다.
4. 성공은 호스트 재도전/클라이언트 대기, 실패는 3초 자동 재시도 안내다. 새 시도에 팝업을 닫고 지난 결과 열람은 보존한다. 재도전 실행부와 디버그 gate는 022 연결 계약을 사용한다.
5. ScreenSpaceOverlay·1920×1080 CanvasScaler·GraphicRaycaster·EventSystem·InputSystemUIInputModule을 연결하고 모달/입력창 focus 요청 공개 API를 제공한다. 전체 게임 입력 잠금은 022가 연결한다. 네트워크/타이머를 멈추지 않는다.
6. 전용 테스트 씬·데모 프리팹을 에디터로 저장한다. 최종 씬에서는 IMGUI 방 UI를 제외하되 개발 도구 자체는 삭제하지 않는다.

## Files

- setup.md의 UI 코드·전용 씬·Tightrope UI 프리팹.
- 읽기 전용: NetworkSessionManager, Run/Results/Fire/Telemetry API, 기존 BalanceGauge·폰트.

## Risks

- TMP 동적 폰트 변경은 포함하지 않는다. UI focus와 게임 입력 차단은 서로 다른 책임이다.
- 실패 자동 재시도 뒤 늦은 결과·호스트 변경·Leave 시 오래된 UI와 중복 버튼 동작 방어.

## Auto Verification

- check-work.ps1, 등록 테스트 부재는 NO_PROJECT_TESTS.
- 에디터에서 Canvas/EventSystem/참조·중복·해상도·초기/접속 중/오류/결과 상태 확인.
- 사람 4인 일치/사용성 검증은 별도, AI 검사로 PASS 처리하지 않는다.

## 공동 테스트 항목

1. 네 명이 데모 UI로 방 생성·참가·호스트 시작을 하고 입력창에서 게임 입력이 발생하지 않는다(022 연결 후).
2. 시간·거리·신체 상태·화재 예고가 실제 진행과 맞고 플레이 시야를 과도하게 가리지 않는다.
3. 네 화면에 같은 고정 결과·사지 상태가 보이고 호스트만 재도전하며 실패 자동 재시도·지난 결과 열람이 동작한다.
