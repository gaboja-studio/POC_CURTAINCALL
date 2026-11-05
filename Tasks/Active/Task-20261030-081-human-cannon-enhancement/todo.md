# Todo

`(n)`은 우선순위(작을수록 중요)이며 작업 순서가 아니다. 순서는 담당자가 그때 정한다. 대상 씬: `Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity`.

## 준비
- [x] 담당자와 브랜치를 지정하고 Task 문서 생성
- [x] 고도화 항목과 우선순위 수령(2026-10-30, @lakuase2), 항목별 씬·코드 진입점 조사

## (1) 우선
- [ ] 수평톱 외줄타기 공통 에셋 수정 — `TightropeSaws`·`SawSettings`(`Assets/Scripts/MainStage/Tightrope/Saws/`) 공통 형태. `HumanCannonFinalSawBuilder`가 형태만 복제 중. 외줄타기(Task-074)와 겹치므로 수정 전 알림.
  - [x] 인간대포 톱 외형을 외줄타기 `HorizontalSaw` 프리팹으로 교체(지상 톱 12, 관문 톱 88슬롯). 위치·판정 유지. 공통 에셋 자체는 미수정.
- [ ] 좌측 이동 장치: 1명 + 트램펄린일 때만 이동, 2명 이상이면 작동하지 않음 — `SplitRouteCourse/DepartureTransport`(x=-13.25), `HumanCannonTransport`, `HumanCannonRouteSystem`(현재 과승 시 바닥 개방, 낙하 시 y-10 복귀), `LiftBoardingState`. 출발 조건은 이미 "1명 + 트램폴린 운반자"다.
  - [x] 과승 시 바닥 개방 제거(`overloadTrap` 끔), 1명+트램폴린 초록·과승/트램폴린 없음 빨강 발판(2026-11-02, ShelterLift·ShelterDeparture도 2026-11-04). 실제 플레이 확인 필요.
- [x] 도착지점 공중 그네 삭제(2026-11-02, 그네·천장·줄 삭제, 테스트 갱신) — `ExtendedCourse044/CeilingPole1_4`·`CeilingPole1_4Ceiling`(z=174, FlightLandingPad 위). 참조하는 `HumanCannonPoleSystem`·빌더·테스트 정리.
- [ ] 1구간 고도화 — Section1(z≈0~72).
  - [x] 관문 톱벽: 88장 틈 0.3m 등간격, 무광 재질, 거리별 외곽선 감쇠.
  - [x] 첫 톱벽 앞 지상 톱 3개 직접 배치(`authoredPath`): z22.5·31.5(수평)·39.6, 10°.
  - [x] 시작 지점 아래 안전 바닥·복귀 계단 삭제, 시작 바닥 폭을 1구간과 같게(x-22~22).

## (2) 다음
- [ ] 플레이어 낙하 위치 UI 그림자 — 비행 중 예상 착지점 표시. 현재 해당 기능 없음(`HumanCannonGroundMarkers`는 지면 표식).
- [ ] 공중 그네 배치 수정 — `PoleGateCourse`·`SecondSectionPoleGate`의 Normal/LinkedPole, `HumanCannonEnhancement/AddedPole0·2`. 목표 배치 미정.
  - [x] 코스 첫 그네(`PoleGateCourse/NormalPole`, z34)와 위 천장·줄 삭제(2026-11-02).
  - [x] 톱벽 그네를 잡으면 톱벽이 좌우로 갈라져 트램펄린 폭 + 0.5m씩 틈이 열리고 유지(2026-11-03). 실제 플레이 확인 필요.
- [ ] 이동 장치 방식 수정 — DepartureTransport·ShelterLift·ShelterDeparture.
  - [x] 발판 5.5×4m, 구멍 가장자리에 붙이고 남은 구멍 메움, ShelterLift는 경유점(`via`)으로 정면 대각선 상승 후 오른쪽 이동.
  - [x] 이동·복귀 중 다른 플레이어 탑승 차단: 둘레 투명 벽 + 들어온 사람 밖으로 이동 + 운반자만 셈(2026-11-03). 실제 멀티 플레이 확인 필요.
- [x] 2구간 추격 불 숨김, 1구간 불만 사용(2026-11-03).
- [x] 추격 불이 나타날 때 6초 동안 작게 시작해 점점 커짐(2026-11-05). 실제 플레이 체감 확인 필요.
- [x] 시작 지면 전체 안전 착지(빠진 메움 바닥 2·이동 장치 발판 등록, 2026-11-05). 테스트는 사용자 확인.
- [x] 트램펄린 들고 낙사하면 시작 지점에 가장 가까운 생존자 발밑 지면으로 이동, 대포 안이거나 발밑 지면이 없으면 그 생존자 2m 뒤(지면 없으면 4m 뒤), 생존자가 없으면 시작 위치(2026-11-05 변경). 실제 멀티 플레이 확인 필요.
- [x] 트램펄린 들고 낙사 외 사망(톱·불 등)이면 생존자 옆 소환 없이 사망 지점에 그대로 둠(2026-11-05). 실제 멀티 플레이 확인 필요.
  - [x] 공중에서 죽으면 그 자리에서 바로 아래 지면까지 떨어짐, 지면이 없으면 시작 위치(2026-11-05). 실제 멀티 플레이 확인 필요.
- [x] 트램펄린은 누군가 손잡이를 잡고 있을 때만 반동(`requireCarrierForBounce` 켬, 2026-11-05). 실제 플레이 확인 필요.
- [x] 전 구간 지상 톱 14개 외형 회전 60 → 720°/s(2026-11-05). 테스트는 사용자 확인.

## (3) 나중
- [ ] 피날레 고도화 — `FinalCannonCourse`, `HumanCannonFinalSaws`, `FinalSawTimeline`. 구체 내용 미정.
  - [x] 최종 대포 4대 무조작 탄도 위 링 12개(상승·정점·하강, 장식·충돌 없음) 배치(2026-11-02).

## 마무리
- [ ] 묶음 완료 시 필요한 검사와 결과 기록
- [ ] 담당자가 공연의 재미·방향·최종 플레이 경험 확인
