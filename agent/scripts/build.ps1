# Build script for CMSAgent solution
param (
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$OutputPath = "$PSScriptRoot\..\build\release",
    [switch]$Clean = $false
)

$ErrorActionPreference = "Stop"
$solutionPath = "$PSScriptRoot\..\CMSAgent.sln"

Write-Host "Building CMSAgent solution..."
Write-Host "Configuration: $Configuration"
Write-Host "Platform: $Platform"
Write-Host "Output Path: $OutputPath"

try {
    # Ensure .NET SDK is available
    if (-not (Get-Command "dotnet" -ErrorAction SilentlyContinue)) {
        throw ".NET SDK is not installed or not in PATH"
    }

    # Clean if requested
    if ($Clean) {
        Write-Host "Cleaning solution..."
        dotnet clean $solutionPath --configuration $Configuration
        if (Test-Path $OutputPath) {
            Remove-Item -Path $OutputPath -Recurse -Force
        }
    }

    # Restore packages
    Write-Host "Restoring NuGet packages..."
    dotnet restore $solutionPath

    # Build solution
    Write-Host "Building solution..."
    dotnet build $solutionPath --configuration $Configuration --no-restore

    # Create output directory if it doesn't exist
    if (-not (Test-Path $OutputPath)) {
        New-Item -Path $OutputPath -ItemType Directory -Force | Out-Null
    }

    # Publish projects
    $projects = @(
        "src\CMSAgent\CMSAgent.csproj",
        "src\CMSUpdater\CMSUpdater.csproj"
    )

    foreach ($project in $projects) {
        $projectName = [System.IO.Path]::GetFileNameWithoutExtension($project)
        Write-Host "Publishing $projectName..."
        
        dotnet publish $project `
            --configuration $Configuration `
            --no-restore `
            --self-contained true `
            --runtime win-x64 `
            --output "$OutputPath\$projectName"
    }

    Write-Host "Build completed successfully!" -ForegroundColor Green
}
catch {
    Write-Host "Build failed: $_" -ForegroundColor Red
    exit 1
}
