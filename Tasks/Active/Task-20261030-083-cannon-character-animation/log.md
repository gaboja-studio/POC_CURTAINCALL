# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

## 2026-10-30 착수
- 기존 제출과 분리한 feat/cannon-character-animation, 기준99d9b58d. doctor1회: sandbox에서 Unity 설치/ThirdParty/gh 접근 제한, 기존 Task 중복077/078/079. 권한 있는 조회에서 Unity6000.3.25f1 ready, private submodule26d5009 초기화 확인. CLI beta15 최신, Blender5.2.3 연결.
- 공용 HumanoidPlayerPresentation·모델 연결 경로가 기존 캐릭터 작업과 겹침. 시작 git clean, 기존 소품 Blender 씬 보존.
- 현재 탑승=Crouch, 비행=Flying 단일 상태. 운반은 Carry 상체 레이어만 사용하며 손잡이 접점 보정이 없다.

## 제작·적용
- AI 동작 참고 그림 2장: Previews/motion-reference.png, rescue-reference.png. 그림 속 문구를 게임 규칙으로 쓰지 않고 실제 고정 절단 0/4/12/13 단계에 맞췄다.
- Blender 원본 cannon-motion.blend + 44 FBX(11동작 × 생존4단계), 자체 제작 편집용 마네킹. private Rabbit 리그는 읽기만 했고 원본 메시·텍스처는 복제하지 않았다.
- CircusPlayer.controller를 네 캐릭터 프리팹에 적용. 대포 얼굴 포구 정렬, 상승/정점/낙하/착지/재반동, 운반 점프 상체 레이어 및 남은 손 접점, 외줄 구조 두 역할을 연결했다. root motion은 끄고 기존 호스트 권한·속도·부상 지연을 유지한다.
- 사용자가 원형+손잡이 외곽을 선택하여 TrampolineOutline 원통+세 봉 swept contact를 추가하고 실제 HumanCannon 씬에 활성화했다. Grab은 손잡이 위치 기준이다. 구형 더미 AABB 경로는 유지한다.
- Blender 리그 단독 FBX의 첫 프레임 기준 자세/90도 축 문제: 동일 리그 이름과 작은 Hips bind marker, FBX_SCALE_UNITS, 검증된 sourceAvatar로 해결. Engine 함정에 기록했다.
- 절단 착지에서 이미 웅크린 골반에 다리 길이를 다시 빼던 오류를 제거. 네 캐릭터 실제 절단 스킨의 지지 높이를 검사하고 새 접지 동작은 소폭 아래로도 정렬한다.
- 외줄 팔 도달 부족은 구조자 무릎/허리 굽힘을 키우고 접근 위치를 조정했다. 구조 진행률은 기존 공유 PullProgress 사용. 테스트용 원격 배우는 공연 부상 참가자가 아니므로 ServerSetOneArm/ServerRestorePart로 권위 있는 부상 상태를 준비한다.

## 집중 검증
- Editor CannonMotionTests 5/5: 네 Avatar × 44 상태 연결, 남은 팔 동작, 엎드림, 루프, 새 지지 동작의 절단 표면 접지. TrampolineOutlineTests 6/6: 원주/손잡이/빈 코너/옆·아래/회전·이동 sweep.
- 실제 LAN 호스트 PlayMode: 네 캐릭터 손잡이/포구, 절단4단계 운반 점프, 발사→정점→낙하→실제 트램펄린 반동, 안전 착지 통과. 외줄 두 역할/절단4단계 완료 통과 후 접지와 취소 복귀 추가 검증 중.
- 시각 증거는 Previews/play-*.png. 자동 플레이는 한 프로세스 호스트와 합성 원격 배우로 확인하며 실제 다중 클라이언트 네트워크 지연/최종 플레이 감각은 검증 범위 밖이다.
- verify-knowledge: OK. 최종 check-work 및 집중 검사 결과는 아래에 기록한다.

## 최종 집중 검사
- 최종 CircusMotion PlayMode 5/5, 66.76초: 네 캐릭터 접점/탑승, 절단4단계 운반 점프, 실제 비행·반동·착지, 외줄 양 역할/절단4단계/취소·재시작·완료 모두 통과.
- AppliedRoundTrampoline PlayMode 4/4, 19.04초: 실제 중앙·원주·손잡이 반동의 원래 속도/높이 유지, 빈 사각 코너 비반동 확인.
- Editor 5/5 + 형상 단위 검사 6/6. sourceAvatar·44클립 Humanoid 임포트 정상, Blender 최종 액션44/빈 액션0. 최종 변경은 일반 이동의 기존 지면 보정을 유지하고 새 접지 동작에만 양방향 작은 보정을 적용한다.
- 원래 private 서브모듈 clean. Unity 프리팹 저장 중 PinkGirl/BluePierrot 손목 quaternion의 소수점 마지막 자리 직렬화 정규화가 함께 발생했으며 크기 변화는 약 1e-8이다.
- 기존 외팔 잡기 지연/거리 이탈 취소/즉시 놓기 PlayMode 1/1, 7.19초 통과. 집중 검사 합계 Editor11 + PlayMode10. 네트워크의 실제 여러 클라이언트 간 지연 검증은 포함하지 않는다.
- 외형 교체 시 운반 레이어 캐시를 비워 새 Animator에도 해당 손잡이 동작을 적용하도록 정리했다. 테스트용 runInBackground 변경은 원래 false로 복원했다. Unity는 원래 Start 씬으로 복귀한다.
- 첫 check-work는 새 미리보기 PNG 6개의 대문자 파일명 규칙 위반으로 중단됐다. 소문자로 바꾸고 생성 스크립트 출력 이름도 수정했다. 기존 문서 길이 경고33개는 변경 범위 밖이며 hard 오류는 없다.

