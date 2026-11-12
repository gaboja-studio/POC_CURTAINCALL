# Log

- 원래 작업과 엔딩 변경을 보존하고 origin/dev 836dd382에서 별도 핫픽스 브랜치로 시작. private 에셋은 기존 gitlink c46b6dc9를 독립 작업 폴더에 준비했다.
- `HudText.ConfigureView(info)`가 `<nobr>`를 삽입하지만 `LobbyRoomInfoView`는 갱신 시 코드 칸의 richText를 끈다. 포매터 등록 한 줄만 제거해 카드의 원래 일반 텍스트·한 줄 표시를 사용한다.
- 시작 doctor는 기존 기본 작업 폴더의 private 메타데이터/Task 중복/Unity 탐지에서 실패했다. 핫픽스 폴더는 독립 에셋과 실제 Unity 6000.3.25f1로 검증한다.
- 2026-11-13 Unity 6000.3.25f1에서 실제 LAN Lobby 표시 확인: 가림 점 8개, plain text 및 포매터 없음, 보기 시 원본 키. 같은 프리팹의 짧은 샘플 코드 표기·복사 이벤트도 원문 그대로 확인. Relay 접속은 실행하지 않았다.

- 2026-11-13 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- Unity 종료 때 별도 생성된 escape-labels/Vivox 변경은 스태시에 보관하고 제출에서 제외한다. 자동 저장의 재검사는 종료 상태라 ENV_BLOCKED였으므로, 직전 검사 및 LAN 검증과 동일한 수정 파일만 명시적으로 저장한다. 종료 후 origin/dev 동기화 결과 변경 없음.
