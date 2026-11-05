# Task-20261105-108

- **Title:** 씬 전환 첫 프레임 멈춤으로 Relay 튕김 원인 측정·해결
- **Type:** fix
- **Assignee:** @MoHoDu
- **Branch:** fix/loading
- **Base:** dev

## Context
씬 로드 도중 "타임오버"로 방에서 나가지는 현상. Relay는 메인 스레드가 약 10초 멈추면 할당을 끊는다([relay-stall-disconnect](../../../Harness/Project/Pitfalls/relay-stall-disconnect.md)). 1단계로 멈춤을 만드는 작업을 측정해 특정하고, 다음 단계에서 고친다.

## 계획
1. 플레이 중 2초 이상 멈춘 프레임의 작업 목록을 남기는 `LongFrameRecorder` 에디터 도구 추가(완료).
2. DevStartLauncher로 1일차 공연1(Tightrope) 진입을 재현해 원인 특정(완료).
3. 해결: 공연 배경 비동기 로드 중 동기 Resources.Load가 일어나지 않게 한다(사용자 확인 후).

## 미정·주의
- 공연 배경(`_ThirdParty` performance_background) 로드 타이밍을 바꾸면 배경이 늦게 보일 수 있다. 연출 판단은 사람이 한다.
