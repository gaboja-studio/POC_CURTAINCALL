# 11/11 비플레이 확대 검사

- 사용자 최신 지시: 플레이는 사용자 담당, 에이전트는 코드·컴파일·에셋·EditMode 검사. 탈출로와 기존 보류14개 전체 수정은 제외.
- 확대1,738개: 통과1,724·실패14. 후속141개: 통과140·실패1(재연습 운송 시작점 복사). 최종 대포 관련76개: 전부 통과.
- 첫 실행은 EditMode에 포함된 StartConnectionTests가 내부 EnterPlayMode를 호출해 결과 콜백이 끊김. 확인 즉시 취소·Play 종료. Explicit·탈출로·EscapeSwingDrive·Play 전환 검사를 제외한1,738개를 새로 실행. 실제 플레이 완료로 계산하지 않음.
- 증거: `.harness/local/trampoline-review-20261111/`의 nonplay-final.json, nonplay-followup.json, cannon-input-before.json, cannon-input-after.json, saved-sources.json.

## 이번에 해결

| 장소·기능 | 확인 전 | 확인 후 |
| --- | --- | --- |
| 기본 대기실 천장 | 몸 충돌46곳 누락, 보정 도구 재실행 시 씬 변경 | 물리 천장 저장·재적용 추가0개·양면 카메라 차단 검사 통과 |
| 인간대포 재연습 원본 | DEV 변경 전 발사속도7.2243333·운송 시작점4m 잔류 | 공연 원본7.0704784·5m와 일치, 원본 연결3검사 통과 |
| 튜토리얼 도착벽 검사 | 도착자를 반드시 막아야 한다는 옛 기대값 | 공통 설정 꺼짐/켜짐2조건 검사, 꺼짐에서 되돌아가도 이수 기록 유지 |
| 첫 목마 그네 | 배치 변경의 범위 검사 없음 | 설정된 협동 연습 첫 봉만0.5m 낮춤, 다른 봉·일반 배치·낮은 봉 연습 유지3검사 통과 |
| 공통 입력·외형·출발대 검사 | 키14개·손잡이 자식 메시·대포 출발대4m 기대 | V 포함15개·분리된 손잡이 피드백 연결·현재 DEV 출발대5m 검사 |
| 런 포기 검사 | 튜토리얼 미완료로 탈출 진입 실패한 상태에서 후속 검사 | 튜토리얼 완료 및 실제 상태 진입 확인 후 포기 거절 검사. 게임 규칙 변경 없음 |
| 대포 비행 A/D | 발판 복귀 응답 대기 중 좌우 입력1초 → 보정0m 재현4개 | 비행 입력을 발판 상태보다 먼저 처리. 동일 조건 포함10개·대포 연관 전체76개 통과 |

- 대포 공중 조작 속도3m/s·반응시간0.1초 유지. 사용자 실제 세션 로그는 없어 위 복귀 대기가 해당 보고의 원인이었다고 단정하지 않음.
- 실제 다른 PC의2·3·4인 패킷 전달, 감각, 코스 완주는 미검증. 의료 스택 검사는 합성 상태순서 검증.
- 일반 낙하5m 이상15m 미만 부상·15m 이상 사망. 대기실은 승인된 SafeFallLandings로 착지 피해 면제. 월드Y -15 즉사·위험물·최초 튜토리얼 바닥 복귀는 별도 유지.

## 남은 실패10개 — 기존 보류 항목

1. HumanCannonWideCourseTests.GroundSaws_HaveDistinctRailsAndSpacedRoutesAtFourSecondCycles: GroundSaw1C 윗면0.42m, 검사 기준0.4m 미만.
2. DifficultySelectionTests.ChosenTierIsLimitedToNextAfterCleared: 준비 단계의 튜토리얼 완료 누락으로 집결 거절.
3. DifficultySelectionTests.HumanCannonNeedsDiscoveryAndResetsWithRun: 같은 준비 문제.
4. DifficultySelectionTests.NewRunForgetsClearedTiers: 같은 준비 문제.
5. DifficultySelectionTests.RestTimeAdjustsOnlyNextPerformanceTier: 같은 준비 문제.
6. DifficultySelectionTests.RestTimeAdjustWorksWithoutChosenTiersAndNotOnDayOne: 같은 준비 문제.
7. UnifiedWaitingRoomSceneTests.UnifiedWaitingRoom_PreservesPursuitAuthoringAndSceneReferences: 통합 씬과 원본 추격자 크기 불일치.
8. AudienceReactionTests.CutIsAwardedOnceAndSurvivorDoublesCheer: 기대50, 실제 완주 보너스 포함150.
9. WaitingRoomTextTests.ShippedFileCoversEveryCodeKeyWithSameNames: 문구 누락7개·기본 문구 불일치2개.
10. WaitingRoomTextTests.ShippedFileHasNoUnusedKeys: 동적 참조 등을 구분하지 못하는 기존 사용처 검사 실패. 삭제하지 않음.

기존14개 기록을 이번10개로 소급 덮어쓰지 않는다. 이전 RunTimeLimit4개는 현재 DEV에서 통과했다. 이번에 실패하지 않았다는 사실과 과거 결과를 구분한다.
