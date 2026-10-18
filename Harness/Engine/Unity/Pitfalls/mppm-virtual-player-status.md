---
summary: MPPM 복제본의 CLI 표시는 버전마다 다름. 정확한 project-path로 구분하며 내부 API 직접 실행은 Unknown Player 오류를 만들 수 있음
status: active
updated: 2026-10-16
source: Task-20260930-007, Task-20261016-052
---

# MPPM 가상 플레이어가 에디터로 잡힘

## 언제 읽나

Multiplayer Play Mode로 여러 명 테스트하는 중에 저장·검사(`save-work`, `check-work`, `verify-unity.ps1`)가 "이 경로에 연결된 에디터가 2개. 대상이 모호하여 중단."으로 멈출 때. 또는 `unity status`를 경로로 걸러 쓰는 스크립트를 만들 때.

## 증상

- `unity status --json --project-path <p>`가 메인 에디터와 함께 `project: <p>/Library/VP/mppm<id>` 인스턴스를 돌려준다(포트가 다름).
- 경로가 `<p>`로 시작하는 것을 모두 세면 에디터가 2개 이상으로 나온다.

## 원인

unity-cli `1.0.0-beta.12`부터 `unity status`·`unity pipeline list`가 MPPM 가상 플레이어도 보여 준다. 가상 플레이어는 프로젝트 안 `Library/VP/` 아래 복제본이라 같은 경로로 걸린다.

## 대처

1. 스크립트는 `instances`에서 `project`가 대상 경로와 **정확히 같은** 것만 쓴다(`verify-unity.ps1`은 2026-10-02 수정).
2. 수정 전 스크립트를 쓰는 브랜치에서는 저장·검사 전에 MPPM 창에서 가상 플레이어를 끈다.
3. 컴파일만 급히 확인하려면 메인 에디터에 직접 `recompile_status`를 묻는다.

## 확인 방법

가상 플레이어를 켠 채 `pwsh -File Scripts/verify-unity.ps1 -ProjectPath . -Compile`이 `Editor: ready`로 진행되는지 본다.

## 2026-10-16 버전 차이·실행 주의

- CLI beta.6 / Unity 6000.3.25f1 / MPPM 2.0.2에서 `unity status --json`은 메인만 반환했지만, 실제 복제본 경로를 지정한 `unity command eval --project-path <project>/Library/VP/mppm<id>`는 연결됐다. 목록 미표시만으로 종료·연결 불가로 판단하지 않는다.
- 포트는 도메인 리로드 때 바뀌므로 고정하지 않는다. 읽기 전용 eval의 `Application.dataPath`·프로세스 ID로 대상을 확인한다.
- `VirtualProjectsApi.Create/Launch`만 리플렉션으로 호출하면 보조 Editor는 열려도 메인에 `Unknown Player with VP ID` Assert와 `MainPlayerSystems.Listen` NullReferenceException이 생겼다. 공식 Scenario 등록 없이 실행한 경로이므로 권장하지 않는다.
- MPPM 2.x는 Window > Play Mode > Scenarios에서 Additional Editor Instance를 구성해 실행한다. 직접 실행 실패를 게임 오류로 오인하지 말고, 공식 경로로 재확인한다.
- 도구가 만든 검증용 Editor만 정상 종료한다. Library/VP 캐시 삭제나 프로젝트 Scenario 에셋 추가는 별개 변경이며 자동으로 하지 않는다.
