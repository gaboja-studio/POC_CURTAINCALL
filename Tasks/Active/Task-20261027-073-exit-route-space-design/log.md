# Log

이전 기록: [History/031](History/031-before-fire-spectator-log.md).

## 2026-11-02 맵 렌더 비용 1차 최적화
- 활성 Renderer2,989/그림자 조명111 확인. A Spot 그림자110→11, 주광 포함12로 축소. Collider/Mono2,142개 설정 동일·씬 dirty0 저장.
- 편집1280x720 교차 반복 렌더 평균 입구5.98→3.98ms, 컨베이어2.57→1.64ms, 마지막구역2.39→1.95ms(변동 큼). 실제 플레이 FPS 미측정. 상세 map-performance.md.
- 최종 check-work 안전성·문서 규칙·컴파일 PASS, A 저장 dirty0. 실제 게임 FPS/최저 프레임은 미측정이며 자동 테스트 SKIPPED.

## 맵 최적화 후속
- 기계11곳 원거리 삼각형486,750→243,366, 정적617 Renderer→78묶음(16m), 활성 메시2700→2161. 복원→재생성 결과 동일, 원본/묶음 삼각형76,177개 동일, 충돌1648개 불변.
- ParticleSystem266개 AlwaysSimulate→Automatic, 추격불 지속 .45→.35. 단일구간14효과의 같은 시드/3초 표본140→112입자. 분출 배율은 기존값. 편집8구도·ABBA렌더 비교, 실제 게임 FPS 미측정.
- Rebuild 검증은 CLI30초 응답 제한을 넘겼지만 네이티브 작업이 완료돼 verified.json 결과와 저장 상태를 재확인했다. 맵 직접 편집 전 Optimization/Restore editable geometry 메뉴 사용. 자세한 절차 map-performance.md.
- 최종 check-work 안전성·문서 규칙·컴파일 PASS, 열린 A dirty0. 자동 테스트 SKIPPED. 네이티브 복원/재생성 및 기하/충돌 검증 결과는 verified.json에 보관했다.

## 2026-11-03 구조 이음새·면 겹침 폴리싱
- 최적화 묶음 복원 후 원본803개를 검사. 겹친 면 후보113→0, 메시57개에3~15mm 깊이 보정. 닫힌 면 유지. 보수판4개/코어12개 접합 정렬, 작성 도구 반영.
- Collider1648개 필드/Mono 설정 동일, 코어 높이는19.92→20으로 월드 범위4cm씩 보정. 편집9구도 확인, 원본617→78묶음 재생성·저장. 실플레이 이동 중 확인 미실시. surface-polish-followup.md 참조.
- 최종 check-work 성공 증거의 현재 작업 지문 일치 확인: 안전성·문서 규칙·컴파일 PASS, 자동 테스트 SKIPPED. 열린 A isDirty=false 저장 확인. 실제 Play 미실행.

## 2026-11-03 그네 이탈 뒤 공중 조작
- dev/작업 브랜치 fetch, HEAD와 origin/dev 차이0/0. 현재 Unity 편집 상태로 실제 통합 불필요. doctor는 기존 private 미커밋 에셋/중복 Task ID로 FAIL, CLI 버전/skill 링크 경고. 기존 변경을 보존하며 이번 코드 수정에는 직접 영향 없음.
- 원인: 그네 이탈 뒤 A/D가 수평 관성 전체를180도/초로 회전시켜 짧은 입력도 착지 방향을 계속 바꾸었다. 실제 Play는 사용자 범위대로 실행하지 않았으며 코드 경로를 확인했다.
- 공통 HumanCannonPilot/CannonAirControl 수정: 관성은 보존하고 카메라 기준 수평 보정만 추가(0.1초 지수 응답/키 해제 시 감쇠). 기존 수평 상한/상승 탄도/부상 배율 유지. 반동 W/S의 같은 입력만 최대0.15초 유예, A/D·새 방향은 즉시 적용. 일반 대포/트램폴린 경로 유지. A 배치/씬/대기실 미변경.
- EditMode CannonSeatTests41/41 PASS: 횡보정/반대입력 제동·상한/높이·키 해제/잠금/재도약 초기화·30/60/144fps 응답·카메라 방향·부상 보정값 확인. .harness/local/pole-air-control-tests.json. A dirty=false 저장 확인, 실제 조작 체감/멀티플레이 미검증.
- 최초 최종 컴파일은 비행 상태 필드명 변경의 기존 장비 코드 참조 누락(CS0103)으로 FAIL. 기존 필드명을 유지해 장비/PlayMode 회귀의 참조까지 보존하고 다시 검사한다.
- 보완 후 최종 check-work 안전성·문서 규칙·컴파일 PASS, 최신 어셈블리로 EditMode41/41 재확인 PASS. A dirty0. 실제 Play/MPPM은 실행하지 않았다.

