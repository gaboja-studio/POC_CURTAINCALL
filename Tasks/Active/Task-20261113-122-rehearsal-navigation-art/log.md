# Log

- 2026-11-13 v2 피드백: 단색 고대비와 순차 등장/소멸 반복을 요청. v1은 보존, 청록 그래픽 신호로 원화1/마스크4/프리팹3/검토 씬1 재제작.
- 재개 doctor 1회: 기존 sandbox editor/third-party 오탐 및 Task ID 중복. 호스트 Unity 연결 정상. 최신 dev PR357(cfb3a3ed) fast-forward 반영. 두 tracked 변경을 `navigation-v2-preserve-pre-sync-20261113` stash에 보관; Facts index의 내비 링크는 최신 index에 재반영. 자동 축소 폰트 아틀라스는 적용하지 않았고 stash에 보존.
- GPU property block 펄스 구현, 기본4.2초/전파2.35초, 편집 모드 미리보기. 처음 halo를 alpha>0.5 경계로 정규화해 과대 확대된 문제를 전체 캔버스 기준으로 수정.
- v2 editor 검증 통과: 출발→도착/휴지/재반복/경로 갱신/직렬화11표식/참조/셰이더/LineRenderer0. Play Mode에서 자동 시간36.91→111.38초, consoleErrors/Warnings0 확인 후 종료.
- 실제 프리팹을24fps 고정 시간으로101프레임 렌더, 서로 다른 SHA256101개. MP4/GIF 저장. Pipeline eval은 await를 지원하지 않아 Task Source의 run_script 비동기 진입점 사용. 인코더는 OS temp에만 설치.
- v2 제출 전 Unity 종료 상태에서 dev 재fetch. 새 PR358(9a5baaa7)을 fast-forward 반영했으며 네비 파일 충돌 없음. 기존 폰트 아틀라스에 최종 변경 없음. Knowledge OK(20 folders). 검토 씬 다시 열고 최종 check-work 진행.
- v2 check-work 1회: 안전성 PASS, 문서/규칙 FAIL. 새 dev의 Task-20261110-115-waiting-room-passage-gimmicks/log.md 154줄 > hard130 때문에 컴파일/자동 테스트 단계 SKIPPED. 다른 Task 문서는 수정하지 않았다.
- 최신 dev 재실행 후 compilationFailed=false, compiling=false 확인 및 ValidateAndCapture 재통과. CLI status의 구 Pipeline 미지원 `editor status` probe 오류1건은 도구 호환 메시지로 기록하고 콘솔 정리. 이전 Play Mode 자체 오류0 검증과 구분한다.
- v2 단계 완료. 편집 모드 NavigationPulseReview가 열려 있고 반복 미리보기 활성. 실게임 네비는 미교체. 커밋/push 없음. 시작 전 추적 파일 보관 stash는 복구 증거로 유지한다.

- 2026-11-13 시작: doctor 1회. 샌드박스에서 editor/third-party 접근 실패, 호스트 확인에서는 Unity 6000.3.25f1 연결 및 private 저장소 clean 확인. 기존 Task ID 충돌은 별도 하네스 문제. 기존 작업트리 clean, Start 씬 미저장 없음.
- Unity 정상 종료 후 origin/dev fetch, 이미 현재 HEAD에 포함됨 확인. feat/rehearsal-navigation-art 브랜치와 Task 생성, Unity 재실행.
- 기존 RehearsalPathGuide는 View 정렬 LineRenderer와 별도 화살표 메시 사용. 이번 범위는 프리팹까지이며 기존 구현은 유지.
- imagegen 내장 도구로 원화1·독립 투명 PNG5 생성. private 폴더에 원본 그대로 보관. 모든 모서리 alpha=0, 링 중앙 alpha=0 확인.
- Unity AssetDatabase/PrefabUtility로 프리팹3·머티리얼·검토 씬 저장. 네임스페이스 Navigation이 Unity UI 타입과 충돌하여 Wayfinding으로 수정, 이후 컴파일 오류 없음.
- 초기 eval 기본 5초 타임아웃에도 에디터 조립은 완료되어 저장 상태 확인 후 진행. 이후 timeout 인자를 50초로 명시. CLI beta.17의 새 명령명 probe가 구 Pipeline에 콘솔 오류를 남길 수 있음.
- 첫 검토 캡처의 TextMesh 크기 초과를 보정. 최종 Camera.Render 캡처3 확인. 검증: 표식11, LineRenderer0, 참조·shader·퇴화/NaN 경로·풀 재사용·Hide 통과.
- 초기 전체 SaveAssets가 무관한 escape-labels 동적 폰트 아틀라스를 재저장한 것을 발견. 조립기는 새 머티리얼만 SaveAssetIfDirty로 저장하도록 수정. 에디터 정상 종료 후 작업 전 clean이었던 해당 파일만 HEAD 원본 바이트로 복구, git diff 일치 확인.
- 제출 전 에디터 종료 상태에서 origin/dev 재fetch, HEAD에 이미 포함됨 확인. 검토 씬 재실행. 커밋·push 없음. 지식 검증 Knowledge OK (20 folders).
- 최종 check-work 1회: 안전성·문서/규칙·컴파일 PASS, 공통 설정 링크 검사 PASS, 자동 테스트 SKIPPED. 별도 에디터 검증 결과는 Captures/validation.txt. 전체 PlayMode 테스트는 실행하지 않음.
- 최종 Unity 상태: NavigationReview 열림, dirty 0, Play Mode stopped, compilationFailed false, consoleErrors 0, consoleWarnings 0. auto-tick은 기존 enabled 상태 유지. 예상 밖 기존 에셋 변경 없음.
- 현재 요청 단계 완료: 원화1, 재질 PNG5, 프리팹3, 검토 씬1, 실제 Unity 렌더 캡처3. 다음 단계는 사용자가 요청한 시점에 기존 안내의 표시부 연결·계단/지면 투영·실게임 리허설 종료 검증. Task는 해당 후속 연결 추적을 위해 Active에 유지.

