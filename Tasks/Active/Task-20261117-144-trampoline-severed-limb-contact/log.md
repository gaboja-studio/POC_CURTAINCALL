# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

- 2026-11-17: Unity 종료 상태 origin/dev fetch/merge Already up to date(01abed27). 추격 인계 PR #403 제출 후 별도 브랜치 생성. 미저장 에셋11개는 선택 stash 후 그대로 apply했고 안전 사본은 유지.
- doctor: 기존 Task ID 충돌18건과 ThirdParty 미저장 변경으로 FAIL. 코드 조사/자동 검사 도구는 사용 가능. 관련 외부 에셋을 변경하거나 제출하지 않는다.
- 코드 조사: Legacy LimbDismemberment.Launch는 DismemberedLimb를 등록하지만 HumanoidModel.BakePiece는 Ignore Raycast 레이어만 지정한다. HumanCannonCarry는 모든 레이어를 쿼리하므로 새 모델의 실제 사지는 공용 제외 조건을 통과하지 못한다. 기존 검사는 수동으로 표식을 붙인 조각만 사용해 실제 생성 누락을 놓쳤다.

- 실제 Rabbit BakePiece로 생성한 사지 검사: 수정 전20/20 실패(운반 스윕8, 공용 표식/물리 연결8, 후발 플레이어4). 수정 후20/20 통과. 공통 표식을 실제 완성 계층에 활성화 전에 연결했다.
- 인간대포 저장 씬의 실제 Trampoline 복제와 런타임 공용 충돌 메시 초기화까지 추가해24/24 PASS. 최초 확장 검사의4실패는 CarryTrampoline 외형 전용 프리팹을 장치 프리팹으로 가정한 테스트 준비 오류이며 저장 씬 실제 장치로 교체했다. 최초 테스트 컴파일 Range 이름 모호성은 NUnit 별칭으로 수정. 준비 실패와 게임 결함 재현을 구분한다.
- 저장 인간대포 씬 로드: 트램펄린1개 활성/컴포넌트 연결/Handle 연결 정상, 누락 스크립트0. 원본 씬 미변경·PlayMode 미실행. 콜라이더는 기존 RefreshCarryBody에서 런타임 생성하는 설계이며 씬 누락으로 오인하지 않는다.

- 2026-11-17 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=PASS

- 최종 check-work -CommonRegression: 안전성·문서·컴파일·공용 설정 PASS, EditMode880/880 PASS(이번 실제 모델/씬 회귀24건 포함, 별도 합산하지 않음). 근거 trampoline-final-check.txt, trampoline-final-common.json. 지식 검사와 git diff --check PASS.
- 별도 변경 에셋11개 SHA256 전부 보존, 씬/프리팹/물리 설정 변경 없음. 현재 branch fix/trampoline-severed-limb-contact. 추격자 수정은 PR #403 제출 완료; 이번 트램펄린 수정은 이 별도 브랜치 작업 파일에 저장하고 커밋/push는 하지 않았다.
- 사용자 테스트 준비: HumanCannon.unity 로드, isPlaying=false/isCompiling=false/isDirty=false. 실제 다인/PlayMode 검증 미실행. 인간대포 씬에서 절단된 사지 사이를 트램펄린으로 운반/회전/내려놓는 최종 체감은 사용자가 확인한다.

## 2026-11-17 저장·제출
- 사용자 요청에 따라 필요한8개 파일만 저장·제출한다. Unity 종료 후 origin/dev fetch/merge: Already up to date(01abed27). 검증된 코드·테스트 SHA256 불변으로 직전 공용880/880 PASS 결과를 재사용한다. 플레이 미실행.
- 별도 에셋11개는 종료 직전 백업/해시와 동일하게 보존. 종료 과정에서 변경된 escape-labels.asset만 종료 직전 사본으로 되돌렸다. ThirdParty 변경은 그대로 보존하고 제출하지 않는다.
- 선행 PR #403이 OPEN이므로 fix/escape-device-activation을 대상으로 후속 PR을 제출한다. DEV 최신화는 현재 브랜치에 반영했고 선행 수정도 유지한다. #403 검토/DEV 반영 이후 후속 PR의 DEV 전환을 검토한다. 자동 병합은 하지 않는다.
