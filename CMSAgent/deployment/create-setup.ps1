# Create Setup Installer Script
# This script builds the CMSAgent and creates a setup installer using Inno Setup

# Configuration
$version = "1.0.0" # Update this for each release
$configuration = "Release"
$projectPath = "src\CMSAgent.Service\CMSAgent.Service.csproj"
$innoSetupPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
$outputDir = "deployment\output"
$setupOutputDir = "$outputDir\setup"
$backupDir = "$outputDir\backup"
$iconPath = Join-Path $PSScriptRoot "icon.ico"

# Create output directories
New-Item -ItemType Directory -Force -Path $outputDir
New-Item -ItemType Directory -Force -Path $setupOutputDir
New-Item -ItemType Directory -Force -Path $backupDir

# Check if version already exists
$existingSetup = Get-ChildItem -Path $setupOutputDir -Filter "Setup.CMSAgent.v$version.exe" -ErrorAction SilentlyContinue
if ($existingSetup) {
    Write-Host "Version $version already exists. Please update version number."
    exit 1
}

# Backup existing files if they exist
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
if (Test-Path $setupOutputDir) {
    Compress-Archive -Path $setupOutputDir\* -DestinationPath "$backupDir\setup_backup_$timestamp.zip" -Force
}

# Update version in appsettings.json
$appSettingsPath = "src\CMSAgent.Service\appsettings.json"
$appSettingsContent = Get-Content $appSettingsPath -Raw | ConvertFrom-Json
$appSettingsContent.AppSettings.Version = $version
$appSettingsContent | ConvertTo-Json -Depth 10 | Set-Content -Path $appSettingsPath
Write-Host "Updated version in appsettings.json to $version"

# Update version in CMSAgent.iss
$issPath = "deployment\InnoSetup\CMSAgent.iss"
$issContent = Get-Content $issPath -Raw
$issContent = $issContent -replace 'MyAppVersion="[^"]*"', "MyAppVersion=`"$version`""
Set-Content -Path $issPath -Value $issContent
Write-Host "Updated version in CMSAgent.iss to $version"

# Build the project
Write-Host "Building CMSAgent.Service..."
dotnet publish $projectPath -c $configuration -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:PublishReadyToRun=true /p:ApplicationIcon="..\..\deployment\icon.ico"

# Copy files to staging directory
$stagingDir = "$outputDir\staging"
New-Item -ItemType Directory -Force -Path $stagingDir

# Copy icon file
Copy-Item $iconPath -Destination $stagingDir

# Copy main service executable
Copy-Item "src\CMSAgent.Service\bin\$configuration\net8.0\win-x64\publish\CMSAgent.Service.exe" -Destination $stagingDir

# Copy appsettings.json
Copy-Item "src\CMSAgent.Service\appsettings.json" -Destination $stagingDir

# Copy Inno Setup script
Copy-Item "deployment\InnoSetup\CMSAgent.iss" -Destination $stagingDir

# Verify file integrity
$serviceHash = Get-FileHash -Path "$stagingDir\CMSAgent.Service.exe" -Algorithm SHA256
Write-Host "CMSAgent.Service.exe SHA256: $($serviceHash.Hash)"

# Run Inno Setup Compiler
Write-Host "Creating setup installer..."
& $innoSetupPath "$stagingDir\CMSAgent.iss"

# Move setup file to output directory
Move-Item "$stagingDir\Output\Setup.CMSAgent.v$version.exe" -Destination $setupOutputDir -Force

# Verify setup file integrity
$setupHash = Get-FileHash -Path "$setupOutputDir\Setup.CMSAgent.v$version.exe" -Algorithm SHA256
Write-Host "Setup.CMSAgent.v$version.exe SHA256: $($setupHash.Hash)"

# Cleanup
Remove-Item -Recurse -Force $stagingDir

Write-Host "Setup installer created successfully at: $setupOutputDir\Setup.CMSAgent.v$version.exe" 