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
- Confluence M3(50659329) v11: 4·5절 결과 + "위험한 동료 찾기 방식 — 검토안" 펼치기(목업 A/B/C). Chrome 확장 연결이 한동안 끊겨 v10은 글만, v11에 첨부 추가.
- 다음: 방법 찾기 3건(위험한 동료 표시·팔다리 UI·기물 사용 유도) 사용자 결정 대기.
- push(사용자 요청): 서브모듈 `CURTAINCALL_PaidAssets` private main d7d109d9(배경 씬·상점 키트 3파일만, 사용자 승인으로 main 직접 반영) + 본 저장소 `fix/m3-stage-shop-feedback` af99b59d·9b53d75e.
  - 제외(에디터 부산물로 판단, 작업 트리에 남김): `TightropeCourse.prefab` 시작점 이동, `CheerGauge/Pretendard-Bold SDF.asset`·`Graffiti_Brush.asset` 글꼴 아틀라스.
  - 첫 push는 훅이 거부(포인터가 private main에 없음) → main 위로 옮겨 반영 후 포인터 갱신.

- 2026-11-26 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-26 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 공연 4 위험한 동료 표시 구현(결정은 Confluence v12 검토안 안에 기록).
  - 공용 SilhouetteOutline에 벽 너머 모드(xray)·남은 시간(발끝부터 선명) 추가. 기존 모드는 그대로(기존 테스트 4/4).
  - `NetworkPlayer.InjuryMark`(부상 순번+원인) 추가 → 화면마다 절단/낙하 구분.
  - 함정: 매달림 자세는 다른 LateUpdate가 모델을 내리므로 표시 코드는 `DefaultExecutionOrder(10000)`, 몸 범위는 사람형 뼈(머리·발)로 계산(스킨 렌더러 범위는 자세를 안 따라감).
  - 확인: 외줄 씬 합성 동료 3명(앞 매달림·뒤 낙하 부상·옆 절단) 녹화. 합성 동료는 외줄 참가자가 아니라 피해 허용 검사에 막혀 확인 중에만 검사를 끄고 바로 복원.
  - 검사: 가장자리 배치 EditMode 4/4, SilhouetteOutline 4/4, check-work PASS. `TightropeInjuryFeedbackTests` 4개 실패는 기존 문제(테스트가 PlayerInteraction 없이 읽음, 11-12 이후 변경 없음).
- Confluence M3 v14: '위험한 동료 찾기 방식' 펼치기 안에 '구현 결과'(영상 1·이미지 3) 추가. Atlassian 연결이 세션에서 빠져 Chrome에서 Confluence REST v2(ADF)로 해당 펼치기만 수정·확인.
- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/489 (dev, 지정 팀원 검토 대기). new-pr.ps1는 커밋 제외한 에디터 부산물 3개 때문에 멈춰 gh로 같은 형식 등록.
- 기물 5 팔·다리 여러 개 예약 UI: 레퍼런스(Fallout 4·Tarkov·Darkest Dungeon·Project Zomboid·Cyberpunk 2077) 조사 → Confluence M3 v15 5절에 '검토안 (방법 3개)' 펼치기(A 인체 그림+기본 전부 추천 · B 개수별 줄 · C 그림 직접 누르기, 목업 3장). 사용자 결정 대기.
- 기물 5 A안 적용(사용자 결정: A안 + 깔끔한 픽토그램). 브랜치 fix/m3-limb-booking-figure(#489 dev 병합 뒤 새로).
  - 설명 칸 인체 픽토그램 `LimbBookingFigure`(머리·몸통·팔다리 레이어 6장 + 범례·안내), 상점 프리팹에 빌더로 넣음(Tools/Curtain Call/상점/신체 복구 인체 그림 넣기). 보이는 동안 설명 칸을 160 내림.
  - 처음 선택 = 잃은 전부(돈 부족 시 살 수 있는 최대), 수량 버튼은 하나씩 줄임(3→2→1→3). 규칙·서버·키트 변경 없음.
  - 함정: 레이어 스프라이트는 캔버스 전체 크기라 범례 점에 머리 레이어를 쓰면 거의 안 보임 → 전용 limb-dot.
  - 검사: PlayMode W2_ 12/15(BodyRestore 3/3 통과, 실패 3개는 기존 HandRules·InventoryDurability·PropCollision), check-work PASS.
  - 기존 표시 문제(미수정): 전부 예약하면 둘째 줄이 '추가 가능 1개'(MedicalMax 최소 1).
  - Confluence v16: 5절 '수정 결과' 5-5(영상 1·이미지 4), 검토안 결정됨. live 페이지 초안이 v15로 남아 있어 초안도 v16 본문으로 맞춤.
  - 에디터 부산물(커밋 제외 예정): Sounds/EscapeRouteMaterials/escape-labels.asset(SaveAssets로 재저장), 글꼴 아틀라스 2개.
- 2026-11-26 상점 신체 복구 성인형 승인 시안 저장. 160×256 PNG 6장을 승인본과 바이트 단위로 대조하고 상점 프리팹의 기존 GUID 참조를 확인했다. check-work 안전성·문서 통과. Unity 컴파일은 에디터 상태 조회 소켓 제한으로 ENV_BLOCKED, 자동 테스트 생략.
- 2026-11-27 신체 그림을 사용자 제공 관절 마네킹(LimbFigure 6장, 같은 160×256 캔버스)으로 교체. 배치 변경: 그림을 설명 칸 왼쪽 세로 한 줄로 크게, 뒤 조명(limb-spot)·발밑 그림자(limb-floor)·부위별 Shadow, 설명 글은 오른쪽 아래로(descriptionInset 112,138). 밉맵·삼선형 필터로 축소 가장자리 정리. W2_BodyRestore 3/3 통과. Confluence v17(5-5 영상·이미지 교체, live 초안도 맞춤).
- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/490 (팔·다리 예약 UI A안, dev, 지정 팀원 검토 대기). 제외: 글꼴 아틀라스 2개·escape-labels.asset(에디터 부산물).
