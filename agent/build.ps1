#region Configuration
# Build Script for CMSAgent Setup
# This script will build the project and create installation file

# Path Configuration
$ProjectRoot = $PSScriptRoot
$SourceDir = Join-Path $ProjectRoot "src"
$BuildDir = Join-Path $ProjectRoot "build"
$ReleaseDir = Join-Path $BuildDir "release"
$InstallerDir = Join-Path $BuildDir "installer"
$InnoSetupCompiler = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

# Paths to project files
$CMSAgentProject = Join-Path $SourceDir "CMSAgent\CMSAgent.csproj"
$CMSAgentCommonProject = Join-Path $SourceDir "CMSAgent.Common\CMSAgent.Common.csproj"
$SetupScriptFile = Join-Path $SourceDir "Setup\SetupScript.iss"
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
    
    # Create backup before modification
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
    Update version in SetupScript.iss file
.DESCRIPTION
    This function creates a backup of the SetupScript.iss file and replaces the version reading from file
    by directly using the provided version value
.PARAMETER SetupScriptFile
    Path to the SetupScript.iss file
.PARAMETER Version
    New version string
#>
function Update-SetupScript {
    param (
        [Parameter(Mandatory = $true)]
        [string]$SetupScriptFile,
        
        [Parameter(Mandatory = $true)]
        [string]$Version
    )
    
    Write-Host "Updating version in setup script file: $SetupScriptFile"
    
    # Create backup
    Copy-Item -Path $SetupScriptFile -Destination "$SetupScriptFile.bak" -Force
    
    # Read file content
    $Content = Get-Content -Path $SetupScriptFile -Raw
    
    # Add AppVersion definition at the beginning of the file
    $NewContent = "#define AppVersion `"$Version`"`r`n" + $Content
    
    # Save updated content
    $NewContent | Set-Content -Path $SetupScriptFile
    
    Write-Host "Added #define AppVersion `"$Version`" at the beginning of setup script file"
}

<#
.SYNOPSIS
    Restore files to their original state from backups
.DESCRIPTION
    Restore .csproj and SetupScript.iss files to their original state after the build process is completed
.PARAMETER ProjectFiles
    Array of paths to .csproj files to restore
.PARAMETER SetupScriptFile
    Path to the SetupScript.iss file to restore
#>
function Restore-ProjectFiles {
    param (
        [Parameter(Mandatory = $true)]
        [string[]]$ProjectFiles,
        
        [Parameter(Mandatory = $false)]
        [string]$SetupScriptFile
    )
    
    foreach ($ProjectFile in $ProjectFiles) {
        if (Test-Path "$ProjectFile.bak") {
            Write-Host "Restoring file $ProjectFile to original state"
            Copy-Item -Path "$ProjectFile.bak" -Destination $ProjectFile -Force
            Remove-Item -Path "$ProjectFile.bak" -Force
        }
    }
    
    if (-not [string]::IsNullOrEmpty($SetupScriptFile) -and (Test-Path "$SetupScriptFile.bak")) {
        Write-Host "Restoring file $SetupScriptFile to original state"
        Copy-Item -Path "$SetupScriptFile.bak" -Destination $SetupScriptFile -Force
        Remove-Item -Path "$SetupScriptFile.bak" -Force
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
    Path to the output directory
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
        Write-Error "Cannot build project $ProjectPath"
        exit $LASTEXITCODE
    }
}

<#
.SYNOPSIS
    Create necessary build directories
.DESCRIPTION
    Create build, release, and installer directories if they don't exist
#>
function Initialize-BuildDirectories {
    # Create build directory if it doesn't exist
    if (-not (Test-Path $BuildDir)) {
        New-Item -ItemType Directory -Path $BuildDir | Out-Null
    }

    if (-not (Test-Path $ReleaseDir)) {
        New-Item -ItemType Directory -Path $ReleaseDir | Out-Null
    }

    if (-not (Test-Path $InstallerDir)) {
        New-Item -ItemType Directory -Path $InstallerDir | Out-Null
    }
}

<#
.SYNOPSIS
    Delete old builds
.DESCRIPTION
    Delete contents of release and installer directories to prepare for new build
#>
function Clear-OldBuilds {
    Write-Host "Deleting old builds..."
    if (Test-Path $ReleaseDir) {
        Remove-Item -Path "$ReleaseDir\*" -Recurse -Force
    }
    if (Test-Path $InstallerDir) {
        Remove-Item -Path "$InstallerDir\*" -Recurse -Force
    }
}
#endregion

#region Main Script
try {
    # Get version information from user
    $Version = Read-Host "Enter version (e.g., 1.0.0)"
    if ([string]::IsNullOrWhiteSpace($Version)) {
        Write-Error "Version cannot be empty. Stopping the build process."
        exit 1
    }
    $Version = $Version.Trim()
    Write-Host "Building version: $Version" -ForegroundColor Cyan

    # Check Inno Setup
    if (-not (Test-Path $InnoSetupCompiler)) {
        Write-Error "Inno Setup Compiler not found. Please install Inno Setup 6 at the default path or update the path in script."
        exit 1
    }

    # Update version in projects
    Write-Host "Starting to update version in project files..." -ForegroundColor Cyan
    Update-ProjectVersion -ProjectFile $CMSAgentProject -Version $Version
    Update-ProjectVersion -ProjectFile $CMSAgentCommonProject -Version $Version

    # Update version in SetupScript.iss file
    Update-SetupScript -SetupScriptFile $SetupScriptFile -Version $Version

    # Create build directories and clear old builds
    Initialize-BuildDirectories
    Clear-OldBuilds

    # Build projects
    Write-Host "Starting build process..." -ForegroundColor Cyan
    
    # Build CMSAgent
    $CMSAgentOutputDir = Join-Path $ReleaseDir "CMSAgent"
    Write-Host "Building CMSAgent..." -ForegroundColor Yellow
    Invoke-DotNetBuild -ProjectPath $CMSAgentProject -OutputPath $CMSAgentOutputDir

    # Copy additional configuration files if needed
    Write-Host "Copying additional configuration files..." -ForegroundColor Yellow
    # TODO: Add configuration file copy commands if needed

    # Create installer with Inno Setup
    Write-Host "Creating installer with Inno Setup..." -ForegroundColor Cyan
    & $InnoSetupCompiler $SetupScriptFile

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Cannot create installer with Inno Setup."
        exit $LASTEXITCODE
    }

    # Check and report results
    $SetupFile = Join-Path $InstallerDir "Setup.CMSAgent.v$Version.exe"
    if (Test-Path $SetupFile) {
        Write-Host "Successfully created installer: $SetupFile" -ForegroundColor Green
    } else {
        Write-Error "Installer file not found after build."
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
    Restore-ProjectFiles -ProjectFiles @($CMSAgentProject, $CMSAgentCommonProject) -SetupScriptFile $SetupScriptFile
    
    Write-Host "Build process completed!" -ForegroundColor Green
}
#endregion
