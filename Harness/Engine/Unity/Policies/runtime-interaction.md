# Runtime Interaction Validation

다음 기능은 자동 로직 테스트만으로 완료 처리하지 않는다.

- Click, Drag, Drop, Touch, Pointer 입력, UI 버튼
- 씬/Inspector 참조, 런타임 GameObject 연결, 프리팹 인스턴스 연결

## 검증 순서

1. Logic Test — EditMode 테스트
2. Runtime Integration — Play Mode에서 참조·연결이 실제로 살아 있는지(로그/테스트)
3. Input Entry — 실제 입력 경로(Input System → 핸들러)가 호출되는지
4. Human Play Check — 사람이 직접 조작

## 실제 입력 검증이 아닌 것

- 리플렉션으로 메서드 직접 호출
- 내부 함수 직접 실행, 상태값 강제 주입
- 실제 Pointer/Input System 이벤트를 거치지 않은 합성 테스트

이런 테스트는 Logic Test로만 기록한다.

## 완료 조건

실제 조작이 필요한 기능은 사람이 Play Mode에서 확인하기 전까지 Task를 `done`으로 바꾸지 않는다.
실제 입력 자동화가 불가능하면 Status를 `human-verify`로 두고, 사람이 해 볼 동작을 최대 3개로 정리한다.
