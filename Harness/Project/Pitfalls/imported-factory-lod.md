---
summary: 공장 프리팹의 근접 LOD 누락과 URP 변환의 노멀맵 별칭 누락을 확인한다.
status: active
updated: 2026-11-03
source: Task-20261027-073
---

# 공장 에셋의 근접 렌더링 누락

## 언제 읽나
Leartes 공장 프리팹이 조감도에서는 보이지만 근접 카메라에서 사라질 때.

- 탈출로 제작에서 `AbandonedFactory/.../SM_ConcreteFloor.prefab`의 LOD 임계값이 `0, 0, 0.01`로 관찰됐다. Renderer.enabled만으로 실제 카메라의 표시 상태를 판단하지 않는다.
- LODGroup.enabled=false만 적용했을 때에도 근접 캡처에서 구조물이 누락되는 현상이 관찰됐다. 이 배경 초안은 인스턴스의 LODGroup을 제거하고 LOD0 렌더러만 활성화해 확인했다. 원본 프리팹은 변경하지 않았다.
- 임의 크기에 맞춰 배치할 때 모든 LOD의 bounds를 합치면 실제 표시되는 LOD0와 크기가 달라진다. LOD0 bounds로 맞추고 바닥 높이·단절 경계를 별도로 확인한다.
- 프리팹 인스턴스의 Transform/Renderer 변경은 `PrefabUtility.RecordPrefabInstancePropertyModifications`로 보존하고 저장·재개방 후 다시 확인한다.
- LOD0 고정은 공간 검토용 조치다. 배포용 최적화를 완료했다는 뜻이 아니며, 정식 아트 통합 시 올바른 LOD 임계값과 성능을 검증한다.

## URP 변환 뒤 기계 표면이 평평할 때

- Clock Factory의 S_Standard 계열 재질은 요철 텍스처를 `_Normal`에 저장한다. `_BumpMap`/`_NormalMap`만 조회하면 색은 정상이어도 노멀맵이 빠진다. 별칭을 함께 조회하고 URP의 `_BumpMap`/`_NORMALMAP`과 실제 NormalMap 임포트 설정을 확인한다.
- 폴더 전체를 일괄 보수하기보다 실제 씬이 참조하는 파생 재질만 수정하고 필요한 텍스처만 복사한다. Task073 factory-art-followup.md의14재질/11텍스처 보수 근거를 참조한다.

## A의 정적 렌더 묶음 편집

- A의 일부 원본 MeshRenderer는 렌더 묶음 생성 후 꺼져 있다. 오브젝트·충돌체는 원래 계층에 보관한다.
- 직접 배치/재질/삭제 편집 전 CurtainCall/Escape/Optimization/Restore editable geometry를 실행한다. 편집 후 Rebuild static geometry chunks로 갱신한다. 원본만 편집하면 기존 묶음에는 반영되지 않는다.
- static-batch-record.asset의 원본 GlobalObjectId와 소유 메시 목록을 사용해 복원한다. 복원→재생성 시 원본/묶음의 삼각형 합계와 활성 렌더러 수가 재현되는지 확인한다.
- 상위 오브젝트의 Transform이 코드에 참조되는지도 검사한다. GameObject 참조만 제외하면 cue.warning 아래 점멸 표시가 정적 메시로 추출돼 고정 빨간 선으로 남는다. 구형 Factory static55/56/80의 이 결함은 제어 분기14곳 복구로 보수했다.
- 겹친 면 검사는 Cube/block 이름이나 메시 bounds만으로 끝내지 않는다. 공장 파생·개구부·모델 내부의 실제 삼각형도 비교한다. 같은 LODGroup의 서로 다른 단계는 동시에 렌더되지 않으므로 중복으로 판정하지 않는다. 겹친 부분을 자를 때 원래 UV·서브메시·충돌을 보존한다.

## 시설 좌표로 새 외부 배경을 놓을 때
- A EscapeBackground의 X/Z 배율은1.25다. 시설 좌표로 생성한 메시의 루트 위치/회전만 맞추면 좌표까지1.25배 되어 출구 밖에 배치한 갈대가 다른 곳에 나타난다. 부모 lossyScale을 보정하고 실제 카메라에서 위치/바닥 높이를 확인한다.
