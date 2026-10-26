---
summary: 외부(서드파티) 에셋은 무료·유료 구분 없이 private 레포 CURTAINCALL_PaidAssets를 Assets/_ThirdParty submodule로 받아 쓴다. 공개 레포에는 직접 만든 것만
status: active
updated: 2026-10-20
source: human (2026-10-09 PM @MoHoDu, 2026-10-16 권한 위임, 2026-10-19 C1·2026-10-20 버튼 정리 승인)
---

> 2026-10-27: [하네스 간소화](/Harness/Project/Decisions/harness-simplification.md)가 우선한다. 아래 PM 전용·구역·인계·순차 착수·부모 자식 제한은 과거 기준이다. 게임 의도·외부 에셋 비공개 보관·참조 보존은 유지한다.


# 외부 에셋 보관
## 언제 읽나
Asset Store 등 외부에서 받은 에셋을 넣거나 옮길 때, 팀원이 `Assets/_ThirdParty`를 받지 못할 때, CI에서 Unity 컴파일을 돌릴 때.

## 결정
- 2026-10-09 / PM(@MoHoDu):
  - 공개 레포(POC_CURTAINCALL)는 GitHub Actions·ruleset 때문에 public을 유지한다. 그래서 **외부에서 받은 에셋은 무료·유료 구분 없이** private 레포 `gaboja-studio/CURTAINCALL_PaidAssets`에 두고 `Assets/_ThirdParty` submodule(HTTPS)로 받는다. Asset Store 무료 에셋도 EULA상 공개 재배포가 안 된다.
  - 공개 레포에는 직접 만든 코드·아트·씬·사운드, Unity 기본 리소스(`TextMesh Pro/`), 공개 라이선스 폰트(Pretendard, OFL), UPM 패키지(`Packages/manifest.json`)만 둔다.
  - 외부 에셋 설정 파일(예: `Assets/Resources/DOTweenSettings.asset`)은 공개 레포에 남긴다.
  - 첫 이관: DOTween·DOTween Pro·DemiLib(`Assets/Plugins/Demigiant` → `Assets/_ThirdParty/Demigiant`). Pro 예제는 삭제.
  - 팀원 전원에게 private 레포 읽기 권한을 준다. 게임 코드가 DOTween을 쓰므로 없으면 컴파일이 깨진다.
- 2026-10-16 / PM 위임:
  - 에셋 관리자 `@gyumin9048-ctrl`는 외부 에셋 추가·수정과 해당 변경의 저장·push·제출을 할 수 있다. 역할 명부는 `Harness/Project/Facts/team.md`의 에셋 관리자 행을 사용한다.
  - private 저장소의 머티리얼·프리팹·제공 씬·에셋 코드 및 `.meta`가 대상이다. 공개 저장소의 게임 코드·씬·Packages·ProjectSettings 권한은 확대하지 않는다. (공개 저장소의 기존 에셋 수정·삭제는 2026-10-16 `team-roles.md` 공통 허용 규칙을 따른다)
  - private 저장소에 먼저 커밋·main 반영·push하고 공개 저장소의 gitlink 포인터만 저장한다. 별도 에셋 작업은 `resource/third-party/*`, Task 관련 포인터는 해당 작업 브랜치에서 기존 제출 대상으로 보낸다. 병합 권한은 위임하지 않는다.
  - Task-20261008-024도 이 위임을 적용한다. 기존 setup의 `_ThirdParty` 수정 금지보다 이 역할 예외가 우선한다. 다른 구역 밖 변경은 별도 PM 결정이 필요하다.
- 2026-10-19 / PM UI 키트 정리:
  - 프리팹·기능 확인 후 키트에는 필수 의존성·라이선스·GUID/.meta를 남기고, 불필요한 제공 코드·Preview·문서·제작 자료는 승인된 작업 구역에서 정리한다. 동적 로딩까지 참조를 확인하며 불명확한 파일은 보류한다.
  - 필요한 유지보수 정보는 Harness에 기록한다. 문서 요약이 원본 그림·재수출 도구를 대체하지는 않는다. 외부 자산의 공개 이전·무관한 변경 폐기·자동 Git 저장 권한은 부여하지 않는다.
  - Task-061은 전용 worktree C1 복사본을 정리하며 메인 원본과 사용자 수정 프리팹을 보존한다. 2026-10-20 PM 동작 확인 후 버튼 v1의 미사용 제작 파일 정리·정보 기록·저장도 승인했다. 다른 키트는 제외하며 필수 자산·라이선스·제공 런타임·조립기를 보존한다. 상세는 [C1 사실](../Facts/c1-cheer-gauge.md)·[버튼 사실](../Facts/common-buttons.md)을 따른다.
## 방법
- 받기: `start-work`·`worktree.ps1 -Add`가 `git submodule update --init`을 자동 실행. 직접 clone은 `git clone --recurse-submodules`. 이관 전 경로(`Assets/Plugins/Demigiant`)에 남은 무시 파일은 같은 단계에서 자동 정리(`doctor`가 경고). 디버그 심볼(`.mdb`·`.pdb`)은 private 레포에도 저장하지 않는다.
- 새 외부 에셋: import 후 `.meta`와 함께 `Assets/_ThirdParty/<퍼블리셔>/<에셋>/`로 옮기고 → private 레포에서 커밋·push → 공개 레포에서 포인터 변경을 커밋. private 레포 README 에셋 표는 main push 때 Actions가 자동 갱신(비고는 에셋 폴더 `.note` 첫 줄).
- 막기(`Scripts/verify-third-party.ps1`): `Assets/` 최상위 허용 목록 밖 폴더, 알려진 외부 에셋 경로, 라이브러리 파일(`.dll`·`.unitypackage` 등)이 공개 레포에 있으면 실패. 직접 만든 콘텐츠용 최상위 폴더가 새로 필요하면 PM이 허용 목록을 고친다.
  - 커밋 전 `verify-fast`, push 전 `.githooks/pre-push`(submodule 포인터가 private 레포 main에 있는지도 확인), PR의 `verify-third-party` 검사. 브랜치 룰셋에서 이 검사를 필수로 지정한다.
  - PR 검사의 포인터 확인에는 Secret `THIRD_PARTY_TOKEN`(private 레포 Contents: Read-only)이 필요하다. 없으면 경로 검사만 한다.
- CI에서 Unity 컴파일이 필요하면 `actions/checkout`에 `submodules: recursive`, `lfs: true`, 같은 토큰을 준다.

## 이유
유료 에셋을 공개 레포에 두면 라이선스 위반·유출 위험이 있고, 가격으로 나누면 팀원이 매번 판단해야 한다. 출처 하나로 나누면 규칙이 단순하다.

## 바꾸려면
`.gitmodules`, `.gitignore`, `Scripts/verify-third-party.ps1`, `Harness/Engine/Unity/Policies/code-folders.md`, `Docs/Guides/assets-folder-rules.md`를 함께 고친다.
