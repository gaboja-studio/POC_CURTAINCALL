# 자동 검사 실패와 행동 조합 감사

## 2026-11-13 수정 후 관련 검사

- 안전성·문서/규칙·컴파일·GameSettings 연결 통과. 공통 601/601, 입력·운반 133/133(서로 중복 포함).
- 목마 2·3·4인 상태 인계와 메시지 순서 6건, 늦은 목마 재연결 요청 차단, F 초기화/장치 리셋, 사지 조각 아래 기립·점프, Space 발사, 트램펄린 실제 물리 통과를 포함한다.
- 실제 PlayMode·서로 다른 PC의 멀티플레이는 사용자 확인. 아래 전체 감사 18개와 이전 보류 목록은 별도 잔여 기록이며 이번 관련 검사 통과로 해결됐다고 간주하지 않는다.

## 2026-11-13 수정 전 전체 감사

- DEV 836dd382 포함, HEAD b81cff65 / 실제 게임 코드·설정 수정 및 제출 없음.
- 전체 EditMode: **2,268개 / 통과 2,250개 / 실패 18개**. 실제 다른 PC의 다인 플레이 완료를 의미하지 않는다.
- 전체 XML: `.harness/local/nov12-all-editmode-results.xml`. 도메인 재로드 뒤 Pipeline JSON에는 174개 상세만 남으므로 실패 전체는 XML로 집계했다.
- 안전성·문서·컴파일·GameSettings 연결은 통과. 전체 검사 중 자동 생성된 폰트 atlas 때문에 최종 파일 지문 확인은 실패했으며 해당 생성분은 원복했다.

### 추가로 재현한 게임 코드 문제 — 후속 수정 반영

| 조합·위치 | 재현 결과 | 원인 |
| --- | --- | --- |
| 인간대포 연습 · 목마 위 사람 · 종료/포기 복귀 | 복귀 좌표 (100,0,100) 지정 뒤 Follow가 동료 머리 위 (0,1.5,0)로 다시 이동 · 목마 연결 유지 | CaptureEndingMover가 Pilot만 해제 · 목마의 임시 Simulated=false를 저장 · 생존자의 연결 해제 누락 |
| 공통 F · 누른 상태에서 메뉴 입력 초기화 · 메뉴 해제 직후 다시 누름 | 새 F 제스처 Consume=false | Forget이 Consumed=true로만 바꾸고 이전 Held 유지 |
| 공통 F · 키보드 상태 초기화 | 실제 해제 이벤트를 보내지 않은 ResetDevice에서 tap=true | canceled 콜백이 장치 초기화를 정상 키 해제로 처리 · 실제 Alt+Tab 조작은 미실행 |
| 공통 숙이기 · 머리 위 잘린 사지 조각 | TryStand=false · 조각 충돌체를 끄면 true | HasHeadroom이 DismemberedLimb를 제외하지 않음 · 앉기 유지 중 점프도 제한됨 |

- 후속 수정: 생존 참가자 목마 연결 해제 및 복귀 순서 대기, F 초기화 시 제스처 전체 폐기, 장치 reset의 가짜 해제 폐기, 기립 검사에서 사지 제외. 최종 공통 회귀 **601/601 통과**, 입력·운반 추가 **133/133 통과**. 신규 트램펄린 2개는 편집 모드 준비 방식을 PreviewScene으로 수정한 뒤 실제 물리 통과까지 확인.
- 추가 요청: 공통 Action(Space) 대포 발사, 트램펄린 본체·자식 충돌체와 사지 간 실제 물리 접촉 제외.
- 합성 재현 원본: `.harness/local/nov12-extra-combination-probe.cs` / 결과 `.json`. 씬이나 GameSettings 저장 없이 임시 객체로 실행했다.
- 이동·점프·달리기·숙이기, 목마·외줄·봉·대포, 운반·사용·치료·복귀를 코드와 기존 자동 검사에서 대조했다. 각 조합의 모든 좌표·프레임·네트워크 지연을 전수 실행한 것은 아니다.

### 전체 검사 실패 18개 분류

