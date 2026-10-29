---
summary: SceneView 눈높이 캡처에서 실제 카메라 위치가 목표보다 뒤로 밀리는 문제
status: active
updated: 2026-10-28
source: Task-20261027-073, Unity 6000.3.25f1 편집 카메라 상태 조회
---

# Scene View 눈높이 위치

## 언제 읽나
SceneView.LookAtDirect로 실제 플레이어 눈높이의 구조 캡처를 준비할 때.

- SceneView.camera.fieldOfView는 화면 비율에 따라 cameraSettings.fieldOfView와 다를 수 있다. 관측값은 각각86.8248도/60도였다.
- 목표 눈 위치를 eye, 시선 회전을 q, 피벗 거리 d라고 할 때 pivot=eye+q*forward*d, size=d*sin(cameraSettings.fieldOfView*Deg2Rad/2)를 사용한다.
- camera.fieldOfView로 size를 계산하면 d=10m 기준 카메라가 약3.74m 더 뒤로 가는 현상을 확인했다. 배치 후 camera.transform.position을 읽어 목표와 비교한다.
- lastActiveSceneView가 null이면 EditorWindow.GetWindow<SceneView>()로 연 뒤 편집 카메라를 조정한다. 이 작업은 플레이어 이동 입력과 구분한다.