## 애니메이션 확인 씬
- 사용자 추가 요청: CircusMotionGallery.unity 저장. 독립된 실제 모델16개로 44클립을 비교하며 uGUI에서 동작/정지/재시작/타임라인/반복/자동순환/속도/확대/카메라360도 회전·고도·줌을 조작한다. 기본 시점은 정면이다. production 프리팹 수정 없이 갤러리 내부 사본만 사용했다.
- 런타임 실제176상태(11동작×16배우)의 컨트롤러 이름·절단 표면·좌표 검사, UI 버튼 확대 전환 및 구간 탐색 통과. 서로 다른 시점에서 Time.time과 포즈 진행 확인. UI 포함 정면 화면은 Previews/gallery-front.png. 사용법 gallery.md.
- 생성과 Play 전환에서 각 명령이5초 제한을 넘어 응답했지만 작업은 완료됐다. 저장된 씬/ready 및 playing 상태를 조회해 확인했다. 직접 YAML 편집은 하지 않았다.
- 테스트용 runInBackground=false 복원. 새 갤러리 씬을 저장된 상태로 열어 둔다. 빌드 씬 목록과 게임 씬은 변경하지 않았다.

## 저장·푸시 보정
- 사용자 저장/푸시 요청으로 gh 계정 MoHoDu 작성 커밋 생성. GitHub가 갤러리 씬355.28MB를100MB 제한으로 거절했다. LFS 에셋 업로드는 완료됐다.
- 원인: 편집 상태에서 실제16개 배우를 전개·샘플링한 씬에 데이터가 크게 직렬화됐다. 씬에는4개 캐릭터 프리팹 참조만 저장하고 Play 시작에16개 배우와 레이블을 생성하도록 경량화했다. 에디터 API로 기존 갤러리 사본만 제거·저장했으며 GUID를 유지했다. 씬13,940바이트.
- 원격에 거절된 로컬 커밋만 amend하여 큰 씬이 전송 이력에 남지 않게 보정하고 일반 push한다. reset/discard/force는 사용하지 않는다.

## 최신 dev 병합
- 사용자 요청: origin/dev(0b28e57d, PR247 포함)를 feat/cannon-character-animation에 merge. 코드/지식/공연 씬7파일 충돌.
- dev의 HumanCannonCarry 공통 운반 등록/조작/권한, 최신 대포 이용 가능 조건과 공연 씬, ThirdParty a151164를 유지. 운반 상체 레이어는 Held 레지스트리 기준으로 조기에 적용하고, 실제 트램펄린을 잡은 때만 본 작업의 절단별 손잡이/점프 동작을 사용한다.
- 씬은 최신 dev 버전을 기준으로 에디터에서 usePropOutline=true 재적용. 기존 슬롯/원본에 YAML 조각을 합치지 않았다. 에디터 저장으로 새 pole/interaction 필드 기본값이 명시 직렬화된다.
- 본 브랜치의 원형 외곽 판정/반동 테스트 및 바인드 자세 지식을 유지하고, dev의 비균등 배율 손잡이 변환 함정과 깃발 양면 렌더링 지식 링크를 함께 보존했다.
- 최신 dev의 다인 봉 매달림 플레이어별 HangingPosition(index, actor) 경로도 유지한다. 병합 후 인간 대포 Editor531/531(10.14초), CircusMotion 실제 Play5/5(73.92초) 통과. 원형 외곽 접촉 Play 회귀 검사를 추가 진행한다.
- 원형 반동 Play4/4(19.88초) 통과: 빈 코너 비반동, 손잡이·테두리·중앙 반동 속도/높이 유지. 공통 운반 조작 회귀도 확인한다. 원격 다중 클라이언트 지연/최종 재미 확인은 이번 검사의 범위 밖이다.
- 공통 운반 이동/속도·놓기·재시작·나가기 복구 Play1/1(7.48초) 통과. 최종 합계 Editor531 + Play10 통과. 테스트가 만든 CheerGauge 동적 폰트 캐시만 최신 dev/index 내용으로 복원·재임포트하고 테스트용 background 설정을 원래 false로 복구했다.
- 병합 커밋32dda84b(부모8a9526e4 + origin/dev 0b28e57d) 저장·푸시. check-work 안전성/문서/컴파일 PASS, dev 원격 최신 커밋 재확인.
- dev PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/248 . 기능·충돌 해결·검사·플레이 제약과 갤러리 사용법을 본문에 기록했다. 지정 팀원 검토 후 Squash 대상이며 dev 병합은 실행하지 않았다.

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
