param(
 [Parameter(Mandatory)][string]$Name,
 [Parameter(Mandatory)][string]$BuildExecutable,
 [ValidateSet(3,4)][int]$Players=3,
 [ValidateSet('walk','crouch','sprint-jump','diagonal-jump','one-leg-crouch','two-leg-crawl')][string]$Case='walk',
 [switch]$KeepOpen
)
$ErrorActionPreference='Stop'
$run=Join-Path $PSScriptRoot $Name
if(Test-Path -LiteralPath $run){throw 'Use a unique run name; old evidence is never overwritten'}
if(Get-Process -Name PursuerCampaign -ErrorAction SilentlyContinue){throw 'Existing standalone players must finish first'}
$exe=[IO.Path]::GetFullPath($BuildExecutable)
if(!(Test-Path -LiteralPath $exe -PathType Leaf)){throw 'Specified build does not exist'}
New-Item -ItemType Directory -Path $run | Out-Null
Copy-Item -LiteralPath $PSCommandPath -Destination (Join-Path $run 'pilot-source.ps1')
$ids=@{};$seen=@{};$processes=@();$events=Join-Path $run 'events.jsonl';$rows=@();$errorText=$null;$started=[DateTime]::UtcNow
$socket=[Net.Sockets.UdpClient]::new(0)
try{$port=([Net.IPEndPoint]$socket.Client.LocalEndPoint).Port}finally{$socket.Dispose()}
$artifacts=@($exe,(Join-Path (Split-Path $exe) 'PursuerCampaign_Data/level0'),(Join-Path (Split-Path $exe) 'PursuerCampaign_Data/Managed/Assembly-CSharp.dll'))|ForEach-Object{
 @{path=$_;sha256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash}
}
function Note([string]$Kind,$Data){[IO.File]::AppendAllText($events,(@{utc=[DateTime]::UtcNow.ToString('o');kind=$Kind;data=$Data}|ConvertTo-Json -Depth 24 -Compress)+[Environment]::NewLine)}
function State([int]$Slot){
 $file=Join-Path $run "state-$Slot.json"
 for($i=0;$i -lt 10;$i++){
  try{
   $stream=[IO.File]::Open($file,[IO.FileMode]::Open,[IO.FileAccess]::Read,([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
   try{$reader=[IO.StreamReader]::new($stream);$raw=$reader.ReadToEnd()}finally{$stream.Dispose()}
   $s=$raw|ConvertFrom-Json
   if(!$seen.ContainsKey($Slot) -or $seen[$Slot] -ne $s.time){$seen[$Slot]=$s.time;[IO.File]::AppendAllText((Join-Path $run "timeline-$Slot.jsonl"),$raw+[Environment]::NewLine)}
   return $s
  }catch{Start-Sleep -Milliseconds 30}
 }
 return $null
}
function Send([int]$Slot,[hashtable]$Data){
 if(!$ids.ContainsKey($Slot)){$ids[$Slot]=0};$ids[$Slot]++
 $Data.id=$ids[$Slot];if(!$Data.ContainsKey('seconds')){$Data.seconds=.1}
 $path=Join-Path $run "command-$Slot.json";$temp="$path.$PID.new"
 $Data|ConvertTo-Json -Depth 8 -Compress|Set-Content -LiteralPath $temp -Encoding utf8
 # Unity briefly opens this command file without delete sharing. Retry only the
 # same atomic delivery; do not create a second gameplay command or hide failure.
 for($attempt=0;$attempt -lt 20;$attempt++){
  try{[IO.File]::Move($temp,$path,$true);Note 'command' @{slot=$Slot;command=$Data};return}
  catch{if($attempt -eq 19){throw};Start-Sleep -Milliseconds 65}
 }
}
function Verify([int]$Slot){
 $s=State $Slot;if(!$s){throw "No fresh state for slot $Slot"}
 $a=@($s.players|Where-Object local);if($a.Count -ne 1){throw "Ambiguous local actor for slot $Slot"};$a=$a[0]
 $rows[$Slot].last=$a;$rows[$Slot].lastClock=$s.clock
 if($s.restarts -ne $rows[$Slot].restart){throw "Unrequested auto-recovery/restart for slot $Slot"}
 if($a.state -ne 'Normal'){throw "Slot $Slot is $($a.state), death $($a.death)"}
 if($a.injury -ne $rows[$Slot].injury){throw "Slot $Slot gained injury $($a.injury) from baseline $($rows[$Slot].injury)"}
 if($s.error){throw "Review error for slot ${Slot}: $($s.error)"}
 if($a.airborne){$rows[$Slot].airborneObserved=$true};if($a.crouched){$rows[$Slot].crouchObserved=$true}
 return $a
}
function MoveGroup([string]$Label,[float[]]$Xs,[float]$Z,[float]$Y,[switch]$Jump,[switch]$Sprint,[switch]$Crouch,[float]$YTolerance=.18){
 foreach($slot in 0..($Players-1)){
  Send $slot @{action='walk';target=@{x=-200-$Xs[$slot];y=25+$Y;z=118.3-$Z};seconds=25;sprint=[bool]$Sprint;crouch=[bool]$Crouch;jump=[bool]$Jump}
 }
 $until=[DateTime]::UtcNow.AddSeconds(28);$done=@{}
 do{
  foreach($slot in 0..($Players-1)){
   $a=Verify $slot
   if([math]::Abs($a.feet.x-(-200-$Xs[$slot])) -lt .25 -and [math]::Abs($a.feet.z-(118.3-$Z)) -lt .25 -and [math]::Abs($a.feet.y-(25+$Y)) -lt $YTolerance -and !$a.airborne){$done[$slot]=$true}
  }
  if($done.Count -eq $Players){foreach($slot in 0..($Players-1)){$rows[$slot].achieved+=,$Label};Note 'waypoint-completed' @{label=$Label;actors=$rows};return}
  Start-Sleep -Milliseconds 90
 }while([DateTime]::UtcNow -lt $until)
 throw "Timed out at $Label; reached slots $($done.Keys -join ',')"
}
try{
 @{scope='Focused checkpoint fixture at the existing button landing. Not an entrance-to-ending run.';case=$Case;players=$Players;port=$port;build=$artifacts;setupAllowed=$true;damageAllowed=($Case -match 'leg');restartAllowed=$false;commonPhysics=$true;started=$started.ToString('o')}|ConvertTo-Json -Depth 9|Set-Content (Join-Path $run 'plan.json')
 for($slot=0;$slot -lt $Players;$slot++){
  $role=if($slot -eq 0){'-escapeHost'}else{'-escapeClient'}
  $arguments=@($role,'-escapePort',$port,'-escapePlayers',$Players,'-escapeReview',('"'+$run+'"'),'-escapeSlot',$slot,'-escapeCampaignReview','-devmode','-screen-width','640','-screen-height','360','-screen-fullscreen','0','-logFile',('"'+(Join-Path $run "player-$slot.log")+'"'))
  $p=Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
  $processes+=@{slot=$slot;pid=$p.Id;start=$p.StartTime.ToUniversalTime().Ticks}
  $processes|ConvertTo-Json|Set-Content (Join-Path $run 'processes.json')
  if($slot -eq 0){$limit=[DateTime]::UtcNow.AddSeconds(50);do{$s=State 0;if($s.connection -eq 'Host'){break};Start-Sleep -Milliseconds 150}while([DateTime]::UtcNow -lt $limit);if($s.connection -ne 'Host'){throw 'Host failed to connect'}}
  else{$limit=[DateTime]::UtcNow.AddSeconds(35);do{$s=State $slot;if($s.connection -eq 'Client' -and @($s.players|Where-Object local).Count -eq 1){break};Start-Sleep -Milliseconds 150}while([DateTime]::UtcNow -lt $limit);if($s.connection -ne 'Client'){throw "Client $slot failed to connect"}}
 }
 $limit=[DateTime]::UtcNow.AddSeconds(65)
 do{$ready=0;foreach($slot in 0..($Players-1)){$s=State $slot;if($s.ready -and !$s.blocked -and @($s.players).Count -eq $Players){$ready++}};if($ready -eq $Players){break};Start-Sleep -Milliseconds 150}while([DateTime]::UtcNow -lt $limit)
 if($ready -ne $Players){throw 'All players did not become ready'}
 $xs=if($Players -eq 3){[float[]]@(121,122.25,123.5)}else{[float[]]@(120.8,121.75,122.7,123.65)}
 $east=[float[]]@($xs|ForEach-Object{$_+$(if($Players -eq 3){4.5}else{4.9})})
 foreach($slot in 0..($Players-1)){
  $s=State $slot;$a=$s.players|Where-Object local
  if($s.continuous){throw 'Fixture launcher unexpectedly enabled continuous mode'}
  if($a.injury -ne 0 -or $a.state -ne 'Normal'){throw 'Fresh healthy actors required'}
  $rows+=@{slot=$slot;actor=$a.id;restart=$s.restarts;injury=0;airborneObserved=$false;crouchObserved=$false;achieved=@();last=$a}
  Send $slot @{action='setup';target=@{x=-200-$xs[$slot];y=25.08;z=84.3};yaw=180;pitch=12;seconds=.1}
 }
 $limit=[DateTime]::UtcNow.AddSeconds(8)
 do{$placed=0;foreach($slot in 0..($Players-1)){$a=Verify $slot;if([math]::Abs($a.feet.x-(-200-$xs[$slot])) -lt .2 -and [math]::Abs($a.feet.z-84.3) -lt .2 -and [math]::Abs($a.feet.y-25) -lt .18 -and !$a.airborne){$placed++}};if($placed -eq $Players){break};Start-Sleep -Milliseconds 90}while([DateTime]::UtcNow -lt $limit)
 if($placed -ne $Players){throw 'Checkpoint placement did not settle on upper landing'}
 Note 'explicit-fixture-placement-observed' $rows
 if($Case -in @('one-leg-crouch','two-leg-crawl')){
  $mask=if($Case -eq 'one-leg-crouch'){4}else{12}
  foreach($slot in 0..($Players-1)){
   Send 0 @{action='damage';actor=$rows[$slot].actor;damage=$mask;seconds=.1}
   $limit=[DateTime]::UtcNow.AddSeconds(5)
   do{$s=State $slot;$a=$s.players|Where-Object local;if($a.injury -eq $mask){break};Start-Sleep -Milliseconds 90}while([DateTime]::UtcNow -lt $limit)
   if($a.injury -ne $mask){throw "Common fixture damage not observed for slot $Slot"}
   $rows[$slot].injury=$mask
  }
  Note 'explicit-common-damage-fixture-observed' $rows
 }
 foreach($row in $rows){$row.airborneObserved=$false;$row.crouchObserved=$false}
 $crouch=$Case -in @('crouch','one-leg-crouch','two-leg-crawl');$sprint=$Case -in @('sprint-jump','diagonal-jump')
 if($Case -eq 'diagonal-jump'){
  foreach($slot in 0..($Players-1)){Send $slot @{action='walk';target=@{x=-200-$east[$slot];y=19;z=71.925};seconds=3;sprint=$true;jump=$true}}
  $limit=[DateTime]::UtcNow.AddSeconds(3.5);do{foreach($slot in 0..($Players-1)){$null=Verify $slot};Start-Sleep -Milliseconds 90}while([DateTime]::UtcNow -lt $limit)
  Note 'diagonal-jump-attempt' $rows
  # A rounded capsule at a 250 mm tread also contacts the previous 200 mm
  # higher riser. Permit that one riser plus common skin only at these stair
  # points; retain the tighter floor tolerance on all flat landings.
  MoveGroup 'second-flight-before-edge-probe' $east 46.375 -6 -Sprint -YTolerance .35
  # Deliberately press into the newly closed west edge. Success means collision
  # refuses this shortcut while players remain supported, not crossing the wall.
  foreach($slot in 0..($Players-1)){Send $slot @{action='walk';target=@{x=-200-$xs[$slot];y=19;z=71.925};seconds=1.5;sprint=$true}}
  $limit=[DateTime]::UtcNow.AddSeconds(1.8)
  do{
   foreach($slot in 0..($Players-1)){
    $a=Verify $slot
    if((-200-$a.feet.x) -lt 125.15){throw "Slot $Slot crossed the west edge guard during ordinary input"}
   }
   Start-Sleep -Milliseconds 90
  }while([DateTime]::UtcNow -lt $limit)
  foreach($row in $rows){$row.guardBlockedShortcut=$true}
  Note 'west-guard-refused-shortcut-without-fall' $rows
  # Return by the actual stairs and middle landing. The previous failing fixture
  # tried a direct diagonal through the unguarded pocket; its evidence is kept.
  MoveGroup 'second-flight-realign-after-guard' $east 46.375 -6 -Sprint -YTolerance .35
  MoveGroup 'backtrack-second-flight-to-middle-landing' $east 42.3 -4 -Sprint
  MoveGroup 'backtrack-middle-landing-turn' $xs 42.3 -4 -Sprint
 }
 MoveGroup 'first-flight-middle-landing' $xs 42.3 -4 -Jump:($Case -eq 'sprint-jump') -Sprint:$sprint -Crouch:$crouch
 MoveGroup 'single-middle-turn' $east 42.3 -4 -Sprint:$sprint -Crouch:$crouch
 MoveGroup 'second-flight-lower-landing' $east 50 -8 -Jump:($Case -eq 'sprint-jump') -Sprint:$sprint -Crouch:$crouch
 MoveGroup 'original-gallery-flush-seam' $xs 50 -8 -Sprint:$sprint -Crouch:$crouch
 if($Case -eq 'diagonal-jump'){
  foreach($slot in 0..($Players-1)){Send $slot @{action='walk';target=@{x=-317;y=17;z=68.3};seconds=1.5;sprint=$true}}
  $limit=[DateTime]::UtcNow.AddSeconds(1.8)
  do{
   foreach($slot in 0..($Players-1)){
    $a=Verify $slot
    if((-200-$a.feet.x) -lt 120.15){throw "Slot $Slot entered the obsolete A05 pocket through the lower west closure"}
   }
   Start-Sleep -Milliseconds 90
  }while([DateTime]::UtcNow -lt $limit)
  foreach($row in $rows){$row.lowerExitGuardBlockedShortcut=$true}
  Note 'lower-west-closure-refused-obsolete-pocket-entry' $rows
  MoveGroup 'lower-gallery-realign-after-exit-guard' $xs 50 -8 -Sprint
 }
 MoveGroup 'original-fire-corridor-entry' $xs 52.2 -8 -Sprint:$sprint -Crouch:$crouch
 foreach($slot in 0..($Players-1)){Send $slot @{action='capture';seconds=.1}}
 if($crouch -and @($rows|Where-Object {!$_.crouchObserved}).Count){throw 'Crouch/crawl posture not observed for every actor'}
 if($Case -match 'jump' -and @($rows|Where-Object {!$_.airborneObserved}).Count){throw 'Requested jump did not produce observed airborne state for every actor'}
 Note 'focused-traversal-complete' $rows
}catch{$errorText=$_.Exception.Message;Note 'failure' @{message=$errorText;stack=$_.ScriptStackTrace};foreach($slot in 0..($Players-1)){try{Send $slot @{action='capture';seconds=.1}}catch{}}}
finally{
 @{success=($null -eq $errorText);error=$errorText;case=$Case;scope='Focused checkpoint fixture, not continuous campaign or full gameplay coverage.';actors=$rows;build=$artifacts;started=$started.ToString('o');finished=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 24|Set-Content (Join-Path $run 'result.json')
 if(!$KeepOpen){Start-Sleep -Milliseconds 500;foreach($slot in 0..($Players-1)){try{Send $slot @{action='quit';seconds=.1}}catch{}}}
}
if($errorText){throw $errorText}
Get-Content (Join-Path $run 'result.json') -Raw
