# W2 상점·서비스·장부 적용
2026-10-21 · 제작 정본 ShopPanel을 실제 대기실 데이터에 연결한다.

## 씬과 원본
- 원본: `Assets/Resources/Prefabs/UIs/ShopUi/ShopPanel.prefab`. 원본 prefab·PNG·폰트·GUID·9-slice 변경 없음. 외부 handoff package 미사용.
- 인스턴스: `Assets/Scenes/UI/CommonWaitingRoomUI.unity`의 `UI/ShopPanelCanvas`. 정본 연결을 유지하고 Canvas 정렬 순서100과 WaitingRoomShopUI만 씬에 추가했다.
- 공간: `Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity`의 기존 Phone이 열기/닫기와 입력 점유를 담당한다. 공통 대기실 UI 씬은 기존 로더가 자동 로드/해제한다.
- 편집: 공통 씬을 Additive로 열고 ShopPanelCanvas 내부 패널을 선택한다. 숨겨진 레이어는 `Tools/Curtain Call UI/Preview selected UI in Scene View`로 보며 조절한다. Apply로 원본을 덮어쓰지 않는다.
- 신규 `WaitingRoomShopInstaller`는 최초 적용용이다. 이미 배치된 씬에서 다시 실행하면 중복 생성을 거절한다.

## 실제 데이터
| 탭 | 연결 |
|---|---|
| 기물 | WaitingRoomShop.Catalog의 이름·가격, 공동 자금, 배송 대기 수, 구매 요청. 현재 상품 자체는 기존 게임의 임시 도구/배낭이며 데모 빵·물 등을 추가하지 않는다. 전용 아이콘이 없는 품목은 정본 배송 아이콘을 쓴다. |
| 서비스 | ExpertService의 일차/밤·담당자 유무·신체 손실·부위당/합계 비용·예약자/방 단계. 의료 예약 후 전문가실 입장 흐름 유지. 시설 강화/약화는 기존 구매 API로 다음 날 조정한다. |
| 장부 | PerformanceRewards.State의 최근20개 자금 내역(증감·전후 잔액·일차), 배송 대기와 최근 수령 기록. 배송과 수령은 자금 내역 뒤에 별도로 표시한다. |

- 의료1·3일차 밤, 시설2일차 밤 등 이용 조건과 가격은 기존 게임 판정이다. 자금 부족·담당자 부재·몸 상태·예약/구매 완료 사유를 표시한다.
- 판매 API는 현재 없으므로 판매 행동을 생성하지 않는다. UI 데모의 품목/가격/거래 성공을 게임 데이터로 쓰지 않는다.
- 요청마다 ID를 기존 네트워크 메시지에 실어 응답과 대응한다. 호스트가 실제 거래를 판정하며 기존 가격·소유·정산 규칙은 유지한다. 기존 요청 API와 이벤트도 유지한다.
- 처리 중 행동 버튼은 잠기고 탭/목록/닫기는 사용할 수 있다. 거절 시 사유 표시, 닫기/재열기 뒤 이전 응답은 무시한다. 창 닫기는 전송된 거래를 취소하지 않는다.
- 15초 무응답은 실패 확정/환불이 아닌 확인 필요 안내다. 재주문 전 장부와 배송구를 확인한다. 서버 거래 재전송·자동 재시도는 하지 않는다.
- UI 열린 동안 커서 표시/플레이어 입력 차단, 닫기·ESC·거리 이탈·씬 전환 때 해제한다. EventSystem이 없을 때만 UI 씬 아래에 런타임 생성한다.
- 정본 Static 폰트는 그대로 두고 기존 UiRuntimeFontFallback이 런타임 복제본에 게임 문구를 보완한다.

## 대체 UI 정리·수정 파일
- WaitingRoomBase의 `ExpertServicePhonePanel` 컴포넌트 제거. Phone의 `showLegacyUi=false`로 구형 상점/장부 IMGUI를 끈다. 테스트 씬용 코드와 실제 전화기·배송구·전문가실은 보존한다.
- 신규: `Assets/Scripts/UI/SceneIntegration/WaitingRoomShopUI.cs`, `Editor/WaitingRoomShopInstaller.cs`, `Tests/PlayMode/WaitingRoomShopPlayTests.cs`와 각각의 meta.
- 변경: 위 두 씬, `ShopStorage/ShopPhone.cs`, `ShopStorage/WaitingRoomShop.cs`, `ExpertService/ExpertService.cs`, `UiRuntimeFontFallback.cs`, 기존 DevStartLauncherPlayTests의 partial 선언.
- Task setup/meta/plan/todo/handoff 및 next-plan에 적용 상태 기록. 기존 로컬 폰트1/재질3·ui-workspace.md는 별도 보존 대상이다.

