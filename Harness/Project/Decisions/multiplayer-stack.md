---
summary: 멀티플레이는 NGO + Unity Multiplayer Services(Host 모드), 이동은 각자 클라·판정은 호스트, 패키지는 PM이 통합 브랜치에 먼저 설치
status: active
updated: 2026-09-30
source: human (2026-09-30 PM @MoHoDu)
---

# 멀티플레이 구성

## 언제 읽나

네트워크 코드를 쓰거나, 동기화 권한을 정하거나, 멀티플레이 관련 패키지를 추가·변경할 때.

## 결정

- 2026-09-30 / PM(@MoHoDu):
  - 솔루션: **Netcode for GameObjects(NGO) + Unity Multiplayer Services**, **Host 모드**. 호스트가 나가면 게임이 끝난다.
  - 권한: **이동은 각 플레이어(클라이언트)가 직접**, **판정(사망·목마 결합/해제·게임 상태)은 호스트가** 한다.
  - 패키지(NGO, Multiplayer Services, Multiplayer Play Mode)는 **PM이 통합 브랜치에 먼저 설치**한 뒤 작업을 인계한다. 작업자는 `Packages/`, `ProjectSettings/`를 수정하지 않는다.
  - 인원: 이번 구현은 **4명 기준**(4명이 모이면 시작).
  - 이번 범위 제외: 거리 기반 음성, 세션 런 데이터 저장.

- 2026-10-02 / PM(@MoHoDu): 기능과 동기화를 같이 만든다 → [feature-with-network.md](feature-with-network.md)

## 이유

5일 안에 끝내는 것이 최우선이다. Unity 공식 경로라 새로 익힐 것이 적고, Host 모드가 "호스트 종료 = 게임 종료"와 그대로 맞는다. 이동을 각자 처리해야 외줄 균형 조작에 지연이 느껴지지 않는다.

## 바꾸려면

패키지·권한 모델 변경은 사람 결정이 필요하다(`Harness/Core/Policies/human-decision.md`). 원문: `Docs/References/tightrope-prototype-brief.md`.
