# 대기실 W4·W5와 수화기

## 적용
- 대상: `Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity`.
- `UI/WorldLabels/W4_ExpertStatus`: Assets `WorldLabelCloth` 인스턴스. 커튼 상단 장식 앞 world(9.6,3.33,76.5)에서 전문가 서비스의 실제 상태를 읽는다. 옛 글자 위치 x10은 벽/장식 뒤라 보정했다.
- `UI/WorldLabels/W5_ShelfSlot_1`~`4`: Assets `WorldLabelShelf` 인스턴스. 아래부터 1~4칸이며 Scene 뷰에서 위치를 조절할 수 있다.
- `UI/WorldLabels/Items`: 실행 중 배송·바닥·손·선반 물건마다 Assets `WorldLabelItem`을 생성한다. 아이템 이름/표시 도형을 읽고 수령·이동·삭제를 따라간다.
- UI는 공간 씬 소유. 완성 프리팹/제공 코드/폰트/PNG/9-slice는 수정하지 않는다. 인계 package 리소스는 사용하지 않는다.

## 수화기 원인과 배치
- `origin/feat/waiting-room-art` ba661044와 현재 씬 모두 Phone은 world(-18,1.45,70)의 작은 임시 Cube였다. 삭제된 전화기 프리팹은 없었다.
- 폐소품 천 더미와 겹쳐 찾기 어려운 자리를 피하고 world(-18,1.5,70.8)에 기존 Assets `Synty/PolygonApocalypse/Prefabs/Item/SM_Item_Phone_01`을 인스턴스로 연결했다.
- Hierarchy: `WaitingRoomGreybox/WaitingRoomGameplay/Phone/Handset`. 원래 Cube MeshFilter/Renderer는 제거하고 ShopPhone/ExpertServicePhonePanel은 유지한다.
- 배송구는 (-18,.76,70.8), 수화기 조명은 새 위치를 비춘다.
- 추가 발견: 배송구 Y90° 회전인데 배출 거리가 -0.6이라 물건이 서쪽 벽 뒤 x=-18.6으로 나갔다. +0.6으로 수정하고 2개씩 네 줄로 두어 x=-17.4~-16.2, z=70.6/71.0의 방 안 바닥을 사용한다. 주문 한도8/가격/수령 규칙은 그대로다.

## 데이터 연결과 표시
- W4: `ExpertRoomView.StatusText` → `ExpertService.Phase/Buyer/Availability`, CampaignRun 일차·밤 여부. 낮 영업 안내, 의료 대기, 시설 담당, 부재중, 구매자 입장 대기, 이용 중, 복구 완료를 반영한다.
- W5: `ShopDeliverySlot`, `FloorPropsView`, `HeldPropsView`, `HandoverShelfView`가 `ShopWorldLabel`로 실제 이름/도형 위치를 제공한다. UI가 구매/소유 상태를 바꾸지 않는다.
- 근접 기준은 로컬 플레이어(네트워크에서는 Local 소유자), 기본 진입3m/이탈3.3m. WaitingRoomWorldLabels Inspector에서 조절한다. 카메라 뒤·벽 뒤·입력 차단 중 숨김, 카메라 방향을 바라봄.
- C6 배송 안내는 행동 줄과 주문번호·구매자 보조 줄을 나누어 표시한다. F 대상은 기존 ShopReach.Focused이다.
- 선반의 기존 실제 아트 `HandoverShelf_Visual`을 외곽선/자기 자신 가림 제외 대상으로 연결하고, 칸 번호와 물건 이름은 판자 앞에 둔다. 다른 벽의 가림은 그대로 적용한다.
- 문자열은 정본 API의 TMP 실측을 거친다. 긴 실제 품목명은 인스턴스 판 너비만 늘리고 정본 글꼴 크기/9-slice를 유지한다. 폰트 fallback은 런타임 복제에만 연결한다.
- 대체 정리: 기존 ExpertRoomSign 오브젝트/참조 제거. 네 기물 뷰의 showLegacyLabels=false. 주문 창·전문가 서비스 창과 무관한 공간 표지는 유지한다.

