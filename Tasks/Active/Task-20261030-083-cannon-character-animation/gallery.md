# 애니메이션 확인 씬

- 씬: Assets/Scenes/Tests/MainStage/HumanCannon/CircusMotionGallery.unity
- 메뉴: CurtainCall > Validation > Open circus motion gallery (44 clips)
- 씬을 열고 Play. 기본 정면 카메라, 4캐릭터 × 절단4단계 16개 배우를 비교한다.
- 왼쪽 01~11 버튼: 동작 선택. 각 열에는 해당 절단 단계 클립이 재생되어 전체44개를 확인할 수 있다.
- Pause/Play, Restart, 시간 슬라이더(탐색 시 정지), Repeat, Auto cycle, Speed로 재생을 조절한다.
- Next character / Next injury stage로 대상을 선택하고 Focus selected actor로 확대한다. Show all 16 actors는 비교 보기로 복귀한다.
- Camera의 슬라이더는 위부터 회전(-180~180도), 고도, 줌이다. 정면·후면·측면 모두 볼 수 있다.
- 이 씬은 실제 캐릭터·절단 메시·실제 컨트롤러 클립의 자세 확인용이다. 대포/트램펄린 접점 및 비행/충돌/외줄 동료 구조의 게임 동작은 실제 공연 씬에서 확인한다.

## 검증
Play에서11동작 × 16배우 = 176상태의 컨트롤러 이름·절단 표면·유효한 머리 좌표를 확인했다. 확대 전환, 특정 절단 클립 탐색과 재생 시간 진행을 확인했다. UI 포함 실제 화면: Previews/gallery-front.png. Source/verify-gallery.cs, gallery-report.json 참고.
