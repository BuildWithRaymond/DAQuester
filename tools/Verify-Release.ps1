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
        foreach($required in @('DAQuester.exe','LICENSE','ARBITER-LICENSE.txt','THIRD_PARTY_NOTICES.md','content/recipes/spare-a-stick.v1.json','licenses/Velopack/LICENSE.txt','licenses/microsoft.netcore.app.runtime.win-x86/LICENSE.TXT','licenses/microsoft.netcore.app.runtime.win-x86/THIRD-PARTY-NOTICES.TXT','licenses/microsoft.windowsdesktop.app.runtime.win-x86/LICENSE')) {
            if(-not($names | Where-Object {$_.Replace('\','/').EndsWith($required,[StringComparison]::OrdinalIgnoreCase)})){throw "Missing $required in $archiveName"}
        }
    }
    finally {$archive.Dispose()}
}
Write-Output "Release verified: $version (assets, checksums, feed, licenses and private-data exclusion)."