## 사용자 확인
1. Dev 모드에서 대기실 시점으로 진입한다. Windows Ctrl+Shift+U / Mac ⌘+Shift+U 또는 상단 Dev 버튼.
2. 서쪽 보관실 벽 배송구 위 수화기: 외곽선과 F 안내, 상점 열기, ESC 닫기 및 이동 입력 복귀 확인.
3. 자금이 있는 런에서 주문: 방 안으로 떨어짐 → W5 실제 이름과 C6 주문 정보 → F 수령. 손/내려놓은 바닥 기물/선반에 옮겨도 이름이 맞는지 확인.
4. 선반 아래부터 1~4칸 표지와 물건 이름 확인. 멀어지거나 벽 뒤·반대 방향으로 보면 W5가 사라지는지 확인.
5. 낮/1·3일차 밤 의료/2일차 밤 시설 표지, 의료 구매자의 입장 대기→이용 중→복구 완료 확인.
6. Play를 끈 뒤 W4와 선반 칸 표지는 UI/WorldLabels에서 편집한다. 근접 조건 때문에 숨은 칸 표지는 해당 오브젝트 선택 후 `Tools/Curtain Call UI/Preview selected UI in Scene View`로 표시한다. Items는 실행 데이터가 있을 때 생성된다. 설치 메뉴는 중복 생성을 거부하므로 재설치하지 않는다.

## 수정 파일
- 씬: `Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity`.
- 신규 연결/설치: `Assets/Scripts/UI/SceneIntegration/WaitingRoomWorldLabels.cs`, `Editor/WaitingRoomWorldLabelInstaller.cs` 및 각 meta.
- 공통 연결: `Assets/Scripts/UI/SceneIntegration/CommonInteractionUI.cs`, `UiRuntimeFontFallback.cs`.
- 게임 표시 API: `Assets/Scripts/WaitingRoom/ExpertService/ExpertRoomView.cs`, `ShopStorage/ShopReach.cs`, `FloorPropsView.cs`, `HeldPropsView.cs`, `HandoverShelfView.cs`, `ShopDeliverySlot.cs`.
- 검사: `Assets/Scripts/UI/SceneIntegration/Tests/PlayMode/InteractionPresentationPlayTests.cs`.
- 문서: 이 가이드와 Task-20261019-061의 meta/setup/todo/plan/handoff.
- 기존 미커밋 폰트1·재질3·ui-workspace.md는 이번 작업과 별개로 보존한다. 정본 리소스 변경 없음. 커밋/푸시는 이번 요청에 포함하지 않는다.

## 검증
- Compile PASS. 최종 관련 PlayMode 4/4 PASS: 수화기 모델/F/ESC/입력복귀, 최근접2개/가림, 전문가 낮·밤/예약·이용·완료, 실제 주문→배송→수령→손→바닥→선반→회수/이름표 수명/거리/칸표지/C6 정보.
- UI 통합 전체 실행은 28/29였다. 실패한 새 검사 1개는 테스트가 방 밖 공중으로 이동해 기존 재입장 금지 로직에 의해 되돌아간 원인. 대기실 내부 동선으로 수정한 최종 관련4/4로 해소. 다른 회귀25개는 앞 실행에서 모두 통과했다. 전체29를 최종 재실행한 것은 아니다.
- 최초 관련 실행3/4에서 배송구 벽 뒤 배출을 발견·수정. 자동 상태 검사 외 실제 1920×1080 화면으로 수화기/배송 이름표, W4 장식 앞 위치, 선반 칸표지와 아이템 이름/외곽선 확인. Play 프레임 증가 확인(최종2547→3678), 마지막 Console 현재 오류0/경고0. 제공 키트의 기존 CS0618 컴파일 경고는 남아 있다.
- 관련 검사 자금은 테스트용으로 주입했고 구매·수령·보관은 실제 요청 경로를 사용했다. 전문가 예약/이용/완료는 서비스 상태 주입으로 UI 갱신을 검사했다. 의료 구매~신체 복구 전체 실플레이는 미실행.
- 증거: `.harness/local/w4w5-interaction4-final.json`, `w4w5-ui29-first.json`, `w4w5-delivery.png`, `w4w5-expert-fixed.png`, `w4w5-shelf-final.png` (로컬, Git 제외).
- check-work -SkipUnity: 구역 PASS(기존 명시 예외 적용), 문서 PASS. Unity 단계 생략은 위 별도 실행 결과로 보완한다. Fast OK/문서 길이 경고11·hard0.
- 미실행: 사람 전체 동선, 2~4인 및 재접속, Mac 실제 입력, 다른 화면비, 빌드, 프로젝트 전체 테스트. 기존 SpaceLink 확장25 중 실패6은 이 작업에서 재실행/해결하지 않았다.
