> 사용자 정정으로 철회된 이전 제작본 기록입니다. 공연 퀘스트·중간 발판·전용 소품은 현재 승인 범위가 아닙니다.

# 전체 코스와 밤·다음 날 전환 검증

2026-11-03 최신 캡처. 리허설은 대기실 연습실에 있고, 공연 퀘스트 촬영 때만 실제 Tightrope 공연 씬을 열었다.

## 확인 결과
- 전체 코스 촬영: W6_Rehearsal_WholeCourseCaptures 1/1 PASS(57.3초). 1단계·2단계 세 소구간·3단계를 각각 촬영했다.
- 후속 연결: W6_FirstDay_GuideSecondPerformanceAndNightQuests 1/1 PASS(87.2초). 공연 사이 안내→집결→두 번째 공연 패널→EndPerformance 공개 신호→밤 일정→확정→취침 준비→NightBed.Interact→2일차 RestTime·침대 카메라 해제까지 확인했다. 실제 툴팁 IsVisible과 TMP font도 검사한다.
- 첫 공연 패널은 수정 후 앞선 확장 검사에서 실제 표시·폰트 검사를 통과하고 확보했다. 그 실행 전체는 이후 대기실 로딩의 35초 제한에서 실패했으므로 전체 PASS로 세지 않는다. 이미 확인한 첫 공연을 반복하지 않고 마지막 검사는 Between부터 좁혔다.
- 전체 코스 협동 완주·공연의 실제 조작 완주 검사가 아니다. 시작 단계/집결 위치와 일정 확정은 검사에서 준비하며, 취침은 기존 침대 상호작용 함수를 사용한다.

## 밤·공연 퀘스트 표시 실패 수정
WaitingRoomRightPanel은 씬을 넘어 유지되지만 임시 폰트의 소유자 UiRuntimeFontFallback은 공용 UI 씬과 함께 삭제된다. 파괴된 Unity 오브젝트와 null을 같은 것으로 비교해 소유자 변경을 놓치면 삭제된 폰트를 계속 쓴다. 실제 재현에서 루트와 뷰가 활성이고 캐릭터가 Normal·커튼 해제·전환 종료인데 TMP 폰트 두 개가 NULL이었다.

소유자 변경을 관리 참조 동일성으로 비교해 패널을 재생성하고, 대기실 UI가 없는 공연에서는 기존 CommonInteractionUI 폰트 보완을 사용하도록 수정했다. 관련 지식은 [폰트 소유자 함정](../../../Harness/Project/Pitfalls/waiting-room-panel-font-owner.md).

별도 검사 문제도 수정했다. 캠페인 Performance 직후 실제 공간은 WaitingRoom, IsTransitioning=true인데 커튼은 아직 false인 상태가 추적에 남았다. 이전 대기는 이 false를 완료로 오인했다. 현재는 전환 종료·커튼 해제·해당 퀘스트의 안정 표시를 함께 확인하며 기존 Launch와 같은 75초 로딩 상한 안에서 준비되는 즉시 끝난다. [실제 상태 추적](quest-transition-trace.txt).

필수 check-work: 안전성·문서/규칙·컴파일 PASS(exit 0). 이 명령의 자동 테스트는 선택하지 않았고 위의 별도 두 실행 결과와 구분한다. 에디터는 WaitingRoomBase로 복귀했으며 씬 dirty 0이다.

## 전체 배치
실제 생성된 코스만 검토 카메라와 임시 조명으로 촬영했다. 주변 천장·장식은 제외했으며 배치·모델·재질은 바꾸지 않았다. 아래쪽이 출발대, 위쪽 녹색 선 쪽이 도착대다. 흰 점선은 현재 연결되지 않은 릴레이 구간이다. 2단계 세 소구간은 동시에 놓인 코스가 아니라 순서대로 교체되는 배치다.

1단계 · 두 줄과 중간 발판

![1단계 · 두 줄과 중간 발판](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Overview/01-stage1.png)

2-1 · 기본 그네 한 개

![2-1 · 기본 그네 한 개](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Overview/02-stage2-1.png)

2-2 · 연속 그네 두 개

![2-2 · 연속 그네 두 개](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Overview/03-stage2-2.png)

2-3 · 막다른 줄의 트리거와 짝 줄 끊김

![2-3 · 막다른 줄의 트리거와 짝 줄 끊김](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Overview/04-stage2-3.png)

3단계 · 네 줄·각 줄 그네·엇갈린 짝 트리거

![3단계 · 네 줄·각 줄 그네·엇갈린 짝 트리거](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Overview/05-stage3.png)

## 이후 진행 화면

첫 공연 중 퀘스트

![첫 공연 중 퀘스트](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Progress/00-first-performance.png)

첫 공연 뒤 대기실 안내

![첫 공연 뒤 대기실 안내](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Progress/01-tutorial.png)

안내 완료 뒤 두 번째 공연 준비

![안내 완료 뒤 두 번째 공연 준비](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Progress/02-second-performance-ready.png)

두 번째 공연 중 퀘스트

![두 번째 공연 중 퀘스트](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Progress/03-second-performance.png)

공연 종료 뒤 밤 일정 정하기

![공연 종료 뒤 밤 일정 정하기](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Progress/04-night-schedule.png)

일정 확정 뒤 취침 준비

![일정 확정 뒤 취침 준비](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Progress/05-night-bed.png)

침대 상호작용 직후

![침대 상호작용 직후](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Progress/06-bed-rest.png)

취침 후 Day 2 아침

![취침 후 Day 2 아침](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Progress/07-next-morning.png)

[퀘스트·패널 편집 안내](quest-editing.md). 기존 WaitingRoomTexts.txt 연결을 유지한다.
