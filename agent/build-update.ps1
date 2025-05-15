#region Configuration
# Build Script for CMSAgent Update Package
# This script will build the project and create update package for CMSAgent

# Path Configuration
$ProjectRoot = $PSScriptRoot
$SourceDir = Join-Path $ProjectRoot "src"
$BuildDir = Join-Path $ProjectRoot "build"
$UpdateDir = Join-Path $BuildDir "update"
$TempDir = Join-Path $UpdateDir "temp"

# Paths to project files
$CMSAgentProject = Join-Path $SourceDir "CMSAgent\CMSAgent.csproj"
$CMSUpdaterProject = Join-Path $SourceDir "CMSUpdater\CMSUpdater.csproj"
$CMSAgentCommonProject = Join-Path $SourceDir "CMSAgent.Common\CMSAgent.Common.csproj"
#endregion

#region Helper Functions
<#
.SYNOPSIS
    Update version in .csproj file
.DESCRIPTION
    This function creates a backup of the .csproj file, then updates or adds the Version tag with a new value
.PARAMETER ProjectFile
    Path to the .csproj file to update
.PARAMETER Version
    New version string
#>
function Update-ProjectVersion {
    param (
        [Parameter(Mandatory = $true)]
        [string]$ProjectFile,
        
        [Parameter(Mandatory = $true)]
        [string]$Version
    )
    
    Write-Host "Updating version $Version for $ProjectFile"
    
    # Create a backup before modifying
    Copy-Item -Path $ProjectFile -Destination "$ProjectFile.bak" -Force
    
    # Read file content
    $Content = Get-Content -Path $ProjectFile -Raw
    
    # Check if Version tag already exists
    if ($Content -match '<Version>[0-9.]+</Version>') {
        # Update version if it exists
        $UpdatedContent = $Content -replace '<Version>[0-9.]+</Version>', "<Version>$Version</Version>"
    } else {
        # Add Version tag after Nullable tag if it doesn't exist
        $UpdatedContent = $Content -replace '(<Nullable>.*?</Nullable>)', "$1`r`n    <Version>$Version</Version>"
    }
    
    # Save updated content
    Set-Content -Path $ProjectFile -Value $UpdatedContent
}

<#
.SYNOPSIS
    Restore files to their original state from backup
.DESCRIPTION
    Restore .csproj files to their original state after the build process is complete
.PARAMETER ProjectFiles
    Array of paths to .csproj files to restore
#>
function Restore-ProjectFiles {
    param (
        [Parameter(Mandatory = $true)]
        [string[]]$ProjectFiles
    )
    
    foreach ($ProjectFile in $ProjectFiles) {
        if (Test-Path "$ProjectFile.bak") {
            Write-Host "Restoring file $ProjectFile to its original state"
            Copy-Item -Path "$ProjectFile.bak" -Destination $ProjectFile -Force
            Remove-Item -Path "$ProjectFile.bak" -Force
        }
    }
}

<#
.SYNOPSIS
    Build .NET project
.DESCRIPTION
    This function is used to build a .NET project with specified settings
.PARAMETER ProjectPath
    Path to the project file (.csproj)
.PARAMETER OutputPath
    Output directory path
.PARAMETER Configuration
    Build configuration (default: Release)
