param([string]$Output='artifacts/release')
$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot
try {
    . (Join-Path $PSScriptRoot 'Assert-WalkabilityAsset.ps1')
    [xml]$props=Get-Content -LiteralPath Directory.Build.props
    $version=[string]$props.Project.PropertyGroup.Version
    if($version -notmatch '^\d+\.\d+\.\d+$'){throw 'Stable packaging requires a major.minor.patch version.'}
    $payload=Join-Path $repoRoot "artifacts/package-$version"
    $releasePath=if([IO.Path]::IsPathRooted($Output)){$Output}else{Join-Path $repoRoot $Output}
    if(Test-Path -LiteralPath $payload){throw "Use a clean package directory: $payload already exists."}
    if(Test-Path -LiteralPath $releasePath){throw "Use a clean output directory: $releasePath already exists."}
    # A running checkout must not hold up release packaging or be replaced by it.
    $buildOutput=Join-Path $repoRoot "artifacts/build-package-$version/"
    # Local portable symbols embed checkout paths; omit them from public release builds only.
    # Rebuild every project so unchanged Core intermediates cannot retain old CodeView paths.
    dotnet build src/DAQuester.App -c Release -r win-x86 --self-contained true --force --no-incremental -m:1 -p:UseSharedCompilation=false -p:DebugType=None -p:DebugSymbols=false -p:CopyOutputSymbolsToPublishDirectory=false "-p:OutDir=$buildOutput"
    if($LASTEXITCODE -ne 0){throw 'Clean release compilation failed.'}
    dotnet publish src/DAQuester.App -c Release -r win-x86 --self-contained true --no-build -o $payload -m:1 -p:UseSharedCompilation=false -p:DebugType=None -p:DebugSymbols=false -p:CopyOutputSymbolsToPublishDirectory=false "-p:OutDir=$buildOutput"
    if($LASTEXITCODE -ne 0){throw 'Application publish failed.'}
    if(Test-Path -LiteralPath (Join-Path $payload 'Data')){throw 'User Data must never enter the release payload.'}
    if(Get-ChildItem -LiteralPath $payload -File -Recurse -Filter '*.pdb'){throw 'Release payload must exclude symbols with local source paths.'}
    if(Get-ChildItem -LiteralPath $payload -File -Recurse | Where-Object {$_.Extension -eq '.map' -or $_.Name -in @('ia.dat','sotp.dat')}){throw 'Raw game map and collision archives must never enter the release payload.'}
    $walkabilityPath=Join-Path $payload 'content/maps/datester-7.41.walkability.json'
    if(-not(Test-Path -LiteralPath $walkabilityPath)){throw 'Missing supported-profile functional walkability asset.'}
    $walkability=Get-Content -LiteralPath $walkabilityPath -Raw | ConvertFrom-Json
    $routeRecords=Get-Content -LiteralPath (Join-Path $payload 'content/routes.json') -Raw | ConvertFrom-Json
    $coverage=Assert-WalkabilityAsset -Asset $walkability -Routes $routeRecords
    Write-Output "Functional walkability: $($coverage.covered)/$($coverage.recorded) maps ($($coverage.missing) unavailable)."
    $privatePaths=@($repoRoot,$repoRoot.Replace('\','/'),$repoRoot.Replace('\','\\'))
    foreach($file in Get-ChildItem -LiteralPath $payload -File -Recurse) {
        $bytes=[IO.File]::ReadAllBytes($file.FullName)
        $decoded=@([Text.Encoding]::UTF8.GetString($bytes),[Text.Encoding]::Unicode.GetString($bytes))
        foreach($text in $decoded){foreach($privatePath in $privatePaths){if($text.IndexOf($privatePath,[StringComparison]::OrdinalIgnoreCase) -ge 0){throw "Local checkout path embedded in release payload: $($file.Name)"}}}
    }
    $assets=Get-Content src/DAQuester.App/obj/project.assets.json -Raw | ConvertFrom-Json
    # SDK downloads can include unused packs; check the frameworks actually referenced and published.
    $frameworkReferences=@($assets.project.frameworks.PSObject.Properties.Value.frameworkReferences.PSObject.Properties.Name)
    if($frameworkReferences | Where-Object {$_ -like 'Microsoft.WindowsDesktop.App*'}){throw 'Avalonia releases must not reference the Windows desktop runtime.'}
    $runtimeConfig=Get-Content -LiteralPath (Join-Path $payload 'DAQuester.runtimeconfig.json') -Raw | ConvertFrom-Json
    $publishedFrameworks=@($runtimeConfig.runtimeOptions.includedFrameworks)+@($runtimeConfig.runtimeOptions.frameworks)+@($runtimeConfig.runtimeOptions.framework)
    if($publishedFrameworks | Where-Object {$_.name -like 'Microsoft.WindowsDesktop.App*'}){throw 'Avalonia releases must not include the Windows desktop runtime.'}
    if(-not($runtimeConfig.runtimeOptions.includedFrameworks | Where-Object {$_.name -eq 'Microsoft.NETCore.App'})){throw 'Expected the self-contained core runtime in the published application.'}
    $runtimePacks=@($assets.project.frameworks.PSObject.Properties.Value.downloadDependencies | Where-Object {$_.name -eq 'Microsoft.NETCore.App.Runtime.win-x86'})
    if(-not($runtimePacks | Where-Object {$_.name -eq 'Microsoft.NETCore.App.Runtime.win-x86'})){throw 'Expected the self-contained x86 core runtime pack.'}
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
    # Preserve notices from the exact restored UI/rendering packages rather than a moving source branch.
    $dependencies=@()
    foreach($library in $assets.libraries.PSObject.Properties | Where-Object {$_.Name -match '^(Avalonia|SkiaSharp|HarfBuzzSharp|MicroCom\.Runtime|Tmds\.DBus\.Protocol)([./]|$)'}) {
        $identity=$library.Name -split '/';$packageName=$identity[0];$packageVersion=$identity[1]
        $packageLocation=$null
        foreach($packageFolder in $assets.packageFolders.PSObject.Properties.Name) {
            $candidate=Join-Path $packageFolder ($packageName.ToLowerInvariant()+'/'+$packageVersion)
            if(Test-Path -LiteralPath $candidate){$packageLocation=$candidate;break}
        }
        if(-not $packageLocation){throw "Cannot locate UI package: $($library.Name)"}
        $noticeDestination=Join-Path $payload ('licenses/'+$packageName.ToLowerInvariant())
        New-Item -ItemType Directory -Path $noticeDestination -Force | Out-Null
        $noticeFiles=@(Get-ChildItem -LiteralPath $packageLocation -File -Recurse | Where-Object {$_.Name -match '^(LICENSE|LICENCE|NOTICE|THIRD-PARTY-NOTICES)(\..*)?$'})
        foreach($notice in $noticeFiles){Copy-Item -LiteralPath $notice.FullName -Destination (Join-Path $noticeDestination $notice.Name) -Force}
        $nuspec=Get-ChildItem -LiteralPath $packageLocation -File -Filter '*.nuspec' | Select-Object -First 1
        if(-not $nuspec){throw "Missing package metadata: $($library.Name)"}
        Copy-Item -LiteralPath $nuspec.FullName -Destination (Join-Path $noticeDestination 'package.nuspec')
        [xml]$metadata=Get-Content -LiteralPath $nuspec.FullName
        $license=$metadata.package.metadata.license
        $licenseText=if($license -is [Xml.XmlElement]){$license.InnerText}else{[string]$license}
        $dependencies+=@{name=$packageName;version=$packageVersion;license=$licenseText;notices=@($noticeFiles | ForEach-Object Name)}
    }
    if(-not($dependencies | Where-Object {$_.name -eq 'Avalonia'})){throw 'Avalonia dependencies were not restored.'}
    $dependencies | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $payload 'licenses/ui-dependencies.json') -Encoding UTF8
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
