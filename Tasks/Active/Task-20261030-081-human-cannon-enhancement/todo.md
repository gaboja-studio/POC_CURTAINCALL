# Todo

`(n)`은 우선순위(작을수록 중요)이며 작업 순서가 아니다. 순서는 담당자가 그때 정한다. 대상 씬: `Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity`.

## 준비
- [x] 담당자와 브랜치를 지정하고 Task 문서 생성
- [x] 고도화 항목과 우선순위 수령(2026-10-30, @lakuase2), 항목별 씬·코드 진입점 조사

## (1) 우선
- [ ] 수평톱 외줄타기 공통 에셋 수정 — `TightropeSaws`·`SawSettings`(`Assets/Scripts/MainStage/Tightrope/Saws/`) 공통 형태. `HumanCannonFinalSawBuilder`가 형태만 복제 중. 외줄타기(Task-074)와 겹치므로 수정 전 알림.
  - [x] 인간대포 톱 외형을 외줄타기 `HorizontalSaw` 프리팹으로 교체(지상 톱 12, 관문 톱 88슬롯). 위치·판정 유지. 공통 에셋 자체는 미수정.
- [ ] 좌측 이동 장치: 1명 + 트램펄린일 때만 이동, 2명 이상이면 작동하지 않음 — `SplitRouteCourse/DepartureTransport`(x=-13.25), `HumanCannonTransport`, `HumanCannonRouteSystem`(현재 과승 시 바닥 개방, 낙하 시 y-10 복귀), `LiftBoardingState`. 출발 조건은 이미 "1명 + 트램폴린 운반자"다.
- [ ] 도착지점 공중 그네 삭제 — `ExtendedCourse044/CeilingPole1_4`·`CeilingPole1_4Ceiling`(z=174, FlightLandingPad 위). 참조하는 `HumanCannonPoleSystem`·빌더·테스트 정리.
- [ ] 1구간 고도화 — Section1(z≈0~72).
  - [x] 관문 톱벽: 88장 틈 0.3m 등간격, 무광 재질, 거리별 외곽선 감쇠.
  - [x] 첫 톱벽 앞 지상 톱 3개 직접 배치(`authoredPath`): z22.5·31.5(수평)·39.6, 10°.
  - [x] 시작 지점 아래 안전 바닥·복귀 계단 삭제, 시작 바닥 폭을 1구간과 같게(x-22~22).

## (2) 다음
- [ ] 플레이어 낙하 위치 UI 그림자 — 비행 중 예상 착지점 표시. 현재 해당 기능 없음(`HumanCannonGroundMarkers`는 지면 표식).
- [ ] 공중 그네 배치 수정 — `PoleGateCourse`·`SecondSectionPoleGate`의 Normal/LinkedPole, `HumanCannonEnhancement/AddedPole0·2`. 목표 배치 미정.
- [ ] 이동 장치 방식 수정 — DepartureTransport·ShelterLift·ShelterDeparture.
  - [x] 발판 5.5×4m, 구멍 가장자리에 붙이고 남은 구멍 메움, ShelterLift는 경유점(`via`)으로 정면 대각선 상승 후 오른쪽 이동.

## (3) 나중
- [ ] 피날레 고도화 — `FinalCannonCourse`, `HumanCannonFinalSaws`, `FinalSawTimeline`. 구체 내용 미정.

## 마무리
- [ ] 묶음 완료 시 필요한 검사와 결과 기록
- [ ] 담당자가 공연의 재미·방향·최종 플레이 경험 확인
