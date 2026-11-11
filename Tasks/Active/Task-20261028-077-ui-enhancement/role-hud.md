# 역할별 HUD 사용법

[대기실의 여섯 역할과 에디터 편집 방법](waiting-room-hud.md)

씬 스크립트는 이벤트가 확정될 때 표시를 요청한다. 네트워크 권한, 투표 성립, 내구도 계산은 기존 게임 시스템의 책임이다. 반환된 카드를 보관하면 내용을 바꾸거나 닫을 수 있다.

| 역할 | 호출 / 데이터 | 배치 |
|---|---|---|
| 팀 목표 | ShowObjective(설명, 소유자, 제목, 진행 조건) | 오른쪽. 같은 역할의 새 임무가 오면 이전 카드를 닫고 같은 자리에 표시 |
| 시스템 사건 | ShowSystemEvent(본문, 소유자, 제목, 시간, 종류). 종류는 Notice(기본)·Warning·Reward | 중앙 위. 구역 표지 아래에 최신 사건 하나만 표시 |
| 학습 가이드 | ShowGuide(본문, 소유자, 제목, 시간, key: 표시할 키) | 왼쪽. 최신순, 최대 3개, 개별 만료 |
| 상호작용 | ShowInteraction(행동, 대상, 키, 카메라). 홀드·진행률이 있는 장치는 InteractionPromptView 오버로드 | 대상 옆. 공통 프롬프트가 있으면 보조 안내를 중복 표시하지 않음 |
| 즉시 피드백 | ShowFeedback(본문, 소유자, 제목, 시간). 탈출 장치 등은 전용 아이콘 토스트 사용 | 일반 피드백은 오른쪽 위, ShowScoreFeedback은 해당 공연 게이지 |
| 상태·소지품 | ShowStatus(본문, 소유자, 제목), ShowInventory(아이템 이름, 슬롯 뷰, 슬롯 데이터, 이름 라벨) | 기본 상태 카드·소지품은 오른쪽 아래. 기존 슬롯의 손·선택 테두리·번호·내구도 유지 |
| 투표 | ShowVote(본문, 소유자, 제목, 조작 힌트, 선택지들). HudVoteChoice = 라벨·활성 여부·클릭 콜백 | 오른쪽 목표들 바로 아래 |
| 사망·결과 | ShowOutcome(본문, 소유자, 제목, 시간) 또는 정산 스냅샷과 ResultsPanelView. 관전은 ShowSpectator | 개인 사망은 중앙. 정산은 기존 결과 화면 |
| 구역 이름 | ShowZoneName(구역명, 소유자) | 대기실 상단 중앙. 로컬 플레이어가 구역을 옮기면 새 구역 표지로 교체 |

[9개 역할 실제 출력 비교](role-catalog.png) · ⑥은 일반 상태 카드의 예시이며, 실제 소지품은 기존 슬롯 뷰에서 표시한다.

**기존 원화와 역할 프리팹** — `Assets/Resources/Prefabs/UIs/Roles/` 안의 이름이다. 각 프리팹의 `Surface/Artwork`에서 배경·프레임·장식 스프라이트와 색을 개별 편집한다.

| 프리팹 | 가져온 기존 디자인 |
|---|---|
| Objective | Tooltips의 테두리와 말풍선 꼬리 |
| SystemEvent | SystemBanner의 사건 배너. HudEventAppearance에서 종류별 스프라이트와 제목 색 지정 |
| Guide | Guides의 안내 패널·키캡. key가 없으면 프리팹의 기본 라벨(TIP) |
| Interaction | Guide와 같은 왼쪽 키캡. 행동명은 오른쪽에, 홀드 진행은 기존 전용 뷰에서 유지 |
| Feedback | Toast의 아이콘과 토스트 배경 |
| Status | 가이드·구역 이름과 같은 smallplate 9-slice 판. 실제 소지품은 원래 슬롯 뷰 사용 |
| Vote | BasicPanels의 나가기 확인 대화상자와 선택 버튼 |
| Outcome | ResultsPanel의 중앙 패널과 제목 리본 |
| ZoneName | 가이드와 같은 짙은 패널·얇은 프레임과 양끝 황동 핀. 최소 높이 48 |

[구역 표지 아래 최신 사건 하나가 표시된 실제 출력](event-current.png). 사건은 빨간 경고 삼각형, 노란 느낌표, 초록 선물로 구분한다. 제목 29 / 본문 24, 구역 이름 20이 기본이다. 짧으면 한 줄, 명시적 개행이나 긴 문구는 두 줄까지 확장한다. 최대 너비에 두 줄을 넘어가면 말줄임한다. 사건의 제목·본문 TMP는 Surface/Text templates 아래에서, 아이콘 크기와 간격은 Surface/Icon and message에서 수정한다.

