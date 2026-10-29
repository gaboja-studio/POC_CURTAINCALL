---
summary: 화면에 쓰이는 메시 에셋을 CopySerialized로 덮어쓰면 데이터는 바뀌어도 화면은 예전 메시를 그린다
status: active
updated: 2026-10-27
source: Task-20261027-075
---

# 메시 에셋 덮어쓰기 후 예전 모양이 그려짐

## 언제 읽나

에디터 도구가 절차적 메시를 만들어 `.asset`으로 저장하고, 다시 만들 때 같은 경로의 기존 에셋을 갱신할 때.

## 증상

Task-075에서 로비를 다시 만들었다. 서벽의 옛 문을 지운 새 벽 메시를 `EditorUtility.CopySerialized(newMesh, existing)`로 기존 `LA_LobbyWalls.asset`에 덮어썼다.

- 메시 데이터(정점·삼각형)와 MeshCollider 레이캐스트는 새 벽이 막혀 있다고 나왔다.
- 화면에는 옛 문 자리와 그 위 벽 전체가 그려지지 않았다. 배경색이 그대로 보였다.
- 렌더러를 껐다 켜거나, 메시를 다시 지정하거나, `UploadMeshData`를 불러도 그대로였다.

## 원인

이미 화면에 쓰이는 메시에 `CopySerialized`로 직렬화 값만 복사하면 GPU 쪽 메시가 다시 만들어지지 않는다. 예전 모양으로 그려진다.

## 대처

- 기존 메시를 메시 API로 다시 채운다: `Clear()` → `indexFormat` → `SetVertices`/`SetNormals`/`SetTangents`/`SetUVs` → `subMeshCount` → `SetTriangles` → `RecalculateBounds()`.
- Task-075 `WaitingRoomLayoutA.SaveMesh`를 이렇게 고친 뒤 모든 방을 다시 만들었다.

## 확인 방법

- 카메라 배경을 자홍색(1,0,1)으로 두고 방 안에서 여러 방향을 렌더링한다.
- 자홍 픽셀이 있으면 그려지지 않은 면이 있는 것이다.
- 레이캐스트만으로는 이 문제를 찾지 못한다(MeshCollider는 새 데이터를 쓴다).
