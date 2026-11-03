---
summary: 추격 상태·위치 전송 통합, 초기화 분산과 첫 씬 로드/반복 로드 성능 측정을 확인할 때
status: active
updated: 2026-11-03
source: Task-20261103-099, 로컬 Unity 실측 및 회귀
---

# 추격 성능 측정·초기화

## 언제 읽나
대기실 로드 멈춤·통신량·초기화 타이밍을 바꾸거나 Probe 수치를 비교할 때.

- Director.Awake는 직렬화된 방/배치 순서로 생성 요청을 수집한다. Update에서2ms 작업 예산·최대4체/프레임으로 프리팹과 행동 그래프를 초기화한다. 개별 Instantiate/Init은 도중에 나눌 수 없어 단일 작업이 예산을 넘을 수 있다.
- 마지막 actor의 Start가 실행된 다음 프레임 IsInitialized가 된다. 그전에는 네트워크 등록/Ready·AI·스냅샷 적용을 시작하지 않는다. SceneCurtainTransition.Reveal은 로컬 초기화를 기다린다. 지연된 coroutine을 만들지 않아 씬/Director 파괴로 큐도 함께 정리된다.
- PursuitPerformanceProbe는 로드 완료와 초기화 준비 완료를 구분해 기록한다. 초기화 총시간/작업시간/최장 단일 생성/분산 프레임, Profiler marker `Pursuit.SpawnAndInitialize`를 제공한다.
- 메뉴 `Tools/Curtain Call/Performance/Measure Pursuit Waiting Room First Load`: Start 씬으로 에디터를 재시작한 뒤 사용. 같은 에디터 세션에서 이미 추격 씬을 열면 거부한다. 일반 메뉴는 반복 측정 가능. JSON의 firstSceneLoadInEditorSession은 OS/GPU 캐시 초기화나 빌드 측정이라는 뜻이 아니다.
- 통합 v3 상태529B(26체, 비활성 측정). 상태4Hz+위치20Hz payload 기준 약19.4KB/s/클라이언트이며 상태 변화 추가 전송과 NGO/UTP 헤더는 별도다. 과거419B/4KB/s는 위치 경로가 분리되기 전 다른 브랜치 수치라 통합 결과에 그대로 쓰지 않는다.
- 로컬 통합 기준 반복 로드 최장1273ms, GC451ms. 중간 측정1076ms, 최종 예산 방식1250ms(GC441ms)로 전체 로드 개선을 확정하지 않는다. 최종26체는7프레임에 생성작업13.65ms/단일최대1.03ms였다. 실측 JSON과 검증 범위는 Task-099 log/roadmap에 기록한다.
- 로비 batches/그림자 부하, 첫 빌드/저사양 PC·Relay 다인 재현은 별도 확인 대상이다. 같은 값의 Light 쓰기만 생략하며 기존 램프 flicker/포획 암전/방 상태 계산은 유지한다.