**보존할 기존 UI:** [Roles/Retained](../../../Assets/Resources/Prefabs/UIs/Roles/Retained)는 현재 씬·게임 코드와 역할 프리팹 조립기가 쓰는 기존 원본 38개다. 이동 전 GUID를 유지한다. 새 역할 카드는 이 폴더의 한 단계 위에 있다.

| 용도 | Retained 안의 폴더 |
|---|---|
| 상호작용·학습·자막 | Interaction(홀드·구조 포함), Guides, Tooltips, Captions |
| 상태·기물·시계 | BackstageHud, DayClock(실게임 v2.7), RestTimerV28, Player, CheerGauge, PerformanceHud, WorldLabels |
| 알림·공연·결과 | Notifications, Feedback, Spectator, ResultsUi |
| 메뉴와 공통 부품 | BasicPanels, CommonButtons, ScheduleUi, ShopUi, SettingsUi, SettingsUiV2, TitleLogo, TransitionCurtain |

삭제 시 주의: 기존 UIs/CommonButtons의 4개는 외부 패널 조립기가 옛 경로로 찾는 호환용 Variant다. 게임 씬과 프로젝트 코드는 Retained의 원본을 쓴다. UIs/CommonIcons의 아이콘 카탈로그, Resources/Fonts와 원본 스프라이트도 별도 공유 의존성이므로 보존한다. 이전 DayClock v2.2·v2.4 프리팹은 Retained에 넣지 않았다.

**키 배지:** 예: `ShowGuide("문을 조사하세요", owner, "상호작용", key: "E")`. key를 생략하면 프리팹에 적힌 기본값을 표시한다. `ShowInteraction("문 열기", target, "E", camera)`도 같은 배지를 사용한다. 키 값은 입력 판정에 쓰이지 않는 표시용 문자열이다. 실제 게임에서는 현재 바인딩의 표시명을 넘긴다. 배경·테두리·라벨은 따로 편집할 수 있고, 긴 키는 너비가 늘어나며 본문도 함께 밀린다.

**프리팹 편집:** Assets/Resources/Prefabs/UIs/Roles에서 해당 역할을 선택한다. HudRoleMotion의 Enter와 Exit에 Fade, Rise, Slide, Zoom, Pop 중 하나씩 지정한다. Opacity, Enter Seconds, Exit Seconds, Display Seconds를 개별 설정한다. Display Seconds가 0이면 명시적으로 닫을 때까지 유지한다. Allow Duration Override를 끄면 호출에서 초를 전달해도 프리팹 노출 시간이 우선한다. 켜면 해당 호출의 시간이 우선한다. 기존 특수 뷰를 받는 오버로드는 해당 뷰의 연출 설정을 사용한다.

**같은 역할이 다시 표시될 때:** Unity 메뉴 *Curtain Call → UI → 공통 HUD 표시 설정* 또는 역할 프리팹 Inspector의 같은 이름 버튼을 누른다. [공통 설정](../../../Assets/Resources/Settings/UI/HudPresentationSettings.asset)에서 역할별로 교체 / 아래로 쌓기 / 위로 쌓기와 최대 개수를 지정한다. 기본값은 학습 가이드만 최신순 최대 3개, 나머지는 교체다. 교체는 소유자가 달라도 이전 카드와 퇴장 중인 카드를 즉시 닫은 후 새 카드의 등장 연출을 실행한다. 쌓기에서 개수를 넘으면 가장 오래된 것부터 제거한다. 월드 상호작용은 같은 대상에서만 세로로 쌓으며 다른 대상에는 해당 위치를 유지한다. 슬롯·게이지 같은 전용 뷰의 내부 항목은 이 역할 카드 규칙과 별개다.

[구역 이름과 상호작용의 실제 변경 전후](hud-plate-comparison.png). 구역 표지는 최소 높이 48, 월드 상호작용 카드는 66이고 긴 문구는 더 높아질 수 있다.

