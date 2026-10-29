---
summary: AddComponent로 만든 TextMesh는 기본 글꼴 재질(GUI/Text Shader, 깊이 무시)을 써서 벽 너머에서도 보인다
status: active
updated: 2026-10-29
source: 2026-10-29 Task-075 폐기물 보관소 글자가 배관 구역 벽 너머로 보인 사용자 지적
---

# TextMesh 기본 재질이 벽을 뚫고 보임

## 증상

에디터 생성기에서 `AddComponent<TextMesh>()`로 만든 표지·번호판 글자가 벽·문·기둥 너머에서도 그려진다.

## 원인

TextMesh의 기본 `Font Material`은 `GUI/Text Shader`라 깊이 검사를 하지 않는다.

## 대응

- 글꼴 텍스처가 같은 깊이 검사 재질 `Assets/Scenes/Tests/WaitingRoom/Base/Materials/GB_Text.mat`(`CurtainCall/GreyboxText`)을 렌더러에 지정한다. Task-075 생성기는 `WaitingRoomLayoutA.DepthText(t)`를 쓴다.
- 바꾼 뒤에는 벽 철골·기둥·선반처럼 글자 앞에 있던 물체가 실제로 글자를 가린다. 글자 위치를 가림 없는 곳으로 옮겨 다시 확인한다.
