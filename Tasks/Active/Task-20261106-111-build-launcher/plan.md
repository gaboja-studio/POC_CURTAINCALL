# Task-20261106-111

- **Title:** 버전·로그·설문·개발/라이브 선택 빌드 실행 도구
- **Type:** feat
- **Assignee:** @MoHoDu
- **Branch:** feat/build-launcher
- **Base:** dev

## Context
빌드 담당자가 창 하나에서 게임 버전(기본 현재 값)·로그/설문 사용·설문 등록(링크·시점·대상 버전)·개발자/라이브를 고르고 Windows 빌드를 만드는 실행 파일(`build-helper.vbs`)을 만든다.
개발자 버전은 Development Build + Dev Mode 항상 켜짐(`CURTAINCALL_DEVMODE_BUILD`), 라이브는 둘 다 끔.

## 계획
1. `Scripts/Build-helper/`: WinForms GUI(launch.ps1), 공용 함수(core.ps1, `-SelfTest`), 설문 등록 창(survey-dialog.ps1).
2. `PlaytestBuildRunner.Build`: batchmode 진입점. 버전 저장, 로그/설문 태그 EditorPrefs 반영, Win64 빌드, 결과 JSON.
3. `DevMode.ReadInitial`: 플레이어에서 `CURTAINCALL_DEVMODE_BUILD` 정의 시 항상 켜짐.
4. 가이드 `Harness/Project/Guides/Log-system/build-helper.md`와 기존 빌드 안내 링크.

## 미정·주의
- 작업 당시 메인 폴더(D:\github\POC_CURTAINCALL, dev)에서 Unity 에디터 빌드가 진행 중이라 별도 worktree(`D:\github\github-worktrees\POC_CURTAINCALLeat-build-launcher`, origin/dev 8ebca611 기준)에서 작업했다. 메인 폴더는 건드리지 않았다.
- 도우미는 프로젝트가 에디터에 열려 있으면(Temp/UnityLockfile 잠김) 파일을 바꾸지 않고 닫힐 때까지 기다린 뒤 시작한다.
- Windows 전용. Mac 빌드는 기존 pc-build.md 순서를 따른다.
