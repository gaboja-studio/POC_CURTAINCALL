param(
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9][A-Za-z0-9_.-]{0,79}$')][string]$Name,
    [ValidateRange(20,90)][double]$MaxSeconds=90,
    [ValidateRange(28,33)][int]$LastBPole=33,
    [switch]$RequireFullB,
    [string]$HarnessRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../../.harness/local/pursuer-space-20261116'))
)
# Continue the SAME live four-client entry fixture. Never launch, restart, damage or quit.
# One new checkpoint placement per client is explicit setup, not continuous traversal.
# Required: A receiver, C two-pole island, B passenger guard + at least 27 -> 28.
# B continues naturally while A/C are in progress, at most through pole 33 and its island.
# Completion may stop on a later shared B pole; the exact achieved scope is recorded.
$ErrorActionPreference='Stop'
$HarnessRoot=[IO.Path]::GetFullPath($HarnessRoot)
$runFolder=Join-Path $HarnessRoot $Name
$entryPath=Join-Path $runFolder 'entry-fixture-summary.json'
if (!(Test-Path -LiteralPath $entryPath)) { throw 'The existing entry checkpoint summary is required' }
$entry=Get-Content -LiteralPath $entryPath -Raw | ConvertFrom-Json
if (!$entry.pass -or $entry.players -ne 4 -or !$entry.checkpointFixture -or $entry.continuous) { throw 'A passed four-client Checkpoint entry fixture is required' }
if($entry.PSObject.Properties['launchLandingPassed'] -and !$entry.launchLandingPassed) { throw 'Four-client real launch/landing evidence did not pass' }
$landingRows=@($entry.landingEvidence.PSObject.Properties | ForEach-Object Value)
if($landingRows.Count -ne 4 -or @($landingRows | Where-Object { $_.state -ne 'Normal' -or [int]$_.injury -ne 0 -or [Math]::Abs($_.feet.y-19) -gt .2 }).Count -gt 0) {
    throw 'Choices requires four recorded Normal, uninjured first-wire landings; a timer-only or readiness-only entry result is insufficient'
}
if($entry.PSObject.Properties['allAliveForChoices'] -and !$entry.allAliveForChoices) { throw 'Entry pursuit observation ended without four grounded survivors; no setup will run. A new fixture is required, not a restart/revival of these actors' }
$summaryPath=Join-Path $runFolder 'choices-fixture-summary.json'
$journal=Join-Path $runFolder 'choices-fixture.jsonl'
if ((Test-Path -LiteralPath $journal) -or (Test-Path -LiteralPath $summaryPath)) { throw 'Choices fixture already attempted in this session; additional setup is not automatic' }
$coordinates=Get-Content -LiteralPath (Join-Path $HarnessRoot 'fixture-coordinates.json') -Raw | ConvertFrom-Json
. (Join-Path $HarnessRoot 'control.ps1') -Name $Name
$script:choiceWatch=[Diagnostics.Stopwatch]::StartNew()
$script:states=@{}; $script:ids=@{}; $script:restarts=@{}; $script:lastTime=@{}; $script:lastFresh=@{}
$script:lastTap=@{}; $script:tapSnapshot=@{}; $script:lastInput=@{}
$script:completedVictims=@{}
$script:phase='preconditions'; $script:healthy=$false; $script:success=$false; $script:failure=$null
$script:evidence=[Collections.Generic.List[object]]::new()
$script:targets=[Collections.Generic.List[object]]::new()
$script:a=@{phase='unstarted';done=$false;seenSeat=$false;seenFlight=$false;seenMovingPole=$false;seenUnloading=$false;began=0}
$script:c=@{phase='unstarted';done=$false;seenFlight=$false;at=0;releaseAt=0;braked=$false}
$script:b=@{phase='unstarted';current=27;next=28;top=-1;bottom=-1;at=0;transitions=0;guard=$false;guardCommand=-1;guardSnapshot=0;full=$false;lowerLanded=$false;releaseAt=0;braked=$false}
function Convert-ChoiceIndexMap([Collections.IDictionary]$Map) {
    $safe=[ordered]@{}
    foreach($key in $Map.Keys) { $safe[[string]$key]=$Map[$key] }
    return $safe
}
function Record-Choice([string]$Kind,$Value) {
    $row=[ordered]@{utc=[DateTime]::UtcNow.ToString('o');elapsed=$script:choiceWatch.Elapsed.TotalSeconds;stage=$script:phase;
        kind=$Kind;checkpointFixture=$true;continuous=$false;value=$Value}
    [IO.File]::AppendAllText($journal,($row|ConvertTo-Json -Depth 22 -Compress)+[Environment]::NewLine)
}
function Budget-Choice {
    if ($script:choiceWatch.Elapsed.TotalSeconds -ge $MaxSeconds) { throw "Choices fixture exceeded $MaxSeconds seconds; processes remain running" }
}
function Actor-Choice([int]$Number) {
    $local=@($script:states[$Number].players|Where-Object local)
    if ($local.Count -eq 1) { return $local[0] }; return $null
}
function Read-Choices {
    for($n=0;$n -lt 4;$n++) {
        Budget-Choice
        $s=Sample $n
        if(!$s) { continue }
        $script:states[$n]=$s
        if(!$script:lastTime.ContainsKey($n) -or [double]$s.time -gt $script:lastTime[$n]) {
            $script:lastTime[$n]=[double]$s.time; $script:lastFresh[$n]=$script:choiceWatch.Elapsed.TotalSeconds
            Record-Choice 'snapshot' @{slot=$n;snapshot=$s}
            if($n -eq 0) { $script:targets.Add(@{clock=$s.clock;progress=$s.routeProgress;pursuer=$s.pursuer;pursuit=$s.pursuit}) }
        } elseif($script:healthy -and $script:choiceWatch.Elapsed.TotalSeconds-$script:lastFresh[$n] -gt 2.5) { throw "Slot $n stale snapshot" }
        if($s.continuous) { throw "Slot $n is continuous; checkpoint setup forbidden" }
        if($s.error) { throw "Slot $n review error: $($s.error)" }
        if($script:healthy) {
            $person=Actor-Choice $n
            if(!$person -or [string]$person.id -ne $script:ids[$n]) { throw "Slot $n actor changed or disappeared" }
            if([int]$s.restarts -ne $script:restarts[$n]) { throw "Slot $n restarted; first-wire event continuity is lost" }
            if($person.state -ne 'Normal') {
                $finished=($n -eq 0 -and $script:a.done) -or ($n -eq 1 -and $script:c.done)
                $captured=$person.state -eq 'EscapeCaptured' -or $person.death -eq 'Pursuit' -or $script:completedVictims.ContainsKey($n)
                if(!$finished -or !$captured){throw "Slot $n became $($person.state), death=$($person.death); this is checkpoint evidence, not a full-route diagnosis"}
                if(!$script:completedVictims.ContainsKey($n)){
                    $script:completedVictims[$n]=@{clock=$s.clock;actor=$person;routeAlreadyCompleted=$true}
                    Record-Choice 'completed-runner-captured-others-continue' @{slot=$n;actor=$person;clock=$s.clock}
                }
            }
        }
    }
}
function Send-Choice([int]$Number,[hashtable]$Arguments) {
    Budget-Choice
    Send-Review -Slot $Number @Arguments
    $cmd=Get-Content -LiteralPath (Join-Path $runFolder "command-$Number.json") -Raw | ConvertFrom-Json
    $script:lastInput[$Number]=$script:choiceWatch.Elapsed.TotalSeconds
    Record-Choice 'command-published' @{slot=$Number;command=$cmd}
    return [int]$cmd.id
}
function Set-CPhase([string]$Next) { $script:c.phase=$Next;$script:c.at=$script:choiceWatch.Elapsed.TotalSeconds;Record-Choice 'C-stage' $Next }
function Set-BPhase([string]$Next) { $script:b.phase=$Next;$script:b.at=$script:choiceWatch.Elapsed.TotalSeconds;Record-Choice 'B-stage' $Next }
function Require-Time([double]$Started,[double]$Limit,[string]$Label) {
    if($script:choiceWatch.Elapsed.TotalSeconds-$Started -gt $Limit) { throw "$Label exceeded $Limit seconds" }
}
function Pole-Choice([int]$Index) {
    $found=@($script:states[0].poles|Where-Object index -eq $Index)
    if($found.Count -ne 1) { throw "Missing observed pole $Index" }
    return $found[0]
}
function Catch-Choice([int]$Number,[int]$Index) {
    $person=Actor-Choice $Number; $s=$script:states[$Number]
    if($person.pole -eq $Index) { return $true }
    if($person.pole -ge 0) { throw "Slot $Number caught pole $($person.pole) instead of $Index" }
    if($person.below -ge 0 -or $person.above -ge 0) { throw "Slot $Number entered piggyback while this fixture intended pole $Index; input-target conflict, not proof that the pole is unreachable" }
    if($person.grabbable -eq $Index -and (!$script:lastTap.ContainsKey($Number) -or $script:choiceWatch.Elapsed.TotalSeconds-$script:lastTap[$Number] -ge .3) -and
        (!$script:tapSnapshot.ContainsKey($Number) -or [double]$s.time -gt $script:tapSnapshot[$Number])) {
        $null=Send-Choice $Number @{Tap=$true;Seconds=.2}
        $script:lastTap[$Number]=$script:choiceWatch.Elapsed.TotalSeconds;$script:tapSnapshot[$Number]=[double]$s.time
    }
    return $false
}
function Pump-Choice([int]$Number) {
    if(!$script:lastInput.ContainsKey($Number) -or $script:choiceWatch.Elapsed.TotalSeconds-$script:lastInput[$Number] -ge 2) {
        $null=Send-Choice $Number @{Move=1;Seconds=3}
    }
}
function Release-Choice([int]$Number,[int]$Index,[double]$TargetX,[double]$TargetZ) {
    $person=Actor-Choice $Number
    if($person.pole -ne $Index) { throw "Slot $Number is no longer on release pole $Index" }
    $hostState=$script:states[0]
    $hostPerson=@($hostState.players|Where-Object {[string]$_.id -eq [string]$person.id})
    if($hostPerson.Count -ne 1 -or $hostPerson[0].state -ne 'Normal' -or $hostPerson[0].pole -ne $Index) { return $false }
    $hostPerson=$hostPerson[0]
    if($script:choiceWatch.Elapsed.TotalSeconds-$script:lastFresh[0] -gt .55 -or [Math]::Abs([double]$hostState.clock-[double]$script:states[$Number].clock) -gt .55) { return $false }
    # A remote +13deg/s sample was already negative on the host in 4-06. Its
    # delayed release fell to ~41deg and left only one observed catch window.
    $bTransfer=$Index -ge 27 -and $Index -le 33
    $minimumAngle=if($bTransfer){60}else{45}
    $minimumSpeed=if($bTransfer){35}else{0}
    if($person.poleAngle -lt $minimumAngle -or $hostPerson.poleAngle -lt $minimumAngle -or
        $person.poleSpeed -le $minimumSpeed -or $hostPerson.poleSpeed -le $minimumSpeed) { return $false }
    $v=$person.poleRelease;$horizontal=[Math]::Sqrt($v.x*$v.x+$v.z*$v.z)
    $toward=$v.x*($TargetX-$person.feet.x)+$v.z*($TargetZ-$person.feet.z)
    if($person.poleAngle -lt 45 -or $person.poleSpeed -le 0 -or $horizontal -lt 5.9 -or $horizontal -gt 6.05 -or [Math]::Abs($v.y-6) -gt .15 -or $toward -le 0) { return $false }
    $yaw=[Math]::Atan2($TargetX-$person.feet.x,$TargetZ-$person.feet.z)*180/[Math]::PI
    Record-Choice 'release-window' @{slot=$Number;pole=$Index;clock=$script:states[$Number].clock;angle=$person.poleAngle;speed=$person.poleSpeed;velocity=$v;horizontal=$horizontal;target=@($TargetX,$TargetZ);hostClock=$hostState.clock;hostTime=$hostState.time;hostAngle=$hostPerson.poleAngle;hostSpeed=$hostPerson.poleSpeed;hostActor=$hostPerson.id;hostSnapshotAge=($script:choiceWatch.Elapsed.TotalSeconds-$script:lastFresh[0]);minimumAngle=$minimumAngle;minimumSpeed=$minimumSpeed}
    $null=Send-Choice $Number @{Action='look';Yaw=[float]$yaw;Pitch=8;Move=1;Jump=$true;Seconds=.6}
    return $true
}
function Landed-Choice($Person,[double]$Y,[double]$MinX,[double]$MaxX,[double]$MinZ,[double]$MaxZ) {
    return $Person -and $Person.state -eq 'Normal' -and !$Person.airborne -and !$Person.flying -and !$Person.seated -and $Person.pole -lt 0 -and
        [Math]::Abs($Person.feet.y-$Y) -lt .22 -and $Person.feet.x -ge $MinX -and $Person.feet.x -le $MaxX -and $Person.feet.z -ge $MinZ -and $Person.feet.z -le $MaxZ
}
function Get-BPair([int]$Index) {
    $left=Actor-Choice 2;$right=Actor-Choice 3
    if($left.pole -ne $Index -or $right.pole -ne $Index) { return $null }
    $server=@($script:states[0].players|Where-Object {([string]$_.id -eq $script:ids[2] -or [string]$_.id -eq $script:ids[3]) -and $_.pole -eq $Index}|Sort-Object {$_.feet.y} -Descending)
    if($server.Count -ne 2) { return $null }
    $gap=[double]$server[0].feet.y-[double]$server[1].feet.y
    if($gap -lt .6 -or $gap -gt 2.4) { return $null }
    $top=if([string]$server[0].id -eq $script:ids[2]){2}else{3};$bottom=if($top -eq 2){3}else{2}
    if((Actor-Choice $top).feet.y -le (Actor-Choice $bottom).feet.y) { return $null }
    return @{top=$top;bottom=$bottom;hostFeetGap=$gap;pole=$Index;note='Observed common pole seat height; below/above are piggyback fields, not pole seat indices'}
}
function Start-BTransfer {
    $pair=Get-BPair $script:b.current
    if(!$pair) { return }
    $script:b.top=$pair.top;$script:b.bottom=$pair.bottom;Record-Choice 'B-seat-order' $pair
    $null=Send-Choice $script:b.top @{Move=1;Seconds=3}
    if($script:b.current -lt $LastBPole) { $script:b.next=$script:b.current+1;Set-BPhase 'pump lower to next pole' }
    elseif($script:b.current -eq 33) { $script:b.braked=$false;Set-BPhase 'pump lower to island' }
    else { Set-BPhase 'scope reached holding pair' }
}
function Step-A {
    if($script:a.done) { return }
    Require-Time $script:a.began 48 'A conveyor input'
    $person=Actor-Choice 0
    if($person.seated){$script:a.seenSeat=$true};if($person.flying){$script:a.seenFlight=$true}
    if($person.pole -ge 3 -and $person.pole -le 8){$script:a.seenMovingPole=$true}
    if($person.pilot.unloading){$script:a.seenUnloading=$true}
    if($script:a.seenMovingPole -and (Landed-Choice $person 5.8 -244.3 -232.2 180 187.4)) {
        $script:a.done=$true;$script:a.phase='receiver landed';$null=Send-Choice 0 @{Seconds=.1}
        Record-Choice 'A-complete' @{clock=$script:states[0].clock;actor=$person;observations=$script:a}
    }
}
function Step-C {
    if($script:c.done){return}
    $person=Actor-Choice 1
    switch($script:c.phase) {
        'board cannon 6' {
            Require-Time $script:c.at 6 'C cannon boarding'
            if($person.seated) { $null=Send-Choice 1 @{Tap=$true;Seconds=.2};Set-CPhase 'cannon flight';break }
            if($person.airborne -or $person.flying){throw 'C flew before seated was observed'}
            if($person.boardable -eq 6 -and (!$script:lastTap.ContainsKey(1) -or $script:choiceWatch.Elapsed.TotalSeconds-$script:lastTap[1] -ge .75)) {
                $null=Send-Choice 1 @{Tap=$true;Seconds=.2};$script:lastTap[1]=$script:choiceWatch.Elapsed.TotalSeconds
            }
        }
        'cannon flight' {
            Require-Time $script:c.at 8 'C receiving balcony'
            if($person.flying){$script:c.seenFlight=$true}
            if($script:c.seenFlight -and (Landed-Choice $person 5.8 -281.2 -272.8 157.1 165.5)) {
                Record-Choice 'C-cannon-landed' $person
                $null=Send-Choice 1 @{Action='walk';X=-273.35;Z=164.95;Seconds=4};Set-CPhase 'walk to first pole'
            }
        }
        'walk to first pole' {
            Require-Time $script:c.at 5 'C first pole approach'
            if([Math]::Abs($person.feet.x+273.35) -lt .32 -and [Math]::Abs($person.feet.z-164.95) -lt .32 -and !$person.airborne) { Set-CPhase 'catch 34' }
        }
        'catch 34' { Require-Time $script:c.at 4 'C catch 34';if(Catch-Choice 1 34){$null=Send-Choice 1 @{Move=1;Seconds=3};Set-CPhase 'pump 34'} }
        'pump 34' {
            Require-Time $script:c.at 10 'C release 34';Pump-Choice 1;$next=Pole-Choice 35
            if(Release-Choice 1 34 $next.grip.x $next.grip.z){Set-CPhase 'catch 35'}
        }
        'catch 35' { Require-Time $script:c.at 4 'C catch 35';if($person.pole -eq 34){break};if(Catch-Choice 1 35){$null=Send-Choice 1 @{Move=1;Seconds=3};Set-CPhase 'pump 35'} }
        'pump 35' {
            Require-Time $script:c.at 10 'C release 35';Pump-Choice 1
            if(Release-Choice 1 35 -262 178.8){$script:c.releaseAt=$script:choiceWatch.Elapsed.TotalSeconds;$script:c.braked=$false;Set-CPhase 'island landing'}
        }
        'island landing' {
            Require-Time $script:c.at 5 'C island landing'
            if(!$script:c.braked -and $script:choiceWatch.Elapsed.TotalSeconds-$script:c.releaseAt -ge .6 -and $person.pole -lt 0) {
                $null=Send-Choice 1 @{Move=-1;Seconds=1.4};$script:c.braked=$true
            }
            if(Landed-Choice $person 5.8 -264.75 -259.25 174.8 182.8) {
                $script:c.done=$true;$null=Send-Choice 1 @{Seconds=.1};Record-Choice 'C-complete' @{clock=$script:states[1].clock;actor=$person}
            }
        }
    }
}
function Step-B {
    if($script:b.full){return}
    switch($script:b.phase) {
        'catch first rider' {
            Require-Time $script:b.at 5 'B first rider'
            if(Catch-Choice 2 27) {
                # F selects the nearest common interaction. Keep the second grounded actor
                # behind the departure lip until the first has actually entered the pole;
                # an actor using a pole is then excluded from piggyback mount candidates.
                Record-Choice 'B-first-rider-held-before-second-approach' @{first=(Actor-Choice 2);waiting=(Actor-Choice 3)}
                $null=Send-Choice 3 @{Action='walk';X=-279.7;Z=127.7;Seconds=3}
                Set-BPhase 'second rider walking to pole'
            }
        }
        'second rider walking to pole' {
            Require-Time $script:b.at 4 'B second rider walking approach'
            if((Actor-Choice 2).pole -ne 27){throw 'B first rider left pole 27 before the second walking approach finished'}
            $person=Actor-Choice 3
            if($person.below -ge 0 -or $person.above -ge 0){throw 'B second rider entered piggyback during the ordinary walking approach'}
            if(!$person.airborne -and !$person.flying -and [Math]::Abs($person.feet.x+279.7) -lt .32 -and [Math]::Abs($person.feet.z-127.7) -lt .32 -and $person.grabbable -eq 27){Set-BPhase 'catch second rider'}
        }
        'catch second rider' {
            Require-Time $script:b.at 5 'B second rider'
            if(Catch-Choice 3 27){$pair=Get-BPair 27;if($pair){$script:b.top=$pair.top;$script:b.bottom=$pair.bottom;Record-Choice 'B-seat-order' $pair;
                $script:b.guardSnapshot=[double]$script:states[$script:b.top].time
                $script:b.guardCommand=Send-Choice $script:b.top @{Jump=$true;Seconds=.2};Set-BPhase 'verify passenger release guard'}}
        }
        'verify passenger release guard' {
            Require-Time $script:b.at 3 'B release guard'
            $top=Actor-Choice $script:b.top;$bottom=Actor-Choice $script:b.bottom
            if($top.pole -ne 27 -or $bottom.pole -ne 27){throw 'B upper Jump released a rider while the passenger was still on pole 27'}
            $s=$script:states[$script:b.top]
            if($s.command -ge $script:b.guardCommand -and [double]$s.time-$script:b.guardSnapshot -ge .55 -and (Get-BPair 27)) {
                $script:b.guard=$true;Record-Choice 'B-upper-release-rejected' @{slot=$script:b.top;command=$script:b.guardCommand;host=$script:states[0].players;local=$top}
                Start-BTransfer
            }
        }
        'pump lower to next pole' {
            Require-Time $script:b.at 10 'B lower release window';Pump-Choice $script:b.top;$next=Pole-Choice $script:b.next
            if(Release-Choice $script:b.bottom $script:b.current $next.grip.x $next.grip.z){Set-BPhase 'lower catching next pole'}
        }
        'lower catching next pole' {
            Require-Time $script:b.at 4 'B lower next catch';$person=Actor-Choice $script:b.bottom
            if($person.pole -eq $script:b.current){break}
            if(Catch-Choice $script:b.bottom $script:b.next){Set-BPhase 'pump upper to next pole'}
        }
        'pump upper to next pole' {
            Require-Time $script:b.at 10 'B upper release window';Pump-Choice $script:b.top;$next=Pole-Choice $script:b.next
            if(Release-Choice $script:b.top $script:b.current $next.grip.x $next.grip.z){Set-BPhase 'upper catching next pole'}
        }
        'upper catching next pole' {
            Require-Time $script:b.at 4 'B upper next catch';$person=Actor-Choice $script:b.top
            if($person.pole -eq $script:b.current){break}
            if(Catch-Choice $script:b.top $script:b.next){$pair=Get-BPair $script:b.next;if($pair){
                $script:b.current=$script:b.next;$script:b.transitions++;Record-Choice 'B-pair-transition' @{transitions=$script:b.transitions;pair=$pair;clock=$script:states[0].clock}
                Start-BTransfer
            }}
        }
        'pump lower to island' {
            Require-Time $script:b.at 10 'B lower final release';Pump-Choice $script:b.top
            if(Release-Choice $script:b.bottom 33 -252 178.8){$script:b.releaseAt=$script:choiceWatch.Elapsed.TotalSeconds;$script:b.braked=$false;Set-BPhase 'lower island landing'}
        }
        'lower island landing' {
            Require-Time $script:b.at 5 'B lower island';$person=Actor-Choice $script:b.bottom
            if(!$script:b.braked -and $script:choiceWatch.Elapsed.TotalSeconds-$script:b.releaseAt -ge .6 -and $person.pole -lt 0){$null=Send-Choice $script:b.bottom @{Move=-1;Seconds=1.4};$script:b.braked=$true}
            if(Landed-Choice $person 5.8 -255 -249 174.8 182.8){$script:b.lowerLanded=$true;$null=Send-Choice $script:b.bottom @{Seconds=.1};Set-BPhase 'pump upper to island'}
        }
        'pump upper to island' {
            Require-Time $script:b.at 10 'B upper final release';Pump-Choice $script:b.top
            if(Release-Choice $script:b.top 33 -252 178.8){$script:b.releaseAt=$script:choiceWatch.Elapsed.TotalSeconds;$script:b.braked=$false;Set-BPhase 'upper island landing'}
        }
        'upper island landing' {
            Require-Time $script:b.at 5 'B upper island';$person=Actor-Choice $script:b.top
            if(!$script:b.braked -and $script:choiceWatch.Elapsed.TotalSeconds-$script:b.releaseAt -ge .6 -and $person.pole -lt 0){$null=Send-Choice $script:b.top @{Move=-1;Seconds=1.4};$script:b.braked=$true}
            if(Landed-Choice $person 5.8 -255 -249 174.8 182.8){$script:b.full=$true;$null=Send-Choice $script:b.top @{Seconds=.1};Record-Choice 'B-seven-pole-route-complete' @{top=$person;bottom=(Actor-Choice $script:b.bottom)}}
        }
        'scope reached holding pair' { Require-Time $script:b.at 18 'B holding at explicit scope cap' }
    }
}

