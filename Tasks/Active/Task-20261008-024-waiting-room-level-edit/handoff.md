# Handoff

덮어쓰는 스냅샷이다. 다음 AI(또는 다음 날의 나)는 이 파일부터 읽는다.

## 지금 상태

- 단계: 3차 배치 완료(무대 진입 터널, Cloth 커튼, 깜박이는 전구, 안개·먼지). 커밋 전.
- 동작하는 것:
  - 방 배치·동선은 1·2차와 같음(대기실 기획서 3.3.1 조감도, 북 = +Z, 2.5m 격자). 루트 `WaitingRoom` 아래 방별 그룹.
  - ⑨+⑩을 폭 5m·높이 4.5m·길이 47.5m(x 10~57.5) 터널로 변경: 5m마다 나무 아치 늑골, 천장 따라 파이프·배선, 바닥 큰 배관·밸브, 낡은 방수포, 바닥 돌무더기·흙더미(먼지), 바랜 깃발 줄. 백스테이지 슬롯 문 10개는 그대로(닫힌 컨테이너 문 + 차양 + 전구).
  - Cloth 커튼 9장(전부 Unity 기본 `Cloth` + `SkinnedMeshRenderer`, 메시·벨벳 재질은 씬에 내장):
    - 좌우로 갈라진 붉은 커튼: 분장실 입구, 소품실 입구, 터널 입구(로비 쪽), 공연 스테이지 입구(터널 끝 큰 문 앞). 숙소·화장실은 제외.
    - 분장실 안 탈의 커튼 1장(기존 정적 커튼 대체). 씬에 정적 커튼 메시는 없음.
    - 윗변 고정, 나머지 자유. 플레이어에 트리거 캡슐 `PlaySetup/Player/ClothCollider`(r 0.38, h 1.6)를 붙여 커튼이 플레이어에 밀려 흔들림. 트리거라 이동(CharacterController)에는 영향 없음.
  - 공연 스테이지 입구: 커튼 뒤 큰 문 앞에 따뜻한 점광(그림자)·스포트라이트를 두어 커튼 틈과 플레이어가 커튼을 밀어 젖힐 때 빛이 새어 나옴. 커튼 둘레 전구 테두리.
  - 깜박이는 전구 27개: 레거시 `Animation` + 씬 내장 클립 `WR_Flicker`(Light 세기·전구 렌더러 on/off, 무작위 간격). 터널 안쪽으로 갈수록 깜박이는 등 비율을 늘림. 폐기물 보관소·숙소 구석 등도 일부.
  - 안개·먼지: Synty `FX_Fog_Flat_01`(푸른색으로 색 덮어쓰기)·`FX_Dust_Interior_01` 13개(로비, 터널, 폐기물 보관소, 소품실, 숙소). RenderSettings 안개(Exp2)는 유지.
  - 배치 스크립트: 이 폴더 `build-layout.cs`(`unity command eval_file --file`로 실행 — 32KB 넘어 `eval` 인자 불가).
- 막힌 것 / 미구현:
  - "플레이어가 다가가면 커튼이 자동으로 걷힘"은 미구현. 근접 감지·열기 동작은 스크립트(코드)가 필요한데 이 Task는 코드 작성 금지(meta Goal, setup 스크립트 폴더 없음). 지금은 플레이어가 커튼을 몸으로 밀어 젖히는 물리 반응만 있음. 자동 열림은 기능 Task로 PM 요청 필요.
  - Cloth는 플레이어 캡슐하고만 충돌(벽·소품과는 충돌 안 함). 다른 플레이어(멀티)와의 충돌은 네트워크 플레이어 구조 확인 필요.
  - `pwsh` 미설치 → `check-work`/`verify-*` 실행 불가, unity-editor-mcp 연결 실패.
- PR: 없음

## 다음 할 일

1. 사람이 Play로 확인: 커튼 통과 시 흔들림, 깜박임 빈도, 안개 농도, 터널 길이감.
2. 자동 열림 커튼이 필요하면 PM에게 기능 Task 요청.
3. `pwsh` 설치 후 `pwsh -File Scripts/check-work.ps1`.

## PM이 확인할 것

- 확인 요청: 커튼 자동 열림(근접 트리거)은 코드가 필요해 이 Task 범위 밖 — 기능 Task로 분리할지.
- 확인 요청: `PlaySetup/Player`에 자식 `ClothCollider`(트리거 캡슐)를 추가함. Player는 프리팹 인스턴스라 씬 오버라이드로만 저장됨(프리팹 원본 미수정).
- 교훈 제안: 투명 재질을 에디터 스크립트로 씬·렌더에 쓰면 Unity가 `.mat`에 `_SrcBlendAlpha`/`_DstBlendAlpha` 줄을 저절로 추가함(서드파티 `PolygonApocalypse_01_A_Alt.mat`, `Generic_Glass.mat`에서 발생, 되돌림). 시험 렌더엔 `new Material(원본)` 복제본 사용 (함정, Engine)
- 교훈 제안: Windows에서 `unity command eval`에 32KB 넘는 C#을 인자로 넘기면 "Argument list too long". `unity command eval_file --file <Windows 경로>`를 쓴다 (함정, Engine)
- 확인 요청: 서커스 전용 에셋 부족(빅탑·간판 등)과 조감도 ⑤/⑥·백스테이지 슬롯 수 불일치는 이전과 동일.
- 대기 중인 결정: 없음
- 공용 파일 요청: 없음

## Verification

- 구역 검사: 스크립트 미실행(`pwsh` 없음). 수동 — 변경은 `waiting_room.unity`와 이 Task 폴더뿐. 서브모듈 `Assets/_ThirdParty`는 깨끗함(자동 변경 재질 2개 되돌림). 잘못 만든 Harness 함정 문서는 지우고 위 교훈 제안으로 옮김.
- 저장: `save-work.ps1` 미실행(`pwsh` 없음)으로 수동 검사 후 로컬 커밋만 함. push 보류, gh 작성자 확인 못 함(git 작성자 mingyu / gyumin9048@gmail.com).
- 1 문서/규칙: 미실행(`pwsh` 없음)
- 2 컴파일: 코드 변경 없음. 에디터 `compilationFailed=false`.
- 3 테스트: NO_PROJECT_TESTS (배치 작업)
- 4 실행 로그: Play 모드 진입 후 일시정지 + `EditorApplication.Step()`으로 프레임 진행해 확인. Console 오류 0·경고 0.
  - Cloth: 플레이어를 터널 입구 커튼으로 통과시키자 커튼 끝이 최대 1.75m 밀려났다가 1.7초 뒤 0.74m까지 흔들리며 복귀.
  - 깜박임: Animation 재생 중, 세기 1.44 → 0.07(전구 꺼짐) → 1.20 확인.
  - 이동: 바닥 266칸 콜라이더 있음, 문·개구부 통과(터널↔입구 구간의 상자 더미는 옮겨서 해결), 스폰에서 모든 방 도달(폐기물 보관소 95%), 큰 문·슬롯 문은 막힘.
- 5 공동 테스트: 미요청
- 참고: 씬 파일 약 5.0MB.
