---
summary: 대기실 등 씬 전환 검사는 그래픽이 켜진 일반 Unity 에디터의 Game View에서 실행
status: active
updated: 2026-11-13
source: Task-20261112-119
---

# 전환 커튼은 닫힌 렌더 프레임이 필요함

- 로그에 `C16 Close rejected: this transition requires a rendered client viewport.`가 있으면 `TransitionCurtainView.Close()`가 배치 모드 또는 Null 그래픽 장치 때문에 닫기를 거부한 것이다. 이어지는 `[Curtain] 커튼 닫힘 확인 실패`는 씬 전환이 취소된 결과다.
- 커튼은 완전히 닫힌 상태에서 한 프레임을 그린 뒤 다음 프레임 경계까지 지나야 전환을 허용한다. `-nographics`만 빼도 `-batchmode`가 남아 있으면 거부된다.
- 실제 씬 흐름을 검사할 때는 배치 옵션 없이 에디터를 열고 Game View를 활성화한 상태에서 `Scripts/verify-unity.ps1 -ProjectPath . -RunTests -Mode PlayMode -Filter <테스트 이름>`을 실행한다. 결과에 다른 실패가 있으면 커튼 실패와 구분한다.
