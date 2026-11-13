# Log

- 2026-11-14: 선행 실패 엔딩 A PR #382 제출 완료. e5a2a9c5에서 feat/escape-success-art 독립 worktree로 분리. 현재는 성공 엔딩 원화 단계이며 Unity 코드/씬 변경 없음.
- doctor -SkipEditor 1회: private submodule 미초기화, Task 생성 전 미등록, 기존 Task ID 중복, editor 설치 탐지 실패를 보고. Task 생성으로 등록 해결. 이번 원화는 추적된 실제 캡처를 사용하므로 private/Editor 환경은 영향을 주지 않으며 3D 구현 전 초기화/설치 경로를 확인한다. 원본 프로젝트에서 Unity 6000.3.25f1 실행은 선행 Task에서 이미 확인했다.
- 기준 캡처: Task-130/Previews/success-day-one.png(낮, 결과 UI), Task-120/Previews/ending-two-player-meadow.png(실제 2인 정면 달리기). 모델 실루엣·바리에이션·카메라 구도를 보존하고 재질/환경/거리 명암만 개발한다.
- 내장 image_gen으로 초기/후반 키프레임 2장 생성, Art/에 복사 저장. 초기 원화는 낡은 서커스와 지면/의상 재질, 후반 원화는 같은 크기의 생존자 뒤로 작아진 서커스와 어두운 원경을 표현한다. 생성 이미지의 미세한 형태 재해석은 실제 모델 교체 지시가 아니며 구현 시 원본 메시/팔레트를 보존한다.
- 현재 Cinematic은 낮 skyColor를 fogColor로도 사용함을 확인. 다음 구현에서 낮 전경과 어두운 원경을 분리해야 한다. 원화 리뷰 후 구현 범위와 값을 구체화한다. 이번 턴의 성공 엔딩 변경은 문서/원화뿐이며 Unity 적용이나 성공 엔딩 PR은 아직 하지 않았다.
- check-work -Base HEAD -SkipUnity: 안전성/문서·규칙 PASS. Unity 변경 없음으로 컴파일 생략, 자동 테스트 미선택. 기존 문서 길이 경고 외 실패 없음. check-work.txt 참조. 두 원화는 분리 worktree에 저장되어 있으며 PR #382는 엔딩 A 변경만 포함한다.
- 2026-11-14 구현 재개: 자체 에디터 종료 상태에서 origin/dev fetch/ff-only 반영 확인(이미 포함). private fdc858a 초기화 후 독립 Library 전체 임포트를 완료했다. 원본 프로젝트 PID35684는 조작하지 않았다.
- 성공: 원본 플레이어 4종과 Cinemachine/결과 UI를 유지하고 SuccessEndingArt·타일·전용 파생 셰이더를 생성했다. 낮 하늘/어두운 거리 안개, 낡은 텐트·로프·갈대·흙·잔돌, 의상 오염/혈흔/섬유를 적용했다. 색상/부상/혈흔 옵션과 복귀 시 원래 재질을 보존한다.
- 실패: 사용자 확정에 따라 다음 런의 1일차 아침 시작, 해금/클리어 초기화, 이수 리허설·본 가이드 유지, 미이수/미열람 보존, 정상 신체/조작 복구와 빨간 실패 시스템 알림을 연결했다.
- 1인 실제 성공/로비, Deadline 및 전멸/새 아침 검증. 메모리 이수 fixture의 true/false 보존 확인. 상세는 implementation-review.md와 Previews/.
- 실제 LAN 2인(본 작업 MPPM clone PID22008) 성공·부상 곰·결과2명/로비, 전원 사망 후 양쪽 WaitingRoom/Day1/Morning/Normal/신체None/조작true 확인. 촬영 준비 로딩 중 전멸 1회는 개발 보류/복구/전원 체크포인트 배치로 재시도했다.
- Videos/: 1인 성공24.24초·1인 4일차 실패22.36초·2인 성공23.34초·2인 전멸 실패28.62초. 실제 Game View MP41920×1080 약30fps 무음. 원본 파일 재생/6프레임 디코딩 시트로 두 결과 탭과 목적지 확인. 공연 미시도 통계는 실제 값이며 촬영 조건을 readme에 명시했다.
- 검사: FailureEndingStateTests 16/16, EscapeEndingTests 4/4 PASS. 복합 필터 시도는0개로 제외했다. 셰이더6개 오류0/모델 매핑4개, verify-knowledge OK(20 folders). 기존 URP/Vivox 경고는 남아 있어 전체 Console0으로 기록하지 않는다.
- 플레이 종료, 임시 MPPM 시나리오 제거·Dev 설정 복구. Assets/Tasks 임시 캡처는 최종 Task/Previews로 복사/해시 일치 확인 후 AssetDatabase로 제거했다. 자동 TMP atlas/Vivox 설정 변경은 제출 범위에서 제외한다. 커밋/push/새 PR은 하지 않았다.
- 최종 check-work: 안전성/문서·규칙/컴파일/공통 설정 PASS. 선택 자동 테스트는 생략했고 위 개별20개 통과 근거를 별도 저장했다. 첫 검사에서 README 대문자 규칙과 검사 로그 자체 파일 잠금이 발생해 readme.md로 수정하고 출력은 검사 완료 후 저장하는 방식으로 재검사했다. check-work-implementation.txt 참조.
- 2026-11-14 저장·제출 요청: 작업 전용 에디터/MPPM clone 종료를 확인하고 origin/dev 및 PR #382 head를 fetch했다. dev c0d2cb09는 이미 포함되어 있으며 #382 head는 기준 e5a2a9c5와 동일하다. 기존 PR을 확장하며 다른 worktree와 자동 캐시/private 변경은 보존한다. 구현 이후 코드 수정이 없으므로 컴파일/20개 회귀/실제 1·2인 영상 검증을 재사용하고, 제출 문서·staged diff만 검사한다.
- 저장·제출 완료: 구현 943b2cb2(MoHoDu) 및 18개 LFS 객체263MB 업로드. 원격 feat/escape-success-art와 feat/failure-ending-a를 fast-forward로 반영했다. PR https://github.com/gaboja-studio/POC_CURTAINCALL/pull/382 의 제목/본문/영상 링크를 전체 구현 기준으로 갱신했고 dev 대상 OPEN/MERGEABLE·PM 검토 요청 유지 확인. dev 병합은 하지 않았다. 다른 worktree를 변경하거나 원격을 force하지 않았다.
- 제출 문서/범위 재검사 PASS. new-pr DryRun은 의도적으로 남긴 캐시/private 미커밋 및 당시 미push 상태만 보고했으며 신규 PR 대신 기존 #382를 갱신했다. 원격 제출 후 로컬 잔여 변경은 자동 폰트 캐시3종·escape-labels·Vivox 설정·private 캐시로, 이번 커밋에서 제외하고 보존했다.