- 2026-11-13 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-13 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-11-13 호출 API와 실게임 연결
- 사용자 후속 요청에 따라 출발/목적지·로컬/공유·색상·속도·강조도 API, 독립 NavigationHandle 수명 관리, 전용 NavMesh 서비스와 지면 투영을 구현했다. 리허설 완료는 로컬 플레이어별 안내로 연결했다.
- 정상 소구간 완료→프리팹 표시→집결 해제 PlayMode 테스트 통과. 최초 테스트는 HostFinishSkip이 집결을 건너뛰므로 실패했으며 정상 TryClearCurrentStage 경로로 수정했다.
- 계약 EditMode 3/3 PASS. UDP 호스트/클라이언트(동일 프로세스)에서 늦은 참가자·정확한 경로/스타일·서버 위상·클라이언트 출발점·삭제·0배속과 실제 ReceiveState/ApplySnapshot 검증 PASS. 다중 PC/지연 환경은 미검증이다.
- Pipeline run_script 임시 DTO를 JsonUtility.FromJson에 전달하면 null이 반환되어 초기 통신 검증이 잘못 실패했다. 수신 카운터로 실제 전송을 확인하고 검증 DTO만 Newtonsoft로 변경했다. 게임의 컴파일된 JsonUtility DTO는 정상이다.
- 테스트에서 NavMesh.CreateSettings의 음수 ID를 확인했다. 생성 여부 bool과 pending 빌드 취소로 재베이크/해제 시 누수를 막았다. record-knowledge로 Navigation 사실 문서를 갱신했고 Knowledge OK(20 folders).
- 테스트용 전송기 초기 해제 순서에서 예외가 발생하여 Shutdown 후 대기하도록 검증 스크립트를 수정했다. 최종 통신 검증은 PASS, 기존 추격 NavMesh/TMP 경고는 별도 기능이다.
- 사용법: navigation-api.md. 실제 캡처/자동 검사 결과: Captures/V2. 공통 집결 규칙 및 GameSettings 변경 없음, 커밋/push 없음.
- 최종 캡처 재실행 중 외부 CLI 호환 명령 탐색(editor status)이 테스트 오류로 집계됨. 작업 에디터 고정 포트 7800으로 다시 실행해 1/1 PASS(21.52초), 청록/마젠타 실게임 캡처를 저장했다.
- 제출 전 작업 에디터(pid30912)를 정상 종료하고 origin/dev fetch: HEAD에 포함됨. 테스트가 자동 재저장한 폰트4개는 navigation-api-preserve-font-atlases-20261113 stash에 보존하여 기능 변경에서 제외했다. 기존 v2 stash도 유지했다.

- 2026-11-13 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)
- 최종 check-work: Safety OK(201 files), Structure/Knowledge OK. 기존 Task115 log 154줄(상한130) 때문에 문서 검사 FAIL 및 뒤쪽 Unity 검사 SKIPPED. 이번 기능의 별도 에디터 컴파일·계약3건·PlayMode1건·UDP 검증은 PASS이며 전체 검사를 PASS로 표기하지 않았다.
- 최종 에디터 pid26572: NavigationPulseReview 열림, Play stopped, compiling false, dirty false. RehearsalNavigationPulse 프리팹/시퀀스 참조 정상, LineRenderer 0. 포트는 재시작 후 재배정되므로 PID/프로젝트 확인 후 사용한다.

