# Log

중요한 사건만 1줄(30~50단어)로 적는다. 기본 읽기 대상이 아니다. 자세한 내용은 `History/`로 옮긴다.
배운 함정·결정·사실은 여기 묻지 말고 지식 폴더로 보낸다(`record-knowledge`).

## 2026-10-14

- Task 생성.
- 구현: 기본 이동 규칙(`ControlSettings`·플랫폼 이동 공용 이전), 씬 규칙, 공용 `InputGate`·`UIInputBlocker`, 외줄 규칙 공연장 폴더 이전(GUID 유지). 컴파일 PASS, EditMode 5/5. 씬 배치·플레이 테스트는 사람(AI Setup None).
- PM 예외로 ControlRules 씬 AI 배치. Step 진행으로 공동 테스트 1~3 확인(proto 미저장, 개발자 모드·입력 설정 원복). 실제 키 이벤트는 AI 재현 불가.
- 작업자 키보드 확인 PASS, 폰트 SDF 자동 변경 되돌림(동의), PR #61 → integration/common-game-systems.
