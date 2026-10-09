# Unity Assets 폴더 배치 규칙

마지막 구조 점검: 2026-09-30 · 기준: `integration/tightrope-prototype` 작업 트리의 `Assets/` (콘텐츠 배치와 실행 동작은 미검증)

## 적용 원칙

- 이 문서는 **현재 확인된 위치**와 **향후 배치 기준**을 구분한다. 게임 기능·에셋의 존재를 폴더 이름만으로 추정하지 않는다.
- 새 폴더·에셋은 Task `setup.md`의 작업 구역과 Integration의 공용 파일 소유 표를 먼저 확인한다. 이 문서는 수정 권한을 부여하지 않는다.
- `Resources/`의 에셋은 `Resources.Load` 등 경로 기반 로딩 대상으로 빌드에 포함될 수 있다. 런타임 로딩 요건이 없는 에셋을 관성적으로 넣지 않는다. 경로는 `Resources/` 아래 상대 경로이며 확장자를 제외한다. **현재 존재하는 폴더가 곧 사용 승인이나 로딩 요구사항은 아니다.**
- Unity 에셋·폴더를 옮기거나 이름을 바꿀 때는 Unity 에디터에서 `.meta`/GUID와 씬·프리팹 참조를 보존한다. 삭제·이동·새 공용 구조는 PM 및 필요한 사람의 승인을 먼저 받는다.

## 확인된 루트 계층

| 경로 | 현재 상태 | 배치 기준·주의점 |
|---|---|---|
| `Assets/Resources/` | 하위 분류 폴더만 있음 | 명시적인 Resources 경로 로딩이 필요한 콘텐츠를 해당 분류에 배치. 다른 에셋의 기본 보관 장소로 쓰지 않음 |
| `Assets/Scenes/` | `SampleScene.unity` (템플릿 씬) | **모든 씬은 여기에만 둔다.** 공용 씬은 파일별 소유 Task를 지정하고 변경 |
| `Assets/Scenes/Tests/<Feature>/` | `NetworkSession/`, `PlayerControl/`, `Tightrope/`, `Piggyback/` 테스트 씬 (PM이 Task별로 생성) | Task 테스트 씬. 테스트 전용 프리팹(더미·임시 캐릭터·테스트 UI)은 같은 폴더의 `Prefabs/`에 둔다 |
| `Assets/Settings/` | PC/Mobile URP 렌더·파이프라인 에셋, Volume Profile | 여러 기능에 영향을 주는 공용 설정. 소유 Task와 사람 결정 없이 수정하지 않음 |
| `Assets/InputSystem_Actions.inputactions` | 입력 액션 에셋 | 공용 입력 계약. 작업 폴더에 들어 있지 않으며 수정 시 공용 파일 배정 필요 |
| `Assets/Readme.asset`, `Assets/TutorialInfo/` | 템플릿 잔여물(튜토리얼 이미지·Editor 스크립트 포함) | 게임 콘텐츠·기능 코드 구역으로 간주하지 않음. 보존/정리는 별도 결정 |
| `Assets/_ThirdParty/` | private submodule(`CURTAINCALL_PaidAssets`). `Demigiant/`(DOTween·Pro·DemiLib) | **외부 에셋은 무료·유료 모두 여기에만.** PM만 추가·수정(`Harness/Project/Decisions/third-party-assets.md`) |
| `Assets/Scripts/` | `Network/Session/`, `Player/`, `Tightrope/{Rope,Piggyback}/` 폴더만 있음(`.gitkeep`), 게임 코드 없음 | 기능별 Task 코드 위치. **Unity C# 코드는 모두 여기에만 둔다.** 실제 생성·배정 전에는 작업 공간이 아님 |

위치 결정 근거: `Harness/Project/Decisions/asset-placement.md`.

## Resources 하위 분류

2026-09-30 기준 아래 폴더에는 `.gitkeep`과 폴더 `.meta`만 있으며 실제 콘텐츠 에셋은 없다. 오른쪽은 **앞으로 해당 종류의 에셋이 필요할 때 적용할 분류 기준**이다.

