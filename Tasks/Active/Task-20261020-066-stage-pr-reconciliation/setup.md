# Setup

2026-10-20 PM @MoHoDu의 승인 계획과 현재 사용자 지시에 따른 로컬 연결 작업. 기존 씬/프리팹 소유권과 Task 상태는 유지한다.

## JIRA

- 관련 JIRA: 없음 — PR #192/#193 로컬 충돌 해결. 기존 이슈 재배정 없음.

## 작업 구역

- `Assets/Scripts/MainStage/Tightrope/` — #192 코드/테스트 및 회귀 검증.
- `Assets/Scripts/MainStage/HumanCannon/` — #193 코드/테스트 충돌 해결만. 기존 Task-044 구현/상태 보존.
- `Assets/Resources/Prefabs/Objects/ETCs/TightropeArtKit/`, `Assets/Resources/Prefabs/Objects/ETCs/PerformanceArtKit/` 및 각 폴더 `.meta` — #192 기존 리소스 적용.
- `Assets/Resources/Models/Materials/PerformanceArtKit/`, 상위 `Materials.meta`, `PerformanceArtKit.meta` — 프리팹 의존 재질 적용.
- `Assets/Resources/Prefabs/Objects/Interactables/Tightrope/TightropeCourse.prefab`, `Assets/Resources/Settings/GameSettings/Tricks/TightropeSettings.asset` — #192 리소스 연결 변경만. 충돌 시 자동 YAML 해결 금지.
- Task 문서: 이 Task 폴더.

## 배정된 공용 파일

- `Assets/Scripts/Systems/Player/PlayerMover.cs` — #193 공중 제어 옵션 충돌, 기본 false 보존.
- `Assets/Scripts/Systems/Network/PlayerSync/NetworkPlayer.cs` — RestartVersion/초기 조건 결합 검증, 확인된 회귀의 최소 수정만.
- `Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity` — 이번 작업의 사용자 Editor 예외 승인에 한정. 기존 Task-044 소유권 자체는 유지.

## 이번 작업의 Editor 예외 및 에셋 저장 결정

- `Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity` — 이번 작업에 한해 사용자 Editor 예외 승인으로 기존 #193 변경 재적용. YAML 직접 병합·테스트 완화·새 밸런스 결정 없음. 기존 Task 소유권 유지.
- 그 밖의 씬 수정 금지 유지. 기존 public 재질2개는 2026-10-20 사용자의 기존 에셋 전체 저장·dev 병합 요청에 따라 현재 변경 그대로 저장한다.
- `Assets/_ThirdParty/` — 같은 사용자 요청으로 기존 재질219개를 private에 저장하고 최신 main과 합쳐 push한 뒤 공개 포인터를 저장한다. 임의 폐기·강제push·보호 규칙 우회 금지.
- `ProjectSettings/`
- `Packages/`

## 검증 및 인계

- 기존 테스트 씬을 참조하므로 새 Unity 씬·폴더 생성은 불필요.
- PM의 승인 계획/확장 및 현재 계속 진행 지시로 로컬 연결 작업을 실제 전달했다.
- 자동 배정 도구의 커밋·push·PR 생성은 승인 밖이므로 실행하지 않는다.
- 전체 소유권 이관/원격 인계 완료를 주장하지 않는다. 구역 검사가 기존 044 소유와 충돌하면 FAIL로 보고한다.
- 2026-10-20 사용자가 이 setup.md에 대한 해시 기반 편집을 명시적으로 허용했다.

## 테스트 씬 처리

- 결정: 기존 씬 유지. 새 씬 생성/삭제 없음.
- 결정자·날짜: PM 승인 범위, 2026-10-20.
