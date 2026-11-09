param([Parameter(Mandatory)][string]$ProjectPath, [switch]$SelfTest)
# CURTAINCALL 빌드 도우미: 버전·로그/설문·설문 등록·개발자/라이브를 고르고 Unity batchmode로 Windows 빌드를 만든다.
# 같은 프로젝트를 Unity 에디터가 열고 있으면(에디터 빌드 중 포함) 아무 파일도 바꾸지 않고, 에디터가 닫힐 때까지 기다린 뒤 시작한다.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3
. (Join-Path $PSScriptRoot 'core.ps1')
. (Join-Path $PSScriptRoot 'survey-dialog.ps1')

$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
if ($SelfTest) { Invoke-BuildHelperSelfTest -ProjectPath $ProjectPath; return }

Add-Type -AssemblyName System.Windows.Forms, System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

function Show-Message([string]$text, [string]$icon = 'Information', [string]$buttons = 'OK') {
    [System.Windows.Forms.MessageBox]::Show($text, 'CURTAINCALL 빌드 도우미', $buttons, $icon)
}

try {
    $script:State = [ordered]@{
        Settings = Read-PlaytestSettings -ProjectPath $ProjectPath
        CurrentVersion = Get-ProjectVersion -ProjectPath $ProjectPath
        ProductName = Get-ProductName -ProjectPath $ProjectPath
        UnityPath = Find-UnityEditor -ProjectPath $ProjectPath
        OutputEdited = $false
        Process = $null; Waiting = $false; Started = $null; LogPath = $null; ResultPath = $null; LogPosition = 0L
        Request = $null
    }
} catch { Show-Message ("시작하지 못했습니다.`n" + $_.Exception.Message) 'Error'; exit 1 }

$font = [System.Drawing.Font]::new('Malgun Gothic', 9)
$form = [System.Windows.Forms.Form]@{
    Text = 'CURTAINCALL 빌드 도우미'; Font = $font; StartPosition = 'CenterScreen'
    ClientSize = [System.Drawing.Size]::new(640, 600); FormBorderStyle = 'FixedDialog'; MaximizeBox = $false
    AutoScaleMode = 'Dpi'
}
function Add-Control($control, [int]$x, [int]$y, [int]$w, [int]$h, $parent = $form) {
    $control.Location = [System.Drawing.Point]::new($x, $y); $control.Size = [System.Drawing.Size]::new($w, $h)
    $parent.Controls.Add($control); $control
}
function New-Label([string]$text) { [System.Windows.Forms.Label]@{ Text = $text; TextAlign = 'MiddleLeft' } }

$null = Add-Control (New-Label "프로젝트: $ProjectPath") 16 10 608 22

# 버전
$null = Add-Control (New-Label '게임 버전') 16 42 90 24
$versionBox = Add-Control ([System.Windows.Forms.TextBox]@{ Text = $State.CurrentVersion }) 110 42 120 24
$null = Add-Control (New-Label "현재 프로젝트 버전: $($State.CurrentVersion)  (예: 0.2.2)") 240 42 384 24

# 빌드 종류
$kindGroup = Add-Control ([System.Windows.Forms.GroupBox]@{ Text = '빌드 종류' }) 16 76 608 78
$liveRadio = Add-Control ([System.Windows.Forms.RadioButton]@{ Text = '라이브 (LIVE) — 일반 플레이어 배포용. Development Build 끔, Dev Mode 끔'; Checked = $true }) 12 20 590 24 $kindGroup
$devRadio = Add-Control ([System.Windows.Forms.RadioButton]@{ Text = '개발자 (TEST) — 검증용. Development Build 켬, Dev Mode 항상 켜짐(-devmode 불필요)' }) 12 46 590 24 $kindGroup

# 로그·설문
$collectGroup = Add-Control ([System.Windows.Forms.GroupBox]@{ Text = '로그·설문' }) 16 162 608 112
$logsCheck = Add-Control ([System.Windows.Forms.CheckBox]@{ Text = '로그 수집 (TEST/LIVE는 위 빌드 종류로 구분)' }) 12 22 400 24 $collectGroup
$surveysCheck = Add-Control ([System.Windows.Forms.CheckBox]@{ Text = '설문 사용' }) 12 50 100 24 $collectGroup
$surveyButton = Add-Control ([System.Windows.Forms.Button]@{ Text = '설문 등록...'; Enabled = $false }) 120 48 110 28 $collectGroup
$surveyInfo = Add-Control (New-Label '') 12 80 590 24 $collectGroup

