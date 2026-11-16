# Log

- 2026-11-16 origin/dev 096ccac6에서 조사 브랜치를 시작했다. 이전 봉 Task 문서와 에디터 종료 에셋은 stash에 보관했다. 저장소 도구가 보관된 봉 Task와 같은 번호를 배정해 조사 Task를 137로 정리했다.
- 보존된 Editor.log에서 전원 집결 후 첫 Tightrope 공연 상태로 정상 진입했다. Tightrope 씬 자체 로드 536.118ms. 같은 전환에 공연 배경 performance_background 로드 11997.608ms, 그중 Deserialize 11577.326ms.
- Unity가 메인 스레드 정지 11975ms를 기록했다. Relay가 inactivity timeout을 반환했고 이어 NGO가 호스트를 TransportFailure로 종료했다.
- 같은 로그에서 이후 공연 배경 로드는 2430.609~2722.764ms였다. PerformanceBackgroundLoader는 공연 씬 진입 직후 배경을 로컬 additive 비동기 로드한다. 첫 로딩 지연의 상세 동기 호출이나 에셋은 프로파일 표본이 없어 미확정이다.
- 이번 요청은 원인 조사까지 진행했다. 플레이 재현과 게임 코드 수정은 하지 않았다.

## 첫 진입 프로파일

- 최신 origin/dev 096ccac6에서 저장된 리허설 완료 → 집결 → 첫 외줄타기 PlayMode 검사 2회 PASS. 기존 단일 LAN 호스트·WaitingRoomBase 픽스처를 사용했다. 같은 다인 Relay 환경은 이번 원인 보고 단계에서 실행하지 않았다.
- 배경 로드 3024.947ms 및 2781.354ms. 첫 계측에서 Tightrope 2782.650ms 프레임의 2686.666ms가 EditorLoop로 나타났다. 두 번째 에디터 루프 상세 계측에서 2613.218ms 프레임 중 Loading.LockPersistentManager 자체 시간이 2430.598ms였다.
- 호출 경로는 SceneHierarchyWindow.Paint → EditorResources.Load → AssetDatabase.GetGUIDFromAssetPathV2 → Loading.LockPersistentManager였다. 같은 프레임에서 PlayerLoop 전체 112.586ms, 비동기 에셋 통합 및 GC 경로 69.205ms.
- 에디터 Hierarchy 갱신의 동기 에셋 조회가 진행 중인 공연 배경 로드와 겹쳐 메인 스레드를 기다리게 하는 병목을 확인했다. 이전 11.975초 Relay 실패와 같은 진입 지점이지만 실패 프레임 자체에는 CPU 스택이 없다. 참가자별 로그 및 빌드에서도 같은 원인인지는 미확정이다.
- 프로파일 원본 Logs/Performance/long-frame-20261116-164044-f628.json. 게임 코드·Relay 설정 변경 없이 조사했다. 실행이 만든 에셋 변경은 별도 stash에 보관했다.

## 2인 Relay 비교 진행

- 동일 096ccac6 소스의 방장·참가자 프로세스로 실제 비공개 Relay에 접속했다. 통합 WaitingRoom에서 기존 호스트 튜토리얼 단계 완료 경로를 사용하고, 각 피어를 입구에 배치해 기존 집결 판정으로 공연을 시작했다.
- Hierarchy 켬: 양쪽 모두 Tightrope 씬 로드(방장 2.026초, 참가자 1.251초) 후 배경 로드와 함께 연결 종료. 배경 27.301초/25.676초. 방장 inactivity timeout과 NGO 호스트 종료, 참가자도 TransportFailure. 프레임 프로파일의 Hierarchy 동기 에셋 조회 잠금 25.381초/24.254초.
- Hierarchy 끔: 두 편 모두 외줄타기 및 배경 로드 후 15초 동안 연결 유지. 배경 4.238초/3.616초, 진입 최대 프레임 4.109초/3.477초. 남은 긴 프레임은 SceneView.Paint의 동기 에셋 조회 잠금 3.872초/3.235초.
- 한 PC 두 에디터이며 Windows 실행 파일 빌드의 셰이더 작업이 같은 PC에서 진행 중이었다. 닫은 조건은 켠 조건 이후 실행되어 캐시가 더 데워졌을 수 있다. 각 조건의 호출 스택에서 실제 잠금 주체를 별도로 확인했다.

