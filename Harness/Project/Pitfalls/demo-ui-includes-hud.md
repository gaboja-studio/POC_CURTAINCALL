---
summary: 이름에 Demo가 붙은 줄타기 UI를 통째로 끄면 HUD·균형 게이지까지 사라진다. 연결 모드에서 시연용 UI를 숨길 때
status: active
updated: 2026-10-18
source: Task-20261017-057
---

# 시연 UI 안의 HUD

## 언제 읽나

공용 흐름으로 공연 씬을 불러오면서 접속·재시작용 시연 UI를 숨기려 할 때.

## 증상

Task-057 연결 모드에서 TightropeDemoUI를 끄자 남은 시간·진행·균형 게이지가 함께 사라졌다.

## 원인

TightropeDemoUI는 접속 패널과 실제 플레이 HUD를 한 컴포넌트에서 그린다. 이름만으로 시연 전용이라고 판단했다.

## 대처

컴포넌트 전체를 끄지 않고 접속·전원 재시작 같은 패널만 막는다(CampaignPerformanceEntry는 PlayerCommandDebugHud·NetworkSessionTestUI·HumanCannonTestHud만 끈다).

## 확인 방법

`PerformanceLinkPlayTests.Managed_KeepsOriginalTightropeUiAndHudVisible` 실행 후 연결 화면에서 남은 시간·게이지 표시를 눈으로 확인한다.
