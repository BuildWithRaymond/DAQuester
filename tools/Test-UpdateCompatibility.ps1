param(
[string]$Output='artifacts/release',
[string]$PreviousPackage='artifacts/update-fixtures/v1.0.7/BuildWithRaymond.DAQuester-1.0.7-full.nupkg',
[string]$Executable,
[switch]$DownloadPrevious
)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
function Resolve-RepoPath([string]$Value){
    if([IO.Path]::IsPathRooted($Value)){
        [IO.Path]::GetFullPath($Value)
    }else{
        [IO.Path]::GetFullPath((Join-Path $repoRoot $Value))
    }
}
if(-not $Executable) {
    [xml]$props=Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props')
    $payloadExecutable=Join-Path $repoRoot ('artifacts/package-'+$props.Project.PropertyGroup.Version+'/DAQuester.exe')
    $Executable=if(Test-Path -LiteralPath $payloadExecutable){
        $payloadExecutable
    }else{
        'src/DAQuester.App/bin/Release/net10.0-windows/win-x86/DAQuester.exe'
    }
}
$appPath=Resolve-RepoPath $Executable
$releasePath=Resolve-RepoPath $Output
$previousPath=Resolve-RepoPath $PreviousPackage
if(-not(Test-Path -LiteralPath $previousPath) -and $DownloadPrevious) {
    $artifactRoot=[IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))+[IO.Path]::DirectorySeparatorChar
    if(-not $previousPath.StartsWith($artifactRoot,[StringComparison]::OrdinalIgnoreCase)){
        throw 'Downloaded compatibility fixtures must remain inside artifacts.'
    }
    if([IO.Path]::GetFileName($previousPath) -ne 'BuildWithRaymond.DAQuester-1.0.7-full.nupkg'){
        throw 'The pinned download fixture is version 1.0.7.'
    }
    New-Item -ItemType Directory -Path (Split-Path $previousPath -Parent) -Force | Out-Null
    Invoke-WebRequest -Uri 'https://github.com/BuildWithRaymond/DAQuester/releases/download/v1.0.7/BuildWithRaymond.DAQuester-1.0.7-full.nupkg' -OutFile $previousPath
}
foreach($required in @($appPath,$previousPath,(Join-Path $releasePath 'releases.win.json'))){
    if(-not(Test-Path -LiteralPath $required)){
        throw "Compatibility fixture missing: $required"
    }
}
# Pin the published release, including cached fixtures; a local rebuild is not the public package.
$expectedDigest='B69A41DEFDBC9CDBC70FF2BA2B1449F56FE3D461FB88E97CD37ACFB309D9E706'
$digest=(Get-FileHash -LiteralPath $previousPath -Algorithm SHA256).Hash
if($digest -ne $expectedDigest){
    throw "The pinned public 1.0.7 package digest did not match. Expected $expectedDigest; received $digest."
}
$result=Join-Path $repoRoot 'artifacts/update-compatibility.json'
if(Test-Path -LiteralPath $result){
    Remove-Item -LiteralPath $result
}
$arguments=@('--verify-local-update','--feed',('"'+$releasePath+'"'),'--previous-package',('"'+$previousPath+'"'))
$process=Start-Process -FilePath $appPath -ArgumentList $arguments -WorkingDirectory $repoRoot -WindowStyle Hidden -PassThru
try {
    if(-not $process.WaitForExit(120000)){
        throw 'Isolated update compatibility verification timed out.'
    }
    if($process.ExitCode -ne 0){
        throw 'Older-version update compatibility failed. Inspect artifacts/update-compatibility.json.'
    }
    $evidence=Get-Content -LiteralPath $result -Raw | ConvertFrom-Json
    if(-not $evidence.updaterIdentical -or -not $evidence.localDownloadVerified -or $evidence.applied -or -not $evidence.isolated){
        throw 'Update compatibility evidence did not verify a local, isolated candidate.'
    }
    Write-Output "Older updater verified $($evidence.previous) -> $($evidence.candidate), unchanged package identity and verified local package; no install performed."
}
finally {
    $process.Dispose()
}
