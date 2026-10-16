# 042 트램폴린 손잡이 소유 표시 — 2026-10-16

- 사용자 요청: 트램폴린을 잡았는지 알 수 있도록 잡은 동안 손잡이를 초록색으로 표시한다.
- HumanCannonTrampoline에서 기존 ownership.IsOccupied를 읽어 Handle 하위2개 Renderer만 초록으로 표시한다. 놓기/소유 해제/Offline 초기화/비활성화 시 기존 PropertyBlock을 복원한다. 원격도 기존 소유 snapshot을 이용하며 프로토콜 변경은 없다.
- 공유 머티리얼·반동 면·몸체·씬 참조를 변경하지 않았다. 기본 노란 손잡이/회색 연결부 색은 놓으면 돌아온다. 새 테스트 파일은 만들지 않았다.
- check-work -Mode PlayMode: 기존 승인·저장된 전환 문서8개로 scope FAIL8/후속 SKIPPED. 이번 수정은042 코드/문서 안이며 별도 verify-fast PASS(기존 WARN5/hard0), diff --check PASS.
- 직접 Pipeline recompile completed·failed=false·errors=[] PASS. 관련 Trampoline EditMode22/22 PASS(실패/건너뜀0). 이전 톱/봉 변경 포함 HumanCannon272/272는 직전 변경의 검증 기록이다.
- Play 중 실제 손잡이 Renderer PropertyBlock 검사: idle 원색→호스트 소유 초록→Drop 원색→원격 소유값 초록→Disable 원색→재활성 초록→Offline 초기화 원색. 공유 머티리얼 색과 손잡이 밖 Renderer PropertyBlock 불변 확인.
- 소유 상태를 직접 주입하고 LateUpdate를 호출한 합성 검사다. 실제 입력·실제 원격 패킷·4인 플레이 검증은 미실행이다. 기존 소유 해제/재시작 경로는 읽기 검토로 확인했다.
- Play 종료, 저장 씬 dirty0. Console 오류2건(seq30/31)은 UnityEditor.SceneViewMotion.SetInputVector/WalkBackward의 NullReferenceException이며 게임 코드 프레임은 없다. 전체 Console 오류0으로 표기하지 않는다.
- 로컬 증거: .harness/local/task042-handle-color-{compile,editmode,runtime,console}.json. Git 제외.
- 사람 확인: 손잡이를 잡아 초록→놓아 원색으로 돌아오는지 확인하고, 원격 담당자가 잡고 놓을 때 같은 표시인지 확인한다.