# 저장 위치·Unity
$null = Add-Control (New-Label '저장 위치') 16 286 90 24
$outputBox = Add-Control ([System.Windows.Forms.TextBox]@{}) 110 286 420 24
$browseButton = Add-Control ([System.Windows.Forms.Button]@{ Text = '찾아보기' }) 536 284 88 28
$null = Add-Control (New-Label 'Unity') 16 320 90 24
$unityLabel = Add-Control (New-Label '') 110 320 420 24
$unityButton = Add-Control ([System.Windows.Forms.Button]@{ Text = '변경' }) 536 318 88 28

# 실행
$buildButton = Add-Control ([System.Windows.Forms.Button]@{ Text = '빌드 시작'; Font = [System.Drawing.Font]::new('Malgun Gothic', 10, [System.Drawing.FontStyle]::Bold) }) 16 358 160 36
$cancelButton = Add-Control ([System.Windows.Forms.Button]@{ Text = '취소'; Enabled = $false }) 184 358 90 36
$openButton = Add-Control ([System.Windows.Forms.Button]@{ Text = '빌드 폴더 열기'; Enabled = $false }) 384 358 120 36
$logButton = Add-Control ([System.Windows.Forms.Button]@{ Text = '로그 열기'; Enabled = $false }) 512 358 112 36
$statusLabel = Add-Control (New-Label '준비됨') 16 400 608 22
$statusBox = Add-Control ([System.Windows.Forms.TextBox]@{ Multiline = $true; ReadOnly = $true; ScrollBars = 'Vertical'; WordWrap = $true }) 16 426 608 160

function Write-Status([string]$line) { $statusBox.AppendText("[$((Get-Date).ToString('HH:mm:ss'))] $line`r`n") }
function Get-Kind { if ($devRadio.Checked) { 'Dev' } else { 'Live' } }
function Update-UnityLabel { $unityLabel.Text = if ($State.UnityPath) { $State.UnityPath } else { '찾지 못함 — [변경]으로 Unity.exe를 선택하세요' } }

