#region Configuration
# Build Script cho CMSAgent Setup
# Script này sẽ build dự án và tạo file cài đặt

# Cấu hình đường dẫn
$ProjectRoot = $PSScriptRoot
$SourceDir = Join-Path $ProjectRoot "src"
$BuildDir = Join-Path $ProjectRoot "build"
$ReleaseDir = Join-Path $BuildDir "release"
$InstallerDir = Join-Path $BuildDir "installer"
$InnoSetupCompiler = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

# Đường dẫn đến các project files
$CMSAgentProject = Join-Path $SourceDir "CMSAgent\CMSAgent.csproj"
$CMSAgentCommonProject = Join-Path $SourceDir "CMSAgent.Common\CMSAgent.Common.csproj"
$SetupScriptFile = Join-Path $SourceDir "Setup\SetupScript.iss"
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
    Cập nhật phiên bản trong file SetupScript.iss
.DESCRIPTION
    Hàm này tạo bản sao lưu file SetupScript.iss và thay thế việc đọc phiên bản từ file
    bằng cách sử dụng trực tiếp giá trị phiên bản được cung cấp
.PARAMETER SetupScriptFile
    Đường dẫn đến file SetupScript.iss
.PARAMETER Version
    Chuỗi phiên bản mới
#>
function Update-SetupScript {
    param (
        [Parameter(Mandatory = $true)]
        [string]$SetupScriptFile,
        
        [Parameter(Mandatory = $true)]
        [string]$Version
    )
    
    Write-Host "Cập nhật phiên bản trong file setup script: $SetupScriptFile"
    
    # Tạo bản sao lưu
    Copy-Item -Path $SetupScriptFile -Destination "$SetupScriptFile.bak" -Force
    
    # Đọc nội dung file
    $Content = Get-Content -Path $SetupScriptFile -Raw
    
    # Thêm định nghĩa AppVersion vào đầu file
    $NewContent = "#define AppVersion `"$Version`"`r`n" + $Content
    
    # Lưu nội dung đã cập nhật
    $NewContent | Set-Content -Path $SetupScriptFile
    
    Write-Host "Đã thêm #define AppVersion `"$Version`" vào đầu file setup script"
}

<#
.SYNOPSIS
    Khôi phục các file về trạng thái ban đầu từ bản sao lưu
.DESCRIPTION
    Khôi phục các file .csproj và SetupScript.iss về trạng thái ban đầu sau khi quá trình build hoàn tất
.PARAMETER ProjectFiles
    Mảng đường dẫn đến các file .csproj cần khôi phục
.PARAMETER SetupScriptFile
    Đường dẫn đến file SetupScript.iss cần khôi phục
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
            Write-Host "Khôi phục file $ProjectFile về trạng thái ban đầu"
            Copy-Item -Path "$ProjectFile.bak" -Destination $ProjectFile -Force
            Remove-Item -Path "$ProjectFile.bak" -Force
        }
    }
    
    if (-not [string]::IsNullOrEmpty($SetupScriptFile) -and (Test-Path "$SetupScriptFile.bak")) {
        Write-Host "Khôi phục file $SetupScriptFile về trạng thái ban đầu"
        Copy-Item -Path "$SetupScriptFile.bak" -Destination $SetupScriptFile -Force
        Remove-Item -Path "$SetupScriptFile.bak" -Force
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
    Tạo các thư mục build, release và installer nếu chưa tồn tại
#>
function Initialize-BuildDirectories {
    # Tạo thư mục build nếu chưa tồn tại
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
    Xóa các bản build cũ
.DESCRIPTION
    Xóa nội dung của thư mục release và installer để chuẩn bị cho bản build mới
#>
function Clear-OldBuilds {
    Write-Host "Xóa các bản build cũ..."
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
    # Lấy thông tin phiên bản từ người dùng
    $Version = Read-Host "Nhập phiên bản (ví dụ: 1.0.0)"
    if ([string]::IsNullOrWhiteSpace($Version)) {
        Write-Error "Phiên bản không được để trống. Đang dừng quá trình build."
        exit 1
    }
    $Version = $Version.Trim()
    Write-Host "Building version: $Version" -ForegroundColor Cyan

    # Kiểm tra Inno Setup
    if (-not (Test-Path $InnoSetupCompiler)) {
        Write-Error "Không tìm thấy Inno Setup Compiler. Vui lòng cài đặt Inno Setup 6 tại đường dẫn mặc định hoặc cập nhật đường dẫn trong script."
        exit 1
    }

    # Cập nhật phiên bản trong các project
    Write-Host "Bắt đầu cập nhật phiên bản trong các project files..." -ForegroundColor Cyan
    Update-ProjectVersion -ProjectFile $CMSAgentProject -Version $Version
    Update-ProjectVersion -ProjectFile $CMSAgentCommonProject -Version $Version

    # Cập nhật phiên bản trong file SetupScript.iss
    Update-SetupScript -SetupScriptFile $SetupScriptFile -Version $Version

    # Tạo thư mục build và xóa các bản build cũ
    Initialize-BuildDirectories
    Clear-OldBuilds

    # Build các project
    Write-Host "Bắt đầu quá trình build..." -ForegroundColor Cyan
    
    # Build CMSAgent
    $CMSAgentOutputDir = Join-Path $ReleaseDir "CMSAgent"
    Write-Host "Building CMSAgent..." -ForegroundColor Yellow
    Invoke-DotNetBuild -ProjectPath $CMSAgentProject -OutputPath $CMSAgentOutputDir

    # Coppy các file cấu hình bổ sung nếu cần
    Write-Host "Sao chép các file cấu hình bổ sung..." -ForegroundColor Yellow
    # TODO: Thêm các lệnh copy file cấu hình nếu cần

    # Tạo file cài đặt với Inno Setup
    Write-Host "Tạo file cài đặt với Inno Setup..." -ForegroundColor Cyan
    & $InnoSetupCompiler $SetupScriptFile

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Không thể tạo file cài đặt với Inno Setup."
        exit $LASTEXITCODE
    }

    # Kiểm tra và thông báo kết quả
    $SetupFile = Join-Path $InstallerDir "Setup.CMSAgent.v$Version.exe"
    if (Test-Path $SetupFile) {
        Write-Host "Đã tạo thành công file cài đặt: $SetupFile" -ForegroundColor Green
    } else {
        Write-Error "Không tìm thấy file cài đặt sau khi build."
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
    Restore-ProjectFiles -ProjectFiles @($CMSAgentProject, $CMSAgentCommonProject) -SetupScriptFile $SetupScriptFile
    
    Write-Host "Quá trình build hoàn tất!" -ForegroundColor Green
}
#endregion
