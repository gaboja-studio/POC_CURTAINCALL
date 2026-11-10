# 남은 자동 검사 실패 14개

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
