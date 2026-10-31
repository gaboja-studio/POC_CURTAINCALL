# 2026-10-31 합류 인원 기반 로딩 전환

## 요청과 범위
- 브랜치 `feat/exit-route/design-map`, 기반 `origin/dev` 5176099c. 기존 공개/비공개 변경과 백업·stash 유지. commit/push 없음.
- 원테이크와 합류 5분 타이머를 폐기. 열쇠 문 해제 후 4인 합류 → 뒤 문 봉쇄 → 기존 로딩 커튼 → 탈출로 도착 공간 → 계단 끝 1인칭으로 변경.
- 일반 대기실 타이머, 튜토리얼/기물/판정, 최종 전원 도착·리타이어 집계 규칙은 이번 변경 대상이 아니다.
- 사용자의 명시적 지시로 Play 및 MPPM 테스트는 실행하지 않았다.

## 구현 목록
- `EscapeAssemblyState`/`EscapeEntrance`: 합류 시간·만료·카운트다운 삭제, 인원만 집계. `EscapeRouteSettings`의 합류 시간 설정 제거.
- `EscapeSceneTransfer`: 원래 참가자 명부와 소유자별 상대 위치·몸/시선 방향 보존, 로딩 중 입력/물리 정지, 호스트가 전원 위치 저장·도착 확인. 끊긴 참가자는 대기 대상에서 제외하되 최종 명부에 보존.
- `SpaceLinkDirector.Escape`: 뒤 문 봉쇄 후 공용 커튼, NGO A 로드, 전원 배치 확인, W 해제, 커튼 해제. 기존 상시 A 사전 로드 경로 제거. 실패 시 완료 처리하지 않고 재시도.
- `EscapeSceneArrival`: A 도착 공간과 계단 카메라 담당. 전원 배치 후 앞 벽 열림, 계단 끝 개인별 1인칭. W 카메라 수명 인계.
- `EscapeCampaignContent`: 로딩 중 활성화 및 배치 준비, 지속 참가자 명부로 최종 도착 집계. 전환 중 리타이어 방지.
- `EscapeLoadingConnectionBuilder`: W 합류실만 남기고 연결 계단·통로를 A에 배치하는 편집 도구. 기존 초기 생성 도구도 새 흐름 연결.
- 기존 AssemblyState EditMode 테스트 소스는 시간 없는 API로 갱신했으나 실행하지 않았다.

## 저장한 씬
- W `BasementEscapeConnection`: 19 Transform. 계단 끝 카메라 트리거 없음. 합류 인원 안내만 표시.
- A `EscapeArrivalStairs`: 117 Transform. 대응 합류실·계단·통로·카메라 트리거 포함. 대기실 전체 복제 아님.
- W 합류 기준(-17.65,-8.20,93.35), A 합류 기준(-117.65,-8.20,93.35). A 프레임(-200,-16,118.3), 계단 경계(-138,-14,98.8). 기존 시설의 월드 경로도 프레임과 함께 이동.
- 양쪽 뒤 봉쇄벽 크기를 일치시킴. W 앞 벽은 닫힌 채 씬 해제, A 앞 벽만 전원 준비 후 열림.

## 검사와 한계
- 편집 모드 저장 보고: 두 씬 clean, 연결 참조 누락0, Play=false. `.harness/local/loading-transition-20261031/authoring-report.cs`와 변경 전 씬 사본 보존.
- `verify-unity.ps1 -Compile`: PASS(up_to_date), Tests SKIPPED.
- 새 전환의 실제 4인 동기화·부상/소지품 유지·카메라 인계·실패 재시도는 미검증. 이전 원테이크 플레이 결과를 새 전환의 통과 근거로 사용하지 않는다.
- 후속 플레이는 사용자 요청 때 진행한다. 이번 작업은 구현과 편집 모드 저장·컴파일까지만 진행.
- 최종 check-work: 안전성 PASS, 문서/규칙 FAIL(다른 담당 Task-20261029-079-waiting-room-m2/log.md 138줄 > 130줄). 내 plan/todo 한도 초과는 History/020 분리로 해소. 통합 검사 컴파일은 선행 문서 실패로 생략됐으나 별도 Compile PASS. 다른 담당 문서는 수정하지 않았다.
