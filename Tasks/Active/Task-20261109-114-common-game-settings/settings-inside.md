# 이미 GameSettings 안에 있는 설정

실제 필드명·현재 에디터 값은 [값 전체 CSV](Inventory/game-settings-values.csv), 선언과 설명은 [필드 CSV](Inventory/serialized-fields.csv)를 본다.
경로는 `Assets/Resources/Settings/GameSettings/` 기준이다. 폴더 안에 있어도 모두 GameSettings.asset의 직접 연결은 아니다.

| 설정 | 현재 들어 있는 항목 |
|---|---|
| GameSettings.asset | Base·Tightrope·HumanCannon·WaitingRoom·Exploration·EscapeRoute의 6개 연결 |
| Base / 성능·세션 | 프레임 제한, 최대·최소 플레이어 수 |
| Base / 하루 진행 | 날짜 수, 하루 공연 수, 첫날 묘기, 기본 묘기, 아침·낮·안내 휴식, 밤 정비 시간, 복귀 경보, 실패 유지 시간 |
| Base / 소지품 | 기본 슬롯 수 |
| Base / 캐릭터 | 자유 이동 속도, 코스 이동 속도, 점프 높이, 회전 속도, 중력, 충돌체 높이·지름, 모델 높이, 밀기 속도, 플레이어 간격 |
| Base / 자세·색 | 달리기·앉기 속도 배율, 앉기 높이 비율, 플레이어 색 4종 |
| Base / 입력 | 길게 누르기 시간 |
| Base / 균형 | 안전·주의 경계, 추락 시간, 주의 구간 추락 누적, 안전 구간 회복, 보정 속도, 이동·정지 흔들림, 방향 전환 간격, 기울기 가속 |
| Base / 행동 표현 | 행동별 이동 허용·중단 허용, 기울기·따라오기, 래그돌 힘, 외줄·봉 매달림 흔들림 |
| Base / HUD·동기화 | 균형 HUD 위치, 균형 전송 변화량 기준 |
| Base / 부상 | 재시작 회복, 한쪽·양쪽 다리 점프 배율, 탐색 다리 이동·팔 도달, 기어가기 속도·점프·옆줄 점프·높이 |
| Base / 절단 표현 | 절단 부위 날림·회전·질량·수명, 피 효과 사용·양·시간 |
| Base / 씬 전환 | 로비·공연·외줄·대포·대기실·탐색·탈출로 씬, 대기실 변형과 유지 여부 |
| Base / 음성 | 활성화, LAN 사용, 대상 씬, 대화·청취 거리, 감쇠, 입 높이, 음소거 키 |
| Tightrope / 코스 | 줄 수·간격·끝점·완주 거리, 옆줄 이동 구간, 그네·릴레이·공중그네 구간, 플랫폼·줄 치수, 판정 폭·높이, 안전망·추락 깊이, 구간 목록 |
| Tightrope / 진행 | 제한 시간, 재시작 지연 |
| Tightrope / 줄 위 동작 | 점프 배율·입력 버퍼, 자연·동적·공유 흔들림, 착지 충격, 옆줄 착지 위험·충격, 흔들림 증폭·지속, 그네 진입 판정, 착지 파동 |
| Tightrope / 손잡기 | 잡는 거리, 흔들림 감소, 준비 시간, 활성 거리·감소량, 릴레이 버팀 활성화·위치·판정 반경 |
| Tightrope / 목마 | 인원별 균형 배율, 위층 균형 전달, 연결 거리, 추락 충격, 점프 배율, 앞·뒤 도약 속도 |
| Tightrope / 구조 매달림 | 버티기 시간, 끌어올리기 시간, 구조 거리·높이 오차, 옆줄 도달·점유 거리, 매달림 높이 |
| Tightrope / 시점 | 높이·거리·주시점, 주변·장애물 탐색, 최대 거리, 화면 여백·완화, 가림 투명도·범위·속도 |
| Tightrope / 부상 | 외줄 이동 배율·최저 속도, 팔·다리별 흔들림, 다리 없음 착지 충격, 팔 손실 도달 감소 |
| Tightrope / 톱·불 | SawSettings·FireSettings의 장애물 배치·동작·협동·추격·접촉 관련 값. 클래스가 다른 폴더에 있어도 공통 설정에 포함됨 |
| TrickSettings 공통 | 톱 배치 비율, 사선 각도, 보상 연결. Tightrope·HumanCannon이 상속 |
| HumanCannon | 해당 공간의 기본 점프 높이, 대포 재사용 시간, 봉 도약 수평 속도 상한 |
| WaitingRoom | 시험 지출, 휴식 중 구매, 신체 복구 기본 가격·증가액·최대 부위 수 |
| WaitingRoom / 추격자 | 활성화, 예고·각성·다운·절단·포획·목표 상실, 단계별 시야·청각·순찰·추격·조사·수색, 단서 표시, 피·외곽선·화면 효과 |
| WaitingRoom / 추격 연출 | 조명 전환·점멸·스파크·단계별 밝기, 눈 색·발광·전환. PursuitSettings·PursuitLightSettings·PursuitEyeSettings는 이미 연결됨 |
| Exploration | 현재 dummyLostCount 시험 항목 1개. 실제 탐색 상호작용 대부분은 여기 없음 |
| EscapeRoute | dummyInputRows 시험 항목, 출구 문 열림 각도·시간. 타이머·협동·화염·운반톱 대부분은 여기 없음 |
| Items/ItemCatalog | 이름·분류·내구도·발견품 내구도·가격·쿨타임·효과 2종·낡은 이름·상점 설명·사용 알림 |
| Rewards/temporary-reward | 환호·야유·난이도별 보상 상한·완주 보상·절단 보너스 |
| Rewards/temporary-audience | 환호 배율, 완주·회피·대포·그네·협동·최초 사용 점수, 추락·정체 야유, 관심 감쇠와 감지 거리 |
| Rewards/performance-ending | 사망 알림, 결과 지연·노출, 암전·로딩 유지·전환 제한 시간 |

## 주의

- ItemCatalog·AudienceReactionSettings·PerformanceEndingSettings는 같은 폴더의 독립 Resources 로더를 쓴다. GameSettings.asset 직접 연결 6개와 구분한다.
- 추격자 클래스가 WaitingRoom/Pursuit에 있다는 이유로 공통 설정 밖이라고 분류하면 안 된다.
- 대포 재사용 시간과 봉 도약 공용 수평 상한은 이미 존재한다. 개별 봉의 상한이 다시 덮어쓸 수 있는 것이 별도 문제다.
- 저장 에셋에 새 필드가 없으면 코드 초기값을 사용할 수 있다. 값 CSV는 에디터에서 읽은 필드도 포함한다.
