# Task-20261117-153

- **Title:** 착지·추격자 월드 UI 키트 고도화
- **Type:** feat
- **Assignee:** @MoHoDu
- **Branch:** feat/ui/ingame-ui
- **Base:** dev

## Context
착지·추격자 공격·탈출로 장애물·대포 궤적의 월드 UI를 조사하고 기존 HUD 디자인과 맞는 공통 프리팹 키트로 고도화한다. 담당은 현재 Git 사용자 및 선행 착지 표시 Task의 @MoHoDu를 따른다.
사용자 요청 순서: 상황 묶음 → 원화 시안 → 피드백·승인 → 레이어 스프라이트 → Unity 프리팹 → 공통/세부 컴포넌트·GameSettings → 사용자와 검증.

## 계획
1. [inventory.md](inventory.md): A 그림자·B 원형 위험·C 사각 위험·D 지면 이동 경로·E 대포 궤적·F 공중 공격 통로로 묶는다. 그랩은 F이며 끝점 원으로 대체하지 않는다.
2. [design-review.md](design-review.md): HUD 색·선·위험 빗금에 맞춘 원화를 제작해 사용자에게 제시한다. 시안은 런타임 캡처나 승인된 최종 에셋이 아니다.
3. 승인 후 `_ThirdParty/curtaincall_world_ui_v1/`에 레이어별 원본·스프라이트·명세를 보관한다. 기존 플레이어 그림자 아트는 재제작하지 않는다.
4. [implementation-plan.md](implementation-plan.md)의 표시 계약을 기준으로 프리팹·어댑터·GameSettings를 구현한다. 기존 물리/피해/공유 시각을 UI에서 재정의하지 않는다.
5. 원화 단계 문서 검사는 한 번, 구현 묶음 완료 시 필요한 Unity 검사와 사용자 플레이 검증을 수행한다.

## 미정·주의
- 제출: [PR #417](https://github.com/gaboja-studio/POC_CURTAINCALL/pull/417), feat/ui/ingame-ui → dev. 저장/push 완료, 지정 팀원 검토 및 사용자 실전 검증 대기.
- 사용자 저장·dev 제출 승인(2026-11-17). 구현/회귀 검증은 완료했으며 dev 대상 PR로 제출한다. 사용자 실전 검증과 추격자 그림자 비용 비교는 별도 후속 항목으로 유지한다.
- 최신 피드백: 준비 표적을 머리 위에서 실제 몸 Aim으로 변경. UI·전투 좌표를 공유하고 기존 위치 확정/선딜/선택 경로는 유지한다. 설정·실전 검증 가이드는 manual-verification.md.
- 현재 단계: 추가 지시의 표적 추적→위치 확정·선택형 경로·화면 위험 효과까지 구현, 프리팹/설정 연결·회귀 검사·Play 캡처 완료. 최종 사용자 가독성 검토·두 peer 실전·조건부 추격자 그림자 성능 비교는 남음. 정본 사용법은 implementation-report.md.
- 재개 시 Unity 종료 확인 후 origin/dev fetch·fast-forward 반영: b933060a → 7e599c96. 새 Task-148과 ID가 겹쳐 미추적 Task를 151로 변경. 기존 작업 보존, 커밋·push 없음.
- 제출 동기화: 7e599c96 → b34664aa, 새 Task-151과 겹쳐 현재 Task153으로 변경. 병합 후 컴파일·설정 검사 및 화면 효과15검사 재통과. 마지막 fetch0/0, Unity 정상 종료.
- 초기 doctor의 Unity 미설치 판정은 sandbox 가시성 한계였다. 상승 권한 CLI에서 Unity 6000.3.25f1과 Pipeline 0.8 연결을 확인해 컴파일·Play 검증 가능. doctor는 같은 Task에서 재실행하지 않음.
- 추격자 원형 그림자 교체는 성능 이득을 측정해 확정한다. 기존 대기실 방향 안내와 그림자는 별개다.
- 원화에서 위험 크기·표시 시간은 설명용이다. 실제 공격 범위와 예고 시간은 기존 게임 설정을 읽는다.
- 후속 수정 겹침: 선행 착지 Task-126, 대기실 추격자 외형, 탈출로 추격자·장애물, 대포 시스템. 작업 범위 기록으로 공유하며 외부 메시지는 보내지 않는다.
