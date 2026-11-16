param(
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9_.-]{0,79}$')][string]$Name='space-entry-4-04',
    [ValidateRange(20,150)][double]$MaxSeconds=85,
    [string]$HarnessRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../../.harness/local/pursuer-space-20261116'))
)
# Continue the existing checkpoint session with ONLY look/walk/Balance/Tap/neutral input.
# This is not an entrance-to-ending continuous run. Never setup, revive, restart or damage.
# Stop on the new Return choice island (local 56,-18.5,-74); the old bridge is gone.
$ErrorActionPreference='Stop'
$run=Join-Path $HarnessRoot $Name
$prior=Get-Content -LiteralPath (Join-Path $run 'choices-fixture-summary.json') -Raw|ConvertFrom-Json
if(!$prior.pass -or !$prior.allSevenBPolesTraversed -or !$prior.continuedSameProcesses){throw 'Passed same-session full B choices evidence is required'}
$journal=Join-Path $run 'merge-fixture.jsonl';$output=Join-Path $run 'merge-fixture-summary.json'
if((Test-Path -LiteralPath $journal) -or (Test-Path -LiteralPath $output)){throw 'Merge already attempted; do not overwrite or silently restart it'}
$coords=Get-Content -LiteralPath (Join-Path $HarnessRoot 'fixture-coordinates.json') -Raw|ConvertFrom-Json
if([Math]::Abs($coords.frame[0]+200) -gt .001 -or [Math]::Abs($coords.frame[1]-25) -gt .001 -or [Math]::Abs($coords.frame[2]-118.3) -gt .001 -or [Math]::Abs($coords.rotation[1]-180) -gt .001){throw 'Review world/frame mapping changed'}
. (Join-Path $HarnessRoot 'control.ps1') -Name $Name
$watch=[Diagnostics.Stopwatch]::StartNew();$actors=@();$fatal=$null;$leverSlot=-1;$continuityVerified=$false
function Note([string]$Kind,$Data){[IO.File]::AppendAllText($journal,(@{utc=[DateTime]::UtcNow.ToString('o');elapsed=$watch.Elapsed.TotalSeconds;kind=$Kind;value=$Data}|ConvertTo-Json -Depth 18 -Compress)+[Environment]::NewLine)}
function Send([int]$Slot,[hashtable]$InputArguments){Send-Review -Slot $Slot @InputArguments;Note 'ordinary-input' @{slot=$Slot;command=(Get-Content (Join-Path $run "command-$Slot.json") -Raw|ConvertFrom-Json)}}
function Way([string]$Label,[double]$X,[double]$Z,[double]$Y,[double]$Seconds=7,[bool]$Balance=$false){@{label=$Label;x=-200-$X;z=118.3-$Z;y=25+$Y;seconds=$Seconds;balance=$Balance;kind='walk'}}
function SetupCount([int]$Slot){$p=Join-Path $run "actions-$Slot.jsonl";if(!(Test-Path $p)){return 0};return @(Get-Content $p|ForEach-Object{if($_){try{$_|ConvertFrom-Json}catch{}}}|Where-Object action -eq 'setup').Count}
function At($Person,$Step){
    $supported=[Math]::Abs($Person.feet.y-$Step.y) -le .22 -and !$Person.airborne -and !$Person.flying -and $Person.pole -lt 0
    if($Step.returnPlatform){
        # Final 4x4 island only: world X[-258,-254], Z[190.3,194.3].
        # Keep the full shared 0.3m body radius inside its edges, rather than
        # requiring several physical players to occupy one exact centre point.
        return $supported -and $Person.feet.x -ge -257.7 -and $Person.feet.x -le -254.3 -and $Person.feet.z -ge 190.6 -and $Person.feet.z -le 194.0
    }
    return $supported -and [Math]::Abs($Person.feet.x-$Step.x) -le .26 -and [Math]::Abs($Person.feet.z-$Step.z) -le .26
}
try{
    for($slot=0;$slot -lt 4;$slot++){
        $s=Sample $slot;$p=@($s.players|Where-Object local)
        if(!$s -or $p.Count -ne 1 -or [string]$p[0].id -ne [string]$prior.actors.PSObject.Properties[[string]$slot].Value){throw "Slot $slot no longer identifies the choices actor"}
        if($s.continuous -or [int]$s.restarts -ne 0){throw "Slot $slot is not the original no-restart checkpoint session"}
        $lastChoiceClock=($prior.hostPursuit|Select-Object -Last 1).clock
        if([double]$s.clock -lt [double]$lastChoiceClock){throw "Slot $slot clock predates the completed choices fixture"}
        $steps=@()
        if($slot -eq 1){
            # C island X59.25..64.75 / Z-64.5..-56.5; align inside its edge first.
            $steps+=Way 'C island rope alignment' 60 -62.5 -19.2
            $steps+=Way 'C joining rope to B island' 54.3 -62.5 -19.2 9 $true
        }
        if($slot -ne 0){
            # B island X49..55 / Z-64.5..-56.5. Stay 0.7m inside the lip to align.
            $steps+=Way 'B island west cable alignment' 49.7 -63 -19.2
            $steps+=Way 'B joining rope onto original receiver' 43.3 -63 -19.2 10 $true
            $steps+=Way 'Original receiver merge' 38 -65 -19.2
        }
        # Existing receiver-to-coop route only through its lever and first outside wire.
        # The original recovery floor/steps connect Y-19.2 to Y-18.5 here.
        $steps+=Way 'Original lever platform' 39.35 -71.6 -18.5 10
        $steps+=@{kind='lever';label='Original return lever';seconds=5}
        $steps+=Way 'Original outer wire alignment' 40.15 -74 -18.5
        $steps+=Way 'Original outer wire exit' 54.45 -74 -18.5 19 $true
        # Four 1.4m-separated targets lie within the same 4x4 destination island.
        # Only this final stage accepts the body-supported island footprint.
        $returnX=if($slot % 2 -eq 0){55.3}else{56.7}
        $returnZ=if($slot -lt 2){-74.7}else{-73.3}
        $returnStep=Way 'New Return choice start; stop before missing old bridge' $returnX $returnZ -18.5 5
        $returnStep.returnPlatform=$true
        $steps+=$returnStep
        $a=@{slot=$slot;id=[string]$p[0].id;restarts=[int]$s.restarts;setupBefore=(SetupCount $slot);steps=$steps;index=0;mode='waiting';started=0.0;lastTime=[double]$s.time;freshAt=0.0;last=$p[0];snapshot=$s;outcome='running';reason=$null;achieved=@();delay=$(if($slot -eq 3){1.6}else{0});lastTap=-10.0}
        $actors+=,$a
        if($p[0].state -eq 'Normal'){
            $x=-200-$p[0].feet.x;$z=118.3-$p[0].feet.z
            $inside=if($slot -eq 0){$x -ge 32.5 -and $x -le 44 -and $z -ge -68.8 -and $z -le -62}elseif($slot -eq 1){$x -ge 59.25 -and $x -le 64.75 -and $z -ge -64.5 -and $z -le -56.5}else{$x -ge 49 -and $x -le 55 -and $z -ge -64.5 -and $z -le -56.5}
            if(!$inside -or $p[0].flying -or $p[0].airborne -or $p[0].pole -ge 0 -or [Math]::Abs($p[0].feet.y-5.8) -gt .22){throw "Slot $slot is outside its observed receiver; no recovery setup is allowed"}
        }
        Note 'start-state' @{slot=$slot;snapshot=$s;setupCount=$a.setupBefore}
    }
    $continuityVerified=$true
    while(@($actors|Where-Object outcome -eq 'running').Count -gt 0){
        if($watch.Elapsed.TotalSeconds -gt $MaxSeconds){throw "Merge budget $MaxSeconds seconds expired"}
        foreach($a in $actors){
            $s=Sample $a.slot;if(!$s){continue};$p=@($s.players|Where-Object local)
            if($p.Count -ne 1 -or [string]$p[0].id -ne $a.id -or [int]$s.restarts -ne $a.restarts){$continuityVerified=$false;throw "Slot $($a.slot) actor/restart continuity changed"}
            $p=$p[0];$a.last=$p;$a.snapshot=$s
            if([double]$s.time -gt $a.lastTime){$a.lastTime=[double]$s.time;$a.freshAt=$watch.Elapsed.TotalSeconds;Note 'snapshot' @{slot=$a.slot;stage=$a.index;snapshot=$s}}
            elseif($watch.Elapsed.TotalSeconds-$a.freshAt -gt 3){throw "Slot $($a.slot) stale telemetry"}
            if($s.error){throw "Slot $($a.slot) review error: $($s.error)"}
            if($p.death -eq 'MapExit'){$a.outcome='failed';$a.reason='MapExit';continue}
            if($a.outcome -ne 'running'){continue}
            if($p.state -ne 'Normal'){
                $a.outcome=if($p.state -in @('EscapeCaptured','Captured') -or [string]$p.death -match 'Pursuit|Capture'){'pursuit-or-retirement'}else{'failed'}
                $a.reason="state=$($p.state); death=$($p.death); no revival";Note 'actor-unavailable-others-continue' @{slot=$a.slot;actor=$p};continue
            }
            if($watch.Elapsed.TotalSeconds -lt $a.delay){continue}
            $step=$a.steps[$a.index]
            if($a.mode -eq 'waiting'){
                $yaw=if($step.kind -eq 'lever'){250}else{[Math]::Atan2($step.x-$p.feet.x,$step.z-$p.feet.z)*180/[Math]::PI}
                Send $a.slot @{Action='look';Yaw=$yaw;Pitch=8;Seconds=.16};$a.mode='look';$a.started=$watch.Elapsed.TotalSeconds;continue
            }
            if($a.mode -eq 'look' -and $watch.Elapsed.TotalSeconds-$a.started -ge .18){
                $a.started=$watch.Elapsed.TotalSeconds;$a.mode='moving'
                if($step.kind -eq 'walk'){Send $a.slot @{Action='walk';X=$step.x;Z=$step.z;Seconds=$step.seconds;Balance=$step.balance}}
            }
            if($a.mode -ne 'moving'){continue}
            $done=$false
            if($step.kind -eq 'lever'){
                $done=$s.locks.Count -gt 0 -and [bool]$s.locks[0]
                $owner=@($actors|Where-Object slot -eq $leverSlot)
                if($leverSlot -lt 0 -or $owner.Count -eq 0 -or $owner[0].last.state -ne 'Normal'){$leverSlot=$a.slot}
                if(!$done -and $a.slot -eq $leverSlot -and $watch.Elapsed.TotalSeconds-$a.lastTap -gt .75){Send $a.slot @{Tap=$true;Seconds=.15};$a.lastTap=$watch.Elapsed.TotalSeconds}
            }else{$done=At $p $step}
            if($done){
                Send $a.slot @{Seconds=.05};$a.achieved+=,$step.label;Note 'waypoint-grounded' @{slot=$a.slot;label=$step.label;clock=$s.clock;actor=$p}
                $a.index++;$a.mode='waiting'
                if($a.index -ge $a.steps.Count){$a.outcome='reached-new-return-start';Note 'actor-complete' @{slot=$a.slot;actor=$p}}
            }elseif($watch.Elapsed.TotalSeconds-$a.started -gt $step.seconds+.7){
                Send $a.slot @{Seconds=.05};$a.outcome='failed';$a.reason="No grounded arrival: $($step.label)";Note 'route-or-pilot-failure' @{slot=$a.slot;reason=$a.reason;actor=$p}
            }
        }
        Start-Sleep -Milliseconds 100
    }
}catch{$fatal=$_.Exception.Message;Note 'merge-aborted' $fatal}
finally{
    # Neutral input only; leave all processes and game states running for inspection.
    foreach($a in $actors){try{if($a.last.state -eq 'Normal'){Send $a.slot @{Seconds=.05}}}catch{} }
    $rows=@($actors|ForEach-Object{@{slot=$_.slot;actor=$_.id;outcome=$_.outcome;reason=$_.reason;achieved=$_.achieved;final=$_.last;clock=$_.snapshot.clock;restarts=$_.snapshot.restarts;setupBefore=$_.setupBefore;setupAfter=(SetupCount $_.slot)}})
    $reached=@($rows|Where-Object outcome -eq 'reached-new-return-start').Count
    $failed=@($rows|Where-Object { $_.outcome -eq 'failed' -or $_.outcome -eq 'running' -or $_.setupAfter -ne $_.setupBefore }).Count
    $addedSetup=@($rows|Where-Object {$_.setupAfter -ne $_.setupBefore}).Count -gt 0
    $summary=@{name=$Name;pass=(!$fatal -and $reached -gt 0 -and $failed -eq 0);failure=$fatal;elapsed=$watch.Elapsed.TotalSeconds;checkpointFixture=$true;continuous=$false;continuedSameSession=$continuityVerified;addedSetup=$addedSetup;automaticRestart=$false;automaticDamage=$false;processesKeptRunning=$true;reached=$reached;allFourReached=($reached -eq 4);actors=$rows;scope='C joining rope -> B island -> B joining rope -> original receiver/lever/outside wire -> new Return choice start only';fullRouteTested=$false;newReturnBranchesTested=$false;priorSummary=(Join-Path $run 'choices-fixture-summary.json');journal=$journal}
    $summary|ConvertTo-Json -Depth 18|Set-Content -LiteralPath $output -Encoding utf8
    $summary|ConvertTo-Json -Depth 18 -Compress|Write-Output
}
if(!$summary.pass){throw "Merge incomplete; inspect $output. No actor was revived or restarted"}
