---
summary: 추가 로드(Additive)로 공간 씬을 바꿀 때 새 씬을 먼저 붙이면 EventSystem 등 씬당 하나인 것이 잠깐 겹침 → 이전 씬을 닫고 붙인다
status: active
updated: 2026-10-16
source: Task-20261013-032
---

# 추가 로드 씬 교체 순서

## 언제 읽나

코어 씬을 남겨 두고 내용 씬만 Additive로 붙였다 떼는 전환을 만들 때. 또는 전환 순간 Console에 "There can be only one active Event System." / "There are 2 event systems in the scene."이 뜰 때.

## 증상

- 전환마다 EventSystem 2개 경고가 난다. AudioListener·메인 카메라·씬 싱글턴도 같은 순간 둘이 된다.
- 통째 교체(Single)에서는 나지 않는다.

## 원인

새 씬을 먼저 `LoadSceneAsync(Additive)`로 붙이고 이전 씬을 나중에 닫으면, 두 씬이 함께 떠 있는 프레임이 생긴다. 각 씬이 자기 EventSystem 등을 갖고 있으면 그 순간 겹친다.

## 대처

1. 이전 내용 씬을 먼저 닫고(`UnloadSceneAsync` 또는 NGO `UnloadScene`) 다 닫힌 뒤 새 씬을 붙인다.
2. 닫을 씬이 활성 씬이면 먼저 코어 씬을 `SetActiveScene`으로 활성화한다.
3. 두 씬 사이 빈 프레임에 코어 씬 카메라가 켜져 깜빡이지 않게, 전환 중에는 코어 카메라를 끈 채 둔다.
4. 예: `Assets/Scripts/Systems/SpaceLink/SpaceLinkDirector.cs` 추가 로드 분기, `SpaceLinkHub.cs` 카메라 처리.

## 확인 방법

전환 중 `SceneManager.sceneCount` 최대값이 코어 + 1을 넘지 않는지 PlayMode 테스트로 확인한다(`SpaceLinkPlayTests.Additive_KeepsHubAndSwapsSpaceScenes`). Console에 EventSystem 경고가 없는지 본다.
