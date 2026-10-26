[CmdletBinding()]
param([Parameter(Mandatory)][string]$RequestPath,[Parameter(Mandatory)][string]$ResultPath,[Parameter(Mandatory)][string]$ProgressPath)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'core.ps1')
$script:AhProgressPath=$ProgressPath
$guard=$null
try {
    $request=Get-Content -LiteralPath $RequestPath -Raw | ConvertFrom-Json
    $project=[IO.Path]::GetFullPath($request.project)
    $common=(Invoke-AhGit $project @('rev-parse','--path-format=absolute','--git-common-dir')).Trim()
    $guard=[IO.File]::Open((Join-Path $common 'asset-helper.guard'),'OpenOrCreate','ReadWrite','None')
    $result=switch ($request.action) {
        'status' {
            $state=Get-AhState $project
            $otherCleanup=Update-AhOtherShareCleanup $project $state.branch
            $state=Get-AhState $project
            $share=Get-AhShareReport $project $state.branch $state
            if ($otherCleanup -and -not $share.cleanup) { $share.cleanup=$otherCleanup }
            # Cleanup may detach a delivered helper branch without changing any asset bytes.
            $state=Get-AhState $project
            $state | Add-Member -NotePropertyName share -NotePropertyValue $share
            $state
        }
        'publish' { Invoke-AhPublish $project $request.snapshot $request.message }
        'sync' { Invoke-AhSync $project $request.snapshot ([bool]$request.keep) }
        'restore' { Invoke-AhRestore $project $request.snapshot $request.backup }
        'login' {
            $gh=Get-GhCli
            if (-not $gh) { throw 'GitHub CLI가 없습니다. GitHub CLI 설치 페이지에서 설치한 뒤 도우미를 다시 열어 주세요.' }
            Write-AhProgress 'GitHub 로그인 화면을 준비하고 있습니다.'
            $psi=[Diagnostics.ProcessStartInfo]::new($gh)
            $psi.UseShellExecute=$false; $psi.CreateNoWindow=$true
            $psi.RedirectStandardInput=$true; $psi.RedirectStandardOutput=$true; $psi.RedirectStandardError=$true
            foreach ($arg in @('auth','login','--web','--git-protocol','https','--hostname','github.com')) { $psi.ArgumentList.Add($arg) }
            $login=[Diagnostics.Process]::Start($psi)
            try {
                $login.StandardInput.WriteLine(''); $login.StandardInput.Close()
                $reads=@($login.StandardOutput.ReadLineAsync(),$login.StandardError.ReadLineAsync())
                $streams=@($login.StandardOutput,$login.StandardError)
                while (-not $login.HasExited -or @($reads | Where-Object { $null -ne $_ }).Count) {
                    for ($i=0;$i -lt 2;$i++) {
                        if ($null -eq $reads[$i] -or -not $reads[$i].IsCompleted) { continue }
                        $line=$reads[$i].GetAwaiter().GetResult()
                        if ($null -eq $line) { $reads[$i]=$null; continue }
                        if ($line -match '\b([A-Z0-9]{4}-[A-Z0-9]{4})\b') { Write-AhProgress "GitHub 로그인 코드: $($Matches[1]). 열린 GitHub 화면에 입력해 주세요." }
                        $reads[$i]=$streams[$i].ReadLineAsync()
                    }
                    Start-Sleep -Milliseconds 100
                }
                if ($login.ExitCode) { throw '로그인을 마치지 못했습니다. 다시 연결해 주세요.' }
            } finally { $login.Dispose(); Write-AhProgress 'GitHub 연결 확인을 마쳤습니다.' }
            @{ kind='login'; message='GitHub에 연결했습니다. 원래 버튼을 다시 눌러 주세요.' }
        }
        default { throw '알 수 없는 작업입니다.' }
    }
    $response=@{ ok=$true; result=$result }
} catch {
    $message=if (-not $guard) { '다른 도우미 작업이 실행 중이거나 저장소에 접근할 수 없습니다. 진행 중 작업을 마친 뒤 다시 눌러 주세요.' } else { $_.Exception.Message }
    $response=@{ ok=$false; error=$message }
} finally {
    if ($guard) { $guard.Dispose() }
}
$json=$response | ConvertTo-Json -Depth 16
$temp=$ResultPath+'.tmp'
[IO.File]::WriteAllText($temp,$json,[Text.UTF8Encoding]::new($false))
Move-Item -LiteralPath $temp -Destination $ResultPath -Force
