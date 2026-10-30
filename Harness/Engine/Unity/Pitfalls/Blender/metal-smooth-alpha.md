---
summary: Blender 이미지 RGB 저장은 smoothness 알파를 버리고 metallic RGB를 알파로 곱할 수 있다
status: active
updated: 2026-10-30
source: Task-20261030-082
---

# URP 천이 거울처럼 반사됨

## 언제 읽나
Blender에서 만든 metallic/smoothness 텍스처를 URP Lit에 넣은 뒤 천까지 반사되거나 수치가 맞지 않을 때.

## 원인과 대처
이번 환경의 `Image.save()`는 RGB로 저장하면서 알파를 버리고 RGB에 알파를 곱했다. URP는 알파 1을 smoothness로 읽어 거울처럼 보였다. 데이터 맵은 색 관리 없는 RGBA8 PNG로 저장하고 R=metallic, A=1-roughness를 유지한다. TextureImporter.sRGBTexture=false, alphaSource=FromInput, alphaIsTransparency=false로 설정한다.

## 확인
메모리 이미지가 아니라 실제 저장된 PNG를 다시 읽는다. Task-082 기준 천 R=0/A=.09, 황동 R=.72/A=.57(8비트 반올림 허용)을 확인한다. Blender 미리보기와 Unity 실물 검토를 모두 수행한다.
