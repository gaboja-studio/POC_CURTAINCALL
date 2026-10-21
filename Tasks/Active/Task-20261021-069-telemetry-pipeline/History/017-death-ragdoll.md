# 사망 후 레그돌 긴급 수정

2026-10-22 PM 추가 요청: 캐시 삭제 안내 보강, 외줄 위험100%·추락·사지 전체 절단 및 대포 추락·전체 절단 사망 후 레그돌/중력. 현재 Task에서 예외 수정·빌드 허용. 기존 PR 미생성, b6ae6f0 저장 후 후속 수정이다.

## 원인과 수정
- 실제 외줄 씬·LAN 호스트의 ServerKill 후 IsLimp=true였다가 .6초 뒤 false 재현. DeathRagdoll 두 씬 검사 최초 대포1PASS/외줄1FAIL, 증거 .harness/local/telemetry-ragdoll-before.json. 공개 사망 API 합성 검사이며 사람 입력/다인 검증과 구분.
- 외줄 CourseControlSwitcher.ReleaseControls가 공연 종료 시 SetBalanceActive(false)→ResetBalance→BalanceReset→PlayerRagdoll.Restore를 호출해 사망자를 다시 서 있는 모델로 바꿈. 확정 사망/HasFallen은 표시만 비활성화해 추락 상태 유지.
- 공용 PlayerRagdoll.Restore는 네트워크 Fallen 동안 콘텐츠 정리 호출을 무시. 실제 ServerRevive/ServerRestoreAll/Restart는 Normal 확정 후 기존 복구를 사용한다. 사망·보상·부상 기준 변경 없음.
- NetworkPlayer.ApplyState에서 ForceFall이 이미 처리된 경우에도 GoLimp를 직접 보장(멱등). PlayerRagdoll은 dynamic+gravity+constraints 해제+WakeUp을 명시해 모델 초기 물리 설정에 의존하지 않음.
- 두 공연 회귀는 활성 실제 씬에서 추락 사망 후 모델 분리·.6초 중력 낙하 유지, 공용 사지4개 절단·외줄 ForceFall, 반복 ResetBalance에도 사망자 유지, 명시적 부활 복구를 확인한다. 전체 장애물/키 조작은 사람 검증으로 남긴다.
- 캐시 삭제 Windows·Mac 경로/SurveyCache만 삭제/로그·Google 응답 보존은 기존 pc-files.md에 이미 있음. pc-build.md에도 직접 안내 추가.

## 검사
- 수정 전 재현 완료. 수정 후 컴파일 PASS. 확장된 외줄 회귀 PASS(6.15초), 대포는 외부 eval의 Pipeline main-thread timeout Error가 TestRunner에 유입되어 FAIL(제품 assertion 아님). 외부 eval 없이 대포 한 건만 재실행.
- 기존 폰트/머티리얼4개 SHA256 보존. 씬·프리팹·외부 에셋 원문 변경 없음. 승인 자동 설정 복원은 빌드 후 적용.
- 대포 재실행 PASS(1/1,6.79초). 외줄 PASS와 합쳐 두 공연 회귀 완료. .harness/local/telemetry-ragdoll-{after-partial,cannon-pass}.json. 첫 실패는 숨기지 않고 별도 보관.
- 최종 빌드 build_4c4a6a21a048 Succeeded(42.4초/errors0/warnings12), 두 태그 manifest 확인. Builds/Telemetry-0.1.0-Windows-TEST-ragdoll/CURTAINCALL.exe 실제 실행·Drive ACK 확인. 이전 final 폴더는 캐시 수정까지만 포함하므로 구분한다.
- 승인된 자동 설정/폰트6개와 다음 빌드 태그off/off 복원. 기존4개 SHA256 보존. Scope/Fast/Compile PASS(151파일,씬dirty0,up_to_date). 첫 Scope는 자동 설정·공용 파일 배정 섹션 위치로 FAIL, 승인 경로를 배정된 공용 파일에 명시하고 자동 설정 복원 후 재검사 PASS.
- PM 실제 최종 빌드의 두 공연 사망 화면에서 정상 작동 확인. 로그/설문 최소 확인 및 레그돌 자동·사람 검사 통과 후 dev PR 제출. 두PC는 보류 유지.
