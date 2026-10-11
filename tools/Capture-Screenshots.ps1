param([string]$Executable='src/DAQuester.App/bin/Release/net10.0-windows/win-x86/DAQuester.exe')
$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
$appPath=if([IO.Path]::IsPathRooted($Executable)){$Executable}else{Join-Path $repoRoot $Executable}
if(-not(Test-Path -LiteralPath $appPath)){throw "Build the app first: $appPath"}
$result=Join-Path $repoRoot 'artifacts/screenshots.txt'
if(Test-Path -LiteralPath $result){Remove-Item -LiteralPath $result}
$process=Start-Process -FilePath $appPath -ArgumentList '--screenshots' -WorkingDirectory $repoRoot -WindowStyle Hidden -PassThru
try {
    if(-not $process.WaitForExit(60000)){throw 'Screenshot capture timed out. Inspect artifacts/screenshots.txt.'}
    if($process.ExitCode -ne 0){throw 'Screenshot capture failed. Inspect artifacts/screenshots.txt.'}
    if(-not(Test-Path -LiteralPath $result)){throw 'Screenshot capture produced no result.'}
    foreach($name in @('planner','characters','history','settings')) {
        Copy-Item -LiteralPath (Join-Path $repoRoot "artifacts/screenshots/$name.png") -Destination (Join-Path $repoRoot "docs/screenshots/$name.png") -Force
    }
    Get-Content -LiteralPath $result
}
finally {$process.Dispose()}