try {
    Record-Choice 'scope' @{entrySummary=$entryPath;entryScope=$entry.completedScope;entryPursuitObserved=$entry.pursuitObserved;
        continuedSameProcesses=$true;setupPerClient=1;fullRouteTraversal=$false;roles=@('slot0 A common conveyor guide','slot1 C station6 and poles34/35','slots2/3 B pair27 onward');maximumSeconds=$MaxSeconds;optionalLastBPole=$LastBPole}
    do {
        Read-Choices
        $ready=$script:states.Count -eq 4
        for($n=0;$n -lt 4;$n++){$person=Actor-Choice $n;$s=$script:states[$n];if(!$person -or !$s.ready -or $s.blocked -or $s.campaign -ne 'Escape' -or @($s.players).Count -ne 4){$ready=$false}}
        if($ready){break};Require-Time 0 5 'existing four-client readiness';Start-Sleep -Milliseconds 100
    }while($true)
    for($n=0;$n -lt 4;$n++) {
        $person=Actor-Choice $n
        if($person.state -ne 'Normal' -or $person.flying -or $person.seated -or [Math]::Abs($person.feet.y-19) -gt .25) { throw "Slot $n is no longer a living, grounded first-wire entry survivor; use a new fixture rather than reviving/resetting this session" }
        Record-Choice 'initial-survivor' @{slot=$n;actor=$person;note='Existing injury is observed, never healed or changed by this fixture'}
        $script:ids[$n]=[string]$person.id;$script:restarts[$n]=[int]$script:states[$n].restarts
        $prior=$entry.actorIds.PSObject.Properties[[string]$n].Value
        if([string]$prior -ne $script:ids[$n]) { throw "Slot $n is not the actor from the passed entry run" }
    }
    $script:healthy=$true
    foreach($index in 27..35){$pole=Pole-Choice $index;if($index -le 33 -and $pole.name -notlike 'Labyrinth lower inspection swing *'){throw "Unexpected B registry entry $index"};if($index -ge 34 -and $pole.name -notlike 'Labyrinth processing swing *'){throw "Unexpected C registry entry $index"}}
    $script:phase='one checkpoint setup per client'
    for($n=0;$n -lt 2;$n++) {
        $index=if($n -eq 0){2}else{6};$station=@($coordinates.stations|Where-Object index -eq $index)[0]
        $p=if($station.PSObject.Properties['worldBoard']){@($station.worldBoard)}else{@($station.board)}
        $yaw=[Math]::Atan2($station.seat[0]-$p[0],$station.seat[2]-$p[2])*180/[Math]::PI
        $null=Send-Choice $n @{Action='setup';X=[float]$p[0];Y=([float]$p[1]+.08);Z=[float]$p[2];Yaw=[float]$yaw;Pitch=8;Seconds=.1}
    }
    $null=Send-Choice 2 @{Action='setup';X=-280.3;Y=1.08;Z=127.7;Yaw=26.565;Pitch=8;Seconds=.1}
    $null=Send-Choice 3 @{Action='setup';X=-279.7;Y=1.08;Z=123.8;Yaw=26.565;Pitch=8;Seconds=.1}
    $placedAt=$script:choiceWatch.Elapsed.TotalSeconds
    do {
        Read-Choices
        $p0=Actor-Choice 0;$p1=Actor-Choice 1;$p2=Actor-Choice 2;$p3=Actor-Choice 3
        $waitingSafely=[Math]::Abs($p3.feet.x+279.7) -lt .32 -and [Math]::Abs($p3.feet.z-123.8) -lt .32 -and [Math]::Abs($p3.feet.y-1.08) -lt .2
        $separation=[Math]::Sqrt([Math]::Pow($p2.feet.x-$p3.feet.x,2)+[Math]::Pow($p2.feet.z-$p3.feet.z,2))
        if($p0.boardable -eq 2 -and $p1.boardable -eq 6 -and $p2.grabbable -eq 27 -and $waitingSafely -and $separation -gt 3.5 -and !$p0.airborne -and !$p1.airborne -and !$p2.airborne -and !$p3.airborne){break}
        Require-Time $placedAt 5 'checkpoint placement/expected devices';Start-Sleep -Milliseconds 100
    }while($true)
    $script:phase='parallel A C B ordinary inputs'
    $script:a.phase='common conveyor';$script:a.began=$script:choiceWatch.Elapsed.TotalSeconds
    $null=Send-Choice 0 @{Action='conveyor';Seconds=46}
    Set-CPhase 'board cannon 6';Set-BPhase 'catch first rider'
    do {
        Read-Choices;Step-A;Step-C;Step-B
        $paired=$script:b.transitions -ge 1 -and $null -ne (Get-BPair $script:b.current)
        if($script:a.done -and $script:c.done -and $script:b.guard -and ($script:b.full -or (!$RequireFullB -and $paired))) {
            # Stop pumping at a stable observed pair. No forced drop or reset is used to end this fixture.
            if(!$script:b.full){foreach($n in @(2,3)){$null=Send-Choice $n @{Seconds=.1}}}
            $script:success=$true;break
        }
        Start-Sleep -Milliseconds 65
    }while($true)
    Record-Choice 'fixture-pass' @{A=$script:a;C=$script:c;B=$script:b;note='Only the achieved checkpoint branches are validated; untraversed B poles and entry-to-ending multiplayer remain untested'}
}catch{
    $script:failure=$_.Exception.Message
    Record-Choice 'fixture-failed' @{error=$script:failure;A=$script:a;C=$script:c;B=$script:b;processesKeptRunning=$true}
}finally{
    $summary=[ordered]@{name=$Name;checkpointFixture=$true;continuous=$false;entrySummary=$entryPath;continuedSameProcesses=$true;
        entryScope=$entry.completedScope;entryPursuitObserved=$entry.pursuitObserved;
        pass=$script:success;failure=$script:failure;elapsed=$script:choiceWatch.Elapsed.TotalSeconds;maximumSeconds=$MaxSeconds;
        actors=(Convert-ChoiceIndexMap $script:ids);A=$script:a;C=$script:c;B=$script:b;hostPursuit=$script:targets;
        completedRunnersCaptured=(Convert-ChoiceIndexMap $script:completedVictims);finalActors=@(0..3|ForEach-Object{Actor-Choice $_});
        fullRouteTested=$false;requiredFullB=[bool]$RequireFullB;allSevenBPolesTraversed=$script:b.full;automaticRestart=$false;automaticDamage=$false;automaticQuit=$false;processesKeptRunning=$true;journal=$journal}
    $summary|ConvertTo-Json -Depth 22|Set-Content -LiteralPath $summaryPath -Encoding utf8
    $summary|ConvertTo-Json -Depth 22 -Compress|Write-Output
}
if(!$script:success){throw "Choices checkpoint failed: $script:failure. Clients remain running; inspect $summaryPath"}
