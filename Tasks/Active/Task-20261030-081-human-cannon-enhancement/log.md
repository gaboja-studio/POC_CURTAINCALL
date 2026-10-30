# Log

## 2026-10-30 작업 준비
- 현재 브랜치에 Task 생성. 담당자 @lakuase2, 공유 대상 dev. 다음: 담당자가 현재 구현을 조사하고 상세 범위를 정한다.
- `doctor.ps1 -SkipEditor`: FAIL. 외부 에셋 submodule 미초기화, 생성 전 현재 브랜치 Task 부재, 기존 Task ID 077·078·079 중복. Unity CLI 버전 차이, skill links 부재, gh 미로그인 경고. 문서 준비에는 필수적인 차단은 없으나 Unity 작업 시 외부 에셋 준비 상태를 확인해야 한다.
- `check-work.ps1` 실행을 시작했으나 결과를 회수하지 못해 완료 여부 미확인. Unity 검사·자동 테스트는 이번 문서 준비에서 확인하지 않았다.

## 2026-10-30 고도화 항목 수령
- @lakuase2가 항목 8개와 우선순위를 전달. todo에 우선순위별로 정리하고 Unity 에디터에서 씬 계층·이동 장치 코드를 조사해 진입점을 적었다.
- 확인한 사실: 좌측 이동 장치(DepartureTransport)는 이미 1인 탑승 대기 후 자동 출발하며 과승 시 바닥이 열린다. 요청은 과승 시 작동하지 않는 방식으로 바뀌는 것이다. 도착지점 그네는 `CeilingPole1_4`(z=174)다. 낙하 위치 그림자는 아직 구현이 없다.
- 우선순위는 작업 순서가 아니라고 담당자가 확인. 다음: 담당자가 지정한 항목부터 착수. 수평톱 공통 에셋은 외줄타기 Task-074와 겹치므로 수정 전 알린다.