## Unity 사용자 확인
1. WaitingRoomBase에서 Play. Dev 배지 또는 Windows Ctrl+Shift+U / Mac ⌘+Shift+U로 원하는 일차/시점 진입. 새 Dev 런은 공동 자금0이므로 구매 버튼 비활성은 정상이다.
2. 서쪽 배송구 위 Phone에 접근해 F. 새 기물/서비스/장부 탭 하나만 뜨고 커서가 표시되는지 확인한다. 닫기/ESC 뒤 시선 조작 복귀를 확인한다.
3. 공연에서 얻은 공동 자금이 있는 런에서 기물 구매→잔액 감소→배송구 수령→장부·W5 표지·소지 HUD 변경을 확인한다. 창을 다시 열어도 실제 거래 기록이 유지되어야 한다.
4. 1·3일차 밤, 신체 손실/공동 자금이 있는 상태에서 의료 예약→전문가실 이동→복구를 확인한다. 2일차 밤은 시설 강화 또는 약화 후 중복 구매가 막혀야 한다. 다른 시간에는 불가 사유가 보여야 한다.
5. 상점을 켠 채 공연 전환/대기실 재진입, 둘 이상 동시 구매 때 자금·예약자·장부의 일치와 UI 중복 유무를 확인한다.

## 검증
- Unity 컴파일 PASS. 신규 W2 PlayMode3/3 PASS: 실제 LAN 호스트·프리팹 버튼 이벤트·구매/수령/장부·거절·연속 클릭 방지·의료 비용/예약·시설 약화/중복 차단·닫기/거리 이탈·EventSystem1개.
- 테스트 자금과 신체 손실은 fixture로 주입하며 거래/응답은 실제 API를 사용한다. 첫3개 실패는 테스트 잔액만 바꾸고 장부를 맞추지 않은 fixture 오류로, 장부 동시 준비 후3/3 통과했다.
- 결과: `.harness/local/w2-integration3.json`, 최초 실패 `w2-first-fixture-fail.json`.
- 기존 UI 포함 PlayMode33/33 PASS(`.harness/local/w2-ui33.json`), 구역/문서 검사 PASS. check-work는 SkipUnity로 실행했으며 컴파일/테스트는 별도 verify-unity 결과다.
- 실제1920×1080 기물/서비스/장부 캡처 확인(`.harness/local/w2-items.png`, `w2-services.png`, `w2-ledger.png`). LAN 호스트에서 시설 강화 후 자금500→480·장부 일치, EventSystem1개, Play 프레임6027→18741. 현재 Console 오류0/경고0(누적 Pipeline 기록과 구분).
- 화면에서 시설 설명 말줄임 발견 후 두 줄로 축약했다. 원본 레이아웃은 보존. 기존 상단 Dev·밤 안내 및 음성 테스트 UI 겹침은 후속 공통 HUD 정리 항목이며 W2로 대체된 표시가 아니다.
- 문구 수정 후 최종 컴파일 PASS·W2 재검사3/3 PASS(`w2-final3.json`). 최초 재검사 요청은 Pipeline HTTP400으로 시작 실패했고 준비 상태에서 재요청하여 완료했다. verify-fast의 첫 샌드박스 submodule 읽기 실패도 권한 있는 실행에서 PASS로 재확인했다.
- 종료: Play 중지, 원래 Dev 설정 복원. WaitingRoomBase+CommonWaitingRoomUI 모두 dirty0. 재로드로 정본 ShopPanel 연결·view 참조·MissingScript0·구형 서비스 컴포넌트0 확인. 씬/프리팹 원본 재생성 없음.
- 미실행: 실제2~4인 동시 요청/재접속·네트워크 지연/끊김, 물리 마우스·Mac, 의료 예약부터 복구까지 사람 전체 동선, 다른 화면비·빌드·전체 프로젝트 테스트. 이전 카메라2개/확장 SpaceLink6개 실패는 별도 추적한다.