#>
function Invoke-DotNetBuild {
    param (
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath,
        
        [Parameter(Mandatory = $true)]
        [string]$OutputPath,
        
        [Parameter(Mandatory = $false)]
        [string]$Configuration = "Release"
    )
    
    Write-Host "Building $ProjectPath to $OutputPath"
    
    # Create output directory if it doesn't exist
    if (-not (Test-Path $OutputPath)) {
        New-Item -ItemType Directory -Path $OutputPath | Out-Null
    }
    
    # Build project
    dotnet publish $ProjectPath `
        --configuration $Configuration `
        --output $OutputPath `
        --self-contained true `
        --runtime win-x64 `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:PublishReadyToRun=true
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to build project $ProjectPath"
        exit $LASTEXITCODE
    }
}

<#
.SYNOPSIS
    Create necessary build directories
.DESCRIPTION
    Create build, update, and temp directories if they don't exist
#>
function Initialize-BuildDirectories {
    # Create build directory if it doesn't exist
    if (-not (Test-Path $BuildDir)) {
        New-Item -ItemType Directory -Path $BuildDir | Out-Null
    }

    if (-not (Test-Path $UpdateDir)) {
        New-Item -ItemType Directory -Path $UpdateDir | Out-Null
    }

    if (-not (Test-Path $TempDir)) {
        New-Item -ItemType Directory -Path $TempDir | Out-Null
    }
}

<#
.SYNOPSIS
    Delete old builds
.DESCRIPTION
    Delete the contents of the temp directory to prepare for a new build
#>
function Clear-OldBuilds {
    Write-Host "Deleting old builds..."
    if (Test-Path $TempDir) {
        Remove-Item -Path "$TempDir\*" -Recurse -Force
    }
}

<#
.SYNOPSIS
    Create update_info.json file for the update package
.DESCRIPTION
    Create update_info.json file containing information about the update package, compatible with CMSUpdater
.PARAMETER Version
    Version of the update package
.PARAMETER FilePath
    Path to the update_info.json file to be created
.PARAMETER PackagePath
    Path to the package file when extracted
#>
function New-UpdateInfo {
    param (
        [Parameter(Mandatory = $true)]
        [string]$Version,
        
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        
        [Parameter(Mandatory = $true)]
        [string]$PackagePath
    )
    
    $UpdateInfo = @{
        package_path = $PackagePath.Replace("\", "\\")
        install_directory = "TO_BE_REPLACED_BY_AGENT"
        new_version = $Version
        timestamp = (Get-Date -Format "yyyy-MM-ddTHH:mm:ss.fffZ")
    }
    
    $UpdateInfoJson = $UpdateInfo | ConvertTo-Json -Depth 10
    Set-Content -Path $FilePath -Value $UpdateInfoJson -Encoding UTF8
    
    Write-Host "Created update_info.json file at $FilePath"
}

<#
.SYNOPSIS
    Calculate SHA256 checksum of a file
.DESCRIPTION
    Calculate and return the SHA256 checksum string of the specified file
.PARAMETER FilePath
    Path to the file to calculate checksum
#>
function Get-FileChecksum {
    param (
        [Parameter(Mandatory = $true)]
        [string]$FilePath
    )
    
    $FileStream = [System.IO.File]::OpenRead($FilePath)
    $SHA256 = [System.Security.Cryptography.SHA256]::Create()
    $Hash = $SHA256.ComputeHash($FileStream)
    $FileStream.Close()
    $SHA256.Dispose()
    
    return ($Hash | ForEach-Object { $_.ToString("x2") }) -join ""
}
#endregion

#region Main Script
try {
    # Get version information from user
    $Version = Read-Host "Enter update package version (e.g., 1.0.0)"
    if ([string]::IsNullOrWhiteSpace($Version)) {
        Write-Error "Version cannot be empty. Stopping the build process."
        exit 1
    }
    $Version = $Version.Trim()
    Write-Host "Building update package version: $Version" -ForegroundColor Cyan

    # Update version in projects
    Write-Host "Starting to update version in project files..." -ForegroundColor Cyan
    Update-ProjectVersion -ProjectFile $CMSAgentProject -Version $Version
    Update-ProjectVersion -ProjectFile $CMSUpdaterProject -Version $Version
    Update-ProjectVersion -ProjectFile $CMSAgentCommonProject -Version $Version

    # Create build directories and delete old builds
    Initialize-BuildDirectories
    Clear-OldBuilds

    # Build projects
    Write-Host "Starting build process..." -ForegroundColor Cyan
    
    # Build CMSAgent
    $CMSAgentOutputDir = Join-Path $TempDir "CMSAgent"
    Write-Host "Building CMSAgent..." -ForegroundColor Yellow
    Invoke-DotNetBuild -ProjectPath $CMSAgentProject -OutputPath $CMSAgentOutputDir

    # Build CMSUpdater
    $CMSUpdaterOutputDir = Join-Path $TempDir "CMSUpdater"
    Write-Host "Building CMSUpdater..." -ForegroundColor Yellow
    Invoke-DotNetBuild -ProjectPath $CMSUpdaterProject -OutputPath $CMSUpdaterOutputDir

    # Read excluded files list from Updater settings
    $UpdaterSettingsPath = Join-Path $SourceDir "CMSUpdater\appsettings.json"
    $ExcludedFiles = @()
    
    if (Test-Path $UpdaterSettingsPath) {
        try {
            $UpdaterSettings = Get-Content -Path $UpdaterSettingsPath -Raw | ConvertFrom-Json
            if ($UpdaterSettings.Updater.FilesToExcludeFromUpdate) {
                $ExcludedFiles = $UpdaterSettings.Updater.FilesToExcludeFromUpdate
                Write-Host "Read excluded files list from Updater settings: $($ExcludedFiles -join ', ')" -ForegroundColor Cyan
            }
        } catch {
            Write-Warning "Unable to read excluded files list from Updater settings: $_"
        }
    }

    # Create update_info.json file (placeholder - will be replaced by UpdateHandler)
    $UpdateInfoPath = Join-Path $CMSUpdaterOutputDir "update_info.json"
    $PlaceholderPackagePath = "C:\\Path\\To\\Package\\CMSAgent_Update_v$Version.zip"
    New-UpdateInfo -Version $Version -FilePath $UpdateInfoPath -PackagePath $PlaceholderPackagePath

    # Create ZIP file for the update package
    $UpdatePackagePath = Join-Path $UpdateDir "CMSAgent_Update_v$Version.zip"
    
    Write-Host "Creating update package $UpdatePackagePath..." -ForegroundColor Cyan
    
    if (Test-Path $UpdatePackagePath) {
        Remove-Item -Path $UpdatePackagePath -Force
    }
    
    Compress-Archive -Path "$TempDir\*" -DestinationPath $UpdatePackagePath -CompressionLevel Optimal
    
    # Calculate SHA256 checksum for the update package
    $Checksum = Get-FileChecksum -FilePath $UpdatePackagePath
    
    # Create checksum file
    $ChecksumFilePath = Join-Path $UpdateDir "CMSAgent_Update_v$Version.sha256"
    Set-Content -Path $ChecksumFilePath -Value $Checksum -Encoding UTF8
    
    # Create update info file (for API usage)
    $UpdateInfoPath = Join-Path $UpdateDir "update_info_v$Version.json"
    $UpdateInfo = @{
        version = $Version
        release_date = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ")
        download_url = "REPLACE_WITH_ACTUAL_DOWNLOAD_URL"
        checksum_sha256 = $Checksum
        file_size_bytes = (Get-Item -Path $UpdatePackagePath).Length
        notes = "CMSAgent update package version $Version"
        is_mandatory = $false
        supported_versions = @("*")
    }
    
    $UpdateInfoJson = $UpdateInfo | ConvertTo-Json -Depth 10
    Set-Content -Path $UpdateInfoPath -Value $UpdateInfoJson -Encoding UTF8

    # Validate and notify results
    if (Test-Path $UpdatePackagePath) {
        Write-Host "Successfully created update package: $UpdatePackagePath" -ForegroundColor Green
        Write-Host "Checksum SHA256: $Checksum" -ForegroundColor Green
        Write-Host "Size: $([Math]::Round((Get-Item -Path $UpdatePackagePath).Length / 1MB, 2)) MB" -ForegroundColor Green
        Write-Host "Update info file: $UpdateInfoPath" -ForegroundColor Green
        Write-Host "NOTE: Download URL in update info file must be replaced before use." -ForegroundColor Yellow
    } else {
        Write-Error "Update package not found after build."
        exit 1
    }
}
catch {
    Write-Error "An error occurred during build process: $_"
    exit 1
}
finally {
    # Restore files to original state
    Write-Host "Restoring files to original state..." -ForegroundColor Yellow
    Restore-ProjectFiles -ProjectFiles @($CMSAgentProject, $CMSUpdaterProject, $CMSAgentCommonProject)
    
    Write-Host "Build process completed!" -ForegroundColor Green
}
#endregion