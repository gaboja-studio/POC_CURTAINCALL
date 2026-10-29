# Task-20261029-079

- **Title:** 대기실 타이머 v2.8 프리팹 제작과 기존 HUD 교체
- **Type:** fix
- **Assignee:** @MoHoDu
- **Branch:** fix/waiting-room-resttimer
- **Base:** fix/controls-and-rope-rescue

## Context
사용자가 추가한 private curtaincall_resttimer_v2_8 패키지의 11 PNG·Runtime·조립기를 이용해 실제 대기실 W1을 교체한다. 중앙은 남은 시간 또는 무한대, 하단은 시간대 아이콘과 D-3→D-0만 표시한다. 일차 표기는 사용자 확인 완료. Unity 조작은 CLI만 사용한다.

## 계획
1. 패키지 조립기를 실제 private 경로에서 동작하도록 연결하고 기존 Pretendard 폰트를 읽기 전용 재사용한다.
2. 전용 Resources 프리팹을 생성하고 CommonWaitingRoomUI 씬에 직렬화된 참조로 배치한다. 옛 W1·하루 시계는 비활성화해 중복 표시를 막는다.
3. CampaignSnapshot의 실제 남은 초·명시적 Unlimited/Running/Stopped 상태와 해당 쉬는 시간/밤 제한 초 설정을 한 snapshot으로 전달한다. 공연·수면·기타 단계에서는 숨긴다.
4. 1~4일차·오전/공연 사이/밤·경고30/10/0초·무제한/미수신·새 구간·화면비 및 실제 대기실 연결을 검증한다.

## 주의
- 기존 브랜치 513b3a25를 기준으로 분기. TMP 폴백 폰트의 별도 변경은 보존한다. 새 패키지는 사용자 미저장 파일이며 삭제·이동·자동 커밋하지 않는다.
- doctor: 신규 private 패키지 dirty 및 기존 Task-077 중복, CLI 버전/skill-link 경고. 대상 에디터는 올바른 프로젝트이며 새 패키지 임포트 후 연결 확인 완료. 초기 CLI 타임아웃은 약117초 패키지 임포트와 겹쳤다.
- 기존 DayClock ScheduleProfile·원본·기타 HUD와 설정을 보존한다. 공통 UI·기존 W1 검사는 현재 사용자 요구로 업데이트하며 관련 Task-077 작업과 겹침을 log에 기록한다.
- 새 타이머는 사용자 위치 정정에 따라 기존 좌상단(48,-64), 192×220이다. 게임 시간대는 별도 시각 필드가 없는 Campaign의 Morning/Guide·Between/Night를 매핑한다.
