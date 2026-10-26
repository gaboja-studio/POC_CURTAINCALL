# Local-only UI server. Open via asset-helper.vbs (Windows) or the Finder launcher app (macOS).
[CmdletBinding()]
param([string]$ProjectPath=(Join-Path $PSScriptRoot '../..'),[switch]$NoBrowser,[int]$Port=0)
$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath($ProjectPath)
$local=Join-Path $project '.harness/local/asset-helper'
New-Item -ItemType Directory -Path $local -Force | Out-Null
$endpoint=Join-Path $local 'endpoint.json'
if (-not $NoBrowser -and (Test-Path -LiteralPath $endpoint)) {
    try {
        $existing=Get-Content -LiteralPath $endpoint -Raw | ConvertFrom-Json
        if ($existing.url -match '^http://127\.0\.0\.1:\d+$' -and $existing.token -match '^[A-F0-9]{64}$') {
            $ping=Invoke-RestMethod ($existing.url+'/api/ping') -Headers @{ 'X-Asset-Helper'=$existing.token } -TimeoutSec 2
            if ($ping.project -ceq $project) { Start-Process ($existing.url+'/#'+$existing.token); exit 0 }
        }
    } catch {} # Stale endpoint; start a fresh process without terminating anything.
}
$token=[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
if (-not $Port) {
    $probe=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0)
    $probe.Start(); $Port=([Net.IPEndPoint]$probe.LocalEndpoint).Port; $probe.Stop()
}
$baseUrl="http://127.0.0.1:$Port"
$listener=[Net.HttpListener]::new()
$listener.Prefixes.Add($baseUrl+'/')
$listener.Start()
@{ url=$baseUrl; token=$token; pid=$PID } | ConvertTo-Json | Set-Content -LiteralPath $endpoint -Encoding utf8NoBOM
$session=Join-Path $local ('session-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $session | Out-Null
$jobs=@{}
$lastSeen=[DateTime]::UtcNow
$running=$true
function Reply($Context,[int]$Code,$Data,[string]$Type='application/json; charset=utf-8') {
    $response=$Context.Response
    $response.StatusCode=$Code
    $response.ContentType=$Type
    $response.Headers['Cache-Control']='no-store'
    $response.Headers['X-Content-Type-Options']='nosniff'
    $response.Headers['Content-Security-Policy']="default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'"
    $text=if ($Type.StartsWith('application/json')) { $Data | ConvertTo-Json -Depth 16 -Compress } else { [string]$Data }
    $bytes=[Text.Encoding]::UTF8.GetBytes($text)
    $response.ContentLength64=$bytes.Length
    try { $response.OutputStream.Write($bytes,0,$bytes.Length) } finally { $response.Close() }
}
function Spawn($Body) {
    $id=[guid]::NewGuid().ToString('N')
    $request=Join-Path $session "$id-request.json"
    $result=Join-Path $session "$id-result.json"
    $progress=Join-Path $session "$id-progress.json"
    @{ project=$project; action=$Body.action; snapshot=$Body.snapshot; message=$Body.message; keep=[bool]$Body.keep; backup=$Body.backup } | ConvertTo-Json |
        Set-Content -LiteralPath $request -Encoding utf8NoBOM
    $psi=[Diagnostics.ProcessStartInfo]::new([Environment]::ProcessPath)
    $psi.UseShellExecute=$false
    $psi.CreateNoWindow=$true
    foreach ($arg in @('-NoProfile','-File',(Join-Path $PSScriptRoot 'worker.ps1'),'-RequestPath',$request,'-ResultPath',$result,'-ProgressPath',$progress)) { $psi.ArgumentList.Add($arg) }
    $process=[Diagnostics.Process]::Start($psi)
    $jobs[$id]=@{ process=$process; result=$result; progress=$progress; action=$Body.action }
    return $id
}
try {
    if (-not $NoBrowser) { Start-Process ($baseUrl+'/#'+$token) }
    else { Write-Output ($baseUrl+'/#'+$token) }
    $pending=$listener.GetContextAsync()
    while ($running) {
        if (-not $pending.Wait(500)) {
            $busy=@($jobs.Values | Where-Object { -not $_.process.HasExited }).Count
            if (-not $busy -and ([DateTime]::UtcNow-$lastSeen).TotalMinutes -gt 15) { break }
            continue
        }
        $context=$pending.GetAwaiter().GetResult()
        $pending=$listener.GetContextAsync()
        $request=$context.Request
        $path=$request.Url.AbsolutePath
        try {
            if ($request.Url.Host -cne '127.0.0.1' -or $request.Url.Port -ne $Port) { Reply $context 403 @{ error='접근할 수 없습니다.' }; continue }
            if ($request.HttpMethod -eq 'GET' -and $path -in '/','/app.js','/style.css') {
                $file=switch ($path) { '/' { 'index.html' } '/app.js' { 'app.js' } '/style.css' { 'style.css' } }
                $type=switch ($path) { '/' { 'text/html; charset=utf-8' } '/app.js' { 'text/javascript; charset=utf-8' } default { 'text/css; charset=utf-8' } }
                Reply $context 200 (Get-Content -LiteralPath (Join-Path $PSScriptRoot $file) -Raw) $type
                continue
            }
            if ($request.Headers['X-Asset-Helper'] -cne $token -or ($request.Headers['Origin'] -and $request.Headers['Origin'] -cne $baseUrl)) { Reply $context 403 @{ error='도우미 아이콘으로 창을 다시 열어 주세요.' }; continue }
            $lastSeen=[DateTime]::UtcNow
            if ($request.HttpMethod -eq 'GET' -and $path -eq '/api/ping') { Reply $context 200 @{ project=$project }; continue }
            if ($request.HttpMethod -eq 'GET' -and $path -match '^/api/job/([a-f0-9]{32})$') {
                $job=$jobs[$Matches[1]]
                if (-not $job) { Reply $context 404 @{ error='작업을 찾을 수 없습니다.' }; continue }
                if (Test-Path -LiteralPath $job.result) { Reply $context 200 @{ done=$true; response=(Get-Content -LiteralPath $job.result -Raw | ConvertFrom-Json) } }
                elseif ($job.process.HasExited) { Reply $context 200 @{ done=$true; response=@{ ok=$false; error='작업이 완료되지 않았습니다. 저장 기록은 보존됩니다. 다시 상태를 확인해 주세요.' } } }
                else {
                    $progress=if (Test-Path -LiteralPath $job.progress) { try { (Get-Content -LiteralPath $job.progress -Raw | ConvertFrom-Json).message } catch { '처리 중입니다.' } } else { '상태를 확인하고 있습니다.' }
                    Reply $context 200 @{ done=$false; message=$progress }
                }
                continue
            }
            if ($request.HttpMethod -ne 'POST' -or $path -ne '/api/action') { Reply $context 404 @{ error='없는 화면입니다.' }; continue }
            if ($request.ContentLength64 -lt 0 -or $request.ContentLength64 -gt 4096) { Reply $context 413 @{ error='요청이 너무 큽니다.' }; continue }
            $reader=[IO.StreamReader]::new($request.InputStream,[Text.Encoding]::UTF8)
            $body=$reader.ReadToEnd() | ConvertFrom-Json
            if ($body.action -notin 'status','publish','sync','restore','login') { Reply $context 400 @{ error='알 수 없는 작업입니다.' }; continue }
            if (@($jobs.Values | Where-Object { -not $_.process.HasExited }).Count) { Reply $context 409 @{ error='현재 작업이 끝난 뒤 다시 눌러 주세요.' }; continue }
            Reply $context 202 @{ job=(Spawn $body) }
        } catch { try { Reply $context 500 @{ error='도우미 요청을 처리하지 못했습니다. 창을 새로 열어 주세요.' } } catch {} }
    }
} finally {
    $listener.Stop(); $listener.Close()
    foreach ($job in $jobs.Values) { $job.process.Dispose() } # Do not kill a running Git transfer.
    try { if ((Get-Content -LiteralPath $endpoint -Raw | ConvertFrom-Json).pid -eq $PID) { Remove-Item -LiteralPath $endpoint } } catch {}
}
