param(
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9_.-]{0,79}$')]
    [string]$Name = ('entry-space-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff')),
    [ValidateSet(3,4)][int]$Players = 4,
    [switch]$ContinueChoices,
    [ValidateRange(15,90)][double]$MaxSeconds = 90,
    [ValidateRange(8,60)][double]$ServiceReadySeconds = 45,
    [string]$HarnessRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../../.harness/local/pursuer-space-20261116'))
)

# Explicit checkpoint fixture, not an entrance-to-ending traversal. Running this file launches
# real clients, uses setup once per client, and then only ordinary Tap/Balance input.
# Both failure and success leave clients running for inspection; root owns shutdown.
$ErrorActionPreference = 'Stop'
if($ContinueChoices -and $Players -ne 4) { throw '-ContinueChoices is a four-client landing-only handoff; three-client entry uses the full pursuit observation' }
$HarnessRoot = [IO.Path]::GetFullPath($HarnessRoot)
$runFolder = Join-Path $HarnessRoot $Name
if (Test-Path -LiteralPath $runFolder) { throw "A new unique Name is required; existing run: $runFolder" }
foreach ($relative in @('launch.ps1','control.ps1','fixture-coordinates.json','build/PursuerCampaign.exe')) {
    if (!(Test-Path -LiteralPath (Join-Path $HarnessRoot $relative))) { throw "Missing fixture dependency: $relative" }
}
$coordinates = Get-Content -LiteralPath (Join-Path $HarnessRoot 'fixture-coordinates.json') -Raw | ConvertFrom-Json
$stationIndices = @(0,7,8,9)[0..($Players-1)]
$stations = @{}
for ($n=0; $n -lt $Players; $n++) {
    $matches = @($coordinates.stations | Where-Object index -eq $stationIndices[$n])
    if ($matches.Count -ne 1) { throw "Expected one station record for index $($stationIndices[$n])" }
    $stations[$n] = $matches[0]
    $board = if ($matches[0].PSObject.Properties['worldBoard']) { @($matches[0].worldBoard) } else { @($matches[0].board) }
    if ($board.Count -ne 3 -or @($matches[0].seat).Count -ne 3) { throw "Invalid world board/seat record for slot $n" }
}
. (Join-Path $HarnessRoot 'control.ps1') -Name $Name

$journal = Join-Path $runFolder 'entry-fixture.jsonl'
$summaryPath = Join-Path $runFolder 'entry-fixture-summary.json'
$script:entryWatch = [Diagnostics.Stopwatch]::StartNew()
$script:entryLatest = @{}
$script:entryLastTime = @{}
$script:entryLastFresh = @{}
$script:entryIds = @{}
$script:entryRestarts = @{}
$script:entryHealthRequired = $false
$script:entryStage = 'launch'
$script:entryFailure = $null
$script:entryPass = $false
$script:entryLandingPassed = $false
$script:entryPursuitObserved = $false
$script:entryCompletedScope = 'incomplete'
$script:entryFlying = @{}
$script:entryLanding = @{}
$script:entryTargetSamples = [Collections.Generic.List[object]]::new()
$script:entryProgress = [Collections.Generic.List[double]]::new()
$script:entryObservationStart = $null
$script:entryObservationEnd = $null
$script:entryProgressBeforeLaunch = $null
$script:entryDispatchSpread = $null
$script:entryBalanceSent = @{}
$script:entryTrackMotion = $false
$script:entryLastPursuer = $null
$script:entryPursuerTravel = 0.0
$script:entryServiceReady = $null
$script:entryReadinessStart = $null

function Convert-EntryIndexMap([Collections.IDictionary]$Map) {
    # ConvertTo-Json rejects integer Hashtable keys, even though in-memory slot indexing is valid.
    $safe=[ordered]@{}
    foreach($key in $Map.Keys) { $safe[[string]$key]=$Map[$key] }
    return $safe
}

