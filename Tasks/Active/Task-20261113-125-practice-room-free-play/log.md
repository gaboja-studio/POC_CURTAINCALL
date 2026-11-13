# Log

## 2026-11-13 자유 연습 공간 1차
- 시작: Task-115 변경을 커밋·push(8e6451b5)한 뒤 `feat/practice-room-free-play`를 만들고 origin/dev(2d440dc3) 병합. 충돌은 Task-115 log.md뿐(내 쪽 유지). 외부 에셋 저장소는 dev 기준 8ece127e로 맞춤(미커밋 `CharacterArt/OldCostumes`는 그대로 보존, 비공개 저장소 커밋 필요).
- 재연습 삭제: `RehearsalPracticeStation`·`RehearsalCannonPracticeStation` 삭제, 외줄 재연습 모드·투표·Dev 버튼, 대포 `CanPractice`/`RequestPractice`, 문구 키 20개. 최초 리허설(외줄 5소구간, 대포)은 그대로. 삭제 기능만 검사하던 테스트 8개 삭제, 나머지는 컴파일되게 최소 수정(실행 안 함).
  - 남김: `GameSettings.WaitingRoom.Rehearsal`(대포 리허설이 계속 사용, 라벨에 "재연습" 표현 남음), `WaitingRoomHud.asset`의 옛 문구 항목(에셋이라 미수정), 대포 리허설을 이수 뒤에도 시작할 수 있는 내부 경로(`StartPractice`, 진입 UI는 이수 뒤 안 뜸).
- 배치 방식: 공연 코스 프리팹을 여러 개 띄우면 씬당 1개 전제(Current·고정 메시지 이름)와 충돌 → 탈출로와 같은 "씬에 놓은 독립 외줄 코스 1개 + 단독 봉(HumanCannonPole) + 바닥·계단 준비 프리팹"으로 구성. 판정·도착·투표 없음.
- 수치(링 가운데 기준, 오른쪽 +x·위 +z): 줄 높이 2.5m, 줄 간격 3m, 연속 봉 간격 8m, 봉은 줄 위 0.6m에 발(리허설 값). 외줄 4개 18m, 그네 구간은 지름 방향 42.5m라 양 끝 발판이 링 가장자리 밖으로 1.2m 떠 있음(높이 2.5m). 계단은 원본(1.83m)을 같은 비율로 키움. 대포는 탄도(앞 8·위 12m/s) 기준으로 봉(바닥에서 7.8m)·중간 발판·도착 지역 배치.
- 표시 조건: 외줄·그네는 런 진행 중이고 리허설 시간대가 아니며 외줄·대포 리허설이 링을 쓰지 않을 때. 대포 구간은 여기에 인간대포 해금 + 대포 리허설 이수. 운반 트램펄린도 이때 등장(이전: 해금 즉시).
- 부상: 대기실 공통 설정 `SafeFallLandings`(켬)로 이미 무피해라 코드 추가 없음.
- 삭제한 씬 오브젝트: `LegacyRing/PracticeLanes`(탑·사다리·그물·옛 줄·바닥 테이프·장식 대포·장식 트램펄린), `LegacyRing/CollisionRepairs`. 인간대포 해금 기물의 "연습실로 옮겨진 기물" 목록 비움.
- 캡처: `1113-freeplay-top.png`(위에서), `1113-freeplay-view.png`(링 안에서).
- 에디터: dev 병합 전 종료 과정에서 에디터가 창 배치 저장 단계에서 비정상 종료(씬은 저장·커밋 뒤라 손실 없음).
- 미확인: 실제 플레이(줄 균형·옆줄 점프·그네 잡기와 간격·대포 발사와 착지·트램펄린), 다른 화면 동기화. 추격 NavMesh는 다시 굽지 않음.

- 2026-11-13 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-11-13 제출 준비
- 커밋 a5d7f11b 뒤 origin/dev(371c0481) 병합, 충돌 없음. dev에 같은 번호의 Task-20261113-124(result-and-rewards)가 먼저 올라와 이 Task를 125로 바꿈.
- 에디터 자동 변경 재질·글꼴 5개는 stash("editor-noise font/material before practice-room commit 2026-11-13")에 보관, 커밋 제외.
