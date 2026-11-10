# 공통 규칙 적용 검사

2026-11-10 · Task-114 · Unity 6000.3.25f1

## 최종 집중 PlayMode 검사

| 검사 | 통과/전체 | 실제 확인한 내용 |
|---|---:|---|
| Ordinary | 4/4 | 바닥 걷기/달리기/앉기, 정상·부상 점프, 재시작/콘텐츠 종료, 공중 조절, 착지 직전 입력 버퍼 |
| CommonRopeRules | 3/3 | 외줄 점프1.5·같은 높이 옆줄3m·T, 바닥 목마와 서비스 수명, 양끝 진입·왕복 |
| CommonPoles | 3/3 | 상하2인·상단만 반동·30초·스위치 해제·공통 외형·바닥 안전 착지·생성 외줄 봉과 해제봉 |
| AppliedRoundTrampoline | 4/4 | 운반자 없는 실제 바닥 반동, 원형 반동 면 중심/가장자리/빈 모서리/손잡이 접촉 |
| CarryMovement | 1/1 | 공통 운반4.5와 다리 부상 배율, 직선/대각선/정지, 내려놓기·재시작·기능 종료 |
| FourFinalFlights | 1/1 | 네 대포 실제 탄도/착지, 정상·한 다리·두 다리·외팔 보존, 새 상호작용 입력만 발사 |
| W6_Rehearsal_Trapeze | 1/1 | 연습실 첫 봉 단독 잡기·목마 전환·2인 매달리기·30초·추락 복귀·단계 재생성 |
| **합계** | **17/17** | 실제 LAN 호스트와 플레이어 이동/기물 구현 경로 |

증거: `.harness/local/task114-ordinary-final-pass.json`, `task114-common-rope-final.json`, `task114-common-poles-final-pass.json`, `task114-trampoline-final-pass.json`, `task114-carry-final.json`, `task114-four-flights-final.json`, `task114-rehearsal-poles-final-pass.json`.

원격 동료가 필요한 검사는 합성 소유자를 사용했다. 실제 두 PC 동시 실행/지연 검사는 하지 않았다. 모든 씬을 사람처럼 처음부터 끝까지 완주한 결과로 해석하지 않는다.

## 전체 EditMode 재검사

전체 실행: **1,845개 중 1,828 통과, 17 실패, 건너뜀0**. 전체 통과로 기록하지 않는다. 증거는 `.harness/local/task114-full-edit-final.xml` 및 같은 이름의 JSON이다. XML에는 도메인 재로드 전후의 전체 개별 결과가 있다.

| 실패 분류 | 개수 | 처리 |
|---|---:|---|
| 바닥 톱 높이 | 1 | 사용자와 보류한0.42m 톱 대0.4m 부상 점프. 수정하지 않음 |
| 달리기 기대값 | 1 | 오래된1.5배 기대를 현재 Base2배로 수정, 집중 재검사 |
| 휴대 절단기 테스트 호출 | 2 | HEAD에도 존재한 선택 인자 누락. reflection 호출에 null 추가, 집중 재검사 |
| 난이도/휴식 상태 | 9 | UI/캠페인 검사 기대와 WaitingForExit 상태 불일치. 해당 런타임·설정·검사 코드 변경 없음 |
| 대기실 추격자 배치 | 1 | 비교 씬 사이 size0/1 불일치. 해당 씬/추격자 코드 변경 없음 |
| 보상 생존 호응 | 1 | 기대50/실제150. 보상 구현/설정 변경 없음 |
| 대기실 문구 | 2 | 누락/중복/미사용 키. UI 제외 범위로 유지 |

전체 실패 이름·메시지·처리는 `Inventory/verification-failures.csv`에 빠짐없이 기록했다. 미수정 기준 브랜치 전체를 별도 Unity 프로젝트에서 재실행하지는 않았으므로, 범위 밖 실패 전부를 재현 확인한 기존 오류라고 단정하지 않는다. 중간 실패/수정 전 통과 수를 최종 집중 검사에 중복 합산하지 않는다.

## 정적 검사·배치 조사

- Knowledge: OK, 17개 폴더.
- git diff --check: 통과.
- 설정/코드 컴파일: 통과. 임시 검증 스크립트 제거 후 최종 check-work를 별도로 기록한다.
- 에디터 preview scene으로 활성 봉36개의 위치·연결/중간 지지면을 조사했다. 원래 씬 값은 `Inventory/pole-scene-values.csv`에 보존했다. 새 배치/씬 저장은 없다.
- 이 검사 시점에는 3m/5m 낙하 피해가 보류였다. 후속 승인/적용 결과는 [후속 검증](followup-verification.md)을 따른다. 대포 지상 톱 높이는 계속 보류.
- 높은 버튼의 부상자 전용 거리/높이 보상이 제거되어 공통 실제 범위 밖에서는 사용할 수 없다. 부상 조합에 따른 동선·협동 난이도는 후속 수동 플레이로 확인한다.
### 전체 검사 뒤 집중 재검사

- ControlRulesTests **18/18**, ExitSawTests **39/39** 통과. 앞서 실패한3개를 포함한다.
- 따라서 실패 목록 중3개는 해결되었고, 미해결은14개다. 전체1,845개를 다시 실행한 결과인 것처럼 통과 수를 합성하지 않는다.
- 증거: `.harness/local/task114-control-rules-final.json`, `task114-exit-saw-final.json`.
- 임시 TestRunner 결과 수집 스크립트와 메타는 제거했다. 테스트가 생성한 CheerGauge/CommonButtons 폰트 캐시는 Unity API로 정리했고, escape-labels는 기존 사용자 변경이 있는 작업 전 사본과 동일하게 복원했다.

### 최종 check-work

- 문서 분리 후 재검사: **안전성 PASS / 문서·규칙 PASS / 컴파일 PASS**. 자동 테스트는 check-work 안에서 다시 실행하지 않았으며 위 별도 결과를 사용한다.
- Context는 hard0, 저장소 기존 문서 길이 경고61개. Knowledge OK(17), diff 공백 검사 통과.
- 증거: `.harness/local/task114-final-check-work-recheck.txt`. 첫 회는 전체 변경 문서 길이 제한으로 실패했고 112개 번호 항목을 그대로3개 문서에 분리하여 해결했다.
- Unity 편집 모드, 컴파일 중 아님, `GameSettings.asset` 선택 상태. API에서 걷기3/달리기6/점프0.8/버퍼0.15/접근2.5/봉30초·80°·180°/초/트램펄린 운반자 필수false를 다시 확인했다.
