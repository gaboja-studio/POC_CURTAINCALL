# Plan

## Context

2026-10-06 승인: 기획 검증용 개인 행동 JSON을 콘텐츠별로 보관하고 향후 Apps Script·Drive·Sheets 업로드에 대비한다. 이번에는 외부 전송·인증·시트 생성이 없다. 015 결과 병합 후 시작하고 최종 수집 hook 연결은 022에 맡긴다.

## Approach

1. 설치 내 영속 익명 localPlayerId를 만든다. 저장 실패면 실행 한정 ID와 identityPersistence=false를 쓴다. 이름·이메일·IP·JoinKey·토큰은 수집하지 않는다.
2. room/session/attempt 호스트 GUID 계약과 참가자 매핑을 제공한다. 재시도는 같은 session·새 attempt이며 자기 신고 ID는 인증이 아니다. 중복/누락 ID·slot 재사용은 연결별 키로 분리한다. 네트워크 연결부는 022 소유다.
3. 개인 × 콘텐츠 × 시도 JSON DTO를 제공한다. schemaVersion/appVersion·시각·호스트 경과/동기화·시작 설정/hash·명부·확정 결과·captureStatus·events/samples를 담는다. UI/저장소가 승패를 재계산하지 않는다.
4. 이동/균형/점프/착지/줄 이동/F 요청·해제/손잡기/목마/발판/톱날·손상/Fire/추락·도착/시도 전이/참가·이탈/개발 명령/설정 변경 카탈로그를 만든다. localIntent/observed/hostConfirmed를 구분한다. 미지원·unknown 원인·정밀 전역 순서의 한계를 표시한다.
5. 지속 값 기본 10Hz·입력 변화 이벤트·의미 이벤트 우선 bounded buffer를 사용한다. sequence/eventId·gap/drop을 기록한다. checkpoint 기본 5초와 중요 전이에 저장하고 매 프레임 파일을 쓰지 않는다.
6. persistentDataPath/Telemetry/localPlayerId/contentId/attemptId.json에 temp → 안전한 교체·마지막 유효 파일 복구를 구현한다. 저장 실패에도 게임은 유지하고 마지막 저장 sequence와 불완전 상태를 제공한다.
7. 앱 시작·새 시도·종료에 각 콘텐츠 최근 10개만 유지한다. unfinished도 하나, 현재 기록 보호, temp/backup은 중복 계산하지 않는다. processInterrupted는 승패와 별개이며 확정 결과는 유지한다. 늦은 결과·중복 eventId는 원래 시도에 병합하되 retention 삭제 파일은 부활시키지 않는다.

## Files

- setup.md의 Telemetry·콘텐츠 adapter·전용 테스트 씬·Task 문서.
- 읽기 전용: 015 결과, 공개 Player/Run/Fire/Saws/Piggyback 이벤트.
- 누락 hook과 방 ID 공유 연결은 022, 데이터·빌드 산출물은 Git 밖.

## Risks

- 강제 종료 시 마지막 checkpoint 이후 손실 가능, 업로드 전 10개 제한으로 오래된 데이터 소실.
- 결과 미수신을 실패로 단정하지 않는다. UTC 전역 정렬·자기 신고 ID 인증을 보장하지 않는다.
- 쓰기 실패·overflow·늦은 메시지·손상 파일·콘텐츠별 보관 경계를 격리 경로에서 검증한다.

## Auto Verification

- check-work.ps1 구역·문서·컴파일·등록 테스트. 없으면 NO_PROJECT_TESTS.
- 새 asmdef 없이 격리 DTO/저장소 검사: 왕복·ID 수명·중복·late 결과·이탈/slot·콘텐츠 A 12→10/B 10 유지·현재 보호·복구·쓰기 실패·overflow·삭제 파일 부활 방지.
- 사용자 기존 로그를 수정하지 않고 Unity를 강제 종료하지 않는다. AI 검사는 사람 4인 대체가 아니다.

## 공동 테스트 항목

1. 각 개인 JSON의 room/session/attempt·행동·사지 결과가 실제 플레이와 맞고 다음 시도와 섞이지 않는다.
2. 다른 콘텐츠를 합산하지 않고 콘텐츠별 최근 10개가 남으며 저장 오류/불완전 상태가 개발자 화면에 보인다.
3. 이탈·호스트 종료·앱 재실행과 승패가 구분되고 확정 결과·지난 시도 늦은 결과가 정확히 유지된다.
