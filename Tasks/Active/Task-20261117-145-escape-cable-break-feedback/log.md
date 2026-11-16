# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

- 2026-11-17: Unity 종료 후 origin/dev fetch/merge Already up to date(01abed27). 선행 수정 유지 별도 branch fix/escape-cable-break-feedback 생성. 기존 에셋11개 백업/선택 stash/apply로 보존. doctor는 기존 Task 번호 충돌18건 및 ThirdParty 미저장으로 FAIL, 코드/Unity 도구 사용 가능. 플레이는 사용자 담당.

- 실제 저장 씬24개 배선/4발사대 확인. 절단 전후 렌더에서 낮은 절단점의 짧은 끝이 접속함 쪽으로 접히고, 첫 프레임의 양 끝 외피가 겹치는 문제 확인. 신규 회귀6개 중3개가 수정 전 실패(전체15개 중12통과). 호스트 절단 자체는 실행되며 가시성/메시 문제와 구분한다.
- 절단 주변 실제 호 길이를 제거해 첫 프레임부터 틈을 만들고, 남은 꼬리 길이로 처짐을 제한했다. 스파크를 월드 좌표의 짧은 선형 입자 및0.12초 접촉광으로 보완. 재수신/늦은 접속에서 연출을 재생하지 않는 기존 규칙 유지.
- 24가닥을 양쪽 벽면에 개별 분산하고4가닥은 공용0.8m 점프로 사격 가능한 높이에 배치. 기존 배선 객체/발사대 배열/필요 개수는 유지하고 접속함·고정대·통전 표시를 개별화. 1차 궤적 검사에서 벽 모서리에 가리는2가닥은 위치 보정 중.

- 2026-11-17 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=PASS
- 최종 위치 보정 후 저장 씬 검사4/4 통과: 24가닥 모두 실제 톱 원판 궤적으로 도달,20가닥 지상/4가닥 공용 점프0.8m 사용. 접근 바닥·머리 공간·배선 간격·벽 부착·네 발사대 참조/필요 개수24 유지 확인.
- 최종 탈출로 EditMode962/962, 공용 회귀880/880, 컴파일·공용 설정·안전성·문서/규칙 PASS. Knowledge20폴더 통과. 기존 문서 길이77건 경고와 ThirdParty 미저장1건은 기존 상태로 제외. 실제 PlayMode/다중 플레이 미실행.
- EditMode 카메라로 절단0.1초/완료1초 렌더 확인. 월드 공간 스파크42개/최대64개 및0.12초 섬광, 새로운 재질 없음. 씬 변경127개 기존 컴포넌트는 배선/효과/그룹 연결에 한정. 종료 전 탈출로 씬 saved, stopped 확인.
- 제출 직전 Unity 정상 종료 후 origin/dev fetch/merge: 01abed27 Already up to date. 검사한 에셋/코드8개 SHA256 일치. 기존 변경11개 해시 보존(종료 시 자동 갱신된 escape-labels는 종료 직전 사본으로 복원).
- 저장35e2d12b, PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/405 (fix/escape-cable-break-feedback → fix/trampoline-severed-limb-contact). 선행PR404/403 아직 OPEN, 이번12파일만 포함. new-pr DryRun은 보존한 다른 변경11개 때문에 dirty 경고; 변경 목록/해시 확인 후 gh body-file로 제출. 자동 병합하지 않음.
- 제출 후 Unity 종료 상태에서 origin/dev 재fetch/merge: 01abed27 Already up to date. 사용자 플레이 검증은 남아 있으며 탈출로 씬을 EditMode로 재개한다.
