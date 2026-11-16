param(
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9][A-Za-z0-9_.-]{0,79}$')][string]$Name,
    [ValidateRange(20,90)][double]$MaxSeconds=90,
    [switch]$FromEntryCheckpoint,
    [string]$HarnessRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../../.harness/local/pursuer-space-20261116'))
)
# Same live checkpoint session after merge, or explicitly four placements after entry.
# No revive/restart/damage/launch/quit; this is not an entrance-to-ending run.
$ErrorActionPreference='Stop'
$run=Join-Path $HarnessRoot $Name
$priorPath=Join-Path $run $(if($FromEntryCheckpoint){'entry-fixture-summary.json'}else{'merge-fixture-summary.json'})
$prior=Get-Content -LiteralPath $priorPath -Raw|ConvertFrom-Json
if($FromEntryCheckpoint){
    if(!$prior.pass -or !$prior.launchLandingPassed -or $prior.players -ne 4 -or !$prior.checkpointFixture -or $prior.continuous){throw 'Actual passed four-player entry launch/landing evidence required'}
    $landingRows=@($prior.landingEvidence.PSObject.Properties|ForEach-Object Value)
    if($landingRows.Count -ne 4 -or @($landingRows|Where-Object {$_.state -ne 'Normal' -or $_.injury -ne 0 -or [Math]::Abs($_.feet.y-19) -gt .2}).Count){throw 'Four recorded healthy first-wire landings required'}
}elseif(!$prior.pass -or !$prior.continuedSameSession -or $prior.addedSetup){throw 'Passed same-session merge evidence required'}
$journal=Join-Path $run 'return-fixture.jsonl';$output=Join-Path $run 'return-fixture-summary.json'
if((Test-Path $journal) -or (Test-Path $output)){throw 'Return fixture already attempted; results must be preserved'}
$coords=Get-Content -LiteralPath (Join-Path $HarnessRoot 'fixture-coordinates.json') -Raw|ConvertFrom-Json
if([Math]::Abs($coords.frame[0]+200) -gt .001 -or [Math]::Abs($coords.frame[1]-25) -gt .001 -or [Math]::Abs($coords.frame[2]-118.3) -gt .001 -or [Math]::Abs($coords.rotation[1]-180) -gt .001){throw 'World/frame mapping changed'}
. (Join-Path $HarnessRoot 'control.ps1') -Name $Name
$watch=[Diagnostics.Stopwatch]::StartNew();$actors=@();$fatal=$null;$continuity=$false;$parkingReady=$false;$setupRequestedCount=0
function Note([string]$Kind,$Data){[IO.File]::AppendAllText($journal,(@{utc=[DateTime]::UtcNow.ToString('o');elapsed=$watch.Elapsed.TotalSeconds;kind=$Kind;value=$Data}|ConvertTo-Json -Depth 20 -Compress)+[Environment]::NewLine)}
function Send([int]$Slot,[hashtable]$Arguments){Send-Review -Slot $Slot @Arguments;Note $(if($Arguments.Action -eq 'setup'){'explicit-checkpoint-input'}else{'ordinary-input'}) @{slot=$Slot;command=(Get-Content (Join-Path $run "command-$Slot.json") -Raw|ConvertFrom-Json)}}
function SetupCount([int]$Slot){$f=Join-Path $run "actions-$Slot.jsonl";if(!(Test-Path $f)){return 0};return @(Get-Content $f|ForEach-Object{if($_){try{$_|ConvertFrom-Json}catch{}}}|Where-Object action -eq 'setup').Count}
function Way([string]$Label,[double]$X,[double]$Z,[bool]$Balance=$false,[double]$Seconds=8){@{kind='walk';label=$Label;x=-200-$X;z=118.3-$Z;balance=$Balance;seconds=$Seconds}}
function Grounded($p){$p.state -eq 'Normal' -and !$p.airborne -and !$p.flying -and !$p.seated -and $p.pole -lt 0 -and [Math]::Abs($p.feet.y-6.5) -lt .23}
function At($p,$step){(Grounded $p) -and [Math]::Abs($p.feet.x-$step.x) -lt .26 -and [Math]::Abs($p.feet.z-$step.z) -lt .26}
function CompleteStep($a,$p,$s){Note 'actual-stage-complete' @{slot=$a.slot;route=$a.route;label=$a.steps[$a.index].label;clock=$s.clock;actor=$p};$a.achieved+=,$a.steps[$a.index].label;$a.index++;$a.mode='waiting';if($a.index -eq $a.steps.Count){$a.outcome='gate-approach-reached';Send $a.slot @{Seconds=.05}}}
function FailActor($a,[string]$Reason){$a.outcome='failed';$a.reason=$Reason;Note 'route-or-pilot-failure' @{slot=$a.slot;route=$a.route;reason=$Reason;actor=$a.last};Send $a.slot @{Seconds=.05}}
try{
    $initialSlots=if($FromEntryCheckpoint){@(0,1,2,3)}else{@($prior.actors|ForEach-Object {[int]$_.slot})}
    $processRows=Get-Content -LiteralPath (Join-Path $run 'processes.json') -Raw|ConvertFrom-Json
    foreach($slot in $initialSlots){
        $old=if(!$FromEntryCheckpoint){$prior.actors|Where-Object slot -eq $slot|Select-Object -First 1}else{$null}
        $s=Sample $slot;$p=@($s.players|Where-Object local)
        $expectedId=if($FromEntryCheckpoint){$prior.actorIds.PSObject.Properties[[string]$slot].Value}else{$old.actor}
        $expectedRestarts=if($FromEntryCheckpoint){0}else{[int]$old.restarts}
        $earliestClock=if($FromEntryCheckpoint){$prior.landingEvidence.PSObject.Properties[[string]$slot].Value.clock}else{$old.clock}
        $processRow=@($processRows|Where-Object slot -eq $slot)
        if(!$s -or $p.Count -ne 1 -or [string]$p[0].id -ne [string]$expectedId -or [int]$s.restarts -ne $expectedRestarts -or $s.continuous -or [double]$s.clock -lt [double]$earliestClock -or $processRow.Count -ne 1 -or [int]$s.process -ne [int]$processRow[0].pid){throw "Slot $slot session continuity mismatch"}
        $p=$p[0]
        $a=@{slot=$slot;id=[string]$p.id;route='unavailable';restarts=[int]$s.restarts;process=[int]$s.process;setupBefore=(SetupCount $slot);setupAllowed=$(if($FromEntryCheckpoint){1}else{0});setupVerified=$false;last=$p;snapshot=$s;lastTime=[double]$s.time;freshAt=0.0;outcome='unavailable';reason=$null;steps=@();achieved=@();index=0;mode='waiting';started=0.0;stepAt=0.0;lastInput=-10.0;lastTap=-10.0;tapTime=-1.0;seenFlight=$false;braked=$false}
        if($FromEntryCheckpoint -and ($p.state -ne 'Normal' -or $p.airborne -or $p.flying -or $p.seated -or $p.hanging -or $p.pole -ge 0 -or [Math]::Abs($p.feet.y-19) -gt .2 -or [Math]::Abs($p.feet.z-43.3) -gt .5 -or $p.feet.x -lt -257 -or $p.feet.x -gt -243)){throw "Slot $slot is no longer a Normal grounded first-wire survivor; no return placement permitted"}
        if($p.state -eq 'Normal'){
            if(!$FromEntryCheckpoint -and ($old.outcome -ne 'reached-new-return-start' -or !(Grounded $p) -or [Math]::Abs($p.feet.x+256) -gt 1.7 -or [Math]::Abs($p.feet.z-192.3) -gt 1.7)){throw "Slot $slot has not actually reached the Return start"}
            $a.outcome='running'
        }else{$a.reason="pre-existing state=$($p.state), death=$($p.death)"}
        $actors+=,$a;Note 'start-state' @{slot=$slot;snapshot=$s;setupCount=$a.setupBefore}
    }
    if($FromEntryCheckpoint){
        # All four original actors were verified before publishing any placement.
        # Their four positions are inside the authored 4x4 floor, with separate capsules.
        $places=@(@{x=57.2;z=-75.1},@{x=57.2;z=-72.9},@{x=54.5;z=-75.4},@{x=54.5;z=-72.6})
        foreach($a in $actors){
            $place=$places[$a.slot];$a.setupTarget=@{x=-200-$place.x;z=118.3-$place.z}
            Send $a.slot @{Action='setup';X=$a.setupTarget.x;Y=6.58;Z=$a.setupTarget.z;Yaw=-90;Pitch=8;Seconds=.1}
            $a.setupCommand=[int](Get-Content -LiteralPath (Join-Path $run "command-$($a.slot).json") -Raw|ConvertFrom-Json).id
            $setupRequestedCount++;Note 'explicit-return-checkpoint-placement' @{slot=$a.slot;actor=$a.id;command=$a.setupCommand;target=$a.setupTarget;intermediateTraversalTested=$false}
        }
        $placementStart=$watch.Elapsed.TotalSeconds
        while(@($actors|Where-Object {!$_.setupVerified}).Count){
            if($watch.Elapsed.TotalSeconds -ge $MaxSeconds -or $watch.Elapsed.TotalSeconds-$placementStart -gt 6){throw 'Return checkpoint placement did not receive four actual grounded acknowledgements'}
            foreach($a in $actors){
                $s=Sample $a.slot;if(!$s){continue};$p=@($s.players|Where-Object local)
                if($p.Count -ne 1 -or [string]$p[0].id -ne $a.id -or [int]$s.restarts -ne $a.restarts -or [int]$s.process -ne $a.process -or $s.continuous){$continuity=$false;throw 'Checkpoint placement actor/process/restart changed'}
                $p=$p[0];$a.last=$p;$a.snapshot=$s;$a.lastTime=[double]$s.time;$a.freshAt=$watch.Elapsed.TotalSeconds
                if($p.state -ne 'Normal' -or $p.death){throw "Slot $($a.slot) became unavailable during placement; no revival"}
                if([int]$s.command -ge $a.setupCommand -and (Grounded $p) -and [Math]::Abs($p.feet.x-$a.setupTarget.x) -lt .25 -and [Math]::Abs($p.feet.z-$a.setupTarget.z) -lt .25){
                    if(!$a.setupVerified){Note 'return-placement-observed' @{slot=$a.slot;command=$s.command;snapshot=$s}}
                    $a.setupVerified=$true
                }
            }
            Start-Sleep -Milliseconds 65
        }
        if(@($actors|Where-Object {(SetupCount $_.slot)-$_.setupBefore -ne 1}).Count){throw 'Expected exactly one additional setup per original actor'}
    }
    $live=@($actors|Where-Object outcome -eq 'running'|Sort-Object slot)
    if($live.Count -eq 0){throw 'No surviving grounded runner; no recovery placement permitted'}
    $gateX=if($FromEntryCheckpoint){72}else{74} # The checkpoint does not operate the original lever; stop before its closed gate.
    for($i=0;$i -lt $live.Count;$i++){
        $a=$live[$i]
        if($i -eq 0 -and $live.Count -gt 1){
            $a.route='north'
            $a.steps=@((Way 'North rope entry' 56 -72.5),(Way 'North 4m rope and departure' 56 -67.3 $true 10),(Way 'North pole edge' 57.6 -66.5),@{kind='pole';label='Pole25 to north receiver';pole=25;z=-66.5;minX=65.35;maxX=72.15;seconds=18},(Way 'North receiver inside turn' 71 -66.5),(Way 'North inside approach around corner rail' 71.55 -71.3),(Way 'North original gate approach' 71.55 -73),(Way 'Original gate north arrival; separated from direct runner' $gateX -73.3))
        }elseif($i -lt 2){
            $a.route='direct'
            $a.steps=@((Way 'Direct pole edge' 57.6 -74),@{kind='pole';label='Pole26 to direct cable island';pole=26;z=-74;minX=64.35;maxX=69.15;seconds=18},(Way 'Direct short rope alignment' 68.9 -74),(Way 'Direct 1.5m rope to original gate platform' 71.6 -74 $true 7),(Way 'Original gate south arrival; separated from north runner' $gateX -74.7))
        }else{
            $a.route='waiting-on-start';$a.mode='parking';$z=if($i -eq 2){-75.4}else{-72.6};$a.steps=@((Way 'Start rear waiting position' 54.5 $z));$a.started=$watch.Elapsed.TotalSeconds
            $step=$a.steps[0];Send $a.slot @{Action='walk';X=$step.x;Z=$step.z;Seconds=5}
        }
        Note 'assigned-route' @{slot=$a.slot;route=$a.route;steps=$a.steps}
    }
    $continuity=$true
    while(@($actors|Where-Object {$_.outcome -eq 'running' -and $_.route -ne 'waiting-on-start'}).Count -gt 0){
        if($watch.Elapsed.TotalSeconds -ge $MaxSeconds){throw "Return fixture budget $MaxSeconds seconds expired"}
        foreach($a in $actors){
            $s=Sample $a.slot;if(!$s){continue};$p=@($s.players|Where-Object local)
            if($p.Count -ne 1 -or [string]$p[0].id -ne $a.id -or [int]$s.restarts -ne $a.restarts){$continuity=$false;throw "Slot $($a.slot) actor/restart changed"}
            $p=$p[0];$a.last=$p;$a.snapshot=$s;$fresh=[double]$s.time -gt $a.lastTime
            if($fresh){$a.lastTime=[double]$s.time;$a.freshAt=$watch.Elapsed.TotalSeconds;Note 'snapshot' @{slot=$a.slot;route=$a.route;stage=$a.index;snapshot=$s}}
            elseif($watch.Elapsed.TotalSeconds-$a.freshAt -gt 3){throw "Slot $($a.slot) stale telemetry"}
            if($s.error){throw "Slot $($a.slot) review error: $($s.error)"}
            if($p.death -eq 'MapExit' -and $a.outcome -ne 'failed'){FailActor $a 'MapExit';continue}
            if($a.outcome -ne 'running'){continue}
            if($p.state -ne 'Normal'){
                $a.outcome=if($p.state -in @('EscapeCaptured','Captured') -or [string]$p.death -match 'Pursuit|Capture'){'pursuit-or-retirement'}else{'failed'}
                $a.reason="state=$($p.state), death=$($p.death); remaining actors continue";Note 'actor-unavailable-others-continue' @{slot=$a.slot;actor=$p};continue
            }
            $step=$a.steps[$a.index]
            if($a.route -eq 'waiting-on-start'){
                if($a.mode -eq 'parking' -and (At $p $step)){$a.mode='parked';$a.achieved+=,$step.label;Send $a.slot @{Seconds=.05};Note 'rear-waiter-grounded' @{slot=$a.slot;actor=$p}}
                elseif($a.mode -eq 'parking' -and $watch.Elapsed.TotalSeconds-$a.started -gt 6){FailActor $a 'Rear parking failed; do not risk F selecting a waiting player'}
                continue
            }
            if(!$parkingReady){$parkingReady=@($actors|Where-Object {$_.route -eq 'waiting-on-start' -and $_.outcome -eq 'running' -and $_.mode -ne 'parked'}).Count -eq 0;if(!$parkingReady){continue}}
            if($a.mode -eq 'waiting'){
                $a.stepAt=$watch.Elapsed.TotalSeconds;$a.started=$a.stepAt;$a.mode='look'
                $yaw=if($step.kind -eq 'pole'){-90}else{[Math]::Atan2($step.x-$p.feet.x,$step.z-$p.feet.z)*180/[Math]::PI}
                Send $a.slot @{Action='look';Yaw=$yaw;Pitch=8;Seconds=.15};continue
            }
            if($watch.Elapsed.TotalSeconds-$a.stepAt -gt $step.seconds+.8){FailActor $a "Timeout at $($step.label), mode=$($a.mode)";continue}
            if($a.mode -eq 'look' -and $watch.Elapsed.TotalSeconds-$a.started -ge .18){
                $a.started=$watch.Elapsed.TotalSeconds
                if($step.kind -eq 'walk'){$a.mode='walk';Send $a.slot @{Action='walk';X=$step.x;Z=$step.z;Balance=$step.balance;Seconds=$step.seconds}}
                else{$a.mode='catch';$a.seenFlight=$false;$a.braked=$false}
            }
            if($a.mode -eq 'walk'){if(At $p $step){Send $a.slot @{Seconds=.05};CompleteStep $a $p $s};continue}
            if($step.kind -ne 'pole'){continue}
            if($p.below -ge 0 -or $p.above -ge 0){FailActor $a 'Unexpected piggyback: common F selected another actor';continue}
            if($a.mode -in @('catch','jump-catch')){
                if($p.pole -eq $step.pole){$a.mode='pump';$a.started=$watch.Elapsed.TotalSeconds;$a.lastInput=$a.started;Send $a.slot @{Action='look';Yaw=-90;Pitch=8;Move=1;Seconds=3};Note 'actual-pole-caught' @{slot=$a.slot;pole=$p.pole;actor=$p};continue}
                if($p.pole -ge 0){FailActor $a "Unexpected pole $($p.pole)";continue}
                if($p.grabbable -eq $step.pole -and $fresh -and $watch.Elapsed.TotalSeconds-$a.lastTap -ge .4 -and [double]$s.time -gt $a.tapTime){
                    $near=@($s.players|Where-Object {$_.id -ne $p.id -and $_.state -eq 'Normal' -and [Math]::Sqrt([Math]::Pow($_.feet.x-$p.feet.x,2)+[Math]::Pow($_.feet.z-$p.feet.z,2)) -lt 3})
                    if($near.Count -eq 0){Send $a.slot @{Tap=$true;Seconds=.18};$a.lastTap=$watch.Elapsed.TotalSeconds;$a.tapTime=[double]$s.time}
                }elseif($a.mode -eq 'catch' -and $fresh -and $watch.Elapsed.TotalSeconds-$a.started -gt .25){
                    # Common FindGrabbable/IsSeatInReach determines reach; do not extend it.
                    Send $a.slot @{Action='walk';X=-261.5;Z=(118.3-$step.z);Jump=$true;Seconds=.9};$a.mode='jump-catch';$a.started=$watch.Elapsed.TotalSeconds;Note 'ordinary-jump-to-grip' @{slot=$a.slot;pole=$step.pole;actor=$p}
                }
                if($a.mode -eq 'jump-catch' -and $watch.Elapsed.TotalSeconds-$a.started -gt 2.2){FailActor $a 'Jump did not obtain the expected pole; no setup or forced grab'}
                continue
            }
            if($a.mode -eq 'pump'){
                if($p.pole -ne $step.pole){FailActor $a 'Pole hold ended before intended release';continue}
                $hostRow=@($actors|Where-Object slot -eq 0|Select-Object -First 1)
                $hostState=if($hostRow.Count){$hostRow[0].snapshot}else{$null}
                $hostPerson=@($hostState.players|Where-Object {[string]$_.id -eq [string]$p.id})
                $hostReady=$hostRow.Count -eq 1 -and $hostPerson.Count -eq 1 -and $hostPerson[0].state -eq 'Normal' -and
                    $hostPerson[0].pole -eq $step.pole -and $hostPerson[0].poleAngle -ge 55 -and $hostPerson[0].poleSpeed -gt 35 -and
                    $watch.Elapsed.TotalSeconds-$hostRow[0].freshAt -le .55 -and [Math]::Abs([double]$hostState.clock-[double]$s.clock) -le .55
                $v=$p.poleRelease;$h=[Math]::Sqrt($v.x*$v.x+$v.z*$v.z)
                if($fresh -and $hostReady -and $p.poleAngle -ge 55 -and $p.poleAngle -le 62 -and $p.poleSpeed -gt 35 -and $h -ge 5.9 -and $h -le 6.05 -and [Math]::Abs($v.y-6) -lt .15 -and $v.x -lt -5.8){
                    Note 'actual-release-window' @{slot=$a.slot;pole=$p.pole;clock=$s.clock;angle=$p.poleAngle;speed=$p.poleSpeed;velocity=$v;hostClock=$hostState.clock;hostTime=$hostState.time;hostAngle=$hostPerson[0].poleAngle;hostSpeed=$hostPerson[0].poleSpeed;hostSnapshotAge=($watch.Elapsed.TotalSeconds-$hostRow[0].freshAt)}
                    Send $a.slot @{Jump=$true;Move=1;Seconds=.2};$a.mode='flight';$a.started=$watch.Elapsed.TotalSeconds;$a.braked=$false;continue
                }
                if($watch.Elapsed.TotalSeconds-$a.lastInput -ge 2){Send $a.slot @{Move=1;Seconds=3};$a.lastInput=$watch.Elapsed.TotalSeconds}
                continue
            }
            if($a.mode -eq 'flight'){
                if($p.flying -and $p.pole -lt 0){$a.seenFlight=$true}
                if(!$a.braked -and $watch.Elapsed.TotalSeconds-$a.started -ge .22){Send $a.slot @{Move=-1;Seconds=1.8};$a.braked=$true;Note 'ordinary-air-brake' @{slot=$a.slot;actor=$p}}
                $localX=-200-$p.feet.x;$localZ=118.3-$p.feet.z
                if($a.seenFlight -and (Grounded $p) -and $localX -ge $step.minX -and $localX -le $step.maxX -and [Math]::Abs($localZ-$step.z) -lt 1.5){Send $a.slot @{Seconds=.05};CompleteStep $a $p $s}
                elseif($watch.Elapsed.TotalSeconds-$a.started -gt 3){FailActor $a 'No observed flight then supported landing on the intended receiver'}
            }
        }
        Start-Sleep -Milliseconds 65
    }
}catch{$fatal=$_.Exception.Message;Note 'return-aborted' $fatal}
finally{
    foreach($a in $actors){try{if($a.last.state -eq 'Normal'){Send $a.slot @{Seconds=.05}}}catch{}}
    $rows=@($actors|ForEach-Object{@{slot=$_.slot;actor=$_.id;route=$_.route;outcome=$_.outcome;reason=$_.reason;achieved=$_.achieved;final=$_.last;clock=$_.snapshot.clock;setupBefore=$_.setupBefore;setupAfter=(SetupCount $_.slot);setupAllowed=$_.setupAllowed;setupVerified=$_.setupVerified}})
    $complete=@($rows|Where-Object outcome -eq 'gate-approach-reached');$failures=@($rows|Where-Object {$_.outcome -eq 'failed' -or ($_.outcome -eq 'running' -and $_.route -ne 'waiting-on-start') -or $_.setupAfter-$_.setupBefore -ne $_.setupAllowed})
    $actualAddedSetup=0;foreach($row in $rows){$actualAddedSetup+=$row.setupAfter-$row.setupBefore}
    $summary=@{name=$Name;pass=(!$fatal -and $complete.Count -gt 0 -and $failures.Count -eq 0);failure=$fatal;elapsed=$watch.Elapsed.TotalSeconds;checkpointFixture=$true;continuous=$false;continuedSameSession=$continuity;fromEntryCheckpoint=[bool]$FromEntryCheckpoint;addedSetup=($actualAddedSetup -gt 0);addedSetupCount=$actualAddedSetup;requestedSetupCount=$setupRequestedCount;intermediateEntryToReturnTraversalTested=$false;mergeTraversalEvidenceUsed=(!$FromEntryCheckpoint);automaticRestart=$false;automaticDamage=$false;processesKeptRunning=$true;actors=$rows;bothBranchesReachedGate=(@($complete|Where-Object route -eq 'north').Count -gt 0 -and @($complete|Where-Object route -eq 'direct').Count -gt 0);scope='New Return start -> north rope/pole25 and direct pole26/rope -> original gate approach';goalLocalX=$gateX;fullRouteTested=$false;priorSummary=$priorPath;journal=$journal}
    $summary|ConvertTo-Json -Depth 20|Set-Content -LiteralPath $output -Encoding utf8
    $summary|ConvertTo-Json -Depth 20 -Compress|Write-Output
}
if(!$summary.pass){throw "Return fixture incomplete; inspect $output. Processes and actors were not restarted"}
