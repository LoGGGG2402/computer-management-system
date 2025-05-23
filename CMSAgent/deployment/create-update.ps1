# Create Update Package Script
# This script builds the CMSAgent and creates an update package

# Configuration
$version = "1.0.1" # Update this for each release
$configuration = "Release"
$projectPath = "src\CMSAgent.Service\CMSAgent.Service.csproj"
$outputDir = "deployment\output"
$updateOutputDir = "$outputDir\updates"
$backupDir = "$outputDir\backup"
$iconPath = Join-Path $PSScriptRoot "icon.ico"

# Create output directories
New-Item -ItemType Directory -Force -Path $outputDir
New-Item -ItemType Directory -Force -Path $updateOutputDir
New-Item -ItemType Directory -Force -Path $backupDir

# Check if version already exists
$existingUpdate = Get-ChildItem -Path $updateOutputDir -Filter "CMSAgent.v$version.zip" -ErrorAction SilentlyContinue
if ($existingUpdate) {
    Write-Host "Version $version already exists. Please update version number."
    exit 1
}

# Backup existing files if they exist
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
if (Test-Path $updateOutputDir) {
    Compress-Archive -Path $updateOutputDir\* -DestinationPath "$backupDir\updates_backup_$timestamp.zip" -Force
}

# Update version in appsettings.json
$appSettingsPath = "src\CMSAgent.Service\appsettings.json"
$appSettingsContent = Get-Content $appSettingsPath -Raw | ConvertFrom-Json
$appSettingsContent.AppSettings.Version = $version
$appSettingsContent | ConvertTo-Json -Depth 10 | Set-Content -Path $appSettingsPath
Write-Host "Updated version in appsettings.json to $version"

# Build the project
Write-Host "Building CMSAgent.Service..."
dotnet publish $projectPath -c $configuration -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:PublishReadyToRun=true /p:ApplicationIcon=$iconPath

# Create update package directory
$updatePackageDir = "$updateOutputDir\v$version"
New-Item -ItemType Directory -Force -Path $updatePackageDir
New-Item -ItemType Directory -Force -Path "$updatePackageDir\Updater"

# Copy main service executable
Copy-Item "src\CMSAgent.Service\bin\$configuration\net8.0\win-x64\publish\CMSAgent.Service.exe" -Destination $updatePackageDir

# Copy appsettings.json
Copy-Item "src\CMSAgent.Service\appsettings.json" -Destination $updatePackageDir

# Copy updater
Copy-Item "src\CMSUpdater\bin\$configuration\net8.0\win-x64\publish\CMSUpdater.exe" -Destination "$updatePackageDir\Updater"

# Verify file integrity
$serviceHash = Get-FileHash -Path "$updatePackageDir\CMSAgent.Service.exe" -Algorithm SHA256
$updaterHash = Get-FileHash -Path "$updatePackageDir\Updater\CMSUpdater.exe" -Algorithm SHA256
Write-Host "CMSAgent.Service.exe SHA256: $($serviceHash.Hash)"
Write-Host "CMSUpdater.exe SHA256: $($updaterHash.Hash)"

# Create update manifest
$manifest = @{
    version = $version
    releaseDate = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ssZ")
    files = @(
        @{
            path = "CMSAgent.Service.exe"
            checksum = $serviceHash.Hash
        },
        @{
            path = "appsettings.json"
            checksum = (Get-FileHash "$updatePackageDir\appsettings.json" -Algorithm SHA256).Hash
        },
        @{
            path = "Updater\CMSUpdater.exe"
            checksum = $updaterHash.Hash
        }
    )
}

# Save manifest
$manifest | ConvertTo-Json -Depth 10 | Set-Content "$updatePackageDir\manifest.json"

# Create update package (ZIP)
$zipFile = "$updateOutputDir\CMSAgent.v$version.zip"
if (Test-Path $zipFile) {
    Remove-Item $zipFile -Force
}
Compress-Archive -Path "$updatePackageDir\*" -DestinationPath $zipFile

# Calculate package checksum
$packageChecksum = (Get-FileHash $zipFile -Algorithm SHA256).Hash
Write-Host "Update package SHA256: $packageChecksum"

# Create update info file
$updateInfo = @{
    version = $version
    releaseDate = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ssZ")
    packageUrl = "CMSAgent.v$version.zip"
    checksum_sha256 = $packageChecksum
    notes = "Update to version $version"
}

# Save update info
$updateInfo | ConvertTo-Json | Set-Content "$updateOutputDir\CMSAgent.v$version.json"

# Cleanup temporary files
Remove-Item -Recurse -Force $updatePackageDir

Write-Host "Update package created successfully:"
Write-Host "Package: $zipFile"
Write-Host "Checksum: $packageChecksum"
Write-Host "Update Info: $updateOutputDir\CMSAgent.v$version.json" 