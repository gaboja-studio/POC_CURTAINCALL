# Build the Finder launcher ZIP without requiring a Mac or modifying Git's index.
[CmdletBinding()]
param([string]$OutputPath=(Join-Path $PSScriptRoot '../../asset-helper-mac.zip'))
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression
$stream=[IO.File]::Open([IO.Path]::GetFullPath($OutputPath),'Create','ReadWrite','None')
$zip=[IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Create,$true)
$plist=@'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict><key>CFBundleExecutable</key><string>asset-helper</string><key>CFBundleIdentifier</key><string>com.gaboja.curtaincall.asset-helper</string><key>CFBundleName</key><string>커튼콜 에셋 도우미</string><key>CFBundlePackageType</key><string>APPL</string><key>CFBundleVersion</key><string>1</string><key>LSUIElement</key><true/></dict></plist>
'@
$shell=@'
#!/bin/sh
ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/../../.." && pwd)
# Finder does not load the shell profile that adds Homebrew tools to PATH.
PATH="/opt/homebrew/bin:/usr/local/bin:${PATH:-/usr/bin:/bin:/usr/sbin:/sbin}"
export PATH
PWSH=$(command -v pwsh 2>/dev/null)
if [ -z "$PWSH" ]; then
  for candidate in /opt/homebrew/bin/pwsh /usr/local/bin/pwsh; do
    if [ -x "$candidate" ]; then PWSH="$candidate"; break; fi
  done
fi
if [ -z "$PWSH" ]; then
  osascript -e 'display dialog "PowerShell 7을 설치한 뒤 도우미를 다시 열어 주세요. 명령어 입력은 필요 없습니다." buttons {"확인"} default button 1'
  open 'https://aka.ms/powershell-release?tag=stable'
  exit 1
fi
nohup "$PWSH" -NoProfile -File "$ROOT/Scripts/Asset-helper/launch.ps1" -ProjectPath "$ROOT" >/dev/null 2>&1 &
exit 0
'@
try {
    foreach ($item in @(@('asset-helper.app/Contents/Info.plist',$plist,$false),@('asset-helper.app/Contents/MacOS/asset-helper',$shell,$true))) {
        $entry=$zip.CreateEntry($item[0])
        $entry.LastWriteTime=[DateTimeOffset]::new(2026,10,27,0,0,0,[TimeSpan]::Zero)
        $mode=if ($item[2]) { 33261 } else { 33188 }
        $entry.ExternalAttributes=($mode -shl 16)
        $writer=[IO.StreamWriter]::new($entry.Open(),[Text.UTF8Encoding]::new($false))
        try { $writer.Write(($item[1] -replace "`r`n","`n")+"`n") } finally { $writer.Dispose() }
    }
} finally { $zip.Dispose(); $stream.Dispose() }
# Mark central directory entries as Unix so Archive Utility preserves execute permission.
$bytes=[IO.File]::ReadAllBytes([IO.Path]::GetFullPath($OutputPath))
for ($i=0;$i -le $bytes.Length-46;$i++) {
    if ($bytes[$i] -eq 80 -and $bytes[$i+1] -eq 75 -and $bytes[$i+2] -eq 1 -and $bytes[$i+3] -eq 2) {
        $bytes[$i+5]=3
        $name=[BitConverter]::ToUInt16($bytes,$i+28); $extra=[BitConverter]::ToUInt16($bytes,$i+30); $comment=[BitConverter]::ToUInt16($bytes,$i+32)
        $i+=45+$name+$extra+$comment
    }
}
[IO.File]::WriteAllBytes([IO.Path]::GetFullPath($OutputPath),$bytes)
Write-Output 'Mac Finder launcher ZIP built (executable permission included).'
