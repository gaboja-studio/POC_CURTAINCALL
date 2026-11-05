# 빌드 도우미 공용 함수. UI 없이 동작하는 부분만 둔다(launch.ps1 -SelfTest로 확인).
$script:RunnerMethod = 'CurtainCall.Telemetry.Pipeline.Editor.PlaytestBuildRunner.Build'
$script:HelperConfigPath = Join-Path $env:LOCALAPPDATA 'CurtainCall\build-helper.json'
$script:Utf8 = [System.Text.UTF8Encoding]::new($false)

function Get-SettingsPath([string]$ProjectPath) { Join-Path $ProjectPath 'Scripts\Telemetry\playtest-settings.json' }

function Get-UnityVersion([string]$ProjectPath) {
    $line = Get-Content -LiteralPath (Join-Path $ProjectPath 'ProjectSettings\ProjectVersion.txt') | Where-Object { $_ -match '^m_EditorVersion:\s*(\S+)' } | Select-Object -First 1
    if (-not $line) { throw 'ProjectSettings/ProjectVersion.txt에서 Unity 버전을 읽지 못했습니다.' }
    [void]($line -match '^m_EditorVersion:\s*(\S+)'); $Matches[1]
}
function Get-PlayerSetting([string]$ProjectPath, [string]$Name) {
    $text = [IO.File]::ReadAllText((Join-Path $ProjectPath 'ProjectSettings\ProjectSettings.asset'))
    $match = [regex]::Match($text, "(?m)^\s{2}$([regex]::Escape($Name)):\s*(.*?)\s*$")
    if (-not $match.Success) { throw "ProjectSettings.asset에서 $Name 값을 찾지 못했습니다." }
    $match.Groups[1].Value
}
function Get-ProjectVersion([string]$ProjectPath) { Get-PlayerSetting $ProjectPath 'bundleVersion' }
function Get-ProductName([string]$ProjectPath) {
    $name = (Get-PlayerSetting $ProjectPath 'productName') -replace '[\\/:*?"<>|]', '_'
    if ($name) { $name } else { 'Game' }
}

