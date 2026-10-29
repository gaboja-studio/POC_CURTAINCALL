# 원본 무대 구성 확대·높인 연습 링 복구 — 2026-10-28

## 사용자 정정과 원인

링 높임/계단을 광범위 원복에 포함한 것이 잘못이었다. 무대는 원본의 공간 구조·배관 비례·커튼 관계를 유지해야 했으나 개별 소품을 새 벽 안에 배치하면서 분위기가 바뀌었다. 이번에는 원본을 직접 렌더링하고 Task-060 Layout/global.md·east-wing.md와 현재 화면을 함께 대조했다.

## 현재 결과

- 무대 원본: S06 바닥/커튼 전면, S07 전체, Mood/Tunnel 전체(원본 Vault·SteelRibs·WallDressing·FloorWalkway·StringLights·Cables·Lights·Rags·StageProscenium·LobbyDoorValance), 원본 커튼/가구, 기존 조명 묶음을 사용한다.
- 하나의 좌표 프레임에서 X 65/42, Y 13/7.6, Z 14/8배로 확대한다. 소품/파이프 조각마다 간격만 벌리지 않아 기존 연결부와 상대 배치를 보존한다. 원본 머티리얼을 그대로 참조한다. 라이트의 색·각도는 유지하고 범위/세기는 확대 비율로 보정한다.
- 원본 8개 커튼 전면과 실제 천 커튼, 굵은 배관/밸브, 녹슨 아치·벽 표면과 조명 리듬을 유지한다. 새로 만들었던 측면 방/높은 평벽·얇은 연속 배관·마키·바닥 러너 설계 코드는 제거했다.
- 현재 전체 외곽 길이65m·폭14m·아치 높이13m와 전이부 유지. 전이부도 원본 Vault와 바닥/벽 머티리얼을 사용한다. 단면 차이는 같은 원본 표면의 막음판으로 폐쇄한다. 집결과 밤 셔터 참조는 유지한다.
- 링: 기존 별무늬 링을 0.6m로 올리고 치마/DeckCollider 높이를 맞춘다. 남/서쪽에 폭2.2m, 단높이0.2m의 3단 계단과 경사 충돌을 둔다. 대포 받침·착지 매트·테이프·네트도 높이에 맞추고 받침 충돌을 연결한다. 사다리의 얇은 뒤쪽 충돌은 유지한다.
- 전용 실행 함수 `RestoreSourceStageAndRaisedRing`은 무대와 링만 갱신한다. 전문가·기계실·물류를 다시 만들지 않는다.

## 검증과 증거

- 원본 직접 캡처: `.harness/local/task075-source-faithful/source-tunnel-reference.png`.
- 원본 대응 대조: 주요 복제 계층 Transform 1,534개, Renderer 854개, Material 슬롯896개. 전체 확대 행렬을 제외한 좌표/계층/재질 차이 0. 원본 Snapshot 보존 검사도 통과했다.
- 실제 PlayerMover 입력/물리: 남쪽 계단 상승, 서쪽 계단 상승, 남쪽 하강, 양쪽 사다리 앞 통과, 무대 전이부 이동, 본통로 이동 7/7. 계단은 점프 없이 올라 발높이0.68m(링 표면0.6m+이동체 간격)에 도달한다. 원본 구성을 복구한 통로는 약54m 이동했다. 최종 JSON은 `motion-results.json`.
- 렌더 메시 외벽 표본: 9지점×170방향=1,530개 중 200m 내 메시 미검출0. 표본 검사/실제 화면 확인 결과이며 모든 틈의 수학적 부재를 보증하지 않는다.
- 이번 요청 외 23개 루트 직렬화 객체 차이0. 로비·전문가·기계실·물류 포함. 기존 LogisticsFrontages의 39개 Transform GlobalObjectID도 보존했다. `unaffected-comparison.json`, `source-comparison.json`, `enclosure-results.json` 참조.
- 최종 플레이 시점: `source-faithful-tunnel.png`, `source-faithful-lobby-view.png`, `source-faithful-stage-curtain.png`, `raised-ring-stairs-final.png`.
- 컴파일/필수 check-work는 log.md에 기록한다. 씬 저장, Play 종료 후 마무리한다. commit/push 없음.

분위기 최종 승인·4인 접속·공연 왕복은 미검증이다. 본 보고서가 이전 보고서의 무대 재제작 및 링 원복 설명을 대체한다.
