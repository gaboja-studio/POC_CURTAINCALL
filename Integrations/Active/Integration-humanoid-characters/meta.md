# Integration-humanoid-characters

- **Feature:** 휴머노이드 캐릭터·입장 순서 배정·부상 절단
- **Status:** active
- **Branch:** integration/humanoid-characters
- **Base:** dev
- **Active Task:** Task-20261019-062
- **PM:** @MoHoDu
- **Merge Time:** 미정
- **Updated:** 2026-10-20

## 목표

- 실제 휴머노이드 모델과 입장 순서에 따른 최대 4인 캐릭터 배정, 기존 부상 상태에 연결한 절단 표현을 PM이 직접 구현한다.
- 2026-10-20 저장·제출 승인. private main 6030fe10 저장 완료, 원격 integration/humanoid-characters를 최신 origin/dev dcfbd22에서 준비했다. 실제 Player 연결·자동 검증 완료, 사람 공동 플레이/다중 클라이언트 확인 및 PR 본문 승인은 남아 있다.

## Tasks

| Task | 종류 | 담당 | 브랜치 | PR | 상태 |
|---|---|---|---|---|---|
| Task-20261019-062 | feat | @MoHoDu | feat/humanoid-characters | - | working |

## 공용 파일 소유 표

| 파일 | 소유 Task | 이유 |
|---|---|---|
| Assets/Resources/Prefabs/Characters/Players/ | Task-20261019-062 | 모델 래퍼·플레이어 프리팹 |
| Assets/Resources/Settings/GameSettings/ | Task-20261019-062 | 모델·부상 관련 설정 |
| Assets/Scripts/Systems/Player/ | Task-20261019-062 | 모델·래그돌·부상 연결 |
| Assets/Scripts/Systems/Network/ | Task-20261019-062 | 입장 순서 캐릭터 번호 공유 |

로컬 PM 직접 작업 배정이다. 다른 진행 중 Task의 소유권을 자동 변경하지 않으며 겹치는 파일은 담당자와 조율한다.

## Decisions

- Open: 실제 모델 선정, 재접속/퇴장 후 슬롯 재사용, 절단 부위별 표현은 구현 중 PM 결정.
- Decided: dev 작업 폴더와 Library를 공유하지 않는 별도 worktree에서 PM이 직접 작업한다.
