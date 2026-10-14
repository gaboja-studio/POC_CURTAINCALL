---
summary: 공통 조작키(2026-10-15 확인) — 단독 Shift/Ctrl hold와 WASD 조합 pressed toggle 구분, F 입력/hold 취소, 카메라·UI 역할; 위기/QTE는 예시
status: active
updated: 2026-10-15
source: human (Confluence 조작키 역할 구성 11633076, 2026-10-13 추가 항목 포함·2026-10-15 재확인)
---

# 공통 조작키 역할

## 언제 읽나

입력·조작 규칙을 추가할 때. 구조는 [player-control-architecture.md](player-control-architecture.md)(키→공통 역할→콘텐츠 규칙). 기획 동기화가 입력 에셋 변경을 자동 허용하지 않는다.

## 결정

- PC 전용, 최초 키 기준(이후 설정 변경 가능):
  - **캐릭터**: WASD hold 이동(줄타기 A/D 균형), Q/E hold 좌/우 감시, Space pressed 수직 점프, Q/E 또는 WASD+Space 방향 점프. F pressed 상호작용, F hold 취소.
  - **단독 입력**: L Shift hold 달리기, L Ctrl hold 앉기.
  - **복합 입력(2026-10-13 추가)**: WASD+L Shift **pressed 달리기 상태 toggle**, 여기에 Space로 달리기 방향 점프. WASD+L Ctrl **pressed 앉기 상태 toggle**. 단독 hold와 혼동하지 않는다.
  - **카메라**: 마우스 좌우/상하 시점, 휠 클릭 대상 집중(락온), 휠 위 줌아웃/아래 줌인.
  - **단축키/UI**: 1/2/3 슬롯, ESC 시스템/취소, Enter·LM 선택, RM 상세, LM 드래그·드롭 항목 이동/슬롯 적용, UI WASD pressed 포커스 이동.
  - **예시**: LM/RM 교차 위기 모면, QTE. 모든 콘텐츠의 필수 구현으로 확정된 것은 아니다.
- 필요한 역할만 할당한다. 목록에 없는 키 추가·작동 시점/시점 예외는 디스코드 `소집`에서 @모호성·@주형락 합의 후 반영한다.
- 부상은 키를 바꾸지 않고 결과만 약하게 한다([injury-rules.md](injury-rules.md)). 인간 대포의 새 F 발사·봉/장치 취소는 [기본 조작](Human-cannon/basic-control.md).

## 이유

공간·콘텐츠 간 조작을 이어 쓰면서 hold·pressed를 구분하여 자동 발사나 의도치 않은 토글을 막는다.

## 바꾸려면

사람 합의 후 Confluence·이 파일을 갱신하고, 입력 에셋(`Assets/Resources/Input/PlayerControls.inputactions`) 수정은 소유 Task에서 수행한다.
