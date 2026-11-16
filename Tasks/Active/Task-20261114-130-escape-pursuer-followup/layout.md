# 은폐 추격전 실험 씬

- 원본: `Assets/Scenes/Levels/escape_route.unity`. 복사 요청 시점의 저장 상태를 보존한다.
- 실험: `Assets/Scenes/Tests/EscapeRoute/Pursuer/EscapeRoutePursuitRemodel.unity`. 추격자 프리팹 연결을 해제하고 메시·이동면을 별도 저장한다.
- 공통 버그 수정·숙임 속도·Revision5 외형은 양쪽 씬에 적용된다. 공간 롤백은 원본 씬을 열면 된다. 새 모델의 공용 프리팹 연결은 별도 사용자 요청에 따른다.
- 최신 사용자 지시가 이전 노출 회랑 설계를 대체한다. 추격자 전용 통로는 전부 숨기고 플레이어 코스와 겹치지 않는다.

## 공간 방향

| 구간 | 추격 구성 | 유지 조건 |
|---|---|---|
| 첫 대포·하강 | 콘크리트 내부 이동, 벽 파괴 후 첫 대포대·펌프 준비대로 도약 | 외줄 아래 빈 낙하 공간, 대포와 줄 순서 |
| 컨베이어·복귀 | 외곽 내부 통로에서 하차장·복귀 발사대로 등장 | 운반 레일, 벨트, 스위치·복귀 대포 |
| 협동 구역 | 꺾인 벽 안 진입부에서 기존 작업섬으로 등장 | 협동 외줄·트램폴린·상부 버튼 연결 |
| 화염·상승 | 벽을 부수고 하부 갤러리·코어 끝 대포대로 도약 | 화염, 상부 외줄, 상승 탄도 |
| 최종 질주·작업장 | 북측 벽 안 이동, 기존 주행면 재등장과 작업장 추격 | 필수 배선·선택 보스·낭떠러지·어둠 |

- 바깥으로 보이는 것은 콘크리트 질량과 파괴구다. 이동용 회랑 바닥은 보이지 않아야 한다.
- 파괴구를 열어도 내부 축이 바로 보이지 않도록 꺾인 진입부와 차폐벽을 둔다.
- 플레이어 바닥 위에 추격자 전용 경로를 겹쳐 놓지 않는다. 등장 후의 추격은 기존 발판과 바닥에서 진행한다.
- 시멘트·브루탈리즘의 큰 덩어리, 깊은 어둠과 끝없는 낭떠러지를 유지한다. 접근 경고등은 벽 돌파를 예고하는 국소 연출이다.
- 74개 경로 앵커와 22개 도약, 10개 벽 돌파 등장을 구성한다. 8번 등장은 추가 S자 진입부로 대각선 시선을 막고 상부 외줄 아래를 비운다. 새 모델의 폭에 맞춰 벽 여유·복귀 대포/후반 착지점과 작업등 한 조립체를 조정했다.

## 참고와 적용

- [The Level Design Book — Circulation](https://book.leveldesignbook.com/process/layout/flow/circulation): 플레이어 묘기 경로와 거구의 숨은 이동 축을 분리하고 필요한 지점에서만 만나게 한다.
- [The Level Design Book — Wayfinding](https://book.leveldesignbook.com/process/blockout/wayfinding): 플레이어에게 필요한 다음 기물 정보와 숨겨야 할 적 이동 정보를 구별한다.
- [The Level Design Book — Composition](https://book.leveldesignbook.com/process/blockout/massing/composition): 큰 콘크리트 질량과 비어 있는 낙하 공간으로 규모와 긴장을 만든다.
- [Stuart Macdonald — Developing Control's Brutalism](https://machinefire.artstation.com/projects/xzynYY): 실제 개발자의 콘크리트 질량·단차·구조 표현 연구를 참고한다. 원본 에셋이나 공간을 복제하지 않는다.
- [World of Level Design — Anticipation and Pacing](https://www.worldofleveldesign.com/categories/level_design_tutorials/horror-fear-level-design/part2-survival-horror-level-design-anticipation-pacing.php): 접근 신호→벽 타격→돌파→도약의 예고와 해소를 연결한다.
- [Magnar Jenssen — Functional Lighting](https://www.worldofleveldesign.com/categories/wold-members-tutorials/magnar_jenssen/functional-lighting-magnar-jenssen.php): 기물과 등장 지점에 국소 조명을 두고 낭떠러지의 어둠을 유지한다.

## 확인 범위

- 닫힌 벽뿐 아니라 파괴구가 열린 상태에서도 서비스 통로의 시야 차단을 검사한다.
- 실제 외줄의 낙하 공간, 플레이어 기준점, 기물 위치·참조, 대포 탄도, 추격자의 왕복 도약·몸통·지지를 편집 상태에서 확인한다.
- 모든 카메라와 동적 기물 상태의 가시성·다인 완주·체감 난이도를 자동 검사가 증명하지 않는다. 플레이 테스트는 사용자가 수행한다.
