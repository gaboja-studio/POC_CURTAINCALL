---
summary: 외부 에셋을 나중에 받은 뒤 DOTween UI 모듈이 컴파일 입력에서 빠질 때
status: active
updated: 2026-10-15
source: Task-20261014-038
---

# 외부 에셋 수신 후 컴파일 입력 누락

## 언제 읽나

새 worktree에서 `Assets/_ThirdParty`가 미초기화된 채 Unity를 열었거나, 외부 에셋 수신 후에도 DOTween 확장 참조 오류가 남을 때.

## 증상

- 외부 에셋이 없으면 DG·Sequence CS0246. 지정 커밋 수신·DLL 임포트 후 CanvasGroup.DOFade CS1929만 남은 사례가 있다.
- DOTweenModuleUI.cs는 존재하고 AssetDatabase·MonoImporter가 인식하지만, CompilationPipeline.GetAssemblies()의 sourceFiles에는 빠져 있었다.
- ForceUpdate·재귀 재임포트·CleanBuildCache 재컴파일로는 해소되지 않았다.

## 원인

관찰된 직접 조건은 UI 모듈의 컴파일 입력 누락이다. DOTWEEN_NOUI·상위 asmdef 배제 근거는 없었고, Unity 내부의 누락 원인은 미확정이다.

## 대처

1. 새 worktree는 Unity를 열기 전에 `git submodule update --init -- Assets/_ThirdParty`로 지정 커밋을 받는다.
2. 같은 증상이 남으면 대상 프로젝트 경로·Play Mode 종료·미저장 씬 여부를 확인한 뒤 그 에디터만 정상 종료·재실행한다. 다른 worktree의 에디터는 건드리지 않는다.
3. 외부 라이브러리 부재를 게임 코드 우회나 임의 define 변경으로 덮지 않는다.

## 확인 방법

- 재시작 후 UI 모듈이 Assembly-CSharp의 sourceFiles에 포함되는지 확인한다.
- 컴파일 → EditMode → PlayMode 순으로 검증한다. 이 사례는 46/46·8/8 PASS, Console 오류·경고 0이었다.
- 근거: `Integrations/Active/Integration-common-game-systems/result.md`의 2026-10-15 통합 재검사. 사람 공동 테스트와 자동 테스트는 구분한다.
