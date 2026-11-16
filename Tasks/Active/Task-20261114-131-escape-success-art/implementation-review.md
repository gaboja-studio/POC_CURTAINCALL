# 구현 및 검증

## 적용
- SuccessEndingArt/SuccessMeadowTile을 기존 Cinemachine·실제 PlayerMover 달리기에 연결했다. 원본 모델/팔레트/부상/네트워크 소유권/결과창을 유지한다.
- GameSettings에서 낮 하늘과 원경 안개 색을 분리했다. 원경 서커스, 낡은 캔버스와 로프, 흔들리는 갈대, 흙/잔돌/젖은 얼룩을 추가했다.
- 원본 4종 셰이더 파생본에만 혈흔/섬유/오염을 적용한다. 혈흔 표시 설정을 존중하고 복귀 시 원본 재질과 조명을 복원한다.
- 실패 A 확인은 RestartAfterFailure로 세대+1/Day1/Morning/RestTime을 시작한다. 공연 클리어·해금은 초기화, 신체/조작 복구, 본 가이드·이수 리허설은 유지한다.
- 실패 버튼 '다음 런 시작', 목적지 도착 후 Warning 시스템 UI '탈출 실패'. 성공 버튼은 기존 로비 복귀다.

## 실제 실행 근거
- 1인 성공 12초 동안 72.84m/718프레임 이동, 타일 재활용과 바닥 유지. 로비 복귀 후 Normal·조작 가능·입력 차단/성공 재질 누출 없음.
- 원본 곰 녹색 의상 선택 후 실제 변형 유지 및 BearPaper 복원 확인. 파생 셰이더 6개 오류0, 원본 매핑 4/4.
- Deadline 및 EscapeAllRetired 각각 다음 세대 Day1/Morning/RestTime, 공연 해금 초기화, 정상 신체/조작, 세션 Playing 확인.
- 별도 메모리 fixture로 본 G11/미열람 N02, 이수 외줄, 인간 대포 이수 true/false 각각 보존 확인. 실제 리허설 완주 검사는 아니다.
- 실제 LAN 2인 성공 결과 탈출2명·부상 곰 달리기·로비 복귀, 전원 사망 실패 후 호스트/클라이언트 모두 새 아침 정상 복귀.
- Videos/의 네 MP4를 VideoPlayer로 디코딩하여 해상도/프레임/길이를 확인하고 결과 탭·목적지 프레임을 검토했다. 상세 조건은 Videos/readme.md.

## 환경과 범위
- 독립 worktree와 독립 Library. 기존 메인 Unity 및 다른 작업 폴더는 조작하지 않았다. MPPM 클론은 이 작업 에디터 소유다.
- 자동 생성 TMP atlas(escape-labels.asset), Vivox 설정은 이번 기능 변경 목록에서 제외한다. 원본/private 에셋은 수정하지 않는다.
- 기존 URP AdditionalLightsShadowCasterPass/Vivox 경고가 있으므로 Console 전체 오류0을 주장하지 않는다.
- 촬영 준비 첫 시도에서 로딩 중 전멸했다. 개발 전멸 보류·전원 복구·기존 호스트 체크포인트 RPC 배치 후 다시 녹화했다. 제품 규칙을 바꾸지 않았다.
- 복합 테스트 필터는 0개를 선택했으므로 PASS로 계산하지 않는다. 개별 fixture 재실행 결과와 최종 check-work는 log.md에 기록한다.
- 3/4인, 빌드 실행, 공연 전체 완주와 모든 기물 조합은 이번 검증 범위에 포함하지 않는다.
