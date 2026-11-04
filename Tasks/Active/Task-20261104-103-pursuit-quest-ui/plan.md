# Task-20261104-103

- **Title:** 추격 단계 안내 문구 편집·추격 퀘스트 UI
- **Type:** feat
- **Assignee:** @MoHoDu
- **Branch:** feat/pursuit-quest-ui
- **Base:** dev

## Context
2026-11-04 사용자 요청.
- 추격 단계별 안내 텍스트를 직접 고칠 수 있게 한다.
- 추격 시간 동안 우측 퀘스트 UI를 띄운다.
  - 제목: `추격자를 피해 달아나세요.`
  - 줄: `- 모든 플레이어가 공연장 입구로 가서 공연 진행`
  - 이번 런에서 1명이라도 탈출로 입구(`B1_EscapeEntrance`)를 방문했으면 줄 추가: `- 모든 플레이어가 탈출로로 진입해 탈출 시도`

## 계획
1. 문구는 대기실 문구 표 `Assets/Resources/Settings/WaitingRoom/WaitingRoomTexts.txt`의 `pursuit.*` 키로 고친다.
   - `pursuit.alert-title`, `pursuit.stage.<번호>`(1부터, 추격 설계 창 단계 순서). 줄이 없으면 설정 에셋의 단계 알림 문구(기존 추격 설계 창 입력)를 쓴다.
   - 포획 알림 `pursuit.captured-title`·`pursuit.captured`, 퀘스트 `pursuit.quest.title/stage/escape`.
   - 2026-11-04 사용자: 기획자가 에디터 창에서 바로 고친다. 추격 설계 창('단계 / 연출' 탭)의 단계 알림 칸과 '추격 화면 문구' 칸에 입력하면 문구 표 해당 줄에 바로 저장(`PursuitTextTable`, 다른 줄·설명 보존, 비우면 줄 삭제 → 기본 문구). 단계 복제·제거 시 `pursuit.stage.<번호>`도 같이 밀어 다시 쓴다. 플레이 중 저장하면 다음 표시부터 반영.
2. 퀘스트 패널 `PursuitQuest`(DontDestroyOnLoad): `RestTime` + `RestTimerState.Pursuit` 동안, 내 캐릭터가 온전하고 잡히지 않았을 때 표시. 오늘 할 일 패널 자리(300) 재사용, 그 시간에는 첫날 퀘스트를 숨긴다.
3. 탈출로 입구 방문: 판정 화면이 `Area_B1_EscapeEntrance` 구역에 플레이 중인 누구든 들어가면 `ExplorationState`에 `visit:B1_EscapeEntrance`로 기록 → 기존 탐색 진행 동기화로 모두에게 전달, 런(세대) 교체 때 비움.

## 미정·주의
- 마지막 아침(공연 없음)에는 공연장 입구 줄을 뺀다(구현 판단, `NotifyAllAtStageEntrance`가 마지막 아침을 무시하므로).
- `WaitingRoomFirstDayFlow`(첫날 퀘스트)와 `ExplorationState`(탐색 진행)를 조금 수정했다.
