# 공통 규칙 Inspector 조정

`Assets/Resources/Settings/GameSettings/`에서 아래 `.asset`을 선택한다. 기본값은 현재 통일 규칙과 동일하다.

1. Unity 플레이 중지 → Project 창에서 위 폴더 열기.
2. 아래 설정 `.asset`을 한 번 클릭. `.cs` 스크립트는 열 필요 없음.
3. Inspector에서 숫자/체크박스를 변경. 항목 이름에 마우스를 올리면 효과·기본값 설명 표시.

- PoleSettings: `Tricks/PoleSettings.asset` → **공통 인원·조작**의 세 항목을 바로 수정.
- TightropeSettings: `Tricks/TightropeSettings.asset` → **줄 위 동작 → Rope Movement** 펼치기 → **외줄 점프 중력 배율**.
- BaseGameSettings: **신체 손상 → Body Damage** 펼치기 → 피해 개수 두 항목. **공통 상호작용 → Interaction** 펼치기 → **구조 대상 먼저 선택**.
- 봉 시간/각도/속도: 같은 PoleSettings의 **매달리기 / 앞뒤 운동**. 트램펄린은 `Tricks/TrampolineSettings.asset`의 세 항목.

| 설정 에셋 | 항목 | 기본값·효과 |
| --- | --- | --- |
| Tricks/PoleSettings | 최대 매달림 인원 | 2명. 1~4명 조정, 위아래 연결. 플레이를 멈추고 변경한 뒤 다시 실행 |
| Tricks/PoleSettings | 맨 위 사람만 반동 조작 | 켬. 끄면 탑승자 입력 평균, 반대 입력 상쇄 |
| Tricks/PoleSettings | 아래 사람이 있으면 놓기 제한 | 켬. 끄면 지지자가 놓을 때 아래 사람도 함께 낙하 |
| Tricks/PoleSettings | 일반 봉 너비 / 스위치 봉 너비 | 각각 1.8m. 외형은 종류별로 유지, 봉 길이·양쪽 줄 간격만 조정 |
| Tricks/TightropeSettings → Rope Movement | 외줄 점프 중력 배율 | 1배. 높이는 유지, 클수록 체공 시간이 짧아짐 |
| BaseGameSettings → Body Damage | 일반 위험물 절단 개수 | 1개. 톱·일반 화염의 공통 피해. 즉사형은 별도 |
| BaseGameSettings → Body Damage | 낙하 부상 절단 개수 | 2개. 부상 높이 이상·사망 높이 미만 착지에 적용 |
| BaseGameSettings → Body Damage | 부상 낙하 높이 / 사망 낙하 높이 | 4m / 6m. 실제 착지면까지 낙하한 높이 |
| BaseGameSettings → Body Damage | 외줄 착지 낙하 피해 면제 | 켬. 사용 가능한 외줄에 실제 착지할 때만 높이 피해 면제 |
| BaseGameSettings → Body Damage | 안전 착지대 낙하 피해 면제 | 켬. 활성 HumanCannonLandingZone 실제 윗면 착지는 높이 피해 없음. 일반 바닥·착지대 밖·직접 위험물은 기존 규칙 |
| WaitingRoomSettings → 최초 튜토리얼 공통 규칙 → Tutorial | 연습 코스 아래 바닥에 떨어지면 재시작 / 튜토리얼 사망 복귀 대기 시간 | 켬 / 1.5초. 최초 교육의 코스 아래 바닥 착지 후 현재 소구간 출발점 복귀 |
| WaitingRoomSettings → Tutorial | 연습실에 사람이 있으면 튜토리얼 문 열기 / 첫 공연 이동 전 사망자 복귀 기다리기 | 모두 켬. 최초 교육 완료 후 집결도 적용 |
| WaitingRoomSettings → Tutorial | 튜토리얼 도착 지점 투명 벽 | 켬. 최초 교육만 적용, 재연습은 적용 안 함 |
| BaseGameSettings → Interaction | 구조 대상 먼저 선택 | 켬. 끄면 구조와 물체를 가까운 순서로 선택 |

- 봉·트램펄린의 모든 조정 칸에 한글 이름과 툴팁. 기존 피해 간격·낙하 높이·공중 조절·외줄 착지 파동의 누락 설명도 추가.
- 값의 효과·단위·기본값·제한·재시작 필요 여부는 항목에 마우스를 올리면 확인 가능.
- 최대 인원은 봉 생성 시 적용하므로 플레이를 멈추고 변경 후 다시 실행. 코스만 재시작하면 이미 생성된 봉의 정원은 유지됨. 외형 프리팹은 재생성, 너비는 표시 갱신 시, 매달림 시간은 다음 탑승부터 적용. 다른 수치·선택 규칙은 해당 동작 처리 시 읽음.
- 3~4인도 바로 위 사람을 붙잡음. 중간 사람 사망/이탈·기한 만료 때 아래 사람부터 순서대로 낙하 승인.
- 정원을 늘리면 봉 아래 공간도 확인. 기존 낮은 봉에서 바닥 아래 사람을 잡으려 하면 공통 층·가림 판정으로 차단하며, 정원 변경이 씬 배치를 자동으로 높이지는 않음.
- 모든 탑승자는 맨 위 사람의 기한을 공유. 스위치는 맨 위 사람 점유 동안만 켜짐.
- 설정은 같은 빌드로 배포. 호스트가 점유·피해를 판정하며 이동 조작은 각 소유자도 읽음.
- 네트워크 전송 형식·최대 지원4인·계산 오차 허용은 Inspector 조정 대상이 아님.
- 기존 실패14개 수정은 보류. 실제 두 PC 동시 플레이는 별도 확인 필요.
- Confluence [변경 전후 페이지](https://hrjoo122770.atlassian.net/wiki/spaces/bYmkRlnpusng/pages/39124994)의 **Inspector 조정 방법**에 동일한 클릭 순서·항목별 위치 표를 수록.
