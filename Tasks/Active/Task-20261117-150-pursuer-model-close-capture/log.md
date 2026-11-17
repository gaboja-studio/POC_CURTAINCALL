# Log

- 2026-11-17: origin/dev fetch 후 b933060a 기준 전용 브랜치 생성. 기존 폰트·재질 14개와 ThirdParty 작업을 보존했다. doctor는 기존 private HEAD 불일치·중복 Task ID로 FAIL, 에디터 초기 미연결 WARN. 코드 수정은 가능하며 에디터 실행 후 연결·컴파일을 확인했다.
- 탈출로 공격은 정면 25도 정렬 후 고정된 손 궤적과 플레이어가 닿을 때만 포획한다. 근접에서 회전 속도보다 빠르게 옆·뒤로 돌면 공격이 시작되지 않거나 계속 빗나갈 수 있다.
- 사용자 확인: 대기실 제외, 추격자 중심의 원형 판정. 실제 Body 메시 최대 수평 크기·스케일로 원형 반경을 정하고 공통 GameSettings의 여유 거리 0.6m와 플레이어 몸 반경을 적용한다. 몸통 Collider는 반지름 1.25m이나 실제 Body 메시 최대 반폭은 약 1.87m여서 콜라이더만으로 모델 크기를 판단하지 않는다.
- 기존 호스트 포획·기물 해제·연출·상태 복제를 재사용한다. 원거리 팔 공격은 유지한다. 벽과 출발점 overlap 및 발 사이의 층간 바닥, 컨베이어 대기·피난처·이미 포획/도착/사망 상태는 별도로 차단한다.
- 근접 EditMode: 초기 실패는 테스트의 NGO private setter 접근 및 프리팹의 등장 전 disabled Collider 설정 누락이었다. 실제 등장 상태로 fixture를 맞추고 disabled 상태 거부를 별도 검사했다. 컴파일 이후 Pipeline 요청 오류 1회는 재연결 후 재실행했다. 최종 36/36 PASS: 8방향 경계, 실제 UpdateAttack의 None/Warning/Strike/Retract에서 뒤쪽 포획, 부상/앉기·점프/목마 높이, 모델 스케일, 원거리 손 메시 제외, 벽/위층/출발점 차폐, 기존 보호·서버 권한. 근거 .harness/local/pursuer-close-targeted-v3-retry.txt. 실제 사용자 입력·목마 해제·다인 플레이 검증과는 구분한다.
- 공용 EditMode 회귀 1006/1006 PASS. 이후 모델 경계를 Awake에서 저장해 애니메이션 변경에 따른 반경 확장을 막고, 기물 해제 전에 포획된 발 위치를 저장하도록 보완했다. 보완 후 컴파일·공용 설정 PASS 및 EscapePursuerComplete 205/205 PASS(신규 36건 포함). 근거 .harness/local/pursuer-close-final.txt, pursuer-close-common-pass.json, pursuer-close-all-pursuer.txt. 최종 코드의 자동 검사 근거는 추격자 205건이며 이전 공용 검사는 보완 직전 결과다.
- PlayMode는 실행하지 않았다. 실제 원형 포획의 체감 거리·다인 입력은 사용자 플레이 확인 대상이다. 새 작업의 커밋·push는 요청하지 않아 수행하지 않는다.

- 2026-11-17 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=PASS

- 2026-11-17 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-17 사용자 저장·제출 요청: Unity EditMode·dirty scene 0 확인 후 정상 종료. origin/dev fetch·merge 결과 b933060a 최신, 충돌 없음. 최종 검사 이후 코드 변경이 없어 컴파일·공용 설정 및 추격자 205/205 결과를 재사용한다. 관련 11개 파일만 제출하고 기존 폰트/재질 14개·ThirdParty는 제외한다.
