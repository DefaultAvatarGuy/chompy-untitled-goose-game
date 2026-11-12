param([ValidateSet('Install','Classic','OriginalHonk','Previous')][string]$Mode='Install')
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$game=Split-Path $root -Parent
$plans=New-Object 'System.Collections.Generic.List[object]'
$committed=New-Object 'System.Collections.Generic.List[object]'
function Hash([string]$path){return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
function Add-Stage([string]$target,[byte[]]$bytes,[string]$expected){
 $exists=Test-Path -LiteralPath $target
 $previous=$null;if($exists){$previous=Hash $target;if($previous -eq $expected){return}}
 $temp="$target.chompy-v05-stage";$rollback="$target.chompy-v05-rollback"
 if((Test-Path -LiteralPath $temp) -or (Test-Path -LiteralPath $rollback)){throw "A previous staging/recovery file exists beside $target. Keep it and send the error before retrying."}
 [void][IO.Directory]::CreateDirectory((Split-Path $target -Parent))
 $plan=[pscustomobject]@{Target=$target;Temp=$temp;Rollback=$rollback;Existed=$exists;Previous=$previous;Expected=$expected;Delete=$false}
 $plans.Add($plan)
 [IO.File]::WriteAllBytes($temp,$bytes)
 if((Hash $temp) -ne $expected){throw "Output verification failed for $target. No game files were changed."}
}
function Add-Delete([string]$target){
 if(!(Test-Path -LiteralPath $target)){return}
 $rollback="$target.chompy-v05-rollback"
 if(Test-Path -LiteralPath $rollback){throw "An existing recovery file was found beside $target. Keep it and send the error."}
 $plans.Add([pscustomobject]@{Target=$target;Temp=$null;Rollback=$rollback;Existed=$true;Previous=(Hash $target);Expected=$null;Delete=$true})
}
function Read-Original([string]$target,[string]$expected,[string]$current){
 foreach($candidate in @("$target.chompy-playtime-original","$target.character-club-original","$target.tiny-rex-original","$target.chompy-roar-original")){
  if((Test-Path -LiteralPath $candidate) -and (Hash $candidate) -eq $expected){return ,([IO.File]::ReadAllBytes($candidate))}
 }
 if($current -ne $expected){throw "The original backup for $target is missing. Restore that original game file from your game ZIP first."}
 $backup="$target.chompy-playtime-original"
 if(Test-Path -LiteralPath $backup){throw "An unexpected backup exists at $backup. It was not overwritten."}
 Copy-Item -LiteralPath $target -Destination $backup
 if((Hash $backup) -ne $expected){throw 'Original backup verification failed.'}
 return ,([IO.File]::ReadAllBytes($backup))
}
function Plan-Asset([string]$kind,[bool]$originalOutput,[bool]$legacyModel=$false){
 $folder=Join-Path $root $kind
 if($legacyModel){$folder=Join-Path $root 'Recovery/OriginalModel'}
 $m=Get-Content -LiteralPath (Join-Path $folder 'patch.json') -Raw|ConvertFrom-Json
 $target=Join-Path $game $m.target
 if(!(Test-Path -LiteralPath $target)){throw "The required game file was not found: $($m.target)"}
 $current=Hash $target
 $known=@($m.original_sha256,$m.patched_sha256)
 if($kind -eq 'Model'){$known+=@($manifest.known_models)}else{$known+=@($manifest.known_audio)}
 if($known -notcontains $current){throw "Unknown game revision or other modification: $($m.target). No game files were changed."}
 [byte[]]$source=Read-Original $target $m.original_sha256 $current
 $expected=$m.patched_sha256;if($originalOutput){$expected=$m.original_sha256}
 if($current -eq $expected){return}
 [byte[]]$bytes=$source
 if(!$originalOutput){
  if($kind -eq 'Model'){
   foreach($patch in $m.patches){
    $file=Join-Path $folder $patch.file
    if((Hash $file) -ne $patch.sha256){throw 'A model payload is damaged. Extract the ZIP again.'}
    $part=[IO.File]::ReadAllBytes($file)
    if($part.Length -ne $patch.size){throw 'Invalid model patch length.'}
    [Array]::Copy($part,0,$bytes,[int]$patch.offset,$part.Length)
   }
  }else{
   $file=Join-Path $folder 'Payload/roar.bin'
   if((Hash $file) -ne $m.payload_sha256){throw 'The roar payload is damaged. Extract the ZIP again.'}
   $part=[IO.File]::ReadAllBytes($file);$bytes=New-Object byte[] ([int]$m.output_size)
   foreach($copy in $m.copies){[Array]::Copy($source,[int]$copy.source_offset,$bytes,[int]$copy.offset,[int]$copy.size)}
   foreach($patch in $m.patches){[Array]::Copy($part,[int]$patch.payload_offset,$bytes,[int]$patch.offset,[int]$patch.size)}
  }
 }
 Add-Stage $target $bytes $expected
}
try{
 if(!(Test-Path -LiteralPath (Join-Path $game 'Untitled.exe'))){throw 'Put the complete Chompy-Playtime folder beside Untitled.exe, then run Install.cmd.'}
 if(Get-Process -Name 'Untitled' -ErrorAction SilentlyContinue){throw 'Close Untitled Goose Game before installing or restoring Chompy.'}
 $manifest=Get-Content -LiteralPath (Join-Path $root 'install-manifest.json') -Raw|ConvertFrom-Json
 $pluginRel='BepInEx/plugins/TinyRexPlaytime/TinyRexPlaytime.dll'
 $plugin=Join-Path $game $pluginRel
 $pluginEntry=$manifest.files|Where-Object {$_.path -eq $pluginRel}
 if($Mode -eq 'Install'){
  foreach($code in @(@('Assembly-CSharp.dll',$manifest.game_code_sha256),@('Assembly-CSharp-firstpass.dll',$manifest.fmod_code_sha256))){
   if((Hash (Join-Path $game "Untitled_Data/Managed/$($code[0])")) -ne $code[1]){throw 'The game code differs from the supplied itch.io 1.1.4 build. No game files were changed.'}
  }
  foreach($entry in $manifest.files){
   $source=Join-Path (Join-Path $root 'Payload') $entry.path
   if((Hash $source) -ne $entry.sha256){throw "Installer payload verification failed: $($entry.path)"}
   $target=Join-Path $game $entry.path
   if((Test-Path -LiteralPath $target) -and !$entry.preserve_existing){
    $allowed=@($entry.sha256)+@($entry.existing_hashes)
    if($allowed -notcontains (Hash $target)){throw "An unknown mod-loader/plugin file exists: $($entry.path). It was not overwritten."}
   }
  }
 }
 if($Mode -eq 'Classic' -or $Mode -eq 'Previous'){
  if((Test-Path -LiteralPath $plugin) -and (@($pluginEntry.sha256,$manifest.previous_plugin_sha256)+@($pluginEntry.existing_hashes)) -notcontains (Hash $plugin)){throw 'An unknown Playtime plugin is installed. It was not overwritten.'}
 }
 if($Mode -eq 'OriginalHonk'){
  Plan-Asset 'Roar' $true
 }else{
  Plan-Asset 'Model' $false ($Mode -eq 'Classic' -or $Mode -eq 'Previous')
  Plan-Asset 'Roar' ($Mode -eq 'Classic')
  if($Mode -eq 'Install'){
   foreach($entry in $manifest.files){
    $target=Join-Path $game $entry.path
    if($entry.preserve_existing -and (Test-Path -LiteralPath $target)){continue}
    Add-Stage $target ([IO.File]::ReadAllBytes((Join-Path (Join-Path $root 'Payload') $entry.path))) $entry.sha256
   }
  }elseif($Mode -eq 'Classic'){
   Add-Delete $plugin
  }elseif($Mode -eq 'Previous'){
   $previous=Join-Path $root 'Recovery/TinyRexPlaytime-v0.5.dll'
   if((Hash $previous) -ne $manifest.previous_plugin_sha256){throw 'The v0.5 recovery payload failed verification.'}
   Add-Stage $plugin ([IO.File]::ReadAllBytes($previous)) $manifest.previous_plugin_sha256
  }
 }
 # Every file is fully prepared and checked before any active file is replaced.
 if(Get-Process -Name 'Untitled' -ErrorAction SilentlyContinue){throw 'The game was opened during setup. Close it and run setup again.'}
 foreach($plan in $plans){
  if((Test-Path -LiteralPath $plan.Target) -ne $plan.Existed){throw 'A destination changed during setup. No game files were changed.'}
  if($plan.Existed -and (Hash $plan.Target) -ne $plan.Previous){throw 'A destination changed during setup. No game files were changed.'}
 }
 foreach($plan in $plans){
  if($plan.Delete){[IO.File]::Move($plan.Target,$plan.Rollback)}
  elseif($plan.Existed){[IO.File]::Replace($plan.Temp,$plan.Target,$plan.Rollback)}
  else{[IO.File]::Move($plan.Temp,$plan.Target)}
  $committed.Add($plan)
 }
 foreach($plan in $committed){if(!$plan.Delete -and (Hash $plan.Target) -ne $plan.Expected){throw 'Post-install verification failed.'}}
}catch{
 $failure=$_.Exception.Message;$recoveryFailed=$false
 for($i=$committed.Count-1;$i -ge 0;$i--){
  $p=$committed[$i]
  try{
   if($p.Existed){if(Test-Path -LiteralPath $p.Target){[IO.File]::Replace($p.Rollback,$p.Target,[NullString]::Value)}else{[IO.File]::Move($p.Rollback,$p.Target)}}
   elseif(Test-Path -LiteralPath $p.Target){Remove-Item -LiteralPath $p.Target}
  }catch{$recoveryFailed=$true;Write-Host ("Recovery error: "+$_.Exception.Message) -ForegroundColor Red}
 }
 Write-Host $failure -ForegroundColor Red
 if($recoveryFailed){Write-Host 'Automatic recovery did not finish. Keep all .chompy-v05-rollback files and send the error before launching.' -ForegroundColor Red}
 exit 1
}finally{
 foreach($plan in $plans){if($plan.Temp -and (Test-Path -LiteralPath $plan.Temp)){Remove-Item -LiteralPath $plan.Temp -ErrorAction SilentlyContinue}}
}
foreach($plan in $committed){if(Test-Path -LiteralPath $plan.Rollback){Remove-Item -LiteralPath $plan.Rollback -ErrorAction SilentlyContinue}}
switch($Mode){
 'Install'{Write-Host 'Chompy Playtime v0.6.1 installed: tiny flailing arms + roar + size controls + head-following fire + fart. Launch Untitled.exe normally.' -ForegroundColor Green}
 'Classic'{Write-Host 'Classic restored: original Chompy and honk; runtime size/fire/fart features disabled.' -ForegroundColor Green}
 'OriginalHonk'{Write-Host 'Original honk restored. Chompy and runtime features are unchanged.' -ForegroundColor Green}
 'Previous'{Write-Host 'Previous working v0.5 plugin + dinosaur roar restored.' -ForegroundColor Green}
}
