# 이벤트 목록 v1

등록 계약과 실제 수집 상태는 다릅니다. 현재 자동 수집은 아래 ‘연결’ 열을 참고하세요.

| eventType | 필수 payload | 연결 |
|---|---|---|
| GAME_STARTED | 없음 | 기반 자동 수집 |
| GAME_EXITED | reason: text | 기반 자동 수집 |
| PLAYER_EXITED | reason: text | 기반 자동 수집 |
| SCENE_ENTERED | sceneName: text | 기반 자동 수집 |
| SCENE_EXITED | sceneName: text, durationMs: duration | 기반 자동 수집 |
| INPUT_KEY | keyId: text, inputMethod: text | 공개 API 연결·범위/실행 검증은 coverage.md |
| BUTTON_INPUT | buttonId: text | 공개 API 연결·범위/실행 검증은 coverage.md |
| INJURY_CHANGED | beforeLostCount: limbs, afterLostCount: limbs | 기반 자동 수집 |
| MICROPHONE_INPUT | startedAt: utc, endedAt: utc | 공개 API 연결·범위/실행 검증은 coverage.md |
| TITLE_ENTERED | 없음 | 기반 자동 수집 |
| ROOM_CREATED | roomType: roomType | 기반 자동 수집 |
| ROOM_ENTERED | roomType: roomType, isHost: bool | 기반 자동 수집 |
| SIGNATURE_CHANGED | signed: bool | 공개 API 연결·범위/실행 검증은 coverage.md |
| RUN_STARTED | playerCount: players | 기반 자동 수집 |
| RUN_ENDED | reason: text | 기반 자동 수집 |
| PERFORMANCE_STARTED | trickType: trick, difficulty: difficulty, retryIndex: positive, playerCount: players, startingLostCount: limbs | 공개 API 연결·범위/실행 검증은 coverage.md |
| PERFORMANCE_SUCCEEDED | trickType: trick, difficulty: difficulty, durationMs: duration, survivors: count, arrivals: count | 공개 API 연결·범위/실행 검증은 coverage.md |
| PERFORMANCE_FAILED | trickType: trick, difficulty: difficulty, durationMs: duration, reason: text | 공개 API 연결·범위/실행 검증은 coverage.md |
| PERFORMANCE_PROGRESS | trickType: trick, difficulty: difficulty, maxDistance: duration | 공개 API 연결·범위/실행 검증은 coverage.md |
| SECTION_ENTERED | sectionId: positive, lostCount: limbs | 공개 API 연결·범위/실행 검증은 coverage.md |
| SECTION_EXITED | sectionId: positive, durationMs: duration, reason: text | 공개 API 연결·범위/실행 검증은 coverage.md |
| PLAYER_DIED | sectionId: optionalPositive, position: position, cause: text, lostCount: limbs | 공개 API 연결·범위/실행 검증은 coverage.md |
| LIMB_SEVERED | sectionId: optionalPositive, position: position, part: text, afterLostCount: limbs | 공개 API 연결·범위/실행 검증은 coverage.md |
| PERFORMANCE_LEFT | trickType: trick, sectionId: optionalPositive, reason: text | 공개 API 연결·범위/실행 검증은 coverage.md |
| TIGHTROPE_FELL | sectionId: positive, ropeNumber: positive, distance: duration, lostCount: limbs, handhold: optionalBool, piggyback: bool | 공개 API 연결·범위/실행 검증은 coverage.md |
| JUMP_RESULT | sectionId: positive, jumpType: text, originRope: positive, targetRope: positive, landed: bool | 공개 API 연결·범위/실행 검증은 coverage.md |
| COOP_ACTIVATED | trickType: trick, gimmickType: text, gimmickId: text, participants: ids | 공개 API 연결·범위/실행 검증은 coverage.md |
| CANNON_FIRED | launchId: uuid, sectionId: optionalPositive, cannonId: text, direction: position, power: duration, lostCount: limbs | 공개 API 연결·범위/실행 검증은 coverage.md |
| CANNON_FLIGHT_ENDED | launchId: uuid, sectionId: optionalPositive, position: position, reachedTarget: optionalBool, reason: text | 공개 API 연결·범위/실행 검증은 coverage.md |
| CANNON_HAZARD_CONTACT | sectionId: optionalPositive, position: position, obstacleId: text, hazardType: text, cutApplied: bool, part: text, lostCount: limbs, reason: text | 공개 API 연결·범위/실행 검증은 coverage.md |
| ZONE_ENTERED | zoneType: text | 공개 API 연결·범위/실행 검증은 coverage.md |
| ZONE_EXITED | zoneType: text, durationMs: duration, reason: text | 공개 API 연결·범위/실행 검증은 coverage.md |
| SCHEDULE_OPENED | flowId: uuid | 공개 API 연결·범위/실행 검증은 coverage.md |
| SCHEDULE_CONFIRMED | flowId: uuid | 공개 API 연결·범위/실행 검증은 coverage.md |
| READY_CHANGED | ready: bool | 공개 API 연결·범위/실행 검증은 coverage.md |
| STAGE_ENTRANCE_REACHED | 없음 | 공개 API 연결·범위/실행 검증은 coverage.md |
| FIRST_PLAYER_LEFT | 없음 | 공개 API 연결·범위/실행 검증은 coverage.md |
| ALL_PLAYERS_ASSEMBLED | 없음 | 공개 API 연결·범위/실행 검증은 coverage.md |
| RECALL_ALARM | 없음 | 공개 API 연결·범위/실행 검증은 coverage.md |
| RECALL_EXPIRED | position: position | 공개 API 연결·범위/실행 검증은 coverage.md |
| ROUTE_BLOCKED | reason: text | 후속 원천 연결 필요 |
| ROUTE_DETOUR | reason: text | 후속 원천 연결 필요 |
| BREAK_ENDED | reason: text | 공개 API 연결·범위/실행 검증은 coverage.md |
| INTERACTION_FAILED | interactionId: text, reason: text | 공개 API 연결·범위/실행 검증은 coverage.md |
| SHOP_OPENED | flowId: uuid | 공개 API 연결·범위/실행 검증은 coverage.md |
| PURCHASE_COMPLETED | flowId: uuid, itemType: text, quantity: positive, balanceBefore: count, balanceAfter: count | 공개 API 연결·범위/실행 검증은 coverage.md |
| DELIVERY_DISPATCHED | flowId: uuid | 공개 API 연결·범위/실행 검증은 coverage.md |
| DELIVERY_AVAILABLE | flowId: uuid, quantity: positive | 공개 API 연결·범위/실행 검증은 coverage.md |
| ITEM_COLLECTED | flowId: uuid, itemType: text, quantity: positive | 공개 API 연결·범위/실행 검증은 coverage.md |
| STORAGE_REACHED | flowId: uuid | 공개 API 연결·범위/실행 검증은 coverage.md |
| ITEM_STORED | flowId: uuid, itemType: text, quantity: positive | 공개 API 연결·범위/실행 검증은 coverage.md |
| PURCHASE_FAILED | flowId: uuid, reason: text | 공개 API 연결·범위/실행 검증은 coverage.md |
| COLLECTION_FAILED | flowId: uuid, reason: text | 공개 API 연결·범위/실행 검증은 coverage.md |
| STORAGE_FAILED | flowId: uuid, reason: text | 공개 API 연결·범위/실행 검증은 coverage.md |
| ITEM_DROPPED | flowId: uuid | 공개 API 연결·범위/실행 검증은 coverage.md |
| ITEM_PICKED_UP | flowId: uuid | 공개 API 연결·범위/실행 검증은 coverage.md |
| ITEM_HANDED_OVER | flowId: uuid, toPlayerId: text | 공개 API 연결·범위/실행 검증은 coverage.md |
| SERVICE_OPENED | flowId: uuid, serviceType: text | 공개 API 연결·범위/실행 검증은 coverage.md |
| SERVICE_RESERVED | flowId: uuid, serviceType: text, lostCount: limbs, targetCount: limbs, amount: count | 공개 API 연결·범위/실행 검증은 coverage.md |
| MEDICAL_ROOM_REACHED | flowId: uuid | 공개 API 연결·범위/실행 검증은 coverage.md |
| MEDICAL_ENTERED | flowId: uuid | 공개 API 연결·범위/실행 검증은 coverage.md |
| RESTORATION_COMPLETED | flowId: uuid, beforeLostCount: limbs, afterLostCount: limbs | 공개 API 연결·범위/실행 검증은 coverage.md |
| MEDICAL_RETURNED | flowId: uuid | 공개 API 연결·범위/실행 검증은 coverage.md |
| SERVICE_FAILED | flowId: uuid, reason: text | 공개 API 연결·범위/실행 검증은 coverage.md |
| SERVICE_ABANDONED | flowId: uuid, reason: text | 공개 API 연결·범위/실행 검증은 coverage.md |
| RESTORATION_VERIFIED | flowId: uuid, lostCount: limbs, movementRestored: bool, jumpRestored: bool, capacityRestored: bool | 공개 API 연결·범위/실행 검증은 coverage.md |
| FUNDS_CHANGED | transactionId: text, gameDay: positive, balanceBefore: count, balanceAfter: count, category: fundCategory | 기반 자동 수집 |
| TELEMETRY_GAP | reason: text, droppedEvents: positive | 기반 자동 수집 |

연결은 전체 항목의 실플레이 검증 완료를 뜻하지 않는다. [실제 범위·미연결 항목](coverage.md) 참조. optionalPositive/optionalBool은 명시적 null 또는 해당 타입만 허용한다.

원천 스키마: Scripts/Telemetry/core.gs. 이 파일 변경 시 node Scripts/Telemetry/generate-catalog.cjs로 재생성.
