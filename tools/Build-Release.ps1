param([string]$Output='artifacts/release')
$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot
try {
    [xml]$props=Get-Content -LiteralPath Directory.Build.props
    $version=[string]$props.Project.PropertyGroup.Version
    if($version -notmatch '^\d+\.\d+\.\d+$'){throw 'Stable packaging requires a major.minor.patch version.'}
    $payload=Join-Path $repoRoot "artifacts/package-$version"
    $releasePath=if([IO.Path]::IsPathRooted($Output)){$Output}else{Join-Path $repoRoot $Output}
    if(Test-Path -LiteralPath $payload){throw "Use a clean package directory: $payload already exists."}
    if(Test-Path -LiteralPath $releasePath){throw "Use a clean output directory: $releasePath already exists."}
    dotnet publish src/DAQuester.App -c Release -r win-x86 --self-contained true -o $payload -m:1 -p:UseSharedCompilation=false
    if($LASTEXITCODE -ne 0){throw 'Application publish failed.'}
    if(Test-Path -LiteralPath (Join-Path $payload 'Data')){throw 'User Data must never enter the release payload.'}
    $assets=Get-Content src/DAQuester.App/obj/project.assets.json -Raw | ConvertFrom-Json
    $runtimePacks=@($assets.project.frameworks.PSObject.Properties.Value.downloadDependencies | Where-Object {$_.name -match '^Microsoft\.(NETCore|WindowsDesktop)\.App\.Runtime\.win-x86$'})
    if($runtimePacks.Count -ne 2){throw 'Expected core and desktop runtime packs for license inclusion.'}
    foreach($runtimePack in $runtimePacks) {
        $runtimeVersion=($runtimePack.version -split ',')[0].Trim('[',']',' ')
        $packageLocation=$null
        foreach($packageFolder in $assets.packageFolders.PSObject.Properties.Name) {
            $candidate=Join-Path $packageFolder ($runtimePack.name.ToLowerInvariant()+'/'+$runtimeVersion)
            if(Test-Path -LiteralPath $candidate){$packageLocation=$candidate;break}
        }
        if(-not $packageLocation){throw "Cannot locate notices for $($runtimePack.name) $runtimeVersion"}
        $noticeFiles=@(Get-ChildItem -LiteralPath $packageLocation -File | Where-Object {$_.Name -match '^(LICENSE|NOTICE|THIRD-PARTY-NOTICES)(\..*)?$'})
        if(-not($noticeFiles | Where-Object Name -Match '^LICENSE')){throw "Missing runtime license: $packageLocation"}
        $noticeDestination=Join-Path $payload ('licenses/'+$runtimePack.name.ToLowerInvariant())
        New-Item -ItemType Directory -Path $noticeDestination -Force | Out-Null
        foreach($notice in $noticeFiles){Copy-Item -LiteralPath $notice.FullName -Destination (Join-Path $noticeDestination $notice.Name)}
    }
    dotnet tool restore
    if($LASTEXITCODE -ne 0){throw 'Release tool restore failed.'}
    dotnet tool run vpk -- pack --packId BuildWithRaymond.DAQuester --packTitle DAQuester --packAuthors BuildWithRaymond --packVersion $version --packDir $payload --mainExe DAQuester.exe --runtime win-x86 --outputDir $releasePath --releaseNotes "docs/releases/v$version.md" --shortcuts StartMenuRoot --delta None
    if($LASTEXITCODE -ne 0){throw 'Velopack packaging failed.'}
    foreach($suffix in @('Setup.exe','Portable.zip')) {
        Move-Item -LiteralPath (Join-Path $releasePath "BuildWithRaymond.DAQuester-win-$suffix") -Destination (Join-Path $releasePath "DAQuester-win-$suffix")
    }
    $index=Join-Path $releasePath 'assets.win.json'
    $indexText=[IO.File]::ReadAllText($index).Replace('BuildWithRaymond.DAQuester-win-Setup.exe','DAQuester-win-Setup.exe').Replace('BuildWithRaymond.DAQuester-win-Portable.zip','DAQuester-win-Portable.zip')
    [IO.File]::WriteAllText($index,$indexText,[Text.UTF8Encoding]::new($false))
    $rows=Get-ChildItem -LiteralPath $releasePath -File | Sort-Object Name | ForEach-Object {
        (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()+'  '+$_.Name
    }
    $rows | Set-Content -LiteralPath (Join-Path $releasePath 'SHA256SUMS.txt') -Encoding ASCII
    & ./tools/Verify-Release.ps1 -Output $releasePath
    Write-Output "Release $version ready: $releasePath"
}
finally {Pop-Location}
