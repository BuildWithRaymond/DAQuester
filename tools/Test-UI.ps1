param([string]$Executable='src/DAQuester.App/bin/Release/net10.0-windows/win-x86/DAQuester.exe')
$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
$appPath=if([IO.Path]::IsPathRooted($Executable)){$Executable}else{Join-Path $repoRoot $Executable}
if(-not(Test-Path -LiteralPath $appPath)){throw "Build the app first: $appPath"}
$result=Join-Path $repoRoot 'artifacts/app-smoke.txt'
if(Test-Path -LiteralPath $result){Remove-Item -LiteralPath $result}
$process=Start-Process -FilePath $appPath -ArgumentList '--smoke' -WorkingDirectory $repoRoot -WindowStyle Hidden -PassThru
try {
    if(-not $process.WaitForExit(120000)){throw 'UI checks timed out. Inspect the smoke process and artifacts.'}
    if($process.ExitCode -ne 0){throw "UI checks failed (exit $($process.ExitCode)). See artifacts/app-smoke.txt."}
    if(-not(Test-Path -LiteralPath $result)){throw 'UI checks did not produce a result.'}
    $summary=[IO.File]::ReadAllText($result)
    if($summary.StartsWith('Running')){throw 'UI checks did not finish.'}
    Write-Output $summary
}
finally {$process.Dispose()}