## 2026-11-03 원본 외줄 UI 대조
- dev/작업 브랜치 fetch, origin/dev 차이0/0. 같은 Task doctor 결과 재사용. 사용자에게 다른 UI 종류를 비동기로 확인하면서 균형 UI를 먼저 대조했다.
- 원본 Tightrope 씬을 읽기 전용 추가 로드/해제해 실제 그래픽/레이아웃 비교. 같은 PerformanceHud 프리팹이지만 A 위험 트랙12/채움4px 대 원본23/15px, 스케일 MatchWidthOrHeight 대 Expand 차이 발견. 균형 값·위험 비율·입력 표시·배치/색 연결은 같은 경로다.
- A의 해당 크기/스케일·편집 샘플 플래그를 원본에 맞추고 저장. 실제 씬 diff는 기존 UI 프리팹 인스턴스1개 오버라이드만, 추가/삭제 문서0. 원본 씬/프리팹·대기실 미변경. 작성 도구에도 동일 치수 반영. 실제 Play 미실행, 세부 증거 .harness/local/rope-ui/.
- 재비교에서 동적 TMP SubMesh를 제외한 균형 그래픽/배치32개 동일, 두 CanvasScaler Expand 확인. 최종 check-work 안전성·문서 규칙·컴파일 PASS, 자동 테스트 SKIPPED, 열린 A dirty0.

## 2026-11-03 원본 재사용·UI 전반 조사
- dev/작업 브랜치 fetch, dev 차이0/0. 같은 Task doctor 결과 재사용. 게임 코드/씬/설정은 이번 조사에서 변경하지 않았다.
- 실제8개 씬·프로젝트 Mono845개·UI Rect1451개 읽기. 누락 스크립트0/감지된 끊긴 참조0. A 타이머/장치 알림 CanvasScaler 차이2곳, 공통 균형 키 A/D 하드코딩, 관전C11/외줄 안내/C7·C15 연결 차이 확인. ui-reuse-audit.md에 확정 차이/설계 차이/의도된 설정 구분.
- 같은 네 가지 타이머 스냅샷을 편집 복제본에 전달해 숫자·크기 상태 비교. 실제 Canvas 런타임 글자 크기는 미확정. HeldPropsView 부재는 Humanoid 도구 표시가 있어 결함에서 제외. 공용 메뉴/장비/음성/C6 및 맵별 반동/480초 유지 확인.
- 증거 .harness/local/reuse-audit/; 원본은 저장 없이 닫았으며 A만 열림 dirty0. 실제 Play/MPPM/자동 테스트 미실행.
- 최종 check-work 안전성·문서 규칙·컴파일 PASS, 자동 테스트 SKIPPED. 조사 문서 저장 완료.

## 2026-11-03 재사용 UI 보수·추가 조작 조사
- dev/작업 브랜치 fetch, dev 차이0/0. doctor 같은 Task 결과 재사용. U01~U08 수정, 표시 전용 공용 관전/안내 연결·원본/A 균형/관전·보조 툴팁 키 재지정·탈출 외줄 C8 설명 추가. ui-reuse-fixes.md 참조.
- 추가 결함2곳: 자유비행 상태 방어의 Retired 누락, 메뉴 입력 차단 중 관전 대상 변경. 공통/원본 이동/부상/장비 및 인원 조건도 대조, 맵별 튜닝 유지. 원본의 비착지 장식 투영 규칙·운반자 PlayerMover 경로는 결함으로 오판하지 않음.
- A native 연결/개발 환경8항목 통과, Transform/Collider 불변, 원본/공용 UI/대기실 씬 미변경. 신규 표시 회귀3/3, 원본 연결/자유비행7/7. 묶음 문자열 필터0개는 제외하고 개별 선별 검사 진행.
- 원본 회귀는 Undo가 늦게 반영된 A의 저장 확인 창에서 대기. Computer Use로 Unity 화면 확인·Save 후 완료, Undo flush 후 저장하도록 도구 보완. 실제 Play/MPPM 미실행.
- 선별13필터177/177 PASS, 저장 A의 네이티브 연결/개발 환경8항목 재확인 PASS. 이동 Transform/Collider 동일, 현재 씬 dirty0. 결과 .harness/local/reuse-fix/test-summary.json 및 verified.json.
- 최종 check-work 안전성·문서 규칙·컴파일 PASS. 묶음 스크립트 자동 테스트는 SKIPPED이며 별도 선별177/177 결과를 사용한다. 실제 Play 미실행, 저장 완료.