## 콜드 진입 및 실행 파일 결과

- 사용자 제보: M2의 다른 플레이어들도 첫 다인 진입은 끊기고 이후는 정상. 해당 배포본 및 원격 PC의 로그는 확보하지 않았다. 이에 따라 Hierarchy 끈 조건을 새 에디터 프로세스로 추가 실행했다.
- 에디터 Hierarchy 끔·새 프로세스: 방장·참가자 모두 15초 유지. 진입 최대 프레임 8.934초/7.967초. 앞의 재진입 4.109초/3.477초보다 길다. 두 피어 모두 SceneView.Paint의 잠금(8.692초/7.755초)이 주된 대기였다. 새 프로세스여도 이 PC의 OS 디스크 캐시는 유지됐다.
- Windows Development 빌드 096ccac6: 같은 PC 두 실행 파일로 첫 새 프로세스 2인 Relay 검증. 양쪽 정상 진입 및 15초 유지. 최대 프레임 1.207초/1.133초. 실행 파일을 다시 띄운 반복도 양쪽 유지, 0.185초/0.196초. 반복은 같은 게임 프로세스에서 재접속한 검사가 아니다.
- 로그 원본과 UTC 역할별 요약은 .harness/local/relay-entry-matrix/{editor-on,editor-off,editor-off-cold,build,build-repeat}/의 host.log·guest.log·*.events에 남겼다. 에디터 프로파일은 각 조건의 *-profile.json.
- 게임 씬·코드·설정 변경 없음. 진단용 소스를 Assets에서 로컬 로그 폴더로 옮겼고 생성 에셋은 stash에 보관했다. 도구가 만든 외부 에디터 캐시는 자동 실행 정책이 재귀 삭제를 거부해 로컬에 남아 있다.

## 수정 착수

- 재개 시 Unity 종료를 확인하고 origin/dev 6a841241까지 fast-forward했다. dev의 다른 Task 137과 번호가 겹쳐 이 조사 Task를 138로 변경했다. 기존 private 변경은 보존했다.
- doctor: private 미저장 3건 및 다른 worktree의 기존 Task 번호 중복으로 FAIL. 현재 Task/Unity 버전은 정상이며 Unity 컴파일·테스트 실행에는 지장이 없다. CLI beta.13 경고는 실제 Pipeline 명령 성공으로 확인했다.
- Relay 할당 전에 양쪽 배경을 준비하고 영속 루트에 보관하도록 공통 로더·접속 진입점을 수정했다. 첫 공연에서는 추가 배경 씬 로드 없이 재사용하며, 비공연에서는 비활성화한다. 기존 GameSettings 규칙·게임플레이·타임아웃 수치는 바꾸지 않았다.
- 준비 중 접속 취소, 참가 제한시간 시작 시점, Single 씬 전환 생존, 피날레 조명의 배경 루트 조회를 연결했다. 준비 후 배경 에셋은 프로세스 종료까지 한 벌 상주한다.
- 배경 PlayMode 12/12, 접속 EditMode 7/7 PASS. 결과 .harness/local/relay-entry-fix/background-playtests.json 및 connection-edittests.json.
- 중간 check-work는 진단 스크립트를 Assets 최상위에 둔 경로 규칙 2건으로 FAIL(컴파일 생략). 진단 스크립트를 기존 Scripts 하위로 옮겼으며 최종 제거 후 검사한다.

## 수정 후 새 프로세스 검증 (6a841241 기반)

