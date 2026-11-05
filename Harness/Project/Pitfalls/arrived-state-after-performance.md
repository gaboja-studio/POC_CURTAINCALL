---
summary: 연결 외줄 공연의 완주자가 Arrived로 남으면 대기실 안내·상호작용·첫날 진행이 그 사람만 멈춘다
status: active
updated: 2026-11-05
source: Task-20261102-097
---

# 공연 완주자의 Arrived 잔류

## 언제 읽나

공연 뒤 대기실에서 완주한 사람만 튜토리얼·F 패널·외곽선이 안 나오고, 나머지는 동료를 기다릴 때.

## 원인

공용 런의 외줄은 ExternallyManaged라 Bridge 경로가 아니라 ServerMarkArrivals를 지나며 완주자를 Arrived로 바꾼다. 쉬는 시간 시작의 부활은 Fallen만 되돌렸고 Arrived 해제는 다음 공연 진입에만 있었다. 튜토리얼, CommonInteractionUI, 월드 라벨, 첫날 퀘스트는 모두 Normal만 받는다. 튜토리얼 완료가 안 올라가므로 가이드 쉬는 시간 타이머도 시작하지 않는다.

## 대처

InjuryRevival이 쉬는 시간·밤 정비 시작 때 Arrived도 Normal로 되돌린다. 새 시간대 규칙에 플레이어 상태 조건을 추가할 때 Arrived를 빠뜨리지 않는다.

## 확인 방법

PerformanceEntryJudgeTests.RestStartReturnsPerformanceFinisherToNormal을 실행한다. 실제 확인은 다인으로 한 명만 완주시키고 대기실 복귀까지 진행한다.
