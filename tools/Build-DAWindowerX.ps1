param([switch]$IncludeTestFixture)
$ErrorActionPreference='Stop'
# MSBuild can inherit PowerShell 7's module search path when launching Windows
# PowerShell. Load this host's utility module, including its hash function.
Import-Module (Join-Path $PSHOME 'Modules/Microsoft.PowerShell.Utility/Microsoft.PowerShell.Utility.psd1') -Force
$repoRoot=Split-Path $PSScriptRoot -Parent
$pin=Get-Content -LiteralPath (Join-Path $repoRoot 'native/DAWindowerX.json') -Raw | ConvertFrom-Json
if($pin.Commit -notmatch '^[a-f0-9]{40}$' -or $pin.Library -ne 'DAWindowerX.dll' -or $pin.Repository -ne 'https://github.com/BuildWithRaymond/DAWindower.git'){
    throw 'Invalid DAWindower X source pin.'
}
$patch=Join-Path $repoRoot 'native/DAWindowerX-daquester.patch'
if($pin.Patch -cne 'DAWindowerX-daquester.patch' -or $pin.PatchSHA256 -notmatch '^[a-f0-9]{64}$' -or
    (Get-FileHash -LiteralPath $patch -Algorithm SHA256).Hash.ToLowerInvariant() -cne $pin.PatchSHA256){
    throw 'DAWindower X integration patch differs from its source pin.'
}
$fixtureIdentity=$pin.Commit+':'+$pin.PatchSHA256
$nativeRoot=Join-Path $repoRoot 'artifacts/native/DAWindowerX'
$cache=Join-Path $nativeRoot $pin.Commit
$source=Join-Path $cache 'source'
$build=Join-Path $cache 'build'
$package=Join-Path $cache 'package'
$output=Join-Path $nativeRoot 'output'
$library=Join-Path $output $pin.Library
$metadataPath=Join-Path $output 'source.json'
$fixturePin=Join-Path $output 'WindowingFixture.commit'
if((Test-Path -LiteralPath $library) -and (Test-Path -LiteralPath $metadataPath)){
    $saved=Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
    if($saved.Commit -eq $pin.Commit -and $saved.Repository -eq $pin.Repository -and $saved.Version -eq $pin.Version -and $saved.Library -eq $pin.Library -and $saved.PatchSHA256 -ceq $pin.PatchSHA256 -and
        $saved.SHA256 -eq (Get-FileHash -LiteralPath $library -Algorithm SHA256).Hash.ToLowerInvariant()){
        Write-Output "DAWindower X $($pin.Version) ready (cached $($pin.Commit.Substring(0,7)))."
        if(-not $IncludeTestFixture -or ((Test-Path -LiteralPath (Join-Path $output 'WindowingFixture.exe')) -and
            (Test-Path -LiteralPath $fixturePin) -and [IO.File]::ReadAllText($fixturePin).Trim() -ceq $fixtureIdentity)){ return }
    }
}
if(-not(Test-Path -LiteralPath (Join-Path $source '.git'))){
    if(Test-Path -LiteralPath $source){ throw 'Native source directory exists without its pinned Git checkout.' }
    New-Item -ItemType Directory -Path $source -Force | Out-Null
    git -C $source init --quiet
    if($LASTEXITCODE -ne 0){ throw 'Native source initialization failed.' }
    git -C $source remote add origin $pin.Repository
    if($LASTEXITCODE -ne 0){ throw 'Native source remote setup failed.' }
    git -C $source fetch --quiet --depth 1 origin $pin.Commit
    if($LASTEXITCODE -ne 0){ throw 'Pinned DAWindower X source download failed.' }
    git -C $source checkout --quiet --detach FETCH_HEAD
    if($LASTEXITCODE -ne 0){ throw 'Native source checkout failed.' }
}
$actual=git -C $source rev-parse HEAD
if($LASTEXITCODE -ne 0 -or $actual -ne $pin.Commit){ throw 'Native source revision differs from its pin.' }
$expectedPatch=[IO.File]::ReadAllText($patch).Replace("`r`n","`n").TrimEnd()
$changes=git -c core.safecrlf=false -C $source diff --binary --no-ext-diff HEAD
if($LASTEXITCODE -ne 0){ throw 'Cannot verify native source changes.' }
if(-not $changes){
    git -C $source apply --check $patch
    if($LASTEXITCODE -ne 0){ throw 'The pinned integration patch cannot be applied.' }
    git -C $source apply $patch
    if($LASTEXITCODE -ne 0){ throw 'Native integration patch failed.' }
    $changes=git -c core.safecrlf=false -C $source diff --binary --no-ext-diff HEAD
    if($LASTEXITCODE -ne 0){ throw 'Cannot verify patched native source.' }
}
if(($changes -join "`n").TrimEnd() -cne $expectedPatch){ throw 'Native source has changes beyond the pinned integration patch.' }
$license=Join-Path $repoRoot 'src/DAQuester.App/Licenses/DAWindowerX/LICENSE.txt'
if([IO.File]::ReadAllText((Join-Path $source 'LICENSE')).Replace("`r`n","`n") -ne [IO.File]::ReadAllText($license).Replace("`r`n","`n")){
    throw 'DAWindower X license differs from the retained upstream notice.'
}
$cmake=(Get-Command cmake -ErrorAction SilentlyContinue).Source
if(-not $cmake){
    $vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    if(Test-Path -LiteralPath $vswhere){
        $cmake=@(& $vswhere -products '*' -version '[17.0,18.0)' -find 'Common7/IDE/CommonExtensions/Microsoft/CMake/CMake/bin/cmake.exe') | Select-Object -First 1
    }
}
if(-not $cmake){ throw 'Install CMake and the Visual Studio 2022 C++ x86 build tools to build DAWindower X.' }
$testing=if($IncludeTestFixture){'ON'}else{'OFF'}
& $cmake -S $source -B $build -G 'Visual Studio 17 2022' -A Win32 "-DBUILD_TESTING=$testing" '-DDAW_BUILD_X=ON' '-DDAW_BUILD_PROXY_TESTS=OFF' "-DCMAKE_INSTALL_PREFIX=$package" '-DCMAKE_CXX_FLAGS_RELEASE=/O2 /Ob2 /DNDEBUG /Brepro' '-DCMAKE_SHARED_LINKER_FLAGS_RELEASE=/INCREMENTAL:NO /Brepro'
if($LASTEXITCODE -ne 0){ throw 'DAWindower X native configuration failed.' }
& $cmake --build $build --config Release --target DAWindower --parallel 2
if($LASTEXITCODE -ne 0){ throw 'DAWindower X native build failed.' }
& $cmake --install $build --config Release
if($LASTEXITCODE -ne 0){ throw 'DAWindower X native install failed.' }
python (Join-Path $source 'tools/check_package.py') $package --edition x
if($LASTEXITCODE -ne 0){ throw 'DAWindower X x86 package validation failed.' }
New-Item -ItemType Directory -Path $output -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $package $pin.Library) -Destination $library -Force
$metadata=[ordered]@{Repository=$pin.Repository;Commit=$pin.Commit;Version=$pin.Version;Library=$pin.Library;PatchSHA256=$pin.PatchSHA256;SHA256=(Get-FileHash -LiteralPath $library -Algorithm SHA256).Hash.ToLowerInvariant()}
[IO.File]::WriteAllText($metadataPath, ($metadata | ConvertTo-Json)+[Environment]::NewLine, [Text.UTF8Encoding]::new($false))
if($IncludeTestFixture){
    & $cmake --build $build --config Release --target x_launch_fixture --parallel 2
    if($LASTEXITCODE -ne 0){ throw 'Synthetic windowing fixture build failed.' }
    Copy-Item -LiteralPath (Join-Path $build 'Release/x_launch_fixture.exe') -Destination (Join-Path $output 'WindowingFixture.exe') -Force
    [IO.File]::WriteAllText($fixturePin, $fixtureIdentity, [Text.UTF8Encoding]::new($false))
}
Write-Output "DAWindower X $($pin.Version) built from $($pin.Commit)."
