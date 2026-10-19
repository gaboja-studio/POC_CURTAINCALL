# Todo

## PM 준비

- [x] 테스트 씬·폴더 생성, `setup.md` 작성
- [x] `plan.md` 공동 테스트 항목 합의
- [x] 인계 전 기술 점검 완료
- [ ] 담당자에게 실제 전달 — PM 확인

## 작업자

- [x] "시작해줘"로 작업 시작
- [x] 구현 (세부 항목은 아래에 추가)
  - [x] 공통 세팅 `음성 채팅` 묶음(+ 기본값 2/20m, 사용 씬 5개, 음소거 `M`)
  - [x] `VoiceRoom`·`VoiceRoomRules`(사망 판정·사용 씬·무리·채널 이름) + EditMode 테스트 16개
  - [x] `VoiceRoomKey`(Services 세션 Id / LAN named message)
  - [x] `VoiceChat` 관리자(Start 흐름·사용 씬 조건, 로그인 순서, 채널 전환, 3D 위치, 음소거 키, 재시도 5→60초)
  - [x] 테스트 씬 대신 개발자 모드 오버레이 `VoiceChatDevOverlay` (테스트 씬 삭제, 2026-10-20 PM)
  - [x] Domain Map `voice-chat.md` + index
  - [x] 실행 로그: Start 흐름 Services 호스트 → Vivox 로그인·생존 채널 입장·사망 전환·복귀·음소거 유지 (Vivox 활성화 후 2026-10-20 통과)
- [ ] "검사해줘" 통과
- [ ] "제출해줘"로 PR 제출

## PM 통합

- [ ] PR 병합
- [ ] 공동 테스트 통과
- [ ] 테스트 씬 처리 결정 기록 (`setup.md`)
