# Integration-humanoid-characters

- **Feature:** 휴머노이드 캐릭터·입장 순서 배정·부상 절단
- **Status:** done
- **Branch:** integration/humanoid-characters
- **Base:** dev
- **Active Task:**
- **PM:** @MoHoDu
- **Merge Time:** #189 dev 병합 2026-10-19 20:04:01 UTC (`f9e0e0a4`)
- **Updated:** 2026-10-22

## 목표

- 실제 휴머노이드 모델과 입장 순서에 따른 최대 4인 캐릭터 배정, 기존 부상 상태에 연결한 절단 표현을 PM이 직접 구현한다.
- private main `6030fe10` 공급·실제 Player 연결·자동 검증 후 #188/#189 병합 완료. 2026-10-22 PM 요청으로 dev 반영 완료 기록 마감·worktree 제거. 사람 공동 플레이/다중 클라이언트·아트/권리·테스트 씬 유지/삭제 결정은 미결로 보존.

## Tasks

| Task | 종류 | 담당 | 브랜치 | PR | 상태 |
|---|---|---|---|---|---|
| Task-20261019-062 | feat | @MoHoDu | feat/humanoid-characters | #188 → dev #189 | done(dev 반영); 사람 검증 별도 |

## 공용 파일 소유 표

| 파일 | 소유 Task | 이유 |
|---|---|---|
| Assets/Resources/Prefabs/Characters/Players/ | Task-20261019-062 | 모델 래퍼·플레이어 프리팹 |
| Assets/Resources/Settings/GameSettings/ | Task-20261019-062 | 모델·부상 관련 설정 |
| Assets/Scripts/Systems/Player/ | Task-20261019-062 | 모델·래그돌·부상 연결 |
| Assets/Scripts/Systems/Network/ | Task-20261019-062 | 입장 순서 캐릭터 번호 공유 |

로컬 PM 직접 작업 배정이다. 다른 진행 중 Task의 소유권을 자동 변경하지 않으며 겹치는 파일은 담당자와 조율한다.

## Decisions

- Open: 부상 전용 동작·공연 접점/접지 개선, 실제 다중 클라이언트 확인, 최종 아트·배포 권리 확인.
- Decided: 별도 worktree·전용 Library, Body Type 1 사전 분할/캡·현재 pose 독립 비산, 기존 슬롯/빈 번호 재사용·Inspector 4색, 이동/부상 규칙 보존. 원격 통합 브랜치 준비는 PM 저장 승인 범위이며 기능 PR 병합 승인은 별도다.
