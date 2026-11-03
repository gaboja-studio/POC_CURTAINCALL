# 리허설·첫날 퀘스트 편집

모든 우측 퀘스트와 리허설 좌측 설명은 `Assets/Resources/Settings/WaitingRoom/WaitingRoomTexts.txt`에서 편집한다. 수정 후 플레이를 다시 시작한다.

| 편집 대상 | 파일의 키 |
|---|---|
| 리허설 단계 제목(좌·우 공용) | `rehearsal.stage.1.title`, `2-1.title`, `2-2.title`, `2-3.title`, `3.title` |
| 리허설 각 단계 좌측 설명 | 같은 단계의 `.intro` |
| 균형·옆줄 점프·그네·트리거 상태별 퀘스트 본문 | `rehearsal.quest.*` |
| 추락·복귀 설명 | `rehearsal.fall-title`, `rehearsal.fall` |
| 완료·무대 입구 집결 설명 | `rehearsal.complete*`, `rehearsal.gather-title`, `rehearsal.quest.gather*` |
| 첫 공연·대기실 안내·두 번째 공연·밤 일정·취침 퀘스트 | `first-day.quest.*.title`, `first-day.quest.*.body` |
| 우측 퀘스트의 위쪽 위치·오른쪽 여백·너비 | `first-day.panel.top`, `.right`, `.width` |
| 우측 제목·본문 글자 크기 | `first-day.panel.title-size`, `.body-size` |

`키 | 내용` 형식이다. 왼쪽 키를 유지하고 오른쪽 내용을 고친다. 본문의 `\n`은 줄바꿈이다. `{inside}/{alive}`, `{ready}/{total}`, `{lane}`은 실제 게임 값으로 대체된다. 키를 지우면 코드 기본 문구를 쓴다.

퀘스트는 실제 상태에 따라 자동으로 바뀐다. 문구 편집으로 코스 구조·단계 완료 조건·철창 잠금·타이머가 바뀌지는 않는다. 리허설 제작본의 규칙은 유지한다.

기존 대기실 튜토리얼의 좌측 설명은 같은 폴더의 `WaitingRoomTutorialTexts.txt`, 좌측 패널 수치는 `WaitingRoomTutorialPanel.txt`에서 편집한다.
