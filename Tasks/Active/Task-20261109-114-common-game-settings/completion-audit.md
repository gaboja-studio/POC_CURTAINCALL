# 공통 규칙 후속·모션 최종 점검

- 기준 DEV: fb39d3dd. 최신 사용자 지시: 완료 후 저장·push·dev PR, 최종빌드37355775에 전후 표 게시. dev 직접 병합 없음.
- 기존 통일값과 튜토리얼 예외는 GameSettings 유지. 탈출로 제외. 원본 서로 다른 PC의 밤→아침 끊김은 로그 미확보로 미확정.
- 증거: `.harness/local/common-rules-completion-20261111/`. 앞선 실제 연결 결과는 [runtime-play-results.md](runtime-play-results.md).
- 최종 check-work: 안전성·문서/규칙·컴파일·공통 설정 연결 PASS. 전체 자동 테스트는 재실행하지 않고 아래 집중 결과 재사용. 제출 전 Unity 종료 후 DEV fb39d3dd 동일 확인.

## 이번에 추가 수정한 실제 결함
1. 외부 이동을 맡긴 뒤 IsMoving 잔류 → 발판 위 제자리 걷기 방지.
2. 대포 착지 동작이 새 점프·걷기·앉기를 가림 → 새 행동에 즉시 양보.
3. 재시작·빠른 부활 후 이전 비행/착지/아이템 정렬 잔류 → Restarted와 Revived 모두 표현 상태 초기화.
4. 목마 윗사람 제자리 점프에 대기 모션 → PiggybackFollow의 실제 점프 상태 전달.
5. 원격 비시뮬레이션 이동기의 낙하 잔류 → 소유자 수신 점프 상태 사용.
6. Animator 재바인딩 후 운반 캐시만 유지 → 실제 재생 상태를 함께 확인해 잡는 자세 복구.
7. 이동·부상 속도와 발/팔 동작 불일치 → BaseGameSettings의 표현 기준 속도로 기본 레이어 재생률 계산. 상체 동작 속도 보존.
8. 외줄 뒤로 이동 시 앞으로 걷기 → 진행 방향에 따라 발동작 역재생.
9. 두 다리 손실 상태의 일반 점프가 정지된 Crawl → 기존 Cut2/3 상승·낙하·착지 동작 연결.
10. 외줄 구조에 서 있는 ItemPull 사용, 손 접점 약48cm 이탈 → RopePull 및 생존 팔 접점 보정. 구조 거리·시간 유지.
11. 개발용 대포 봇이 공통 봉 목록의 빈 칸을 읽어 반복 예외 → 없어진/비활성 봉 건너뛰기. 옛 공중 입력 기준4.5도 BaseGameSettings.AirControl.Speed 사용.

## 현재 파일의 집중 검사
| 범위 | 결과 | 근거 |
|---|---|---|
| 최초 모션 결함6개 | 수정 전6실패 / 후6통과 | motion-before/after-results.json |
| 확장 모션 경계9개 | 9통과 | motion-final-results.json |
| 네 캐릭터×생존 부상4단계 걷기·점프·착지 | 1개 통합검사 통과(16조건) | motion-visual-after-results.json, cannon-motion PNG |
| 캐릭터 모션 전체 | 36통과 | humanoid-final-results.json |
| 착지 후 정지 반복 | 1통과 | motion-stop-after-landing-results.json |
| 공연 모션12개 | 11통과 후 구조1개 수정·재검사 통과 | circus-first-results.json / rope-rescue-final-results.json |
| 리허설 설정·수명·복귀 | 120통과 | rehearsal-edit-final.json |
| 공통 낙하·상호작용 | 21통과 | common-rules-edit-final.json |
| 실제 별도 프로세스 대포 봇 역할 교대·발사·반동·재시작 | 1통과 | bot-current-complete.json |
| 대포 빠른 톱/재접촉/봉 접근 | 4+1+1통과 | final-FastSaw_ / OriginalCourse_GeneralHazard / PoleGrab_ExpandedReach.json |
| 외줄 전멸 정산·외팔 부활/제한시간 정산 | 2통과 | final-AllDeadRecordsDeathThenRevivesOneArmedWithoutRestart / TimeLimitEndsThroughSharedSettlementWithoutRestoringSurvivors.json |

