# Task-20261118-162

- **Title:** 외줄 묘기 밧줄 소실 VFX 교체
- **Type:** feat
- **Assignee:** @RagSpirin
- **Branch:** feat/tightrope-dissolve-burn
- **Base:** dev

## 목적

외줄타기 묘기 씬의 발화 예고 동안 The Ultimate Dissolve and Burn Shader Tool Kit으로 실제 밧줄이 타들어 가게 한다.

## 작업 범위

FireView가 기존 경고 진행률과 소각 범위를 읽어 밧줄의 외형에 적용한다. 씬의 화재 프리팹 인스턴스에 패키지 머티리얼을 지정한다. 발화 확정, 지지면 제거, 네트워크 권한 및 이동은 기존 처리를 따른다.

## 검증

에디터에서 실제 밧줄 메시를 단계별로 렌더링하고 부분 소각, 협동 관문 보호 범위, 이웃 줄, 경고 해제, 중간 시점 진입과 씬 한정을 검사한다. 플레이 테스트는 사용자가 진행한다.
