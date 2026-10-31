# Todo

## 준비
- [x] origin/dev 병합(에디터 종료 확인, 5da1e1ea 로컬 병합 커밋)
- [x] 문구 확정(guide-copy-draft.md 3차) + G11A 소집 경보 추가 → 24항목

## 구현
- [x] 문구 정본: `Assets/Resources/Settings/WaitingRoom/WaitingRoomTutorialTexts.txt`(`ID | 제목 | 본문`, 사용자가 직접 수정). 빠진 줄은 코드 기본 문구
- [x] 가이드 전용 패널 `WaitingRoomTutorialPanel`(툴팁 정본 패널 모드, 왼쪽 시계 아래). 나레이션은 `WaitingRoomGuidePanel`(상단) 유지
- [x] 공용 UI 재로드(공연 다녀옴) 뒤 두 패널 글자 폰트 유실 → 다시 만듦
- [x] 사용자 문구 수정본 반영(24줄 일치, 캡처 확인)
- [x] 진행 장부 `WaitingRoomTutorialBook`(줄 세우기·입력 막힘 중 예외·저장 문자열·키 이름 치환)
- [x] 진행 관리자 `WaitingRoomTutorial`(자동 생성, 기존 상태·이벤트만 구독, 개인별)
- [x] ① 가이드 쉬는 시간 · ② 밤 정비 · 상황 안내 연결
- [x] 본 안내 기록 앱 실행 단위(메모리, 앱 재시작 때만 초기화. 날·세대 교체·새 게임 유지) · 끄기만 PC 저장 · 설정 게임 탭 끄기/처음부터/본 안내 다시 보기
- [x] 키 이름: 자판 배치가 아닌 키 위치 이름(한글 자판 'ㄹ' 문제 수정)
- [x] 안내 패널 한글 깨짐(□) 수정: UiRuntimeFontFallback 대상에 WaitingRoomGuidePanel 추가
- [ ] 패널 단계 표시·다른 UI와 겹침 회피(지금은 입력 막힘 중 대기만)
- [x] 설명 패널 수치 설정 파일(WaitingRoomTutorialPanel.txt)·안내별 표시 시간 칸
- [x] 대기실 화면 문구 파일(WaitingRoomTexts.txt, 236키)·아이템 이름/설명
- [ ] (선택) 위치 안내
- [x] 테스트가 PC 안내 설정·기록을 바꾸지 않게(MemoryOnly)
- [ ] 할 일(지시형) 가이드 — directive-guide-plan.md 계획 확정 대기

- [x] 침대: 스케줄표 확정 전 사용 불가 + 가이드 패널 안내
- [x] 밤 정비 소품실 개방(철창을 작업실 갈림길로 이동)
- [x] 연계 튜토리얼 조작 잠금·클로즈업(리스트 확정) · 기물 안내는 연계 뒤 출력
- [x] 컴파일·PlayMode W5_Tutorial 2/2·M2_NightPowerGatesAndBedWake 1/1·W3_BedRestCamera 1/1
- [ ] 사람 플레이: 클로즈업 구도(벽에 가리는지)·철창 위치

## 검사
- [x] EditMode WaitingRoomTutorialBookTests 7/7(문구 파일 파싱·전 ID 포함)
- [x] PlayMode W5_Tutorial 2/2(가이드 순서·저장·다시 보기 / 밤·끄기), 회귀 W3_ 4/4·W2_BodyRestore 2/2·SettingsUpgrade 5/5 (가이드 패널 분리 후 W5 2/2·W3_HumanCannonSet 1/1·W2_BodyRestore 2/2 재확인)
- [ ] check-work: 문서/규칙 FAIL — Task-079 log.md 138줄(hard 130), 이 Task 무관. 압축 후 재실행
- [ ] MPPM 2인: 서로 다른 진행 상태
- [ ] 사람 플레이 확인(가독성·방해 여부·실제 트리거 순서) — human-verify
