# Task-20261114-132

- **Title:** 스케줄표 난이도 화살표 UI·제작 키트·포커스 가림 개선
- **Type:** fix
- **Assignee:** @MoHoDu
- **Branch:** fix/ui/schedule
- **Base:** dev

## Context
사용자 요청: 기존 1/2/3 난이도 버튼을 좌우 화살표 방식으로 바꾸고 현재 단계·적용 결과를 명확히 표시한다. 포커스 중 다른 캐릭터의 시각 가림과 클릭 방해를 제거한다.
진행 순서: 프로젝트 조사 → GPT Image 시안 1~2개 → 사용자 피드백 → 레이어/재료별 GPT Image 스프라이트 제작 키트 → Unity 신규 프리팹 교체 → 버그 수정·테스트·화면 캡처 보고.
담당은 현재 Git user.name 및 기존 공통 UI 담당을 근거로 @MoHoDu로 기록했다. 기존 미등록 작업 브랜치 fix/ui/schedule을 이 Task에 연결했다.

## 계획
1. 현재 SchedulePanel 프리팹, WaitingRoomScheduleUI 어댑터, ScheduleBoardView 포커스, ScheduleBoard 규칙을 조사한다.
2. 사용자 승인된 공연 중심 수정안으로 제작했다. 공연명이 카드에서 가장 크며, 좌우 화살표만 난이도를 변경하고 그 외 슬롯 클릭은 공연을 변경한다. 화살표 클릭이 부모 슬롯 공연 변경으로 전파되지 않는 것을 실제 포인터로 검사했다.
3. 확정 방향의 원본과 텍스트 없는 독립 스프라이트를 private _ThirdParty 신규 키트에 보존한다. 패널/종이/카드/난이도 배지/화살표 상태/잠금·체크/적용 강조를 분리하고 TMP 문구와 수치는 게임 데이터로 표시한다.
4. 신규 프리팹과 설치기를 통해 기존 런타임 생성 버튼을 교체한다. 실제 선택 성공 후 카드 강조와 적용됨 피드백을 표시한다. 기존 호스트 권한·해금·1일차 제한·보상 규칙을 유지한다.
5. 포커스 중 로컬 화면에서만 캐릭터 가림을 방지한다. 화면 Canvas 또는 전용 렌더 경로를 검토하고 ScheduleScreenHold 예외·툴팁·닫기 복귀를 함께 처리한다. 캐릭터 충돌/동기화 자체를 바꾸지 않는다.
6. 단계 경계·잠금·호스트/클라이언트·닫기 복귀·캐릭터 개입 상태의 렌더 및 실제 포인터 클릭을 검사하고 캡처를 제출한다.

## 미정·주의
- 공연명 최우선·화살표 난이도/그 외 슬롯 공연 변경 및 최종 수정안은 사용자 승인 완료. 구현·로컬 검증 완료, 커밋/push/PR은 요청하지 않아 하지 않는다.
- 시작 fetch 결과 HEAD와 origin/dev 차이 0/0. Unity가 열려 있으므로 실제 병합/체크아웃은 하지 않았다. 재개·제출 때 다시 fetch하고 차이가 생기면 에디터 상태를 보존해 종료한 뒤 반영한다.
- 기존 미저장 변경 4개(폰트 3개, escape-labels.asset)를 보존한다.
- SchedulePanelView와 제작 원본은 private _ThirdParty, 실게임 어댑터·설치기는 공개 Assets/Scripts에 있다. 기존 프리팹 GUID와 공용 UI 참조를 확인한 뒤 교체한다.
- 대기실 담당 @gyumin9048-ctrl와 겹칠 수 있는 위치: WaitingRoomScheduleUI*.cs, ScheduleBoardView.cs, CommonWaitingRoomUI 씬. 별도 메시지는 전송하지 않았다.
