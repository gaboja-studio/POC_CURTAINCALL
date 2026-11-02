# Task-20261102-096

- **Title:** 혈흔 설정으로 피 묻은 의상도 켜고 끄기
- **Type:** fix
- **Assignee:** @MoHoDu
- **Branch:** fix/settings/injury-and-blood
- **Base:** dev

## Context
혈흔 설정(게임 설정 신체 손상 · 혈흔 연출, 이 PC의 '혈흔 연출 표시')이 피 튀김만 켜고 끄던 것을 부상에 따른 피 묻은 의상(RabbitSurfaceStates Light/Heavy)까지 함께 켜고 끄게 한다(사용자, 2026-11-02).
- 꺼짐: 부상 상태와 상관없이 처음 의상(Clean) 유지.
- 켜짐: 지금처럼 부상 수에 따라 Light/Heavy.

## 계획
1. `HumanoidPlayerPresentation.ApplySurface`에서 `InjuryBloodEffects.IsVisible`이 거짓이면 Clean으로 고정한다. 매 프레임 호출되므로 설정 변경이 즉시 반영된다.
2. 설정 툴팁·주석 갱신, PlayMode 검사 추가(기존 의상 검사는 개인 설정을 켠 상태로 고정).

## 미정·주의
- 꺼진 상태에서 잘린 조각도 절단 당시 의상(Clean)을 그대로 가진다. 나중에 켜도 이미 떨어진 조각은 바뀌지 않는다.
- 공용 파일 `HumanoidPlayerPresentation.cs`(캐릭터 표현) 수정.
