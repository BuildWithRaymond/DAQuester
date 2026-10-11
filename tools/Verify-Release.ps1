param([string]$Output='artifacts/release')
$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
$releasePath=if([IO.Path]::IsPathRooted($Output)){$Output}else{Join-Path $repoRoot $Output}
[xml]$props=Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props')
$version=[string]$props.Project.PropertyGroup.Version
$package="BuildWithRaymond.DAQuester-$version-full.nupkg"
foreach($required in @('DAQuester-win-Setup.exe','DAQuester-win-Portable.zip',$package,'releases.win.json','assets.win.json','SHA256SUMS.txt')) {
    $file=Join-Path $releasePath $required
    if(-not(Test-Path -LiteralPath $file) -or (Get-Item -LiteralPath $file).Length -eq 0){throw "Missing release asset: $required"}
}
$checked=@{}
foreach($line in Get-Content -LiteralPath (Join-Path $releasePath 'SHA256SUMS.txt')) {
    if($line -notmatch '^([a-f0-9]{64})  ([^/\\]+)$'){throw "Invalid checksum row: $line"}
    $expected=$Matches[1];$name=$Matches[2]
    if($name -in @('.','..') -or $checked.ContainsKey($name)){throw 'Unsafe or duplicate checksum filename.'}
    $actual=(Get-FileHash -LiteralPath (Join-Path $releasePath $name) -Algorithm SHA256).Hash.ToLowerInvariant()
    if($actual -ne $expected){throw "Checksum mismatch: $name"};$checked[$name]=$true
}
foreach($asset in Get-ChildItem -LiteralPath $releasePath -File | Where-Object Name -ne 'SHA256SUMS.txt') {
    if(-not $checked.ContainsKey($asset.Name)){throw "Asset missing from checksum list: $($asset.Name)"}
}
$feed=Get-Content -LiteralPath (Join-Path $releasePath 'releases.win.json') -Raw | ConvertFrom-Json
$full=@($feed.Assets | Where-Object {$_.Type -eq 'Full'})
if($full.Count -ne 1 -or $full[0].Version -ne $version -or $full[0].FileName -ne $package){throw 'Update feed does not match the release version and package.'}
$packageInfo=Get-Item -LiteralPath (Join-Path $releasePath $package)
if([long]$full[0].Size -ne $packageInfo.Length){throw 'Update feed package size mismatch.'}
if($full[0].SHA256 -notmatch '^[A-Fa-f0-9]{64}$' -or $full[0].SHA256.ToLowerInvariant() -ne (Get-FileHash -LiteralPath $packageInfo.FullName -Algorithm SHA256).Hash.ToLowerInvariant()){throw 'Update feed package digest mismatch.'}
if($full[0].SHA1 -notmatch '^[A-Fa-f0-9]{40}$' -or $full[0].SHA1.ToLowerInvariant() -ne (Get-FileHash -LiteralPath $packageInfo.FullName -Algorithm SHA1).Hash.ToLowerInvariant()){throw 'Update feed SHA1 mismatch.'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
foreach($archiveName in @('DAQuester-win-Portable.zip',$package)) {
    $archive=[IO.Compression.ZipFile]::OpenRead((Join-Path $releasePath $archiveName))
    try {
        $names=@($archive.Entries | ForEach-Object FullName)
        if($names | Where-Object {$_ -match '(^|[\\/])Data([\\/]|$)|(^|[\\/])(\.aws|\.git|\.env)([\\/]|$)'}){throw "Private data in archive: $archiveName"}
        if($names | Where-Object {$_ -match '\.pdb$'}){throw "Private source paths may remain in bundled symbols: $archiveName"}
        if($names | Where-Object {$_.Replace('\','/') -match '(^|/)(System\.Windows\.Forms|PresentationFramework|PresentationCore)\.dll$'}){throw "A Windows desktop framework remains in the Avalonia release: $archiveName"}
        $runtimeEntries=@($archive.Entries | Where-Object {$_.FullName.Replace('\','/').EndsWith('DAQuester.runtimeconfig.json',[StringComparison]::OrdinalIgnoreCase)})
        if($runtimeEntries.Count -ne 1){throw "Expected one application runtime configuration in $archiveName"}
        $reader=[IO.StreamReader]::new($runtimeEntries[0].Open())
        try {$runtimeConfig=$reader.ReadToEnd() | ConvertFrom-Json}
        finally {$reader.Dispose()}
        $frameworks=@($runtimeConfig.runtimeOptions.includedFrameworks)+@($runtimeConfig.runtimeOptions.frameworks)+@($runtimeConfig.runtimeOptions.framework)
        if($frameworks | Where-Object {$_.name -like 'Microsoft.WindowsDesktop.App*'}){throw "Windows desktop runtime dependency in $archiveName"}
        if(-not($runtimeConfig.runtimeOptions.includedFrameworks | Where-Object {$_.name -eq 'Microsoft.NETCore.App'})){throw "Missing self-contained core runtime in $archiveName"}
        foreach($required in @('DAQuester.exe','Avalonia.Base.dll','Avalonia.Controls.dll','Avalonia.Desktop.dll','Avalonia.Win32.dll','Avalonia.Skia.dll','SkiaSharp.dll','HarfBuzzSharp.dll','libSkiaSharp.dll','libHarfBuzzSharp.dll','LICENSE','ARBITER-LICENSE.txt','THIRD_PARTY_NOTICES.md','content/catalog.json','content/routes.json','content/recipes/spare-a-stick.v1.json','content/recipes/mundane-birth.v1.json','content/recipes/perfect-hairstyle.v1.json','content/recipes/entered-muisir-village.v1.json','content/recipes/emerald-dawn.v1.json','content/recipes/shinewood-faerie.v1.json','licenses/Velopack/LICENSE.txt','licenses/Avalonia/LICENSE.txt','licenses/SkiaSharp/LICENSE.txt','licenses/SkiaSharp/THIRD-PARTY-NOTICES.txt','licenses/HarfBuzzSharp/LICENSE.txt','licenses/HarfBuzzSharp/THIRD-PARTY-NOTICES.txt','licenses/MicroCom.Runtime/LICENSE.txt','licenses/Tmds.DBus.Protocol/LICENSE.txt','licenses/microsoft.netcore.app.runtime.win-x86/LICENSE.TXT','licenses/microsoft.netcore.app.runtime.win-x86/THIRD-PARTY-NOTICES.TXT','licenses/ui-dependencies.json','licenses/avalonia/package.nuspec')) {
            if(-not($names | Where-Object {$_.Replace('\','/').EndsWith($required,[StringComparison]::OrdinalIgnoreCase)})){throw "Missing $required in $archiveName"}
        }
    }
    finally {$archive.Dispose()}
}
Write-Output "Release verified: $version (assets, checksums, feed, licenses and private-data exclusion)."