**배치 편집:** Prefab Mode에서 루트 RectTransform의 Anchor / Pivot / Pos를 변경한다. 매니저는 이 자리를 기준으로 카드가 겹칠 때만 이동한다. Avoid Previous Lane은 목표가 피드백을, 투표가 목표를 가리지 않도록 하는 옵션이다. Interaction의 Pos는 월드 대상에 대한 화면 오프셋이다.
**안쪽 정렬과 크기:** Surface의 Vertical Layout Group에서 Padding, Spacing, Child Alignment를 편집한다. 기본은 좌우 16, 상하 12, 행 간격 5다. 행 순서는 Hierarchy 순서다. Fit Width / Fit Height를 각각 끈 축은 루트 크기를 유지한다. 두 축과 자식 배치를 직접 그리려면 Vertical Layout Group을 끄고 각 RectTransform을 움직인다. 추가 TMP를 일반 자식으로 두면 고정 행, Layout Element의 Ignore Layout을 켜면 자유 배치 장식이 된다.

**문구와 스타일:** 루트의 HudRoleCard → ‘문구 템플릿 · TMP 원본 편집’에서 수정하거나 TMP 자식에서 직접 편집한다. 예: 제목에 ‘현재 목표: {title}’, 본문에 ‘{content}’, 조건에 ‘진행: {condition}’. 값이 없는 토큰 행은 숨기고, 토큰이 전혀 없는 텍스트는 새 호출에도 고정한다. 선택지 라벨은 ‘{choice}’를 쓴다. ‘고정 텍스트 행 추가’로 별도 라벨을 넣을 수 있다. 색·폰트·크기·정렬은 해당 TMP에서, 배경은 Surface/Artwork의 Image에서 수정한다. 이 설정은 새로 표시하는 카드에 적용된다. 생성 메뉴를 다시 실행해도 이미 설정된 프리팹은 초기화하지 않는다.

**수명:** 팀 목표처럼 지속되는 카드는 매 프레임 새로 생성하지 말고 반환된 카드의 텍스트를 갱신한다. 정상 종료는 Close(), 씬 종료처럼 즉시 제거할 때는 Close(true). 소유 오브젝트가 파괴되거나 비활성화되면 매니저가 제거한다. 시간 기반 카드는 게임 시간이 멈춰도 만료되며, 스케줄표·메뉴·정산에 가려진 동안은 시간을 보류한다.

**직접 테스트:** Unity 메뉴 Curtain Call → UI → Open role HUD playground 또는 Assets/Scenes/Tests/UI/HudRolePlayground.unity → Play. 아홉 역할 중 하나를 고른다. 시스템 사건은 알림·경고·보상을 선택하고 본문에 개행을 입력할 수 있다. 해당 필드를 채운 뒤 출력을 누른다. 상호작용은 A/B 대상을 선택하고 WASD 또는 범위 안/밖 버튼으로 플레이어를 옮긴다. 대상 ←/→는 월드 대상을 이동시킨다. 대기실과 같은 ExplorationReach.From을 사용하며 현재 거리/게임 설정 반경을 m로 표시한다. 범위 안이어도 가림벽이 있으면 프롬프트와 행동 입력이 차단된다. 출력한 키(기본 F)나 행동 실행 버튼으로 테스트 입력을 확인한다. 이 행동은 실제 아이템을 지급하지 않는다. 패널 접기는 HUD 표시를 유지한다.

## 공연과 투표 편집

- **인게임 → 설정 → 조작**: ‘투표 · 첫 번째 선택 (수락)’은 기본 Z, ‘두 번째 선택 (거절)’은 X입니다. 실제 의미는 버튼 문구를 따릅니다(예: 넘기기 / 포기). 키 변경 후 적용을 누르면 열린 투표도 갱신됩니다. 한 버튼만 있으면 첫 키만 사용합니다.
- **Unity → CurtainCall → UI → 공연 HUD 문구**: 항목 제목은 표시 조건입니다. 펼쳐서 제목·내용·키 배지를 수정하면 아래에 기본 키로 문구가 미리 표시됩니다. 레이아웃은 기존 Roles 프리팹에서 수정합니다.
- 조작 키 변수: {interact} 상호작용, {jump} 행동, {forward} 전후 이동, {balance} 좌우 균형, {side} 옆줄 방향, {move} 이동 키 모음, {seconds} 중계봉 유지 시간. 게임에서는 해당 플레이어의 실제 설정을 사용합니다.
- 팀 목표는 두 공연 진행 중 오른쪽에, 학습 안내는 왼쪽에 표시합니다. 외줄 조작 안내는 기존 첫날 조건을 따릅니다. 인간대포는 장비 접근·탑승·운반·리프트 이용 때 각각 안내합니다. 현재 가능한 조작은 대상 옆에서, 리프트의 남은 시간은 현장 툴팁에서 확인합니다.
