# Create Setup Installer Script
# This script builds the CMSAgent and creates a setup installer using Inno Setup

# Configuration
$version = "1.0.0" # Update this for each release
$configuration = "Release"
$projectPath = "src\CMSAgent.Service\CMSAgent.Service.csproj"
$innoSetupPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
$outputDir = "deployment\output"
$setupOutputDir = "$outputDir\setup"

# Create output directories
New-Item -ItemType Directory -Force -Path $outputDir
New-Item -ItemType Directory -Force -Path $setupOutputDir

# Update version in AppSettings.cs
$appSettingsPath = "src\CMSAgent.Service\Configuration\Models\AppSettings.cs"
$appSettingsContent = Get-Content $appSettingsPath -Raw
$appSettingsContent = $appSettingsContent -replace 'public string Version { get; set; } = "[^"]*"', "public string Version { get; set; } = `"$version`""
Set-Content -Path $appSettingsPath -Value $appSettingsContent
Write-Host "Updated version in AppSettings.cs to $version"

# Update version in CMSAgent.iss
$issPath = "deployment\InnoSetup\CMSAgent.iss"
$issContent = Get-Content $issPath -Raw
$issContent = $issContent -replace 'MyAppVersion="[^"]*"', "MyAppVersion=`"$version`""
Set-Content -Path $issPath -Value $issContent
Write-Host "Updated version in CMSAgent.iss to $version"

# Build the project
Write-Host "Building CMSAgent.Service..."
dotnet publish $projectPath -c $configuration -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:PublishReadyToRun=true

# Copy files to staging directory
$stagingDir = "$outputDir\staging"
New-Item -ItemType Directory -Force -Path $stagingDir

# Copy main service executable
Copy-Item "src\CMSAgent.Service\bin\$configuration\net8.0\win-x64\publish\CMSAgent.Service.exe" -Destination $stagingDir

# Copy updater
Copy-Item "src\CMSUpdater\bin\$configuration\net8.0\win-x64\publish\CMSUpdater.exe" -Destination "$stagingDir\Updater"

# Copy Inno Setup script
Copy-Item "deployment\InnoSetup\CMSAgent.iss" -Destination $stagingDir

# Run Inno Setup Compiler
Write-Host "Creating setup installer..."
& $innoSetupPath "$stagingDir\CMSAgent.iss"

# Move setup file to output directory
Move-Item "$stagingDir\Output\Setup.CMSAgent.v$version.exe" -Destination $setupOutputDir -Force

# Cleanup
Remove-Item -Recurse -Force $stagingDir

Write-Host "Setup installer created successfully at: $setupOutputDir\Setup.CMSAgent.v$version.exe" 