## 2026-11-13 화면 외곽 실루엣과 오브젝트 끝점
- doctor 1회: 기존 Task ID 중복과 샌드박스 editor/private 접근 실패. 호스트에서는 정상 연결·접근 확인. 시작 동기화는 에디터 종료 후 dev ff8af551을 fast-forward 반영하고 기존 작업을 복구했다. Facts/index 충돌은 escape-results-kit/Navigation 링크를 모두 보존하여 해결했다.
- 상호작용의 메시 셸을 제거하고 네비와 공용 화면 실루엣을 구현했다. PC/Mobile Renderer의 Render Graph mask/info/private-depth→외부 윤곽 합성으로 내부 겹침선 없이 벽 가림·스킨 포즈·로컬 카메라를 따른다. 색상/강조도와 상호작용 우선순위를 유지했다.
- 명시적 Object/Player 끝점은 아웃라인으로 표시하고 목적지 고정 문양만 숨긴다. 도착 파동 유지, 출발 파동 추가 및 지면 법선 정렬, Position 전환 시 문양 복원을 연결했다. 공유 식별자는 NetworkObject ID/자식 경로 또는 검증된 씬 경로다.
- EditMode 실루엣 픽셀/가림/다른 카메라/스킨 포즈/끝점 전환/경사면 파동 3/3 PASS(0.62초). 실제 리허설 및 전화기+플레이어 네비 PlayMode 2/2 PASS. 실제 캡처: Captures/Outline/real-phone-navigation.png.
- 상호작용 회귀 1/1 PASS(15.48초). 테스트의 옛 4m 배치를 현행 GameSettings 2.5m 안으로 조정하고 셸 생성 기대를 제거했다. 게임 거리/규칙 변경 없음.
- 광범위 Navigation 필터에 함께 잡힌 기존 PursuitNavigation_PatrolRestoresPathAfterListeningStop은 NavMesh 위 활성 agent가 아닌 상태의 SetPath 오류로 FAIL. 관련 네비 테스트 통과와 구분하며 추격 로직은 수정하지 않았다.
- 실제 UDP 두 NGO 관리자와 production ReceiveState/ApplySnapshot 검증 PASS: 늦은 참가·경로/색상/위상·개별 경로 격리·클라이언트 요청/삭제·spawned 대상의 원격 인스턴스/자식 아웃라인·파동 유지. 초기 테스트 프리팹 비활성 복제 때문에 실패했고 활성 템플릿으로 수정 후 통과했다. 다중 PC/지연 실험은 미실행이다.
- URP 검토는 outline-render-review.md, API는 navigation-api.md. record-knowledge로 기존 Navigation 사실/index를 갱신, Knowledge OK(20 folders). 알파 컷아웃 구멍·ForceLOD·XR/모바일 실기기 검증은 남은 범위다.
- 제출 전 에디터 종료·dev 재fetch 후 새 08600e05(PR362 및 가이드 노출 hotfix)을 fast-forward 반영했다. 현재 변경과 겹치는 파일 없음. 테스트 종료 뒤 남은 Graffiti 동적 폰트 변경은 navigation-outline-preserve-font-atlas-20261113 stash에 보존했다. 기존 작업/보관 stash 유지, 커밋/push 없음.

- 2026-11-13 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 이번 묶음 check-work 1회 최종 결과: Safety OK(227 files), 문서/규칙 PASS, 컴파일 PASS(up_to_date), 공통 설정 PASS. 자동 테스트는 선택하지 않아 SKIPPED이며 별도 실행한 위 테스트 결과와 구분한다. 현재 문서 검사에서는 기존 Task115 길이 오류도 사라져 hard 0이다.
- 재시작한 에디터 pid31056에서 PC의 SSAO+SilhouetteOutlineFeature, Mobile의 SilhouetteOutlineFeature, 프리팹/시퀀스/Resources 셰이더 참조 정상 확인. 컴파일 중 아님. 기능 완료, 사람 최종 경험 및 다중 PC 확인은 별도 남김.

## 2026-11-13 저장 및 dev PR 제출
- 사용자 저장·dev PR 요청을 받아 기존 RehearsalPathGuide→WorldNavigation→RehearsalNavigationPulse 연결을 확인했다. 최신 dev 28ef042b(PR361)를 에디터 종료 후 반영했다. WaitingRoomRehearsal.Local의 포획 중 복귀 방지 수정과 현재 안내 문구 수정을 모두 보존해 자동 병합했다.
- 최신 dev의 실제 리허설 소구간 완료→SpriteRenderer 프리팹 안내(LineRenderer 0)→집결 해제 PlayMode 1/1 PASS(31.84초). 결과 Captures/V2/rehearsal-pr-validation.json, 실게임 청록/마젠타 캡처 갱신. 최종 fetch에서 추가 dev 변경 없음.
- 에셋 32개 파일/이미지11개를 private 전용 브랜치 resource/third-party/rehearsal-navigation의 8ece127e에 저장하고 LFS와 함께 게시했다. 에셋 PR: https://github.com/gaboja-studio/CURTAINCALL_PaidAssets/pull/21 . 기본 브랜치 직접 push는 자동 승인 검토가 별도 승인 부재로 거절하여 실행하지 않았고 PR 병합 승인을 요청했다.
- private main 포함을 요구하는 게임 pre-push 규칙 때문에 에셋 PR 병합이 게임 PR 게시의 선행 조건이다. 그 외 기능 저장·검증 준비는 진행한다. Unity가 재저장한 Graffiti 폰트는 navigation-pr-preserve-font-atlas-20261113 stash에 보존하여 제외했다.
- 사용자가 에셋 PR #21 병합을 명시 승인하여 검사 SUCCESS와 head 8ece127e를 확인한 뒤 merge commit으로 병합했다. private origin/main 1253278a에 8ece127e가 포함됨을 확인했다. 기본 브랜치 직접 push는 하지 않았다.

- 2026-11-13 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