# ---- 로그·설문 설정 (Unity PlaytestCollectionSettings와 같은 규칙) ----
function Test-VersionPattern([string]$Pattern) { $null -ne $Pattern -and $Pattern -match '^(\*|[0-9]+\.[0-9]+\.(?:[0-9]+|x))$' }
function Test-VersionMatch([string]$Version, [string]$Pattern) {
    if (-not (Test-VersionPattern $Pattern) -or $Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+(?:[-+][0-9A-Za-z.-]+)?$') { return $false }
    if ($Pattern -eq '*') { return $true }
    if (-not $Pattern.EndsWith('.x')) { return $Version -ceq $Pattern }
    $Version.StartsWith($Pattern.Substring(0, $Pattern.Length - 1), [StringComparison]::Ordinal)
}
function Get-SurveyProblems($Survey) {
    $problems = [System.Collections.Generic.List[string]]::new()
    if ([string]$Survey.id -notmatch '^[A-Za-z0-9_-]{1,40}$') { $problems.Add('설문 ID는 영문·숫자·_·- 1~40자') }
    if ([long]$Survey.round -lt 1 -or [long]$Survey.round -gt 999999999) { $problems.Add('설문 회차는 1 이상') }
    if ([long]$Survey.nightDay -lt 1) { $problems.Add('일차는 1 이상') }
    if (-not (Test-VersionPattern ([string]$Survey.versions))) { $problems.Add('대상 버전은 0.2.2 / 0.2.x / * 형식') }
    if ([string]$Survey.entry -notmatch '^entry\.[0-9]+$') { $problems.Add('자동 입력 질문 ID는 entry.숫자 형식') }
    if ([string]$Survey.formUrl -cnotmatch '^https://docs\.google\.com/forms/d/e/[A-Za-z0-9_-]+/viewform$') { $problems.Add('설문 주소는 https://docs.google.com/forms/d/e/…/viewform 형식(뒤의 ?… 없이)') }
    , $problems
}
function Get-SurveySlots($Settings) {
    @([pscustomobject]@{ Key = 'night'; Label = "$([int]$Settings.night.nightDay)일차 밤 정비"; Survey = $Settings.night },
      [pscustomobject]@{ Key = 'exit'; Label = '게임 종료'; Survey = $Settings.exit })
}
function Get-MatchingSurveys($Settings, [string]$Version) {
    Get-SurveySlots $Settings | Where-Object { $_.Survey.enabled -and (Get-SurveyProblems $_.Survey).Count -eq 0 -and (Test-VersionMatch $Version $_.Survey.versions) }
}
function Test-SurveysWanted($Settings, [string]$Version) { @(Get-MatchingSurveys $Settings $Version).Count -gt 0 }

function Read-PlaytestSettings([string]$ProjectPath) {
    $path = Get-SettingsPath $ProjectPath
    $defaults = { param($id, $title, $day) [pscustomobject]@{ enabled = $false; id = $id; title = $title; versions = '0.2.x'; formUrl = ''; entry = ''; round = 1; nightDay = $day } }
    $settings = if (Test-Path -LiteralPath $path) { Get-Content -LiteralPath $path -Raw -Encoding utf8 | ConvertFrom-Json } else { [pscustomobject]@{} }
    if (-not ($settings.PSObject.Properties.Name -contains 'logVersions')) { $settings | Add-Member logVersions '*' }
    if (-not ($settings.PSObject.Properties.Name -contains 'night') -or -not $settings.night) { $settings | Add-Member -Force night (& $defaults 'NIGHT' '밤 정비' 1) }
    if (-not ($settings.PSObject.Properties.Name -contains 'exit') -or -not $settings.exit) { $settings | Add-Member -Force exit (& $defaults 'EXIT' '게임 종료' 1) }
    $settings
}
function ConvertTo-SettingsJson($Settings) {
    $survey = { param($s) [ordered]@{ enabled = [bool]$s.enabled; id = [string]$s.id; title = [string]$s.title; versions = [string]$s.versions
        formUrl = [string]$s.formUrl; entry = [string]$s.entry; round = [int]$s.round; nightDay = [int]$s.nightDay } }
    $ordered = [ordered]@{ logVersions = [string]$Settings.logVersions; night = (& $survey $Settings.night); exit = (& $survey $Settings.exit) }
    (($ordered | ConvertTo-Json -Depth 5) -replace "`r?`n", "`r`n") + "`r`n"
}
function Copy-PlaytestSettings($Settings) { (ConvertTo-SettingsJson $Settings) | ConvertFrom-Json }
function Write-PlaytestSettings([string]$ProjectPath, $Settings) {
    $path = Get-SettingsPath $ProjectPath
    $json = ConvertTo-SettingsJson $Settings
    $current = if (Test-Path -LiteralPath $path) { ConvertTo-SettingsJson (Get-Content -LiteralPath $path -Raw -Encoding utf8 | ConvertFrom-Json) } else { '' }
    if ($current -ceq $json) { return $false }
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path)) | Out-Null
    [IO.File]::WriteAllText($path, $json, $script:Utf8); $true
}

# ---- Unity·프로젝트 상태 ----
function Get-HelperConfig { if (Test-Path -LiteralPath $script:HelperConfigPath) { Get-Content -LiteralPath $script:HelperConfigPath -Raw | ConvertFrom-Json } }
function Save-HelperConfig([hashtable]$Values) {
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($script:HelperConfigPath)) | Out-Null
    [IO.File]::WriteAllText($script:HelperConfigPath, ($Values | ConvertTo-Json), $script:Utf8)
}
function Find-UnityEditor([string]$ProjectPath) {
    $version = Get-UnityVersion $ProjectPath
    $candidates = [System.Collections.Generic.List[string]]::new()
    $config = Get-HelperConfig
    if ($config -and $config.PSObject.Properties.Name -contains 'unityPath' -and $config.unityPath -like "*$version*") { $candidates.Add($config.unityPath) }
    $candidates.Add((Join-Path $env:ProgramFiles "Unity\Hub\Editor\$version\Editor\Unity.exe"))
    $secondary = Join-Path $env:APPDATA 'UnityHub\secondaryInstallPath.json'
    if (Test-Path -LiteralPath $secondary) {
        try { $dir = Get-Content -LiteralPath $secondary -Raw | ConvertFrom-Json; if ($dir) { $candidates.Add((Join-Path $dir "$version\Editor\Unity.exe")) } } catch { }
    }
    $candidates.Add((Join-Path $env:ProgramFiles "Unity $version\Editor\Unity.exe"))
    $candidates | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
}
function Test-ProjectOpen([string]$ProjectPath) {
    # Unity가 프로젝트를 열고 있는 동안 Temp/UnityLockfile을 잠근다. 파일만 남고 잠기지 않았으면 닫힌 상태다.
    $lock = Join-Path $ProjectPath 'Temp\UnityLockfile'
    if (-not (Test-Path -LiteralPath $lock)) { return $false }
    try { $stream = [IO.File]::Open($lock, 'Open', 'ReadWrite', 'None'); $stream.Dispose(); $false } catch { $true }
}
function Test-TelemetryConfigured {
    foreach ($name in 'CURTAINCALL_TELEMETRY_URL', 'CURTAINCALL_TELEMETRY_TOKEN') {
        if (-not ([Environment]::GetEnvironmentVariable($name) -or [Environment]::GetEnvironmentVariable($name, 'User'))) { return $false }
    }
    $true
}

