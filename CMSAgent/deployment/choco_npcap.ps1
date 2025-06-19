param(
    [switch]$reinstall = $true,
    [switch]$buildbot = $false
)

$WorkingDir = $PSScriptRoot

$ChocoPKG   = "autoit.commandline"
$AutoItPKG  = "autoit.commandline"
$Setup      = "npcap-1.82.exe"

# Always use this public URL.
$SetupURL = "https://nmap.org/npcap/dist/"

$SetupFlags = "/disable_restore_point=yes",
              "/npf_startup=yes",
              "/loopback_support=yes",
              "/dlt_null=no",
              "/admin_only=no",
              "/dot11_support=yes",
              "/vlan_support=yes",
              "/winpcap_mode=yes"

$SetupTitle = "Npcap"
$SetupEULA  = "License Agreement"
$SetupLast  = "Installation Complete"

function Install-Chocolatey {
    if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "Error: This script must be run with Administrator privileges. Please restart PowerShell with 'Run as Administrator'."
    }
    Write-Host "Chocolatey not found. Installing silently..." -ForegroundColor Cyan
    Set-ExecutionPolicy Bypass -Scope Process -Force;
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072;
    iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))
    Write-Host "Chocolatey installation finished. Verifying..." -ForegroundColor Green
    $env:Path = [System.Environment]::GetEnvironmentVariable("Path", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path", "User")
    if (-not (Get-Command choco -ErrorAction SilentlyContinue)) {
        throw "Chocolatey installation failed or it's not in the PATH."
    }
}

function InitializeScriptEnvironment() {
    $global:TimeStart = (Get-Date)
    if ($env:CI) {
        $global:reinstall = $true
        $global:buildbot = $true
    }
    $global:ErrorActionPreference = "Stop"
}

function InstallPackage() {
    Write-Host "Installing/verifying AutoIt package..." -ForegroundColor Cyan
    choco install autoit.commandline -y
}

function ImportPoshModule() {
    $ChocoRoot = if ($env:ChocolateyInstall) { $env:ChocolateyInstall } else { "C:\ProgramData\chocolatey" }
    $ModuleFullPath = Join-Path $ChocoRoot "lib\$AutoItPKG\tools\install\AutoItX\AutoItX.psd1"
    Write-Host "Checking for module at: $ModuleFullPath" -ForegroundColor Cyan
    if (!(Test-Path $ModuleFullPath)) {
        Write-Host "Module not found, waiting a moment..." -ForegroundColor Yellow
        Start-Sleep -Seconds 5
    }
    if (Test-Path $ModuleFullPath) {
        Write-Host "Importing AutoIt module..." -ForegroundColor Cyan
        Import-Module $ModuleFullPath
    }
    else {
        throw "Could not find the AutoIt module at '$ModuleFullPath'."
    }
}

function DownloadFile([Parameter(Mandatory = $true)]$Link, [Parameter(Mandatory = $true)]$OutFile) {
    Write-Host "Downloading $Link to $WorkingDir\$OutFile..." -ForegroundColor Cyan
    Invoke-WebRequest $Link -UseBasicParsing -OutFile (Join-Path $WorkingDir $OutFile)
}

function RunSetup() {
    Write-Host "Launching Npcap installer using Start-Process..." -ForegroundColor Cyan
    Start-Process -FilePath (Join-Path $WorkingDir $Setup) -ArgumentList $SetupFlags
}

function FocusSetup() {
    Wait-AU3Win -Title $SetupTitle | Out-Null
    $winHandle = Get-AU3WinHandle -Title $SetupTitle
    Show-AU3WinActivate -WinHandle $winHandle | Out-Null
    $controlHandle = Get-AU3ControlHandle -WinHandle $winhandle -Control "Static"
    if ($reinstall) {
        Send-AU3ControlKey -ControlHandle $controlHandle -Key "!y" -WinHandle $winHandle | Out-Null
    }
    Wait-AU3Win -Title $SetupTitle -Text $SetupEULA | Out-Null
    $winHandle = Get-AU3WinHandle -Title $SetupTitle
    Show-AU3WinActivate -WinHandle $winHandle | Out-Null
}

function NavigateSetup() {
    $winHandle = Get-AU3WinHandle -Title $SetupTitle
    $controlHandle = Get-AU3ControlHandle -WinHandle $winhandle -Control "Static"
    Send-AU3ControlKey -ControlHandle $controlHandle -Key "!a" -WinHandle $winHandle | Out-Null
    Send-AU3ControlKey -ControlHandle $controlHandle -Key "!i" -WinHandle $winHandle | Out-Null
    Wait-AU3Win -Title $SetupTitle -Text $SetupLast | Out-Null
    $winHandle = Get-AU3WinHandle -Title $SetupTitle
    Show-AU3WinActivate -WinHandle $winHandle | Out-Null
    Send-AU3ControlKey -ControlHandle $controlHandle -Key "!n" -WinHandle $winHandle | Out-Null
    Send-AU3ControlKey -ControlHandle $controlHandle -Key "{ENTER}" -WinHandle $winHandle | Out-Null
}

function ScriptCleanup() {
    $global:ErrorActionPreference = "Continue"
    if (!$buildbot) {
        Start-Sleep -Milliseconds 500
        Remove-Item (Join-Path $WorkingDir $Setup) -Force -ErrorAction SilentlyContinue
    }
}

function ShowExecutionTime() {
    $timeEnd = New-TimeSpan -Start $global:TimeStart -End $(Get-Date)
    Write-Host "Execution time: $($timeEnd.Minutes) minute(s), $($timeEnd.Seconds) second(s)"
}

function main() {
    InitializeScriptEnvironment
    try {
        if (-not (Get-Command choco -ErrorAction SilentlyContinue)) {
            Install-Chocolatey
        }
        InstallPackage
        ImportPoshModule
        DownloadFile -Link "$SetupURL$Setup" -OutFile $Setup
        RunSetup
        FocusSetup
        NavigateSetup
    }
    finally {
        ScriptCleanup
        ShowExecutionTime
    }
}

main
