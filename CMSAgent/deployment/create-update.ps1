# Create Update Package Script
# This script builds the CMSAgent and creates an update package

# Configuration
$version = "1.0.0" # Update this for each release
$configuration = "Release"
$projectPath = "src\CMSAgent.Service\CMSAgent.Service.csproj"
$outputDir = "deployment\output"
$updateOutputDir = "$outputDir\updates"

# Create output directories
New-Item -ItemType Directory -Force -Path $outputDir
New-Item -ItemType Directory -Force -Path $updateOutputDir

# Update version in AppSettings.cs
$appSettingsPath = "src\CMSAgent.Service\Configuration\Models\AppSettings.cs"
$appSettingsContent = Get-Content $appSettingsPath -Raw
$appSettingsContent = $appSettingsContent -replace 'public string Version { get; set; } = "[^"]*"', "public string Version { get; set; } = `"$version`""
Set-Content -Path $appSettingsPath -Value $appSettingsContent
Write-Host "Updated version in AppSettings.cs to $version"

# Build the project
Write-Host "Building CMSAgent.Service..."
dotnet publish $projectPath -c $configuration -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:PublishReadyToRun=true

# Create update package directory
$updatePackageDir = "$updateOutputDir\v$version"
New-Item -ItemType Directory -Force -Path $updatePackageDir

# Copy main service executable
Copy-Item "src\CMSAgent.Service\bin\$configuration\net8.0\win-x64\publish\CMSAgent.Service.exe" -Destination $updatePackageDir

# Copy updater
Copy-Item "src\CMSUpdater\bin\$configuration\net8.0\win-x64\publish\CMSUpdater.exe" -Destination "$updatePackageDir\Updater"

# Create update manifest
$manifest = @{
    version = $version
    releaseDate = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ssZ")
    files = @(
        @{
            path = "CMSAgent.Service.exe"
            checksum = (Get-FileHash "$updatePackageDir\CMSAgent.Service.exe" -Algorithm SHA256).Hash
        },
        @{
            path = "Updater\CMSUpdater.exe"
            checksum = (Get-FileHash "$updatePackageDir\Updater\CMSUpdater.exe" -Algorithm SHA256).Hash
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

Write-Host "Update package created successfully:"
Write-Host "Package: $zipFile"
Write-Host "Checksum: $packageChecksum"
Write-Host "Update Info: $updateOutputDir\CMSAgent.v$version.json" 