function Write-EntryRecord([string]$Kind, $Value) {
    if (!(Test-Path -LiteralPath $runFolder)) { return }
    $row = [ordered]@{ utc=[DateTime]::UtcNow.ToString('o'); elapsed=$script:entryWatch.Elapsed.TotalSeconds;
        stage=$script:entryStage; kind=$Kind; checkpointFixture=$true; continuous=$false; value=$Value }
    [IO.File]::AppendAllText($journal, ($row | ConvertTo-Json -Depth 24 -Compress) + [Environment]::NewLine)
}
function Assert-EntryBudget {
    if ($script:entryWatch.Elapsed.TotalSeconds -ge $MaxSeconds) {
        throw "Entry checkpoint exceeded its $MaxSeconds second stopwatch budget at $script:entryStage; clients remain running"
    }
}
function Get-EntryActor([int]$Number) {
    $local = @($script:entryLatest[$Number].players | Where-Object local)
    if ($local.Count -eq 1) { return $local[0] }
    return $null
}
function Update-EntryStates {
    for ($n=0; $n -lt $Players; $n++) {
        Assert-EntryBudget
        $s = Sample $n
        if (!$s) { continue }
        $script:entryLatest[$n] = $s
        if (!$script:entryLastTime.ContainsKey($n) -or [double]$s.time -gt $script:entryLastTime[$n]) {
            $script:entryLastTime[$n] = [double]$s.time
            $script:entryLastFresh[$n] = $script:entryWatch.Elapsed.TotalSeconds
            Write-EntryRecord 'snapshot' @{slot=$n; snapshot=$s}
            if($n -eq 0 -and $script:entryTrackMotion) {
                if($null -ne $script:entryLastPursuer) {
                    $step=Entry-Distance $s.pursuer @($script:entryLastPursuer.x,$script:entryLastPursuer.y,$script:entryLastPursuer.z)
                    $script:entryPursuerTravel+=$step
                }
                $script:entryLastPursuer=$s.pursuer
            }
        } elseif ($script:entryIds.ContainsKey($n) -and $script:entryWatch.Elapsed.TotalSeconds-$script:entryLastFresh[$n] -gt 2.5) {
            throw "Slot $n snapshot stopped advancing"
        }
        if ($s.continuous) { throw "Slot $n is continuous; setup is forbidden. This script requires a separate Checkpoint run" }
        if ($s.error) { throw "Slot $n review error: $($s.error)" }
        if ($script:entryRestarts.ContainsKey($n) -and [int]$s.restarts -ne $script:entryRestarts[$n]) { throw "Slot $n restarted during the fixture" }
        if ($script:entryIds.ContainsKey($n)) {
            $a = Get-EntryActor $n
            if (!$a) { throw "Slot $n lost its unique local actor" }
            if ([string]$a.id -ne $script:entryIds[$n]) { throw "Slot $n actor changed during the fixture" }
            if ($script:entryHealthRequired -and ($a.state -ne 'Normal' -or [int]$a.injury -ne 0)) { throw "Slot $n state=$($a.state), injury=$($a.injury), death=$($a.death)" }
            if($script:entryLanding.ContainsKey($n) -and $a.state -eq 'Normal' -and !$a.airborne -and !$a.flying -and !$a.seated -and
                (!$script:entryBalanceSent.ContainsKey($n) -or $script:entryWatch.Elapsed.TotalSeconds-$script:entryBalanceSent[$n] -ge 15)) {
                Send-EntryCommand $n @{Balance=$true;Seconds=60}
            }
        }
    }
}
function Wait-Entry([string]$Label, [double]$Seconds, [scriptblock]$Condition) {
    $script:entryStage = $Label
    $until = $script:entryWatch.Elapsed.TotalSeconds + $Seconds
    do {
        Update-EntryStates
        if (& $Condition) { Write-EntryRecord 'stage-complete' @{label=$Label}; return }
        if ($script:entryWatch.Elapsed.TotalSeconds -ge $until) { break }
        Start-Sleep -Milliseconds 100
    } while ($true)
    throw "Timed out after $Seconds seconds: $Label"
}
function Send-EntryCommand([int]$Number, [hashtable]$Arguments) {
    Assert-EntryBudget
    Send-Review -Slot $Number @Arguments
    if($Arguments.ContainsKey('Balance') -and $Arguments.Balance) { $script:entryBalanceSent[$Number]=$script:entryWatch.Elapsed.TotalSeconds }
    $sent = Get-Content -LiteralPath (Join-Path $runFolder "command-$Number.json") -Raw | ConvertFrom-Json
    Write-EntryRecord 'command-published' @{slot=$Number; command=$sent}
}
function Entry-Distance($Feet, $Point) {
    return [Math]::Sqrt([Math]::Pow($Feet.x-$Point[0],2)+[Math]::Pow($Feet.y-$Point[1],2)+[Math]::Pow($Feet.z-$Point[2],2))
}

