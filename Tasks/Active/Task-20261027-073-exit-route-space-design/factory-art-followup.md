# 공장 에셋 후속 마감 — 2026-11-02

담당 @RagSpirin / feat/exit-route/design-map / 기준 dev b0669a96.
직전 두 공장 패키지 외형 통합을 이어서 수행했다. 새 패키지는 임포트하지 않았다.

## 변경

| 대상 | 문제 | 반영 |
|---|---|---|
| A 도착 계단·연결 바닥·문틀80개 | EscapeBackground 바깥의 A 연결 구조에 기존 단색 콘크리트가 남음 | 벽/문틀은 factory-concrete,계단/바닥/참은 factory-floor로 연결 |
| 실제 사용하는 공장 기계 재질14개 | 원본 _Normal 텍스처가 변환기에서 누락돼 평평한 표면으로 표시됨 | 해당 요철 텍스처를 A 파생 재질의 _BumpMap/_NORMALMAP에 연결,강도0.6 |
| EscapeFactoryArtPass.Materials | _BumpMap/_NormalMap만 조회 | Clock Factory의 _Normal 별칭도 조회하도록 보완 |
| 파생 텍스처11개 | 원본을 그대로 연결하면 큰 원본 임포트 설정을 사용 | private Task073/FactoryTextures에1K·노멀맵·mip/streaming·비읽기 파생,실제 A 의존 파일만 유지 |

벽·바닥 두께/위치,그네·외줄·대포·컨베이어 동선,화염·배선·제한시간·성공 조건은 변경하지 않았다. 대기실 씬/B·C/벤더 원본 에셋은 수정하지 않았다.

## 확인 결과

- 저장 씬 문서 추가0/삭제0. 변경80개 모두 MeshRenderer의 재질 참조다.
- 저장된 Collider/MonoBehaviour2,132개와 Transform4,677개의 변경 전후 설정 동일.
- 렌더러3,052개 유지,참조 재질57→56개. 실제 사용14개 source 재질의 노멀맵 연결14/14,지원되지 않는 셰이더0,이전 brutal-concrete 참조0.
- 편집 시점7개(입구·첫 대포·하강·컨베이어·협동·화염 복도·마지막 구간) 전후 렌더 확인. 모든 각도/실제 플레이의 충돌·가림을 보증하는 검사는 아니다.
- 새 텍스처11개는 모두 A 씬의 의존성에 포함된다. 폴더 안의 미사용 파생 재질까지 변경하지 않도록 정리했다.
- A 저장/Play stopped. 실제 Play/MPPM·FPS/로딩 시간 측정은 하지 않았다. 통합 검사 결과는 log.md에 기록한다.

## 보존·증거

- `.harness/local/factory-followup/before.unity`: 변경 전 저장 A.
- `materials.json`, `renderers.json`, `logic-before.json`: 변경 전 재질/표시/장치·충돌.
- `after/`: 같은 시점의 렌더와 변경 후 참조/장치 기록.
- `*.mat.before`: 수정 전 파생 재질 보관. 벤더 원본은 변경하지 않았다.
- `applied.json`: 최초 처리 기록이며 최종 범위는 미사용12재질/신규9텍스처 정리 이후14재질·11텍스처다.
- 동적 escape-labels 폰트 캐시 자동 갱신은 별도 보존했다. 커밋·푸시는 하지 않았다.