| 검사 묶음 | 수 | 확인 내용 |
| --- | ---: | --- |
| EscapeSwingDriveTests의 30/60/120fps | 3 | 공통 봉 2초 반동 기대값 >40° · 현재 약37.58~37.60° · 옛 고정 기대값과 현재 공통 수치 불일치, 탈출로 씬은 수정 안 함 |
| TightropeInjuryFeedbackTests | 4 | 검사에서 PlayerInteraction을 생성하지 않고 ReachMultiplier 조회 · 검사 코드 34행 NullReferenceException · 점프 높이/이동 배율 검사는 그 앞에서 통과 |
| DifficultySelectionTests | 5 | 첫 공연 뒤 튜토리얼 완료 처리를 생략하고 집결 승인 요구 · 현행 IsGuideRestWaiting 조건에서 거절 |
| RehearsalArrivalTests 안내 문구 | 1 | 기대 ‘발판에서는’ · 실제 ‘발판 위에서는’ |
| UnifiedWaitingRoomSceneTests | 1 | 두 대기실 추격자 배치 인덱스6 크기가 Small/Medium으로 다름 · 기존 미해결 배치 차이 |
| HumanoidValidationTests 절단 다리 물리 | 1 | 전체 실행에서 상승/하강 검증 실패 · **단독 재실행 1/1 통과** · 제작 검증용 에셋이며 실행 순서/환경 영향은 미확정 |
| AudienceReactionTests | 1 | 기대 보상50 · 현재 완주 보너스100 포함150 · 기대값에 완주 보너스 누락 |
| WaitingRoomTextTests | 2 | 문구 키 누락7개·기본값 충돌2개, 사용처 미검출 키 · 동적 문자열 참조가 있어 미검출을 전부 미사용으로 확정하지 않음 |

## 이전 검사 기록: 당시 실패 14개

- 마지막 전체 EditMode 검사: 1,860개 / 통과 1,845개 / 실패 15개
- 15개 중 외줄 매달림 검사 좌표 오류 1개 수정 → 외줄 집중 검사 69/69 통과
- 아래 14개: 미해결 · 이번 저장·제출에서 추가 수정 없음
- 자동 검사 실패 목록 · 실제 플레이 버그 14개로 확정한 목록 아님
- 2~10번: 현재 튜토리얼 완료 전 대기 규칙과 검사 준비·기대값 불일치
- 이전 작업본 검사에서도 동일 실패 · 깨끗한 dev에서 별도 재현하지 않음

| 검사 항목 | 관련 씬·시스템 | 기대값 | 실제 결과·원인 |
| --- | --- | --- | --- |
| • 1. 대포 코스 바닥 톱 높이 | • 인간대포 공연 씬<br>• HumanCannon.unity · GroundSaw1C | • 톱 윗면: 0.4m 미만 | • 실제 톱 윗면: 0.42m<br>• 두 다리 손실 시 점프 높이 0.4m보다 높음<br>• 톱 높이 낮추기: 사용자 요청으로 보류 |
| • 2. 클리어 단계 다음까지만 선택 | • 씬 미실행 · RunState 단위 검사<br>• 대기실 1일 차 → 다음 공연 | • 공연장 집결 승인: true<br>• 이후 난이도 상한 검사 | • 집결 승인: false<br>• 튜토리얼 완료 처리 없는 검사 준비 단계<br>• 난이도 상한 검사 도달 전 실패 |
| • 3. 대포 발견·새 런 초기화 | • 씬 미실행 · RunState 단위 검사<br>• 대기실 1일 차 → 대포 해금 | • 공연장 집결 승인: true<br>• 이후 대포 발견·초기화 검사 | • 집결 승인: false<br>• 튜토리얼 완료 처리 누락<br>• 대포 해금·초기화 검사 도달 전 실패 |
| • 4. 새 런의 클리어 단계 초기화 | • 씬 미실행 · RunState 단위 검사<br>• 대기실 1일 차 → 런 초기화 | • 공연장 집결 승인: true<br>• 이후 클리어 이력 초기화 검사 | • 집결 승인: false<br>• 튜토리얼 완료 처리 누락<br>• 초기화 검사 도달 전 실패 |
| • 5. 다음 공연 난이도만 조절 | • 씬 미실행 · RunState 단위 검사<br>• 대기실 쉬는 시간 · 난이도 선택 | • 공연장 집결 승인: true<br>• 이후 다음 공연 난이도 변경 검사 | • 집결 승인: false<br>• 튜토리얼 완료 처리 누락<br>• 난이도 변경 검사 도달 전 실패 |
| • 6. 미선택 난이도 조절·1일 차 제한 | • 씬 미실행 · RunState 단위 검사<br>• 대기실 1일 차 이후 전환 | • 공연장 집결 승인: true<br>• 이후 난이도 미선택·날짜 조건 검사 | • 집결 승인: false<br>• 튜토리얼 완료 처리 누락<br>• 후속 날짜·난이도 조건 검사 도달 전 실패 |
| • 7. 쉬는 시간 제한 시간 시작 | • 씬 미실행 · RunState 단위 검사<br>• 대기실 1일 차 · Guide 쉬는 시간 | • 333초 타이머: Running | • 실제 상태: WaitingForExit<br>• 튜토리얼 완료 전 대기 상태<br>• 검사 기대값: 입장 즉시 카운트다운 |
| • 8. 종료 전 경보·시간 만료 | • 씬 미실행 · RunState 단위 검사<br>• 대기실 1일 차 · 제한 100초 / 경보 10초 전 | • 90초 경과 처리: true<br>• 이후 경보·시간 만료 검사 | • 90초 경과 처리: false<br>• 튜토리얼 완료 전 카운트다운 대기<br>• 경보·만료 후속 검사 도달 전 실패 |
| • 9. 무제한 쉬는 시간·탈출 | • 씬 미실행 · RunState 단위 검사<br>• 대기실 1일 차 · 무제한 쉬는 시간 | • 타이머 상태: Unlimited | • 실제 상태: WaitingForExit<br>• 튜토리얼 완료 전 대기 상태<br>• 탈출 검사 도달 전 실패 |
| • 10. 무제한 시간의 경보·집결 | • 씬 미실행 · RunState 단위 검사<br>• 대기실 1일 차 · 무제한 쉬는 시간 | • 타이머 상태: Unlimited<br>• 이후 경보 없음·집결 검사 | • 실제 상태: WaitingForExit<br>• 튜토리얼 완료 전 대기 상태<br>• 경보·집결 후속 검사 도달 전 실패 |
| • 11. 통합 대기실 추격자 크기 | • 원본: WaitingRoomPursuit.unity<br>• 통합: WaitingRoom.unity<br>• 추격자 배치 목록 · 인덱스 6 | • 통합 씬 배치 = 원본 씬 배치<br>• 크기: Small(0) | • 통합 씬 크기: Medium(1)<br>• 원본과 크기 설정 불일치<br>• 첫 불일치에서 검사 중단 · 나머지 배치 일치 미확정 |
| • 12. 생존자 환호 보상 정산 | • 씬 미실행 · 보상 모델 단위 검사<br>• 외줄 공연 성공 상황 | • 정산 보상: 25 × 2 = 50 | • 실제 정산: 150<br>• 환호 50 + 완주 보너스 100<br>• 검사 기대값에 현재 완주 보너스 미반영 |
| • 13. 대기실 문구 누락·불일치 | • 씬 미실행 · 대기실 문구 파일 검사<br>• 탐색 · 인간대포 연습 · 외줄 안내 | • 코드 문구 키: 파일에 모두 존재<br>• 같은 키의 기본 문구: 동일 | • 누락 2개: explore.locked-before-tutorial / cannon-rehearsal.vote-body<br>• 기본 문구 불일치 2개: rehearsal.lane-guide-title / rehearsal.lane-guide<br>• UI 문구 정리 범위 · 이번 작업에서 수정 안 함 |
| • 14. 대기실 문구 사용처 검사 | • 씬 미실행 · 대기실 문구 파일 검사<br>• 이전 튜토리얼 · 연습 · 내레이션 문구 | • 사용처 미검출 문구 키: 0개 | • 검사에서 사용처 미검출: 121개<br>• 예: rehearsal.quest.* / first-day.quest.* / cannon-rehearsal.*<br>• 실제 미사용 여부: 동적 참조 포함 별도 확인 필요<br>• 이번 작업에서 삭제 안 함 |

