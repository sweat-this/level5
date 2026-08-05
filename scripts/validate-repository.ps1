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

    $missingMeta = Get-ChildItem Assets -Recurse -File | Where-Object {
        $_.Extension -ne '.meta' -and
        -not $_.Name.StartsWith('.') -and
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

    if ($errors.Count -gt 0) {
        $errors | ForEach-Object { Write-Host "ERROR: $_" -ForegroundColor Red }
        exit 1
    }

    Write-Host 'Repository validation passed.'
}
finally {
    Pop-Location
}