function Update-SurveyInfo {
    $version = $versionBox.Text.Trim()
    $names = @(Get-MatchingSurveys -Settings $State.Settings -Version $version | ForEach-Object { "$($_.Label)($($_.Survey.id))" })
    $surveyInfo.ForeColor = if (-not $surveysCheck.Checked) { [System.Drawing.Color]::Gray } elseif ($names.Count) { [System.Drawing.Color]::DarkGreen } else { [System.Drawing.Color]::Firebrick }
    $surveyInfo.Text = if ($names.Count) { "이 버전에 열릴 설문: $($names -join ', ')" } else { "이 버전($version)에 열릴 설문이 없습니다. 설문을 쓰려면 [설문 등록]에서 추가하세요." }
}
function Update-Output {
    if ($State.OutputEdited) { return }
    $outputBox.Text = Get-DefaultOutput -ProjectPath $ProjectPath -Version $versionBox.Text.Trim() -Kind (Get-Kind) `
        -Logs $logsCheck.Checked -Surveys $surveysCheck.Checked -ProductName $State.ProductName
}
function Set-Busy([bool]$busy) {
    foreach ($c in @($versionBox, $kindGroup, $collectGroup, $outputBox, $browseButton, $unityButton, $buildButton)) { $c.Enabled = -not $busy }
    $cancelButton.Enabled = $busy
    if (-not $busy) { $surveyButton.Enabled = $surveysCheck.Checked }
}

$versionBox.Add_TextChanged({ Update-SurveyInfo; Update-Output })
$liveRadio.Add_CheckedChanged({ Update-Output })
$logsCheck.Add_CheckedChanged({ Update-Output })
$surveysCheck.Add_CheckedChanged({ $surveyButton.Enabled = $surveysCheck.Checked; Update-SurveyInfo; Update-Output })
$outputBox.Add_KeyPress({ $State.OutputEdited = $true })
$browseButton.Add_Click({
    $dialog = [System.Windows.Forms.SaveFileDialog]@{ Filter = '게임 실행 파일 (*.exe)|*.exe'; FileName = [IO.Path]::GetFileName($outputBox.Text)
        InitialDirectory = [IO.Path]::GetDirectoryName($outputBox.Text); OverwritePrompt = $false; Title = '빌드 저장 위치(새 폴더 권장)' }
    if ($dialog.ShowDialog($form) -eq 'OK') { $outputBox.Text = $dialog.FileName; $State.OutputEdited = $true }
})
$unityButton.Add_Click({
    $dialog = [System.Windows.Forms.OpenFileDialog]@{ Filter = 'Unity.exe|Unity.exe'; Title = "Unity $(Get-UnityVersion -ProjectPath $ProjectPath)의 Unity.exe 선택" }
    if ($dialog.ShowDialog($form) -eq 'OK') { $State.UnityPath = $dialog.FileName; Save-HelperConfig @{ unityPath = $dialog.FileName }; Update-UnityLabel }
})
$surveyButton.Add_Click({
    $edited = Show-SurveyDialog -Owner $form -Settings $State.Settings -Version $versionBox.Text.Trim()
    if ($edited) { $State.Settings = $edited; Update-SurveyInfo; Write-Status '설문 등록 내용을 바꿨습니다. 빌드를 시작할 때 설정 파일에 저장됩니다.' }
})
$openButton.Add_Click({ if ($State.Request) { Start-Process explorer.exe -ArgumentList ('"' + [IO.Path]::GetDirectoryName($State.Request.Output) + '"') } })
$logButton.Add_Click({ if ($State.LogPath -and (Test-Path $State.LogPath)) { Start-Process notepad.exe -ArgumentList ('"' + $State.LogPath + '"') } })

$timer = [System.Windows.Forms.Timer]@{ Interval = 1000 }

function Start-UnityBuild {
    $request = $State.Request
    try {
        Write-PlaytestSettings -ProjectPath $ProjectPath -Settings $State.Settings
        $paths = New-BuildLogPaths -ProjectPath $ProjectPath -Request $request
        $State.LogPath = $paths.Log; $State.ResultPath = $paths.Result; $State.LogPosition = 0L
        $State.Process = Start-UnityBatchBuild -UnityPath $State.UnityPath -ProjectPath $ProjectPath -Request $request -LogPath $paths.Log -ResultPath $paths.Result
        $State.Started = Get-Date
        $logButton.Enabled = $true
        Write-Status "Unity 빌드 시작: $($request.Version) / $(if ($request.Kind -eq 'Dev') { '개발자(TEST)' } else { '라이브(LIVE)' }) / 로그 $(if ($request.Logs) { '켬' } else { '끔' }) / 설문 $(if ($request.Surveys) { '켬' } else { '끔' })"
        Write-Status "Unity 창 없이 진행됩니다. 처음 실행은 에셋 확인 때문에 오래 걸릴 수 있습니다."
    } catch {
        $State.Process = $null; Set-Busy $false; $timer.Stop()
        Write-Status ('시작 실패: ' + $_.Exception.Message); Show-Message ('빌드를 시작하지 못했습니다.' + "`n" + $_.Exception.Message) 'Error'
    }
}

function Complete-UnityBuild {
    $timer.Stop()
    $outcome = Read-BuildOutcome -Process $State.Process -LogPath $State.LogPath -ResultPath $State.ResultPath
    $State.Process = $null; Set-Busy $false
    foreach ($line in $outcome.Details) { Write-Status $line }
    if ($outcome.Success) {
        Write-BuildInfo -Request $State.Request
        $openButton.Enabled = $true
        $statusLabel.Text = '빌드 완료'
        Write-Status ($outcome.Message + ' → ' + $State.Request.Output)
        $note = if ($State.Request.Version -ne $State.CurrentVersion) { "`n프로젝트 버전이 $($State.CurrentVersion) → $($State.Request.Version)로 저장되었습니다(ProjectSettings 변경)." } else { '' }
        $State.CurrentVersion = $State.Request.Version
        Show-Message ("빌드가 끝났습니다.`n$($State.Request.Output)`n`n폴더 전체를 압축해서 전달하세요.$note")
    } else {
        $statusLabel.Text = '빌드 실패'
        Write-Status ('실패: ' + $outcome.Message)
        Show-Message ("빌드에 실패했습니다.`n$($outcome.Message)`n`n[로그 열기]로 자세한 내용을 볼 수 있습니다.") 'Error'
    }
}