## 2026-11-03 화염 사망 관전 조사
- dev/작업 브랜치 fetch, 차이0/0. doctor 재사용. 직전 수정 전 A의 비활성 관전이 유력 원인. 현재 저장 A는 관전 enabled/active=true·Director 연결/공용 C11 유효. 일반불14개 절단·추격불1개 즉사 유지.
- 추격불 ServerKill→탈출 Retired, Director/Spectator 모두 Retired/Fallen 처리. 사망 관찰1.5초 뒤 생존자 선택 경로에 별도 누락은 발견하지 못함. 당시 실제 화면 재현 없이 원인을 단정하지 않는다.
- 신규 사망/관전3가지·표시 연결3가지6/6, 추격불14/14·최종 함정4/4 총24/24 PASS. 결과 fire-spectator-investigation.md 및 .harness/local/reuse-fix/fire-*.json. 게임 동작 추가 수정/실제 Play 없음. 이전125줄 log는 History/031 보관.
- 최초 최종 검사는 log 보관 후 남은 EOF 빈 줄로 문서 검사 FAIL. 끝 공백을 정리하고 다시 검사한다.
- 보완 후 최종 check-work 안전성·문서 규칙·컴파일 PASS. 별도 선별24/24 PASS, 실제 Play/MPPM 미실행, A dirty0.

## 2026-11-03 갈대밭·The End? 엔딩
- origin/dev/작업 브랜치 fetch·dev 차이0/0, 기존 미커밋 작업 보존. 같은 Task doctor 결과 재사용. 기존 풀255군락/바위3종과 새 갈대20묶음·연속 흙 표면 적용, 정적 렌더 묶음 재생성·A 저장. exit-meadow-ending.md 참조.
- 도착1초 뒤1.8초 제목/레터박스 표시, 전원 정산 성공 후 갈대밭 고정 카메라6초 뒤 기존 Hub 전환. 기존 전원 도착/리타이어 집계·성공1명 이상·480초 불변. 대기실/B/C 미수정, 실제 Play/MPPM·commit/push 없음.
- 편집4구도/제목 미리보기·참조/기본 비활성 상태 정상, 기존 Collider1647문서/도착 볼륨 동일·신규 식생 충돌/조명0·누락 스크립트/셰이더 오류0. 배경 부모 XZ1.25 배율을 보정해 시설 좌표·바닥 높이 정합 확인.
- 신규4/4·타이머12/12·시설 배치2/2·런 시간16/16·공용 표시6/6 최종40/40 PASS. 최초 공용 표시5/6은 새 회귀의 InputGate 재무장 상태 누수였으며 테스트 격리 보수 후4/4·6/6 재실행 PASS. 실제 FPS·다인 연출은 미검증.
- 최종 check-work 안전성·문서 규칙·컴파일 PASS, 스크립트 자동 테스트 SKIPPED/별도 선별40/40 PASS. A만 열림 dirty0, 실제 Play/MPPM 미실행. 갈대밭/엔딩 및 Task 기록 저장 완료.

