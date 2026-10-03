# Todo

## PM 준비

- [ ] 테스트 씬·폴더 생성, `setup.md` 작성
- [ ] `plan.md` 공동 테스트 항목 합의
- [ ] 인계 전 체크 완료, 담당자에게 전달

## 작업자

- [x] "시작해줘"로 작업 시작
- [ ] 구현 (세부 항목은 아래에 추가)
  - [x] 세팅 클래스 5종(`GameSettings`·`BaseGameSettings`·`TrickSettings`·`TightropeSettings`·`RewardTable`), 칸마다 한글 Tooltip·Header
  - [x] 세팅 에셋 3개 생성·연결(`GameSettings`, `BaseGameSettings`, `Tricks/TightropeSettings`), 값 = 옮기기 전 프리팹 값
  - [x] Player 컴포넌트(이동·균형·입력·기울기·래그돌·모델 키), 코스·진행, 세션 정원·최소 인원, 균형 전송 기준을 세팅 읽기로 변경
  - [x] Player·TightropeCourse·NetworkManager 프리팹 숫자 칸 삭제(재저장, 삭제 50줄만)
  - [x] 컴파일 PASS, Tightrope 씬 플레이 중 줄 수 변경 → 코스 재생성 확인
  - [x] 공동 테스트 1~3 직접 플레이 확인(작업자, 2026-10-03 @MoHoDu "잘 되는 것 같아")
  - [ ] Domain Map 조정값 표 "지금 위치" 갱신 — `Docs/` 구역 밖, PM 처리
- [ ] "검사해줘" 통과
- [ ] "제출해줘"로 PR 제출

## PM 통합

- [ ] PR 병합
- [ ] 공동 테스트 통과
- [ ] 테스트 씬 처리 결정 기록 (`setup.md`)