| 상대 경로 (`Assets/Resources/` 아래) | 배치 기준 |
|---|---|
| `Animations/` | 런타임 경로 로딩이 필요한 애니메이션 클립·컨트롤러 등. 프리팹 자체는 `Prefabs/`로 분리 |
| `Input/` | 폴더만 있음 — 플레이어 조작 입력 에셋(`.inputactions`). 루트의 템플릿 `InputSystem_Actions`와 분리 |
| `Effects/` | 런타임 로딩 대상 효과 리소스(예: VFX·파티클 에셋). 효과를 완성한 프리팹이면 `Prefabs/`의 쓰임에 맞춰 결정하고 중복 보관하지 않음 |
| `Images/` | 경로 로딩 대상 이미지·스프라이트. UI 프리팹과 이미지 원본을 구분 |
| `Models/` | 경로 로딩 대상 모델 원본. 모델을 사용하는 프리팹과 구분 |
| `Sounds/` | 경로 로딩 대상 오디오 클립. 재생용 컴포넌트를 가진 프리팹과 구분 |
| `Prefabs/Characters/Players/` | 플레이어 캐릭터 프리팹 |
| `Prefabs/Characters/NPCs/` | 적이 아닌 비플레이어 캐릭터 프리팹 |
| `Prefabs/Characters/Enemies/` | 적 캐릭터 프리팹. 캐릭터의 적대 여부가 미정이면 분류 결정을 요청 |
| `Prefabs/Objects/Obstacles/` | 진행을 막거나 회피 대상인 환경 오브젝트 프리팹 |
| `Prefabs/Objects/Tools/` | 사용 도구 오브젝트 프리팹. 상호작용 여부만으로 `Interactables/`와 중복하지 않음 |
| `Prefabs/Objects/ETCs/` | 다른 Objects 분류에 맞지 않는 **승인된 예외**만 임시 배치. 용도와 재분류 시점을 기록 |
| `Prefabs/Objects/Interactables/` | 도구·장애물이 아닌, 상호작용 자체가 주된 역할인 오브젝트 프리팹 |
| `Prefabs/Objects/Decorations/` | 기능 상호작용 없이 환경을 꾸미는 오브젝트 프리팹 |
| `Prefabs/Controllers/` | 캐릭터·오브젝트·UI 자체가 아닌 제어용 프리팹. 스크립트 파일은 이곳에 두지 않음 |
| `Prefabs/UIs/` | UI 프리팹. UI용 이미지 원본·기능 코드는 각각 종류/코드 작업 구역에 둠 |

하나의 에셋이 여러 분류에 맞거나 `Resources` 경로 로딩 자체가 불필요하다면 임의로 `ETCs`에 넣지 않는다. 담당 Task가 용도와 참조 방식을 적어 PM과 위치를 확정한다. 공용 프리팹은 같은 폴더에 있어도 Integration 소유 표에서 **파일 단위**로 배정한다.

## 기능 스크립트 작업 공간

- 루트 `Scripts/`는 검증·운영 도구이고 `Assets/TutorialInfo/Scripts/`는 튜토리얼 템플릿 코드다. 기능 코드의 기본 위치로 사용하지 않는다.
- PM은 실제 Task가 정해지면 Unity에서 `Assets/Scripts/<Feature>/`를 기능 단위(PascalCase)로 만들고 `.meta`와 함께 관리한다. `Assets/Scenes/Tests/<Feature>/`의 테스트 씬도 필요할 때 만든다(테스트 전용 프리팹은 그 안의 `Prefabs/`). `setup.md`에는 폴더 경로를 `/`로 끝나게 적고, 작업자가 수정할 정확한 영역을 지정한다. 같은 Integration에서 동시 Task가 같은 기능을 다뤄도 소유 폴더·파일이 겹치지 않도록 나누거나 순차 진행한다.
- 예: 한 Task에 `Assets/Scripts/CurtainOpen/`, 다른 Task에 `Assets/Scripts/StageLighting/`를 배정한다. 어느 쪽도 상대 Task 폴더를 고치지 않는다. 둘의 연결이 필요하면 PM이 **연결 Task**를 만들어 필요한 양쪽 구역과 공용 파일을 명시한다.
- 이미 `dev`에 합쳐진 기능의 수정은 현재 Task `setup.md`에 해당 경로가 재배정된 경우에만 허용한다. 공유 `Core`·`Common` 폴더나 asmdef는 실제 의존성과 소유자가 확정될 때 별도 결정한다.
- 이 절은 [코드 폴더 정책](../../Harness/Engine/Unity/Policies/code-folders.md)과 [에셋 소유 정책](../../Harness/Engine/Unity/Policies/asset-ownership.md)을 적용한 예시다. 충돌하면 해당 정책과 Task `setup.md`를 우선한다.

## 변경 시 재점검 체크리스트

1. 새 Task·에셋 유형·로딩 방식·폴더 변경 요청이 생기면 실제 `Assets/` 계층, `.meta`, 관련 참조를 **읽기 전용**으로 확인한다. 기존 위치는 위 표와, 미생성 위치는 `planned` 표기와 대조한다.
2. 경로 로딩이 필요한지, 기능 소유인지 공용 파일인지, 복수 분류/예외인지 판단한다. `setup.md`와 Integration 소유 표에 없는 구역·공용 파일은 PM에게 먼저 배정을 요청한다. 플레이어 체감 선택·에셋 이동/삭제는 별도 승인을 받는다.
3. 승인된 경로만 Unity에서 만들거나 변경하고 `.meta`/GUID·씬/프리팹 참조를 확인한다. 승인 전에는 실제 구조를 바꾸지 말고 제안과 이유만 기록한다.
4. 이 문서의 점검일·실제 경로/역할·예외 사유와 만료/재분류 시점을 갱신하고 `Docs/Domains/assets-structure.md`의 담당 경로를 동기화한다. 해당 Task `handoff.md`에 변경 이유, 검증 결과, 미해결 결정을 남긴다.
5. 범위 검사와 문서 검사(`Scripts/verify-scope.ps1`, `Scripts/verify-fast.ps1`) 및 실제 트리 대조를 수행한다. Unity 변경 시에는 별도의 컴파일/테스트 단계도 수행하고 생략·실패를 PASS로 쓰지 않는다.
