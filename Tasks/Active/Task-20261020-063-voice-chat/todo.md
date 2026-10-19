# Todo

## PM 준비

- [x] 테스트 씬·폴더 생성, `setup.md` 작성
- [x] `plan.md` 공동 테스트 항목 합의
- [x] 인계 전 기술 점검 완료
- [ ] 담당자에게 실제 전달 — PM 확인

## 작업자

- [ ] "시작해줘"로 작업 시작
- [ ] 구현 (세부 항목은 아래에 추가)
  - [ ] 공통 세팅 `음성 채팅` 묶음(+ 기본값 2/20m, 음소거 `M`)
  - [ ] `VoiceRoom`·`VoiceRoomRules`(사망 판정·무리·채널 이름) + EditMode 테스트
  - [ ] `VoiceRoomKey`(Services 세션 Id / LAN named message)
  - [ ] `VoiceChat` 관리자(로그인 순서·reconcile 채널 전환·3D 위치·음소거 키·끊김 정리)
  - [ ] `VoiceChatTestUI` + 테스트 씬 구성
  - [ ] Domain Map `voice-chat.md` + index
  - [ ] 실행 로그: Services 호스트 1인 로그인·생존 채널 입장
- [ ] "검사해줘" 통과
- [ ] "제출해줘"로 PR 제출

## PM 통합

- [ ] PR 병합
- [ ] 공동 테스트 통과
- [ ] 테스트 씬 처리 결정 기록 (`setup.md`)
