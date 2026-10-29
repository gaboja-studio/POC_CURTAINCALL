# 런 포기·그래픽 설정 앞 묶음 기록

설정 5탭 재구성 전의 완료 기록을 보존한다. 현재 상태는 plan/todo/log와 Facts/settings-ui.md를 따른다.

## 2026-10-28 런 포기·그래픽 설정 구현
- 사용자 확정: 온라인 런 포기는 호스트 전용(오프라인 허용). 대기실 쉬는 시간/밤 재확인 후 Abandoned 정식 실패 경로로 다음 세대1일차 첫 공연, 방 유지. 권한·장소·전환·확인 시점 상태 재검사 및 중복 방지.
- ClientSettings에 화면3모드·지원 해상도·FPS30/60기본/90/120/144/165/240/무제한 로컬 저장·기존 bool 이관 구현. FrameRateController의 공통 FPS 매 프레임 강제 제거. C14 원본·씬/프리팹 변경 없이 게임 탭 진입 버튼과 그래픽/확인 보조 패널 추가, 기존 음량·음성 유지.
- 관련 검사: 컴파일PASS, EditMode RunAbandon6/6·PlayMode FrameRate21/21·SettingsUpgrade2/2·CommonMenu3/3. 최초 SettingsUpgrade1/2 실패는 전환 직후 InputGate 해제 대기를 보정한 뒤 통과. 누락 화살표 글리프는 ASCII로 교체, 최종 캡처 확인. 전체 회귀 재실행 아님.
- 계획 묶음 첫 검사: log137줄 hard1로 문서FAIL·컴파일/테스트SKIPPED. 완료된 환경·초기 문서 기록을 History/007에 보존 후 문서PASS. 구현 최종 check-work: 안전성·문서/규칙·컴파일PASS, Knowledge OK, hard0·경고14, 전체 테스트SKIPPED(선택 안 함). diff-check 공백 오류0·LF/CRLF 경고만 있음.
- 실제2인·PC 빌드 화면 전환/재실행·게임패드 미검증은 manual-verification에 유지. 테스트 후 공용 TMP 폰트 동적 캐시 변경 보존, Vivox 설정은 실내용 diff 없이 줄바꿈 상태만 관측. 이번 기능 commit/push 없음.
