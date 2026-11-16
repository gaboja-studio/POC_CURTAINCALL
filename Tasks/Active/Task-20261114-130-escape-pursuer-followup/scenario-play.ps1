param(
    [Parameter(Mandatory)] [string]$Build,
    [Parameter(Mandatory)] [string]$Evidence,
    [Parameter(Mandatory)] [ValidateSet('InjuredWire','StackAndPole','TrampolineHandoff','ConveyorQueue','LowPosturePursuit')] [string]$Scenario,
    [int]$Port = 17839
)
$ErrorActionPreference = 'Stop'
$Build = (Resolve-Path -LiteralPath $Build).Path
if (Test-Path -LiteralPath $Evidence) { throw 'Use a new evidence directory for each run.' }
$Evidence = (New-Item -ItemType Directory -Path $Evidence).FullName
$count = if ($Scenario -in 'StackAndPole','ConveyorQueue') { 4 } else { 3 }
$processes = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
$ids = [int[]]::new($count)
$result = 'FAIL'
$script:lastProbeTime = -1

function Send([int]$Slot, [hashtable]$Fields) {
    $Fields.id = ++$ids[$Slot]
    if (!$Fields.ContainsKey('seconds')) { $Fields.seconds = .25 }
    if (!$Fields.ContainsKey('action')) { $Fields.action = 'input' }
    $file = Join-Path $Evidence "command-$Slot.json"
    $Fields | ConvertTo-Json -Compress | Set-Content -LiteralPath "$file.new"
    for ($i = 0; $i -lt 15; $i++) {
        try { [IO.File]::Move("$file.new", $file, $true); return }
        catch [IO.IOException] { Start-Sleep -Milliseconds 55 }
    }
    throw "Cannot write input for slot $Slot"
}

function ReadClient([int]$Slot) {
    $file = Join-Path $Evidence "state-$Slot.json"
    if (!(Test-Path -LiteralPath $file)) { return }
    for ($attempt = 0; $attempt -lt 3; $attempt++) {
        try { return (Get-Content -LiteralPath $file -Raw | ConvertFrom-Json) }
        catch { Start-Sleep -Milliseconds 35 }
    }
}

function LocalActor($Snapshot) { $Snapshot.players | Where-Object local }
function ReadTeam { foreach ($slot in 0..($count-1)) { $state = ReadClient $slot; ,$state } }

function Await([string]$Step, [scriptblock]$Condition, [float]$Seconds = 8) {
    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
    do {
        $team = @(ReadTeam)
        if ($team.Count -eq $count -and @($team | Where-Object { $_ -and $_.players }).Count -eq $count) {
            $actor = @($team | ForEach-Object { LocalActor $_ })
            $driverError = $team | Where-Object error | Select-Object -First 1
            if ($driverError) { throw "Review slot $($driverError.slot) reported $($driverError.error)" }
            if ($team[0].time -ne $script:lastProbeTime) {
                $script:lastProbeTime = $team[0].time
                @{step=$Step; clock=$team[0].clock; pursuer=$team[0].pursuer; progress=$team[0].routeProgress;
                  pursuit=$team[0].pursuit; authoritative=$team[0].players; owners=$actor} | ConvertTo-Json -Compress -Depth 8 |
                    Add-Content -LiteralPath (Join-Path $Evidence 'trace.jsonl')
            }
            if ($actor.Count -eq $count -and (& $Condition $team $actor)) {
                @{step=$Step; clock=$team[0].clock; views=$team} | ConvertTo-Json -Compress -Depth 11 |
                    Add-Content -LiteralPath (Join-Path $Evidence 'milestones.jsonl')
                Write-Host "PASS $Step"
                return
            }
        }
        Start-Sleep -Milliseconds 130
    } while ([DateTime]::UtcNow -lt $deadline)
    $team | ConvertTo-Json -Depth 11 | Set-Content -LiteralPath (Join-Path $Evidence 'failure-state.json')
    throw "Timed out before $Step"
}

