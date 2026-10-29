# Start 타이틀
- 이전 공통 메뉴·음성 HUD: 1671f2f commit/push 완료. 이번 Start 변경은 미커밋.
- Start.unity의 UI/Start Canvas 아래 Assets/Resources/Prefabs/UIs/TitleLogo/TitleLogo.prefab 인스턴스 적용. 원본/스프라이트/9-slice 유지.
- 1920×1080 기준 중앙 로고와 방 만들기·방 코드로 참가·LAN 접속·설정·종료 세로 메뉴. 코드/LAN 페이지도 중앙 배치. 기존 StartMenu 버튼 참조/콜백 유지.
- 구형 텍스트 타이틀·부제·푸터·왼쪽 음영/구분선 제거. Canvas와 EventSystem을 UI 하위로 정리.
- 시안의 별도 배경 그림은 적용하지 않음. 현재 Start 무대/커튼을 활용하고 전체 음영으로 글자 대비 확보.
- 검증: 실제 Play 1920×1080 화면 캡처 확인. 메뉴 콜백 자동 조회는 Pipeline 메인 스레드 5초 시간 초과로 확인 불가. 실제 연결·종료·Mac·다른 화면비 검증 미실행.
- 사용자 확인: Start Play에서 중앙 메뉴 호버/클릭, 코드 참가→뒤로, LAN→뒤로, 설정 열기/닫기, 방 만들기→Lobby, 종료. Dev 배지는 개발 모드 표시.
- 위치 수정: UI/Start Canvas/TitleLogo 및 Main의 RectTransform. 기존 3D 무대는 Start Stage.
- 최종 verify-unity: Editor ready, dirty 0, Compile PASS(up_to_date), Tests SKIPPED. 캡처 .harness/local/captures/start-title.png.

## 참가 팝업·화면비 후속
- Start의 Join/LAN에 BasicPanels 프리팹 인스턴스에서 메뉴 컨트롤을 제거한 PopupSkin 배치. CommonButtons Confirm/Cancel/General로 기존 붉은 버튼 교체, StartMenu 참조 재연결. 입력 필드의 값·이벤트 유지, 새 색상/간격/폰트 적용.
- StartPopupLayoutInstaller.cs(및 meta): 위 편집 작업과 실제 씬 Canvas 통일 도구. 이미 적용된 씬에는 중복 실행하지 않는다.
- 수정 씬: Start, Lobby, Tightrope, HumanCannon, CommonInteractionUI, CommonMenuUI, CommonPerformanceUI, CommonWaitingRoomUI, PerformanceTransitionUI.
- 화면용 Canvas23개를 Scale With Screen Size /1920×1080 /Expand로 검증. 기준 영역 전체를 유지하며 남는 방향으로 확장한다. WaitingRoomBase 자체에는 화면 Canvas가 없고 CommonWaitingRoomUI가 HUD를 제공하므로 공간 씬 파일 변경 없음. 월드 UI는 그대로 유지.
- Play 프레임1→5060→9998 진행 확인. LAN 표시, 뒤로→메인, 코드 참가, 빈 코드 안내 콜백 PASS. LAN/코드 팝업 캡처 시각 확인. 캡처 출력 크기 변경은 실제 Game View 화면비 변경 검증과 구분한다.
- 미실행: 실제 포인터 클릭/네트워크 접속, 각 공간에서4:3/16:10/21:9/세로 화면 실제 플레이·모든 HUD 겹침 확인, Mac/빌드. Expand 설정만으로 모든 콘텐츠의 겹침까지 보장하지 않는다.
- 사용자 확인: Start 두 팝업 입력/참가/뒤로, Game View의4:3·16:10·16:9·21:9에서 메뉴/결과/상점·스케줄·공연 HUD가 잘리지 않는지 확인.
