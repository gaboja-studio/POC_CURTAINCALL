# Log

## 2026-10-30 착수
- 최신 dev f6c3e61e(PR248 병합)를 fetch하고 Unity 정상 종료 후 새 브랜치 생성. 열린 갤러리 씬은 저장 상태였음.
- doctor: 기존 Task ID 중복 5종 및 Unity 설치 탐지 FAIL. 실제 설치 경로와 CLI 6000.3.25f1 연결 확인. CLI beta15 최신.
- imagegen 참고 시트 2회가 violence 분류로 거절됨. 기존 Blender 소품을 정적 배치/렌더링한 참고 시트를 먼저 만드는 방식으로 전환(애니메이션/상태 구현 전).
- 종료 시 TMP 동적 캐시 3개 변경 발생. 자동 승인 검토가 restore 폐기를 거절해 그대로 보존: CheerGauge Bold, CommonButtons SemiBold, escape-labels. 작업 시작 전 git clean이었음.

## 구현·확인
- 참고 시트 완성/확인 후 Blender 원본에서 포신과 차체를 분리. 4 Action/NLA take, 단일 FBX 및 Unity 4 AnimationClip 생성. 새 원본은 회복 Action 선택 상태로 열리며 원래 정적 원본은 보존했다.
- 비활성 -32도, 14cm 후퇴와 감쇠 덜컹임 후 1.2초 내려앉기, .35초 휴지, 2.8초 네 번 회복. 각각 한 재질을 쓰는 포신/차체 Renderer 2개. 메시 바닥 간격 약 .40m.
- 호스트 Launch 확정 시각을 73바이트 State 메시지에 포함. 늦은 Sync는 현재 구간 seek, Reset/Offline은 시각 초기화. 외부 InteractionAvailable을 유지하며 ReadyToUse로 회복 완료까지 탑승/발사 차단. 컴포넌트 비활성→재활성도 회복 연결.
- CC_Cannon.prefab GUID 유지, HumanCannon 10 Station 설정 적용. 대기실 장식 인스턴스는 갱신 프리팹의 활성 자세. Seat/Muzzle/BoardingPoint·발사 속도·게임 Collider 보존.
- 초기 EditMode 전체: 기존 531 PASS, 신규 6 중 실제 형상 대신 AABB를 검사한 1개 실패. 실제 정점 검사로 수정 후 신규 6/6 PASS. 최종 OnEnable/Disable 반영 후에도 6/6 PASS.
- 실제 LAN 호스트 Play: BarrelMotion_ 3/3 PASS (21.48초), 발사 순환/앵커 고정/재탑승, 외부 잠금 반복 갱신, 컴포넌트 재활성 확인.
- 기존 캐릭터 회귀 4/4 PASS: 네 캐릭터 손/포구, 4절단 운반점프, 발사-정점-낙하-반동, 안전착지. 외줄 구조는 CLI status가 지원되지 않는 editor status를 호출해 로그 실패했으며 해당 조회 없이 단독 재실행 1/1 PASS (20.12초). 이전 Task083 Previews는 실제 회귀 재생으로 갱신됐다.
- 갤러리 5모드×5지점=25 seek 검사, 정면/측면/3/4/후면 버튼 확인. 프레임 2180→3328, 재생 시간이 실제 전진함. 4포즈 Unity 렌더 및 UI 스크린샷 저장.
- 시작부터 꺼진 Station도 OnDisable 호출 유무와 무관하게 비활성 자세를 갖도록 SampleBarrel에서 활성 여부를 확인한다. 갤러리 패널/카메라 경계는 화면 비율에 맞춰 계산한다.
- 최종 포신 EditMode 7/7 PASS(.33초), Play 3/3 PASS(21.50초). 백그라운드 실행 설정은 원래 false로 복구·저장했다. 첫 check-work는 새 미리보기 4개의 대문자 파일명 규칙에서 실패하여 파일/생성 스크립트 이름을 소문자로 수정했다.
- 다중 클라이언트 실기기 지연·중도 참가 검사는 미실시. 공유 시계/seek/Reset의 결정적 검사는 통과했지만 실네트워크 검증과 구분한다. 자동 승인 거절에 따른 글꼴 캐시 보존 외 기능 작업 차단 없음. commit/push 미요청으로 실행하지 않음.

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 사용자 요청으로 대포 포신·공통 쿨타임 작업만 저장·push 및 dev PR 제출. 2026-10-30 fetch 기준 origin/dev f6c3e61e와 작업 기준 일치. 글꼴 캐시 3개와 Task083 테스트 스크린샷은 보존하고 커밋에서 제외.
- 최종: 위 check-work의 테스트 SKIPPED는 별도 실행한 집중 테스트와 구분한다. 포신 EditMode 7/7, 포신 Play 3/3, 기존 캐릭터 4/4+구조 재실행 1/1 통과. 갤러리 저장/비실행 상태로 열어 둠. 새 브랜치의 dev 추적은 해제해 후속 일반 push가 dev를 대상으로 하지 않게 함. 신규 commit/push 없음.

## 공통 쿨타임 추가
- `HumanCannonSettings.asset` 공통 4~20초 범위, 기본 5초. 하강 1.2초·회복 2.8초 유지, 나머지만 비활성 대기. Station의 외형 적용 여부와 무관하게 F 차단하며 툴팁에 사용자 지정 문구 표시.
- 외부 OFF는 비활성, ON은 발사 후 남은 대기를 건너뛰고 회복. 이후 발사는 설정된 전체 쿨타임. 늦은 참가의 스냅샷에는 발사·활성화 시각 2개를 실어 해당 구간 seek.
- HumanCannon 10개에 더해 탈출로 A 4/B 5/C 3개 모든 Station에 실제 포신 프리팹 연결, 구형 Renderer 숨김·앵커 보존. 해당 세 씬은 다른 탈출로 작업과 겹칠 수 있음.
- 씬 포함 검사 실행기에서 완료 신호가 오지 않아 취소. `unity close` 30초 제한 초과 후 정상 종료 확인, 에디터 재시작 후 재검증. 별도 Unity 버그 리포트는 보내지 않음.
- 집중 EditMode 12/12, LAN 호스트 PlayMode 4/4 통과. 저장된 탈출로 12개 Station을 에디터에서 직접 읽어 새 포신·기존 Renderer 비활성을 확인. 다중 클라이언트 실기기 및 전체 탈출로 플레이는 미실시.

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