function Setup([int]$Slot,[float]$X,[float]$Y,[float]$Z,[int]$Yaw) {
    Send $Slot @{action='setup'; target=@{x=$X;y=$Y;z=$Z}; yaw=$Yaw; pitch=8}
}
function Walk([int]$Slot,[float]$X,[float]$Z,[float]$Seconds=5,[switch]$Balance) {
    Send $Slot @{action='walk'; target=@{x=$X;z=$Z}; seconds=$Seconds; balance=[bool]$Balance}
}
function Damage([int]$Slot,[int]$Mask) {
    if ($Mask -notin 4,12,13,15) { throw 'Use a canonical injury mask.' }
    $actor = LocalActor (ReadClient $Slot)
    if (!$actor) { throw "Missing client actor $Slot" }
    foreach ($part in 4,12,13,15) {
        if (($part -band $Mask) -ne $part -or ($actor.injury -band $part) -eq $part) { continue }
        Send 0 @{action='damage'; actor=$actor.id; damage=$part}
        Await "damage mask $part replicated for slot $Slot" {
            param($views,$players)
            $id = $players[$Slot].id
            (@($views | Where-Object {
                $observed = $_.players | Where-Object id -eq $id
                !$observed -or ($observed.injury -band $part) -ne $part
            }).Count -eq 0)
        } 5
    }
}
function WasCaught($Actor) { $Actor.state -eq 'EscapeCaptured' -or ($Actor.state -eq 'Retired' -and $Actor.death -eq 'Pursuit') }

function InjuredWire {
    Setup 0 -238.5 5.9 183.8 270
    Setup 1 -236.9 5.9 184.4 270
    Setup 2 -234.8 5.9 185.5 270
    Await 'three players on the lower receiver' { param($v,$p) @($p | Where-Object { $_.feet.z -gt 182 -and $_.feet.z -lt 186 }).Count -eq 3 }
    Damage 0 4
    Damage 1 12
    Damage 2 13
    Walk 0 -239.35 189.9 12
    Await 'single leg player walks up receiver steps' { param($v,$p) $p[0].feet.y -gt 6.48 -and $p[0].feet.z -gt 189.3 } 14
    Send 0 @{tap=$true}
    Await 'lever opens for all three injured players' { param($v,$p) @($v | Where-Object { $_.locks[0] }).Count -eq 3 }
    foreach ($slot in 0..2) {
        Walk $slot -239.8 192.3 16
        Await "slot $slot approaches the wire" {
            param($v,$p) [math]::Abs($p[$slot].feet.z - 192.3) -lt .38 -and $p[$slot].feet.x -lt -239.35
        } 16
        Walk $slot -254.35 192.3 26 -Balance
        Await "slot $slot crosses the wire and stays alive" {
            param($v,$p) $p[$slot].state -eq 'Normal' -and $p[$slot].feet.x -lt -254.07 -and $p[$slot].feet.y -gt 6.46 -and !$p[$slot].hanging
        } 26
        Walk $slot -260 192.65 5
    }
    Await 'pursuer follows the injured party beyond the conveyor and attacks a real survivor' {
        param($v,$p)
        $v[0].routeProgress -ge 10 -and
        (($v[0].pursuit.action -in 'Warning','Strike','Grab','Throw','DebrisWarning','DebrisFlight' -and $v[0].pursuit.attackActor -in $p.id) -or
        @($p | Where-Object { WasCaught $_ }).Count -gt 0)
    } 100
    Await 'pursuer switches to a living teammate after the first pursuit retirement' {
        param($v,$p)
        $remaining = @($p | Where-Object state -eq 'Normal')
        @($p | Where-Object { $_.state -eq 'Retired' -and $_.death -eq 'Pursuit' }).Count -ge 1 -and
            $remaining.Count -gt 0 -and $v[0].pursuit.target -in $remaining.id
    } 60
}

