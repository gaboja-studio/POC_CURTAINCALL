---
summary: 곡면에 붙인 얼굴 페인트는 Blender 양면 표시와 Unity의 뒷면 제거 결과가 다를 수 있다
status: active
updated: 2026-10-30
source: Task-20261029-078
---

# 얼굴 페인트의 뒤집힌 면

## 언제 읽나
Blender에서는 정상인데 Unity에서 눈·입·눈썹이 삼각형 조각처럼 빠질 때.

## 증상과 원인
- 삐에로 v03의 곡면에 투영한 페인트가 Unity에서 부분 누락됐다. 열린 면을 독립된 섬으로 만든 뒤 닫힌 입체용 `recalc_face_normals`를 실행하여 일부 면의 방향이 뒤집혔다.
- Blender 재질 미리보기의 양면 표시는 이 결함을 숨긴다. Avatar·가중치·절단 캡·유한 좌표 검사만으로 발견되지 않았다.
- 절단 제작 중 법선을 다시 계산하면서 정상 메시에서 교정한 페인트가 다시 뒤집힐 수 있다.

## 대처
1. 페인트 외곽을 실제 머리 삼각형에 클리핑해 곡면 내부로 묻히지 않게 만든다.
2. 열린 페인트 섬의 의도한 앞 방향으로 winding을 명시적으로 맞춘다. 삐에로는 Blender -Y 전면이다. 모든 모델에 같은 축을 가정하지 않는다.
3. 정상뿐 아니라 각 절단 몸체의 최종 export 직전에도 방향과 corner normal을 다시 확인한다. 양면 셰이더로 문제를 숨기지 않는다.
4. 프레임이 진행되는 Unity 게임 카메라에서 정상/절단 표면을 촬영한다. v03은 각 몸체의708개 얼굴 삼각형 중 역방향0을 별도 기록했다.

근거: private `CharacterArt/BluePierrot/Source~/face-winding-v03-report.json`, `export-v03.py`, `rebuild-v03-unity-clean.png`.
