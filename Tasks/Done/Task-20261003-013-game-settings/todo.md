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

- [x] PR 병합 (GitHub 병합 기록 확인, 2026-10-05)
- [x] 공동 테스트 통과 (기존 PM 확인 기록 기준)
- [ ] 테스트 씬 처리 결정 기록 (`setup.md`)

## 완료 보관 (2026-10-05 PM)

- [x] PM 요청으로 `Tasks/Done/` 이동·상태 `done` 반영 (dev 반영과 별도)
- 기존 세부 체크의 미갱신 항목은 임의로 통과 처리하지 않는다. 최신 결과는 `meta.md`·Integration `checklist.md` 기준.
- 테스트 씬 유지/삭제 결정은 미정인 경우 그대로 두고 통합 종료 때 확정한다. 이번 작업은 씬을 변경하지 않는다.
