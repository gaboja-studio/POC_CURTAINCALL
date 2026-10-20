# UI 제작 브랜치 수신 준비

## 승인·범위 (2026-10-20)
- PM 직접 요청으로 C11 및 작업과 무관한 현재 에셋 변경까지 모두 commit/push한다. 공개 공유 Bold·Interaction 1.meta, private 미추적 키트4종·Leartes 변경과 기존 공급 방향 정책3파일을 포함한다.
- 공급은 `origin/feat/ui-resources` → `feat/ui-scene-integration`. 사용자가 수신 worktree에서 직접 pull하며 이 작업에서는 수신 브랜치·upstream·작업 파일을 변경하지 않는다. PR/최종 통합 병합은 하지 않는다.
- private 추가 저장 대상807파일: Leartes220, Buttons unity-only76, Curtain130, Feedback122, Settings259. 기존 C11/Schedule334파일 저장 이후의 추가분이다. `.meta`·라이선스·제작 자료를 보존한다.
- 주의: 기존 로컬 `LeartesStudios/AbandonedFactory/HDRP/Scene/AbandonedFactory.unity`는 3,539바이트 YAML/SceneRoots 비어 있음. 24,771,936바이트 HEAD 바이너리와 다르다. PM 전체 변경 저장 요청에 따라 현재 내용 그대로 저장하며 복원·씬 YAML 편집은 하지 않는다. 기능/외형 정상 판정이 아니다.
- 제공 원본 공백 경고7건은 보존한다(LICENSE4, Feedback 검토 문서1, 제작 스크립트 EOF2). 미추적 스크립트는 실행하지 않는다.
- 공유 Bold/Buttons 글리프·Tooltip 과거 실패와 추가 에셋 기능/외형 검증은 이번 업로드로 해결된 것이 아니다.

## 사용자 실행 순서
수신 worktree에서 현재 브랜치가 `feat/ui-scene-integration`인지 확인한다. Unity 씬 저장·Play/Prefab Stage 종료 후 공개 및 private 작업 트리가 모두 clean일 때만 실행한다. 변경이 있으면 먼저 해당 작업의 승인된 저장 절차를 끝내고, 자동 stash/reset/discard는 하지 않는다.

```powershell
git branch --show-current
git status --short
git -C Assets/_ThirdParty status --short
git fetch origin
git pull --no-rebase --no-recurse-submodules origin feat/ui-resources
```

### 확인된 문서 충돌 처리
수신 기준 `c4a5ade`에서는 같은 Task061을 씬 통합 용도로 전환해 handoff/meta/plan/setup/todo 5문서가 충돌한다. 제작 문서로 덮으면 수신 쪽 승인·검증 FAIL/미검증 기록이 사라지므로 **수신의 현재 5문서를 유지**한다. 공급 최신 5문서는 이번 커밋의 `History/Resource-supply-c11/`에 별도로 보존했다. 코드/씬/prefab 충돌은 사전검사에서 없었다.

pull이 충돌로 멈춘 경우 아래는 알려진 5문서에만 적용한다. 다른 충돌이 있으면 이 절차를 실행하지 말고 중단한다. HEAD는 pull 전 수신 커밋이다.

```powershell
$task = 'Tasks/Active/Task-20261019-061-cheer-gauge-ui'
$allowed = @('handoff.md','meta.md','plan.md','setup.md','todo.md') | ForEach-Object { "$task/$_" }
$conflicts = @(git diff --name-only --diff-filter=U)
if ($conflicts.Count -eq 0 -or @($conflicts | Where-Object { $_ -notin $allowed }).Count -gt 0) { throw '예상한 제작 Task 문서 충돌만 있는 상태가 아닙니다. 중단합니다.' }
git restore --source=HEAD --staged --worktree -- $conflicts
if ($LASTEXITCODE -ne 0) { throw '수신 문서 유지 실패' }
git diff --name-only --diff-filter=U
# 위 출력이 비었는지 확인한 뒤 일반 수신 merge commit 생성
git commit -m 'chore: UI 제작 리소스 수신 및 양쪽 Task 기록 보존'
```

pull 또는 위 수신 merge commit이 성공한 후에만 아래를 실행한다. 씬/prefab YAML은 자동 해결하지 않는다.

```powershell
git submodule update --init --recursive
git lfs pull
git -C Assets/_ThirdParty lfs pull
git submodule status --recursive
git status --short
pwsh -File Scripts/doctor.ps1
```

- submodule은 공개 커밋에 기록된 SHA를 받는다. `--remote`로 다른 private main을 따라가지 않는다.
- 양쪽 이력이 갈라졌으므로 일반 merge commit이 생길 수 있다. 제작 브랜치로 upstream/push 대상을 바꾸지 않는다.
- 수신 후 Unity import/컴파일·실제 연결 씬 UI 확인은 수신 Task에서 한다. 공급 브랜치의 C11 독립 검증(Play76/76·Edit798/798·합성 입력72/72·24캡처)은 연결 씬 검증을 대체하지 않는다.

## 저장·준비 결과
- private807파일 `d409dc80` 커밋 후 최초 push는 동시 원격 재질 저장 때문에 non-fast-forward로 거절됐다. 사전 병합검사에서 재질 충돌을 확인했고 실제 충돌 병합 전에 PM에게 선택을 요청했다. PM은 **현재 worktree 재질**을 선택했다. 원격 이력을 일반 병합하되 Leartes 재질219개 전체 내용을 로컬 커밋과 동일하게 보존한다. 강제 push·rebase·씬/prefab YAML 충돌 해결은 하지 않는다.
- private 최종 `62250960c0d82c90aa2ac18e3501ed2a59ab7d16` main push·직후 원격 SHA 일치 확인, d409dc80 대비 Leartes 전체 바이트 차이0·작업 트리 clean. LFS 포인터 검사 PASS/추가 전송 없음. 후속 자동 README 커밋은 공개 gitlink를 바꾸지 않는다.
- 공개 최초 a588330 commit은 폰트133.64MB로 GitHub GH001 push 거절. 해당 폰트 한 파일만 LFS 지정해 실제 바이트를 보존하고 미공개 커밋을 amend한다. 원격 이력 재작성/강제 push 없음.
- 전체 저장 전 구역·문서·컴파일 PASS, 테스트는 DryRun에서 SKIPPED(기존 C11 검증과 구분). 최종 보정 후 검사를 다시 통과해야 저장한다. 최종 SHA·수신 사전검사 결과는 원격 이력과 최종 보고로 확인한다.
