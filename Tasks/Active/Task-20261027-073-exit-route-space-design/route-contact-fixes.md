# 양방향 외줄·실체 충돌·일반 화염 수정

2026-11-01 사용자3항목과 ‘반대편에서 W 대신 S를 눌러야 진입한다’ 정정을 반영했다. A 씬/관련 원본 기능 코드만 수정했다. 대기실·공용 UI·원본 에셋 파일은 변경하지 않았다.

## 수정 내용

- **외줄11곳:** 조작 전환 때 원래 작성 방향을 강제하던 것을 A 전용 bidirectionalSceneTraversal로 수정했다. 플랫폼에서 쓰던 카메라 전진축(사용 불가 시 캐릭터 방향)을 줄 축의 같은 방향으로 맞추고,이후 프레임에도 그 부호를 유지한다. 반대편에서도 W로 진입하며 S 후진도 유지한다. 반대로 볼 때 옆줄 점프 좌우·동료 뒤 착지 방향도 함께 맞췄다. 다른 씬은 기본값 false로 기존 동작을 유지한다.
- **다리3곳:** 전개/회수 중 Collider를 끄던 처리를 없앴다. 두 철판의 실제 충돌을 항상 유지하고 -80 순서에서 회전을 갱신,변경 때 Physics.SyncTransforms 후 플레이어를 이동한다. 원래 배치·반동·개방 조건/추가 발판 없음.
- **전체 배치 충돌:** 활성 MeshRenderer3416개 조사. 얇은 난간을 포함한 물리 검토 대상1525개 중1496개는 원본/보완 충돌체로 대응한다. A 인스턴스650곳에 실제 메시 형태의 MeshCollider를 추가했다. 벽/난간/정비 발판/배관·큰 설비가 대상이며 참조 원본 에셋/비공개 저장소 파일은 건드리지 않았다. [추가 목록650곳](collision-repairs.csv).
- **일반 화염14곳:** 깜빡임 예고0.65→1.15초(+0.5),작동1.6/휴지2.25초는 유지한다. 전체 주기4.5→5초에 맞춰 반대쪽 위상2.25→2.5초로 보정,두쪽 동시 분사를 피한다. A의 FireJet만 fireSeversLimbs를 켜 기존 호스트 ServerCut 경로로 사지를 절단한다. 화염 박스 접촉 모양·회전 고정·톱 밀도와 무관한 활성은 보존했다.
- **마지막 추격:** EscapePursuitFire의 즉사 ServerKill 경로/13구역·시작20초 유예·진행5.2초는 그대로다. 일반 화염 판정 변경과 분리했다.

검토 제외는 피해 효과/예고 범위·표면 장식/글자·반동/운반 자체의 접촉 볼륨·대포 투입구처럼 별도 기능이 담당하는 부분이다. 임의로 이런 효과를 벽으로 만들지 않는다. 컨베이어 낭떠러지에 새 바닥은 추가하지 않았다.

## 검증

- EditMode: CooperativeUpgradeTests23/23, TightropeSurfaceControlTests32/32, RouteGeometryTests46/46, PursuitFireTests14/14,합계115/115. 마지막 묶음 검사 결과는 log 참조.
- 외줄11×앞뒤22 경로: 실제 CharacterController 이동/낙하 없음·진입축/좌우 부호 확인. 입력 전환은 실제 CourseControlSwitcher 회귀 검사에도 포함했다.
- 다리6철판×5전개 자세30건: 실제 Collider 표면과 몸통의 면 관통 없음. 완전 전개 후4자리×양방향×3교량24경로 통과. 회수/전개 중 몸통600표본에서 교면 아래 관통0.
- 추가 MeshCollider650/650은 실제 표면 Raycast에 응답. 재분류에서 추가 수리가 필요한 물리 대상0. 이는 선택한 물리 자산 범주/표본의 검사이며 모든 프레임·각도의 실제 게임 경험을 보증하지 않는다.
- 추가 충돌 후에도 대포3/3 실제 CharacterController 비행이 원래 외줄로 착지. 외줄 지지99/99·화염 출구 그네308자세 간섭0 회귀 확인.
- 화염6쌍×5초/0.01초 표본 동시 분사0. 14곳 절단 설정/기존 박스 접촉·밀도 설정과의 분리,추격 타이밍/원래 즉사 경로 보존 확인.
- 트램폴린/휴대 절단기2대의 운반자 몸통과 새 충돌체 간섭0. 누락 스크립트/참조0,대포4/봉15/협동5/외줄11/추가 수신면0 보존.
- 실제 Play/MPPM은 실행하지 않았다. A 저장 완료,커밋/push 없음.

## 범위·증거

코드: EscapeCooperativeSpan, HumanCannonHazard/HazardCourse, TightropeCourse.SceneRopes, CourseControlSwitcher, RopeLaneJumpRules 및 기존 테스트3파일. 소실 주기6/3초·줄 폭0.55m/색·발사 톱2.2m/2초·시점/HUD/손은 유지한다.

`.harness/local/route-contact-current.txt` → `.harness/local/route-contact-20261101-2100/`. 기존54파일 SHA256 백업, before/setup/solid-inventory/solids-applied/final-physics/moving-deck/carry-audit/final-audit JSON을 보관했다. 초기 안전문 시안과 교면 바깥에서 출발한 잘못된 통행 표본은 최종 구현/검증에서 제외했다. 현재 씬에는 새 안전문/추가 바닥이 없다.
