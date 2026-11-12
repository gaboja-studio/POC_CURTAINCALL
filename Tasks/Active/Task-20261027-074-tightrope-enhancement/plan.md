# Task-20261027-074

- **Title:** 외줄타기 공연 고도화
- **Type:** feat
- **Assignee:** @lakuase2
- **Branch:** feat/tight-rope/enhancement
- **Base:** dev

## Context
@lakuase2가 현 브랜치에서 외줄타기 공연을 고도화한다. 추격 불벽·톱날 외형·균형 HUD 개선은 기존 PR로 dev에 반영됐다. 이번 제출은 해제봉 무작위 연결 줄·고정 LED 유지 시간 전광판·봉 매달림 중 위험 회복을 최신 dev와 통합한다.

### 2026-10-29 통합·제출 범위
- 사용자 요청: `feat/tight-rope/enhancement`에서 최신 `origin/dev`를 병합하고 양쪽 기능을 보존한 뒤 저장·push·dev PR 제출. dev 직접 병합은 하지 않는다.
- dev의 M2 매달림·구조(`IsSuspended`, `GaugeFallCatcher`)와 새 `RecoverWhileSupported`를 함께 유지한다. 일반 균형 계산은 정지하되 봉의 명시적 회복은 작동하는지 검사한다.
- 무작위 연결은 출발 봉 줄 제외·열린 줄 중복 방지·교대 봉 유지 규칙을 보존한다. 전광판은 SS.ss·상시 배치, 기존 연결 케이블/문구 숨김 토글을 유지한다.
- 수용 기준: 충돌 미해결 0개, Unity 컴파일 성공, 관련 EditMode·PlayMode 결과와 기존 실패/미확인 사항 공개, 최신 dev가 작업 브랜치의 조상, 지정 팀원 검토용 PR 제출.

- 관련 지도: [외줄 묘기](../../../Docs/Domains/tightrope-course.md). 과거 조사 날짜의 구현·계획 표기는 현재 코드와 대조한다.
- 조사 진입점: `Assets/Scripts/MainStage/Tightrope/`, `Assets/Resources/Settings/GameSettings/Tricks/TightropeSettings.asset`. 실제 씬·프리팹과 공용 시스템 연결은 담당자가 확인한다.

### 2026-10-29 실패 테스트 후속 수정
- 사용자 요청: 통합 검사 실패 10건의 원인을 확인하고 수정한다. 현재 제품 계약과 실제 비동기 준비 조건을 기준으로 fixture를 정리하며, assertion 삭제나 제품 설정 변경으로 통과시키지 않는다.
- 원래 실패한 시나리오와 관련 회귀 테스트를 재실행하고 결과를 기록한다. 이번 후속 수정에서는 별도 요청 없이 commit/push·PR Ready 전환·dev 병합하지 않는다.

## 계획
1. [start-work](../../../Harness/Core/Skills/start-work.md)를 따라 현재 브랜치에서 이어 간다.
2. 외줄 공연의 코스·조작·장애물·진행·결과와 관련 씬을 조사해 개선 항목과 확인 방법을 정한다. 미정 게임 의도는 [human-decision](../../../Harness/Core/Policies/human-decision.md)을 따른다.
3. Unity 변경 전 [코드 위치](../../../Harness/Engine/Unity/Policies/code-folders.md)와 [에셋 협업](../../../Harness/Engine/Unity/Policies/asset-ownership.md)을 확인한다. 씬·프리팹 편집은 integrate-editor, 코드 구현은 implement-code를 따른다.
4. 수정 묶음 완료 시 `pwsh -File Scripts/check-work.ps1`을 한 번 실행하고 결과·환경 제약·다음 할 일을 log에 기록한다. 재미·방향·최종 플레이 경험은 사람이 확인한다.

## 외줄타기 1단계 (2026-11-11 사용자 지정)
현재 `TightropeSettings.asset` 코스 값과 공중그네 봉 배치를 1단계로 한다. 줄 6개, 0~165m, 줄 간격 3m. 톱날 번호는 플레이에 나오는 순서(`sawDensity` 0.6, 14개 중 9개).
- 구간: 1구간 0~40 / 해제봉 빈칸 40~50 / 2구간 50~100 / 공중그네 빈칸 100~116 / 3구간 116~165m.
- 해제봉: 출발 37.5m·도착 52m(2·5번 줄), 1명 유지로 연결.
- 공중그네(3번 줄): 출발 봉 99m(줄 위 5.0m)·낮은 복귀 봉 110.5m(2.1m), 가운데 교대 봉 없음.
- 가로 톱날: 1번 21.8 / 2번 43.4 / 3번 55.3m(수평, 60.78m에서 가로지름) / 4번 70.8 / 5번 80.6 / 6번 119.8 / 7번 129.6 / 8번 149.2 / 9번 159m. 3번 외에는 공통 30° 사선.
- 플레이 확인 전: 공중그네 11.5m 비행 도달·116m 착지, 톱날·해제봉 배치 체감.
- 단계 연결: 1단계 = `TightropeSettings` 코스·톱날 칸. 2·3단계 = 1단계 + `laterStages` 칸의 추가 기물(지금은 `extraHorizontalSaws`, 항상 켜짐). 대기실에서 고른 단계를 `CampaignTightropeAdapter`가 판 시작 전에 적용.
- 2단계: 1단계 9개 톱날마다 X자 반대 사선 짝(+2m/s, 3번 짝은 57.78m 중심). 추격 불 0.7m/s.
- 추격 불 속도(사용자 결정 2026-11-11): 1단계 0.5 / 2단계 0.7 / 3단계 1m/s.
- 3단계(사용자 결정 2026-11-12, 브랜치 `fix/main-stages`): 2단계 구성을 그대로 가져감 — `laterStages[1]`에 2단계 X자 짝 9개 복사 + 불 1m/s. 단계는 누적이 아니라 "1단계 + 그 단계 칸"이므로 2단계 추가 톱날을 바꾸면 3단계 칸도 같이 맞춘다. 3단계 전용 추가: 정면 톱날(줄 위 세운 원형 톱, 지름 1m, 옆줄로만 회피, 발판 없음) — 도착 쪽 외줄 끝 5m 앞(160m)에서 생겨 출발점 0m까지 줄을 따라 직진. 누군가 줄에 오르는 즉시 6개 줄 중 랜덤 1곳에 생성, 사라지면 20초 뒤 반복. 테스트 씬 `startStage`는 1로 되돌림(2026-11-12).

## 미정·주의
- 구체적인 기능 추가·밸런스·연출 방향은 아직 지정되지 않았다. 이번 세팅에서 확정하지 않는다.
- 다른 작업과 수정이 겹치면 알리고, 씬·프리팹 YAML 충돌은 담당자와 해결한다. 기존 에셋의 큰 수정·삭제는 PM·에셋 관리자와 조율한다.
- 별도 구역 배정·PM 인계 승인·Jira 등록은 착수 조건이 아니다.
- dev 반영은 PR 1회와 지정 팀원 검토를 거친다. commit/push는 사용자 요청 시에만 한다.
