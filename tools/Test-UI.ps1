param([string]$Executable='src/DAQuester.App/bin/Release/net10.0-windows/win-x86/DAQuester.exe')
$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
$appPath=if([IO.Path]::IsPathRooted($Executable)){$Executable}else{Join-Path $repoRoot $Executable}
if(-not(Test-Path -LiteralPath $appPath)){throw "Build the app first: $appPath"}
$result=Join-Path $repoRoot 'artifacts/app-smoke.txt'
$evidence=Join-Path $repoRoot 'artifacts/app-smoke.json'
foreach($file in @($result,$evidence)){if(Test-Path -LiteralPath $file){Remove-Item -LiteralPath $file}}
$process=Start-Process -FilePath $appPath -ArgumentList '--smoke' -WorkingDirectory $repoRoot -WindowStyle Hidden -PassThru
try {
    if(-not $process.WaitForExit(240000)){throw 'UI checks timed out. Inspect the smoke process and artifacts.'}
    if($process.ExitCode -ne 0){throw "UI checks failed (exit $($process.ExitCode)). See artifacts/app-smoke.txt."}
    if(-not(Test-Path -LiteralPath $result)){throw 'UI checks did not produce a result.'}
    $summary=[IO.File]::ReadAllText($result)
    if($summary.StartsWith('Running')){throw 'UI checks did not finish.'}
    if(-not(Test-Path -LiteralPath $evidence)){throw 'Avalonia UI checks did not produce structured evidence.'}
    $checks=Get-Content -LiteralPath $evidence -Raw | ConvertFrom-Json
    if($checks.framework -ne 'Avalonia' -or -not $checks.isolated -or -not $checks.synthetic -or $checks.failed -ne 0){throw 'Avalonia UI checks require successful, isolated synthetic verification.'}
    if(@($checks.checks).Count -lt 20){throw 'Avalonia UI checks did not preserve behavioral coverage.'}
    foreach($required in @('planner','characters','history','settings','empty','populated','disconnected','running','paused','completed','blocked','error','loading')) {
        if($required -notin @($checks.states)){throw "Avalonia UI evidence is missing state: $required"}
    }
    foreach($render in $checks.renders) {
        $renderPath=Join-Path $repoRoot $render
        if(-not(Test-Path -LiteralPath $renderPath) -or (Get-Item -LiteralPath $renderPath).Length -lt 1000){throw "UI render is missing or empty: $render"}
    }
    if(@($checks.renders).Count -lt 12){throw 'Avalonia UI checks did not produce the complete render matrix.'}
    Write-Output $summary
}
finally {$process.Dispose()}
