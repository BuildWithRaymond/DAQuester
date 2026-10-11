param(
    [string]$Output='artifacts/release',
    [string]$PreviousPackage='artifacts/release-1.0.7/BuildWithRaymond.DAQuester-1.0.7-full.nupkg',
    [string]$Executable='src/DAQuester.App/bin/Release/net10.0-windows/win-x86/DAQuester.exe',
    [switch]$DownloadPrevious
)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
function Resolve-RepoPath([string]$Value){if([IO.Path]::IsPathRooted($Value)){[IO.Path]::GetFullPath($Value)}else{[IO.Path]::GetFullPath((Join-Path $repoRoot $Value))}}
$appPath=Resolve-RepoPath $Executable
$releasePath=Resolve-RepoPath $Output
$previousPath=Resolve-RepoPath $PreviousPackage
if(-not(Test-Path -LiteralPath $previousPath) -and $DownloadPrevious) {
    $artifactRoot=[IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))+[IO.Path]::DirectorySeparatorChar
    if(-not $previousPath.StartsWith($artifactRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Downloaded compatibility fixtures must remain inside artifacts.'}
    if([IO.Path]::GetFileName($previousPath) -ne 'BuildWithRaymond.DAQuester-1.0.7-full.nupkg'){throw 'The pinned download fixture is version 1.0.7.'}
    New-Item -ItemType Directory -Path (Split-Path $previousPath -Parent) -Force | Out-Null
    Invoke-WebRequest -Uri 'https://github.com/BuildWithRaymond/DAQuester/releases/download/v1.0.7/BuildWithRaymond.DAQuester-1.0.7-full.nupkg' -OutFile $previousPath
    $digest=(Get-FileHash -LiteralPath $previousPath -Algorithm SHA256).Hash
    if($digest -ne 'B856C6E646C3DDA595CFA2BE6B96F4876086B3C496FC951FC783FCA382FF62DB'){throw 'The pinned previous package digest did not match.'}
}
foreach($required in @($appPath,$previousPath,(Join-Path $releasePath 'releases.win.json'))){if(-not(Test-Path -LiteralPath $required)){throw "Compatibility fixture missing: $required"}}
$result=Join-Path $repoRoot 'artifacts/update-compatibility.json'
if(Test-Path -LiteralPath $result){Remove-Item -LiteralPath $result}
$arguments=@('--verify-local-update','--feed',('"'+$releasePath+'"'),'--previous-package',('"'+$previousPath+'"'))
$process=Start-Process -FilePath $appPath -ArgumentList $arguments -WorkingDirectory $repoRoot -WindowStyle Hidden -PassThru
try {
    if(-not $process.WaitForExit(120000)){throw 'Isolated update compatibility verification timed out.'}
    if($process.ExitCode -ne 0){throw 'Older-version update compatibility failed. Inspect artifacts/update-compatibility.json.'}
    $evidence=Get-Content -LiteralPath $result -Raw | ConvertFrom-Json
    if(-not $evidence.updaterIdentical -or -not $evidence.localDownloadVerified -or $evidence.applied -or -not $evidence.isolated){throw 'Update compatibility evidence did not verify a local, isolated candidate.'}
    Write-Output "Older updater verified $($evidence.previous) -> $($evidence.candidate), unchanged package identity and verified local package; no install performed."
}
finally {$process.Dispose()}
