---
summary: 그네 탑승·빠른 카메라 이동 때 프레임 드랍·GC 급증 — 카메라 가림 메시 검사가 움직이는 메시를 매 프레임 새 배열로 다시 만들고, 처음 보는 대형 메시를 한 프레임에 몰아 만들던 문제
status: active
updated: 2026-11-26
source: Task-20261126-204-swing-piggyback-fixes
---

# 움직이는 메시와 카메라 가림 검사

## 언제 읽나

그네·이동 발판처럼 움직이는 장식이 카메라와 캐릭터 사이에 걸릴 때 프레임이 떨어지거나,
`CameraOcclusionFade`·`CameraMeshCollision`·`CameraCollisionSurface`를 고칠 때.

## 증상

- 외줄 공중그네를 잡고 흔들면 CinemachineBrain.LateUpdate 아래 GC가 프레임마다 수백 KB~2MB, 가끔 28MB/151ms 스파이크(에디터 측정).
- 같은 렌더러가 90프레임 중 85~87프레임 재생성(봉 외형 파이프 2,272삼각형, 조명 빔 816삼각형).

## 원인

`CameraMeshCollision`은 렌더러 행렬이 바뀌면 `mesh.vertices`·`mesh.triangles`와 삼각형 배열을 새로 할당했다.
흔들리는 봉·빔은 행렬이 매 프레임 바뀌고, 카메라가 크게 움직이면 처음 보는 3만 삼각형급 메시를 한 프레임에 여러 개 만들었다.

## 대처

- 메시 원본(정점·인덱스)은 메시별로 한 번만 읽고, 움직이면 같은 배열을 제자리에서 다시 변환한다.
- 가림 표시(`deferrable: true`)만 프레임당 24,000삼각형 예산으로 나눠 만든다. 가림은 35ms 확인 지연이 있어 차이가 없다.
- 카메라 벽 충돌(`CameraCollisionSurface`)은 벽을 통과하면 안 되므로 예산을 쓰지 않고 즉시 계산한다.

## 확인 방법

EditMode `CameraMeshCollisionCacheTests`. 플레이에서는 Profiler로 그네를 잡고 흔드는 동안 CinemachineBrain.LateUpdate GC가 약 1KB/프레임인지 본다.
남은 공통 비용(카메라 갱신 약 11ms/프레임·UI 매 프레임 할당)은 그네와 무관하게 있으며 Task-204 log에 기록했다.
