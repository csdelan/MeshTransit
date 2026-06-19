param(
    [string]$NuGetPath = "\\BART\MyNuget",
    [string]$Configuration = "Release",
    [string]$ReleaseNotes = "",
    [switch]$NoOverwrite
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptRoot "..")
$artifactsPath = Join-Path $repoRoot "artifacts\nuget"

# Packable libraries, in dependency order.
$projects = @(
    Join-Path $repoRoot "src\MeshTransit.Contracts\MeshTransit.Contracts.csproj"
    Join-Path $repoRoot "src\MeshTransit\MeshTransit.csproj"
    Join-Path $repoRoot "src\MeshTransit.Client\MeshTransit.Client.csproj"
)

foreach ($project in $projects) {
    if (-not (Test-Path -LiteralPath $project)) {
        throw "Project not found: $project"
    }
}

$isFileSource = [System.IO.Path]::IsPathRooted($NuGetPath) -or $NuGetPath.StartsWith("\\")

if ($isFileSource -and -not (Test-Path -LiteralPath $NuGetPath)) {
    New-Item -ItemType Directory -Path $NuGetPath | Out-Null
}

if (Test-Path -LiteralPath $artifactsPath) {
    Remove-Item -LiteralPath $artifactsPath -Recurse -Force
}
New-Item -ItemType Directory -Path $artifactsPath | Out-Null

$packArgs = @("--configuration", $Configuration, "--output", $artifactsPath)
if ($ReleaseNotes -ne "") {
    $packArgs += "/p:PackageReleaseNotes=$ReleaseNotes"
}

Push-Location $repoRoot
try {
    foreach ($project in $projects) {
        dotnet build $project --configuration $Configuration
    }

    foreach ($project in $projects) {
        dotnet pack $project @packArgs --no-build
    }

    $packages = Get-ChildItem -LiteralPath $artifactsPath -Filter "*.nupkg" -File |
        Where-Object { $_.Name -notlike "*.symbols.nupkg" }

    if (-not $packages) {
        throw "No NuGet packages were created in $artifactsPath"
    }

    foreach ($package in $packages) {
        if ($isFileSource) {
            $existing = Get-ChildItem -LiteralPath $NuGetPath -Filter $package.Name -File -Recurse -ErrorAction SilentlyContinue |
                Select-Object -First 1
            $targetPath = Join-Path $NuGetPath $package.Name

            if ($existing) {
                if ($NoOverwrite) {
                    Write-Host "Skipping existing $($package.Name)"
                    continue
                }
                $targetPath = $existing.FullName
                Remove-Item -LiteralPath $existing.FullName -Force
            }

            Copy-Item -LiteralPath $package.FullName -Destination $targetPath -Force
            Write-Host "Published $($package.Name) to $targetPath"
        }
        else {
            dotnet nuget push $package.FullName --source $NuGetPath --skip-duplicate
        }
    }
}
finally {
    Pop-Location
}
