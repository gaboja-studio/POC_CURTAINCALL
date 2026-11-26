# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

## 2026-11-26
- 정리: 내 작업 중 dev에 들어간 로컬 브랜치 50개·원격 41개, 워크트리 3개 제거. 미병합·미이식 변경이 있는 b087/b089·room-replay-fix 등은 보존.
  - `git worktree remove`가 서브모듈 포함 워크트리를 거부 → 깨끗함 확인 뒤 폴더 삭제 + `git worktree prune`.
- 공연 1 원인: 구조하는 동안 구조자 균형은 '멈춤'일 뿐이라, 끌어올리기가 끝나면 위험 상태 그대로 다시 흔들려 같이 떨어짐. 구조 중 위험 시간 회복 + 성공 시 균형 0으로. 구조 중 톱에 다쳐 취소되는 것은 원래 규칙.
- 공연 2: 처음 값이 너무 밝다는 피드백(눈 아픔) → 발광 2.6→1.1, 번짐 1.6→.55, 조명 6→3, 깜빡임 .22→.08, 카메라 8m 안에서 줄임.
- 공연 5: 배경 씬은 비공개 서브모듈 YAML이라 에디터로 마차 6개 삭제·저장(삭제 546줄만). 서브모듈 기존 무관 수정 10개는 그대로 둠.
- 기물 3: 상점 키트는 셋째 탭(Ledger)에 버튼을 막고, 장부 안내 문구를 코드에 적어 둠 → 키트 2줄 수정(비공개 서브모듈).
- 테스트
  - EditMode `ExpertServiceBookTests` 27/27 통과.
  - PlayMode `W2_` 1차: 15개 중 10 실패. 원인 대부분은 이번 수정 전부터 있던 것:
    - 11-18 첫 습득 해금 이후 처음부터 팔지 않는 공연 기물(구조 장갑·스프링 신발 등)을 테스트가 그대로 구매·선택함.
    - 11-18 입장 전 추가 예약 규칙 이전의 기대값.
  - → 상점 테스트 도우미에서 기물 해금(`UnlockShopItems`) + 기대값 갱신 후 재실행(아래).
  - 2차: 탭 분리로 내가 깨뜨린 2개(공연 기물을 탭 전환 없이 선택: PurchaseDelivery·ShopDescription) 수정 → `W2_` 12/15 통과.
    - 남은 3개는 이번 변경과 무관으로 판단(수정 전 기준선은 따로 돌리지 않음):
      - HandRules·InventoryDurability: 대기실에서 공연 기물 사용 → 11-18부터 연습실 안에서만 사용 가능.
      - PropCollision: 의료실 의자 충돌체.
  - 구조 규칙 변경에 맞춰 `HangRescue_` 기대값 갱신(구조 중 위험 시간 감소, 성공 시 구조자 안정, 취소 시 위험 누적 재개) → 4/4 통과. EditMode `Rescue` 16/16.
  - PlayMode `Rescue` 묶음에서 실패한 4개는 이번 변경과 무관으로 판단(수정 전 기준선은 따로 돌리지 않음):
    - Performance_RescueRequiresSafeTarget: 텔레메트리 CarryLogTracker "Invalid carry snapshot" 예외.
    - W6_Learning: "동료 구출" 문구 기대.
    - CircusMotion: SetUp 실패.
    - RescueGlovesTwoSpawnedPlayers: 착지 받아주기.
- 다음: Chrome 확장 연결 뒤 Confluence 4·5절 기록, 방법 찾기 3건 사용자 승인 대기.
- push(사용자 요청): 서브모듈 `CURTAINCALL_PaidAssets` 브랜치 `fix/m3-stage-shop-feedback` 129a79f9(배경 씬·상점 키트 3파일만) + 본 저장소 같은 이름 브랜치.
  - 제외(에디터 부산물로 판단, 작업 트리에 남김): `TightropeCourse.prefab` 시작점 이동, `CheerGauge/Pretendard-Bold SDF.asset`·`Graffiti_Brush.asset` 글꼴 아틀라스.
  - 서브모듈 main 병합은 PR로 따로 필요.

- 2026-11-26 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
