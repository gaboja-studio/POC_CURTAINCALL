---
summary: LineRenderer 좌표를 로컬/월드 좌표로 잘못 읽어 정상 동작을 버그로 오해함
status: active
updated: 2026-09-29
source: Octoplug 회고
---

# LineRenderer 좌표계 착각

## 언제 읽나

LineRenderer(또는 TrailRenderer) 위치 값을 조회해 다른 오브젝트 위치와 비교할 때.

## 증상

선 위치가 대상과 어긋나 보이는데 화면에서는 정상이다.

## 원인

`useWorldSpace = false`이면 `GetPosition()` 값은 LineRenderer 트랜스폼 기준 로컬 좌표다.

## 대처

비교 전에 `useWorldSpace`를 확인하고, 로컬이면 `transform.TransformPoint()`로 월드 좌표로 변환한 뒤 비교한다.

## 확인 방법

조회 결과에 `useWorldSpace` 값을 함께 출력한다.
