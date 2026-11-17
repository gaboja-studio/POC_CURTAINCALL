---
summary: MonoBehaviour 필드 초기화에서 MaterialPropertyBlock을 생성하면 CreateImpl 예외가 발생한다. Awake 또는 메인 스레드 실행 메서드에서 생성한다
status: active
updated: 2026-11-17
source: Task-20261015-041
---

# Unity 객체를 필드 초기화에서 생성함

## 언제 읽나

컴파일은 성공했지만 AddComponent·씬 로드·EditMode 검사에서 `CreateImpl is not allowed to be called from a MonoBehaviour constructor`가 발생할 때.

## 증상·원인

- 041 관문 검사 8건이 `readonly MaterialPropertyBlock tint = new()` 필드 초기화 예외로 실패했다.
- MonoBehaviour 생성자·필드 초기화 시점에는 이 Unity 네이티브 API 호출이 허용되지 않았다. 컴파일 성공만으로 초기화 성공을 보장하지 않는다.

## 대처·확인

- 필드에는 참조만 선언하고 Awake 또는 메인 스레드 실행 메서드에서 생성한다. 041은 표시 메서드에서 필요할 때 한 번 생성하도록 수정했다.
- 같은 컴포넌트를 추가하는 EditMode 검사를 다시 실행한다. 수정 후 HumanCannon 113/113 통과, 실제 플레이 검증과 구분한다.
- Task-20261116-144에서도 씬 재로드 PlayMode 검사에서 재현됐다. 평소 실행 중 AddComponent로 만든 객체가 보였다고 역직렬화 경로까지 안전한 것은 아니다. MaterialPropertyBlock 생성을 Awake로 옮기고 실제 외줄·인간대포 씬 재진입 검사를 각각 통과했다.
