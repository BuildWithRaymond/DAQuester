# Validate the same functional grid contract in a published folder and inside release archives.
function Assert-WalkabilityAsset {
    param([object]$Asset,[object[]]$Routes)
    if(($Asset.schemaVersion -isnot [int] -and $Asset.schemaVersion -isnot [long]) -or $Asset.schemaVersion -ne 1 -or $Asset.serverProfile -cne 'datester-7.41' -or $Asset.sotpSha256 -isnot [string] -or $Asset.sotpSha256 -notmatch '^[a-fA-F0-9]{64}$'){
        throw 'Invalid supported-profile walkability metadata.'
    }
    if($Asset.maps -isnot [array] -or $Asset.missingMapIds -isnot [array]){
        throw 'Walkability coverage must include maps and missingMapIds arrays.'
    }
    $routeIndex=@{
    }
    foreach($route in $Routes){
        $id=[int]$route.id
        if($routeIndex.ContainsKey($id)){
            throw "Duplicate route map: $id"
        }
        $routeIndex[$id]=$route
    }
    $covered=@{
    }
    $missing=@{
    }
    foreach($map in $Asset.maps){
        if($map.id -isnot [int] -and $map.id -isnot [long]){
            throw 'Walkability map IDs must be integers.'
        }
        $id=[int]$map.id
        if(-not $routeIndex.ContainsKey($id) -or $covered.ContainsKey($id)){
            throw "Unknown or duplicate walkability map: $id"
        }
        $route=$routeIndex[$id]
        if(($map.width -isnot [int] -and $map.width -isnot [long]) -or ($map.height -isnot [int] -and $map.height -isnot [long]) -or $map.width -ne $route.width -or $map.height -ne $route.height -or $map.width -le 0 -or $map.height -le 0){
            throw "Walkability dimensions do not match recorded map: $id"
        }
        if($map.sourceSha256 -isnot [string] -or $map.sourceSha256 -notmatch '^[a-fA-F0-9]{64}$'){
            throw "Missing source digest for walkability map: $id"
        }
        if($map.blockedBits -isnot [string]){
            throw "Missing blocked-bit encoding for walkability map: $id"
        }
        try {
            $bits=[Convert]::FromBase64String([string]$map.blockedBits)
        }
        catch {
            throw "Invalid blocked-bit encoding for walkability map: $id"
        }
        $cells=[long]$map.width*[long]$map.height
        if($bits.Length -ne [Math]::Ceiling($cells/8.0)){
            throw "Incorrect blocked-bit length for walkability map: $id"
        }
        if([Convert]::ToBase64String($bits) -cne $map.blockedBits){
            throw "Noncanonical blocked-bit encoding for walkability map: $id"
        }
        $remainder=[int]($cells%8)
        if($remainder -gt 0 -and ($bits[$bits.Length-1] -band (255 -bxor ((1 -shl $remainder)-1))) -ne 0){
            throw "Unexpected padding flags for walkability map: $id"
        }
        $covered[$id]=$true
    }
    foreach($value in $Asset.missingMapIds){
        if($value -isnot [int] -and $value -isnot [long]){
            throw 'Missing walkability map IDs must be integers.'
        }
        $id=[int]$value
        if(-not $routeIndex.ContainsKey($id) -or $covered.ContainsKey($id) -or $missing.ContainsKey($id)){
            throw "Invalid missing walkability map: $id"
        }
        $missing[$id]=$true
    }
    foreach($id in $routeIndex.Keys){
        if(-not $covered.ContainsKey($id) -and -not $missing.ContainsKey($id)){
            throw "Unaccounted walkability map: $id"
        }
    }
    # The recorded Mileth-to-Suomi Tavern chain must work without missing intermediate client maps.
    foreach($id in @(500,3006,3210,3081,502,3008,504,3016,503,412,413)){
        if(-not $covered.ContainsKey($id)){
            throw "Missing required Mileth-to-Suomi walkability map: $id"
        }
    }
    [pscustomobject]@{
        profile=$Asset.serverProfile
        covered=$covered.Count
        missing=$missing.Count
        recorded=$routeIndex.Count
    }
}
