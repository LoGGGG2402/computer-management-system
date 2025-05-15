#region Configuration
# Build Script cho CMSAgent Update Package
# Script này sẽ build dự án và tạo gói cập nhật cho CMSAgent

# Cấu hình đường dẫn
$ProjectRoot = $PSScriptRoot
$SourceDir = Join-Path $ProjectRoot "src"
$BuildDir = Join-Path $ProjectRoot "build"
$UpdateDir = Join-Path $BuildDir "update"
$TempDir = Join-Path $UpdateDir "temp"

# Đường dẫn đến các project files
$CMSAgentProject = Join-Path $SourceDir "CMSAgent\CMSAgent.csproj"
$CMSUpdaterProject = Join-Path $SourceDir "CMSUpdater\CMSUpdater.csproj"
$CMSAgentCommonProject = Join-Path $SourceDir "CMSAgent.Common\CMSAgent.Common.csproj"
#endregion

#region Helper Functions
<#
.SYNOPSIS
    Cập nhật phiên bản trong file .csproj
.DESCRIPTION
    Hàm này tạo bản sao lưu file .csproj, sau đó cập nhật hoặc thêm thẻ Version với giá trị mới
.PARAMETER ProjectFile
    Đường dẫn đến file .csproj cần cập nhật
.PARAMETER Version
    Chuỗi phiên bản mới
#>
function Update-ProjectVersion {
    param (
        [Parameter(Mandatory = $true)]
        [string]$ProjectFile,
        
        [Parameter(Mandatory = $true)]
        [string]$Version
    )
    
    Write-Host "Cập nhật phiên bản $Version cho $ProjectFile"
    
    # Tạo bản sao lưu trước khi sửa đổi
    Copy-Item -Path $ProjectFile -Destination "$ProjectFile.bak" -Force
    
    # Đọc nội dung file
    $Content = Get-Content -Path $ProjectFile -Raw
    
    # Kiểm tra xem thẻ Version đã tồn tại chưa
    if ($Content -match '<Version>[0-9.]+</Version>') {
        # Cập nhật phiên bản nếu đã tồn tại
        $UpdatedContent = $Content -replace '<Version>[0-9.]+</Version>', "<Version>$Version</Version>"
    } else {
        # Thêm thẻ Version vào sau thẻ Nullable nếu chưa tồn tại
        $UpdatedContent = $Content -replace '(<Nullable>.*?</Nullable>)', "$1`r`n    <Version>$Version</Version>"
    }
    
    # Lưu nội dung đã cập nhật
    Set-Content -Path $ProjectFile -Value $UpdatedContent
}

<#
.SYNOPSIS
    Khôi phục các file về trạng thái ban đầu từ bản sao lưu
.DESCRIPTION
    Khôi phục các file .csproj về trạng thái ban đầu sau khi quá trình build hoàn tất
.PARAMETER ProjectFiles
    Mảng đường dẫn đến các file .csproj cần khôi phục
#>
function Restore-ProjectFiles {
    param (
        [Parameter(Mandatory = $true)]
        [string[]]$ProjectFiles
    )
    
    foreach ($ProjectFile in $ProjectFiles) {
        if (Test-Path "$ProjectFile.bak") {
            Write-Host "Khôi phục file $ProjectFile về trạng thái ban đầu"
            Copy-Item -Path "$ProjectFile.bak" -Destination $ProjectFile -Force
            Remove-Item -Path "$ProjectFile.bak" -Force
        }
    }
}

<#
.SYNOPSIS
    Build dự án .NET
.DESCRIPTION
    Hàm này dùng để build một dự án .NET với các cài đặt được chỉ định
.PARAMETER ProjectPath
    Đường dẫn đến file project (.csproj)
.PARAMETER OutputPath
    Đường dẫn thư mục đầu ra