## 넓은 검사와 판정 한계
- 최초 전체 EditMode1968개: 1954통과/14실패. 대포80개:64통과/16실패, 외줄99개:63통과/35실패/1생략. 전체 녹색으로 보고하지 않는다.
- 옛6m 사망/변경된 리허설 함수 인자/최초 튜토리얼 준비 누락은 실제 규칙을 유지하면서 검사 준비를 고쳐 위 집중 검사에서 확인했다.
- 외줄의 옛 ServerKill 인자 개수, 기존 대포 전용 부상·공중 조절·영구 스위치 개방 기대와 현재 공통 규칙을 구분한다. 옛 기대값을 만족시키려고 공통 규칙을 되돌리지 않는다.
- 옆줄3m는 실제 도착점까지의 수평 거리다. 동료를 피하려고 뒤로 이동한 도착점이3m를 넘는 옛 검사는 현재 규칙에서 거절 대상이다.
- 외줄 정산 검사에5초 제한이 있었지만 공통 결과 연출은5.8초다. 검사만 현재 설정의TransitionAt+5초를 기다리도록 수정했다. 봉 접근 검사도 순간이동한 프레임의 옛 F 선택을 쓰지 않도록 다음 입력 프레임을 준비했다.
- 대포2인 역할 교대 검사는 손잡이 대기점(-9,4,0)이 출발 리프트(-13.25,4,5) 밖인데 이동 없이 도착을 기다렸다. 리프트 탑승→도착 LateUpdate 종료→중앙 수신 위치·시점 준비로 fixture를 보완해 최신 봇으로 통과했다. 게임 자동 이동은 추가하지 않았다.
- 튜토리얼 출구 실제 WASD 이동은 재실행에서 공연 진입까지 통과. 최초 경로 추종 정지는 재현되지 않아 게임 결함 수정으로 세지 않는다.
- 기존 보류14개와 이번 넓은 검사의14실패는 같은 목록이 아니다. 보류한 톱 배치·난이도/UI·추격/보상 및 미정인 스위치 봉 완료 조건은 완료로 세지 않는다.

## 최신 2·3·4인 Relay 실행 결과
- 11/11 13시대 최신 Development 빌드(오류0/경고5), 한 PC의 독립 실행 파일4개를 실제 Relay로 연결했다. 별도 PC·회선 검증이나 모든 코스 완주와 구분한다.
- 2인: 일반 이동·점프/부상, 사망 후 밤→아침, 목마 윗사람 점프, 일반 봉 잡기/놓기, 스위치 봉 유지/해제 확인.
- 3인: 전원 이동·점프/부상, 호스트 사망 후 밤→아침, 전원 복귀·조작·접속 유지 확인.
- 4인: 캐릭터4종/의상, 전원 이동·점프, 호스트 포획+다른 참가자 사망 후 밤→아침, 목마 두 쌍과 윗사람 점프 확인.
- 4인: 원격 스위치 봉 점유/길 개방→점유자 사망→점유 해제/체크포인트 복귀. 높은 외줄 착지 무피해와 튜토리얼 바닥 추락 후 온전한 복귀 확인.
- 4인 대포 재연습: 원격 대포 탑승·트램펄린 운반→두 이용자 사망→좌석/운반 해제·온전한 복귀→다른 이용자의 재운반을 네 관찰자에서 확인.
- 안정 상태14개 체크포인트 모두 상태/부상/연결/탑승 불일치0, 정지 위치 차이 최대0.004957m. 이동 중에는 일반 네트워크 전송·보간 지연이 있으며 즉시 동일 프레임으로 표시된다는 뜻이 아니다.
- 렌더링 실행 로그에서 NullReferenceException/ProtocolTimeout/TransportFailure/OutOfMemoryException 없음. 최초 headless 준비는 TMP 예외와 암전 대기로 중단·별도 보관했고 정상 렌더링 빌드로 다시 실행했다.
- 증거: multiplayer/checkpoint-*-summary.json, peer-0~3.jsonl, four-tutorial-falls-history.json. 원본 다른 PC의 밤→아침 끊김 원인은 여전히 미확정.
