# Asset Ownership

## 기본 원칙

작업자는 Task `setup.md`의 **작업 구역**과 **배정된 공용 파일**만 수정한다. 코드 폴더 규칙은 `code-folders.md`.

## 공용 파일

씬(`Assets/Scenes/`), 여러 기능이 쓰는 프리팹, 중요한 `.asset`·ScriptableObject, Render Pipeline 에셋(`Assets/Settings/`), `ProjectSettings/`, `Packages/`, 공용 UI·머티리얼·원본 아트/사운드.

- 한 Integration 안에서 공용 파일 하나는 **Task 하나만** 소유한다. PM이 `Integrations/Active/Integration-*/meta.md`의 **공용 파일 소유 표**에 배정하고, 해당 Task `setup.md`에도 적는다.
- 소유 범위는 중첩 프리팹이 영향을 주는 범위까지 포함한다.
- 소유 범위를 몰래 늘리지 않는다. 필요하면 PM에게 요청한다.
- 코드가 참조한다고 해서 그 에셋을 수정해도 된다는 뜻은 아니다.
- `ProjectSettings/`, `Packages/` 변경은 사람의 결정이 필요하다(`Harness/Core/Policies/human-decision.md`).
- `Assets/_ThirdParty/`(외부 에셋 submodule)는 PM만 추가·수정한다. 작업자는 쓰기만 하고, 필요한 외부 에셋은 PM에게 요청한다.

## 수정 방법

- 에디터가 연결되어 있으면 raw YAML 편집 대신 에디터 조작을 쓴다(`editor-targeting.md`).
- 수정 전후로 하이어라키·컴포넌트를 확인하고, 소유한 에셋만 저장한다.
- **저장 여부는 에디터 보고가 아니라 `git status`로 확인한다**(`Pitfalls/scene-not-saved.md`).
- 씬·프리팹 병합 충돌은 AI가 해결하지 않는다. 담당자와 PM이 함께 해결한다.

## handoff

소유한 공용 파일 목록, 각 파일의 저장 상태, 눈으로 확인한 내용을 적는다.
