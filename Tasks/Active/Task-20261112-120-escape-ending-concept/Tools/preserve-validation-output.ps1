param([Parameter(Mandatory)][string]$Iteration)
$ErrorActionPreference='Stop'
$validationRoot=(Resolve-Path 'D:/github/POC_CURTAINCALL/.harness/local/ui120').Path
$backupRoot=[IO.Path]::GetFullPath((Join-Path $validationRoot "Artifacts/$Iteration"))
if(-not $backupRoot.StartsWith($validationRoot+[IO.Path]::DirectorySeparatorChar)){throw 'Outside validation project'}
New-Item -ItemType Directory -Path $backupRoot -ErrorAction Stop | Out-Null
foreach($entry in @('Assets/Resources/Prefabs/UIs/EscapeResultsKit','Assets/Scenes/Tests/EscapeResultsKit')){
    $target=[IO.Path]::GetFullPath((Join-Path $validationRoot $entry))
    if(-not $target.StartsWith($validationRoot+[IO.Path]::DirectorySeparatorChar)){throw 'Outside validation project'}
    if(Test-Path -LiteralPath $target){
        $name=if($entry -like '*Prefabs*'){'Prefabs'}else{'Scenes'}
        Move-Item -LiteralPath $target -Destination (Join-Path $backupRoot $name)
        if(Test-Path -LiteralPath "$target.meta"){Move-Item -LiteralPath "$target.meta" -Destination (Join-Path $backupRoot "$name.meta")}
    }
}
