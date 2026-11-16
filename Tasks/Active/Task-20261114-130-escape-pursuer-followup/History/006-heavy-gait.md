# 2026-11-16 무거운 질주와 발소리

## 범위

- 사용자: 발이 너무 자주 닿는 달리기를 거구의 넓은 보폭으로 수정하고, 고음이 적은 묵직한 쿵쾅 소리로 변경.
- 시작 전 Editor 종료 확인 → origin/dev fetch/FF, 0f95ba6891b6417d85bb65ab79f8f96ab6ed4345 최신 상태. 기존 변경을 보존한다.
- 게임의 실제 이동 속도·공격/피해/경로·플레이어 공통 규칙은 변경하지 않는다. 이번 검사는 전체 맵 완주 결과를 대체하지 않는다.

## 구현

- 기존 Revision7 207본·9스킨을 사용하는 새 Run만 생성했다. 원본 Blender/FBX·기존 Run과 나머지24모션·소켓/부위 매핑을 보존한다.
- 주기5.94→8.58m, 한 발2.97→4.29m, 원본 속도6.6m/s 기준1.3초/좌우2착지. 실제6m/s 추격에서 초당2.02→1.40착지, 약31% 감소. 접지 비율.36→.30, 발 들기.55→.72m, 착지 후 골반 압축과 공중 회복/팔 반동을 함께 작성했다.
- 최초 과도한 다리 신장4개를 소스 검사에서 거부했다. 골반 높이/60fps 샘플을 교정한 v2는 IK clamp0, 접지 높이/월드 미끄러짐1µm 이하, 루트 이동0, 단위 스케일 오차0.000005 이하, 공중구간26/26 확인.
- 기존 프로젝트의 UnderstageHeavyStep/UnderstageConcreteAndSteps에서 좌우 발소리0.86초/충격1.08초를 파생했다. PCM48kHz mono, peak−3.5dBFS, 1.5kHz 이상 에너지0.00003% 미만. 큰 저음 몸통과 낮은 콘크리트 질감/짧은 잔향을 사용한다.
- Play() 재시작을 PlayOneShot으로 바꾸고 착지/공격 충격은 별도 AudioSource로 분리했다. 발소리 도플러를 제거하고 숨은20m/s 통로에서는 최소0.45초 간격으로 합친다. 실제 정상/가속 추격의 접지는 유지한다.
- 별도 거리 누적 대신 현재 애니메이션 반주기 통과로 접지를 센다. 정지 때 포즈를 정렬해도 다음 걸음이 어긋나지 않고 큰 프레임 간격은 접지 카운터만 보존하여 한꺼번에 소리를 쌓지 않는다.
- 보스 Leaping의 이동 거리를 보행으로 세지 않는다. 착지 프레임도 이동 전 상태를 기준으로 검사해 별도 착지음과 중복하지 않는다.
- 공용 prefab·현재 LabyrinthPrototype·운영 escape_route·UnderstagePursuerPlaytest에 연결. 롤백용 PursuitRemodel 씬은 저장하지 않는다. R7 재연결 builder도 파생 모션/거리 값을 보존한다.
- 애니메이션 전용 FBX는 Unity가 armature 컨테이너를 제거하므로 경로를 기존 PursuerDollRig 하위로 복원한다. 최초 Unity 검사는31개 중3개가 이 경로 오류로 실패했고, 해당 문제를 수정했다. 상수 커브의 중복 키만 제거해 클립76.7→11.6MB로 줄였다.

## 증거와 남은 검사

- 생성/검사: `.harness/local/pursuer-weight-20261116/heavy-858-v2/verification.json`, `audio/audio-verification.json`.
- 재생성 소스: Task Source의 heavy-pursuer-run.py, build-heavy-pursuer-steps.py, import-heavy-gait.cs.
- Unity 최종 회귀31/31 PASS, 실패0. 기존25모션의 리그/스케일·포획·투척·절단 유지와 새 양발 접지·공중 회복·정지 재출발·프레임 건너뜀·보스 이동 상태를 검사했다. `tests-corrected.json`에 보존.
- Unity 제작 재질로32프레임을 렌더했고 접지/반동/공중 자세를 확인했다. 첫 렌더 타깃 초기화로 frame0이 비어 warm-up을 추가했다. 나머지 자세는 정상 렌더됐다.
- 실제1인 호스트의 기존 showcase Run을8초 관측:469샘플, 착지음10번, 순항 간격 약0.72초(짧은 코스 재출발 구간은 더 길다), AudioSource pitch1 유지. 별도 Play Mode 주입 검사에서는 AI를 정지한 채20m/s 이동분만 전달해478샘플/접지37/소리13, 최소 음 간격0.630초를 기록했다. 중간 착지 충격에도 발소리 pitch1 유지. 실제 숨은 통로를 걷거나 전체 맵을 완주한 결과로 주장하지 않는다.
- 첫 고속 probe는 showcase 비활성화가 chase를 복원하는 것을 확인해 폐기하고, chase/breach를 명시적으로 비활성화한 격리 probe로 다시 실행했다. 테스트 종료 후 Play Mode와 원래 플레이 인원 설정을 복원했다.
- 원본 PursuitRemodel 씬 SHA256 `9556701BDC3E8F88B246C38B41F8253C22E3804AA7E33FB5D3F113CB00190814` 보존. Knowledge 검사20폴더 PASS. 최종 check-work/DEV 확인은 아래에 덧붙인다.
- 음색의 최종 청감과 게임에서의 압도감은 사용자 확인 대상이다. 자동 수치 검사를 청감/멀티플레이 완주 성공으로 기록하지 않는다.

- 최종 check-work exit0: 안전성/문서·규칙/컴파일/공통설정 PASS. 자동 전체 테스트는 선택하지 않았으며 위 별도31/31 결과를 유지한다. 기존 문서 길이 경고는 hard0이다. frame0 재렌더도 정상 확인했다.
- 제출 전 Editor 실제 종료를 확인하고 origin/dev fetch/FF를 다시 수행했다. HEAD/origin/dev는 동일한0f95ba6891b6417d85bb65ab79f8f96ab6ed4345, Already up to date. 커밋/push 없음. 임시 import worker 설정을 복원하고 현재 Prototype을 다시 연다.
- 최종 Editor 확인: LabyrinthPrototype, clean=true, Run=Golem_RunHeavy, 거리8.58m, Play Mode=false, 자동 review=false. 사용자가 바로 Play할 상태다(`final-editor.json`).