.PARAMETER Configuration
    Cấu hình build (mặc định: Release)
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
    
    # Tạo thư mục đầu ra nếu chưa tồn tại
    if (-not (Test-Path $OutputPath)) {
        New-Item -ItemType Directory -Path $OutputPath | Out-Null
    }
    
    # Build dự án
    dotnet publish $ProjectPath `
        --configuration $Configuration `
        --output $OutputPath `
        --self-contained true `
        --runtime win-x64 `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:PublishReadyToRun=true
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Không thể build dự án $ProjectPath"
        exit $LASTEXITCODE
    }
}

<#
.SYNOPSIS
    Tạo các thư mục build cần thiết
.DESCRIPTION
    Tạo các thư mục build, update và temp nếu chưa tồn tại
#>
function Initialize-BuildDirectories {
    # Tạo thư mục build nếu chưa tồn tại
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
    Xóa các bản build cũ
.DESCRIPTION
    Xóa nội dung của thư mục temp để chuẩn bị cho bản build mới
#>
function Clear-OldBuilds {
    Write-Host "Xóa các bản build cũ..."
    if (Test-Path $TempDir) {
        Remove-Item -Path "$TempDir\*" -Recurse -Force
    }
}

<#
.SYNOPSIS
    Tạo file update_info.json cho gói cập nhật
.DESCRIPTION
    Tạo file update_info.json chứa thông tin về gói cập nhật, với định dạng tương thích với CMSUpdater
.PARAMETER Version
    Phiên bản của gói cập nhật
.PARAMETER FilePath
    Đường dẫn đến file update_info.json sẽ được tạo
.PARAMETER PackagePath
    Đường dẫn đến file package khi được giải nén
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
    
    Write-Host "Đã tạo file update_info.json tại $FilePath"
}

<#
.SYNOPSIS
    Tính toán checksum SHA256 của một file
.DESCRIPTION
    Tính toán và trả về chuỗi checksum SHA256 của file được chỉ định
.PARAMETER FilePath
    Đường dẫn đến file cần tính checksum
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
    # Lấy thông tin phiên bản từ người dùng
    $Version = Read-Host "Nhập phiên bản gói cập nhật (ví dụ: 1.0.0)"
    if ([string]::IsNullOrWhiteSpace($Version)) {
        Write-Error "Phiên bản không được để trống. Đang dừng quá trình build."
        exit 1
    }
    $Version = $Version.Trim()
    Write-Host "Building update package version: $Version" -ForegroundColor Cyan

    # Cập nhật phiên bản trong các project
    Write-Host "Bắt đầu cập nhật phiên bản trong các project files..." -ForegroundColor Cyan
    Update-ProjectVersion -ProjectFile $CMSAgentProject -Version $Version
    Update-ProjectVersion -ProjectFile $CMSUpdaterProject -Version $Version
    Update-ProjectVersion -ProjectFile $CMSAgentCommonProject -Version $Version

    # Tạo thư mục build và xóa các bản build cũ
    Initialize-BuildDirectories
    Clear-OldBuilds

    # Build các project
    Write-Host "Bắt đầu quá trình build..." -ForegroundColor Cyan
    
    # Build CMSAgent
    $CMSAgentOutputDir = Join-Path $TempDir "CMSAgent"
    Write-Host "Building CMSAgent..." -ForegroundColor Yellow
    Invoke-DotNetBuild -ProjectPath $CMSAgentProject -OutputPath $CMSAgentOutputDir

    # Build CMSUpdater
    $CMSUpdaterOutputDir = Join-Path $TempDir "CMSUpdater"
    Write-Host "Building CMSUpdater..." -ForegroundColor Yellow
    Invoke-DotNetBuild -ProjectPath $CMSUpdaterProject -OutputPath $CMSUpdaterOutputDir

    # Đọc danh sách file cần loại trừ từ cài đặt Updater
    $UpdaterSettingsPath = Join-Path $SourceDir "CMSUpdater\appsettings.json"
    $ExcludedFiles = @()
    
    if (Test-Path $UpdaterSettingsPath) {
        try {
            $UpdaterSettings = Get-Content -Path $UpdaterSettingsPath -Raw | ConvertFrom-Json
            if ($UpdaterSettings.Updater.FilesToExcludeFromUpdate) {
                $ExcludedFiles = $UpdaterSettings.Updater.FilesToExcludeFromUpdate
                Write-Host "Đã đọc danh sách file loại trừ từ cài đặt Updater: $($ExcludedFiles -join ', ')" -ForegroundColor Cyan
            }
        } catch {
            Write-Warning "Không thể đọc danh sách file loại trừ từ cài đặt Updater: $_"
        }
    }

    # Tạo file update_info.json (placeholder - sẽ được UpdateHandler thay thế)
    $UpdateInfoPath = Join-Path $CMSUpdaterOutputDir "update_info.json"
    $PlaceholderPackagePath = "C:\\Path\\To\\Package\\CMSAgent_Update_v$Version.zip"
    New-UpdateInfo -Version $Version -FilePath $UpdateInfoPath -PackagePath $PlaceholderPackagePath

    # Tạo file ZIP chứa gói cập nhật
    $UpdatePackagePath = Join-Path $UpdateDir "CMSAgent_Update_v$Version.zip"
    
    Write-Host "Đang tạo gói cập nhật $UpdatePackagePath..." -ForegroundColor Cyan
    
    if (Test-Path $UpdatePackagePath) {
        Remove-Item -Path $UpdatePackagePath -Force
    }
    
    Compress-Archive -Path "$TempDir\*" -DestinationPath $UpdatePackagePath -CompressionLevel Optimal
    
    # Tính toán checksum SHA256 cho gói cập nhật
    $Checksum = Get-FileChecksum -FilePath $UpdatePackagePath
    
    # Tạo file checksum
    $ChecksumFilePath = Join-Path $UpdateDir "CMSAgent_Update_v$Version.sha256"
    Set-Content -Path $ChecksumFilePath -Value $Checksum -Encoding UTF8
    
    # Tạo file thông tin cập nhật (để sử dụng cho API)
    $UpdateInfoPath = Join-Path $UpdateDir "update_info_v$Version.json"
    $UpdateInfo = @{
        version = $Version
        release_date = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ")
        download_url = "REPLACE_WITH_ACTUAL_DOWNLOAD_URL"
        checksum_sha256 = $Checksum
        file_size_bytes = (Get-Item -Path $UpdatePackagePath).Length
        notes = "Gói cập nhật CMSAgent phiên bản $Version"
        is_mandatory = $false
        supported_versions = @("*")
    }
    
    $UpdateInfoJson = $UpdateInfo | ConvertTo-Json -Depth 10
    Set-Content -Path $UpdateInfoPath -Value $UpdateInfoJson -Encoding UTF8

    # Kiểm tra và thông báo kết quả
    if (Test-Path $UpdatePackagePath) {
        Write-Host "Đã tạo thành công gói cập nhật: $UpdatePackagePath" -ForegroundColor Green
        Write-Host "Checksum SHA256: $Checksum" -ForegroundColor Green
        Write-Host "Kích thước: $([Math]::Round((Get-Item -Path $UpdatePackagePath).Length / 1MB, 2)) MB" -ForegroundColor Green
        Write-Host "File thông tin cập nhật: $UpdateInfoPath" -ForegroundColor Green
        Write-Host "LƯU Ý: Cần thay thế URL tải xuống trong file thông tin cập nhật trước khi sử dụng." -ForegroundColor Yellow
    } else {
        Write-Error "Không tìm thấy gói cập nhật sau khi build."
        exit 1
    }
}
catch {
    Write-Error "Đã xảy ra lỗi trong quá trình build: $_"
    exit 1
}
finally {
    # Khôi phục các file về trạng thái ban đầu
    Write-Host "Khôi phục các file về trạng thái ban đầu..." -ForegroundColor Yellow
    Restore-ProjectFiles -ProjectFiles @($CMSAgentProject, $CMSUpdaterProject, $CMSAgentCommonProject)
    
    Write-Host "Quá trình build hoàn tất!" -ForegroundColor Green
}
#endregion 