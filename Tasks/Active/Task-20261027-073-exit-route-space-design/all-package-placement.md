# 추천8개 패키지의 A 배치

2026-11-02 @RagSpirin, feat/exit-route/design-map.

사용자가 추천한 모든 패키지의 다운로드와 적용을 요청했다. 학원 NAS의 원본8개를 D:/UnityAssetStaging/Task073에 수신했다. Unity 프로젝트에는 배치에 사용한 외형과 의존 파일을 보관했다.

| 패키지 | 실제 적용 |
|---|---|
| Abandoned Industrial Factory Environment | 앞서 적용한 공장 콘크리트·배관·기계 외형을 사용 |
| Steampunk Clock Factory Environment | 앞서 적용한 기계·컨베이어·표면 노멀과 누수 자국을 사용 |
| Industrial Elevator Modular | 하강 공동의 경로 옆 벽에 정지한 화물 승강기와 수직 가이드 |
| Disel generator | 마지막 구간 남측 가장자리의 출구 전원 발전기 |
| Levers Buttons and Switches | 기존 상부 대포 유지 버튼과 전원 버튼의 Renderer를 통한 외형 교체 |
| Big Tools Pack | 마지막 구간 북측 가장자리의 정비 작업대와 공구 |
| Industrial Warehouse Pack | 출구 앞 북측 가장자리의 팔레트 운반 카트 |
| Fence Gate pack | 정비 작업대 뒤의 철제 펜스 |

씬 Hierarchy의 EscapeCampaignFrame/EscapeBackground/55_FactoryEquipment에 신규 설비를 모았다. 승강기는 배경 설비다. 발전기는 전원 설비의 외형이며 절단 성공 판정은 기존 배선 기믹이 담당한다.

## 확인

- 기존 Transform/Collider/MonoBehaviour6,809개 직렬화 설정 비교 동일. 기존 버튼 두 개는 MeshFilter/Renderer 외형만 수정했다.
- 신규 Renderer16개, 신규 Collider10개. 새 기물과 기존 활성 비트리거 충돌체의 ComputePenetration 검사에서 깊이 .015m 초과 겹침0건을 확인했다.
- 편집 카메라로 하강 공동·발전기·정비대·출구 카트를 확인하고 발전기의 종횡비·펜스 길이·공구의 접지 높이를 조정했다.
- private SelectedEquipment 폴더의 에셋 파일은 약23.72MiB다. 기존 두 공장 팩 파일은 이 수치에 포함하지 않았다. 텍스처 상한1K·mipmap streaming·URP 변환과 원본 LOD를 사용했다.
- 실제 Play/MPPM과 로딩 시간/FPS 측정은 수행하지 않았다. 최종 check-work 결과는 log.md에 기록한다.
- 배치 전 씬과 검사 자료는 .harness/local/equipment-placement/에 보관했다.
