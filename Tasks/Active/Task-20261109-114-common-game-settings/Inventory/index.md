# 설정 조사 원본 색인

[기능별 전체 목록](../settings-inventory.md)으로 돌아간다.

현재 반영 범위는 [UI 제외 목록](../settings-without-ui.md)이다. 이 폴더는 UI를 포함한 조사 원본이며 전체 행을 구현 대상으로 삼지 않는다.

## 필드별 원본

| 파일 | 내용 |
|---|---|
| [serialized-fields.csv](serialized-fields.csv) | 명시적 직렬화 필드 1,217개. 소스 경로·행·타입·필드명·초기값·Tooltip |
| [public-field-candidates.csv](public-field-candidates.csv) | public 필드 1,476개. 설정·참조뿐 아니라 상태 DTO도 포함, 후보 목록 |
| [code-constants.csv](code-constants.csv) | 상수 선언 640개. 기술 상수·문구 포함, 이전 대상 확정 아님 |
| [game-settings-values.csv](game-settings-values.csv) | GameSettings 폴더 에셋 11개의 에디터 읽기 값 876개 |
| [settings-assets.csv](settings-assets.csv) | 자체 스크립트를 참조하는 에셋 26개와 GameSettings 폴더 안/밖 구분 |
| [scene-direct-values.csv](scene-direct-values.csv) | 30개 씬의 직접 저장 스칼라 값 9,187행. 프리팹 상속 전개 제외 |
| [settings-access-sites.csv](settings-access-sites.csv) | 공통 설정 접근·덮어쓰기·키 직접 입력·개인 옵션·에디터 생성 코드 접근 위치 |
| [input-bindings.csv](input-bindings.csv) | PlayerControls.inputactions 바인딩 13개 |
| [inventory-counts.json](inventory-counts.json) | 이번 조사 수량 |

## 파일별 필드 빠른 목록

- [01](fields-01.md)
- [02](fields-02.md)
- [03](fields-03.md)
- [04](fields-04.md)
- [05](fields-05.md)
- [06](fields-06.md)

## 읽는 주의점

- 소스 초기값은 런타임 적용값이 아니다. 씬·프리팹 저장값, 상태, 덮어쓰기가 우선할 수 있다.
- Source 구문 트리로 선언을 조사했다. 조건부 컴파일 양쪽과 개발/미리보기 컴포넌트도 포함할 수 있다.
- 별도 public 필드 파일에는 통신·저장 자료형과 현재 상태도 있다. 이름만 보고 모두 조정값으로 분류하지 않는다.
- 비밀값으로 오인하거나 노출할 수 있는 필드명은 값 칸을 redacted로 처리했다. 퍼즐 암호도 같은 기준으로 가렸다.
- 에셋 값은 에디터에서 읽기만 했으며 저장하지 않았다. 게임 코드·씬·프리팹은 변경하지 않았다.

## 봉 상세 조사 (2026-11-09)
- [pole-scene-values.csv](pole-scene-values.csv): 봉 64개 에디터 읽기 값. 프리팹 상속 반영·활성 여부·시스템 등록 여부·실효 정원·진자 길이. [봉 비교](../pole-differences.md)의 근거다.
