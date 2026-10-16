# Asset Ownership

## 기본 원칙

작업자는 Task `setup.md`의 **작업 구역**과 **배정된 공용 파일**만 수정한다. 코드 폴더 규칙은 `code-folders.md`.

## 공용 파일

씬(`Assets/Scenes/`), 여러 기능이 쓰는 프리팹, 중요한 `.asset`·ScriptableObject, Render Pipeline 에셋(`Assets/Settings/`), `ProjectSettings/`, `Packages/`, 공용 UI·머티리얼·원본 아트/사운드.

- 한 Integration 안에서 공용 파일 하나는 **Task 하나만** 소유한다. PM이 `Integrations/Active/Integration-*/meta.md`의 **공용 파일 소유 표**에 배정하고, 해당 Task `setup.md`에도 적는다.
- 소유 범위는 중첩 프리팹이 영향을 주는 범위까지 포함한다.
- 소유 범위를 몰래 늘리지 않는다. 필요하면 PM에게 요청한다.
- 코드가 참조한다고 해서 그 에셋을 수정해도 된다는 뜻은 아니다.
- `ProjectSettings/`, `Packages/` 변경은 사람의 결정이 필요하다(`Harness/Core/Policies/human-decision.md`). 단 빌드 씬 목록(`ProjectSettings/EditorBuildSettings.asset`)의 씬 추가·삭제는 모든 작업자에게 허용한다. 다른 사람의 씬을 빼기 전에는 그 담당자에게 알린다.
- `Assets/_ThirdParty/`(외부 에셋 submodule)의 추가·수정은 PM 또는 프로젝트 정책에 지정된 에셋 관리자가 한다. 씬·프리팹에 가져다 배치·참조하는 것은 모든 작업자에게 허용한다(Task `setup.md`와 무관). 지정 관리자는 private 저장소 저장·push 후 공개 포인터를 저장·제출한다. 그 밖의 작업자는 PM에게 요청한다.

## 에셋 추가·수정·삭제 (2026-10-16 PM)

- **추가**: 모든 작업자가 `Assets/` 어디든 새 에셋을 추가할 수 있다(`setup.md`와 무관). 코드(`.cs`)·씬(`.unity`)은 제외하고, 위치는 `Harness/Project/Decisions/asset-placement.md`를 따른다. 진행 중인 다른 Task 구역에 추가하면 담당자에게 알린다.
- **수정·삭제**: 내 작업 구역·배정된 공용 파일이 아닌 기존 에셋은 PM 또는 에셋 관리자(`Harness/Project/Facts/team.md`)만 수정·삭제한다. 그 밖의 작업자는 PM·에셋 관리자에게 요청한다.
- 게임 세팅 값(`Assets/Resources/Settings/GameSettings/`) 변경은 모든 작업자에게 허용한다(프로젝트 `game-settings.md`).

## 수정 방법

- 에디터가 연결되어 있으면 raw YAML 편집 대신 에디터 조작을 쓴다(`editor-targeting.md`).
- 수정 전후로 하이어라키·컴포넌트를 확인하고, 소유한 에셋만 저장한다.
- **저장 여부는 에디터 보고가 아니라 `git status`로 확인한다**(`Pitfalls/scene-not-saved.md`).
- 씬·프리팹 병합 충돌은 AI가 해결하지 않는다. 담당자와 PM이 함께 해결한다.

## handoff

소유한 공용 파일 목록, 각 파일의 저장 상태, 눈으로 확인한 내용을 적는다.
