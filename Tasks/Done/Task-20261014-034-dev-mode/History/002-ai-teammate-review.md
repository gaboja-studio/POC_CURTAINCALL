# 테스트 AI 동료 검토 (2026-10-14, 구현하지 않음)

결론: 가능하지만 **기반 작업이 공용 파일 여러 곳에 걸치고, 묘기별 행동은 묘기마다 따로** 만들어야 한다. 더미 공간·실제 묘기가 정해진 뒤 별도 Task로 권장.

필요한 변경:
1. **가짜 플레이어 생성** — 지금 캐릭터는 접속한 클라이언트마다 승인 단계에서 만든다(`NetworkSessionManager.cs` `CreatePlayerObject`). AI는 호스트가 직접 스폰해야 하는데, 그러면 호스트가 주인이 된다.
2. **"내 캐릭터" 분리** — `NetworkPlayer`는 `IsOwner`면 `Local`로 잡고(`NetworkPlayer.cs:215`) 입력·이동·균형을 켠다(`ApplyOwnership`). 호스트 소유 AI가 `Local`을 덮어쓴다. `IsBot` 같은 구분이 필요하고, `NetworkPlayer.Local`을 쓰는 15개 파일(카메라·외줄·업기·결과 등)이 영향 범위.
3. **인원 집계에 포함** — 세션 인원(`ConnectedClientsIds`), 런 준비 인원(`CampaignRun.cs:265`), 외줄 결과 보고(`TightropeResults.cs:256`, 접속자만 받음)가 모두 클라이언트 ID 기준이라 AI가 빠진다.
4. **입력 대체** — 입력은 `PlayerInputReader` → `PlayerInputFrame` → 조작 규칙 구조라, AI 두뇌가 `PlayerInputFrame`을 만들어 넣는 방식이 가장 작다.
5. **묘기별 행동** — 외줄 차선·점프, 업기 협력 등 묘기마다 행동 규칙이 필요(가장 큰 비용, 묘기 로직 확정 후).

규모 추정: 1~4 기반은 공용 파일 5~8개(NetworkPlayer, NetworkSessionManager, CampaignRun, 카메라·뷰, 결과) 수정의 중간 규모 Task 1개. 5는 묘기당 Task 1개.