## 2026-10-30 톱 외형 교체·판정 비교
- 인간대포 톱 외형을 `TightropeArtKit/HorizontalSaw.prefab` 인스턴스로 교체. 대상: `GeneralHazards044/GroundSaw*/Visual`(12개), 두 `SawGate/SawVisual/GateSawDisc*`(88슬롯). 기존 `AppliedSaw`·`AppliedGateSaw`(Synty 톱 메시 + Steel)는 삭제. 해저드·관문·슬롯 위치는 바꾸지 않았다.
- 판정 유지 방법: 지상 톱은 기존 규칙(외형 = 판정, 반지름 0.65·두께 0.24)에 맞춰 비균등 확대. 관문은 렌더러 bounds가 판정이므로 얇은 날 2장을 슬롯 앞뒤(±0.25)에 겹쳐 기존 0.6m 두께·43.6m 폭을 유지. 관문 날의 Sparks 파티클은 판정 bounds와 성능 때문에 끔. 지상 톱은 Sparks 유지.
- 판정 비교: 외줄타기 가로/정면 톱 판정 지름 1.0·두께 0.1, 외형 1.2(외형이 0.1m 더 큼). 인간대포 지상 톱 판정 지름 1.3·두께 0.24·외형 = 판정, 중심 높이 0.3(윗면 0.42, 외줄 가로 톱 윗면 0.2). 관문은 원판 판정이 아니라 톱 렌더러 AABB와 몸 상자(+0.15 여유) 겹침.
- 검사: Unity EditMode `HumanCannon` 필터 495/495 PASS. 첫 시도는 외형이 판정과 달라 4건 FAIL → 위 방식으로 수정 후 통과. PlayMode 미실행. 실제 플레이 외형은 사람 확인 필요.
- 담당자 결정: 지상 톱 판정 크기는 외줄타기와 맞추지 않고 현재 값(지름 1.3·두께 0.24)을 유지한다.
- 톱 외곽선이 멀리서 검게 뭉쳐 보이는 문제: `HumanCannonOutline`은 거리와 무관한 2px 고정 외곽선이라 작은 톱니를 덮었다(외줄타기는 5→14m 선형 감쇠). `HumanCannonOutline`에 선택형 거리 감쇠(`fadeWithDistance`, 기본 꺼짐)를 추가하고 지상 톱 12·관문 톱벽 2에만 켰다. 봉·트램폴린은 기존 그대로.
- 검사: EditMode `HumanCannon` 필터 500/500 PASS(감쇠 테스트 5건 추가). 플레이 화면에서의 외곽선은 아직 사람 확인 전.
- 관문 톱 재배치(담당자 요청): 쌍 배치(틈 0.21/0.40 교대)를 없애고 88장을 틈 0.3m 등간격으로 배치(날 두께 0.194, 간격 0.494). 슬롯 44개 구조는 유지. 톱벽 전체 폭은 43.6→43.19m로 양 끝이 0.2m씩 줄어 관문 판정 범위도 그만큼 줄었다. EditMode `HumanCannon` 500/500 PASS.
- 관문 톱벽 눈부심(담당자 요청): 외줄타기 공용 `HorizontalSaw.mat`(금속 0.85·광택 맵·반사 켜짐)은 두고, 사본 `HumanCannon/Materials/human-cannon-gate-saw-matte.mat`(광택 맵 제거, 금속 0.2, 매끄러움 0.1, 하이라이트·환경 반사 끔)을 만들어 두 관문 날 176개 렌더러에만 적용. 지상 톱은 기존 재질 유지.
- 1구간 첫 톱벽 앞 지상 톱 3개 재배치(담당자 스케치): `HumanCannonHazard.authoredPath` 추가(공용 30° 대각선·밀도 미적용, 항상 켜짐). 1AExtra z41 왼45.6→오36.4, 1D z32.5 거의 수평, 1C z23.5 왼29→오18. 관련 테스트(30° 고정, 밀도 개수) 갱신.
- 미해결: 1C 레일이 좌측 이동 장치 도착 발판(x-12~-6, z18~26) 위 z≈25.6을 지나 도착 직후 플레이어를 벨 수 있다. `HumanCannonWideCourseTests.GroundSaws_HaveDistinctRails...`(도착 발판 앞 2m 확보) FAIL. 담당자에게 조정안 확인 중.
- 담당자 결정으로 10° 무교차 배치로 확정: 1C z28(왼32.2→오23.8), 1D z34 수평, 1AExtra z40.1(왼44.3→오35.9). 도착 발판 앞 2m·톱벽 여유 확보. 15°는 톱벽(z47)을 넘어 불가. 검사: EditMode `HumanCannon` 499/500 후 테스트 수정분 재컴파일, `HumanCannonWideCourseTests` 6/6 PASS.
- 좌측 이동 장치 도착 위치(담당자 요청): `DepartureTransport/Destination` z 22→20. 발판 뒤쪽 끝(z16)이 `Floor_0_0` 뒤쪽 끝과 맞는다. 발판 앞(z24~26, x-12~-6)에 2m 빈칸이 생겨 `Floor_1_2`(z26)까지 비어 있다. EditMode `HumanCannon` 500/500 PASS.
- 도착 발판 앞 빈칸 메움: `Floor_1_2`를 z26~35 → z24~35로 늘림(콘크리트 외형은 루트 크기를 따라감). 레이캐스트로 빈칸 없음 확인.
- 검사: EditMode `HumanCannon` 500/500 PASS.
- 시작 바닥 확장(담당자 요청, 계단 덮어도 됨): `Ground/Floor_0_0`·`Floor_0_1` 바깥쪽을 x -15 → -23(계단 바깥 끝)으로 늘림. 높이 3.5~4, z -11~7 그대로. EditMode `HumanCannon` 500/500 PASS.
- 주의: 아래 `ReturnStairs`(구덩이 복귀 계단)는 남아 있으나 위가 바닥 판(아래면 y3.5)으로 막혀, 계단 윗부분(디딤 높이 약 2m 이상, z≈-7 이후)에서 머리가 걸려 시작 바닥으로 올라올 수 없다. 좌측 이동 장치 과승 시 바닥 개방(구덩이 낙하)이 남아 있는 동안 복귀 경로가 없다. 과승 시 미작동으로 바꾸는 (1) 항목과 함께 정리 필요.
- 시작 바닥 폭을 1구간 바닥(x -22~22)에 맞춤(담당자 재요청): `Floor_0_0`·`Floor_0_1` 왼쪽 -23→-22, `Floor_2_0`·`Floor_2_1` 오른쪽 15→22. 계단 위 x -23~-22에 1m 틈이 다시 열림. EditMode `HumanCannon` 결과는 아래.
- 검사: EditMode `HumanCannon` 500/500 PASS.
- 시작 지점 아래 구간·계단 삭제(담당자 요청): `SplitRouteCourse/LiftSafetyLesson` 전체(RecoveryFloor·StairBottom·StairTop·ReturnStairs 40칸·RecoveryDirection) 삭제. 씬 내 다른 참조 없음. 과승 낙하자는 y -10 아래에서 `HumanCannonRouteSystem` 복귀 처리. 테스트 갱신: 낙하 공간을 fallHeight까지 검사, 안전 바닥·계단 테스트를 "아래 구간 없음" 테스트로 교체, 아트 바닥 수 81→38. `HumanCannonLiftTrapBuilder`(에디터 도구)는 남아 있어 다시 실행하면 구간이 재생성된다.
- 검사: 콜라이더 수 90→47 갱신 후 EditMode `HumanCannon` 결과는 다음 줄.
- 검사: EditMode `HumanCannon` 500/500 PASS.
- 엘리베이터 3대(DepartureTransport·ShelterLift·ShelterDeparture) 좌우·앞뒤 절반(담당자 요청, "일단"): 발판 6×8 → 3×4, halfSize (3,4)→(1.5,2). 중심·출발·도착 위치 유지.
- 영향(미해결): ① 모든 출발·도착 자리에서 기존 6×8 구멍이 남아 발판 둘레(좌우 1.5m·앞뒤 2m)가 빈칸. ② 트램폴린 적재 반경 2.48m > 발판 반폭 1.5m라 DepartureTransport는 트램폴린을 실을 수 없어 출발 조건(사람+트램폴린)을 만족하지 못함. ③ EditMode 498/500: `ShelterAccess_ConnectsFirstCourseToLowerLiftAndUpperLiftToDeck`, `ShelterDirectJump_CannotReachLowerLiftOrAccessEvenWithCapsuleAndSkinAllowance` FAIL(쉼터 리프트 접근로와 발판 사이 2m 틈).
- 축소 리프트 빈틈 메움(담당자 요청): 출발은 앞쪽 끝, 도착은 뒤쪽 끝을 구멍 가장자리에 붙이도록 이동 — DepartureTransport (-9,4,5)→(-9,0,18)[도착 뒤쪽 끝 z16 유지], ShelterDeparture (-9,6,82)→(-9,6,97), ShelterLift (-30.5,0,68)→(-23.5,6,78)[접근로 끝 z66·갑판 x-22에 붙임]. 남은 구멍은 이웃 바닥 복제 `ElevatorFill_*` 12장으로 메움(Ground·FirstCourseFloor·ShelterDeck·SecondCourseFloor 각 3장). 이동 방향 앞쪽(공중 구간)은 원래 설계대로 비어 있음. `HumanCannonGroundMarkers.courseFloors`에 새 코스 바닥 6개 추가. 테스트 갱신: 쉼터 리프트 좌표, 바닥 면 38→50, 콜라이더 47→59. EditMode `HumanCannon` 500/500 PASS. 트램폴린(반경 2.48)은 3×4 발판에 여전히 실을 수 없음.
- 정정: 이동 장치 출발 조건(`HumanCannonRouteSystem.CanStart`)은 탑승자가 트램폴린을 들고 있고 발이 발판 위에 있는지만 본다. 트램폴린 크기 검사(`HumanCannonTransport.ContainsLoad`)는 테스트에서만 쓰여 실제 출발을 막지 않는다. 앞선 '트램폴린을 실을 수 없어 출발 불가' 기록은 틀렸다.
- 이동 장치 3대 좌우 폭 3→5.5m(담당자 요청, 트램폴린 지름 약 4.95m 고려), 앞뒤 4m 유지. halfSize (2.75,2). 구멍(x-12~-6)은 유지하고 메움 바닥을 새 폭에 맞춤(옆 0.25m, 앞뒤 x-11.75~-6.25). ShelterLift는 +x 끝 고정으로 (-31.75,0,68)→(-24.75,6,78). 테스트 좌표 갱신. EditMode `HumanCannon` 500/500 PASS.
- 쉼터 리프트 경로 꺾기(담당자 요청): `HumanCannonTransport`에 선택 경유점 `via` 추가(두 구간 길이 비례, 같은 속도). ShelterLift/Via (-31.75,6,78): 정면 위 대각선으로 갑판 높이까지 오른 뒤 오른쪽(+x)으로 이동. 경로가 13.6→18.66m로 길어져 이동 시간 3.00→4.12초(속도 유지). 경로 상 걸리는 고정 물체 없음. 테스트에 경로 모양 검사 추가.
- 검사: EditMode `HumanCannon` 500/500 PASS.
- 좌측 이동 장치 위치 이동(담당자 요청): 1구간 바닥 왼쪽 끝 → 구멍 6m. 발판 중심 x -9 → -13.25(구멍 x -16~-10.5). 도착 쪽: FirstCourseFloor/Floor_0_0 x-22~-16 z16~20, Floor_0_1 x-22~-12 z20~26, ElevatorFill_Front x-12~-10.5 z20~24, ElevatorFill_Right x-10.5~-6 z16~24, ElevatorFill_Left 삭제. 출발 쪽: Ground/Floor_0_1 x-22~-16, ElevatorFill_Back x-16~-10.5 z-1~3, ElevatorFill_Right x-10.5~-6, ElevatorFill_Left 삭제. 지면 표식 목록 정리(20). 이동 경로 걸림 없음. 테스트 바닥 수 50→48, 콜라이더 59→57. EditMode `HumanCannon` 500/500 PASS.
- 1구간 지상 톱 3개를 3.5m 앞으로(담당자 요청: 첫 톱을 20m 거리선 z24.5에): 1C z24.5, 1D z30.5, 1AExtra z36.6. 간격·10° 각도 유지. 도착 발판 앞(z20)과 아래 톱 레일 사이 5.7m.
- 검사: EditMode `HumanCannon` 500/500 PASS.
- 1구간 지상 톱 재배치(담당자 요청): 2번 1D를 27m 거리선 z31.5, 3번 1AExtra를 z37.6(2번과 기존 간격 유지). 톱날 가장자리 최소 틈 1-2 1.47m, 2-3 0.57m, 3번-톱벽 4.22m. EditMode `HumanCannon` 500/500 PASS.
- 1번 톱(1C)만 z24.5→25.4: 1-2 틈을 2-3과 같은 0.57m로, 엘리베이터 도착 발판 앞과 6.6m. 2·3번 유지. EditMode 결과는 다음 줄.
- 검사: EditMode `HumanCannon` 500/500 PASS.
- 1번 톱(1C)을 18m 거리선 z22.5로(담당자 요청). 왼28.7→오18.3... 실제 왼26.73→오18.27, 1-2 틈 3.47m, 엘리베이터 도착 발판 앞과 3.70m(중심-발판 앞 2.5m, 규칙 2m 이상 충족). EditMode `HumanCannon` 500/500 PASS.
- 3번 톱(1AExtra) z37.6→39.6(담당자 요청: 톱벽 쪽으로 조금 더). 2-3 틈 2.57m, 톱벽과 2.22m.
- 검사: EditMode `HumanCannon` 500/500 PASS.

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 커밋 전 검사: `check-work.ps1` 컴파일 PASS, 형식/규칙 PASS, 연결 PASS, 테스트 SKIPPED(스크립트 기본값). 별도로 EditMode `HumanCannon` 500/500 PASS. `ProjectSettings/Packages/com.unity.services.vivox/Settings.json` 자동 변경은 커밋에서 제외.
