---
summary: 로비를 생략한 다중 프로세스 진단에서 클라이언트 출발점 배치 관리자가 빠져 공연 시작이 멈춤
status: active
updated: 2026-11-17
source: Task-20261116-144 v6 동일 빌드의 4인 legacy/fixed 대조
---

# 로비를 생략한 진단의 공연 진입

## 언제 읽나
- Start에서 LAN 접속 후 DevStartLauncher로 바로 공연을 열었는데 원격자가 기다리다가 나가야 호스트 공연이 시작될 때.

## 원인
- 일반 Lobby의 SpaceLinkHub는 각 peer에서 SpaceLinkDirector를 생성한다. 진단에서 호스트만 DevStartLauncher를 호출하면 클라이언트의 이 초기화가 생략된다.
- director의 sceneLoaded → CampaignPerformanceEntry.Prepare → 로컬 출발점 배치가 연결되지 않는다.
- 호스트 CampaignTightropeAdapter는 모든 접속자의 출발점 도착 전 보상/공연을 열지 않는다. 이는 정상 보호 조건이므로 timeout을 늘리거나 검사를 제거하면 안 된다.

## 확인·수정
- 결합 predicate의 timeout만으로 씬 로드 실패라고 판단하지 않는다. 씬/UI/코스/로컬 캐릭터/입장 관리자/배치/보상 열림을 각각 기록한다.
- 로비를 생략하는 진단은 모든 peer에 일반 로비와 같은 CampaignRun/SpaceLinkDirector 초기화를 하고, Start 구독 완료 뒤 공연을 연다.
- v6 부정 대조군: 클라이언트 씬/UI/코스/Local=true, director/entry/placed/rewards=false. 수정 경로는 네 명 모두 공연 진입, 16개 off/on 측정 구간 전부 유효.
- 추가 초기화는 측정 전용 코드에 한정했다. 일반 게임의 씬/배치 규칙을 변경한 수정과 구분한다.
