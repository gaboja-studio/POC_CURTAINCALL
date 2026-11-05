---
summary: 추격자 형상은 유지하면서 원본 UV에 새 재질을 칠하거나 베이크할 때
status: active
updated: 2026-11-04
source: Task-20261104-102, PursuitTroupeV7Importer.cs, Source~/V8/bake-layers.py
---

# 마네킹 UV 베이크와 원본 삼각형 보존

## 언제 읽나
원본 모델에 재질만 바꿔도 되는지 판단하거나 베이크 메시를 Unity에 가져올 때.

## 함정과 해결
- Pursuit 원본 UV는 매우 좁은 영역에 겹친다. 단순 재질 교체로 개체별 천/봉제를 칠할 수 없어 새 UV가 필요했다. 형상 변경과 UV 재배치를 구분한다.
- 베이크 프록시를 weld/unwrap하면서 F03/F04의 매우 작은 원본 면이 각각2개 사라졌다. 프록시 삼각형을 그대로 내보내지 않는다.
- V7는 원본 canonical triangle 목록을 유지하고 corner별 UV만 매핑한다. 작은 퇴화 면에도 기존 인덱스를 남기며 원본 위치·노멀·가중치·bindpose를 복사한다.
- UV 경계에서 정점 수가 늘어도 삼각형 수/순서와 변형된 표면은 동일해야 한다. importer의 canonical 검사 및 810자세 비교로 확인했다.
- 기본 조합과 독립 피부/봉제 → 때/마모 → 혈흔 층은 같은 메시·공유 재질과 property block을 사용한다. Blender의 높은 폴리곤 천/실밥은 베이크 원본이며 런타임 형상이 아니다.
- PlayMode에서 Read/Write=false 원본 메시의 triangles를 읽으면 로그 오류가 난다. 개수 검사만 필요하면 subMesh별 GetIndexCount를 사용하며 검사를 위해 에셋 Read/Write를 켜지 않는다.

- 관절 봉제만 보강하는 V8에서는 이미 검증한 V7 corner별 UV를 그대로 쓴다. 다시 unwrap하거나 런타임 메시를 재생성하지 않고 베이크 텍스처만 교체한다.
- 때/마모와 혈흔은 seed를 분리해야 혈흔 무늬 변경 시 아래 오염이 움직이지 않는다. 피 숨김은 최상층만 차단하며 하위 층의 동일성을 실제 렌더 픽셀 비교로 확인한다.
