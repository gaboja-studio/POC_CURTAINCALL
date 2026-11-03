# 기존 공연 시스템 재사용 수정·입력 검증

2026-11-03 최신 수정본. 이전 verification.md의 리허설 화면과 상태 배치 검사는 현재 입력 검증을 대신하지 않는다.

세 단계를 같은 코스로 만들지 않고 [기획 원문](https://hrjoo122770.atlassian.net/wiki/pages/viewpage.action?pageId=32702474)의 서로 다른 구조를 유지했다. 기존 TightropeCoursePrepared·PerformanceHud·균형바·인간 그네·릴레이 시스템을 사용한다.

| 단계 | 유지한 구조 | 현재 길이 |
| --- | --- | --- |
| 1 | 두 줄·중간 발판·옆줄 점프 | 38m |
| 2-1 | 그네 1개 | 32m |
| 2-2 | 연속 그네 2개·공중 F 잡기 | 38m |
| 2-3 | 위쪽 트리거·아래쪽 끊어진 줄 | 32m |
| 3 | 나란한 네 줄·각 줄 그네·짝 트리거 | 34m |

길이는 기획서에 확정 수치가 없어 기존보다 늘린 임시 조정값이다.

PlayMode W6_Rehearsal_PerformanceInputHudAndFallRecovery: 1/1 PASS, 48.1초. 실제 키 입력으로 W 진입과 A/D 균형 보정, E+Space 옆줄 이동, 추락 후 복귀·재진입, F 잡기·W/S 반동·Space 놓기·공중 W 전진과 착지, 연속 그네 공중 F 잡기, 2-3/3단계 F 연결·Space 해제를 확인했다. Q 방향은 이번 검사에서 별도로 누르지 않았다.

필수 check-work: 안전성·문서/규칙·Unity 컴파일 PASS(exit 0). 이 명령의 전체 자동 테스트는 선택하지 않았으며, 위의 실제 입력 검사 결과와 구분한다.

각 시나리오 시작 위치와 단계는 검사에서 준비했다. 강제 Fallen·잡기 서버 호출로 통과시키지 않았다. 단독 호스트 검사이며, 2인 협동 전 코스 완주와 조작감은 미확인이다.

수정한 실제 버그: 줄에서 떨어져 바닥의 일반 이동으로 전환되면 추락 검사가 빠져 진행이 막혔다. 연습 모드 실행 중 기존 깊이 판정을 유지해 출발 지점으로 복귀하고 다시 줄에 오를 수 있게 했다.

테스트 진입: WaitingRoomBase.unity → Play → 개발 시작 지점(F8 또는 Cmd+Shift+U) → 1일차 Rehearsal. 기존 실행 중 상태가 있으면 Stop/Play로 새로 시작한다.

퀘스트·단계 설명·패널 편집은 [편집 안내](quest-editing.md)를 따른다. WaitingRoomTexts.txt의 rehearsal.stage.*, rehearsal.quest.*, first-day.quest.*, first-day.panel.*를 수정하고 Play를 다시 시작한다.

1단계 줄 진입·기존 균형바

![1단계 줄 진입·기존 균형바](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Rehearsal-reuse/01-rope-and-balance.png)

1단계 E+Space 옆줄 점프

![1단계 E+Space 옆줄 점프](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Rehearsal-reuse/02-lane-jump.png)

실제 추락 후 복귀·W 재진입

![실제 추락 후 복귀·W 재진입](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Rehearsal-reuse/03-retry-input.png)

2-1 기존 그네 F 잡기

![2-1 기존 그네 F 잡기](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Rehearsal-reuse/04-swing-grab.png)

2-1 Space 놓기·W 전진 후 착지

![2-1 Space 놓기·W 전진 후 착지](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Rehearsal-reuse/05-swing-land.png)

2-2 공중 F로 두 번째 그네 잡기

![2-2 공중 F로 두 번째 그네 잡기](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Rehearsal-reuse/06-chain-f-grab.png)

2-2 두 번째 그네에서 착지

![2-2 두 번째 그네에서 착지](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Rehearsal-reuse/06b-chain-land.png)

2-3 F로 트리거 연결

![2-3 F로 트리거 연결](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Rehearsal-reuse/07-stage2-3-held.png)

2-3 Space로 해제

![2-3 Space로 해제](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Rehearsal-reuse/08-stage2-3-released.png)

3단계 짝 줄 트리거 연결

![3단계 짝 줄 트리거 연결](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Rehearsal-reuse/09-stage3-held.png)

3단계 트리거 해제

![3단계 트리거 해제](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Rehearsal-reuse/10-stage3-released.png)