function StackAndPole {
    Setup 0 -324.55 17.08 13.8 270
    Setup 1 -324.55 17.08 14.9 180
    Setup 2 -326.5 16.8 12.3 90
    Await 'passenger, carrier and pole runner at checkpoints' {
        param($v,$p) @($p[0..2] | Where-Object { $_.feet.x -lt -323 -and $_.feet.x -gt -328 }).Count -eq 3
    }
    Damage 0 4
    Damage 1 13
    Damage 2 12
    Send 0 @{crouch=$true; sprint=$true; seconds=3}
    Await 'crouch takes priority over sprint' { param($v,$p) $p[0].crouched -and !$p[0].sprinting }
    Send 1 @{tap=$true}
    Start-Sleep -Milliseconds 550
    if ((LocalActor (ReadClient 1)).below -ge 0) { throw 'Passenger mounted an actively crouching carrier' }
    Send 0 @{seconds=.2}
    Await 'injured carrier stands again' { param($v,$p) !$p[0].crouched }
    Await 'pursuer navigates toward the cooperative ascent under its own route logic' {
        param($v,$p) $v[0].pursuit.gate -eq 7 -or $v[0].routeProgress -ge 38
    } 105
    Send 1 @{tap=$true}
    Await 'injured carrier and passenger link on both owners' { param($v,$p) $p[1].below -eq $p[0].id -and $p[0].above -eq $p[1].id }
    Send 2 @{tap=$true}
    Await 'double leg injury can still grab ascent pole while a separate stack is active' {
        param($v,$p) $p[2].pole -eq 10 -and $p[1].below -eq $p[0].id
    }
    Send 0 @{move=-.28; sprint=$true; seconds=.5}
    Await 'pursuer commits an attack while a stack and pole rider are nearby' {
        param($v,$p)
        $p[1].below -eq $p[0].id -and $p[2].pole -eq 10 -and $v[0].pursuit.attackActor -in $p[0..2].id
    } 22
    Await 'pursuer actually captures a mounted or suspended player' {
        param($v,$p) @($p[0..2] | Where-Object { WasCaught $_ }).Count -gt 0
    } 27
    Await 'pole rider releases after pursuit damage rather than an ordinary dismount' {
        param($v,$p) $p[2].pole -lt 0 -and ($p[2].injury -ne 12 -or (WasCaught $p[2]))
    } 8
    Await 'captured player releases the seat or piggyback link on all observers' {
        param($v,$p)
        $caught = @($p[0..2] | Where-Object { WasCaught $_ })
        $caught.Count -gt 0 -and @($v | Where-Object {
            @($_.players | Where-Object { $_.id -in $caught.id -and ($_.pole -ge 0 -or $_.above -ge 0 -or $_.below -ge 0) }).Count -eq 0
        }).Count -eq 4
    } 5
    Await 'pursuer reselects a living player after the takedown' {
        param($v,$p)
        $alive = @($p | Where-Object state -eq 'Normal')
        @($p[0..2] | Where-Object { $_.state -eq 'Retired' -and $_.death -eq 'Pursuit' }).Count -gt 0 -and
            $alive.Count -gt 0 -and $v[0].pursuit.target -in $alive.id
    } 50
}

function TrampolineHandoff {
    Setup 0 -289.8 20.08 90 0
    Setup 1 -285 25.08 94.3 180
    Setup 2 -284.15 25.08 95.1 270
    Await 'single lower worker and two upper survivors at rebound island' {
        param($v,$p) $p[0].feet.y -gt 19.9 -and $p[0].feet.y -lt 20.3 -and
            $p[1].feet.y -gt 24.8 -and $p[2].feet.y -gt 24.8 -and $p[1].feet.x -lt -283 -and $p[2].feet.x -lt -283
    }
    Damage 0 13
    Damage 1 4
    Await 'one arm player can take trampoline from its handle' { param($v,$p) $v[0].trampoline.canGrab }
    Send 0 @{tap=$true}
    Await 'one arm trampoline acquisition is synchronized' {
        param($v,$p) @($v | Where-Object { $_.trampoline.carrier -eq $p[0].id }).Count -eq 3
    }
    Await 'pursuer reaches and captures the trampoline carrier while the team is in play' {
        param($v,$p) (WasCaught $p[0])
    } 108
    Await 'pursuit capture frees trampoline on every observer' {
        param($v,$p) @($v | Where-Object { $_.trampoline.carrier -eq -1 }).Count -eq 3
    } 8
    $surface = (ReadClient 1).trampoline.surface
    Send 1 @{action='walk'; target=@{x=$surface.x; z=$surface.z}; jump=$true; sprint=$true; seconds=2.2}
    Await 'injured remote jumper rebounds from the released trampoline while pursuit continues' {
        param($v,$p) $v[1].trampoline.bounces -gt 0 -and $p[1].state -eq 'Normal'
    } 8
    $handle = (ReadClient 2).trampoline.handle
    Walk 2 ($handle.x + .85) ($handle.z + .8) 5
    Await 'survivor can take the dropped trampoline' { param($v,$p) $v[2].trampoline.canGrab } 5
    Send 2 @{tap=$true}
    Await 'survivor receives ownership after carrier capture' {
        param($v,$p) @($v | Where-Object { $_.trampoline.carrier -eq $p[2].id }).Count -eq 3
    }
}