# ---- 빌드 요청 ----
function Get-DefaultOutput([string]$ProjectPath, [string]$Version, [string]$Kind, [bool]$Logs, [bool]$Surveys, [string]$ProductName) {
    $kindName = if ($Kind -eq 'Dev') { 'DEV' } else { 'LIVE' }
    $collect = if ($Logs -and $Surveys) { '로그설문' } elseif ($Logs) { '로그' } elseif ($Surveys) { '설문' } else { '기본' }
    $safeVersion = if ($Version) { $Version -replace '[\\/:*?"<>|]', '_' } else { 'version' }
    Join-Path $ProjectPath "Builds\$safeVersion-Windows-$kindName-$collect\$ProductName.exe"
}
function Test-BuildRequest($Request, $Settings, [string]$UnityPath, [string]$ProjectPath) {
    $problems = [System.Collections.Generic.List[string]]::new()
    if ($Request.Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+$') { $problems.Add('게임 버전은 0.2.2처럼 숫자 3개로 입력하세요.') }
    if (-not $UnityPath -or -not (Test-Path -LiteralPath $UnityPath)) { $problems.Add('Unity.exe를 찾지 못했습니다. [변경]으로 선택하세요.') }
    if (-not $Request.Output -or -not [IO.Path]::IsPathRooted($Request.Output) -or -not $Request.Output.EndsWith('.exe', [StringComparison]::OrdinalIgnoreCase)) {
        $problems.Add('저장 위치는 전체 경로의 .exe 파일이어야 합니다.')
    } elseif ([IO.Path]::GetFullPath($Request.Output).StartsWith([IO.Path]::GetFullPath((Join-Path $ProjectPath 'Assets')) + '\', [StringComparison]::OrdinalIgnoreCase)) {
        $problems.Add('저장 위치를 Assets 폴더 안에 둘 수 없습니다.')
    }
    foreach ($slot in Get-SurveySlots $Settings) {
        if ($slot.Survey.enabled) {
            $bad = Get-SurveyProblems $slot.Survey
            if ($bad.Count) { $problems.Add("$($slot.Label) 설문이 '사용'인데 내용이 올바르지 않습니다: $($bad -join ', '). [설문 등록]에서 고치거나 사용을 끄세요.") }
        }
    }
    if (-not (Test-VersionPattern $Settings.logVersions)) { $problems.Add('설정 파일의 로그 대상 버전(logVersions)이 올바르지 않습니다.') }
    if ($Request.Logs) {
        if (-not (Test-VersionMatch $Request.Version $Settings.logVersions)) { $problems.Add("로그 대상 버전($($Settings.logVersions))에 $($Request.Version)이 포함되지 않습니다.") }
        if (-not (Test-TelemetryConfigured)) { $problems.Add('이 PC에 로그 전송 설정이 없습니다. Harness/Project/Guides/Log-system/local-test.md의 최초 전송 설정을 먼저 하세요.') }
    }
    if ($Request.Surveys -and -not (Test-SurveysWanted $Settings $Request.Version)) {
        $problems.Add("이 버전($($Request.Version))에 열릴 설문이 없습니다. [설문 등록]에서 대상 버전을 맞추거나 설문 사용을 끄세요.")
    }
    , $problems
}
function New-BuildLogPaths([string]$ProjectPath, $Request) {
    $dir = Join-Path $ProjectPath 'Builds\_logs'
    [IO.Directory]::CreateDirectory($dir) | Out-Null
    $stem = Join-Path $dir ("{0}-{1}-{2}" -f (Get-Date).ToString('yyyyMMdd-HHmmss'), $Request.Version, $Request.Kind)
    [pscustomobject]@{ Log = "$stem.log"; Result = "$stem.result.json" }
}
function Get-UnityBuildArguments([string]$ProjectPath, $Request, [string]$LogPath, [string]$ResultPath) {
    @('-batchmode', '-quit', '-projectPath', $ProjectPath, '-buildTarget', 'Win64', '-executeMethod', $script:RunnerMethod,
      '-ccVersion', $Request.Version, '-ccKind', $Request.Kind,
      '-ccLogs', ([string]$Request.Logs).ToLowerInvariant(), '-ccSurveys', ([string]$Request.Surveys).ToLowerInvariant(),
      '-ccOutput', $Request.Output, '-ccResult', $ResultPath, '-logFile', $LogPath)
}
function Start-UnityBatchBuild([string]$UnityPath, [string]$ProjectPath, $Request, [string]$LogPath, [string]$ResultPath) {
    $info = [System.Diagnostics.ProcessStartInfo]::new($UnityPath)
    foreach ($arg in Get-UnityBuildArguments $ProjectPath $Request $LogPath $ResultPath) { $info.ArgumentList.Add($arg) }
    $info.UseShellExecute = $false; $info.CreateNoWindow = $true; $info.WorkingDirectory = $ProjectPath
    [System.Diagnostics.Process]::Start($info)
}

$script:LogPatterns = '\[BuildHelper\]|error CS\d+|BuildFailedException|Build (?:Finished|succeeded|failed)|compiler errors|DisplayProgressbar: |another Unity instance|No valid Unity Editor license|Aborting batchmode'
function Read-NewLogLines($State) {
    if (-not $State.LogPath -or -not (Test-Path -LiteralPath $State.LogPath)) { return @() }
    try {
        $stream = [IO.File]::Open($State.LogPath, 'Open', 'Read', 'ReadWrite, Delete')
        try {
            if ($stream.Length -lt $State.LogPosition) { $State.LogPosition = 0L; $State.LogRemainder = '' }
            [void]$stream.Seek($State.LogPosition, 'Begin')
            $reader = [IO.StreamReader]::new($stream, $script:Utf8)
            $text = $State.LogRemainder + $reader.ReadToEnd()
            $State.LogPosition = $stream.Length
        } finally { $stream.Dispose() }
    } catch { return @() }
    $lines = $text -split "`r?`n"
    $State.LogRemainder = $lines[-1]
    $shown = [System.Collections.Generic.List[string]]::new()
    if (-not $State.SeenLines) { $State.SeenLines = [System.Collections.Generic.HashSet[string]]::new() }
    foreach ($line in $lines[0..([Math]::Max(0, $lines.Count - 2))]) {
        if ($lines.Count -lt 2) { break }
        $trimmed = $line.Trim()
        if ($trimmed -match $script:LogPatterns -and $State.SeenLines.Add($trimmed) -and $shown.Count -lt 20) {
            $shown.Add(($trimmed -replace '^DisplayProgressbar: ', '진행: '))
        }
    }
    $shown
}
function Read-BuildOutcome($Process, [string]$LogPath, [string]$ResultPath) {
    $Process.WaitForExit()
    $details = [System.Collections.Generic.List[string]]::new()
    $log = if ($LogPath -and (Test-Path -LiteralPath $LogPath)) { Get-Content -LiteralPath $LogPath -Encoding utf8 } else { @() }
    $compileErrors = @($log | Where-Object { $_ -match 'error CS\d+' } | Select-Object -Unique -First 8)
    foreach ($line in $compileErrors) { $details.Add('컴파일 오류: ' + $line.Trim()) }
    if ($ResultPath -and (Test-Path -LiteralPath $ResultPath)) {
        $result = Get-Content -LiteralPath $ResultPath -Raw -Encoding utf8 | ConvertFrom-Json
        $ok = [bool]$result.success -and $Process.ExitCode -eq 0 -and (Test-Path -LiteralPath $result.output)
        return [pscustomobject]@{ Success = $ok; Message = [string]$result.message; Details = $details }
    }
    $message = if ($log -match 'another Unity instance') { 'Unity 에디터가 이 프로젝트를 열고 있습니다. 에디터를 닫고 다시 시도하세요.' }
        elseif ($log -match 'No valid Unity Editor license') { 'Unity 라이선스를 확인하지 못했습니다. Unity Hub에 로그인한 뒤 다시 시도하세요.' }
        elseif ($compileErrors.Count -or ($log -match 'compiler errors')) { '스크립트 컴파일 오류로 빌드하지 못했습니다.' }
        elseif ($log -match 'executeMethod.*could not be found|could not find.*PlaytestBuildRunner') { '프로젝트에 PlaytestBuildRunner가 없습니다. 최신 dev를 받은 프로젝트인지 확인하세요.' }
        else { "Unity가 결과 없이 종료되었습니다(종료 코드 $($Process.ExitCode))." }
    [pscustomobject]@{ Success = $false; Message = $message; Details = $details }
}
function Write-BuildInfo($Request) {
    $folder = [IO.Path]::GetDirectoryName($Request.Output)
    $kind = if ($Request.Kind -eq 'Dev') { 'TEST (개발자 버전 · Development Build · Dev Mode 항상 켜짐)' } else { 'LIVE (서비스용)' }
    $text = @(
        "게임 $($Request.Version) / Windows / $kind",
        "로그 $(if ($Request.Logs) { '켬' } else { '끔' }) / 설문 $(if ($Request.Surveys) { '켬' } else { '끔' })",
        "빌드 시각 $((Get-Date).ToString('yyyy-MM-dd HH:mm'))",
        '', '전달할 때는 이 폴더 전체를 압축하세요. 받는 사람은 .exe만 실행하면 됩니다.'
    ) -join "`r`n"
    [IO.File]::WriteAllText((Join-Path $folder '빌드정보.txt'), $text + "`r`n", [System.Text.UTF8Encoding]::new($true))
}

function Invoke-BuildHelperSelfTest([string]$ProjectPath) {
    $settings = Read-PlaytestSettings $ProjectPath
    $version = Get-ProjectVersion $ProjectPath
    $checks = [ordered]@{
        'Unity 버전' = Get-UnityVersion $ProjectPath
        'Unity.exe' = Find-UnityEditor $ProjectPath
        '게임 버전' = $version
        '제품 이름' = Get-ProductName $ProjectPath
        '에디터 열림' = Test-ProjectOpen $ProjectPath
        '설정 왕복 동일' = ((ConvertTo-SettingsJson $settings) -ceq (ConvertTo-SettingsJson (Copy-PlaytestSettings $settings)))
        '0.2.2 대상 설문' = (@(Get-MatchingSurveys $settings '0.2.2') | ForEach-Object Label) -join ', '
        '0.3.0 대상 설문' = (@(Get-MatchingSurveys $settings '0.3.0') | ForEach-Object Label) -join ', '
        '버전 규칙' = ((Test-VersionMatch '0.2.2' '0.2.x') -and -not (Test-VersionMatch '0.3.0' '0.2.x') -and (Test-VersionMatch '1.0.0' '*') -and -not (Test-VersionMatch '0.2.20' '0.2.2'))
        '기본 저장 위치' = Get-DefaultOutput $ProjectPath '0.2.2' 'Dev' $true $false (Get-ProductName $ProjectPath)
        '요청 검사(0.2.2 라이브·설문)' = (Test-BuildRequest ([pscustomobject]@{ Version = '0.2.2'; Kind = 'Live'; Logs = $false; Surveys = $true; Output = (Get-DefaultOutput $ProjectPath '0.2.2' 'Live' $false $true 'Game') }) $settings (Find-UnityEditor $ProjectPath) $ProjectPath) -join ' | '
        '요청 검사(잘못된 버전)' = (Test-BuildRequest ([pscustomobject]@{ Version = '0.2'; Kind = 'Dev'; Logs = $false; Surveys = $true; Output = (Join-Path $ProjectPath 'Assets\x.exe') }) $settings (Find-UnityEditor $ProjectPath) $ProjectPath) -join ' | '
        'Unity 인자' = (Get-UnityBuildArguments $ProjectPath ([pscustomobject]@{ Version = '0.2.2'; Kind = 'Dev'; Logs = $true; Surveys = $false; Output = 'C:\b\g.exe' }) 'C:\b\l.log' 'C:\b\r.json') -join ' '
    }
    $checks.GetEnumerator() | ForEach-Object { '{0}: {1}' -f $_.Key, $_.Value }
}
