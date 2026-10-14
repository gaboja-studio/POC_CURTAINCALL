---
summary: 공통 조작키 역할(조작키 역할 구성 11633076, 2026-10-13) — WASD 이동·QE 좌우 감시·Space 점프(방향 조합)·F 상호작용(홀드=취소)·Shift 달리기·Ctrl 앉기(WASD 조합 토글)·1/2/3 기물 슬롯·마우스 카메라·휠 집중/줌, 키 추가·시점 변경은 결정권자 합의
status: active
updated: 2026-10-13
source: human (Confluence 조작키 역할 구성 11633076, 2026-10-13 PM @MoHoDu)
---

# 공통 조작키 역할

## 언제 읽나

새 입력·조작 규칙을 만들거나 입력 에셋에 역할을 추가할 때. 구조는 [player-control-architecture.md](player-control-architecture.md)(키→공통 역할→콘텐츠별 조작 규칙).

## 결정

- 2026-10-13 / PM(@MoHoDu), PC 전용(모바일·터치 제외), 최초 키 기준(이후 설정에서 변경 가능):
  - 캐릭터: W/S/A/D 홀드 = 이동(줄타기 A/D = 균형), Q/E 홀드 = 좌/우 감시(줄타기 옆줄 준비), Q/E+Space = 좌/우 오브젝트 극복(옆줄 점프), Space = 점프, W/A/S/D+Space = 방향 점프, F 입력 = 상호작용, F 홀드 = 상호작용 취소, L Shift 홀드 = 달리기, L Ctrl 홀드 = 앉기.
  - 복합(2026-10-13 추가): W/A/S/D+L Shift = 달리기 상태 토글, +Space = 달리기 점프, W/A/S/D+L Ctrl = 앉기 상태 토글.
  - 카메라: 마우스 = 시점, 휠 클릭 = 대상 집중(락온), 휠 스크롤 = 줌.
  - 단축키·UI: 1/2/3 = 기물 슬롯 선택, ESC = 시스템 메뉴/취소, Enter·LM = 선택, RM = 상세(액션 툴팁), LM 드래그·드랍 = 항목 이동·슬롯 적용, UI 안 W/A/S/D = 슬롯 이동.
  - 이벤트: LM+RM 교차 입력 = 위기 모면(잡혀 끌려갈 때 탈출), * = QTE.
- 규칙: 필요한 키만 할당, 목록에 없는 키 추가·작동 시점 변경은 디스코드 `소집`으로 @모호성·@주형락 합의 후. 부상은 키 역할을 바꾸지 않고 결과만 약하게 한다([injury-rules.md](injury-rules.md) #7).

## 이유

공간·콘텐츠를 하나의 게임으로 느끼고, 탈출에서 묘기·기물·탐색 조작이 그대로 이어지게 한다.

## 바꾸려면

Confluence 조작키 문서와 이 파일, 입력 에셋(`Assets/Resources/Input/PlayerControls.inputactions`)을 함께 고친다.