## 2026-11-03 화염 예고·인원별 절단기
- Unity 저장/종료 후 dev56363116/private051db4ab 최신화, 기존 작업 stash 백업/복원·CommonMenuUI 자동 통합/미해결 충돌0. 사전 doctor FAIL2는 기존 private dirty·중복 Task ID이며 보존, 이번 수정 진행에 영향 없음.
- 공장 배칭의 상위 Transform 참조 누락으로 빨간 경고가 고정 메시로 추출된 원인 확인. 고정 빨간3묶음 비활성·경고14분기 복구·작성 도구 보호·초기 숨김 적용. 일반 화염/추격 피해와 시간표 유지. flame-warning-and-crew-saws.md 참조.
- A 발사기4대/고유ID0~3을 접속 인원당1대로 제공. 운반 중 보존·여분 컨트롤러는 배선 공유 계속 수신·장치 겹침0/24배선 참조 유지. 기존2대 위치/발사·운반 수치 불변.
- 최종 EditMode52/52 PASS, A의 경고26곳/130위상·1~4인 표시 개수·참조/배치 통과. 최초 경고1/2는 EditMode 수명주기 fixture 보수 후2/2 PASS. 기존 Collider1647·Cue 설정·도착 볼륨 불변, 새 장치 Collider6개 추가·A 저장. 실제 Play/MPPM·commit/push 없음.
- 후속 코드 검토에서 여분 장치를 숨길 때 비행 중 톱날이 멈출 수 있는 경로를 보완했다. 장치 운반/표시와 투사체 업데이트/표시 준비를 분리해 이미 발사된 날은 충돌까지 계속 비행한다. 관련 신규1개 포함 ExitSaw39/39 재실행·최종합53/53 PASS.
- 보완 후 최종 check-work 안전성·문서 규칙·컴파일 PASS, 자동 묶음 테스트 SKIPPED/별도53/53 PASS. 네이티브130위상/장치수·겹침·참조 재확인 PASS, A dirty0. 로컬 dev와 작업 HEAD 모두56363116, 원격 dev 차이0/0·private051db4ab. 저장 완료.

## 2026-11-03 심부 외피·정적 면 재검사
- dev fetch 차이0/0, 기존 dirty 보존/A 저장 백업. doctor FAIL2는 기존 private dirty와 중복 Task ID, 이번 기하 검사 진행에 영향 없음. 이전 이름 필터 검사가 파생 메시를 놓친 원인을 확인해 실제 기하 판정으로 보완했다.
- 심부 콘크리트/암부 차폐 동일 면 중복과 편집 사선 자글거림 확인. 외피3개 중복 렌더 제외·시각 차폐2면 폭을 상부 이음에 맞춤. 기둥/코어/하강 외벽·점검대/레일과 모델 내부 면도 실제 삼각형으로 보수. shell-seam-review.md 참조.
- 불투명 정적1678메시/LOD 상호배타 구분 후 구조 간 같은 방향 중복169쌍→0, 동일 메시 내부297평면 그룹→0. 총41필터를53단계 파생 메시로 보수, 원본 패키지 미수정. 별도 도색/습윤층·반대 방향 접합과 비축 정렬/투명/동적 면의 범위를 구분한다.
- Collider1653/Mono370 씬 문서·도착 볼륨 불변. 수정 필터의 원래62193정점·UV0~7/법선/탄젠트/색·서브메시 보존 확인. 기하5/5 PASS, 심부4+전체9 편집 구도 확인·사선 패턴 해소. 실제 Play/MPPM·성능 체감 미검증.
- 최적화617원본→78묶음 재생성, 원본/합성 삼각형76177 동일·원본 렌더 활성0·누락 스크립트/메시0·A dirty0 저장. 누적 기존 변경과 사용자 배치를 유지했으며 commit/push 없음.
- 최종 check-work 안전성·문서 규칙·컴파일 PASS, 자동 묶음 테스트 SKIPPED/별도 기하5/5 PASS. A만 열림 dirty0, 충돌/게임 설정 보존·평면 중복0/0·렌더 묶음/시각 확인 완료. 저장했고 실제 Play/MPPM은 실행하지 않았다.

## 2026-11-03 누적 작업 저장·제출
- 사용자 저장/제출 승인. Unity 저장/종료 후 private main의 README 갱신을 포함한 resource/exit-route-polish-20261103에서 Task073의529파일 저장, c4fe7c5f를 branch/main에 게시(LFS30개/23MB 업로드 완료). 외부 원본/파생 에셋은 비공개에 보존하고 공개에는 gitlink만 반영한다.
- 공개 제출은 아트/최적화·봉/공중 보정·공용 UI/관전·갈대밭 엔딩·화염 예고/인원별 발사기·심부 면 보수 누적분 전체다. 기존 선별 검사/편집 시각 검증 결과를 재사용하며 실제 Play/MPPM은 실행하지 않는다. dev56363116 포함/차이0, dev 병합은 하지 않는다.
- Unity 저장/재진입에서 CommonButtons SemiBold 아틀라스가1024→1px로 비워진 자동 변경을 발견했다. 해당 파일만 별도 stash에 보존하고 dev 정상본을 유지해 이번 제출에서 제외했다. 다른 기존 변경은 보존한다.
