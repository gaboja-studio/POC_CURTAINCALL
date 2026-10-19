# Setup

## JIRA

- 관련 JIRA: 없음 — PM의 로컬 직접 작업 준비. 외부 이슈는 생성하지 않았다.

## 작업 구역

- Unity 코드·씬·프리팹·모델·머티리얼·애니메이션·리소스: `Assets/`
- 문서: `Docs/`
- 하네스: `Harness/`
- 도구: `Scripts/`
- 프로젝트 설정: `ProjectSettings/`
- 패키지: `Packages/`
- CI·훅·공용 도구 설정: `.github/`, `.githooks/`, `.claude/`, `.mcp.json`
- 작업 지침: `AGENTS.md`, `CLAUDE.md`
- 통합 기록: `Integrations/Active/Integration-humanoid-characters/`
- Task 문서: 이 Task 폴더 (자동 포함)

2026-10-19 PM @MoHoDu의 명시적 요청으로 이 Task에 한해 넓은 작업 구역을 승인한다. 스크립트·리소스·씬·문서의 추가/수정/삭제를 허용한다. 프로젝트의 전역 권한 정책이나 AI 실행 권한 모드는 변경하지 않는다. 실제 변경은 휴머노이드 모델 교체·입장 순서 캐릭터 배정·부상 절단과 관련된 것으로 한정한다.

- 전용 worktree: D:/github/POC_CURTAINCALL-humanoid-characters
- 작업 브랜치: feat/humanoid-characters
- 제출 대상: integration/humanoid-characters → dev. 원격 통합 브랜치 등록은 저장/제출 요청 후 수행한다.
- 테스트 씬 예정 경로: `Assets/Scenes/Tests/Systems/HumanoidCharacters/`
- 새 코드 예정 경로: `Assets/Scripts/Systems/Player/` (기존 모델·부상 기능 확장 우선)
- 모델 래퍼 예정 경로: `Assets/Resources/Prefabs/Characters/Players/Models/`
- 외부 모델 원본: `Assets/_ThirdParty/` private submodule. 외부 에셋은 공개 저장소에 넣지 않는다.

## 배정된 공용 파일

- `Assets/Resources/Prefabs/Characters/Players/`
- `Assets/Resources/Settings/GameSettings/`
- `Assets/Scripts/Systems/Player/`
- `Assets/Scripts/Systems/Network/`

통합 meta에도 동일한 소유 범위를 기록한다. 다른 진행 중 Task의 구역까지 자동으로 소유권을 가져오는 승인은 아니다. 겹치는 파일이 실제로 필요하면 담당자와 조율한다.

## 수정 금지

- 메인 dev 체크아웃과 다른 worktree의 파일·미저장 Unity 상태.
- 다른 Task 문서 및 담당자 작업을 무단 덮어쓰기/삭제하는 행위.
- 비밀값·토큰 추적/출력, 외부 에셋 공개 재배포, .meta/GUID 누락.
- 별도 요청 없는 커밋·push·PR·병합·reset/discard/force.
- 작업 편의를 위한 공통 검증·안전장치의 무력화.

Packages/ProjectSettings/게임 규칙·모델 선정·절단 연출 등 결과에 영향을 주는 선택은 작업자인 PM이 결정한다. AI는 포괄적 파일 수정 권한을 개별 UX·밸런스 결정 승인으로 해석하지 않는다.

## 인계 전 체크 (PM)

- [x] 담당자 @MoHoDu, PM 권한·관련 JIRA 없음 기록
- [x] Unity에서 전용 빈 테스트 씬을 만들고 저장했다 (.meta 생성)
- [x] 전용 테스트 폴더를 만들었다 (빈 폴더에는 .gitkeep)
- [ ] plan.md 공동 테스트 초안을 구현 전에 확인했다

이 Task는 PM 본인의 로컬 직접 작업이다. 자동 커밋·push가 포함되는 assign-task.ps1은 실행하지 않았다. 로컬 작업 브랜치와 Task 문서만 준비하며 원격 배정/제출 완료로 취급하지 않는다.

## 테스트 씬 처리

- 결정: 미정 (유지 / 삭제)
- 결정자·날짜: 기능 완료 후 PM 결정
