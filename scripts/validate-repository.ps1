$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repositoryRoot

try {
    $errors = [System.Collections.Generic.List[string]]::new()
    $trackedFiles = @(git ls-files)

    $forbiddenTrackedPaths = $trackedFiles | Where-Object {
        (Test-Path -LiteralPath $_) -and (
            $_ -match '(^|/)(Library|Temp|Logs|UserSettings|Assets/_Recovery)(/|$)' -or
            $_ -match '(^|/)\.DS_Store$'
        )
    }
    foreach ($path in $forbiddenTrackedPaths) {
        $errors.Add("Generated or machine-local file is tracked: $path")
    }

    # Unity ignores any path segment ending in '~' (and hidden files), so it never generates
    # .meta files for them. Requiring one here would fail on folders that are deliberately
    # excluded from the asset database, such as Assets/Scripts/menu_start/Legacy~.
    $missingMeta = Get-ChildItem Assets -Recurse -File | Where-Object {
        $_.Extension -ne '.meta' -and
        -not $_.Name.StartsWith('.') -and
        (($_.FullName -replace '\\', '/') -notmatch '/[^/]*~/') -and
        -not (Test-Path ($_.FullName + '.meta'))
    }
    foreach ($asset in $missingMeta) {
        $errors.Add("Unity asset is missing its .meta file: $($asset.FullName.Substring($repositoryRoot.Length + 1))")
    }

    $synchronousNetworkPatterns = 'HttpWebRequest|WebClient|System\.Net\.NetworkInformation\.Ping'
    $networkMatches = @(Get-ChildItem Assets\Scripts -Recurse -Filter *.cs |
        Select-String -Pattern $synchronousNetworkPatterns)
    foreach ($match in $networkMatches) {
        $relativePath = $match.Path.Substring($repositoryRoot.Length + 1)
        $errors.Add("Synchronous networking API found at ${relativePath}:$($match.LineNumber)")
    }

    # AUD-111: production menu code must use explicit, narrowly-scoped failure boundaries instead
    # of broad catch(Exception). Everything under Assets/Scripts/menu_* is in scope except
    # Legacy~ folders, tests, and editor tooling. The three files below are the deliberate
    # exception: PendingProgressionStore.cs, ProgressionResultStore.cs, and ProgressionService.cs
    # each catch (System.)Exception only around a narrow persistence read/write/JSON-parse call,
    # where file I/O and JsonUtility can throw a heterogeneous mix of exception types (IOException,
    # UnauthorizedAccessException, ArgumentException, ...) with no single verifiable type to name.
    $menuGenericCatchAllowlist = @(
        'Assets/Scripts/menu_progression/PendingProgressionStore.cs'
        'Assets/Scripts/menu_progression/ProgressionResultStore.cs'
        'Assets/Scripts/menu_progression/ProgressionService.cs'
    )
    $menuCatchPattern = 'catch\s*\(\s*(System\.)?Exception'
    $menuCsFiles = Get-ChildItem Assets\Scripts -Recurse -Filter *.cs | Where-Object {
        $normalized = $_.FullName -replace '\\', '/'
        $normalized -match '/menu_[^/]*/' -and
            $normalized -notmatch '/[^/]*~/' -and
            $normalized -notmatch '/(Tests|Editor)/'
    }
    foreach ($file in $menuCsFiles) {
        $relativePath = ($file.FullName.Substring($repositoryRoot.Length + 1)) -replace '\\', '/'
        if ($menuGenericCatchAllowlist -contains $relativePath) {
            continue
        }
        $catchMatches = @(Select-String -LiteralPath $file.FullName -Pattern $menuCatchPattern)
        foreach ($match in $catchMatches) {
            $errors.Add("Unapproved generic catch(Exception) in production menu code at ${relativePath}:$($match.LineNumber) (AUD-111)")
        }
    }

    if ($errors.Count -gt 0) {
        $errors | ForEach-Object { Write-Host "ERROR: $_" -ForegroundColor Red }
        exit 1
    }

    Write-Host 'Repository validation passed.'
}
finally {
    Pop-Location
}