$timer.Add_Tick({
    if ($State.Waiting) {
        if (Test-ProjectOpen -ProjectPath $ProjectPath) { $statusLabel.Text = "Unity 에디터가 닫히기를 기다리는 중… ($((Get-Date).ToString('HH:mm:ss')))"; return }
        $State.Waiting = $false
        Write-Status 'Unity 에디터가 닫혔습니다. 빌드를 시작합니다.'
        Start-UnityBuild
        return
    }
    if (-not $State.Process) { return }
    $elapsed = (Get-Date) - $State.Started
    foreach ($line in (Read-NewLogLines -State $State)) { Write-Status $line }
    if ($State.Process.HasExited) { Complete-UnityBuild; return }
    $statusLabel.Text = "빌드 중… 경과 $($elapsed.ToString('hh\:mm\:ss'))"
})

$buildButton.Add_Click({
    $request = [pscustomobject]@{
        Version = $versionBox.Text.Trim(); Kind = Get-Kind; Logs = $logsCheck.Checked; Surveys = $surveysCheck.Checked
        Output = $outputBox.Text.Trim(); ProductName = $State.ProductName
    }
    $problems = @(Test-BuildRequest -Request $request -Settings $State.Settings -UnityPath $State.UnityPath -ProjectPath $ProjectPath)
    if ($problems.Count) { Show-Message ("빌드 전에 확인해 주세요.`n`n- " + ($problems -join "`n- ")) 'Warning'; return }
    $folder = [IO.Path]::GetDirectoryName($request.Output)
    if ((Test-Path -LiteralPath $folder) -and (Get-ChildItem -LiteralPath $folder -Force | Select-Object -First 1)) {
        if ((Show-Message "저장 폴더에 이미 파일이 있습니다. 덮어쓸까요?`n$folder" 'Question' 'YesNo') -ne 'Yes') { return }
    }
    $State.Request = $request; $openButton.Enabled = $false
    Set-Busy $true; $statusBox.Clear(); $timer.Start()
    if (Test-ProjectOpen -ProjectPath $ProjectPath) {
        $State.Waiting = $true
        Write-Status 'Unity 에디터가 이 프로젝트를 열고 있습니다(에디터 빌드 진행 중일 수 있음). 지금은 어떤 파일도 바꾸지 않습니다.'
        Write-Status '에디터 작업이 끝나면 Unity를 직접 닫아 주세요. 닫히는 즉시 자동으로 빌드를 시작합니다. 그만두려면 [취소].'
        return
    }
    Start-UnityBuild
})

$cancelButton.Add_Click({
    if ($State.Waiting) { $State.Waiting = $false; $timer.Stop(); Set-Busy $false; $statusLabel.Text = '대기 취소됨'; Write-Status '대기를 취소했습니다. 아무 파일도 바꾸지 않았습니다.'; return }
    if ($State.Process -and -not $State.Process.HasExited) {
        if ((Show-Message 'Unity 빌드를 중단할까요? 만들던 게임 파일은 불완전할 수 있습니다.' 'Warning' 'YesNo') -ne 'Yes') { return }
        try { $State.Process.Kill($true) } catch { }
        Write-Status '빌드를 중단했습니다.'
    }
})

$form.Add_FormClosing({
    param($sender, $e)
    if ($State.Process -and -not $State.Process.HasExited) {
        if ((Show-Message 'Unity 빌드가 진행 중입니다. 창을 닫으면 빌드를 중단합니다. 닫을까요?' 'Warning' 'YesNo') -ne 'Yes') { $e.Cancel = $true; return }
        try { $State.Process.Kill($true) } catch { }
    }
    $timer.Stop()
})

$surveysCheck.Checked = Test-SurveysWanted -Settings $State.Settings -Version $State.CurrentVersion
$logsCheck.Checked = $true
Update-UnityLabel; Update-SurveyInfo; Update-Output
if (Test-ProjectOpen -ProjectPath $ProjectPath) {
    Write-Status '지금 Unity 에디터가 이 프로젝트를 열고 있습니다. 설정은 미리 골라 두고 [빌드 시작]을 누르면, 에디터가 닫힐 때까지 기다렸다가 시작합니다.'
}
[void]$form.ShowDialog()
