# 컨베이어 동선과 대기 재설계

## 조사에서 적용한 기준

- [GDC Level Design Workshop — Invisible Intuition](https://gdcvault.com/play/1025529/Level-Design-Workshop-Invisible-Intuition): 시야와 조명으로 필요한 선택을 먼저 읽게 한다. 발사 방향과 움직이는 봉은 시야를 막는 벽을 더하지 않고 낮은 난간으로 낭떠러지와 분리한다.
- [The Level Design Book — Flow](https://book.leveldesignbook.com/process/layout/flow), [Wayfinding](https://book.leveldesignbook.com/process/blockout/wayfinding): 바닥 높이, 도달 거리, 통로 너비와 시각 단서가 실제 이동 규칙에 맞아야 한다. 하차장은 기존 지지면 안의 17.5cm 연결단 4개를 쓰고, 같은 위치를 덮던 낮은 단차 두 개를 제거한다. 단독 또는 파티의 필수 장치는 외줄을 밟기 전에 같은 위치에서 작동한다.
- [Nintendo Iwata Asks — Applying a Single Idea to Both Land and Sky](https://www.nintendo.com/en-gb/Iwata-Asks/New-Super-Mario-Bros-Wii/Volume-2/5-Applying-A-Single-Idea-To-Both-Land-And-Sky/5-Applying-A-Single-Idea-To-Both-Land-And-Sky-233080.html): 플레이어가 따라갈 만한 단서는 실제 안전한 동작과 일치해야 한다. 대포 아래나 낭떠러지 쪽에 새로운 착지 면·진행 화살표를 만들지 않는다.
- [Mike Booth — The AI Systems of Left 4 Dead](https://valvearchive.com/Presentations/AIIDE%202009/ai_systems_of_l4d_mike_booth.pdf): 강도가 계속 최대가 되지 않도록 호흡 구간을 둔다. 여기서는 조작상 필요한 대기 발판에만 일정 시간을 주고 첫 발사 또는 시간 만료 뒤 다시 공격한다.

## 변경 및 검증 구역

- 복사 씬의 재사용 가능한 낮은 연결단과 실제 조작 레버를 유지했다. 중복 스위치 배치 두 개, 폐기된 레버 지지 구조, 겹친 단차, 비활성화되어 대체된 벽체를 제거했다. 실제 카메라 확인 뒤 외줄 양옆의 작동하지 않는 톱 반출 상자 4개를 제거하고 하차장 뒤와 옆 낭떠러지 경계를 낮은 난간으로 표시했다. 원본 씬과 공유 배칭 메시에는 이번 배치 편집을 적용하지 않았다.
- 실제 컨베이어 이동 봉 6개가 약 5.49초 간격으로 지나간다. 플레이어당 발판 진입 후 16초, 그 안에 첫 탑승을 하면 그때부터 최대 13초까지 보장한다. 좌석에서 발사하면 바로 종료되며 내렸다 다시 타거나 라운드 중 돌아와도 초기화되지 않는다. 조준 중 대기에 들어가면 새 타격과 투척 준비를 취소한다.
- 일반 달리기 속도 6.6→6 m/s, 벽 뒤 이동 24→20 m/s, 최대 압박 속도 배율 1.45→1.30. 대기 중인 대상만 있는 해당 출현구는 벽 뒤에서 대기할 수 있다.

## 검증 결과

- Windows 개발 빌드에서 3인 LAN 연결. 대포대 도착 후 9.36초 기다려 탑승했고, 좌석에서 11초 이상 기다린 다음 실제 입력으로 발사·이동 봉 포획·점프 하차까지 성공했다. 하차 시 생존, 지상 상태였고 두 동료는 이미 탈락했다.
- 별도 3인 LAN 연결에서 탑승한 채 발사하지 않았다. 대기 창이 끝난 뒤 추격자가 실제 컨베이어 벽 출현점으로 이동해 탑승자를 포획했다. Unity 회귀 검사에는 늦은 첫 탑승, 이탈, 발사, 재탑승으로 대기 창을 갱신할 수 없는 경우가 포함된다.
- 4인 LAN 연결에서 원격 참가자가 하차장부터 레버까지 걸어가 F로 전원을 켰다. 호스트와 다른 참가자도 전원 변경을 받았다. 동일한 참가자가 실제 균형 입력으로 외줄 끝의 착지대까지 이동했다.
- LAN은 각 구간 진입 위치를 맞춘 뒤 실제 플레이어 입력을 사용했다. 위 실기는 최종 하차장 바깥쪽 난간 추가 전이다. 최종 저장 씬에서 하차장쪽 바닥 770점과 실제로 걸은 경로 93점의 난간 비침범, 대포 발사 궤적, 계단 다섯 구간 134단의 중심 지지를 편집 물리로 확인했다. 입구부터 엔딩까지 연속 재실행한 결과는 아니다.
- EditMode: 탈출로 453/453, 공통 진행 712/712. `check-work`의 안전·문서·컴파일·공통 진행 검사 통과. 개발 빌드는 완료됐다.