## 검사 이름

- **1. 대포 코스 바닥 톱 높이**: `HumanCannonWideCourseTests.GroundSaws_HaveDistinctRailsAndSpacedRoutesAtFourSecondCycles`
- **2. 클리어 단계 다음까지만 선택**: `DifficultySelectionTests.ChosenTierIsLimitedToNextAfterCleared`
- **3. 대포 발견·새 런 초기화**: `DifficultySelectionTests.HumanCannonNeedsDiscoveryAndResetsWithRun`
- **4. 새 런의 클리어 단계 초기화**: `DifficultySelectionTests.NewRunForgetsClearedTiers`
- **5. 다음 공연 난이도만 조절**: `DifficultySelectionTests.RestTimeAdjustsOnlyNextPerformanceTier`
- **6. 미선택 난이도 조절·1일 차 제한**: `DifficultySelectionTests.RestTimeAdjustWorksWithoutChosenTiersAndNotOnDayOne`
- **7. 쉬는 시간 제한 시간 시작**: `RunTimeLimitTests.EachRestUsesItsOwnLimitOnlyOnEntry(1,Between,Guide,333.0f)`
- **8. 종료 전 경보·시간 만료**: `RunTimeLimitTests.FiniteRestStillRaisesAlarmAndExpiresBeforePerformance`
- **9. 무제한 쉬는 시간·탈출**: `RunTimeLimitTests.UnlimitedRestCanEscapeAndDebugRequiresFiniteValue`
- **10. 무제한 시간의 경보·집결**: `RunTimeLimitTests.UnlimitedRestNeverExpiresOrAlarmsButCanGather(1,Between,Guide)`
- **11. 통합 대기실 추격자 크기**: `UnifiedWaitingRoomSceneTests.UnifiedWaitingRoom_PreservesPursuitAuthoringAndSceneReferences`
- **12. 생존자 환호 보상 정산**: `AudienceReactionTests.CutIsAwardedOnceAndSurvivorDoublesCheer`
- **13. 대기실 문구 누락·불일치**: `WaitingRoomTextTests.ShippedFileCoversEveryCodeKeyWithSameNames`
- **14. 대기실 문구 사용처 검사**: `WaitingRoomTextTests.ShippedFileHasNoUnusedKeys`