function LowPosturePursuit {
    Setup 0 -219.1 25.08 95.4 180
    Setup 1 -214.2 25.08 86.0 180
    Setup 2 -215.1 25.08 79.6 180
    Await 'three players on the entrance floor' {
        param($v,$p) @($p | Where-Object { $_.feet.z -ge 78 -and $_.feet.z -le 97 -and $_.feet.y -gt 24.9 }).Count -eq 3
    }
    Damage 0 4
    Damage 1 4
    Send 0 @{crouch=$true; sprint=$true; seconds=90}
    Await 'injured player stays crouched before pursuer arrives' { param($v,$p) $p[0].crouched -and !$p[0].sprinting }
    Send 1 @{action='walk'; target=@{x=-214.3;z=79.8}; jump=$true; sprint=$true; seconds=1.4}
    Await 'another survivor changes position while the crouched runner holds' {
        param($v,$p) $p[1].feet.z -lt 84.8 -and $p[1].state -eq 'Normal' -and $p[0].crouched
    } 6
    Await 'pursuer actually aims at the crouched injured player' {
        param($v,$p) $p[0].crouched -and $v[0].pursuit.attackActor -eq $p[0].id -and
            $v[0].pursuit.action -in 'Warning','Strike','DebrisWarning','DebrisFlight'
    } 65
    Await 'pursuer captures the crouched player' { param($v,$p) (WasCaught $p[0]) } 22
    Await 'pursuer attacks a different survivor after retirement' {
        param($v,$p) $p[0].state -eq 'Retired' -and $p[0].death -eq 'Pursuit' -and
            $v[0].pursuit.attackActor -in $p[1..2].id -and $v[0].pursuit.action -in 'Warning','Strike','Grab','DebrisWarning'
    } 45
}

function ConveyorQueue {
    Setup 0 -268.35 3.08 116.12 0
    Setup 1 -265.65 3.08 116.08 0
    Setup 2 -266.95 3.08 112.7 0
    Await 'three on the conveyor deck in a four player round' {
        param($v,$p) $p[0].boardable -eq 2 -and $p[1].feet.y -lt 3.3 -and $p[2].feet.y -lt 3.3
    }
    Damage 0 4
    Damage 1 13
    Damage 2 12
    Send 0 @{tap=$true}
    Await 'first injured player boards' { param($v,$p) $p[0].seated } 5
    Await 'host grants separate waiting windows to injured teammates rather than the occupied seat' {
        param($v,$p)
        @($v[0].players | Where-Object { $_.id -in $p[0..2].id -and $_.protectedWait }).Count -eq 3
    } 5
    Send 1 @{tap=$true}
    Start-Sleep -Milliseconds 550
    if ((LocalActor (ReadClient 1)).seated) { throw 'Simultaneous occupancy in one cannon' }
    Send 0 @{action='conveyor'; seconds=24}
    Await 'first injured player clears the seat and catches a moving pole' { param($v,$p) $p[0].pole -in 3..8 } 15
    Walk 1 -268.35 116.12 4
    Await 'waiting survivor can board after launcher cycle' { param($v,$p) $p[1].boardable -eq 2 -and !$p[0].seated } 10
    Send 1 @{action='conveyor';seconds=24}
    Await 'second injured player boards and launches' { param($v,$p) $p[1].flying -or $p[1].pole -in 3..8 } 15
    Await 'second injured player catches a moving pole' { param($v,$p) $p[1].pole -in 3..8 -and $p[1].state -eq 'Normal' } 8
    Await 'pursuer returns to the injured player who stays after the waiting window expires' {
        param($v,$p) (WasCaught $p[2]) -and !($v[0].players | Where-Object id -eq $p[2].id).protectedWait
    } 110
}

try {
    for ($slot = 0; $slot -lt $count; $slot++) {
        $role = if ($slot -eq 0) { '-escapeHost' } else { '-escapeClient' }
        $out = Join-Path $Evidence "player-$slot.log"
        $processes.Add((Start-Process -FilePath $Build -WindowStyle Hidden -PassThru -ArgumentList @(
            $role,'-escapePort',$Port,'-escapePlayers',$count,'-escapeReview',('"' + $Evidence + '"'),'-escapeSlot',$slot,
            '-escapeCampaignReview','-devmode','-screen-width','640','-screen-height','360','-screen-fullscreen','0','-logFile',('"' + $out + '"'))))
        if ($slot -eq 0) {
            $deadline = [DateTime]::UtcNow.AddSeconds(45)
            do {
                if ($processes[0].HasExited) { throw 'Host exited during launch' }
                if ((ReadClient 0).connection -eq 'Host') { break }
                Start-Sleep -Milliseconds 180
            } while ([DateTime]::UtcNow -lt $deadline)
        }
    }
    Await 'all participants report the active campaign' {
        param($v,$p) @($v | Where-Object { $_.ready -and !$_.blocked -and @($_.players).Count -eq $count }).Count -eq $count
    } 60
    Await 'development build exposes the authoritative pursuer probe' { param($v,$p) $v[0].pursuit.action } 4
    & $Scenario
    $result = 'PASS'
} finally {
    @{result=$result; scenario=$Scenario; players=$count} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Evidence 'result.json')
    for ($slot = 0; $slot -lt $processes.Count; $slot++) {
        if (!$processes[$slot].HasExited) { Send $slot @{action='quit'} }
    }
    foreach ($process in $processes) { if (!$process.WaitForExit(6500)) { $process.Kill() } }
}
