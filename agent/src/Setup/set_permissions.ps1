# Script để thiết lập quyền truy cập cho CMSAgent theo tài liệu CMSAgent_Comprehensive_Doc.md
param (
    [string]$DataPath = "C:\ProgramData\CMSAgent"
)

$ErrorActionPreference = "Stop"

# Kiểm tra nếu script chạy với quyền Administrator
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Script này phải được chạy với quyền Administrator"
}

Write-Host "Thiết lập thư mục dữ liệu và quyền truy cập cho CMSAgent..."
Write-Host "Đường dẫn dữ liệu: $DataPath"

try {
    # Tạo thư mục dữ liệu chính nếu chưa tồn tại
    if (-not (Test-Path $DataPath)) {
        New-Item -Path $DataPath -ItemType Directory -Force | Out-Null
        Write-Host "Đã tạo thư mục dữ liệu chính: $DataPath"
    }

    # Tạo các thư mục con theo cấu trúc được mô tả trong tài liệu
    $subdirs = @(
        "logs",
        "runtime_config",
        "updates\download",
        "updates\extracted", 
        "updates\backup",
        "error_reports",
        "offline_queue\status_reports",
        "offline_queue\command_results",
        "offline_queue\error_reports"
    )

    foreach ($subdir in $subdirs) {
        $path = Join-Path $DataPath $subdir
        if (-not (Test-Path $path)) {
            New-Item -Path $path -ItemType Directory -Force | Out-Null
            Write-Host "Đã tạo thư mục: $path"
        }
    }

    # Thiết lập quyền cho thư mục dữ liệu chính (theo phần VIII.3 của tài liệu)
    Write-Host "Thiết lập quyền cho thư mục dữ liệu chính..."
    
    # Xóa các quyền kế thừa cũ trước khi áp dụng quyền mới
    $mainDirAcl = icacls $DataPath /inheritance:r /Q
    Write-Host "Đã xóa quyền kế thừa cho thư mục chính"

    # Cấp quyền Full Control cho SYSTEM với kế thừa
    $mainDirAcl = icacls $DataPath /grant "SYSTEM:(OI)(CI)F" /Q
    Write-Host "Đã cấp quyền Full Control cho SYSTEM trên thư mục chính"

    # Cấp quyền Full Control cho Administrators với kế thừa
    $mainDirAcl = icacls $DataPath /grant "Administrators:(OI)(CI)F" /Q
    Write-Host "Đã cấp quyền Full Control cho Administrators trên thư mục chính"

    # Thiết lập quyền đặc biệt cho thư mục runtime_config (theo phần VIII.3 của tài liệu)
    $runtimeConfigPath = Join-Path $DataPath "runtime_config"
    Write-Host "Thiết lập quyền đặc biệt cho thư mục runtime_config..."
    
    # Xóa quyền kế thừa và sao chép quyền hiện có
    $runtimeConfigAcl = icacls $runtimeConfigPath /inheritance:r /Q
    Write-Host "Đã xóa quyền kế thừa cho thư mục runtime_config"

    # Cấp quyền Full Control cho SYSTEM
    $runtimeConfigAcl = icacls $runtimeConfigPath /grant "SYSTEM:(OI)(CI)F" /Q
    Write-Host "Đã cấp quyền Full Control cho SYSTEM trên thư mục runtime_config"

    # Cấp quyền Read & Execute cho Administrators (giới hạn quyền)
    $runtimeConfigAcl = icacls $runtimeConfigPath /grant "Administrators:(OI)(CI)RX" /Q
    Write-Host "Đã cấp quyền Read & Execute cho Administrators trên thư mục runtime_config"

    Write-Host "Thiết lập quyền thành công!" -ForegroundColor Green
    Write-Host "Cấu trúc thư mục và quyền đã được cấu hình theo yêu cầu bảo mật trong tài liệu CMSAgent" -ForegroundColor Green
}
catch {
    Write-Host "Không thể thiết lập quyền: $_" -ForegroundColor Red
    exit 1
}