| 조건 | 방장 최대 진입 프레임 | 참가자 최대 진입 프레임 | 결과 |
| --- | --- | --- | --- |
| 새 Windows Development 실행 1 | 169.2ms | 184.4ms | 양쪽 PASS |
| 종료 후 새 Windows 실행 2 | 165.4ms | 179.1ms | 양쪽 PASS |
| 새 에디터·Hierarchy 켬 | 930.8ms | 833.4ms | 양쪽 PASS |
| 새 에디터·Hierarchy 켬·각 배경 로드에 12초 정지 주입 | 778.4ms | 720.5ms | 양쪽 PASS |

- 모든 조건은 실제 Relay, 새 방장·참가자 프로세스의 첫 Play/첫 실행에서 튜토리얼 완료 → 입구 집결 → 첫 Tightrope → 30초 관찰이다. 같은 PC이며 OS 캐시는 초기화하지 않았다. 수동 코스 완주나 다른 PC/4인 검증이 아니다.
- 모든 피어가 `backgroundLoad listening=False`에서 원본 배경을 1회 로드하고, 연결 후 첫 공연에서는 같은 활성 루트를 재사용했다. 12초 정지는 진단용 sceneLoaded 콜백에만 주입했으며 양쪽 모두 Relay 시작 전에 끝났다.
- 별도 관찰: 에디터 로비→대기실에서 Hierarchy 에셋 잠금 9.53초(참가자 정지 9.03초)가 남아 있다. 이 구간은 이번 공연 배경 재사용 수정의 범위 밖이다. 첫 외줄 진입 정지 및 실제 연결 종료와 구분하며 전 구간 무중단을 보장하지 않는다.
- 일반 에디터 검사 PASS 후 종료 처리 중 guest 에디터의 메인 스레드 응답이 멈췄다. 테스트 인스턴스만 종료하고 백업을 보관했다. 다음 실행의 복구 확인 창을 처리한 뒤 Computer Use 세션을 종료했다. 추가 지연 검사 후 두 에디터는 정상 종료했다.
- 로그·프로브 소스·빌드: .harness/local/relay-entry-fix/. 테스트 생성 폰트/머티리얼 11건은 별도 stash에 보관했다.
- 사용자 저장·제출 요청 후 다시 fetch하여 origin/dev 0f65367a로 fast-forward했다. Start 메뉴와 WaitingRoom 변경이 포함돼 최신 기반으로 재검증한다. 새 dev Task 138들과 충돌하지 않도록 이번 Task를 139로 정리했다.

## 최신 dev 최종 검증 (0f65367a 기반)

- 배경 PlayMode 12/12 및 접속 EditMode 7/7을 재실행해 모두 PASS. Windows Development 빌드 성공(2분 5초).
- 새 Windows 프로세스 첫 진입: 방장 169.7ms / 참가자 180.4ms, 양쪽 30초 연결 유지 PASS (`final-build-cold`).
- 새 에디터·Hierarchy 켬·각 배경 준비 12초 정지: 방장 788.2ms / 참가자 741.2ms, 양쪽 30초 연결 유지 PASS (`final-editor-cold-slow`). 양쪽 모두 정지는 Relay 시작 전, 실제 첫 공연에서는 배경 재로드 0회.
- 최신 대기실에서 별도 에디터 로딩 정지는 방장 13.449초 / 참가자 8.597초였다. 이번 측정에서는 연결이 유지됐지만 다른 환경에서의 연결 위험까지 해결한 것으로 보지 않는다.
- 모든 진단용 프로세스를 종료하고 프로브·빌더·meta를 Assets 밖의 로컬 증거 폴더로 옮겼다. guest 테스트 패키지 설정을 원복하고 자동 생성 폰트·재질 11건은 별도 stash로 보관했다. 사용자의 기존 private 변경은 유지했다.

- 2026-11-16 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)
- 제출 직전 origin/dev 89421ccd를 추가 반영했다(카메라/상호작용 변경, 이 수정 파일과 충돌 없음). 새 dev Task 139와 번호가 겹쳐 이번 Task는 140으로 정리했다. 사용자의 즉시 제출 지시에 따라 추가 플레이 재검증은 하지 않는다. 위 실제 2인 Relay 결과의 기반은 0f65367a이며 최신 병합 후 상태와 구분한다.

- 2026-11-16 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