try {
    & (Join-Path $HarnessRoot 'launch.ps1') -Name $Name -Players $Players -Checkpoint | Out-Host
    Assert-EntryBudget
    New-Item -ItemType File -Path (Join-Path $runFolder 'record-fixture.flag') | Out-Null
    Write-EntryRecord 'scope' @{name=$Name; players=$Players; maxSeconds=$MaxSeconds; stations=$stationIndices;
        setup='One recorded relocation per local client to its world boarding point';
        traversalClaim=$false; automaticDamage=$false; automaticRestart=$false; automaticQuit=$false;
        launch='Separate real-client Checkpoint run'; continueChoices=[bool]$ContinueChoices;
        build=(Join-Path $HarnessRoot 'build/PursuerCampaign.exe')}
    Wait-Entry 'all clients ready' 25 {
        if ($script:entryLatest.Count -ne $Players) { return $false }
        for ($n=0; $n -lt $Players; $n++) {
            $s=$script:entryLatest[$n]; $a=Get-EntryActor $n
            if (!$s.ready -or $s.blocked -or $s.campaign -ne 'Escape' -or @($s.players).Count -ne $Players -or !$a) { return $false }
            if ($a.state -ne 'Normal' -or [int]$a.injury -ne 0) { throw "Fresh slot $n is not healthy and Normal" }
        }
        return $script:entryLatest[0].connection -eq 'Host'
    }
    for ($n=0; $n -lt $Players; $n++) {
        $script:entryIds[$n]=[string](Get-EntryActor $n).id
        $script:entryRestarts[$n]=[int]$script:entryLatest[$n].restarts
    }
    if (@($script:entryIds.Values | Select-Object -Unique).Count -ne $Players) { throw 'Slots do not represent distinct real actors' }
    $script:entryHealthRequired=$true
    $script:entryStage='checkpoint placement'
    for ($n=0; $n -lt $Players; $n++) {
        $station=$stations[$n]
        $board=if($station.PSObject.Properties['worldBoard']){@($station.worldBoard)}else{@($station.board)}
        $yaw=[Math]::Atan2($station.seat[0]-$board[0],$station.seat[2]-$board[2])*180/[Math]::PI
        Send-EntryCommand $n @{Action='setup'; X=[float]$board[0]; Y=[float]$board[1]; Z=[float]$board[2]; Yaw=[float]$yaw; Pitch=8; Seconds=.1}
    }
    Wait-Entry 'all exact boarding stations available' 5 {
        for ($n=0; $n -lt $Players; $n++) {
            $a=Get-EntryActor $n; $station=$stations[$n]
            $board=if($station.PSObject.Properties['worldBoard']){@($station.worldBoard)}else{@($station.board)}
            if (!$a -or $a.airborne -or $a.flying -or $a.seated -or (Entry-Distance $a.feet $board) -gt .35 -or $a.boardable -ne $stationIndices[$n]) { return $false }
        }
        return $true
    }
    $lastTap=@{}; $lastTapSnapshot=@{}
    Wait-Entry 'all clients seated in distinct cannons' 6 {
        $all=$true
        for ($n=0; $n -lt $Players; $n++) {
            $a=Get-EntryActor $n; $s=$script:entryLatest[$n]
            if ($a.seated) {
                if ((Entry-Distance $a.feet @($stations[$n].seat)) -gt .7) { throw "Slot $n seated at the wrong cannon position" }
                continue
            }
            $all=$false
            if ($a.airborne -or $a.flying) { throw "Slot $n left the ground before a seated observation; boarding Tap stopped" }
            if ($a.boardable -eq $stationIndices[$n] -and
                (!$lastTap.ContainsKey($n) -or $script:entryWatch.Elapsed.TotalSeconds-$lastTap[$n] -ge .75) -and
                (!$lastTapSnapshot.ContainsKey($n) -or [double]$s.time -gt $lastTapSnapshot[$n])) {
                Send-EntryCommand $n @{Tap=$true; Seconds=.2}
                $lastTap[$n]=$script:entryWatch.Elapsed.TotalSeconds; $lastTapSnapshot[$n]=[double]$s.time
            }
        }
        return $all
    }
    $script:entryProgressBeforeLaunch=[double]$script:entryLatest[0].routeProgress
    $script:entryTrackMotion=$true
    $script:entryLastPursuer=$script:entryLatest[0].pursuer
    $script:entryStage='same-batch ordinary launch Taps'
    $firstDispatch=$script:entryWatch.Elapsed.TotalSeconds
    for ($n=0; $n -lt $Players; $n++) { Send-EntryCommand $n @{Tap=$true; Seconds=.2} }
    $script:entryDispatchSpread=$script:entryWatch.Elapsed.TotalSeconds-$firstDispatch
    Write-EntryRecord 'launch-batch' @{dispatchSeconds=$script:entryDispatchSpread; note='File commands published in one batch; each real client consumes ordinary Tap on its own next review/input tick'}
    if ($script:entryDispatchSpread -gt .35) { throw "Launch batch took $script:entryDispatchSpread seconds; simultaneous launch evidence is not valid" }
    Wait-Entry 'all real flights land on first wire without injury' 8 {
        $all=$true
        for ($n=0; $n -lt $Players; $n++) {
            $a=Get-EntryActor $n; $s=$script:entryLatest[$n]
            if ($a.flying) { if (!$script:entryFlying.ContainsKey($n)) { $script:entryFlying[$n]=@{time=$s.time;clock=$s.clock;feet=$a.feet} } }
            $landed=$script:entryFlying.ContainsKey($n) -and !$a.flying -and !$a.airborne -and !$a.seated -and $a.pole -lt 0 -and
                [Math]::Abs($a.feet.y-19) -le .2 -and [Math]::Abs($a.feet.z-43.3) -le .7 -and $a.feet.x -le -242 -and $a.feet.x -ge -260.5
            if ($landed) {
                if (!$script:entryLanding.ContainsKey($n)) {
                    $script:entryLanding[$n]=@{actor=$a.id;station=$stationIndices[$n];time=$s.time;clock=$s.clock;feet=$a.feet;state=$a.state;injury=$a.injury}
                    Write-EntryRecord 'first-wire-landed' @{slot=$n; evidence=$script:entryLanding[$n]}
                    Send-EntryCommand $n @{Balance=$true; Seconds=60}
                }
            } else { $all=$false }
        }
        return $all
    }
    $script:entryLandingPassed=$true
    if($ContinueChoices) {
        # Do not manufacture an idle survival test before testing the independent branches.
        # Four real flights/landings have been observed; pursuit remains explicitly untested here.
        # The caller may immediately run play-space-choices.ps1 with the SAME Name/processes.
        $script:entryCompletedScope='four-client launch and landing; immediate choices handoff'
        $script:entryPass=$true
        Write-EntryRecord 'landing-only-handoff' @{launchLandingPassed=$true;pursuitObserved=$false;
            next='Run play-space-choices.ps1 with the same Name; do not launch or restart'}
        return
    }
    # Damage after the verified landing is intended pursuit behavior, not a launch
    # failure. Record it; do not require every runner to stay invulnerable here.
    $script:entryHealthRequired=$false
    Wait-Entry 'original entrance animation finished' 16 { return $script:entryLatest[0].pursuit.entranceComplete }
    # The host deliberately has no attack target and cursor remains zero while NavMesh
    # walks from the entrance to fullRoute.Entry. Following begins only within .65m of
    # that real anchor (EscapePursuerRoute.TryEnter), so intro completion is not readiness.
    $script:entryReadinessStart=$script:entryWatch.Elapsed.TotalSeconds
    Wait-Entry 'physical approach reaches service route and selects a live target' $ServiceReadySeconds {
        $hostState=$script:entryLatest[0]
        $target=[string]$hostState.pursuit.target
        $liveTarget=@($hostState.players | Where-Object { [string]$_.id -eq $target -and $_.state -eq 'Normal' })
        Write-EntryRecord 'service-readiness' @{clock=$hostState.clock;pursuer=$hostState.pursuer;travelledMetres=$script:entryPursuerTravel;
            routeProgress=$hostState.routeProgress;target=$hostState.pursuit.target;action=$hostState.pursuit.action}
        if([double]$hostState.routeProgress -gt $script:entryProgressBeforeLaunch+.05 -and $liveTarget.Count -eq 1 -and $script:entryIds.Values -contains $target) {
            $script:entryServiceReady=@{clock=$hostState.clock;time=$hostState.time;elapsed=$script:entryWatch.Elapsed.TotalSeconds;
                routeProgress=$hostState.routeProgress;target=$hostState.pursuit.target;pursuer=$hostState.pursuer;travelledMetres=$script:entryPursuerTravel}
            return $true
        }
        return $false
    }
    $script:entryStage='eight-second host pursuit observation'
    $script:entryObservationStart=$script:entryWatch.Elapsed.TotalSeconds
    $startClock=[double]$script:entryLatest[0].clock
    $hostLastTime=[double]::NegativeInfinity
    do {
        Update-EntryStates
        $hostState=$script:entryLatest[0]
        if ([double]$hostState.time -gt $hostLastTime) {
            $hostLastTime=[double]$hostState.time
            $script:entryProgress.Add([double]$hostState.routeProgress)
            $target=[string]$hostState.pursuit.target
            $liveTarget=@($hostState.players | Where-Object { [string]$_.id -eq $target -and $_.state -eq 'Normal' })
            $entry=@{clock=$hostState.clock;time=$hostState.time;routeProgress=$hostState.routeProgress;pursuer=$hostState.pursuer;
                target=$hostState.pursuit.target;targetIsLiveFixtureActor=($liveTarget.Count -eq 1 -and $script:entryIds.Values -contains $target);
                action=$hostState.pursuit.action;gate=$hostState.pursuit.gate;attackActor=$hostState.pursuit.attackActor}
            $script:entryTargetSamples.Add($entry); Write-EntryRecord 'host-pursuit' $entry
        }
        if ([double]$hostState.clock-$startClock -ge 8 -and $script:entryWatch.Elapsed.TotalSeconds-$script:entryObservationStart -ge 8) { break }
        Start-Sleep -Milliseconds 100
    } while ($true)
    $script:entryObservationEnd=$script:entryWatch.Elapsed.TotalSeconds
    $maximum=($script:entryProgress | Measure-Object -Maximum).Maximum
    if ($maximum -le $script:entryProgressBeforeLaunch+.05) { throw 'Host route progress did not advance after the entrance launches' }
    if (@($script:entryTargetSamples | Where-Object targetIsLiveFixtureActor).Count -eq 0) { throw 'No live fixture actor was observed as host pursuit target in the eight-second window' }
    if($script:entryPursuerTravel -lt 1) { throw 'No meaningful actual pursuer displacement was observed after launch (less than one metre)' }
    $script:entryPursuitObserved=$true
    $script:entryCompletedScope='launch and landing plus service readiness and eight-second pursuit observation'
    $script:entryPass=$true
    Write-EntryRecord 'fixture-pass' @{scope='First-cannon checkpoint only; no full route completion';players=$Players}
} catch {
    $script:entryFailure=$_.Exception.Message
    Write-EntryRecord 'fixture-failed' @{error=$script:entryFailure;processesKeptRunning=$true}
} finally {
    $survivors=@()
    for($n=0;$n -lt $Players;$n++) {
        $actor=Get-EntryActor $n
        if($actor -and $actor.state -eq 'Normal' -and !$actor.airborne -and !$actor.flying -and !$actor.seated -and !$actor.hanging -and [Math]::Abs($actor.feet.y-19) -le .25) { $survivors+=$n }
    }
    $summary=[ordered]@{name=$Name;players=$Players;checkpointFixture=$true;continuous=$false;fullRouteTested=$false;
        pass=$script:entryPass;failure=$script:entryFailure;elapsed=$script:entryWatch.Elapsed.TotalSeconds;maximumSeconds=$MaxSeconds;
        launchLandingPassed=$script:entryLandingPassed;pursuitObserved=$script:entryPursuitObserved;
        continueChoicesRequested=[bool]$ContinueChoices;completedScope=$script:entryCompletedScope;
        stationIndices=$stationIndices;actorIds=(Convert-EntryIndexMap $script:entryIds);flightEvidence=(Convert-EntryIndexMap $script:entryFlying);landingEvidence=(Convert-EntryIndexMap $script:entryLanding);
        launchDispatchSeconds=$script:entryDispatchSpread;progressBeforeLaunch=$script:entryProgressBeforeLaunch;
        observedProgress=@($script:entryProgress);hostPursuitSamples=$script:entryTargetSamples.Count;
        observationStart=$script:entryObservationStart;observationEnd=$script:entryObservationEnd;
        serviceReadinessMaximumSeconds=$ServiceReadySeconds;serviceReadinessStart=$script:entryReadinessStart;serviceReady=$script:entryServiceReady;
        actualPursuerTravelMetres=$script:entryPursuerTravel;groundedSurvivorSlots=$survivors;allAliveForChoices=($Players -eq 4 -and $survivors.Count -eq 4);
        processesKeptRunning=$true;automaticQuit=$false;automaticRestart=$false;automaticDamage=$false;journal=$journal}
    if (Test-Path -LiteralPath $runFolder) { $summary | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $summaryPath -Encoding utf8 }
    $summary | ConvertTo-Json -Depth 20 -Compress | Write-Output
}
if (!$script:entryPass) { throw "Entry checkpoint failed: $script:entryFailure. Processes are left running; inspect $summaryPath" }
