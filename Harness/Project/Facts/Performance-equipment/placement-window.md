---
summary: 외줄 설정 코스와 인간 대포 씬 장치의 난이도별 배치 편집 및 저장 경계
status: active
updated: 2026-11-18
source: Task-20261118-158, 사용자 후속 요청
---

# 공연 배치 편집

## 언제 읽나
공연 배치 창, 난이도별 위치, 새 장치의 실제 동작 등록을 수정할 때.

## 사실과 주의
- `PerformanceCheerWindow`와 `.Layout`은 기존 IMGUI 창을 확장한다. 메뉴는 `Tools/CurtainCall/공연/공연 배치`. [사용 가이드](../../../../Docs/Guides/performance-placement-guide.md).
- 환호 포인트는 `difficultyMask`(1/2/4, 기존 7), 공유 배치 분리 시 새 pointId를 발급한다. 표시와 호스트 획득 모두 같은 난이도를 검사한다.
- `PerformanceLayout`은 씬의 Transform 참조와 3개 로컬 pose/active를 저장한다. 캠페인 난이도로 초기 Awake와 변경 시 적용한다. 서버/클라이언트가 같은 authored 목록 인덱스를 유지하도록 삭제는 해당 난이도에서 제외한다.
- 편집기는 preview 적용 전 원본 pose를 보관하며 창 종료/리로드/Play 전/씬 저장 전에 복원한다. Save 후 미리보기만 다시 적용한다. 미리보기 모델·카메라는 HideAndDontSave.
- 새 대포·톱날·봉·운반 장치·추격 불은 원래 시스템의 직렬화 배열 끝에 등록한다. 중간 삭제/정렬로 네트워크 인덱스를 바꾸지 않는다. 복합 프리팹은 연결된 자식을 함께 복제해야 한다.
- 외줄은 GameSettings.Tightrope의 placementStages에서 선택 난이도만 독립 복사한다. 기존 추가 톱날과 baseHorizontalCount의 밀도 예외를 복사/직렬화한다. inline class의 null 직렬화에 의존하지 않고 enabled 플래그로 기본 코스 사용을 구분한다.
- 톱날 제외는 HorizontalSawEntry.excluded로 번호와 장애물 순서를 유지한다. 비활성 인간 대포 장애물/불/운반 장치는 표시뿐 아니라 접촉·탑승 판정도 제외해야 한다.
- 회수 시 Renderer bounds를 합칠 때 정지된 ParticleSystem의 원점 빈 bounds를 포함하면 고리가 화면 밖이어도 숨겨지지 않는다. 살아 있는 파티클만 포함한다.
- 예상 탄도는 발사 속도·공통 중력을 쓴 참고선이다. 선택 공연·난이도의 실제 확인은 DevStartLauncher로 진입한다. 종료 시 임시 Dev 설정을 복원한다.

## 검사
`PerformancePlacement` EditMode와 `CheerPointToggle` PlayMode. 고리 보상 테스트는 일반 회피·착지 환호 관찰을 잠시 끄고 고리의 중복 보상만 확인한다. 중단된 coroutine의 finally에만 의존하지 않고 TearDown에서도 변경한 공통 설정을 복원한다.

- 링 크기는 Transform 균일 배율로 저장. Crossed의 InverseTransformPoint와 파티클 Hierarchy 스케일이 같은 배율을 따르며, 복제/분리도 크기를 보존한다. 로프 두께는 별도 공통 설정이다.
