---
summary: 별도 asmdef 테스트에서 Assembly-CSharp 타입을 직접 참조해 컴파일이 실패할 때
status: active
updated: 2026-11-19
source: Task-20261119-167, EscapeRopeSoundTests CS0234 재현과 수정
---

# 테스트 asmdef의 런타임 타입 참조

## 언제 읽나

새 회귀 테스트를 추가한 뒤 런타임 네임스페이스를 찾지 못한다는 CS0234가 발생하거나 Unity가 Safe Mode를 제안할 때.

## 원인과 수정

- 별도 asmdef는 미리 정의된 `Assembly-CSharp`를 직접 참조할 수 없다. 같은 Assets/Scripts 아래에서도 테스트의 asmdef 경계를 확인한다.
- 이 프로젝트의 `CurtainCall.EscapeRoute.EditTests`는 기존 테스트처럼 로드된 어셈블리에서 런타임 타입을 찾아 reflection으로 호출한다. 새 테스트에서도 같은 방식을 사용한다.
- `System`과 `UnityEngine`을 함께 사용할 때 `Object`는 명시적으로 구분한다.

## 확인

컴파일 오류가 해소되고 정상 모드로 복귀한 뒤 추가 테스트와 영향을 받는 기존 테스트를 실행한다.
